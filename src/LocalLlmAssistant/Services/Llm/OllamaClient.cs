using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using LocalLlmAssistant.Models;
using Microsoft.Extensions.Options;

namespace LocalLlmAssistant.Services.Llm;

public class OllamaClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _base;
    private readonly string _model;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(IHttpClientFactory factory, IOptions<LlmConfig> config, string model, ILogger<OllamaClient> logger)
    {
        _http = factory.CreateClient();
        _base = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? config.Value.OllamaBaseUrl;
        _model = model;
        _logger = logger;
    }

    public async IAsyncEnumerable<LlmStreamEvent> ChatStreamAsync(List<Dictionary<string, string>> messages, object? tools = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new { model = _model, stream = true, messages = messages, tools = tools };
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}/api/chat");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var sr = new StreamReader(stream);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                // 不正なJSONをスキップ（ストリーミング中は正常な動作）
                _logger.LogDebug(ex, "Skipping malformed JSON in stream");
                continue;
            }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("message", out var msg))
                {
                    if (msg.TryGetProperty("content", out var content))
                        yield return new LlmStreamEvent("token", content.GetString());
                    if (msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array && tc.GetArrayLength() > 0)
                    {
                        var f = tc[0].GetProperty("function");
                        yield return new LlmStreamEvent("tool_call", Name: f.GetProperty("name").GetString(), Arguments: f.GetProperty("arguments").GetString());
                    }
                }
            }
        }
    }
}
