namespace Valuation.Api.Models
{
    /// <summary>
    /// A stored photo reading, kept on the valuation document.
    ///
    /// The reading runs by itself when the QC page opens, which only works because the
    /// answer is kept: without this every reviewer opening a case — and every reopen of
    /// the same case — would pay for the same twenty images again. It is re-read when
    /// <see cref="PhotoFingerprint"/> stops matching the case's photos, because that is
    /// the only thing that can change the answer.
    /// </summary>
    public class QcAiAuditRecord
    {
        /// <summary>Checklist key to verdict, for keys the reading could settle.</summary>
        public Dictionary<string, string> Cl { get; set; } = new();

        /// <summary>Checklist key to the evidence behind it. Populated even without a verdict.</summary>
        public Dictionary<string, string> Why { get; set; } = new();

        public List<string> Observations { get; set; } = new();

        /// <summary>Raw values read off the photos, shown to the reviewer as-is.</summary>
        public QcAiReadings? Readings { get; set; }

        /// <summary>Identifies the photo set this reading was made from.</summary>
        public string? PhotoFingerprint { get; set; }

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
    }
}
