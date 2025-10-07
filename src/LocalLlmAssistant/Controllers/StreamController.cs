using LocalLlmAssistant.SSE;
using Microsoft.AspNetCore.Mvc;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly StreamBroker _broker;
    public StreamController(StreamBroker broker) { _broker = broker; }

    [HttpGet]
    public async Task Get()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        var userId = HttpContext.User?.Identity?.Name ?? "guest";
        var key = $"sse:{userId}";
        var reader = _broker.Subscribe(key);

        await foreach (var packet in reader.ReadAllAsync(HttpContext.RequestAborted))
        {
            await Response.WriteAsync($"event: {packet.Event}\n");
            await Response.WriteAsync($"data: {packet.Data}\n\n");
            await Response.Body.FlushAsync();
        }
    }
}
