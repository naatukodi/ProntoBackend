public class CompleteValuationRequestDto
{
    public string Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }

    // Payment details from frontend
    public string? PaymentStatus { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentAmount { get; set; }
    public string? CompletedByPhoneNumber { get; set; }
    public string? CompletedByEmail { get; set; }
    public string? CompletedByWhatsapp { get; set; }
}
