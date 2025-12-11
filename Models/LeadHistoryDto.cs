namespace Valuation.Api.Models;

public class LeadHistoryDto
{
    // Core fields
    public string ValuationId { get; set; } = default!;
    public DateTime DateTime { get; set; }                    // ← NEW
    public string? Action { get; set; }
    public string? Remarks { get; set; }
    
    // User tracking (WHO did this)
    public string? PerformedByUserId { get; set; }           // ← NEW
    public string? PerformedByUserName { get; set; }         // ← NEW
    
    // Status transition (WHAT changed)
    public string? StatusFrom { get; set; }                  // ← NEW
    public string? StatusTo { get; set; }                    // ← NEW
    
    // TAT tracking (HOW LONG)
    public int CurrentTat { get; set; }
    public int TotalTat { get; set; }
    
    // Additional tracking (existing)
    public DateTime? FirstDateTime { get; set; }
    public bool FirstUpdate { get; set; }
    public bool StatusChange { get; set; }
    public DateTime StatusChangedDateTime { get; set; }
    public string? PreviousStatus { get; set; }
    public string? CurrentStatus { get; set; }
}
