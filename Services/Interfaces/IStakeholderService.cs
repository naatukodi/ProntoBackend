using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IStakeholderService
    {
        Task UpdateAsync(StakeholderUpdateDto dto);
        Task UpdateDocumentsAsync(StakeholderUpdateDto dto);
        Task<Stakeholder?> GetAsync(string valuationId, string vehicleNumber, string applicantContact);
        Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact);
    }
}