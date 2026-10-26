using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    public interface IChatGptRepository
    {
        /// <summary>
        /// The vision models these reads run against, recorded with every stored
        /// reading so a finding can be traced back to the model that produced it.
        /// The wheel check runs on its own model, so this names both.
        /// </summary>
        string ModelName { get; }

        /// <summary>
        /// Low, mid and high market values for a case, worked out from comparable
        /// listings found by searching Indian used-vehicle sites, with the listings used.
        /// Takes 15–70 seconds. Throws InvalidOperationException when OpenAI is not configured.
        /// </summary>
        Task<VehicleValuationAi?> GetVehicleValuationAsync(
            VehicleDetailsAIDto details, CancellationToken ct = default);

        /// <summary>
        /// Single-paragraph market valuation for the "Vehga Value" screen, searched from
        /// listing sites like the case valuation, with the listings checked under it.
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

        /// <summary>
        /// THE WHEEL CHECK — wheels and tyres the photos show to be missing: a bare hub,
        /// drum or disc, an empty fork or swingarm, a bare rim, a vehicle on blocks. Only
        /// WheelsMissing and Observations are populated.
        ///
        /// Its own request on its own model, and both were measured (2026-10-23, 53
        /// labelled photos and 10-photo cases). Pass 2's model, asked about wheels
        /// alongside the bodywork, caught 9 of 17 missing wheels hidden in a case, all but
        /// never saw a tractor's bare hub or a truck's bare rim, and asked outright insisted
        /// a motorcycle with no rear wheel had one. Moving all of Pass 2 onto gpt-5-mini
        /// caught more, but it listed two to three times as many bodywork findings and the
        /// portal grades damage by counting them. Asked about wheels alone, gpt-5-mini
        /// caught all 60 single-photo and all 34 in-case missing wheels, motorcycles
        /// included, with no false alarm on a complete vehicle.
        /// </summary>
        Task<QcAiVisionResult?> ReadMissingWheelsAsync(
            IReadOnlyDictionary<string, string> photos,
            CancellationToken ct = default);
    }
}
