using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/valuations/{valuationId:guid}/photos")]
    public class VehiclePhotosController : ControllerBase
    {
        private readonly IVehiclePhotoService _photoService;

        public VehiclePhotosController(IVehiclePhotoService photoService)
        {
            _photoService = photoService;
        }

        [HttpPut]
        [RequestSizeLimit(100 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
        public async Task<IActionResult> UpdatePhotos(
            Guid valuationId,
            [FromForm] VehiclePhotosDto dto)
        {
            dto.ValuationId = valuationId.ToString();
            var resultMap = await _photoService.UpdatePhotosAsync(dto);
            return Ok(resultMap);
        }

        [HttpGet]
        public async Task<ActionResult<Dictionary<string, string>>> GetPhotoUrls(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var map = await _photoService.GetPhotoUrlsAsync(
                valuationId.ToString(),
                vehicleNumber,
                applicantContact);

            if (map == null) return NotFound();
            return Ok(map);
        }
        
        // ✅ NEW: Endpoint for the PDF generator to retrieve dynamic custom images
        [HttpGet("custom")]
        public async Task<ActionResult<List<SavedCustomPhoto>>> GetCustomPhotos(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var customPhotos = await _photoService.GetCustomPhotosAsync(
                valuationId.ToString(), 
                vehicleNumber, 
                applicantContact);

            return Ok(customPhotos);
        }

        [HttpGet("validate")]
        public async Task<ActionResult<ValidatePhotosResponse>> ValidateMandatoryPhotos(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            try
            {
                var photoUrls = await _photoService.GetPhotoUrlsAsync(valuationId.ToString(), vehicleNumber, applicantContact);
                var videoUrls = await _photoService.GetVideoUrlsAsync(valuationId.ToString(), vehicleNumber, applicantContact);

                var mandatoryPhotoFields = new List<string>
                {
                    "FrontLeftSide", "FrontRightSide", "RearLeftSide", "RearRightSide",
                    "FrontViewGrille", "RearViewTailgate", "DriverSideProfile", "PassengerSideProfile",
                    "EngineBay", "VinPlate", "ChassisImprint", "Odometer",
                    "SelfieWithVehicle", "VehicleVideo",
                    "ChassisVerification", "ChassisStencilTrace", "WorkingOperationPhoto"
                };

                var photoDisplayNames = new Dictionary<string, string>
                {
                    { "FrontLeftSide", "Front Left Side" }, { "FrontRightSide", "Front Right Side" },
                    { "RearLeftSide", "Rear Left Side" }, { "RearRightSide", "Rear Right Side" },
                    { "FrontViewGrille", "Front View (Grille)" }, { "RearViewTailgate", "Rear View (Tailgate)" },
                    { "DriverSideProfile", "Driver's Side Profile" }, { "PassengerSideProfile", "Passenger Side Profile" },
                    { "Dashboard", "Dashboard" }, { "InstrumentCluster", "Instrument Cluster" },
                    { "EngineBay", "Engine Bay" }, { "VinPlate", "VIN Plate" },
                    { "ChassisImprint", "Chassis Imprint" },
                    { "GearInterior", "Gear (Interior)" }, { "FrontSeat", "Front Seat" }, { "RearSeat", "Rear Seat" },
                    { "DashboardCloseup", "Dashboard Close-up" }, { "Odometer", "Odometer" },
                    { "SelfieWithVehicle", "Selfie with Vehicle" },
                    { "TireFrontLeft", "Tire Front Left" }, { "TireFrontRight", "Tire Front Right" },
                    { "TireRearLeft", "Tire Rear Left" }, { "TireRearRight", "Tire Rear Right" },
                    { "Underbody", "Under Body" },
                    { "ChassisVerification", "Chassis Verification" }, { "ChassisStencilTrace", "Chassis Stencil Trace" },
                    { "WorkingOperationPhoto", "Working / Operation Photo" }
                };

                var normalizedPhotoUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (photoUrls != null) foreach (var kvp in photoUrls) normalizedPhotoUrls[kvp.Key] = kvp.Value;

                var normalizedVideoUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (videoUrls != null) foreach (var kvp in videoUrls) normalizedVideoUrls[kvp.Key] = kvp.Value;

                var missingPhotos = new List<string>();
                foreach (var field in mandatoryPhotoFields)
                {
                    if (!normalizedPhotoUrls.ContainsKey(field) || string.IsNullOrWhiteSpace(normalizedPhotoUrls[field]))
                    {
                        var displayName = photoDisplayNames.ContainsKey(field) ? photoDisplayNames[field] : field;
                        missingPhotos.Add(displayName);
                    }
                }

                bool videoExists = normalizedVideoUrls.ContainsKey("VehicleVideo") && !string.IsNullOrWhiteSpace(normalizedVideoUrls["VehicleVideo"]);
                if (!videoExists) missingPhotos.Add("Vehicle Video");

                var response = new ValidatePhotosResponse
                {
                    IsComplete = missingPhotos.Count == 0,
                    MissingPhotos = missingPhotos
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeletePhotos(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            await _photoService.DeletePhotosAsync(valuationId.ToString(), vehicleNumber, applicantContact);
            return NoContent();
        }

        // ✅ UPDATED: Requires vehicleNumber and applicantContact for efficient DB routing
        [HttpPut("{photoType}/metadata")]
        public async Task<IActionResult> UpdateMetadata(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            string photoType,
            [FromBody] PhotoMetadataUpdateDto input)
        {
            try
            {
                var result = await _photoService.UpdatePhotoMetadataAsync(valuationId.ToString(), vehicleNumber, applicantContact, photoType, input);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ✅ UPDATED: Requires vehicleNumber and applicantContact for efficient DB routing
        [HttpGet("metadata")]
        public async Task<IActionResult> GetMetadata(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var result = await _photoService.GetPhotoMetadataAsync(valuationId.ToString(), vehicleNumber, applicantContact);
            return Ok(result);
        }
    }
}