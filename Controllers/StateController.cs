using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers;

[ApiController]
[Route("api/states")]
public class StateController : ControllerBase
{
    private readonly IStateService _svc;

    public StateController(IStateService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<List<StateModel>>> GetAllStates()
    {
        var states = await _svc.GetAllStatesAsync();
        return Ok(states);
    }

    [HttpGet("{stateKey}/districts")]
    public async Task<ActionResult<List<string>>> GetDistrictsByState(string stateKey)
    {
        var districts = await _svc.GetDistrictsByStateAsync(stateKey);
        return Ok(districts);
    }
}