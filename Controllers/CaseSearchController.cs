using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Services;
using Valuation.Api.Services.Interfaces;

namespace Valuation.Api.Controllers
{
    /// <summary>
    /// Case lookup by reference, vehicle number, chassis number or engine number.
    /// Reaches closed and archived cases the dashboard no longer lists.
    /// </summary>
    [ApiController]
    [Route("api/valuations/search")]
    public class CaseSearchController : ControllerBase
    {
        private readonly ICaseSearchService _search;

        public CaseSearchController(ICaseSearchService search) => _search = search;

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
        {
            var term = (q ?? "").Trim();
            if (term.Length < CaseSearchService.MinimumQueryLength)
                return BadRequest(new
                {
                    message = $"Enter at least {CaseSearchService.MinimumQueryLength} characters to search."
                });

            return Ok(await _search.SearchAsync(term, ct));
        }
    }
}
