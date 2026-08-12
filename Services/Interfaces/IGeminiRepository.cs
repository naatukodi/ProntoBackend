using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    /// <summary>
    /// Gemini-backed calls. Kept separate from <see cref="IChatGptRepository"/>:
    /// that one is OpenAI, on a different account and key, and serves the QC page.
    /// </summary>
    public interface IGeminiRepository
    {
        /// <summary>
        /// Single-paragraph market valuation for the "Instant AI Value" screen.
        /// Throws InvalidOperationException when Gemini is not configured.
        /// </summary>
        Task<string> GetMarketValueAsync(MarketValueRequestDto request);
    }
}
