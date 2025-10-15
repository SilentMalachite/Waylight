using LocalLlmAssistant.SSE;
using Microsoft.AspNetCore.Mvc;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly StreamBroker _broker;
    private readonly ILogger<StreamController> _logger;

    public StreamController(StreamBroker broker, ILogger<StreamController> logger)
    {
        _broker = broker;
        _logger = logger;
    }

    [HttpGet]
    public async Task Get()
    {
        var sessionId = GetOrCreateSessionId();
        var userId = HttpContext.User?.Identity?.Name ?? "guest";
        var key = $"sse:{sessionId}:{userId}";

        try
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            _logger.LogInformation("SSE stream started for user: {UserId}", userId);

            var reader = _broker.Subscribe(key);

            await foreach (var packet in reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                await Response.WriteAsync($"event: {packet.Event}\n");
                await Response.WriteAsync($"data: {packet.Data}\n\n");
                await Response.Body.FlushAsync();
            }

            _logger.LogInformation("SSE stream ended for user: {UserId}", userId);
        }
        catch (OperationCanceledException)
        {
            // クライアント切断は正常な動作
            _logger.LogInformation("SSE stream cancelled (client disconnected)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SSE stream");
            throw;
        }
    }

    private string GetOrCreateSessionId()
    {
        const string cookieName = "waylight-session";
        if (!Request.Cookies.TryGetValue(cookieName, out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            Response.Cookies.Append(cookieName, sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                MaxAge = TimeSpan.FromDays(30)
            });
        }

        return sessionId;
    }
}
