// src/Valuation.Api/Controllers/VehiclePhotosController.cs
using Microsoft.AspNetCore.Mvc;
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

        /// <summary>
        /// PUT /api/valuations/{valuationId}/photos
        /// Accepts up to 19 IFormFile fields and updates Cosmos.
        /// </summary>
        [HttpPut]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UpdatePhotos(
            Guid valuationId,
            [FromForm] VehiclePhotosDto dto)
        {
            // Ensure route and DTO match
            dto.ValuationId = valuationId.ToString();
            var resultMap = await _photoService.UpdatePhotosAsync(dto);
            return Ok(resultMap);
        }

        /// <summary>
        /// GET  /api/valuations/{valuationId}/photos?vehicleNumber=…&applicantContact=…
        /// Returns the existing PhotoUrls dictionary or 404 if none.
        /// </summary>
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
            if (map == null)
                return NotFound();
            return Ok(map);
        }

        /// <summary>
        /// ✅ NEW ENDPOINT: Validate mandatory photos
        /// GET /api/valuations/{valuationId}/photos/validate?vehicleNumber=…&applicantContact=…
        /// Returns { isComplete: bool, missingPhotos: string[] }
        /// </summary>

        [HttpGet("validate")]
        public async Task<ActionResult<ValidatePhotosResponse>> ValidateMandatoryPhotos(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
            {
                try
                {
                    // Get existing photos
                    var photoUrls = await _photoService.GetPhotoUrlsAsync(
                        valuationId.ToString(),
                        vehicleNumber,
                        applicantContact);
                    
                    // Define mandatory photo fields (18 required)
                    var mandatoryPhotoFields = new List<string>
                    {
                        "FrontLeftSide",
                        "FrontRightSide",
                        "RearLeftSide",
                        "RearRightSide",
                        "FrontViewGrille",
                        "RearViewTailgate",
                        "DriverSideProfile",
                        "PassengerSideProfile",
                        "Dashboard",
                        "InstrumentCluster",
                        "EngineBay",
                        "ChassisNumberPlate",
                        "ChassisImprint",
                        "GearAndSeats",
                        "DashboardCloseup",
                        "Odometer",
                        "SelfieWithVehicle",
                        "TiresAndRims"
                        // Note: "Underbody" is optional, NOT included
                    };
                    
                    // Display names for user-friendly error messages
                    var photoDisplayNames = new Dictionary<string, string>
                    {
                        { "FrontLeftSide", "Front Left Side" },
                        { "FrontRightSide", "Front Right Side" },
                        { "RearLeftSide", "Rear Left Side" },
                        { "RearRightSide", "Rear Right Side" },
                        { "FrontViewGrille", "Front View (grille)" },
                        { "RearViewTailgate", "Rear View (tailgate)" },
                        { "DriverSideProfile", "Driver's Side Profile" },
                        { "PassengerSideProfile", "Passenger Side Profile" },
                        { "Dashboard", "Dashboard" },
                        { "InstrumentCluster", "Instrument Cluster" },
                        { "EngineBay", "Engine Bay" },
                        { "ChassisNumberPlate", "Chassis Number Plate" },
                        { "ChassisImprint", "Chassis Imprint" },
                        { "GearAndSeats", "Gear and Seats" },
                        { "DashboardCloseup", "Dashboard Close-up" },
                        { "Odometer", "Odometer" },
                        { "SelfieWithVehicle", "Selfie with Vehicle" },
                        { "TiresAndRims", "Tires and Rims" }
                    };
                    // Normalize keys for case-insensitive comparison
                    var normalizedPhotoUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (photoUrls != null)
                    {
                        foreach (var kvp in photoUrls)
                        {
                            normalizedPhotoUrls[kvp.Key] = kvp.Value;
                            }
                    }

                    // Check each mandatory field
                    var missingPhotos = new List<string>();
                    foreach (var field in mandatoryPhotoFields)
                    {
                        // Check if photo exists and has a valid URL
                        if (!normalizedPhotoUrls.ContainsKey(field) || 
                        string.IsNullOrWhiteSpace(normalizedPhotoUrls[field]))
                        {
                            var displayName = photoDisplayNames.ContainsKey(field) 
                            ? photoDisplayNames[field] 
                            : field;
                            missingPhotos.Add(displayName);
                        }
                    }
                    
                    // Return response
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


        /// <summary>
        /// DELETE /api/valuations/{valuationId}/photos?vehicleNumber=…&applicantContact=…
        /// Deletes all blobs and clears PhotoUrls in Cosmos.
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeletePhotos(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            await _photoService.DeletePhotosAsync(
                valuationId.ToString(),
                vehicleNumber,
                applicantContact);
            return NoContent();
        }
    }
}