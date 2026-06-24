namespace Valuation.Api.Models;
// Models/QualityControl.cs
public class QualityControl
{
    public string OverallRating { get; set; } = default!;

    public decimal ValuationAmount { get; set; }

    public string ChassisPunch { get; set; } = default!;

    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToPhoneNumber { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? AssignedToWhatsapp { get; set; }

    public Dictionary<string, string?>? QcChecklist { get; set; }
    public Dictionary<string, string?>? QcChecklistRemarks { get; set; }
}
