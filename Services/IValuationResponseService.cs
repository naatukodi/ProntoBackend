// src/Valuation.Api/Services/IValuationResponseService.cs
using System.Threading.Tasks;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IValuationResponseService
    {
        /// <summary>
        /// Retrieves the ValuationResponse sub‐document for the given valuationId, vehicleNumber, and applicantContact.
        /// Returns null if not found (or if not yet set).
        /// </summary>
        Task<ValuationResponse?> GetValuationResponseAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact);

        /// <summary>
        /// Upserts (creates or updates) the ValuationResponse sub‐document inside the ValuationDocument.
        /// </summary>
        Task UpdateValuationResponseAsync(
            string valuationId,
            ValuationResponse dto,
            string vehicleNumber,
            string applicantContact);

        /// <summary>
        /// Deletes the ValuationResponse sub‐document (if present) from the ValuationDocument.
        /// </summary>
        Task DeleteValuationResponseAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact);
    }
}
