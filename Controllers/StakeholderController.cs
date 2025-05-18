using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/valuations/{valuationId:guid}/stakeholder")]
    public class StakeholderController : ControllerBase
    {
        private readonly IStakeholderService _svc;
        public StakeholderController(IStakeholderService svc) => _svc = svc;

        [HttpPut]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> UpsertStakeholder(
            Guid valuationId,
            [FromForm] StakeholderUpdateDto dto)
        {
            dto.ValuationId = valuationId.ToString();
            await _svc.UpdateAsync(dto);
            return NoContent();
        }

        // GET: api/valuations/{valuationId}/stakeholder
        [HttpGet]
        public async Task<ActionResult<Stakeholder>> GetStakeholder(Guid valuationId, [FromQuery] string vehicleNumber, [FromQuery] string applicantContact)
        {
            var stakeholder = await _svc.GetAsync(valuationId.ToString(), vehicleNumber, applicantContact);
            if (stakeholder == null)
                return NotFound();
            return Ok(stakeholder);
        }

        // DELETE: api/valuations/{valuationId}/stakeholder
        [HttpDelete]
        public async Task<IActionResult> DeleteStakeholder(Guid valuationId, [FromQuery] string vehicleNumber, [FromQuery] string applicantContact)
        {
            await _svc.DeleteAsync(valuationId.ToString(), vehicleNumber, applicantContact);
            return NoContent();
        }
    }
}
