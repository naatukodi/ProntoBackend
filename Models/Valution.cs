// Define ValuationDocument if it does not exist elsewhere

namespace Valuation.Api.Models;

public class ValuationDocument
{
    public string id { get; set; }
    public Stakeholder? Stakeholder { get; set; }
    // Add other properties as needed
    public string? CompositeKey { get; set; }
    public string? VehicleNumber { get; set; }
    public string? ApplicantContact { get; set; }
    public string? VehicleSegment { get; set; }
    public List<Document>? Documents { get; set; }
    public VehicleDetailsDto? VehicleDetails { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public InspectionDetails? InspectionDetails { get; set; }
    public QualityControl? QualityControl { get; set; }
    public ValuationResponse? ValuationResponse { get; set; }
    public Dictionary<string, string> PhotoUrls { get; set; } = new();
    public List<WorkflowStep>? Workflow { get; set; }
    public string? Status { get; set; } = "Open";
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToPhoneNumber { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? AssignedToWhatsapp { get; set; }
    public string? AssignedToRole { get; set; }
    public string? RedFlag { get; set; } // Nullable to allow for no red flag
    public string? Remarks { get; set; }
}
