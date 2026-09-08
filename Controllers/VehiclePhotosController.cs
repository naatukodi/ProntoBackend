using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            try
            {
                var resultMap = await _photoService.UpdatePhotosAsync(dto);
                return Ok(resultMap);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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

        // Streams every photo on the case, and the walkaround video, as one .zip attachment.
        //
        // The bytes are fetched server-side on purpose: the blob container serves no CORS
        // headers, so the portal cannot fetch() the images to zip them in the browser, and
        // a cross-origin <a download> is ignored -- the image would simply open in a tab.
        // Content-Disposition on this response makes the browser save the file instead,
        // and because that is a plain navigation it needs no CORS entry of its own.
        [HttpGet("download")]
        public async Task<IActionResult> DownloadPhotosArchive(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            CancellationToken ct)
        {
            var entries = await _photoService.GetPhotoArchiveEntriesAsync(
                valuationId.ToString(), vehicleNumber, applicantContact);

            // Both answers have to be settled before the first archive byte is written:
            // once the body is streaming the status code is already on the wire, and a
            // failure after that can only be reported inside the file itself.
            if (entries == null)
                return NotFound(new { message = "Case not found." });
            if (entries.Count == 0)
                return NotFound(new { message = "This case has no photos or video to download." });

            var fileName = $"{ArchiveFileNamePart(vehicleNumber)}-media.zip";
            Response.ContentType = "application/zip";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

            // ZipArchive writes its central directory synchronously when it is disposed,
            // and Kestrel refuses synchronous writes on a response body by default. Without
            // this the archive streams every photo and then dies on the last few bytes with
            // "Synchronous operations are disallowed". Scoped to this one response.
            var bodyControl = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (bodyControl != null) bodyControl.AllowSynchronousIO = true;

            await _photoService.WritePhotoArchiveAsync(entries, Response.Body, ct);
            return new EmptyResult();
        }

        // Keeps the download name to characters that survive a Content-Disposition header
        // and every file system it might land on. A registration that sanitises away to
        // nothing still yields "case-photos.zip".
        private static string ArchiveFileNamePart(string? vehicleNumber)
        {
            var cleaned = new string((vehicleNumber ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '-')
                .ToArray())
                .Trim('-');

            return string.IsNullOrEmpty(cleaned) ? "case" : cleaned;
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

        // Gallery page photo selection (chosen by QC, consumed by the PDF generator)
        [HttpGet("gallery-selection")]
        public async Task<ActionResult<List<string>>> GetGallerySelection(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var selection = await _photoService.GetGalleryPhotoSelectionAsync(valuationId.ToString(), vehicleNumber, applicantContact);
            return Ok(selection);
        }

        [HttpPut("gallery-selection")]
        public async Task<ActionResult<List<string>>> UpdateGallerySelection(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            [FromBody] List<string> selectedKeys)
        {
            var result = await _photoService.UpdateGalleryPhotoSelectionAsync(
                valuationId.ToString(), vehicleNumber, applicantContact, selectedKeys ?? new List<string>());
            return Ok(result);
        }

        // Burns a text note onto an already-uploaded photo (fixed slot or custom photo)
        // and replaces it in place. Compositing happens server-side to avoid Azure Blob
        // Storage's lack of CORS headers, which would otherwise taint a client-side canvas.
        [HttpPut("{photoKey}/annotate")]
        public async Task<IActionResult> AnnotatePhoto(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            string photoKey,
            [FromBody] AnnotatePhotoRequest request)
        {
            try
            {
                var result = await _photoService.AnnotatePhotoAsync(
                    valuationId.ToString(), vehicleNumber, applicantContact, photoKey, request.Note);
                return Ok(new { photoUrl = result.PhotoUrl, note = result.Note });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Stamps the case's company wordmark onto every photo, or takes it back off.
        // Which company is read from the case itself, so a Pronto case gets the Pronto
        // mark whoever presses the button. Compositing is server-side for the same
        // reason annotation is: Azure Blob Storage serves no CORS headers, so a
        // browser canvas drawing these images would be tainted and unreadable.
        [HttpPut("logo")]
        public async Task<IActionResult> ApplyBrandLogo(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            [FromBody] BrandLogoRequest? request)
        {
            try
            {
                var result = await _photoService.ApplyBrandLogoAsync(
                    valuationId.ToString(), vehicleNumber, applicantContact, request?.Apply ?? true);
                return Ok(result);
            }
            catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound(new { message = "Case not found." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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