using LocalLlmAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalLlmAssistant.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ToolLog> ToolLogs => Set<ToolLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>().Property(p => p.ToolCallsJson).HasColumnType("TEXT");
        modelBuilder.Entity<Message>().Property(p => p.ErrorJson).HasColumnType("TEXT");
        modelBuilder.Entity<DocumentChunk>().Property(p => p.Embedding).HasColumnType("TEXT");
        modelBuilder.Entity<Document>().Property(p => p.Visibility).HasDefaultValue("private");
    }
}
