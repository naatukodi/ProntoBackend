using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public class WorkflowTableService : IWorkflowTableService
    {
        private const string TableName = "Workflows";
        private readonly TableClient _tableClient;

        public WorkflowTableService(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            // Read connection string from appsettings.json
            var connString = configuration.GetConnectionString("TableStorage")
                             ?? throw new InvalidOperationException("TableStorage connection string not configured.");

            // Create a TableClient (client for the Workflows table)
            var serviceClient = new TableServiceClient(connString);
            _tableClient = serviceClient.GetTableClient(TableName);

            // Ensure the table exists (synchronous method)
            _tableClient.CreateIfNotExists();
        }

        public async Task UpdateAsync(WorkflowUpdateDto dto)
        {
            // Compute PartitionKey and RowKey
            var partitionKey = $"{dto.VehicleNumber}|{dto.ApplicantContact}";
            var rowKey = dto.ValuationId;

            // Try to fetch existing entity (to preserve CreatedAt if exists)
            DateTime createdAtUtc;
            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                // Existing: preserve CreatedAt
                createdAtUtc = response.Value.CreatedAt;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: mark new record
                createdAtUtc = DateTime.UtcNow;
            }

            // Build the TableEntity
            var entity = new WorkflowEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                VehicleNumber = dto.VehicleNumber,
                ApplicantName = dto.ApplicantName,
                ApplicantContact = dto.ApplicantContact,
                Workflow = dto.Workflow,
                WorkflowStepOrder = dto.WorkflowStepOrder,
                Status = dto.Status,
                CreatedAt = createdAtUtc,
                CompletedAt = dto.CompletedAt,
                AssignedTo = dto.AssignedTo,
                Location = dto.Location,
                RedFlag = dto.RedFlag,
                Remarks = dto.Remarks,
                AssignedToPhoneNumber = dto.AssignedToPhoneNumber,
                AssignedToEmail = dto.AssignedToEmail,
                AssignedToWhatsapp = dto.AssignedToWhatsapp
            };

            // Upsert (insert or replace)
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace).ConfigureAwait(false);
        }

        public async Task<List<WorkflowModel?>> GetWorkflowInProgressAsync()
        {
            var results = new List<WorkflowModel?>();

            try
            {
                // Query for the latest workflow step in progress
                await foreach (var entity in _tableClient.QueryAsync<WorkflowEntity>(
                    filter: $"Status eq 'InProgress'").ConfigureAwait(false))
                {
                    results.Add(new WorkflowModel
                    {
                        ValuationId = entity.RowKey,
                        VehicleNumber = entity.VehicleNumber,
                        ApplicantName = entity.ApplicantName,
                        ApplicantContact = entity.ApplicantContact,
                        Workflow = entity.Workflow,
                        WorkflowStepOrder = entity.WorkflowStepOrder,
                        Status = entity.Status,
                        CreatedAt = entity.CreatedAt,
                        CompletedAt = entity.CompletedAt,
                        AssignedTo = entity.AssignedTo,
                        Location = entity.Location,
                        RedFlag = entity.RedFlag,
                        Remarks = entity.Remarks,
                        AssignedToPhoneNumber = entity.AssignedToPhoneNumber,
                        AssignedToEmail = entity.AssignedToEmail,
                        AssignedToWhatsapp = entity.AssignedToWhatsapp
                    });
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // No records found, return empty list
            }
            catch (Exception ex)
            {
                // Handle other exceptions as needed
                throw new InvalidOperationException("Error querying workflow in progress", ex);
            }

            return results;
        }



        public async Task<WorkflowModel?> GetAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var e = response.Value;
                return new WorkflowModel
                {
                    ValuationId = e.RowKey,
                    VehicleNumber = e.VehicleNumber,
                    ApplicantName = e.ApplicantName,
                    ApplicantContact = e.ApplicantContact,
                    Workflow = e.Workflow,
                    WorkflowStepOrder = e.WorkflowStepOrder,
                    Status = e.Status,
                    CreatedAt = e.CreatedAt,
                    CompletedAt = e.CompletedAt,
                    AssignedTo = e.AssignedTo,
                    Location = e.Location,
                    RedFlag = e.RedFlag,
                        Remarks = e.Remarks,
                        AssignedToPhoneNumber = e.AssignedToPhoneNumber,
                        AssignedToEmail = e.AssignedToEmail,
                        AssignedToWhatsapp = e.AssignedToWhatsapp
                };
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found → return null
                return null;
            }
        }

        public async Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                await _tableClient.DeleteEntityAsync(partitionKey, rowKey).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Nothing to delete if not found
            }
        }
    }
}
