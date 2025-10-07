namespace LocalLlmAssistant.Models;

public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document Document { get; set; } = default!;
    public string Text { get; set; } = "";
    public string? Embedding { get; set; } // JSON of double[]
    public string? SourcePath { get; set; }
    public int? SpanStart { get; set; }
    public int? SpanEnd { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}