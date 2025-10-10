using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LocalLlmAssistant.DTOs;

public class ChainRequest
{
    [JsonPropertyName("query")]
    [Required(ErrorMessage = "Query is required")]
    [StringLength(5000, MinimumLength = 1, ErrorMessage = "Query must be between 1 and 5000 characters")]
    public string Query { get; set; } = "";

    [JsonPropertyName("chain_models")]
    public List<string> ChainModels { get; set; } = new();

    [JsonPropertyName("enable_cot")]
    public bool EnableCoT { get; set; } = true;

    [JsonPropertyName("max_iterations")]
    [Range(1, 10, ErrorMessage = "MaxIterations must be between 1 and 10")]
    public int MaxIterations { get; set; } = 5;

    [JsonPropertyName("context")]
    public Dictionary<string, object>? Context { get; set; }
}

public class ChainResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("iterations")]
    public List<ChainIteration> Iterations { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("execution_time_ms")]
    public long ExecutionTimeMs { get; set; }
}

public class ChainIteration
{
    [JsonPropertyName("iteration")]
    public int Iteration { get; set; }

    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = "";

    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = "";

    [JsonPropertyName("input")]
    public string Input { get; set; } = "";

    [JsonPropertyName("output")]
    public string Output { get; set; } = "";

    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }

    [JsonPropertyName("is_final")]
    public bool IsFinal { get; set; }

    [JsonPropertyName("execution_time_ms")]
    public long ExecutionTimeMs { get; set; }
}