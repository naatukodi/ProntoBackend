namespace Valuation.Api.Models
{
    /// <summary>
    /// One row of the MIS report — the 24 columns of MIS FORMAT.xlsx,
    /// joined server-side from a single Cosmos ValuationDocument.
    /// Text columns are returned display-ready; ValuationPrice stays numeric for Excel.
    /// </summary>
    public class MisRowDto
    {
        public string UniqId { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string ClientState { get; set; } = "";
        public string Branch { get; set; } = "";
        public string InspectionType { get; set; } = "";
        public string LeadCreationDateTime { get; set; } = "";
        public string InspectionDateTime { get; set; } = "";
        public string ApprovedDateTime { get; set; } = "";
        public string LeadStatus { get; set; } = "";
        public string Tat { get; set; } = "";
        public string VehicleNo { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public string ApplicantName { get; set; } = "";
        public string MobileNo { get; set; } = "";
        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public string Variant { get; set; } = "";
        public string VehicleCategory { get; set; } = "";
        public string Inspector { get; set; } = "";
        public string Year { get; set; } = "";
        public string VehicleClass { get; set; } = "";
        public decimal? ValuationPrice { get; set; }
        public string ExecutiveName { get; set; } = "";
        public string ExecutiveMobile { get; set; } = "";

        // ── Payment (merged from the Workflows / CompletedWorkflows tables,
        //    falling back to the payment fields stored on the Cosmos document) ──
        public string PaymentStatus { get; set; } = "";
        public string PaymentMode { get; set; } = "";
        /// <summary>UTR / transaction reference as entered by the user (free text).</summary>
        public string PaymentReference { get; set; } = "";
        public decimal? PaymentAmount { get; set; }
        public string PaymentDate { get; set; } = "";
    }
}
