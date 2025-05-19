// Services/WorkflowService.cs
using Microsoft.Azure.Cosmos;
using System.Net;
using Valuation.Api.Models;

public class WorkflowService : IWorkflowService
{
    private readonly Container _container;
    public WorkflowService(CosmosClient client, IConfiguration cfg)
    {
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
            throw new KeyNotFoundException($"Valuation {id} not found");
        }
    }

    public async Task StartStepAsync(string id, string veh, string appl, int stepOrder)
    {
        var pk = Pk(veh, appl);
        var doc = await LoadDoc(id, pk);

        // enforce sequence: prior step must be Completed (or this is step 1)
        if (stepOrder > 1)
        {
            var prev = doc.Workflow?.FirstOrDefault(s => s.StepOrder == stepOrder - 1);
            if (prev == null || prev.Status != "Completed")
                throw new InvalidOperationException($"Cannot start step {stepOrder} before completing step {stepOrder - 1}");
        }

        var step = doc.Workflow!.FirstOrDefault(s => s.StepOrder == stepOrder)
                   ?? throw new KeyNotFoundException($"Step {stepOrder} not defined");
        step.Status = "InProgress";
        step.StartedAt = DateTime.UtcNow;

        await _container.UpsertItemAsync(doc, pk);
    }

    public async Task CompleteStepAsync(string id, string veh, string appl, int stepOrder)
    {
        var pk = Pk(veh, appl);
        var doc = await LoadDoc(id, pk);

        var step = doc.Workflow!.FirstOrDefault(s => s.StepOrder == stepOrder)
                   ?? throw new KeyNotFoundException($"Step {stepOrder} not defined");
        if (step.Status != "InProgress")
            throw new InvalidOperationException($"Cannot complete step {stepOrder} which is not InProgress");

        step.Status = "Completed";
        step.CompletedAt = DateTime.UtcNow;

        await _container.UpsertItemAsync(doc, pk);
    }

    public async Task DeleteAsync(string id, string veh, string appl)
    {
        var pk = Pk(veh, appl);
        var doc = await LoadDoc(id, pk);
        doc.Workflow = null;
        await _container.UpsertItemAsync(doc, pk);
    }
}
