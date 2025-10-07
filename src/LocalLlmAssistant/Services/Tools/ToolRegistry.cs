namespace LocalLlmAssistant.Services.Tools;

public record ToolFunction(string Name, string Description, string ParametersJson);

public class ToolRegistry
{
    public object Schema() => new object[]
    {
        new {
            type = "function",
            function = new {
                name = "get_time",
                description = "現在時刻を返す",
                parameters = new { type = "object", properties = new { } }
            }
        },
        new {
            type = "function",
            function = new {
                name = "echo",
                description = "引数をそのまま返す",
                parameters = new { type = "object", properties = new { text = new { type = "string" } }, required = new []{ "text" } }
            }
        }
    };
}
