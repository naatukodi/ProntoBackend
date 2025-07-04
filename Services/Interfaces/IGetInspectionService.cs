using Valuation.Api.Models;

namespace Valuation.Api.Services;

public interface IGetInspectionService
{
    // …existing…
    Task<InspectionDetails?> GetInspectionAsync(string id, string vehicleNumber, string applicantContact);
    Task UpdateInspectionAsync(string id, InspectionDetailsDto dto, string vehicleNumber, string applicantContact);
    Task DeleteInspectionAsync(string id, string vehicleNumber, string applicantContact);
}
