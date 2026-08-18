using System.Text.Json.Serialization;

namespace Valuation.Api.Models
{
    public class SurepassRcResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("message_code")]
        public string? MessageCode { get; set; }

        [JsonPropertyName("data")]
        public SurepassRcData? Data { get; set; }
    }

    public class SurepassRcData
    {
        [JsonPropertyName("client_id")]
        public string? ClientId { get; set; }

        [JsonPropertyName("rc_number")]
        public string? RcNumber { get; set; }

        [JsonPropertyName("registration_date")]
        public string? RegistrationDate { get; set; }

        [JsonPropertyName("owner_name")]
        public string? OwnerName { get; set; }

        [JsonPropertyName("father_name")]
        public string? FatherName { get; set; }

        [JsonPropertyName("present_address")]
        public string? PresentAddress { get; set; }

        [JsonPropertyName("permanent_address")]
        public string? PermanentAddress { get; set; }

        [JsonPropertyName("mobile_number")]
        public string? MobileNumber { get; set; }

        [JsonPropertyName("vehicle_category")]
        public string? VehicleCategory { get; set; }

        [JsonPropertyName("vehicle_category_description")]
        public string? VehicleCategoryDescription { get; set; }

        // Note: Surepass has a typo in this field name ("chasi" instead of "chassis")
        [JsonPropertyName("vehicle_chasi_number")]
        public string? VehicleChasisNumber { get; set; }

        [JsonPropertyName("vehicle_engine_number")]
        public string? VehicleEngineNumber { get; set; }

        [JsonPropertyName("maker_description")]
        public string? MakerDescription { get; set; }

        [JsonPropertyName("maker_model")]
        public string? MakerModel { get; set; }

        [JsonPropertyName("variant")]
        public string? Variant { get; set; }

        [JsonPropertyName("body_type")]
        public string? BodyType { get; set; }

        [JsonPropertyName("fuel_type")]
        public string? FuelType { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("norms_type")]
        public string? NormsType { get; set; }

        [JsonPropertyName("manufacturing_date")]
        public string? ManufacturingDate { get; set; }

        [JsonPropertyName("manufacturing_date_formatted")]
        public string? ManufacturingDateFormatted { get; set; }

        [JsonPropertyName("registered_at")]
        public string? RegisteredAt { get; set; }

        [JsonPropertyName("owner_number")]
        public string? OwnerNumber { get; set; }

        [JsonPropertyName("financed")]
        public bool Financed { get; set; }

        [JsonPropertyName("financer")]
        public string? Financer { get; set; }

        [JsonPropertyName("insurance_company")]
        public string? InsuranceCompany { get; set; }

        [JsonPropertyName("insurance_policy_number")]
        public string? InsurancePolicyNumber { get; set; }

        [JsonPropertyName("insurance_upto")]
        public string? InsuranceUpto { get; set; }

        [JsonPropertyName("fit_up_to")]
        public string? FitUpTo { get; set; }

        [JsonPropertyName("tax_upto")]
        public string? TaxUpto { get; set; }

        [JsonPropertyName("tax_paid_upto")]
        public string? TaxPaidUpto { get; set; }

        [JsonPropertyName("cubic_capacity")]
        public string? CubicCapacity { get; set; }

        [JsonPropertyName("vehicle_gross_weight")]
        public string? VehicleGrossWeight { get; set; }

        [JsonPropertyName("no_cylinders")]
        public string? NoCylinders { get; set; }

        [JsonPropertyName("seat_capacity")]
        public string? SeatCapacity { get; set; }

        [JsonPropertyName("sleeper_capacity")]
        public string? SleeperCapacity { get; set; }

        [JsonPropertyName("standing_capacity")]
        public string? StandingCapacity { get; set; }

        [JsonPropertyName("wheelbase")]
        public string? Wheelbase { get; set; }

        [JsonPropertyName("unladen_weight")]
        public string? UnladenWeight { get; set; }

        [JsonPropertyName("pucc_number")]
        public string? PuccNumber { get; set; }

        [JsonPropertyName("pucc_upto")]
        public string? PuccUpto { get; set; }

        [JsonPropertyName("permit_number")]
        public string? PermitNumber { get; set; }

        [JsonPropertyName("permit_issue_date")]
        public string? PermitIssueDate { get; set; }

        [JsonPropertyName("permit_valid_from")]
        public string? PermitValidFrom { get; set; }

        [JsonPropertyName("permit_valid_upto")]
        public string? PermitValidUpto { get; set; }

        [JsonPropertyName("permit_type")]
        public string? PermitType { get; set; }

        [JsonPropertyName("blacklist_status")]
        public string? BlacklistStatus { get; set; }

        [JsonPropertyName("rc_status")]
        public string? RcStatus { get; set; }

        [JsonPropertyName("less_info")]
        public bool? LessInfo { get; set; }

        [JsonPropertyName("masked_name")]
        public bool? MaskedName { get; set; }
    }
}
