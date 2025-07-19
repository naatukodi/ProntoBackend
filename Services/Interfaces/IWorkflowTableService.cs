using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IWorkflowTableService
    {
        Task UpdateAsync(WorkflowUpdateDto dto);
        Task<WorkflowModel?> GetAsync(string valuationId, string vehicleNumber, string applicantContact);
        Task<List<WorkflowModel?>> GetWorkflowInProgressAsync();
        Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact);

        Task StakeholderWFUpdateAssignmentAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact,
            string assignedTo,
            string assignedToPhoneNumber,
            string assignedToEmail,
            string assignedToWhatsapp);
        Task BackendWFUpdateAssignmentAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact,
            string assignedTo,
            string assignedToPhoneNumber,
            string assignedToEmail,
            string assignedToWhatsapp);

        Task AVOWFUpdateAssignmentAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact,
            string assignedTo,
            string assignedToPhoneNumber,
            string assignedToEmail,
            string assignedToWhatsapp);

        Task QualityControlWFUpdateAssignmentAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact,
            string assignedTo,
            string assignedToPhoneNumber,
            string assignedToEmail,
            string assignedToWhatsapp);

        Task FinalReportWFUpdateAssignmentAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact,
            string assignedTo,
            string assignedToPhoneNumber,
            string assignedToEmail,
            string assignedToWhatsapp);

            Task CompleteFinalReportWFAsync(
                string valuationId,
                string vehicleNumber,
                string applicantContact);

    }


}
