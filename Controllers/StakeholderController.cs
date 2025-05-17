using Microsoft.AspNetCore.Mvc;

namespace Valuation.Api.Services;

// Ensure this is the correct namespace for IStakeholderService

[ApiController]
[Route("api/valuations/{valuationId:guid}/stakeholder")]
public class StakeholderController : ControllerBase
{
    private readonly IStakeholderService _svc;

    public StakeholderController(IStakeholderService svc)
    {
        _svc = svc;
    }

    [HttpPut]
    public async Task<IActionResult> UpsertStakeholder(
        Guid valuationId,
        [FromForm] StakeholderUpdateDto dto)
    {
        dto.ValuationId = valuationId.ToString();
        await _svc.UpdateAsync(dto);
        return NoContent();
    }
}
