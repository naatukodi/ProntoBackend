namespace Valuation.Api.Models
{
    /// <summary>
    /// What the valuation prompt is told about a vehicle.
    ///
    /// Nothing here identifies the vehicle or its owner. The model searches the web with
    /// what it is given, so the registration number, the owner and any street address
    /// stay out; only the state code from the number goes in.
    /// </summary>
    public class VehicleDetailsAIDto
    {
        public string? Segment { get; set; }
        public string? ClassOfVehicle { get; set; }
        public string? BodyType { get; set; }
        public string Make { get; set; } = default!;
        public string Model { get; set; } = default!;
        public string? Variant { get; set; }
        public int? MonthOfMfg { get; set; }
        public int? YearOfMfg { get; set; }
        public string? Fuel { get; set; }
        public int? EngineCC { get; set; }
        public decimal? ExShowroomPrice { get; set; }
        public DateTime? DateOfRegistration { get; set; }
        public string? OwnerSerialNo { get; set; }
        public long? Odometer { get; set; }

        /// <summary>A place name to search listings near, never a street address.</summary>
        public string? City { get; set; }

        /// <summary>The two letters a registration number starts with, e.g. "AP".</summary>
        public string? StateCode { get; set; }

        /// <summary>The inspector's condition bands, e.g. "exterior good, engine average".</summary>
        public string? Condition { get; set; }
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

        /// <summary>How the model got from the listings to the range.</summary>
        public string? Rationale { get; set; }

        /// <summary>The listings the range was built from.</summary>
        public List<ValuationComparable> Comparables { get; set; } = new();

        /// <summary>The raw JSON the model returned, stored verbatim.</summary>
        public string? Raw { get; set; }
    }

    /// <summary>A used-vehicle listing a valuation was based on, as the site showed it.</summary>
    public class ValuationComparable
    {
        public string? Site { get; set; }
        public string? Title { get; set; }
        public string? Url { get; set; }
        public decimal? Price { get; set; }
        public int? Year { get; set; }
        public long? Km { get; set; }
        public string? Location { get; set; }
    }

    public class ValuationResponse
    {
        public string? RawResponse { get; set; } = "";
        public decimal? LowRange { get; set; }
        public decimal? MidRange { get; set; }
        public decimal? HighRange { get; set; }

        /// <summary>How the range was reached. Null on ranges from before listing search.</summary>
        public string? Rationale { get; set; }

        /// <summary>The listings the range was built from. Null on ranges from before listing search.</summary>
        public List<ValuationComparable>? Comparables { get; set; }

        /// <summary>When the listings were read. Asking prices move, so a range is only as current as this.</summary>
        public DateTime? GeneratedAt { get; set; }
    }
}
