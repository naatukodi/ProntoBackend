namespace Valuation.Api.Models;

public class QualityControlDto
{
    public string OverallRating { get; set; } = default!;  // dropdown
    public decimal ValuationAmount { get; set; }             // number
    public string ChassisPunch { get; set; } = default!;  // dropdown
    public string? Remarks { get; set; }             // text

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToPhoneNumber { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? AssignedToWhatsapp { get; set; }

    public Dictionary<string, string?>? QcChecklist { get; set; }
    public Dictionary<string, string?>? QcChecklistRemarks { get; set; }
}