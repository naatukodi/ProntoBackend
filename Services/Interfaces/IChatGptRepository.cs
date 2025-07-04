using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    public interface IChatGptRepository
    {
        Task<string> GetVehicleValuationAsync(VehicleDetailsAIDto details);
    }
}
