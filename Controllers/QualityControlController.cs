using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

// Controllers/QualityControlController.cs
[ApiController]
[Route("api/valuations/{id:guid}/qualitycontrol")]
public class QualityControlController : ControllerBase
{
    private readonly IQualityControlService _svc;
    public QualityControlController(IQualityControlService svc) => _svc = svc;

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
