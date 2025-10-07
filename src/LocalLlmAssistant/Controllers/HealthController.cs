using Microsoft.AspNetCore.Mvc;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Show() => Ok(new { status = "ok" });
}
