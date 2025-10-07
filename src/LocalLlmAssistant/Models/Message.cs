namespace LocalLlmAssistant.Models;

public class Message
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = default!;
    public string Role { get; set; } = "user"; // user | assistant | system
    public string? Content { get; set; }
    public string? Model { get; set; }
    public string? PromptVersion { get; set; }
    public string? ToolCallsJson { get; set; } = "{}";
    public string? ErrorJson { get; set; } = "{}";
    public int TokenCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}