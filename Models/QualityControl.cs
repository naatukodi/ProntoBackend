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

    /// <summary>
    /// Checklist keys a reviewer actually decided, as opposed to ones the system or
    /// the photo reader filled in.
    ///
    /// Without this the two were indistinguishable once saved: the page persists the
    /// whole checklist, so every AI verdict came back looking like a human decision
    /// and was then protected from ever being updated. One Save locked the reader out
    /// of the case permanently.
    /// </summary>
    public List<string>? QcChecklistReviewerKeys { get; set; }
}
