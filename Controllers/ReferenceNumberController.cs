using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Services.Interfaces;

namespace Valuation.Api.Controllers
{
    /// <summary>
    /// The case's human-readable reference, e.g. VG-519499-K.
    ///
    /// Completing the stakeholder step already mints one in the background, but a
    /// background task cannot be awaited by the page that has just submitted. This
    /// endpoint is idempotent — it returns the stored reference, or assigns one if the
    /// background mint has not landed yet — so the portal can ask for it and always get
    /// the same answer.
    /// </summary>
    [ApiController]
    [Route("api/valuations/{valuationId:guid}/reference")]
    public class ReferenceNumberController : ControllerBase
    {
        private readonly IReferenceNumberService _refs;

        public ReferenceNumberController(IReferenceNumberService refs) => _refs = refs;

        [HttpGet]
        public async Task<IActionResult> Get(
            Guid valuationId,
            [FromQuery] string vehicleNumber,
            [FromQuery] string applicantContact,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(vehicleNumber) || string.IsNullOrWhiteSpace(applicantContact))
                return BadRequest("vehicleNumber and applicantContact are required.");

            var reference = await _refs.EnsureAsync(
                valuationId.ToString(), vehicleNumber,
                Uri.UnescapeDataString(applicantContact), ct);

            if (reference is null) return NotFound();
            return Ok(new { referenceNumber = reference });
        }
    }
}
