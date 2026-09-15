using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IVehicleValuationService
    {
        Task<VehicleValuation?> GetVehicleValuationAsync(
            string id,
            string vehicleNumber,
            string applicantContact);

        /// <summary>
        /// The stored market range, generating one on first use. Pass force to discard
        /// what is stored and ask again.
        /// </summary>
        Task<VehicleValuation?> EnsureAsync(
            string id,
            string vehicleNumber,
            string applicantContact,
            bool force = false,
            CancellationToken ct = default);
    }
}

