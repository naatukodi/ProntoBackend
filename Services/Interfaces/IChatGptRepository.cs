using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    public interface IChatGptRepository
    {
        Task<string> GetVehicleValuationAsync(VehicleDetailsAIDto details);

        /// <summary>
        /// Single-paragraph market valuation for the "Instant AI Value" screen.
        /// Throws InvalidOperationException when OpenAI is not configured.
        /// </summary>
        Task<string> GetMarketValueAsync(MarketValueRequestDto request);
    }
}
