using Microsoft.AspNetCore.Mvc;
using SentimentAnalyzer.API.Models;
using SentimentAnalyzer.API.Services;

namespace SentimentAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SentimentController : ControllerBase
{
    private readonly ISentimentService _sentimentService;
    private readonly ILogger<SentimentController> _logger;

    public SentimentController(ISentimentService sentimentService, ILogger<SentimentController> logger)
    {
        _sentimentService = sentimentService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<SentimentResponse>> AnalyzeSentiment([FromBody] SentimentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = "Text cannot be empty" });
        }

        if (request.Text.Length > 5000)
        {
            return BadRequest(new { error = "Text exceeds maximum length of 5000 characters" });
        }

        try
        {
            _logger.LogInformation("Analyzing sentiment for text: {Text}", request.Text[..Math.Min(50, request.Text.Length)]);
            var result = await _sentimentService.AnalyzeSentimentAsync(request.Text);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sentiment analysis");
            return StatusCode(500, new { error = "An error occurred while analyzing sentiment" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
