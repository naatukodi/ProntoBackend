using Valuation.Api.Models;

namespace Valuation.Api.Services;

public interface IGetInspectionService
{
    // …existing…
    Task<InspectionDetails?> GetInspectionAsync(string id, string vehicleNumber, string applicantContact);
    Task UpdateInspectionAsync(string id, InspectionDetailsDto dto, string vehicleNumber, string applicantContact);
    Task DeleteInspectionAsync(string id, string vehicleNumber, string applicantContact);
    Task UpdateAssignmentAsync(string valuationId, string vehicleNumber, string applicantContact, string? assignedTo, string? assignedToPhoneNumber, string? assignedToEmail, string? assignedToWhatsapp);
}
