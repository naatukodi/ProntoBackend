using System;
using System.Threading.Tasks;
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
            [FromQuery] string applicantContact)
        {
            var result = await _svc.GetVehicleValuationAsync(
                id.ToString(),
                vehicleNumber,
                applicantContact);

            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
