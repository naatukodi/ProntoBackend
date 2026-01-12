using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IWorkflowTableService
    {
        Task UpdateAsync(WorkflowUpdateDto dto);
        Task<WorkflowModel?> GetAsync(string valuationId, string vehicleNumber, string applicantContact);
        Task<List<WorkflowModel?>> GetWorkflowInProgressAsync();
        Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact);

        Task<List<WorkflowModel?>> FilterByStatesAsync(IEnumerable<string> stateKeys);

        Task<List<WorkflowModel?>> FilterByDistrictsAsync(IEnumerable<string> districtKeys);

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

        /// <summary>
        /// Returns the current AssignedTo for the given valuation.
        /// </summary>
        Task<UserModel?> GetAssignedToByValuationAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact);

        /// <summary>
        /// Returns all workflows assigned to the given phone number.
        /// </summary>
        Task<List<WorkflowModel?>> GetByAssignedToPhoneNumberAsync(
            string phoneNumber);

        /// <summary>
        /// Returns the AssignedTo for the given valuation, resolving via the Workflow field if empty.
        /// </summary>
        Task<UserModel?> GetAssignedToByWorkflowAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact);
        Task CompleteFinalReportWFAsync(
            string ValuationId,
            string VehicleNumber,
            string ApplicantContact,
            AssignmentDto assignment);

        Task AddHistoryAsync(LeadHistoryDto dto);

        Task<List<LeadHistoryDto>> GetHistoryAsync(string valuationId);

        Task RejectWorkflowStepAsync(WorkflowRejectDto rejectDto);

    }


}
