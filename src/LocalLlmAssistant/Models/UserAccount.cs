namespace LocalLlmAssistant.Models;

public class UserAccount
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

