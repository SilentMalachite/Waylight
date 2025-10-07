namespace LocalLlmAssistant.Models;

public class ChainConfig
{
    public List<ChainModel> Models { get; set; } = new();
    public int MaxIterations { get; set; } = 5;
    public bool EnableCoT { get; set; } = true;
    public string CoTInstruction { get; set; } = "思考過程を段階的に示してください。";
}

public class ChainModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Backend { get; set; } = ""; // "ollama" or "lmstudio"
    public string Model { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2048;
    public bool IsEnabled { get; set; } = true;
}