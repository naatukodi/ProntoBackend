using System.Threading.Tasks;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IWorkflowTableService
    {
        Task UpdateAsync(WorkflowUpdateDto dto);
        Task<WorkflowModel?> GetAsync(string valuationId, string vehicleNumber, string applicantContact);
        Task<List<WorkflowModel?>> GetWorkflowInProgressAsync();
        Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact);
    }
}
