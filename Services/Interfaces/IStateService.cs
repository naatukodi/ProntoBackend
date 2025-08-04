using Valuation.Api.Models;
namespace Valuation.Api.Services;

public interface IStateService
{
    Task<List<StateModel>> GetAllStatesAsync();
    Task<List<string>> GetDistrictsByStateAsync(string state);
}