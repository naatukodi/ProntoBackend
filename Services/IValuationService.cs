using Valuation.Api.Models;
public interface IValuationService
{
    // existing methods...
    Task<VehicleDetailsDto?> GetVehicleDetailsAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task UpdateVehicleDetailsAsync(string valuationId, VehicleDetailsDto dto, string applicantContact);
    Task DeleteVehicleDetailsAsync(string valuationId, string vehicleNumber, string applicantContact);
}
