using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    /// <summary>
    /// Gemini-backed calls. Kept separate from <see cref="IChatGptRepository"/>:
    /// that one is OpenAI, on a different account and key.
    ///
    /// No implementation of this interface is currently consumed — "Instant AI Value"
    /// moved to <see cref="IChatGptRepository.GetMarketValueAsync"/> after the Gemini
    /// key stopped working. Still registered so switching back is a one-line change.
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
