using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Repositories;

namespace Valuation.Api.Controllers
{
    /// <summary>
    /// "Instant AI Value" — proxies the market-value prompt to Gemini so the API key
    /// stays server-side. The Angular client used to call Google directly, which
    /// shipped the key in the browser bundle.
    /// </summary>
    [ApiController]
    [Route("api/market-value")]
    public class MarketValueController : ControllerBase
    {
        private readonly IGeminiRepository _repo;
        private readonly ILogger<MarketValueController> _logger;

        public MarketValueController(IGeminiRepository repo, ILogger<MarketValueController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        /// <summary>
        /// POST: api/market-value
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<MarketValueResponseDto>> Post([FromBody] MarketValueRequestDto request)
        {
            if (request is null)
                return BadRequest(new { message = "Request body is required." });

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(request.Make)) missing.Add(nameof(request.Make));
            if (string.IsNullOrWhiteSpace(request.Model)) missing.Add(nameof(request.Model));
            if (string.IsNullOrWhiteSpace(request.Year)) missing.Add(nameof(request.Year));
            if (string.IsNullOrWhiteSpace(request.Kms)) missing.Add(nameof(request.Kms));
            if (string.IsNullOrWhiteSpace(request.Location)) missing.Add(nameof(request.Location));

            if (missing.Count > 0)
                return BadRequest(new { message = $"Missing required field(s): {string.Join(", ", missing)}." });

            try
            {
                var result = await _repo.GetMarketValueAsync(request);

                if (string.IsNullOrWhiteSpace(result))
                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        message = "No valid response from AI. The model may be busy or the request was filtered. Please try again."
                    });

                return Ok(new MarketValueResponseDto { Result = result });
            }
            catch (InvalidOperationException ex)
            {
                // Gemini key not configured — a deployment problem, not a bad request.
                _logger.LogError(ex, "Market value requested but Gemini is not configured.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "AI valuation is not configured on the server."
                });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Gemini request timed out.");
                return StatusCode(StatusCodes.Status504GatewayTimeout, new
                {
                    message = "The AI server took too long to respond. Please try again."
                });
            }
            catch (HttpRequestException ex)
            {
                // Upstream detail goes to the log, not to the browser — it can echo the key.
                _logger.LogError(ex, "Gemini call failed.");
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "The AI service could not be reached. Please try again."
                });
            }
        }
    }
}
