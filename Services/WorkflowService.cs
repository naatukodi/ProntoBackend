// Services/WorkflowService.cs
using Valuation.Api.Services;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Valuation.Api.Models;

public class WorkflowService : IWorkflowService
{
    private readonly Container _container;
    // Company this request belongs to; case listings are narrowed to it.
    private readonly IBrandContext _brand;

    public WorkflowService(CosmosClient client, IConfiguration cfg, IBrandContext brand)
    {
        _brand = brand;
        _container = client
           .GetDatabase(cfg["Cosmos:DatabaseId"])
           .GetContainer(cfg["Cosmos:ContainerId"]);
    }

    private PartitionKey Pk(string veh, string applicant)
        => new($"{veh}|{applicant}");

    public async Task<List<WorkflowStep>?> GetAsync(string id, string veh, string appl)
    {
        try
        {
            var resp = await _container.ReadItemAsync<ValuationDocument>(id, Pk(veh, appl));
            return resp.Resource.Workflow;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<ValuationDocument> LoadDoc(string id, PartitionKey pk)
    {
        try
        {
            var resp = await _container.ReadItemAsync<ValuationDocument>(id, pk);
            return resp.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new Exception($"Database Error: Valuation Document {id} could not be found with Partition Key {pk}");
        }
    }

    public async Task StartStepAsync(string id, string veh, string appl, int stepOrder)
    {
        var pk = Pk(veh, appl);
        var doc = await LoadDoc(id, pk);

        if (doc.Workflow == null) throw new Exception("Workflow array is completely missing for this case.");

        if (stepOrder > 1)
        {
            var prev = doc.Workflow.FirstOrDefault(s => s.StepOrder == stepOrder - 1);
            if (prev == null || prev.Status != "Completed")
                throw new Exception($"Sequence Error: Cannot start step {stepOrder} before completing step {stepOrder - 1}");
        }

        var step = doc.Workflow.FirstOrDefault(s => s.StepOrder == stepOrder)
                   ?? throw new Exception($"Step {stepOrder} is not defined in the database workflow array.");

        step.Status = "InProgress";
        step.StartedAt = DateTime.UtcNow;

        await _container.UpsertItemAsync(doc, pk);
    }

    public async Task CompleteStepAsync(string id, string veh, string appl, int stepOrder, string? approvedBy = null)
    {
        var pk = Pk(veh, appl);
        var doc = await LoadDoc(id, pk);

        if (doc.Workflow == null) throw new Exception("Workflow array is completely missing for this case.");

        var step = doc.Workflow.FirstOrDefault(s => s.StepOrder == stepOrder)
                   ?? throw new Exception($"Step {stepOrder} is not defined in the database workflow array.");

        if (step.Status != "InProgress")
            throw new Exception($"Cannot complete step {stepOrder} because its current status is '{step.Status}', not 'InProgress'.");

        step.Status = "Completed";
        step.CompletedAt = DateTime.UtcNow;

        if (stepOrder == 5 && !string.IsNullOrWhiteSpace(approvedBy))
        {
            doc.CompletedBy = approvedBy;
            doc.CompletedAt = DateTime.UtcNow;
        }

        await _container.UpsertItemAsync(doc, pk);
    }

    public async Task RejectStepAsync(string id, string veh, string appl, int stepOrder)
    {
        var pk = Pk(veh, appl);
        var doc = await LoadDoc(id, pk);

        if (doc.Workflow == null) throw new Exception("Workflow array is completely missing for this case.");

        var step = doc.Workflow.FirstOrDefault(s => s.StepOrder == stepOrder)
                   ?? throw new Exception($"Step {stepOrder} is not defined in the database workflow array.");
                   
        if (step.Status != "InProgress")
            throw new Exception($"Cannot reject step {stepOrder} because its current status is '{step.Status}', not 'InProgress'.");

        step.Status = "Rejected";
        step.CompletedAt = DateTime.UtcNow;

        if (stepOrder > 1)
        {
            var prev = doc.Workflow.FirstOrDefault(s => s.StepOrder == stepOrder - 1)
                       ?? throw new Exception($"Previous step {stepOrder - 1} not defined");
            prev.Status = "InProgress";
            prev.StartedAt = DateTime.UtcNow;
        }

        await _container.UpsertItemAsync(doc, pk);
    }

    public async Task DeleteAsync(string id, string veh, string appl)
    {
        var pk = Pk(veh, appl);
        var doc = await LoadDoc(id, pk);
        doc.Workflow = null;
        await _container.UpsertItemAsync(doc, pk);
    }

    public async Task<List<WorkflowModel>> GetCompletedCasesAsync()
    {
        var results = new List<WorkflowModel>();

        // Vehga and Pronto cases must never appear in one another's lists.
        var sql = "SELECT * FROM c WHERE EXISTS(" +
                  "SELECT VALUE wf FROM wf IN c.Workflow " +
                  "WHERE wf.StepOrder = 5 AND wf.Status = 'Completed'" +
                  ")";
        if (!_brand.IsUnscoped) sql += $" AND {BrandContext.SqlFilter}";

        var query = new Microsoft.Azure.Cosmos.QueryDefinition(sql);
        if (!_brand.IsUnscoped) query = query.WithParameter(BrandContext.SqlParam, _brand.Current);

        using var iter = _container.GetItemQueryIterator<Valuation.Api.Models.ValuationDocument>(query);
        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync();
            foreach (var doc in page)
            {
                var step5 = doc.Workflow?.FirstOrDefault(w => w.StepOrder == 5);
                results.Add(new Valuation.Api.Models.WorkflowModel
                {
                    ValuationId = doc.id,
                    VehicleNumber = doc.VehicleNumber ?? "",
                    ApplicantName = doc.Stakeholder?.Applicant?.Name ?? "",
                    ApplicantContact = doc.ApplicantContact ?? "",
                    Workflow = "FinalReport",
                    WorkflowStepOrder = 5,
                    Status = "Completed",
                    CreatedAt = doc.CreatedAt,
                    CompletedAt = step5?.CompletedAt ?? doc.CompletedAt,
                    AssignedTo = doc.AssignedTo,
                    AssignedToPhoneNumber = doc.AssignedToPhoneNumber,
                    AssignedToEmail = doc.AssignedToEmail,
                    AssignedToWhatsapp = doc.AssignedToWhatsapp,
                    RedFlag = doc.RedFlag,
                    Remarks = doc.Remarks,
                    Name = doc.Stakeholder?.Name ?? "",
                    ValuationType = doc.Stakeholder?.ValuationType ?? ""
                });
            }
        }

        return results;
    }
}