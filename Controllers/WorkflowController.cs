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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(IWorkflowService svc, IWorkflowTableService tableSvc,
                              IServiceScopeFactory scopeFactory, ILogger<WorkflowController> logger)
    {
        _svc = svc;
        _tableSvc = tableSvc;
        _scopeFactory = scopeFactory;
        _logger = logger;
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

            // AVO is done, so the photo set is final — start reading it now rather
            // than leaving the cost and the half-minute wait to whoever opens QC
            // first. Deliberately not awaited: the submit must not get slower, and
            // the reader stores its own answer. If this never lands, the QC page
            // still reads on open exactly as before.
            if (stepOrder == 3)
                StartPhotoReadInBackground(valuationId.ToString(), vehicleNumber,
                                           Uri.UnescapeDataString(applicantContact));

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

    /// <summary>
    /// Kicks off the photo reading outside the request.
    ///
    /// Its own scope, because the request's scoped services are disposed the
    /// moment the response is written. CancellationToken.None for the same
    /// reason: the client navigating away from the AVO page must not abort a
    /// read that the QC page is about to want.
    ///
    /// Failure is swallowed on purpose — this is a warm-up, and the QC page
    /// reads on open when there is nothing stored.
    /// </summary>
    private void StartPhotoReadInBackground(string valuationId, string vehicleNumber, string applicantContact)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var vision = scope.ServiceProvider.GetRequiredService<IQcVisionAuditService>();
                await vision.AuditAsync(valuationId, vehicleNumber, applicantContact, false, CancellationToken.None);
                _logger.LogInformation("Photo read warmed for {Valuation} after AVO submit.", valuationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Photo read after AVO submit failed for {Valuation}; QC will read on open.", valuationId);
            }
        });
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