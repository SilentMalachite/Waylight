using System.Text.Json;
using LocalLlmAssistant.Data;
using LocalLlmAssistant.DTOs;
using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services.Llm;
using LocalLlmAssistant.Services.Rag;
using LocalLlmAssistant.Services.Tools;
using LocalLlmAssistant.SSE;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LlmClientResolver _resolver;
    private readonly RagRetriever _rag;
    private readonly ToolRegistry _toolRegistry;
    private readonly ToolRunner _toolRunner;
    private readonly StreamBroker _broker;
    private readonly ILogger<MessagesController> _log;

    public MessagesController(AppDbContext db, LlmClientResolver resolver, RagRetriever rag, ToolRegistry toolRegistry, ToolRunner toolRunner, StreamBroker broker, ILogger<MessagesController> log)
    {
        _db = db; _resolver = resolver; _rag = rag; _toolRegistry = toolRegistry; _toolRunner = toolRunner; _broker = broker; _log = log;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChatRequest req)
    {
        // 入力検証
        if (string.IsNullOrWhiteSpace(req.Content))
        {
            return BadRequest(new { error = "Content is required" });
        }

        if (req.Content.Length > 10000)
        {
            return BadRequest(new { error = "Content exceeds maximum length of 10000 characters" });
        }

        try
        {
            var userId = HttpContext.User?.Identity?.Name ?? "guest";
            var pref = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
            if (pref == null)
            {
                pref = new UserPreference { UserId = userId, Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen2.5-coder:7b" };
                _db.UserPreferences.Add(pref);
                await _db.SaveChangesAsync();
            }

            var backend = req.Backend ?? pref.Backend.ToString().ToLowerInvariant();
            var convo = await _db.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();

            if (convo == null)
            {
                convo = new Conversation { UserId = userId };
                _db.Conversations.Add(convo);
                await _db.SaveChangesAsync();
            }

            convo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var userKey = $"sse:{userId}";
            var contexts = pref.RagEnabled ? await _rag.SimilarChunksAsync(req.Content) : new List<DocumentChunk>();

            var messages = new List<Dictionary<string, string>>
            {
                new() { ["role"] = "system", ["content"] = pref.SystemPrompt + "\n安全に配慮し、不確実な情報は推測しないでください。" }
            };

            // RAGコンテキストを追加（空でない場合のみ）
            var ragContext = RagFormatter.ToSystemContext(contexts);
            if (!string.IsNullOrWhiteSpace(ragContext))
            {
                messages.Add(new() { ["role"] = "system", ["content"] = ragContext });
            }

            // 会話履歴を追加（最新10件）
            var recentMessages = await _db.Messages
                .Where(m => m.ConversationId == convo.Id)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { m.Role, m.Content })
                .ToListAsync();

            foreach (var msg in recentMessages)
            {
                messages.Add(new Dictionary<string, string> { ["role"] = msg.Role, ["content"] = msg.Content ?? "" });
            }

            // 現在のユーザーメッセージを追加
            messages.Add(new() { ["role"] = "user", ["content"] = req.Content });

            var client = _resolver.Resolve(backend, pref.Model ?? "qwen2.5-coder:7b");
            var assistantResponse = "";
            var toolCalls = new List<Dictionary<string, object>>();
            const int maxToolIterations = 3; // 無限ループ防止
            var toolIterationCount = 0;

            while (toolIterationCount < maxToolIterations)
            {
                var hasToolCall = false;

                await foreach (var ev in client.ChatStreamAsync(messages, tools: pref.ToolsEnabled ? _toolRegistry.Schema() : null))
                {
                    if (ev.Type == "token")
                    {
                        assistantResponse += ev.Data ?? "";
                        await _broker.PublishAsync(userKey, "token", ev.Data ?? "");
                    }
                    else if (ev.Type == "tool_call")
                    {
                        hasToolCall = true;
                        var name = ev.Name ?? "unknown";
                        var args = ev.Arguments ?? "{}";

                        _log.LogInformation("Executing tool: {Name} with args: {Args}", name, args);
                        var result = await _toolRunner.CallAsync(userId, name, args);

                        // ツール呼び出しを記録
                        toolCalls.Add(new Dictionary<string, object>
                        {
                            ["name"] = name,
                            ["arguments"] = args,
                            ["result"] = result
                        });

                        _log.LogInformation("Tool executed: {Name} with result: {Result}", name, result);

                        // ツール結果をメッセージに追加
                        if (!string.IsNullOrEmpty(assistantResponse))
                        {
                            messages.Add(new Dictionary<string, string> { ["role"] = "assistant", ["content"] = assistantResponse });
                        }
                        messages.Add(new Dictionary<string, string> { ["role"] = "tool", ["content"] = $"Tool: {name}\nResult: {result}" });

                        assistantResponse = ""; // リセット
                        toolIterationCount++;
                        break; // 現在のストリームを終了し、再試行
                    }
                    else if (ev.Type == "error")
                    {
                        _log.LogError("LLM stream error: {Error}", ev.Data);
                        await _broker.PublishAsync(userKey, "error", JsonSerializer.Serialize(new { error = ev.Data }));
                    }
                }

                // ツール呼び出しがなければループを終了
                if (!hasToolCall)
                {
                    break;
                }
            }

            await _broker.PublishAsync(userKey, "done", "{}");

            // ユーザーメッセージを保存
            _db.Messages.Add(new Message
            {
                ConversationId = convo.Id,
                Role = "user",
                Content = req.Content,
                Model = pref.Model,
                TokenCount = EstimateTokenCount(req.Content)
            });

            // アシスタントの応答を保存
            if (!string.IsNullOrEmpty(assistantResponse))
            {
                _db.Messages.Add(new Message
                {
                    ConversationId = convo.Id,
                    Role = "assistant",
                    Content = assistantResponse,
                    Model = pref.Model,
                    ToolCallsJson = toolCalls.Count > 0 ? JsonSerializer.Serialize(toolCalls) : null,
                    TokenCount = EstimateTokenCount(assistantResponse)
                });
            }

            await _db.SaveChangesAsync();

            return Accepted();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    private static int EstimateTokenCount(string text)
    {
        // 簡易的なトークン数推定（実際にはより正確なトークナイザーを使用すべき）
        return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
    }
}
