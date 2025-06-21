using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/valuations/{valuationId:guid}/vehicledetails")]
    public class VehicleDetailsController : ControllerBase
    {
        private readonly IValuationService _svc;
        public VehicleDetailsController(IValuationService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<VehicleDetailsDto>> Get(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var dto = await _svc.GetVehicleDetailsAsync(
                valuationId.ToString(), vehicleNumber, applicantContact);
            if (dto == null)
                return Ok(new VehicleDetailsDto());
            return Ok(dto);
        }

        /// <summary>
        /// Get the stored vehicle details AND enrich them with Attestr API RC data.
        /// </summary>
        [HttpGet("with-rc")]
        public async Task<ActionResult<VehicleDetailsDto>> GetWithRc(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var dto = await _svc.GetVehicleDetailsWithRcCheckAsync(
                valuationId.ToString(), vehicleNumber, applicantContact);
            if (dto is null)
                return NotFound();
            return Ok(dto);
        }

        // PUT /api/valuations/{valuationId}/vehicledetails  (multipart/form-data)
        [HttpPut]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upsert(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromForm] VehicleDetailsDto dto,
            [FromQuery] string applicantContact)
        {
            // VehicleDetailsDto now contains all scalar props + IFormFile fields + ApplicantContact
            await _svc.UpdateVehicleDetailsAsync(
                valuationId.ToString(), dto, vehicleNumber, applicantContact);
            return NoContent();
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            await _svc.DeleteVehicleDetailsAsync(
                valuationId.ToString(), vehicleNumber, applicantContact);
            return NoContent();
        }
    }

    // New controller for /api/valuations/open
    [ApiController]
    [Route("api/valuations")]
    public class ValuationsController : ControllerBase
    {
        private readonly IValuationService _svc;
        public ValuationsController(IValuationService svc) => _svc = svc;

        // GET /api/valuations/open
        [HttpGet("open")]
        public async Task<ActionResult<IEnumerable<OpenValuationDto>>> GetAllOpen()
        {
            var openValuations = await _svc.GetOpenValuationsAsync();
            if (openValuations == null || !openValuations.Any()) return NotFound();
            return Ok(openValuations);
        }
    }
}
