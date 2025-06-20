// Controllers/StakeholderController.cs
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

        [HttpGet]
        public async Task<ActionResult<Stakeholder>> GetStakeholder(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            var s = await _svc.GetAsync(
                valuationId.ToString(), vehicleNumber, applicantContact);
            if (s == null) return NotFound();
            return Ok(s);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteStakeholder(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact)
        {
            await _svc.DeleteAsync(
                valuationId.ToString(), vehicleNumber, applicantContact);
            return NoContent();
        }
    }

    [ApiController]
    [Route("api/valuations/{valuationId:guid}/stakeholder/documents")]
    public class StakeholderDocumentsController : ControllerBase
    {
        private readonly IStakeholderService _svc;
        public StakeholderDocumentsController(IStakeholderService svc) => _svc = svc;

        /// <summary>
        /// Upload stakeholder documents.
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> UploadDocuments(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            [FromForm] StakeholderDocumentsDto dto)
        {
            // Delegate into your existing UpdateDocumentsAsync,
            // passing along the files in the DTO.
            await _svc.UpdateDocumentsAsync(new StakeholderUpdateDto
            {
                ValuationId = valuationId.ToString(),
                VehicleNumber = vehicleNumber,
                ApplicantContact = applicantContact,
                RcFile = dto.RcFile,
                InsuranceFile = dto.InsuranceFile,
                OtherFiles = dto.OtherFiles
            });

            return NoContent();
        }
    }
}