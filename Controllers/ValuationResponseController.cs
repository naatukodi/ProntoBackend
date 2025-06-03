// src/Valuation.Api/Controllers/ValuationResponseController.cs

using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/valuations/{id:guid}/valuationresponse")]
    public class ValuationResponseController : ControllerBase
    {
        private readonly IValuationResponseService _svc;

        public ValuationResponseController(IValuationResponseService svc)
        {
            _svc = svc;
        }

        /// <summary>
        /// GET /api/valuations/{id}/valuationresponse?vehicleNumber=…&applicantContact=…
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ValuationResponse>> Get(
            Guid id,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var resp = await _svc.GetValuationResponseAsync(
                id.ToString(),
                vehicleNumber,
                applicantContact);

            if (resp == null)
            {
                return NotFound();
            }

            return Ok(resp);
        }

        /// <summary>
        /// PUT /api/valuations/{id}/valuationresponse?vehicleNumber=…&applicantContact=…
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> Upsert(
            Guid id,
            [FromBody] ValuationResponse dto,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            await _svc.UpdateValuationResponseAsync(
                id.ToString(),
                dto,
                vehicleNumber,
                applicantContact);

            return NoContent();
        }

        /// <summary>
        /// DELETE /api/valuations/{id}/valuationresponse?vehicleNumber=…&applicantContact=…
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> Delete(
            Guid id,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            await _svc.DeleteValuationResponseAsync(
                id.ToString(),
                vehicleNumber,
                applicantContact);

            return NoContent();
        }
    }
}
