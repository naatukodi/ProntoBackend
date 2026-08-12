using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    /// <summary>
    /// MIS report — one Cosmos query returning all 24 columns of MIS FORMAT.xlsx.
    /// Access is gated admin-only in the Angular client (RoleGuard); the rest of
    /// this API is likewise unauthenticated at the endpoint level.
    /// </summary>
    [ApiController]
    [Route("api/valuations/mis")]
    public class MisController : ControllerBase
    {
        private readonly IMisService _svc;

        public MisController(IMisService svc) => _svc = svc;

        /// <summary>
        /// GET: api/valuations/mis?from=&to=&status=&client=&state=
        /// Dates are inclusive; from/to bound the LEAD CREATION date.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<MisRowDto>>> GetMis(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? status,
            [FromQuery] string? client,
            [FromQuery] string? state)
        {
            var rows = await _svc.GetMisAsync(from, to, status, client, state);
            return Ok(rows);
        }
    }
}
