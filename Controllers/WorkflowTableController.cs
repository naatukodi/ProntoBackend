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

        [HttpPost("addhistory")]
        public async Task<IActionResult> AddHistory(
            [FromBody] LeadHistoryDto dto)
        {
            await _svc.AddHistoryAsync(dto);
            return NoContent();
        }

        [HttpGet("gethistory")]
        public async Task<IActionResult> GetHistory(
            Guid valuationId)
        {
            var results = await _svc.GetHistoryAsync(valuationId.ToString());
            return Ok(results);
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

        [HttpGet("assignedto/valuation")]
        public async Task<IActionResult> GetAssignedToByValuationAsync(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var assignedTo = await _svc.GetAssignedToByValuationAsync(
                valuationId.ToString(),
                vehicleNumber,
                applicantContact);

            if (assignedTo == null)
                return NotFound();

            return Ok(assignedTo);
        }

        [HttpGet("assignedto/phone")]
        public async Task<IActionResult> GetByAssignedToPhoneNumberAsync(
            string assignedToPhoneNumber)
        {
            var records = await _svc.GetByAssignedToPhoneNumberAsync(
                assignedToPhoneNumber);


            if (records == null || records.Count == 0)
                return NotFound();

            return Ok(records);
        }

        [HttpGet("assignedto")]
        public async Task<IActionResult> GetAssignedToByWorkflowAsync(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var assignedTo = await _svc.GetAssignedToByWorkflowAsync(
                valuationId.ToString(),
                vehicleNumber,
                applicantContact);

            if (assignedTo == null)
                return NotFound();

            return Ok(assignedTo);
        }

        [HttpGet("filter/states")]
        public async Task<IActionResult> FilterByStatesAsync([FromQuery] List<string> stateKeys)
        {
            if (stateKeys == null || stateKeys.Count == 0)
                return BadRequest("State keys must be provided.");

            var results = await _svc.FilterByStatesAsync(stateKeys);
            if (results == null || results.Count == 0)
                return NotFound("No workflows found for the specified states.");

            return Ok(results);
        }

        [HttpGet("filter/districts")]
        public async Task<IActionResult> FilterByDistrictsAsync([FromQuery] List<string> districtKeys)
        {
            if (districtKeys == null || districtKeys.Count == 0)
                return BadRequest("District keys must be provided.");

            var results = await _svc.FilterByDistrictsAsync(districtKeys);
            if (results == null || results.Count == 0)
                return NotFound("No workflows found for the specified districts.");

            return Ok(results);
        }
    }
}

