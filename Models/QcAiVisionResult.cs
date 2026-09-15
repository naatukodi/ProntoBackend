using System.Text.Json.Serialization;

namespace Valuation.Api.Models
{
    /// <summary>
    /// What the vision model reports back after looking at a case's inspection photos.
    ///
    /// Deliberately it returns what it READ, not whether things match. Asking a model
    /// "does the chassis match?" invites a confident wrong "pass" on a document a bank
    /// lends against; asking it "what characters are stamped on the plate?" is a
    /// question it can actually answer. The comparison against the RC happens in C#,
    /// where it is exact, auditable, and cannot hallucinate.
    ///
    /// Only genuinely subjective judgements — lighting, damage, tyre wear — come back
    /// as verdicts, because there is nothing to compare those against.
    /// </summary>
    public class QcAiVisionResult
    {
        // ── Values read off the photos ────────────────────────────────────────
        // Null means "could not read it", which is different from reading it and
        // getting a different answer. The first leaves the check unresolved; the
        // second is a genuine mismatch.

        [JsonPropertyName("registrationPlate")]
        public string? RegistrationPlate { get; set; }

        [JsonPropertyName("chassisNumber")]
        public string? ChassisNumber { get; set; }

        [JsonPropertyName("chassisStencil")]
        public string? ChassisStencil { get; set; }

        [JsonPropertyName("vinPlate")]
        public string? VinPlate { get; set; }

        /// <summary>Odometer reading in km, digits only.</summary>
        [JsonPropertyName("odometerKm")]
        public long? OdometerKm { get; set; }

        // ── Judgements with nothing to compare against ────────────────────────

        /// <summary>"pass" | "fail" | null — photos bright enough to inspect.</summary>
        [JsonPropertyName("daylight")]
        public string? Daylight { get; set; }

        /// <summary>"pass" | "fail" | null — plate legible in front AND rear shots.</summary>
        [JsonPropertyName("plateLegible")]
        public string? PlateLegible { get; set; }

        /// <summary>"original" | "repunched" | "tampered" | null.</summary>
        [JsonPropertyName("chassisPunch")]
        public string? ChassisPunch { get; set; }

        // Condition, judged from the photos rather than taken from the AVO's typed
        // answer. These belong here for the reason the class comment gives: there is
        // nothing in the RC to compare a dent or a worn tyre against, so a verdict is
        // the only useful shape. The C# side maps these three bands onto each
        // checklist key's own vocabulary and keeps the AVO's entry beside it.

        /// <summary>"good" | "average" | "poor" | null — panels, paint, visible damage.</summary>
        [JsonPropertyName("exteriorCondition")]
        public string? ExteriorCondition { get; set; }

        /// <summary>"good" | "average" | "poor" | null — engine bay, from the bay shot.</summary>
        [JsonPropertyName("engineCondition")]
        public string? EngineCondition { get; set; }

        /// <summary>"good" | "average" | "poor" | null — worst tyre across the wheels shown.</summary>
        [JsonPropertyName("tyreCondition")]
        public string? TyreCondition { get; set; }

        /// <summary>Why the exterior was banded as it was — the panels and defects seen.</summary>
        [JsonPropertyName("exteriorReason")]
        public string? ExteriorReason { get; set; }

        /// <summary>
        /// Visible damage, one entry per defect: "rear bumper — cracked", "driver door —
        /// deep scratch". Findings, not a verdict: the comparison against what the AVO
        /// recorded happens in the portal, where the per-vehicle field registry lives.
        /// </summary>
        [JsonPropertyName("damageFound")]
        public List<string> DamageFound { get; set; } = new();

        /// <summary>
        /// Parts the photos show to be absent, e.g. "left wing mirror". Only where a
        /// photo shows the mounting point empty — a part merely out of frame is not
        /// missing, and saying so would accuse a sound vehicle.
        /// </summary>
        [JsonPropertyName("missingParts")]
        public List<string> MissingParts { get; set; } = new();

        // ── Per-photo stamp text, for location and timestamp consistency ──────
        // Photos that came through WhatsApp have no EXIF at all, so the burned-in
        // GPS Map Camera overlay is the only capture record that survives.

        /// <summary>
        /// The chassis/VIN characters read from EACH photo that shows them.
        ///
        /// <see cref="ChassisNumber"/> and <see cref="ChassisStencil"/> are single
        /// values and cannot say which photo they came from, so a chassis imprint
        /// belonging to a different vehicle was indistinguishable from a misread of the
        /// right one. Per photo, the comparison can see that one image disagrees with
        /// the rest — which is the only signal that separates a swapped photo from a
        /// bad reading.
        /// </summary>
        [JsonPropertyName("chassisReadings")]
        public List<QcAiChassisReading> ChassisReadings { get; set; } = new();

        [JsonPropertyName("photoStamps")]
        public List<QcAiPhotoStamp> PhotoStamps { get; set; } = new();

        /// <summary>Anything the model considered worth a human look. Free text.</summary>
        [JsonPropertyName("observations")]
        public List<string> Observations { get; set; } = new();
    }

    /// <summary>Chassis/VIN characters as read from one specific photo.</summary>
    public class QcAiChassisReading
    {
        /// <summary>Which photo these characters were read from.</summary>
        [JsonPropertyName("photoKey")]
        public string? PhotoKey { get; set; }

        /// <summary>The characters exactly as seen, or null if not legible.</summary>
        [JsonPropertyName("characters")]
        public string? Characters { get; set; }
    }

    public class QcAiPhotoStamp
    {
        /// <summary>Photo slot key, echoed back so stamps can be attributed.</summary>
        [JsonPropertyName("photoKey")]
        public string? PhotoKey { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        /// <summary>Place name as printed on the stamp, e.g. "Talelma, Telangana".</summary>
        [JsonPropertyName("placeName")]
        public string? PlaceName { get; set; }

        /// <summary>Capture instant as printed, ISO-8601 where the stamp allows it.</summary>
        [JsonPropertyName("capturedAt")]
        public string? CapturedAt { get; set; }
    }
}
