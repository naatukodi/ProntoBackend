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

        // Gallery page photo selection (used by QC + the PDF generator)
        Task<List<string>> GetGalleryPhotoSelectionAsync(string valuationId, string vehicleNumber, string applicantContact);

        Task<List<string>> UpdateGalleryPhotoSelectionAsync(string valuationId, string vehicleNumber, string applicantContact, List<string> selectedKeys);

        // Burns a text note onto an already-uploaded photo and replaces it in place.
        Task<(string PhotoUrl, string Note)> AnnotatePhotoAsync(string valuationId, string vehicleNumber, string applicantContact, string photoKey, string note);
    }
}