// Controllers/WorkflowController.cs
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Valuation.Api.Models;
using Valuation.Api.Services;
using Valuation.Api.Services.Interfaces;

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

            // Registration is done, so the case can be given the reference people will
            // quote for it. Same background shape as the photo read below: the submit
            // must not wait on a Cosmos round-trip, and the reference is looked up
            // again on every read rather than being held in the response.
            if (stepOrder == 1)
                StartReferenceMintInBackground(valuationId.ToString(), vehicleNumber,
                                               Uri.UnescapeDataString(applicantContact));

            // AVO is done, so the photo set is final — start reading it now rather
            // than leaving the cost and the half-minute wait to whoever opens QC
            // first. Deliberately not awaited: the submit must not get slower, and
            // the reader stores its own answer. If this never lands, the QC page
            // still reads on open exactly as before.
            if (stepOrder == 3)
            {
                StartPhotoReadInBackground(valuationId.ToString(), vehicleNumber,
                                           Uri.UnescapeDataString(applicantContact));

                // The market range is generated from the vehicle details plus the AVO's
                // odometer, so this is the first moment it can be asked for. The portal
                // used to make this call itself, inside the submit pipeline and with its
                // failures swallowed — which is why a case could reach approval with no
                // range and nothing to say why.
                StartValuationInBackground(valuationId.ToString(), vehicleNumber,
                                           Uri.UnescapeDataString(applicantContact));
            }

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
    /// <summary>
    /// Assigns the case reference outside the request, for the same reasons as
    /// <see cref="StartPhotoReadInBackground"/>.
    ///
    /// Failure is swallowed: a case without a reference still works everywhere, and
    /// the PDF service assigns one on first report generation exactly as it did before.
    /// </summary>
    private void StartReferenceMintInBackground(string valuationId, string vehicleNumber, string applicantContact)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var refs = scope.ServiceProvider.GetRequiredService<IReferenceNumberService>();
                var assigned = await refs.EnsureAsync(valuationId, vehicleNumber, applicantContact, CancellationToken.None);
                _logger.LogInformation("Reference {Reference} ensured for {Valuation}.", assigned, valuationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reference mint failed for {Valuation}; the PDF service will assign one.", valuationId);
            }
        });
    }

    /// <summary>
    /// Generates the market range outside the request, for the same reasons as
    /// <see cref="StartPhotoReadInBackground"/>.
    ///
    /// Swallowed on failure because it is a warm-up: the approval page asks for the
    /// range itself, and that call now returns what is stored rather than paying for
    /// a fresh one, so a missed warm-up costs one slow page load rather than an
    /// empty range for the life of the case.
    /// </summary>
    private void StartValuationInBackground(string valuationId, string vehicleNumber, string applicantContact)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var valuation = scope.ServiceProvider.GetRequiredService<IVehicleValuationService>();
                await valuation.EnsureAsync(valuationId, vehicleNumber, applicantContact, false, CancellationToken.None);
                _logger.LogInformation("Market range warmed for {Valuation} after AVO submit.", valuationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Market range warm-up failed for {Valuation}; the approval page will ask again.", valuationId);
            }
        });
    }

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