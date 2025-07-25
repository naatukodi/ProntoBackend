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

            WorkflowEntity entity;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                entity = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: create new entity
                entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    CreatedAt = DateTime.UtcNow
                };
            }

            // Only update fields that are not null in dto (for reference types) or have value (for value types)
            if (dto.VehicleNumber != null) entity.VehicleNumber = dto.VehicleNumber;
            if (dto.ApplicantName != null) entity.ApplicantName = dto.ApplicantName;
            if (dto.ApplicantContact != null) entity.ApplicantContact = dto.ApplicantContact;
            if (dto.Workflow != null) entity.Workflow = dto.Workflow;
            if (dto.WorkflowStepOrder != 0) entity.WorkflowStepOrder = dto.WorkflowStepOrder;
            if (dto.Status != null) entity.Status = dto.Status;
            if (dto.CompletedAt.HasValue) entity.CompletedAt = dto.CompletedAt;
            if (dto.AssignedTo != null) entity.AssignedTo = dto.AssignedTo;
            if (dto.AssignedToPhoneNumber != null) entity.AssignedToPhoneNumber = dto.AssignedToPhoneNumber;
            if (dto.AssignedToEmail != null) entity.AssignedToEmail = dto.AssignedToEmail;
            if (dto.AssignedToWhatsapp != null) entity.AssignedToWhatsapp = dto.AssignedToWhatsapp;
            if (dto.StakeholderAssignedTo != null) entity.StakeholderAssignedTo = dto.StakeholderAssignedTo;
            if (dto.StakeholderAssignedToPhoneNumber != null) entity.StakeholderAssignedToPhoneNumber = dto.StakeholderAssignedToPhoneNumber;
            if (dto.StakeholderAssignedToEmail != null) entity.StakeholderAssignedToEmail = dto.StakeholderAssignedToEmail;
            if (dto.StakeholderAssignedToWhatsapp != null) entity.StakeholderAssignedToWhatsapp = dto.StakeholderAssignedToWhatsapp;
            if (dto.BackEndAssignedTo != null) entity.BackEndAssignedTo = dto.BackEndAssignedTo;
            if (dto.BackEndAssignedToPhoneNumber != null) entity.BackEndAssignedToPhoneNumber = dto.BackEndAssignedToPhoneNumber;
            if (dto.BackEndAssignedToEmail != null) entity.BackEndAssignedToEmail = dto.BackEndAssignedToEmail;
            if (dto.BackEndAssignedToWhatsapp != null) entity.BackEndAssignedToWhatsapp = dto.BackEndAssignedToWhatsapp;
            if (dto.AVOAssignedTo != null) entity.AVOAssignedTo = dto.AVOAssignedTo;
            if (dto.AVOAssignedToPhoneNumber != null) entity.AVOAssignedToPhoneNumber = dto.AVOAssignedToPhoneNumber;
            if (dto.AVOAssignedToEmail != null) entity.AVOAssignedToEmail = dto.AVOAssignedToEmail;
            if (dto.AVOAssignedToWhatsapp != null) entity.AVOAssignedToWhatsapp = dto.AVOAssignedToWhatsapp;
            if (dto.QualityControlAssignedTo != null) entity.QualityControlAssignedTo = dto.QualityControlAssignedTo;
            if (dto.QualityControlAssignedToPhoneNumber != null) entity.QualityControlAssignedToPhoneNumber = dto.QualityControlAssignedToPhoneNumber;
            if (dto.QualityControlAssignedToEmail != null) entity.QualityControlAssignedToEmail = dto.QualityControlAssignedToEmail;
            if (dto.QualityControlAssignedToWhatsapp != null) entity.QualityControlAssignedToWhatsapp = dto.QualityControlAssignedToWhatsapp;
            if (dto.FinalReportAssignedTo != null) entity.FinalReportAssignedTo = dto.FinalReportAssignedTo;
            if (dto.FinalReportAssignedToPhoneNumber != null) entity.FinalReportAssignedToPhoneNumber = dto.FinalReportAssignedToPhoneNumber;
            if (dto.FinalReportAssignedToEmail != null) entity.FinalReportAssignedToEmail = dto.FinalReportAssignedToEmail;
            if (dto.FinalReportAssignedToWhatsapp != null) entity.FinalReportAssignedToWhatsapp = dto.FinalReportAssignedToWhatsapp;
            if (dto.Location != null) entity.Location = dto.Location;
            if (dto.RedFlag != null) entity.RedFlag = dto.RedFlag;
            if (dto.Remarks != null) entity.Remarks = dto.Remarks;
            if (dto.AssignedToPhoneNumber != null) entity.AssignedToPhoneNumber = dto.AssignedToPhoneNumber;
            if (dto.AssignedToEmail != null) entity.AssignedToEmail = dto.AssignedToEmail;
            if (dto.AssignedToWhatsapp != null) entity.AssignedToWhatsapp = dto.AssignedToWhatsapp;
            if (dto.Name != null) entity.Name = dto.Name;
            if (dto.ValuationType != null) entity.ValuationType = dto.ValuationType;

            entity.UpdatedAt = DateTime.UtcNow;

            // Upsert (insert or merge)
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
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
                        AssignedToWhatsapp = entity.AssignedToWhatsapp,
                        Name = entity.Name,
                        ValuationType = entity.ValuationType
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

        public async Task UpdateCurrentWFAssignedToAsync(
            string valuationId, string vehicleNumber, string applicantContact, string? assignedTo = null,
            string? assignedToPhoneNumber = null,
            string? assignedToEmail = null, string? assignedToWhatsapp = null)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                // Update assignment fields
                entity.AssignedTo = assignedTo ?? entity.AssignedTo;
                entity.AssignedToPhoneNumber = assignedToPhoneNumber ?? entity.AssignedToPhoneNumber;
                entity.AssignedToEmail = assignedToEmail ?? entity.AssignedToEmail;
                entity.AssignedToWhatsapp = assignedToWhatsapp ?? entity.AssignedToWhatsapp;
                entity.UpdatedAt = DateTime.UtcNow;

                // Upsert (insert or merge)
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: create new entity with assignment fields
                var entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    VehicleNumber = vehicleNumber,
                    ApplicantContact = applicantContact,
                    AssignedTo = assignedTo ?? "",
                    AssignedToPhoneNumber = assignedToPhoneNumber ?? "",
                    AssignedToEmail = assignedToEmail ?? "",
                    AssignedToWhatsapp = assignedToWhatsapp ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
        }

        public async Task StakeholderWFUpdateAssignmentAsync(
                    string ValuationId,
                    string VehicleNumber,
                    string ApplicantContact,
                    string? AssignedTo,
                    string? AssignedToPhoneNumber,
                    string? AssignedToEmail,
                    string? AssignedToWhatsapp)
        {
            var partitionKey = $"{VehicleNumber}|{ApplicantContact}";
            var rowKey = ValuationId;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                // Update assignment fields
                entity.StakeholderAssignedTo = AssignedTo ?? "";
                entity.StakeholderAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.StakeholderAssignedToEmail = AssignedToEmail ?? "";
                entity.StakeholderAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                // Upsert (insert or merge)
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: create new entity with assignment fields
                var entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    VehicleNumber = VehicleNumber,
                    ApplicantContact = ApplicantContact,
                    StakeholderAssignedTo = AssignedTo ?? "",
                    StakeholderAssignedToPhoneNumber = AssignedToPhoneNumber ?? "",
                    StakeholderAssignedToEmail = AssignedToEmail ?? "",
                    StakeholderAssignedToWhatsapp = AssignedToWhatsapp ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
        }

        public async Task BackendWFUpdateAssignmentAsync(
                    string ValuationId,
                    string VehicleNumber,
                    string ApplicantContact,
                    string? AssignedTo,
                    string? AssignedToPhoneNumber,
                    string? AssignedToEmail,
                    string? AssignedToWhatsapp)
        {
            var partitionKey = $"{VehicleNumber}|{ApplicantContact}";
            var rowKey = ValuationId;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                // Update assignment fields
                entity.BackEndAssignedTo = AssignedTo ?? "";
                entity.BackEndAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.BackEndAssignedToEmail = AssignedToEmail ?? "";
                entity.BackEndAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                // Upsert (insert or merge)
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: create new entity with assignment fields
                var entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    VehicleNumber = VehicleNumber,
                    ApplicantContact = ApplicantContact,
                    BackEndAssignedTo = AssignedTo ?? "",
                    BackEndAssignedToPhoneNumber = AssignedToPhoneNumber ?? "",
                    BackEndAssignedToEmail = AssignedToEmail ?? "",
                    BackEndAssignedToWhatsapp = AssignedToWhatsapp ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
        }

        public async Task AVOWFUpdateAssignmentAsync(
                    string ValuationId,
                    string VehicleNumber,
                    string ApplicantContact,
                    string? AssignedTo,
                    string? AssignedToPhoneNumber,
                    string? AssignedToEmail,
                    string? AssignedToWhatsapp)
        {
            var partitionKey = $"{VehicleNumber}|{ApplicantContact}";
            var rowKey = ValuationId;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                // Update assignment fields
                entity.AVOAssignedTo = AssignedTo ?? "";
                entity.AVOAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.AVOAssignedToEmail = AssignedToEmail ?? "";
                entity.AVOAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                // Upsert (insert or merge)
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: create new entity with assignment fields
                var entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    VehicleNumber = VehicleNumber,
                    ApplicantContact = ApplicantContact,
                    AVOAssignedTo = AssignedTo ?? "",
                    AVOAssignedToPhoneNumber = AssignedToPhoneNumber ?? "",
                    AVOAssignedToEmail = AssignedToEmail ?? "",
                    AVOAssignedToWhatsapp = AssignedToWhatsapp ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
        }

        public async Task QualityControlWFUpdateAssignmentAsync(
                    string ValuationId,
                    string VehicleNumber,
                    string ApplicantContact,
                    string? AssignedTo,
                    string? AssignedToPhoneNumber,
                    string? AssignedToEmail,
                    string? AssignedToWhatsapp)
        {
            var partitionKey = $"{VehicleNumber}|{ApplicantContact}";
            var rowKey = ValuationId;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                // Update assignment fields
                entity.QualityControlAssignedTo = AssignedTo ?? "";
                entity.QualityControlAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.QualityControlAssignedToEmail = AssignedToEmail ?? "";
                entity.QualityControlAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                // Upsert (insert or merge)
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: create new entity with assignment fields
                var entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    VehicleNumber = VehicleNumber,
                    ApplicantContact = ApplicantContact,
                    QualityControlAssignedTo = AssignedTo ?? "",
                    QualityControlAssignedToPhoneNumber = AssignedToPhoneNumber ?? "",
                    QualityControlAssignedToEmail = AssignedToEmail ?? "",
                    QualityControlAssignedToWhatsapp = AssignedToWhatsapp ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
        }

        public async Task FinalReportWFUpdateAssignmentAsync(
                    string ValuationId,
                    string VehicleNumber,
                    string ApplicantContact,
                    string? AssignedTo,
                    string? AssignedToPhoneNumber,
                    string? AssignedToEmail,
                    string? AssignedToWhatsapp)
        {
            var partitionKey = $"{VehicleNumber}|{ApplicantContact}";
            var rowKey = ValuationId;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                // Update assignment fields
                entity.FinalReportAssignedTo = AssignedTo ?? "";
                entity.FinalReportAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.FinalReportAssignedToEmail = AssignedToEmail ?? "";
                entity.FinalReportAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                // Upsert (insert or merge)
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not found: create new entity with assignment fields
                var entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    VehicleNumber = VehicleNumber,
                    ApplicantContact = ApplicantContact,
                    FinalReportAssignedTo = AssignedTo ?? "",
                    FinalReportAssignedToPhoneNumber = AssignedToPhoneNumber ?? "",
                    FinalReportAssignedToEmail = AssignedToEmail ?? "",
                    FinalReportAssignedToWhatsapp = AssignedToWhatsapp ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
        }

        public async Task CompleteFinalReportWFAsync(
            string valuationId, string vehicleNumber, string applicantContact)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                // Fetch existing entity
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                // Update status to Completed
                entity.Status = "Completed";
                entity.CompletedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;

                // Upsert (insert or merge)
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new InvalidOperationException("Workflow not found for completion", ex);
            }
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
                    AssignedToWhatsapp = e.AssignedToWhatsapp,
                    Name = e.Name,
                    ValuationType = e.ValuationType
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
