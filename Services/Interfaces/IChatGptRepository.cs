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

        /// <summary>
        /// Reads a case's inspection photos and reports what is legible on them:
        /// plate, chassis, VIN, odometer, and each photo's GPS/time stamp.
        ///
        /// Returns readings, not verdicts — the comparison against the RC is done by
        /// the caller so a wrong answer cannot present itself as a pass.
        /// Throws InvalidOperationException when OpenAI is not configured.
        /// </summary>
        /// <param name="photos">Photo slot key to publicly reachable blob URL.</param>
        /// <param name="closeUpKeys">Slots needing full detail because fine characters
        /// must be read; everything else is sent at low detail to keep cost flat as
        /// photo counts grow.</param>
        Task<QcAiVisionResult?> ReadInspectionPhotosAsync(
            IReadOnlyDictionary<string, string> photos,
            IReadOnlySet<string> closeUpKeys,
            CancellationToken ct = default);
    }
}
