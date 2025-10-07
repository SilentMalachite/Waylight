namespace LocalLlmAssistant.DTOs;

public class ChatRequest
{
    public string Content { get; set; } = "";
    public string? Backend { get; set; }
    public int? ConversationId { get; set; }
}
