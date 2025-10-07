using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LocalLlmAssistant.Services.Embeddings;

public class EmbeddingsClient
{
    private readonly HttpClient _http;
    private readonly string _backend;
    private readonly string _base;
    private readonly string _path;
    private readonly string _model;
    private readonly string _apiKey;

    public EmbeddingsClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _backend = Environment.GetEnvironmentVariable("EMBEDDINGS_BACKEND") ?? "ollama";
        _model   = Environment.GetEnvironmentVariable("EMBEDDINGS_MODEL") ?? "nomic-embed-text:latest";
        if (_backend == "lmstudio")
        {
            _base = Environment.GetEnvironmentVariable("LMSTUDIO_BASE_URL") ?? "http://localhost:1234/v1";
            _path = "/embeddings";
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "dummy";
        }
        else
        {
            _base = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
            _path = "/api/embeddings";
            _apiKey = "";
        }
    }

    public async Task<List<double[]>> EmbedAsync(List<string> texts)
    {
        if (_backend == "lmstudio")
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}{_path}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(new { model = _model, input = texts }), Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("embedding").EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToList();
        }
        else
        {
            var result = new List<double[]>();
            foreach (var t in texts)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}{_path}");
                req.Content = new StringContent(JsonSerializer.Serialize(new { model = _model, prompt = t }), Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement.GetProperty("embedding").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                result.Add(arr);
            }
            return result;
        }
    }

    public async Task<double[]> EmbedAsync(string text) => (await EmbedAsync(new List<string>{ text }))[0];
}
