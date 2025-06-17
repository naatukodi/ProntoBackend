using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/valuations/{valuationId:guid}/workflow")]
    public class WorkflowController : ControllerBase
    {
        private readonly IWorkflowTableService _svc;

        public WorkflowController(IWorkflowTableService svc)
        {
            _svc = svc;
        }

        /// <summary>
        /// GET: api/valuations/{valuationId}/workflow
        ///   → retrieve the workflow record by (valuationId, vehicleNumber, applicantContact)
        ///   Query parameters: ?vehicleNumber=…&applicantContact=…
        /// </summary>
        [HttpGet("Table")]
        public async Task<ActionResult<WorkflowModel>> GetWorkflow(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var record = await _svc.GetAsync(
                valuationId.ToString(),
                vehicleNumber,
                applicantContact);

            if (record == null)
                return NotFound();

            return Ok(record);
        }

        /// <summary>
        /// PUT: api/valuations/{valuationId}/workflow
        ///   → Upsert (create or update) a workflow row.
        ///   The client sends everything except ValuationId (it comes from route).
        ///   JSON body should match WorkflowUpdateDto.
        /// </summary>
        [HttpPut("Table")]
        public async Task<IActionResult> UpsertWorkflow(
            Guid valuationId,
            [FromBody] WorkflowUpdateDto dto)
        {
            // Overwrite ValuationId from the route:
            dto.ValuationId = valuationId.ToString();

            await _svc.UpdateAsync(dto);
            return NoContent();
        }

        /// <summary>
        /// DELETE: api/valuations/{valuationId}/workflow
        ///   → Delete the workflow record.
        ///   Query parameters: ?vehicleNumber=…&applicantContact=…
        /// </summary>
        [HttpDelete("Table")]
        public async Task<IActionResult> DeleteWorkflow(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            await _svc.DeleteAsync(
                valuationId.ToString(),
                vehicleNumber,
                applicantContact);

            return NoContent();
        }
    }

    [ApiController]
    [Route("api/valuations/workflows/open")]
    public class OpenWorkflowsController : ControllerBase
    {
        private readonly IWorkflowTableService _svc;

        public OpenWorkflowsController(IWorkflowTableService svc)
        {
            _svc = svc;
        }

        /// <summary>
        /// GET: api/workflows/open
        ///   → Retrieve all open workflow records.
        /// </summary>
        ///   → Retrieve all open workflow records.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<WorkflowModel[]>> GetOpenValuations()
        {
            var records = await _svc.GetWorkflowInProgressAsync();

            if (records == null || records.Count == 0)
                return NotFound();

            return Ok(records);
        }
    }
}

