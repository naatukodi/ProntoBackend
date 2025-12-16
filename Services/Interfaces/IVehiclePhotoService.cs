// src/Valuation.Api/Services/IVehiclePhotoService.cs
using Valuation.Api.Models;
using System.Collections.Generic; // Required for Dictionary
using System.Threading.Tasks;     // Required for Task

namespace Valuation.Api.Services
{
    public interface IVehiclePhotoService
    {
        /// <summary>
        /// Uploads any non‐null IFormFile in the DTO and updates/inserts the Cosmos document.
        /// Returns the updated dictionary of PhotoUrls (fieldKey→URL) after upload.
        /// </summary>
        Task<Dictionary<string, string>> UpdatePhotosAsync(VehiclePhotosDto dto);

        /// <summary>
        /// Returns the current map of PhotoUrls for a given valuationId / vehicleNumber / applicantContact.
        /// </summary>
        Task<Dictionary<string, string>?> GetPhotoUrlsAsync(string valuationId, string vehicleNumber, string applicantContact);

        /// <summary>
        /// Deletes all photo URLs and (optionally) the blobs for a given valuationId/key if needed.
        /// </summary>
        Task DeletePhotosAsync(string valuationId, string vehicleNumber, string applicantContact);

        /// <summary>
        /// ✅ Get all video URLs from database
        /// </summary>
        Task<Dictionary<string, string>?> GetVideoUrlsAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact);

        // =========================================================================
        // ✅ NEW METHODS FOR METADATA (Resolve CS1061 Error)
        // =========================================================================

        /// <summary>
        /// Updates the Date/Time and Location text for a specific photo type.
        /// </summary>
        Task<PhotoMetadata> UpdatePhotoMetadataAsync(string valuationId, string photoType, PhotoMetadataUpdateDto input);

        /// <summary>
        /// Retrieves the dictionary of photo metadata (Dates/Locations).
        /// </summary>
        Task<Dictionary<string, PhotoMetadata>> GetPhotoMetadataAsync(string valuationId);
    }
}