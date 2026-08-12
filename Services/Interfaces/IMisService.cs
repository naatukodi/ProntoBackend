using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IMisService
    {
        /// <summary>
        /// Build MIS rows for cases created within [from, to], optionally filtered
        /// by lead status / client name / state. All 24 columns are composed here.
        /// </summary>
        Task<List<MisRowDto>> GetMisAsync(
            DateTime? from,
            DateTime? to,
            string? status,
            string? client,
            string? state);
    }
}
