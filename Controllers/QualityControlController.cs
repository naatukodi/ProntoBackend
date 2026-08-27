using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

// Controllers/QualityControlController.cs
[ApiController]
[Route("api/valuations/{id:guid}/qualitycontrol")]
public class QualityControlController : ControllerBase
{
    private readonly IQualityControlService _svc;
    private readonly IQcVisionAuditService _vision;

    public QualityControlController(IQualityControlService svc, IQcVisionAuditService vision)
    {
        _svc = svc;
        _vision = vision;
    }

    /// <summary>
    /// Reads the case's inspection photos and returns checklist verdicts with the
    /// evidence behind each one.
    ///
    /// Called automatically when the QC page opens. The first call for a photo set
    /// reads the images and stores the answer; later calls return that stored answer
    /// until the photos change, so opening a case repeatedly costs nothing. Pass
    /// force=true to read again anyway.
    ///
    /// Nothing is written to the QC form — the reviewer reviews the suggestions and
    /// saves as usual, so a reading can never overwrite a human verdict on its own.
    /// </summary>
    [HttpPost("ai-photo-audit")]
    public async Task<ActionResult<QcAiAuditDto>> RunPhotoAudit(
        Guid id,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact,
        CancellationToken ct,
        [FromQuery] bool force = false)
    {
        if (string.IsNullOrWhiteSpace(vehicleNumber) || string.IsNullOrWhiteSpace(applicantContact))
            return BadRequest(new { message = "vehicleNumber and applicantContact are required." });

        var result = await _vision.AuditAsync(id.ToString(), vehicleNumber, applicantContact, force, ct);

        // An audit that could not run comes back 200 with an Error set rather than a
        // failure status: the page needs to show "not verified" on every card, which
        // is a normal outcome, not a broken request.
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<QualityControl>> Get(
        Guid id,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        var qc = await _svc.GetQualityControlAsync(id.ToString(), vehicleNumber, applicantContact);
        if (qc == null)
            return Ok(new QualityControlDto());
        return Ok(qc);
    }

    [HttpPost("assignment")]
    public async Task<IActionResult> UpdateAssignment(
        Guid id,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact,
        [FromQuery] string? assignedTo = null,
        [FromQuery] string? assignedToPhoneNumber = null,
        [FromQuery] string? assignedToEmail = null,
        [FromQuery] string? assignedToWhatsapp = null)
    {
        await _svc.UpdateAssignmentAsync(id.ToString(), vehicleNumber, applicantContact, assignedTo, assignedToPhoneNumber, assignedToEmail, assignedToWhatsapp);
        return NoContent();
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(
        Guid id,
        [FromBody] QualityControlDto dto,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        await _svc.UpdateQualityControlAsync(id.ToString(), dto, vehicleNumber, applicantContact);
        var updatedQc = await _svc.GetQualityControlAsync(id.ToString(), vehicleNumber, applicantContact);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        await _svc.DeleteQualityControlAsync(id.ToString(), vehicleNumber, applicantContact);
        return NoContent();
    }
}
