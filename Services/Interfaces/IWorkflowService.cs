// Services/IWorkflowService.cs
using Valuation.Api.Models;

public interface IWorkflowService
{
    Task<List<WorkflowStep>?> GetAsync(string valuationId, string vehicleNumber, string applicantContact);
    Task StartStepAsync(string valuationId, string vehicleNumber, string applicantContact, int stepOrder);
    Task CompleteStepAsync(string valuationId, string vehicleNumber, string applicantContact, int stepOrder);
    Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact);
}
