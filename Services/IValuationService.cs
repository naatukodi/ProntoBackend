using Valuation.Api.Models;

namespace Valuation.Api.Services;

public interface IValuationService
{
    // existing methods...
    Task<ValuationDocument?> GetValuationDocumentAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task<VehicleDetailsDto?> GetVehicleDetailsAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task UpdateVehicleDetailsAsync(string valuationId, VehicleDetailsDto vehicleDetails, string vehicleNumber, string applicantContact);
    Task DeleteVehicleDetailsAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task<List<OpenValuationDto>> GetOpenValuationsAsync();

    Task<VehicleDetailsDto?> GetVehicleDetailsWithRcCheckAsync(string valuationId, string vehicleNumber, string applicantContact);

}
