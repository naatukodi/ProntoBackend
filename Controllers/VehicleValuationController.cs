using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/valuations/{id:guid}/valuation")]
    public class VehicleValuationController : ControllerBase
    {
        private readonly IVehicleValuationService _svc;
        public VehicleValuationController(IVehicleValuationService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<VehicleValuation>> Get(
            Guid id,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            [FromQuery] bool force = false,
            CancellationToken ct = default)
        {
            var result = await _svc.EnsureAsync(
                id.ToString(),
                vehicleNumber,
                applicantContact,
                force,
                ct);

            if (result == null) return NotFound();
            return Ok(result);
        }

        
    }
}
