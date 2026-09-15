namespace Valuation.Api.Services.Interfaces
{
    /// <summary>
    /// Assigns a case its permanent human-readable reference, e.g. VG-519499-K.
    ///
    /// The scheme itself is not new: ProntoPDFGeneration has minted these since
    /// multi-brand, but only on first PDF generation — so for the whole of a case's
    /// working life there was no number anyone could quote over the phone, and the
    /// portal fell back to showing eight characters of a GUID.
    ///
    /// This moves the mint to registration. The algorithm is a deliberate copy of
    /// PdfReportService.ComposeReference so the two can never disagree: the PDF's
    /// EnsureReferenceNumberAsync returns early when a reference is already stored,
    /// so once this has run the PDF simply uses what it finds.
    /// </summary>
    public interface IReferenceNumberService
    {
        /// <summary>
        /// Returns the case's reference, assigning and persisting one on first call.
        /// Never recomputes an existing reference: the report QR encodes the blob path
        /// reports/{reference}.pdf, so changing it orphans reports already issued.
        /// </summary>
        Task<string?> EnsureAsync(string valuationId, string vehicleNumber,
                                  string applicantContact, CancellationToken ct = default);
    }
}
