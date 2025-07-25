using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IWorkflowTableService
    {
        Task UpdateAsync(WorkflowUpdateDto dto);
        Task<WorkflowModel?> GetAsync(string valuationId, string vehicleNumber, string applicantContact);
        Task<List<WorkflowModel?>> GetWorkflowInProgressAsync();
        Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact);

        Task UpdateCurrentWFAssignedToAsync(
            string valuationId, string vehicleNumber, string applicantContact, string? assignedTo = null,
            string? assignedToPhoneNumber = null, string? assignedToEmail = null, string? assignedToWhatsapp = null);

        Task StakeholderWFUpdateAssignmentAsync(
            string ValuationId,
            string VehicleNumber,
            string ApplicantContact,
            string AssignedTo,
            string AssignedToPhoneNumber,
            string AssignedToEmail,
            string AssignedToWhatsapp);
        Task BackendWFUpdateAssignmentAsync(
            string ValuationId,
            string VehicleNumber,
            string ApplicantContact,
            string AssignedTo,
            string AssignedToPhoneNumber,
            string AssignedToEmail,
            string AssignedToWhatsapp);

        Task AVOWFUpdateAssignmentAsync(
            string ValuationId,
            string VehicleNumber,
            string ApplicantContact,
            string AssignedTo,
            string AssignedToPhoneNumber,
            string AssignedToEmail,
            string AssignedToWhatsapp);

        Task QualityControlWFUpdateAssignmentAsync(
            string ValuationId,
            string VehicleNumber,
            string ApplicantContact,
            string AssignedTo,
            string AssignedToPhoneNumber,
            string AssignedToEmail,
            string AssignedToWhatsapp);

        Task FinalReportWFUpdateAssignmentAsync(
            string ValuationId,
            string VehicleNumber,
            string ApplicantContact,
            string AssignedTo,
            string AssignedToPhoneNumber,
            string AssignedToEmail,
            string AssignedToWhatsapp);

        Task CompleteFinalReportWFAsync(
            string ValuationId,
            string VehicleNumber,
            string ApplicantContact);

    }


}
