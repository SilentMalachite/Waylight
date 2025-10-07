namespace LocalLlmAssistant.Models;

public class Conversation
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? Title { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<Message> Messages { get; set; } = new();
}