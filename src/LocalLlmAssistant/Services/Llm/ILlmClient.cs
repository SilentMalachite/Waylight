using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace LocalLlmAssistant.Services.Llm;

public record LlmStreamEvent(string Type, string? Data = null, string? Name = null, string? Arguments = null);

public class LlmClientResolver
{
    private readonly IServiceProvider _sp;
    public LlmClientResolver(IServiceProvider sp) { _sp = sp; }

    public ILlmClient Resolve(string backend, string model)
    {
        return backend switch
        {
            "lmstudio" => ActivatorUtilities.CreateInstance<LmStudioClient>(_sp, model),
            _ => ActivatorUtilities.CreateInstance<OllamaClient>(_sp, model)
        };
    }
}

public interface ILlmClient
{
    IAsyncEnumerable<LlmStreamEvent> ChatStreamAsync(List<Dictionary<string,string>> messages, object? tools = null, CancellationToken ct = default);
}

