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
            try
            {
                var dto = await _svc.GetVehicleDetailsAsync(
                    valuationId.ToString(), vehicleNumber, applicantContact);
                if (dto == null)
                    return Ok(new VehicleDetailsDto());
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, type = ex.GetType().Name });
            }
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

        [HttpPost("assignment")]
        public async Task<IActionResult> UpdateAssignment(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            [FromQuery] string? assignedTo = null,
            [FromQuery] string? assignedToPhoneNumber = null,
            [FromQuery] string? assignedToEmail = null,
            [FromQuery] string? assignedToWhatsapp = null)
        {
            await _svc.UpdateAssignmentAsync(valuationId.ToString(), vehicleNumber, applicantContact, assignedTo, assignedToPhoneNumber, assignedToEmail, assignedToWhatsapp);
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

    // New controller for /api/valuations routes (open valuations and duplicate check)
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
            if (openValuations == null || !openValuations.Any()) 
                return NotFound();
            return Ok(openValuations);
        }

        /// <summary>
        /// Check if vehicle number, engine number, or chassis number already exists in the system
        /// Used before registering a new case to prevent duplicates
        /// </summary>
        /// <param name="vehicleNumber">Vehicle registration number</param>
        /// <param name="engineNumber">Engine number</param>
        /// <param name="chassisNumber">Chassis number (VIN)</param>
        /// <returns>Duplicate check response with existing records</returns>
        [HttpGet("check-duplicate")]
        public async Task<ActionResult<VehicleDuplicateCheckResponse>> CheckDuplicateVehicle(
            [FromQuery] string? vehicleNumber = null,
            [FromQuery] string? engineNumber = null,
            [FromQuery] string? chassisNumber = null)
        {
            try
            {
                // Validate at least one parameter is provided
                if (string.IsNullOrWhiteSpace(vehicleNumber) &&
                    string.IsNullOrWhiteSpace(engineNumber) &&
                    string.IsNullOrWhiteSpace(chassisNumber))
                {
                    return BadRequest(new
                    {
                        message = "At least one parameter (vehicleNumber, engineNumber, or chassisNumber) must be provided"
                    });
                }

                var response = await _svc.CheckDuplicateVehicleAsync(
                    vehicleNumber, engineNumber, chassisNumber);

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while checking for duplicates",
                    error = ex.Message
                });
            }
        }
    }
}
