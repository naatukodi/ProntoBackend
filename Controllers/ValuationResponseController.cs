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
        private readonly IValuationService _valuationService;
        private readonly IFinalReportPdfService _pdfService;

        public ValuationResponseController(IValuationResponseService svc, IValuationService valuationService, IFinalReportPdfService pdfService)
        {
            _svc = svc;
            _valuationService = valuationService;
            _pdfService = pdfService;
        }

        [HttpGet("FinalReport")]
        public async Task<ActionResult<ValuationDocument>> GetValuationDocument(
            Guid id,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var resp = await _valuationService.GetValuationDocumentAsync(
                id.ToString(),
                vehicleNumber,
                applicantContact);

            if (resp == null)
            {
                return NotFound();
            }

            return Ok(resp);
        }

        [HttpGet("FinalReport/pdf")]
        public async Task<IActionResult> GetFinalReportPdf(
        Guid id,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
        {
            // 1) Fetch the FinalReport object from your repository
            var report = await _valuationService.GetValuationDocumentAsync(id.ToString(), vehicleNumber, applicantContact);
            if (report == null)
                return NotFound();

            // 2) Generate PDF bytes
            byte[] pdfBytes = await _pdfService.GeneratePdfAsync(report);

            // 3) Return as a file result
            string fileName = $"{vehicleNumber}_{System.DateTime.UtcNow:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
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
