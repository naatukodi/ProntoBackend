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

        // Stamps (or removes) the case's own company wordmark on every photo. The brand
        // comes from the case, not the caller, and photos already in the requested state
        // are skipped, so this is safe to run again after more photos arrive.
        Task<BrandLogoResult> ApplyBrandLogoAsync(string valuationId, string vehicleNumber, string applicantContact, bool apply);

        // One-shot repair across every case: takes the wordmark back off the two chassis
        // evidence slots, which were exempted only after some cases had been stamped.
        // dryRun reports what it would clear without writing anything.
        Task<UnbrandSweepResult> StripChassisWordmarkAsync(bool dryRun);

        // Every photo on the case, named for the .zip. Null means the case does not exist,
        // which the caller must answer before a single archive byte is on the wire.
        Task<List<PhotoArchiveEntry>?> GetPhotoArchiveEntriesAsync(string valuationId, string vehicleNumber, string applicantContact);

        // Fetches those photos and writes them into destination as a .zip.
        Task WritePhotoArchiveAsync(IReadOnlyList<PhotoArchiveEntry> entries, Stream destination, CancellationToken ct = default);
    }
}