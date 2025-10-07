namespace LocalLlmAssistant.Models;

public class ToolLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "{}";
    public string Result { get; set; } = "{}";
    public bool Success { get; set; } = true;
    public int DurationMs { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}