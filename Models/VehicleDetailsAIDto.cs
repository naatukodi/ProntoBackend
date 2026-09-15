namespace Valuation.Api.Models
{
    // Renamed from VehicleDetailsDto → VehicleDetailsAIDto
    public class VehicleDetailsAIDto
    {
        public string RegistrationNumber { get; set; } = default!;
        public string Make { get; set; } = default!;
        public string Model { get; set; } = default!;
        public int? YearOfMfg { get; set; }
        public string? Colour { get; set; }
        public string? Fuel { get; set; } = default!;
        public int? EngineCC { get; set; }
        public decimal? IDV { get; set; }
        public DateTime? DateOfRegistration { get; set; }
        public string? City { get; set; }
        public long? Odometer { get; set; }
    }

    /// <summary>
    /// What the model returns for a valuation, as structured output.
    ///
    /// The ranges used to be scraped out of free-form prose with three regexes over
    /// "₹7.5 L – ₹8 L". A phrasing the pattern did not match produced 0/0/0, and 0 is
    /// indistinguishable from a genuine answer of zero — the approval page rendered
    /// "₹0" and "Outside Market Range" with nothing to say the call had failed.
    /// </summary>
    public class VehicleValuationAi
    {
        public decimal? LowRange { get; set; }
        public decimal? MidRange { get; set; }
        public decimal? HighRange { get; set; }

        /// <summary>Why the model landed there. Printed nowhere; kept for audit.</summary>
        public string? Rationale { get; set; }

        /// <summary>The raw JSON the model returned, stored verbatim.</summary>
        public string? Raw { get; set; }
    }

    public class ValuationResponse
    {
        public string? RawResponse { get; set; } = "";
        public decimal? LowRange { get; set; }
        public decimal? MidRange { get; set; }
        public decimal? HighRange { get; set; }
    }
}