namespace LocalLlmAssistant.Models;

public enum Backend { ollama = 0, lmstudio = 1 }

public class UserPreference
{
    public int Id { get; set; }
    public string? UserId { get; set; }  // 認証導入時に置換
    public Backend Backend { get; set; } = Backend.ollama;
    public string? Model { get; set; }
    public string SystemPrompt { get; set; } = "あなたはやさしく具体的で、根拠を示すアシスタントです。";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 1024;
    public bool ToolsEnabled { get; set; } = true;
    public bool RagEnabled { get; set; } = true;
    public bool AccessibilityMode { get; set; } = false;
}