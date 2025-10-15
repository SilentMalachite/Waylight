using LocalLlmAssistant.DTOs;
using LocalLlmAssistant.Services.Chain;
using Microsoft.AspNetCore.Mvc;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChainController : ControllerBase
{
    private readonly ChainService _chainService;
    private readonly ILogger<ChainController> _logger;

    public ChainController(ChainService chainService, ILogger<ChainController> logger)
    {
        _chainService = chainService;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessChain([FromBody] ChainRequest request)
    {
        try
        {
            _logger.LogInformation("Processing chain request with {ModelCount} models", request.ChainModels.Count);

            var response = await _chainService.ProcessChainAsync(request);

            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chain request");
            return StatusCode(500, new ChainResponse
            {
                Success = false,
                Error = "Internal server error",
                ExecutionTimeMs = 0
            });
        }
    }

    [HttpGet("models")]
    public IActionResult GetAvailableModels()
    {
        try
        {
            var models = _chainService.GetAvailableModels();
            return Ok(new { models });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models");
            return StatusCode(500, new { error = "Failed to get available models" });
        }
    }

    [HttpPost("stream")]
    public async Task ProcessChainStream([FromBody] ChainRequest request)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            _logger.LogInformation("Processing chain stream request with {ModelCount} models", request.ChainModels.Count);

            var response = await _chainService.ProcessChainAsync(request);

            // 各イテレーションをストリーミング
            foreach (var iteration in response.Iterations)
            {
                var iterationData = new
                {
                    type = "iteration",
                    iteration = iteration.Iteration,
                    model_id = iteration.ModelId,
                    model_name = iteration.ModelName,
                    input = iteration.Input,
                    output = iteration.Output,
                    thinking = iteration.Thinking,
                    is_final = iteration.IsFinal,
                    execution_time_ms = iteration.ExecutionTimeMs
                };

                await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(iterationData)}\n\n");
                await Response.Body.FlushAsync();
            }

            // 最終結果を送信
            var finalData = new
            {
                type = "final",
                success = response.Success,
                result = response.Result,
                error = response.Error,
                execution_time_ms = response.ExecutionTimeMs
            };

            await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(finalData)}\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chain stream processing");

            var errorData = new
            {
                type = "error",
                error = ex.Message
            };

            await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(errorData)}\n\n");
            await Response.Body.FlushAsync();
        }
    }
}
