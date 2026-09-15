namespace Valuation.Api.Models
{
    /// <summary>
    /// A stored photo reading, kept on the valuation document.
    ///
    /// The reading runs by itself when the QC page opens, which only works because the
    /// answer is kept: without this every reviewer opening a case — and every reopen of
    /// the same case — would pay for the same twenty images again. It is re-read when
    /// <see cref="PhotoFingerprint"/> stops matching the case's photos, because that is
    /// the only thing that can change what the photos say.
    ///
    /// What is cached here is only what the reader SAW. The verdicts are not: they are
    /// recomputed on every request from this reading plus the case's current data.
    /// Caching them meant an AVO could fix an odometer or a chassis number after a QC
    /// bounce and, because no photo had changed, get the old verdict back — the reading
    /// was still right, but the comparison against it was stale.
    /// </summary>
    public class QcAiAuditRecord
    {
        /// <summary>
        /// The three passes merged, as the checks below consume it. Kept for display and
        /// so an older record still renders; the per-pass entries are what get reused.
        /// </summary>
        public QcAiVisionResult? Reading { get; set; }

        /// <summary>Identifies the photo set this reading was made from.</summary>
        public string? PhotoFingerprint { get; set; }

        public DateTime ReadAt { get; set; }

        /// <summary>
        /// The vision model that produced this reading, e.g. "gpt-4o-mini".
        ///
        /// Stored in its own right rather than left inside the fingerprint. The
        /// fingerprint is a hash built for cache identity and answers only "do these
        /// photos and this prompt still match?"; it cannot tell anyone, six months
        /// later, which model and which wording produced a finding a reviewer is
        /// looking at. Null on records written before this was added.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// The prompt version in force when this reading was made. One value for the
        /// whole record: all three passes share a version, and a change to it changes
        /// every pass's fingerprint, so they are always re-read together.
        /// </summary>
        public string? PromptVersion { get; set; }

        // Each pass is cached against its OWN photos, because they do not depend on the
        // same ones. Replacing a damaged-panel photo changes what the condition and
        // provenance passes would see, but not one character of the chassis or the
        // odometer — re-reading identity for it would be paying for an answer that
        // cannot have changed. The passes are re-run independently.

        /// <summary>Pass 1: plate, chassis, VIN, odometer, punch.</summary>
        public QcAiPassRecord? Identity { get; set; }

        /// <summary>Pass 2: exterior band, damage, missing parts, tyres, engine bay.</summary>
        public QcAiPassRecord? Condition { get; set; }

        /// <summary>Pass 3: the burned-in capture place and time, per photo.</summary>
        public QcAiPassRecord? Provenance { get; set; }
    }

    /// <summary>One pass's reading, with the fingerprint of the photos it was made from.</summary>
    public class QcAiPassRecord
    {
        public QcAiVisionResult? Reading { get; set; }
        public string? Fingerprint { get; set; }
        public DateTime ReadAt { get; set; }
    }

    /// <summary>
    /// What the reader actually saw, separate from what the comparison concluded.
    ///
    /// Shown verbatim on the QC page so a reviewer can tell a wrong reading from a
    /// wrong vehicle — the two look identical if only the verdict is displayed.
    /// </summary>
    public class QcAiReadings
    {
        public string? RegistrationPlate { get; set; }
        public string? ChassisNumber { get; set; }
        public string? ChassisStencil { get; set; }
        public string? VinPlate { get; set; }
        public long? OdometerKm { get; set; }

        /// <summary>Distinct place names found across the photo stamps.</summary>
        public List<string> Places { get; set; } = new();

        /// <summary>Distinct capture dates found across the photo stamps, as printed.</summary>
        public List<string> CaptureDates { get; set; } = new();

        /// <summary>How many photos carried a readable stamp, out of how many were sent.</summary>
        public int StampedPhotos { get; set; }
        public int TotalPhotos { get; set; }

        /// <summary>Visible defects, "part - what is wrong". Findings, not a verdict.</summary>
        public List<string> DamageFound { get; set; } = new();

        /// <summary>Externally visible parts the photos show to be absent.</summary>
        public List<string> MissingParts { get; set; } = new();
    }
}
