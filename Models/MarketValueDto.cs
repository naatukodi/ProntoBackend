namespace Valuation.Api.Models
{
    /// <summary>
    /// Input for the "Instant AI Value" screen — the six fields the user fills in
    /// on the market-value form. Free-text: these go straight into the model prompt.
    /// </summary>
    public class MarketValueRequestDto
    {
        public string VehicleType { get; set; } = "";
        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public string Year { get; set; } = "";
        public string Kms { get; set; } = "";
        public string Location { get; set; } = "";
    }

    /// <summary>
    /// The model's single-paragraph valuation, ready to render as-is.
    /// </summary>
    public class MarketValueResponseDto
    {
        public string Result { get; set; } = "";
    }
}
