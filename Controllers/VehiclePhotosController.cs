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