using Valuation.Api.Models;
using Microsoft.AspNetCore.Mvc;
namespace Valuation.Api.Services;

public interface IValuationService
{
    // existing methods...
    Task<ValuationDocument?> GetValuationDocumentAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task<VehicleDetailsDto?> GetVehicleDetailsAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task UpdateVehicleDetailsAsync(string valuationId, VehicleDetailsDto vehicleDetails, string vehicleNumber, string applicantContact);
    Task UpdateAssignmentAsync(string valuationId, string vehicleNumber, string applicantContact, string? assignedTo, string? assignedToPhoneNumber, string? assignedToEmail, string? assignedToWhatsapp);

    Task updateAssignmentAsync(
        string valuationId,
        string vehicleNumber,
        string applicantContact,
        string? assignedTo,
        string? assignedToPhoneNumber,
        string? assignedToEmail,
        string? assignedToWhatsapp);

    Task DeleteVehicleDetailsAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task<List<OpenValuationDto>> GetOpenValuationsAsync();

    Task<ActionResult> CompleteValuationDocumentAsync(
        string valuationId,
        string vehicleNumber,
        string applicantContact,
        CompleteValuationRequestDto request);

    /// <summary>
    /// Runs the duplicate check for a case and stores the outcome on it.
    ///
    /// Engine and chassis are read from the case itself, so the caller does not
    /// have to fetch vehicle details first. Storing the result is the point:
    /// the printed report has no way to run this query, and the portal was
    /// discarding the answer every time it ran.
    /// </summary>
    Task<VehicleDuplicateCheckResponse> RunAndStoreDedupeAsync(
        string valuationId, string vehicleNumber, string applicantContact);

    Task<VehicleDuplicateCheckResponse> CheckDuplicateVehicleAsync(
        string? vehicleNumber,
        string? engineNumber,
        string? chassisNumber,
        string? excludeId = null);

    Task<VehicleDetailsDto?> GetVehicleDetailsWithRcCheckAsync(string valuationId, string vehicleNumber, string applicantContact);

}
