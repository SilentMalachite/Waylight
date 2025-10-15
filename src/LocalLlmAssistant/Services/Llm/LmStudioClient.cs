using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using LocalLlmAssistant.Models;
using Microsoft.Extensions.Options;

namespace LocalLlmAssistant.Services.Llm;

public class LmStudioClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _base;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly ILogger<LmStudioClient> _logger;

    public LmStudioClient(IHttpClientFactory factory, IOptions<LlmConfig> config, string model, ILogger<LmStudioClient> logger)
    {
        _http = factory.CreateClient();
        _base = Environment.GetEnvironmentVariable("LMSTUDIO_BASE_URL") ?? config.Value.LmStudioBaseUrl;
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "dummy";
        _model = model;
        _logger = logger;
    }

    public async IAsyncEnumerable<LlmStreamEvent> ChatStreamAsync(List<Dictionary<string, string>> messages, object? tools = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["stream"] = true,
            ["messages"] = messages
        };
        if (tools != null) { body["tools"] = tools; body["tool_choice"] = "auto"; }

        var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var sr = new StreamReader(stream);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            if (!line.StartsWith("data:")) continue;
            var payload = line[5..].Trim();
            if (payload == "[DONE]") break;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(payload);
            }
            catch (JsonException ex)
            {
                // 不正なJSONをスキップ（ストリーミング中は正常な動作）
                _logger.LogDebug(ex, "Skipping malformed JSON in stream");
                continue;
            }

            using (doc)
            {
                var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                if (choice.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    yield return new LlmStreamEvent("token", c.GetString());
                }
                if (choice.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array && tcs.GetArrayLength() > 0)
                {
                    var f = tcs[0].GetProperty("function");
                    yield return new LlmStreamEvent("tool_call", Name: f.GetProperty("name").GetString(), Arguments: f.GetProperty("arguments").GetString());
                }
            }
        }
    }
}
