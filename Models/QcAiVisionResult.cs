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

        // ── Per-photo stamp text, for location and timestamp consistency ──────
        // Photos that came through WhatsApp have no EXIF at all, so the burned-in
        // GPS Map Camera overlay is the only capture record that survives.

        [JsonPropertyName("photoStamps")]
        public List<QcAiPhotoStamp> PhotoStamps { get; set; } = new();

        /// <summary>Anything the model considered worth a human look. Free text.</summary>
        [JsonPropertyName("observations")]
        public List<string> Observations { get; set; } = new();
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
