// src/Valuation.Api/Services/IVehiclePhotoService.cs
using Valuation.Api.Models;

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
    }
}
