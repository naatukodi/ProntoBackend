namespace Valuation.Api.Models;

public class QualityControlDto
{
    public string OverallRating { get; set; } = default!;  // dropdown
    public decimal ValuationAmount { get; set; }             // number
    public string ChassisPunch { get; set; } = default!;  // dropdown
    public string? Remarks { get; set; }             // text
}