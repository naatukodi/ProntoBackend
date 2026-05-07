using Valuation.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Valuation.Api.Services
{
    public interface IVehiclePhotoService
    {
        Task<Dictionary<string, string>> UpdatePhotosAsync(VehiclePhotosDto dto);

        Task<Dictionary<string, string>?> GetPhotoUrlsAsync(string valuationId, string vehicleNumber, string applicantContact);

        Task DeletePhotosAsync(string valuationId, string vehicleNumber, string applicantContact);

        Task<Dictionary<string, string>?> GetVideoUrlsAsync(string valuationId, string vehicleNumber, string applicantContact);

        // ✅ NEW: Fetch custom photos for the PDF
        Task<List<SavedCustomPhoto>> GetCustomPhotosAsync(string valuationId, string vehicleNumber, string applicantContact);

        // ✅ UPDATED: Added vehicleNumber and applicantContact to prevent cross-partition queries
        Task<PhotoMetadata> UpdatePhotoMetadataAsync(string valuationId, string vehicleNumber, string applicantContact, string photoType, PhotoMetadataUpdateDto input);

        Task<Dictionary<string, PhotoMetadata>> GetPhotoMetadataAsync(string valuationId, string vehicleNumber, string applicantContact);
    }
}