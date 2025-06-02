using System.Threading.Tasks;
using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    public interface IChatGptRepository
    {
        Task<string> GetVehicleValuationResponseAsync(VehicleDetailsAIDto details);
    }
}
