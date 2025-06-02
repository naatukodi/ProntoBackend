using System.Threading.Tasks;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public interface IVehicleValuationService
    {
        Task<VehicleValuation?> GetVehicleValuationAsync(
            string id,
            string vehicleNumber,
            string applicantContact);
    }
}
