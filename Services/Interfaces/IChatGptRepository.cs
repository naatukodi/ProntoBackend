using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    public interface IChatGptRepository
    {
        /// <summary>
        /// The vision model these reads run against, recorded with every stored
        /// reading so a finding can be traced back to the model that produced it.
        /// </summary>
        string ModelName { get; }

        Task<VehicleValuationAi?> GetVehicleValuationAsync(VehicleDetailsAIDto details);

        /// <summary>
        /// Single-paragraph market valuation for the "Instant AI Value" screen.
        /// Throws InvalidOperationException when OpenAI is not configured.
        /// </summary>
        Task<string> GetMarketValueAsync(MarketValueRequestDto request);

        /// <summary>
        /// PASS 1 — reads a case's inspection photos and reports what is legible on
        /// them: plate, chassis, VIN, odometer, and each photo's GPS/time stamp.
        ///
        /// Returns readings, not verdicts — the comparison against the RC is done by
        /// the caller so a wrong answer cannot present itself as a pass.
        /// Throws InvalidOperationException when OpenAI is not configured.
        ///
        /// The condition fields of the result are left null here; they come from
        /// <see cref="ReadVehicleConditionAsync"/> and are merged by the caller.
        /// </summary>
        /// <param name="photos">Photo slot key to publicly reachable blob URL.</param>
        /// <param name="closeUpKeys">Slots to send at full detail because fine characters
        /// must be read. Everything else goes at low detail. The caller sizes this set
        /// against a token budget rather than a fixed list, so cost stays bounded however
        /// many photos a case has.</param>
        Task<QcAiVisionResult?> ReadInspectionPhotosAsync(
            IReadOnlyDictionary<string, string> photos,
            IReadOnlySet<string> closeUpKeys,
            CancellationToken ct = default);

        /// <summary>
        /// PASS 2 — judges vehicle condition from the body, tyre and engine-bay photos:
        /// exterior band and reason, visible damage, missing parts, tyres, engine bay.
        /// Only those fields are populated; the rest of the result stays at its default.
        ///
        /// This is a second request rather than more text in the Pass 1 prompt, and that
        /// is a measured decision, not a stylistic one. A corrosion instruction that
        /// reliably finds rust on its own stops working once it sits inside the identity
        /// prompt: on the same photos, at the same detail, with the same output fields,
        /// the focused prompt reported "rust on the lower edges of the cargo bed" and the
        /// combined one reported "faded paint". Resolution and schema were each ruled out
        /// separately — what breaks it is asking one request to do everything at once.
        /// </summary>
        Task<QcAiVisionResult?> ReadVehicleConditionAsync(
            IReadOnlyDictionary<string, string> photos,
            IReadOnlySet<string> closeUpKeys,
            CancellationToken ct = default);

        /// <summary>
        /// PASS 3 — photo provenance. Reads the burned-in camera overlay on every photo:
        /// the place printed, coordinates where the overlay literally prints them, and the
        /// capture date and time. Only PhotoStamps is populated.
        ///
        /// Its own request for the same measured reason as Pass 2, and the effect is
        /// larger here. Asked alongside the identity readings, the identical instruction
        /// returned stamps for 2 photos out of 20; asked on its own, 17 — same photos,
        /// same low detail, same wording. Per-photo enumeration is the first work the
        /// model sheds when a request is also doing something else, and those stamps are
        /// what the location and timestamp checks are built on.
        ///
        /// Every photo is sent, always at low detail: the overlay is burned in large and
        /// reads perfectly at 512px. Measured, not assumed — six photos at low detail
        /// gave 6 stamps out of 6, and the same six at high detail gave 5.
        /// </summary>
        Task<QcAiVisionResult?> ReadPhotoProvenanceAsync(
            IReadOnlyDictionary<string, string> photos,
            CancellationToken ct = default);
    }
}
