// src/Valuation.Api/Services/ValuationResponseService.cs
using System.Net;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public class ValuationResponseService : IValuationResponseService
    {
        private readonly Container _container;
        private readonly IWorkflowTableService _workflowTableService;

        public ValuationResponseService(CosmosClient cosmosClient, IConfiguration configuration, IWorkflowTableService workflowTableService)
        {
            // Read database & container from environment variables or appsettings.json
            var databaseName = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
            var containerName = configuration["Cosmos:ContainerId"] ?? "Valuations";

            _container = cosmosClient.GetContainer(databaseName, containerName);
            _workflowTableService = workflowTableService;
        }

        private PartitionKey GetPartitionKey(string vehicleNumber, string applicantContact) =>
            new PartitionKey($"{vehicleNumber}|{applicantContact}");

        public async Task<ValuationResponse?> GetValuationResponseAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact)
        {
            var pk = GetPartitionKey(vehicleNumber, applicantContact);

            try
            {
                // Read the full document, then extract its ValuationResponse
                var response = await _container.ReadItemAsync<ValuationDocument>(
                    id: valuationId,
                    partitionKey: pk);

                return response.Resource.ValuationResponse;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Document doesn’t exist → return null (no ValuationResponse)
                return null;
            }
        }

        public async Task UpdateValuationResponseAsync(
            string valuationId,
            ValuationResponse dto,
            string vehicleNumber,
            string applicantContact)
        {
            var pk = GetPartitionKey(vehicleNumber, applicantContact);

            ValuationDocument doc;
            try
            {
                // 1) Try to read existing document
                var readResp = await _container.ReadItemAsync<ValuationDocument>(
                    id: valuationId,
                    partitionKey: pk);
                doc = readResp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // 2) If not found, create a fresh document shell
                doc = new ValuationDocument
                {
                    id = valuationId,
                    CompositeKey = $"{vehicleNumber}|{applicantContact}",
                    VehicleNumber = vehicleNumber,
                    ApplicantContact = applicantContact,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    Workflow = new System.Collections.Generic.List<WorkflowStep>()
                    {
                        // If you need to initialize workflow, do so here (optional)
                    },
                    // All other sub-documents (like QualityControl) remain null
                    ValuationResponse = null
                };
            }

            // 3) Assign or overwrite the ValuationResponse sub‐document
            doc.ValuationResponse = new ValuationResponse
            {
                RawResponse = dto.RawResponse,
                LowRange = dto.LowRange,
                MidRange = dto.MidRange,
                HighRange = dto.HighRange
            };

            // 4) Upsert the document (create or replace)
            await _container.UpsertItemAsync(doc, pk);
        }

        public async Task UpdateAssignmentAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact,
            string? assignedTo = null,
            string? assignedToPhoneNumber = null,
            string? assignedToEmail = null,
            string? assignedToWhatsapp = null)
        {
            var pk = GetPartitionKey(vehicleNumber, applicantContact);

            // 1) Read existing document
            var readResp = await _container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: pk);
            var doc = readResp.Resource;

            // 2) Update assignment fields
            if (doc.ValuationResponse == null)
            {
                doc.ValuationResponse = new ValuationResponse();
            }

            // Assign values
            if (doc.AssignedTo != assignedTo)
                doc.AssignedTo = assignedTo;
            if (doc.AssignedToPhoneNumber != assignedToPhoneNumber)
                doc.AssignedToPhoneNumber = Uri.UnescapeDataString(assignedToPhoneNumber ?? string.Empty);
            if (doc.AssignedToEmail != assignedToEmail)
                doc.AssignedToEmail = Uri.UnescapeDataString(assignedToEmail ?? string.Empty);
            if (doc.AssignedToWhatsapp != assignedToWhatsapp)
                doc.AssignedToWhatsapp = Uri.UnescapeDataString(assignedToWhatsapp ?? string.Empty);
            doc.UpdatedAt = DateTime.UtcNow;

            // 3) Upsert back to Cosmos
            await _container.UpsertItemAsync(doc, pk);

            // 4) Update workflow table (if applicable)
            // This is optional and depends on your workflow logic
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

        public async Task DeleteValuationResponseAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact)
        {
            var pk = GetPartitionKey(vehicleNumber, applicantContact);

            try
            {
                // 1) Read existing document
                var readResp = await _container.ReadItemAsync<ValuationDocument>(
                    id: valuationId,
                    partitionKey: pk);
                var doc = readResp.Resource;

                // 2) Remove the ValuationResponse sub-document
                doc.ValuationResponse = null;

                // 3) Upsert back (so the document is not fully deleted—only the sub-field is cleared)
                await _container.UpsertItemAsync(doc, pk);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Document does not exist → nothing to delete
            }
        }
    }
}
