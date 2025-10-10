using System.ComponentModel.DataAnnotations;

namespace LocalLlmAssistant.DTOs;

public class ChatRequest
{
    [Required(ErrorMessage = "Content is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 10000 characters")]
    public string Content { get; set; } = "";
    
    public string? Backend { get; set; }
    
    public int? ConversationId { get; set; }
}
