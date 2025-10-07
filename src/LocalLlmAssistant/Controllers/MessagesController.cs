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
        var userId = HttpContext.User?.Identity?.Name ?? "guest";
        var pref = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (pref == null)
        {
            pref = new UserPreference { UserId = userId, Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen2.5-coder:7b" };
            _db.UserPreferences.Add(pref);
            await _db.SaveChangesAsync();
        }

        var backend = req.Backend ?? pref.Backend.ToString();
        var convo = await _db.Conversations.Where(c => c.UserId == userId).OrderByDescending(c => c.UpdatedAt).FirstOrDefaultAsync()
                    ?? (await _db.Conversations.AddAsync(new Conversation { UserId = userId })).Entity;
        convo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var userKey = $"sse:{userId}";
        var contexts = pref.RagEnabled ? await _rag.SimilarChunksAsync(req.Content) : new List<DocumentChunk>();

        var messages = new List<Dictionary<string, string>>
        {
            new() { ["role"] = "system", ["content"] = pref.SystemPrompt + "\n安全に配慮し、不確実な情報は推測しないでください。" },
            new() { ["role"] = "system", ["content"] = RagFormatter.ToSystemContext(contexts) },
            new() { ["role"] = "user", ["content"] = req.Content }
        };

        // 会話履歴を追加（簡易実装）
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

        var client = _resolver.Resolve(backend, pref.Model ?? "");
        var assistantResponse = "";
        var toolCalls = new List<Dictionary<string, object>>();

        await foreach (var ev in client.ChatStreamAsync(messages, tools: pref.ToolsEnabled ? _toolRegistry.Schema() : null))
        {
            if (ev.Type == "token")
            {
                assistantResponse += ev.Data ?? "";
                await _broker.PublishAsync(userKey, "token", ev.Data ?? "");
            }
            else if (ev.Type == "tool_call")
            {
                var name = ev.Name ?? "unknown";
                var args = ev.Arguments ?? "{}";
                var result = await _toolRunner.CallAsync(userId, name, args);
                
                // ツール呼び出しを記録
                toolCalls.Add(new Dictionary<string, object>
                {
                    ["name"] = name,
                    ["arguments"] = args,
                    ["result"] = result
                });
                
                _log.LogInformation("Tool executed: {Name} with result: {Result}", name, result);
                
                // ツール結果をメッセージに追加して再実行
                messages.Add(new Dictionary<string, string> { ["role"] = "assistant", ["content"] = assistantResponse });
                messages.Add(new Dictionary<string, string> { ["role"] = "tool", ["content"] = result });
                
                // ツール実行後の再実行（簡易実装）
                assistantResponse = "";
                await foreach (var followUpEv in client.ChatStreamAsync(messages, tools: pref.ToolsEnabled ? _toolRegistry.Schema() : null))
                {
                    if (followUpEv.Type == "token")
                    {
                        assistantResponse += followUpEv.Data ?? "";
                        await _broker.PublishAsync(userKey, "token", followUpEv.Data ?? "");
                    }
                    else if (followUpEv.Type == "error")
                    {
                        await _broker.PublishAsync(userKey, "error", JsonSerializer.Serialize(new { error = followUpEv.Data }));
                    }
                }
                break; // ツール実行後は終了
            }
            else if (ev.Type == "error")
            {
                await _broker.PublishAsync(userKey, "error", JsonSerializer.Serialize(new { error = ev.Data }));
            }
        }

        await _broker.PublishAsync(userKey, "done", "{}");

        // ユーザーメッセージを保存
        _db.Messages.Add(new Message { ConversationId = convo.Id, Role = "user", Content = req.Content, Model = pref.Model });
        
        // アシスタントの応答を保存
        if (!string.IsNullOrEmpty(assistantResponse))
        {
            _db.Messages.Add(new Message 
            { 
                ConversationId = convo.Id, 
                Role = "assistant", 
                Content = assistantResponse, 
                Model = pref.Model,
                ToolCallsJson = toolCalls.Count > 0 ? JsonSerializer.Serialize(toolCalls) : null
            });
        }
        
        await _db.SaveChangesAsync();

        return Accepted();
    }
}
