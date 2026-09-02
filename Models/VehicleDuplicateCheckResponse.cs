// Models/VehicleDuplicateCheckResponse.cs

namespace Valuation.Api.Models
{
    public class VehicleDuplicateCheckResponse
    {
        public bool IsDuplicate { get; set; }
        public bool IsVehicleNumberExists { get; set; }
        public bool IsEngineNumberExists { get; set; }
        public bool IsChassisNumberExists { get; set; }
        public int TotalDuplicatesFound { get; set; }
        public List<ExistingVehicleRecord> ExistingRecords { get; set; } = new List<ExistingVehicleRecord>();
        public List<string> Messages { get; set; } = new List<string>();
        // NEW
        public decimal? AverageValuationAmount { get; set; }
    }

    public class ExistingVehicleRecord
    {
        public string ValuationId { get; set; } = string.Empty;
        public string VehicleNumber { get; set; } = string.Empty;
        public string? EngineNumber { get; set; }
        public string? ChassisNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string MatchedField { get; set; } = string.Empty;
        // NEW FIELDS
        public string? Company { get; set; }
        public decimal? ValuationAmount { get; set; }
    }
}

namespace Valuation.Api.Models
{
    /// <summary>
    /// The outcome of the duplicate check, kept on the case.
    ///
    /// The portal has always run this check live and thrown the answer away, so
    /// the printed report had nothing to state. Storing it means the report can
    /// say what was actually found, and says it as of the moment it was checked
    /// rather than re-deriving a different number months later.
    /// </summary>
    public class DedupeCheckRecord
    {
        /// <summary>Other cases matching this vehicle, engine or chassis. Excludes this case.</summary>
        public int MatchCount { get; set; }

        /// <summary>Which fields matched, e.g. "Vehicle Number, Chassis Number".</summary>
        public string? MatchedOn { get; set; }

        public DateTime CheckedAt { get; set; }
    }
}
