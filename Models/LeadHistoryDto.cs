namespace Valuation.Api.Models;

public class LeadHistoryDto
{
    public string ValuationId { get; set; } = default!;

    public bool FirstUpdate { get; set; }
    public DateTime? FirstDateTime { get; set; }
    public string? Action { get; set; }
    public bool StatusChange { get; set; }
    public string? Remarks { get; set; }
    public string? PreviousStatus { get; set; }
    public string? CurrentStatus { get; set; }
    public DateTime StatusChangedDateTime { get; set; }
    public int CurrentTat { get; set; }
    public int TotalTat { get; set; }

}
