using Valuation.Api.Models;

namespace Valuation.Api.Services.Interfaces
{
    /// <summary>
    /// Finds a case by the identifiers people actually quote.
    ///
    /// The dashboard's own filter only sees the rows already loaded, which excludes
    /// completed cases past the 30-day cutoff and cannot match on chassis or engine
    /// number at all — neither is on the workflow row. This queries the case documents
    /// directly, so an old or closed case is still reachable.
    /// </summary>
    public interface ICaseSearchService
    {
        /// <summary>
        /// Cases whose reference, vehicle number, chassis number or engine number
        /// contains <paramref name="term"/>. Empty for a term shorter than the minimum.
        /// </summary>
        Task<IReadOnlyList<CaseSearchResultDto>> SearchAsync(string term, CancellationToken ct = default);
    }
}
