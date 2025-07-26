using System.Net;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;

namespace Valuation.Api.Services;

public class QualityControlService : IQualityControlService
{
    private readonly Container Container;
    private readonly IWorkflowTableService _workflowTableService;

    public QualityControlService(CosmosClient cosmosClient, IWorkflowTableService workflowTableService)
    {
        _workflowTableService = workflowTableService;
        // Use environment variables or default values for database and container names
        var databaseName = Environment.GetEnvironmentVariable("Cosmos:DatabaseId") ?? "ValuationsDb";
        var containerName = Environment.GetEnvironmentVariable("Cosmos:ContainerId") ?? "Valuations";
        Container = cosmosClient.GetContainer(databaseName, containerName);
    }
    private PartitionKey GetPk(string vehicleNumber, string applicantContact) =>
        new($"{vehicleNumber}|{applicantContact}");

    public async Task<QualityControl?> GetQualityControlAsync(string id, string vehicleNumber, string applicantContact)
    {
        var pk = GetPk(vehicleNumber, applicantContact);
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(id, pk);
            return resp.Resource.QualityControl ?? new QualityControl
            {
                OverallRating = "",
                ValuationAmount = 0,
                ChassisPunch = "",
                Remarks = null
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new QualityControl
            {
                OverallRating = "",
                ValuationAmount = 0,
                ChassisPunch = "",
                Remarks = null
            };
        }
    }

    public async Task UpdateQualityControlAsync(string id, QualityControlDto dto, string vehicleNumber, string applicantContact)
    {
        var pk = GetPk(vehicleNumber, applicantContact);

        // 1) Read
        var resp = await Container.ReadItemAsync<ValuationDocument>(id, pk);
        var doc = resp.Resource;

        // 2) Patch
        if (doc.QualityControl == null)
        {
            doc.QualityControl = new QualityControl();
        }

        if (dto.OverallRating != null)
            doc.QualityControl.OverallRating = dto.OverallRating;

        if (dto.ValuationAmount != 0)
            doc.QualityControl.ValuationAmount = dto.ValuationAmount;

        if (dto.ChassisPunch != null)
            doc.QualityControl.ChassisPunch = dto.ChassisPunch;

        if (dto.Remarks != null)
            doc.QualityControl.Remarks = dto.Remarks;

        // 3) Upsert
        await Container.UpsertItemAsync(doc, pk);

        // 4) update Workflow table
        await _workflowTableService.QualityControlWFUpdateAssignmentAsync(
            id,
            vehicleNumber,
            applicantContact,
            dto.AssignedTo ?? string.Empty,
            dto.AssignedToPhoneNumber ?? string.Empty,
            dto.AssignedToEmail ?? string.Empty,
            dto.AssignedToWhatsapp ?? string.Empty
        );

        await Task.Delay(1000); // Simulate delay for workflow update

        await _workflowTableService.UpdateCurrentWFAssignedToAsync(
            id,
            vehicleNumber,
            applicantContact,
            dto.AssignedTo ?? string.Empty,
            Uri.UnescapeDataString(dto.AssignedToPhoneNumber ?? string.Empty),
            Uri.UnescapeDataString(dto.AssignedToEmail ?? string.Empty),
            Uri.UnescapeDataString(dto.AssignedToWhatsapp ?? string.Empty)
        );
    }

    public async Task UpdateAssignmentAsync(string valuationId, string vehicleNumber, string applicantContact, string? assignedTo, string? assignedToPhoneNumber, string? assignedToEmail, string? assignedToWhatsapp)
    {
        var pk = GetPk(vehicleNumber, applicantContact);
        // 1) Read
        var resp = await Container.ReadItemAsync<ValuationDocument>(valuationId, pk);
        var doc = resp.Resource;

        // 2) Patch
        if (doc.QualityControl == null)
        {
            doc.QualityControl = new QualityControl();
        }

        if (doc.QualityControl.AssignedTo != assignedTo)
            doc.QualityControl.AssignedTo = assignedTo;

        if (doc.QualityControl.AssignedToPhoneNumber != assignedToPhoneNumber)
            doc.QualityControl.AssignedToPhoneNumber = Uri.UnescapeDataString(assignedToPhoneNumber ?? string.Empty);

        if (doc.QualityControl.AssignedToEmail != assignedToEmail)
            doc.QualityControl.AssignedToEmail = Uri.UnescapeDataString(assignedToEmail ?? string.Empty);

        if (doc.QualityControl.AssignedToWhatsapp != assignedToWhatsapp)
            doc.QualityControl.AssignedToWhatsapp = Uri.UnescapeDataString(assignedToWhatsapp ?? string.Empty);

        doc.QualityControl.UpdatedAt = DateTime.UtcNow;

        doc.AssignedTo = assignedTo;
        doc.AssignedToPhoneNumber = Uri.UnescapeDataString(assignedToPhoneNumber ?? string.Empty);
        doc.AssignedToEmail = Uri.UnescapeDataString(assignedToEmail ?? string.Empty);
        doc.AssignedToWhatsapp = Uri.UnescapeDataString(assignedToWhatsapp ?? string.Empty);

        // 3) Upsert
        await Container.UpsertItemAsync(doc, pk);
        // 4) update Workflow table
        await _workflowTableService.QualityControlWFUpdateAssignmentAsync(
            valuationId,
            vehicleNumber,
            applicantContact,
            assignedTo ?? string.Empty,
            Uri.UnescapeDataString(assignedToPhoneNumber ?? string.Empty),
            Uri.UnescapeDataString(assignedToEmail ?? string.Empty),
            Uri.UnescapeDataString(assignedToWhatsapp ?? string.Empty)
        );

        await Task.Delay(1000); // Simulate delay for workflow update

        await _workflowTableService.UpdateCurrentWFAssignedToAsync(
            valuationId,
            vehicleNumber,
            applicantContact,
            assignedTo ?? string.Empty,
            Uri.UnescapeDataString(assignedToPhoneNumber ?? string.Empty),
            Uri.UnescapeDataString(assignedToEmail ?? string.Empty),
            Uri.UnescapeDataString(assignedToWhatsapp ?? string.Empty)
        );
    }

    public async Task DeleteQualityControlAsync(string id, string vehicleNumber, string applicantContact)
    {
        var pk = new PartitionKey($"{vehicleNumber}:{applicantContact}");
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(id, pk);
            var doc = resp.Resource;
            doc.QualityControl = null;
            await Container.UpsertItemAsync(doc, pk);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignore
        }
    }
}
