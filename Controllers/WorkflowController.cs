// Controllers/WorkflowController.cs
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Valuation.Api.Models;

[ApiController]
[Route("api/valuations/{valuationId:guid}/workflow")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _svc;
    public WorkflowController(IWorkflowService svc) => _svc = svc;

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
        int stepOrder)
    {
        try 
        {
            await _svc.CompleteStepAsync(valuationId.ToString(), vehicleNumber, applicantContact, stepOrder);
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