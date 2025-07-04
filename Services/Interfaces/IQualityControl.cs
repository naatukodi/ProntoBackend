using Valuation.Api.Models;

namespace Valuation.Api.Services;

public interface IQualityControlService
{
    Task<QualityControl?> GetQualityControlAsync(string id, string vehicleNumber, string applicantContact);
    Task UpdateQualityControlAsync(string id, QualityControlDto dto, string vehicleNumber, string applicantContact);
    Task DeleteQualityControlAsync(string id, string vehicleNumber, string applicantContact);
}