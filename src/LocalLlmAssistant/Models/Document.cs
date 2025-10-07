namespace LocalLlmAssistant.Models;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? UserId { get; set; }
    public string Visibility { get; set; } = "private";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentChunk> Chunks { get; set; } = new();
}