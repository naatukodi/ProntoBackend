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
