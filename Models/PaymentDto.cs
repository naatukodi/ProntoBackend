namespace Valuation.Api.Models
{
    public class PaymentDto
    {
        public string ValuationId { get; set; } = default!;
        public string VehicleNumber { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;

        public string PaymentStatus { get; set; } = default!;
        public string PaymentMethod { get; set; } = default!;
        public string PaymentReference { get; set; } = default!;
        public DateTime? PaymentDate { get; set; }
        public decimal? PaymentAmount { get; set; }
        public string? PaymentNotes { get; set; }
        public string? SavedBy { get; set; }
        public DateTime? SavedAt { get; set; }
    }
}
