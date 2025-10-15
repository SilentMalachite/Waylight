using System.Diagnostics;
using System.Text.Json;
using LocalLlmAssistant.Data;
using LocalLlmAssistant.DTOs;
using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LocalLlmAssistant.Services.Chain;

public class ChainService
{
    private readonly AppDbContext _db;
    private readonly LlmClientResolver _llmResolver;
    private readonly ChainConfig _config;
    private readonly ILogger<ChainService> _logger;

    public ChainService(
        AppDbContext db,
        LlmClientResolver llmResolver,
        IOptions<ChainConfig> config,
        ILogger<ChainService> logger)
    {
        _db = db;
        _llmResolver = llmResolver;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<ChainResponse> ProcessChainAsync(ChainRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var iterations = new List<ChainIteration>();

        try
        {
            _logger.LogInformation("Starting chain processing with {ModelCount} models", request.ChainModels.Count);

            // チェーンモデルを取得
            var chainModels = GetChainModels(request.ChainModels);
            if (!chainModels.Any())
            {
                return new ChainResponse
                {
                    Success = false,
                    Error = "No valid chain models found",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            var currentInput = request.Query;
            var context = request.Context ?? new Dictionary<string, object>();

            for (int i = 0; i < Math.Min(request.MaxIterations, chainModels.Count); i++)
            {
                var model = chainModels[i];
                var iteration = await ProcessIterationAsync(
                    i + 1,
                    model,
                    currentInput,
                    context,
                    request.EnableCoT,
                    i == chainModels.Count - 1 // 最後のイテレーションかどうか
                );

                iterations.Add(iteration);
                currentInput = iteration.Output;

                _logger.LogInformation("Iteration {Iteration} completed with model {ModelId}", i + 1, model.Id);

                // 最終イテレーションの場合は終了
                if (iteration.IsFinal)
                {
                    break;
                }
            }

            stopwatch.Stop();

            return new ChainResponse
            {
                Success = true,
                Result = currentInput,
                Iterations = iterations,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chain processing");
            stopwatch.Stop();

            return new ChainResponse
            {
                Success = false,
                Error = ex.Message,
                Iterations = iterations,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<ChainIteration> ProcessIterationAsync(
        int iterationNumber,
        ChainModel model,
        string input,
        Dictionary<string, object> context,
        bool enableCoT,
        bool isFinal)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // システムプロンプトを構築
            var systemPrompt = BuildSystemPrompt(model, enableCoT, isFinal, context);

            // メッセージを構築
            var messages = new List<Dictionary<string, string>>
            {
                new() { ["role"] = "system", ["content"] = systemPrompt },
                new() { ["role"] = "user", ["content"] = input }
            };

            // LLMクライアントを取得
            var client = _llmResolver.Resolve(model.Backend, model.Model);

            // ストリーミング応答を処理
            var output = "";
            var thinking = "";
            var isThinking = false;

            await foreach (var ev in client.ChatStreamAsync(messages))
            {
                if (ev.Type == "token")
                {
                    var token = ev.Data ?? "";
                    if (isThinking)
                    {
                        thinking += token;
                    }
                    else
                    {
                        output += token;
                    }
                }
            }

            stopwatch.Stop();

            return new ChainIteration
            {
                Iteration = iterationNumber,
                ModelId = model.Id,
                ModelName = model.Name,
                Input = input,
                Output = output,
                Thinking = enableCoT ? thinking : null,
                IsFinal = isFinal,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in iteration {Iteration} with model {ModelId}", iterationNumber, model.Id);
            stopwatch.Stop();

            return new ChainIteration
            {
                Iteration = iterationNumber,
                ModelId = model.Id,
                ModelName = model.Name,
                Input = input,
                Output = $"Error: {ex.Message}",
                IsFinal = isFinal,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private string BuildSystemPrompt(ChainModel model, bool enableCoT, bool isFinal, Dictionary<string, object> context)
    {
        var prompt = model.SystemPrompt;

        if (enableCoT && !isFinal)
        {
            prompt += $"\n\n{_config.CoTInstruction}";
        }

        if (context.Any())
        {
            prompt += $"\n\nコンテキスト情報:\n{JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true })}";
        }

        if (isFinal)
        {
            prompt += "\n\nこれは最終的な回答です。簡潔で明確に回答してください。";
        }

        return prompt;
    }

    private List<ChainModel> GetChainModels(List<string> modelIds)
    {
        var availableModels = _config.Models.Where(m => m.IsEnabled).ToList();

        if (!modelIds.Any())
        {
            return availableModels.Take(2).ToList(); // デフォルトで2つのモデル
        }

        return modelIds
            .Select(id => availableModels.FirstOrDefault(m => m.Id == id))
            .Where(m => m != null)
            .Cast<ChainModel>()
            .ToList();
    }

    public List<ChainModel> GetAvailableModels()
    {
        return _config.Models.Where(m => m.IsEnabled).ToList();
    }
}