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
    private readonly ILogger<EmbeddingsClient> _logger;

    public EmbeddingsClient(IHttpClientFactory factory, ILogger<EmbeddingsClient> logger)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(60); // タイムアウト設定
        _logger = logger;
        
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
        
        _logger.LogInformation("EmbeddingsClient initialized with backend: {Backend}, model: {Model}", _backend, _model);
    }

    public async Task<List<double[]>> EmbedAsync(List<string> texts)
    {
        if (texts == null || texts.Count == 0)
        {
            _logger.LogWarning("Empty text list provided to EmbedAsync");
            return new List<double[]>();
        }

        try
        {
            if (_backend == "lmstudio")
            {
                return await EmbedWithLmStudioAsync(texts);
            }
            else
            {
                return await EmbedWithOllamaAsync(texts);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during embedding generation with {Backend}", _backend);
            throw new InvalidOperationException($"Failed to generate embeddings: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during embedding generation");
            throw;
        }
    }

    private async Task<List<double[]>> EmbedWithLmStudioAsync(List<string> texts)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}{_path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { model = _model, input = texts }), 
            Encoding.UTF8, 
            "application/json"
        );
        
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        
        return doc.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("embedding")
                .EnumerateArray()
                .Select(v => v.GetDouble())
                .ToArray())
            .ToList();
    }

    private async Task<List<double[]>> EmbedWithOllamaAsync(List<string> texts)
    {
        var result = new List<double[]>(texts.Count);
        
        foreach (var t in texts)
        {
            if (string.IsNullOrWhiteSpace(t))
            {
                _logger.LogWarning("Skipping empty text in embedding batch");
                result.Add(Array.Empty<double>());
                continue;
            }
            
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_base}{_path}");
            req.Content = new StringContent(
                JsonSerializer.Serialize(new { model = _model, prompt = t }), 
                Encoding.UTF8, 
                "application/json"
            );
            
            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            var arr = doc.RootElement.GetProperty("embedding")
                .EnumerateArray()
                .Select(v => v.GetDouble())
                .ToArray();
            
            result.Add(arr);
        }
        
        return result;
    }

    public async Task<double[]> EmbedAsync(string text) 
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }
        
        var results = await EmbedAsync(new List<string> { text });
        return results.FirstOrDefault() ?? Array.Empty<double>();
    }
}
