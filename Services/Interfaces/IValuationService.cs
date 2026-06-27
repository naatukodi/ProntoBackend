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

    Task<VehicleDuplicateCheckResponse> CheckDuplicateVehicleAsync(
        string? vehicleNumber,
        string? engineNumber,
        string? chassisNumber,
        string? excludeId = null);

    Task<VehicleDetailsDto?> GetVehicleDetailsWithRcCheckAsync(string valuationId, string vehicleNumber, string applicantContact);

}
