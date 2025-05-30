// Valuation.Api.Models/AttestrRcResponse.cs
using System.Text.Json.Serialization;

namespace Valuation.Api.Models
{
    public class AttestrRcResponse
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("registered")]
        public string? Registered { get; set; }

        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }  // not used here

        [JsonPropertyName("manufactured")]
        public string? Manufactured { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("father")]
        public string? Father { get; set; }

        [JsonPropertyName("currentAddress")]
        public string? CurrentAddress { get; set; }

        [JsonPropertyName("permanentAddress")]
        public string? PermanentAddress { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("categoryDescription")]
        public string? CategoryDescription { get; set; }

        [JsonPropertyName("makerDescription")]
        public string? MakerDescription { get; set; }

        [JsonPropertyName("makerModel")]
        public string? MakerModel { get; set; }

        [JsonPropertyName("makerVariant")]
        public string? MakerVariant { get; set; }

        [JsonPropertyName("bodyType")]
        public string? BodyType { get; set; }

        [JsonPropertyName("fuelType")]
        public string? FuelType { get; set; }

        [JsonPropertyName("colorType")]
        public string? ColorType { get; set; }

        [JsonPropertyName("chassisNumber")]
        public string? ChassisNumber { get; set; }

        [JsonPropertyName("engineNumber")]
        public string? EngineNumber { get; set; }

        [JsonPropertyName("cubicCapacity")]
        public string? CubicCapacity { get; set; }

        [JsonPropertyName("grossWeight")]
        public string? GrossWeight { get; set; }

        [JsonPropertyName("wheelBase")]
        public string? WheelBase { get; set; }

        [JsonPropertyName("unladenWeight")]
        public string? UnladenWeight { get; set; }

        [JsonPropertyName("cylinders")]
        public string? Cylinders { get; set; }

        [JsonPropertyName("seatingCapacity")]
        public string? SeatingCapacity { get; set; }

        [JsonPropertyName("sleepingCapacity")]
        public string? SleepingCapacity { get; set; }

        [JsonPropertyName("standingCapacity")]
        public string? StandingCapacity { get; set; }

        [JsonPropertyName("financed")]
        public bool Financed { get; set; }

        [JsonPropertyName("lender")]
        public string? Lender { get; set; }

        [JsonPropertyName("rto")]
        public string? Rto { get; set; }

        [JsonPropertyName("normsType")]
        public string? NormsType { get; set; }

        [JsonPropertyName("pollutionCertificateNumber")]
        public string? PollutionCertificateNumber { get; set; }

        [JsonPropertyName("pollutionCertificateUpto")]
        public string? PollutionCertificateUpto { get; set; }

        [JsonPropertyName("permitNumber")]
        public string? PermitNumber { get; set; }

        [JsonPropertyName("permitIssued")]
        public string? PermitIssued { get; set; }

        [JsonPropertyName("permitFrom")]
        public string? PermitFrom { get; set; }

        [JsonPropertyName("permitType")]
        public string? PermitType { get; set; }

        [JsonPropertyName("permitUpto")]
        public string? PermitUpto { get; set; }

        [JsonPropertyName("taxUpto")]
        public string? TaxUpto { get; set; }

        [JsonPropertyName("taxPaidUpto")]
        public string? TaxPaidUpto { get; set; }

        [JsonPropertyName("insuranceProvider")]
        public string? InsuranceProvider { get; set; }

        [JsonPropertyName("insurancePolicyNumber")]
        public string? InsurancePolicyNumber { get; set; }

        [JsonPropertyName("insuranceUpto")]
        public string? InsuranceUpto { get; set; }

        [JsonPropertyName("exShowroomPrice")]
        public decimal? ExShowroomPrice { get; set; }

        [JsonPropertyName("blacklistStatus")]
        public string? BlacklistStatus { get; set; }
    }

    // Define a POCO matching the expected response JSON:
    public class CheckXResponse
    {
        public bool Valid { get; set; }
        public string? Status { get; set; }
        [JsonPropertyName("registered")]
        public string? Registered { get; set; }

        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }  // not used here

        [JsonPropertyName("manufactured")]
        public string? Manufactured { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("father")]
        public string? Father { get; set; }

        [JsonPropertyName("currentAddress")]
        public string? CurrentAddress { get; set; }

        [JsonPropertyName("permanentAddress")]
        public string? PermanentAddress { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("categoryDescription")]
        public string? CategoryDescription { get; set; }

        [JsonPropertyName("makerDescription")]
        public string? MakerDescription { get; set; }

        [JsonPropertyName("makerModel")]
        public string? MakerModel { get; set; }

        [JsonPropertyName("makerVariant")]
        public string? MakerVariant { get; set; }

        [JsonPropertyName("bodyType")]
        public string? BodyType { get; set; }

        [JsonPropertyName("fuelType")]
        public string? FuelType { get; set; }

        [JsonPropertyName("colorType")]
        public string? ColorType { get; set; }

        [JsonPropertyName("chassisNumber")]
        public string? ChassisNumber { get; set; }

        [JsonPropertyName("engineNumber")]
        public string? EngineNumber { get; set; }

        [JsonPropertyName("cubicCapacity")]
        public string? CubicCapacity { get; set; }

        [JsonPropertyName("grossWeight")]
        public string? GrossWeight { get; set; }

        [JsonPropertyName("wheelBase")]
        public string? WheelBase { get; set; }

        [JsonPropertyName("unladenWeight")]
        public string? UnladenWeight { get; set; }

        [JsonPropertyName("cylinders")]
        public string? Cylinders { get; set; }

        [JsonPropertyName("seatingCapacity")]
        public string? SeatingCapacity { get; set; }

        [JsonPropertyName("sleepingCapacity")]
        public string? SleepingCapacity { get; set; }

        [JsonPropertyName("standingCapacity")]
        public string? StandingCapacity { get; set; }

        [JsonPropertyName("financed")]
        public bool Financed { get; set; }

        [JsonPropertyName("lender")]
        public string? Lender { get; set; }

        [JsonPropertyName("rto")]
        public string? Rto { get; set; }

        [JsonPropertyName("normsType")]
        public string? NormsType { get; set; }

        [JsonPropertyName("pollutionCertificateNumber")]
        public string? PollutionCertificateNumber { get; set; }

        [JsonPropertyName("pollutionCertificateUpto")]
        public string? PollutionCertificateUpto { get; set; }

        [JsonPropertyName("permitNumber")]
        public string? PermitNumber { get; set; }

        [JsonPropertyName("permitIssued")]
        public string? PermitIssued { get; set; }

        [JsonPropertyName("permitFrom")]
        public string? PermitFrom { get; set; }

        [JsonPropertyName("permitType")]
        public string? PermitType { get; set; }

        [JsonPropertyName("permitUpto")]
        public string? PermitUpto { get; set; }

        [JsonPropertyName("taxUpto")]
        public string? TaxUpto { get; set; }

        [JsonPropertyName("taxPaidUpto")]
        public string? TaxPaidUpto { get; set; }

        [JsonPropertyName("insuranceProvider")]
        public string? InsuranceProvider { get; set; }

        [JsonPropertyName("insurancePolicyNumber")]
        public string? InsurancePolicyNumber { get; set; }

        [JsonPropertyName("insuranceUpto")]
        public string? InsuranceUpto { get; set; }

        [JsonPropertyName("exShowroomPrice")]
        public decimal? ExShowroomPrice { get; set; }

        [JsonPropertyName("blacklistStatus")]
        public string? BlacklistStatus { get; set; }
    }
}
