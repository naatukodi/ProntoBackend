// Controllers/WorkflowController.cs
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Valuation.Api.Models;
using Valuation.Api.Services;

[ApiController]
[Route("api/valuations/{valuationId:guid}/workflow")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _svc;
    private readonly IWorkflowTableService _tableSvc;

    public WorkflowController(IWorkflowService svc, IWorkflowTableService tableSvc)
    {
        _svc = svc;
        _tableSvc = tableSvc;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkflowStep>>> Get(
        Guid valuationId,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        var wf = await _svc.GetAsync(
            valuationId.ToString(), vehicleNumber, applicantContact);
        if (wf == null) return NotFound();
        return Ok(wf);
    }

    [HttpPost("{stepOrder}/start")]
    public async Task<IActionResult> Start(
        Guid valuationId,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact,
        int stepOrder)
    {
        try
        {
            await _svc.StartStepAsync(valuationId.ToString(), vehicleNumber, applicantContact, stepOrder);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message); // Sends the exact error to Flutter
        }
    }

    [HttpPost("{stepOrder}/complete")]
    public async Task<IActionResult> Complete(
        Guid valuationId,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact,
        int stepOrder,
        [FromQuery] string? approvedBy = null)
    {
        try
        {
            await _svc.CompleteStepAsync(valuationId.ToString(), vehicleNumber, applicantContact, stepOrder, approvedBy);

            if (stepOrder == 5)
            {
                var decodedContact = Uri.UnescapeDataString(applicantContact);
                await _tableSvc.CompleteFinalReportWFAsync(
                    valuationId.ToString(),
                    vehicleNumber,
                    decodedContact,
                    new AssignmentDto
                    {
                        AssignedTo = approvedBy ?? "",
                        AssignedToPhoneNumber = "",
                        AssignedToEmail = "",
                        AssignedToWhatsapp = ""
                    });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    [HttpPost("{stepOrder}/reject")]
    public async Task<IActionResult> Reject(
        Guid valuationId,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact,
        int stepOrder)
    {
        try 
        {
            await _svc.RejectStepAsync(valuationId.ToString(), vehicleNumber, applicantContact, stepOrder);
            return NoContent();
        } 
        catch (Exception ex) 
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(
        Guid valuationId,
        [FromQuery] string vehicleNumber,
        [FromQuery] string applicantContact)
    {
        try 
        {
            await _svc.DeleteAsync(valuationId.ToString(), vehicleNumber, applicantContact);
            return NoContent();
        } 
        catch (Exception ex) 
        {
            return BadRequest(ex.Message);
        }
    }
}