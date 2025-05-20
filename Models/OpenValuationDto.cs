namespace Valuation.Api.Models;

public class OpenValuationDto
{
    public string Id { get; set; } = default!;
    public string VehicleNumber { get; set; } = default!;
    public string ApplicantName { get; set; } = default!;
    public string ApplicantContact { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public List<WorkflowStep> InProgressWorkflow { get; set; } = new();
}