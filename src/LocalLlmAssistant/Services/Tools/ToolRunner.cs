using System.Text.Json;
using LocalLlmAssistant.Data;
using LocalLlmAssistant.Models;

namespace LocalLlmAssistant.Services.Tools;

public class ToolRunner
{
    private readonly AppDbContext _db;
    public ToolRunner(AppDbContext db) { _db = db; }

    public async Task<string> CallAsync(string? userId, string name, string argsJson)
    {
        var started = DateTime.UtcNow;
        var success = true;
        string resultJson;
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson) ?? new();
            Dictionary<string, object?> result = name switch
            {
                "get_time" => new() { ["now"] = DateTimeOffset.Now.ToString("o") },
                "echo" => new() { ["echo"] = TryGetString(args, "text") },
                _ => new() { ["error"] = $"unknown tool: {name}" }
            };
            success = !result.ContainsKey("error");
            resultJson = JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            success = false;
            resultJson = JsonSerializer.Serialize(new { error = ex.Message });
        }
        var ms = (int)(DateTime.UtcNow - started).TotalMilliseconds;
        _db.ToolLogs.Add(new ToolLog { UserId = userId, Name = name, Arguments = argsJson, Result = resultJson, Success = success, DurationMs = ms });
        await _db.SaveChangesAsync();
        return resultJson;
    }

    private static string TryGetString(IReadOnlyDictionary<string, JsonElement> args, string key)
    {
        if (!args.TryGetValue(key, out var element)) return "";
        if (element.ValueKind == JsonValueKind.Null) return "";
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? "";
        return element.ToString();
    }
}
