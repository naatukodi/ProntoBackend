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
            if (dto == null) return NotFound();
            return Ok(dto);
        }

        // PUT /api/valuations/{valuationId}/vehicledetails  (multipart/form-data)
        [HttpPut]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upsert(
            Guid valuationId,
            [FromForm] VehicleDetailsDto dto,
            [FromQuery] string applicantContact)
        {
            // VehicleDetailsDto now contains all scalar props + IFormFile fields + ApplicantContact
            await _svc.UpdateVehicleDetailsAsync(
                valuationId.ToString(), dto, applicantContact);
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
}
