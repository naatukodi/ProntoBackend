using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers;

[ApiController]
[Route("api/valuations/{id:guid}/inspection")]
public class InspectionController : ControllerBase
{
    private readonly IGetInspectionService _svc;
    public InspectionController(IGetInspectionService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<InspectionDetails>> Get(
        Guid id,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        var x = await _svc.GetInspectionAsync(id.ToString(), vehicleNumber, applicantContact);
        if (x == null)
            return Ok(new InspectionDetailsDto());
        return Ok(x);
    }

    [HttpPut, Consumes("multipart/form-data")]
    public async Task<IActionResult> Put(
        Guid id,
        [FromForm] InspectionDetailsDto dto,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        await _svc.UpdateInspectionAsync(id.ToString(), dto, vehicleNumber, applicantContact);
        return NoContent();
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

    [HttpDelete]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        await _svc.DeleteInspectionAsync(id.ToString(), vehicleNumber, applicantContact);
        return NoContent();
    }
}
