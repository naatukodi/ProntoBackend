using Azure;
using Azure.Data.Tables;
using Valuation.Api.Models;
// No System.Text.Json needed if we aren't parsing the column

namespace Valuation.Api.Services
{
    public class WorkflowTableService : IWorkflowTableService
    {
        private const string TableName = "Workflows";
        private const string CompletedTableName = "CompletedWorkflows";
        private const string LeadHistoryTableName = "LeadHistory";
        private const string UserTableName = "Users";

        private readonly TableClient _tableClient;
        private readonly TableClient _completedTableClient;
        private readonly TableClient _leadHistoryTableClient;
        private readonly TableClient _userTableClient;

        public WorkflowTableService(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            var connString = configuration.GetConnectionString("TableStorage")
                             ?? throw new InvalidOperationException("TableStorage connection string not configured.");

            var serviceClient = new TableServiceClient(connString);
            _tableClient = serviceClient.GetTableClient(TableName);
            _completedTableClient = serviceClient.GetTableClient(CompletedTableName);
            _leadHistoryTableClient = serviceClient.GetTableClient(LeadHistoryTableName);
            _userTableClient = serviceClient.GetTableClient(UserTableName);

            _tableClient.CreateIfNotExists();
            _completedTableClient.CreateIfNotExists();
            _leadHistoryTableClient.CreateIfNotExists();
            _userTableClient.CreateIfNotExists();
        }

        // ==============================================================================
        //  RETURN WORKFLOW STEP
        // ==============================================================================
        public async Task ReturnWorkflowStepAsync(WorkflowReturnDto returnDto)
        {
            var partitionKey = $"{returnDto.VehicleNumber}|{returnDto.ApplicantContact}";
            var rowKey = returnDto.ValuationId;

            // 1. Fetch Current Workflow Entity
            WorkflowEntity entity;
            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(partitionKey, rowKey);
                entity = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new Exception("Valuation not found.");
            }

            string nextStep = "";
            string nextAssigneeId = "";
            UserEntity? nextUser = null;

            // Normalize step name
            string currentStepNormalized = returnDto.CurrentStep;
            if (currentStepNormalized.Equals("QC", StringComparison.OrdinalIgnoreCase))
                currentStepNormalized = "QualityControl";

            // =========================================================
            // DETERMINE NEXT STEP & ASSIGNEE
            // =========================================================

            // SCENARIO A: Final Report -> Back to QualityControl
            if (currentStepNormalized == "FinalReport")
            {
                nextStep = "QualityControl";

                if (!string.IsNullOrEmpty(entity.QualityControlAssignedToPhoneNumber))
                {
                    nextAssigneeId = entity.QualityControlAssignedToPhoneNumber;
                }
                else
                {
                    var historyList = new List<LeadHistoryEntity>();
                    await foreach (var h in _leadHistoryTableClient.QueryAsync<LeadHistoryEntity>(
                        h => h.PartitionKey == returnDto.ValuationId && h.StatusTo == "FinalReport"))
                    {
                        historyList.Add(h);
                    }
                    var history = historyList.OrderByDescending(h => h.DateTime).FirstOrDefault();

                    if (history != null && !string.IsNullOrEmpty(history.PerformedByUserId))
                        nextAssigneeId = history.PerformedByUserId;
                    else
                        throw new Exception("Could not find the original QC user. Check data integrity.");
                }
            }
            // SCENARIO B: QualityControl -> Back to AVO or Backend
            else if (currentStepNormalized == "QualityControl")
            {
                if (string.IsNullOrEmpty(returnDto.TargetReturnStep))
                    throw new ArgumentException("Target step (AVO or Backend) is required.");

                nextStep = returnDto.TargetReturnStep;

                if (!string.IsNullOrEmpty(returnDto.OverrideAssigneeId))
                {
                    nextAssigneeId = returnDto.OverrideAssigneeId;
                }
                else if (nextStep == "AVO" && !string.IsNullOrEmpty(entity.AVOAssignedToPhoneNumber))
                {
                    nextAssigneeId = entity.AVOAssignedToPhoneNumber;
                }
                else if ((nextStep == "Backend" || nextStep == "BackEnd") && !string.IsNullOrEmpty(entity.BackEndAssignedToPhoneNumber))
                {
                    nextAssigneeId = entity.BackEndAssignedToPhoneNumber;
                }
                else
                {
                    var historyList = new List<LeadHistoryEntity>();
                    await foreach (var h in _leadHistoryTableClient.QueryAsync<LeadHistoryEntity>(h =>
                        h.PartitionKey == returnDto.ValuationId &&
                        (h.StatusTo == "QualityControl" || h.StatusTo == "QC") &&
                        h.StatusFrom == nextStep))
                    {
                        historyList.Add(h);
                    }
                    var lastAction = historyList.OrderByDescending(x => x.DateTime).FirstOrDefault();

                    if (lastAction != null && !string.IsNullOrEmpty(lastAction.PerformedByUserId))
                        nextAssigneeId = lastAction.PerformedByUserId;
                    else
                        throw new Exception($"No history found for {nextStep}. Please provide 'overrideAssigneeId' manually.");
                }
            }
            // SCENARIO C: AVO -> Back to Backend
            else if (currentStepNormalized == "AVO")
            {
                nextStep = "Backend";

                if (!string.IsNullOrEmpty(entity.BackEndAssignedToPhoneNumber))
                {
                    nextAssigneeId = entity.BackEndAssignedToPhoneNumber;
                }
                else
                {
                    var historyList = new List<LeadHistoryEntity>();
                    await foreach (var h in _leadHistoryTableClient.QueryAsync<LeadHistoryEntity>(h =>
                        h.PartitionKey == returnDto.ValuationId &&
                        h.StatusTo == "AVO" &&
                        (h.StatusFrom == "Backend" || h.StatusFrom == "BackEnd")))
                    {
                        historyList.Add(h);
                    }
                    var lastAction = historyList.OrderByDescending(x => x.DateTime).FirstOrDefault();

                    if (lastAction != null && !string.IsNullOrEmpty(lastAction.PerformedByUserId))
                        nextAssigneeId = lastAction.PerformedByUserId;
                    else
                        throw new Exception("No history found for Backend assignee. Cannot revert automatically.");
                }
            }

            // =========================================================
            // 3. FETCH USER
            // =========================================================
            if (nextUser == null && !string.IsNullOrEmpty(nextAssigneeId))
            {
                try
                {
                    var userResponse = await _userTableClient.GetEntityAsync<UserEntity>("Users", nextAssigneeId);
                    nextUser = userResponse.Value;

                    bool isServiceable = nextUser.ServiceStatus == "Serviceable" || nextUser.ServiceStatus == "Servicable";
                    if (!isServiceable)
                    {
                        throw new Exception($"The user ({nextUser.Name}) is currently '{nextUser.ServiceStatus}'. Please select a different user.");
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    throw new Exception($"User ID ({nextAssigneeId}) not found. Please provide valid 'overrideAssigneeId'.");
                }
            }

            if (nextUser == null) throw new Exception("Target user details could not be resolved.");

            // =========================================================
            // 4. UPDATE WORKFLOW ENTITY 
            // =========================================================

            entity.Status = "Returned";
            entity.RedFlag = "true";

            // UPDATED REMARKS
            entity.Remarks = $"RETURNED by {returnDto.CurrentStep}: {returnDto.ReturnReason}";

            entity.Workflow = nextStep;

            // Step Order Logic
            int targetOrder = 0;
            if (nextStep == "Stakeholder") targetOrder = 1;
            else if (nextStep == "Backend" || nextStep == "BackEnd") targetOrder = 2;
            else if (nextStep == "AVO") targetOrder = 3;
            else if (nextStep == "QualityControl" || nextStep == "QC") targetOrder = 4;
            else if (nextStep == "FinalReport") targetOrder = 5;

            if (targetOrder > 0) entity.WorkflowStepOrder = targetOrder;

            // Update Current Assignments
            entity.AssignedTo = nextUser.Name;
            entity.AssignedToPhoneNumber = nextUser.PhoneNumber;
            entity.AssignedToEmail = nextUser.Email;
            entity.AssignedToWhatsapp = nextUser.Whatsapp;

            // Update Specific Role Assignments (Preserve history)
            if (nextStep == "AVO")
            {
                entity.AVOAssignedTo = nextUser.Name;
                entity.AVOAssignedToPhoneNumber = nextUser.PhoneNumber;
                entity.AVOAssignedToEmail = nextUser.Email;
                entity.AVOAssignedToWhatsapp = nextUser.Whatsapp;
            }
            else if (nextStep == "Backend" || nextStep == "BackEnd")
            {
                entity.BackEndAssignedTo = nextUser.Name;
                entity.BackEndAssignedToPhoneNumber = nextUser.PhoneNumber;
                entity.BackEndAssignedToEmail = nextUser.Email;
                entity.BackEndAssignedToWhatsapp = nextUser.Whatsapp;
            }
            else if (nextStep == "QualityControl" || nextStep == "QC")
            {
                entity.QualityControlAssignedTo = nextUser.Name;
                entity.QualityControlAssignedToPhoneNumber = nextUser.PhoneNumber;
                entity.QualityControlAssignedToEmail = nextUser.Email;
                entity.QualityControlAssignedToWhatsapp = nextUser.Whatsapp;
            }

            entity.UpdatedAt = DateTime.UtcNow;
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);

            // =========================================================
            // 5. HISTORY
            // =========================================================
            var historyDto = new LeadHistoryDto
            {
                ValuationId = returnDto.ValuationId,
                DateTime = DateTime.UtcNow,
                Action = "Returned",
                StatusFrom = returnDto.CurrentStep,
                StatusTo = nextStep,
                Remarks = $"RETURNED: {returnDto.ReturnReason}",
                PerformedByUserId = returnDto.CurrentUserId,
                PerformedByUserName = returnDto.CurrentUserName,
                CurrentStatus = "Returned",
                StatusChange = true,
                StatusChangedDateTime = DateTime.UtcNow
            };

            await AddHistoryAsync(historyDto);
        }

        public async Task AddHistoryAsync(LeadHistoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ValuationId))
                throw new ArgumentException("ValuationId is required.", nameof(dto.ValuationId));

            string partitionKey = dto.ValuationId;
            string rowKey = DateTime.UtcNow.Ticks.ToString("D20");

            var history = await GetHistoryAsync(dto.ValuationId).ConfigureAwait(false);
            bool isFirstUpdate = history == null || !history.Any();

            var firstDateTime = isFirstUpdate
                ? DateTime.UtcNow
                : dto.FirstDateTime ?? DateTime.UtcNow;

            var totalTat = (int)Math.Max(0, Math.Floor((DateTime.UtcNow - firstDateTime).TotalDays));

            var statusChangeDateTime = dto.StatusChangedDateTime is DateTime dt && dt != default(DateTime)
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : (DateTime?)null;

            int currentTat = statusChangeDateTime.HasValue
                ? (int)Math.Max(0, Math.Floor((statusChangeDateTime.Value - firstDateTime.ToUniversalTime()).TotalDays))
                : 0;

            var entity = new LeadHistoryEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                DateTime = dto.DateTime == default ? DateTime.UtcNow : DateTime.SpecifyKind(dto.DateTime, DateTimeKind.Utc),
                FirstUpdate = isFirstUpdate ? true : dto.FirstUpdate,
                FirstDateTime = DateTime.SpecifyKind(firstDateTime, DateTimeKind.Utc),
                Action = dto.Action,
                Remarks = dto.Remarks,
                PerformedByUserId = dto.PerformedByUserId,
                PerformedByUserName = dto.PerformedByUserName,
                StatusFrom = dto.StatusFrom,
                StatusTo = dto.StatusTo,
                StatusChange = dto.StatusChange,
                StatusChangedDateTime = statusChangeDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                PreviousStatus = dto.PreviousStatus,
                CurrentStatus = dto.CurrentStatus,
                TotalTat = totalTat,
                CurrentTat = currentTat
            };

            await _leadHistoryTableClient.AddEntityAsync(entity).ConfigureAwait(false);
        }

        public async Task<List<LeadHistoryDto>> GetHistoryAsync(string valuationId)
        {
            var results = new List<LeadHistoryDto>();

            await foreach (var entity in _leadHistoryTableClient.QueryAsync<LeadHistoryEntity>(
                filter: $"PartitionKey eq '{valuationId.Replace("'", "''")}'").ConfigureAwait(false))
            {
                results.Add(new LeadHistoryDto
                {
                    ValuationId = entity.PartitionKey,
                    DateTime = entity.DateTime,
                    Action = entity.Action,
                    Remarks = entity.Remarks,
                    PerformedByUserId = entity.PerformedByUserId,
                    PerformedByUserName = entity.PerformedByUserName,
                    StatusFrom = entity.StatusFrom,
                    StatusTo = entity.StatusTo,
                    CurrentTat = entity.CurrentTat,
                    TotalTat = entity.TotalTat,
                    FirstDateTime = entity.FirstDateTime,
                    FirstUpdate = entity.FirstUpdate,
                    StatusChange = entity.StatusChange,
                    StatusChangedDateTime = entity.StatusChangedDateTime,
                    PreviousStatus = entity.PreviousStatus,
                    CurrentStatus = entity.CurrentStatus
                });
            }

            return results.OrderByDescending(x => x.DateTime).ToList();
        }

        public async Task UpdateAsync(WorkflowUpdateDto dto)
        {
            var partitionKey = $"{dto.VehicleNumber}|{dto.ApplicantContact}";
            var rowKey = dto.ValuationId;

            WorkflowEntity entity;

            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                entity = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                entity = new WorkflowEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    CreatedAt = DateTime.UtcNow
                };
            }

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
            if (dto.State != null) entity.State = dto.State;
            if (dto.District != null) entity.District = dto.District;
            if (dto.RedFlag != null) entity.RedFlag = dto.RedFlag;
            if (dto.Remarks != null) entity.Remarks = dto.Remarks;
            if (dto.AssignedToPhoneNumber != null) entity.AssignedToPhoneNumber = dto.AssignedToPhoneNumber;
            if (dto.AssignedToEmail != null) entity.AssignedToEmail = dto.AssignedToEmail;
            if (dto.AssignedToWhatsapp != null) entity.AssignedToWhatsapp = dto.AssignedToWhatsapp;
            if (dto.Name != null) entity.Name = dto.Name;
            if (dto.ValuationType != null) entity.ValuationType = dto.ValuationType;

            entity.UpdatedAt = DateTime.UtcNow;

            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
        }

        public async Task<List<WorkflowModel?>> GetWorkflowInProgressAsync()
        {
            var results = new List<WorkflowModel?>();

            try
            {
                // ✅ UPDATED: Include 'Returned' so they are fetched
                await foreach (var entity in _tableClient.QueryAsync<WorkflowEntity>(
                    filter: $"Status eq 'InProgress' or Status eq 'Rejected' or Status eq 'Returned'").ConfigureAwait(false))
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
                        AssignedDistricts = entity.AssignedDistricts,
                        AssignedStates = entity.AssignedStates,
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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error querying workflow in progress", ex);
            }

            return results;
        }

        public async Task<List<WorkflowModel?>> FilterByStatesAsync(IEnumerable<string> stateKeys)
        {
            var results = new List<WorkflowModel?>();
            if (stateKeys == null || !stateKeys.Any())
                return results;

            var stateFilter = string.Join(" or ", stateKeys.Select(s => $"State eq '{s.Replace("'", "''")}'"));
            
            // ✅ UPDATED: Added 'Returned' so AVO/QC/Admin dashboards see them
            var filter = $"(Status eq 'InProgress' or Status eq 'Returned') and ({stateFilter})";

            try
            {
                await foreach (var entity in _tableClient.QueryAsync<WorkflowEntity>(
                    filter: filter).ConfigureAwait(false))
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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error querying workflow by states", ex);
            }

            return results;
        }

        public async Task<List<WorkflowModel?>> FilterByDistrictsAsync(IEnumerable<string> districtKeys)
        {
            var results = new List<WorkflowModel?>();
            if (districtKeys == null || !districtKeys.Any())
                return results;

            var districtFilter = string.Join(" or ", districtKeys.Select(d => $"District eq '{d.Replace("'", "''")}'"));
            
            // ✅ UPDATED: Added 'Returned' so AVO/QC/Admin dashboards see them
            var filter = $"(Status eq 'InProgress' or Status eq 'Returned') and ({districtFilter})";

            try
            {
                await foreach (var entity in _tableClient.QueryAsync<WorkflowEntity>(
                    filter: filter).ConfigureAwait(false))
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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error querying workflow by districts", ex);
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
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                entity.AssignedTo = assignedTo ?? entity.AssignedTo;
                entity.AssignedToPhoneNumber = assignedToPhoneNumber ?? entity.AssignedToPhoneNumber;
                entity.AssignedToEmail = assignedToEmail ?? entity.AssignedToEmail;
                entity.AssignedToWhatsapp = assignedToWhatsapp ?? entity.AssignedToWhatsapp;
                entity.UpdatedAt = DateTime.UtcNow;

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                entity.StakeholderAssignedTo = AssignedTo ?? "";
                entity.StakeholderAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.StakeholderAssignedToEmail = AssignedToEmail ?? "";
                entity.StakeholderAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                entity.BackEndAssignedTo = AssignedTo ?? "";
                entity.BackEndAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.BackEndAssignedToEmail = AssignedToEmail ?? "";
                entity.BackEndAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                entity.AVOAssignedTo = AssignedTo ?? "";
                entity.AVOAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.AVOAssignedToEmail = AssignedToEmail ?? "";
                entity.AVOAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                entity.QualityControlAssignedTo = AssignedTo ?? "";
                entity.QualityControlAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.QualityControlAssignedToEmail = AssignedToEmail ?? "";
                entity.QualityControlAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                entity.FinalReportAssignedTo = AssignedTo ?? "";
                entity.FinalReportAssignedToPhoneNumber = AssignedToPhoneNumber ?? "";
                entity.FinalReportAssignedToEmail = AssignedToEmail ?? "";
                entity.FinalReportAssignedToWhatsapp = AssignedToWhatsapp ?? "";
                entity.UpdatedAt = DateTime.UtcNow;
                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
            string valuationId, string vehicleNumber, string applicantContact, AssignmentDto assignment)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                entity.Status = "Status";
                entity.CompletedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.FinalReportAssignedTo = assignment.AssignedTo ?? "";
                entity.FinalReportAssignedToPhoneNumber = assignment.AssignedToPhoneNumber ?? "";
                entity.FinalReportAssignedToEmail = assignment.AssignedToEmail ?? "";
                entity.FinalReportAssignedToWhatsapp = assignment.AssignedToWhatsapp ?? "";
                entity.AssignedTo = assignment.AssignedTo ?? "";
                entity.AssignedToPhoneNumber = assignment.AssignedToPhoneNumber ?? "";
                entity.AssignedToEmail = assignment.AssignedToEmail ?? "";
                entity.AssignedToWhatsapp = assignment.AssignedToWhatsapp ?? "";

                await _completedTableClient.CreateIfNotExistsAsync().ConfigureAwait(false);
                await _completedTableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace).ConfigureAwait(false);

                try
                {
                    var getResponse = await _tableClient.GetEntityAsync<WorkflowEntity>(partitionKey, rowKey).ConfigureAwait(false);
                    if (getResponse != null && getResponse.Value != null)
                    {
                        await _tableClient.DeleteEntityAsync(partitionKey, rowKey).ConfigureAwait(false);
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return;
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
                try
                {
                    var completedResponse = await _completedTableClient.GetEntityAsync<WorkflowEntity>(
                        partitionKey: partitionKey,
                        rowKey: rowKey).ConfigureAwait(false);

                    var e = completedResponse.Value;
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
                catch (RequestFailedException ex2) when (ex2.Status == 404)
                {
                    return null;
                }
            }
        }

        public async Task<UserModel?> GetAssignedToByValuationAsync(
            string valuationId, string vehicleNumber, string applicantContact)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                var AssignedUser = new UserModel
                {
                    Name = entity.AssignedTo ?? string.Empty,
                    PhoneNumber = entity.AssignedToPhoneNumber ?? string.Empty,
                    Email = entity.AssignedToEmail ?? string.Empty,
                    Whatsapp = entity.AssignedToWhatsapp ?? string.Empty
                };

                if (string.IsNullOrEmpty(AssignedUser.Name))
                {
                    var workflowStep = entity.Workflow;
                    if (workflowStep != null)
                    {
                        var prefix = workflowStep;
                        string nameProp = $"{prefix}AssignedTo";
                        string phoneProp = $"{prefix}AssignedToPhoneNumber";
                        string emailProp = $"{prefix}AssignedToEmail";
                        string whatsappProp = $"{prefix}AssignedToWhatsapp";

                        string GetVal(string propName) =>
                            entity.GetType()
                                .GetProperty(propName)?
                                .GetValue(entity) as string
                            ?? string.Empty;

                        AssignedUser = new UserModel
                        {
                            Name = GetVal(nameProp).Trim(),
                            PhoneNumber = GetVal(phoneProp),
                            Email = GetVal(emailProp),
                            Whatsapp = GetVal(whatsappProp)
                        };
                    }
                }

                return AssignedUser;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<List<WorkflowModel?>> GetByAssignedToPhoneNumberAsync(string phoneNumber)
        {
            var results = new List<WorkflowModel?>();

            phoneNumber = Uri.UnescapeDataString(phoneNumber.Trim());

            try
            {
                await foreach (var entity in _tableClient.QueryAsync<WorkflowEntity>(
                    filter: $"AssignedToPhoneNumber eq '{phoneNumber}'").ConfigureAwait(false))
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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error querying workflows by phone number", ex);
            }

            return results;
        }

        public async Task<UserModel?> GetAssignedToByWorkflowAsync(
            string valuationId, string vehicleNumber, string applicantContact)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(
                    partitionKey: partitionKey,
                    rowKey: rowKey).ConfigureAwait(false);

                var entity = response.Value;

                return new UserModel
                {
                    Name = entity.AssignedTo ?? string.Empty,
                    PhoneNumber = entity.AssignedToPhoneNumber ?? string.Empty,
                    Email = entity.AssignedToEmail ?? string.Empty,
                    Whatsapp = entity.AssignedToWhatsapp ?? string.Empty
                };
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
            }
        }

        // ==============================================================================
        //  ⚠️ START & COMPLETE METHODS ADDED BELOW
        // ==============================================================================

        public async Task StartWorkflowStepAsync(string valuationId, string vehicleNumber, string applicantContact, int stepOrder)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(partitionKey, rowKey);
                var entity = response.Value;

                // 1. Update Status
                entity.Status = "InProgress";
                entity.WorkflowStepOrder = stepOrder;

                // 2. Map Step Order to Name (Keep consistent with your frontend)
                if (stepOrder == 1) entity.Workflow = "Stakeholder";
                else if (stepOrder == 2) entity.Workflow = "Backend";
                else if (stepOrder == 3) entity.Workflow = "AVO";
                else if (stepOrder == 4) entity.Workflow = "QualityControl";
                else if (stepOrder == 5) entity.Workflow = "FinalReport";

                // 3. Clear RedFlag (Important: removes the 'Rejected' banner)
                entity.RedFlag = "false"; 
                entity.UpdatedAt = DateTime.UtcNow;

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new Exception("Valuation record not found.");
            }
        }

        // ==============================================================================
        //  ✅ FIX 3: CompleteWorkflowStepAsync (Un-stuck the case)
        // ==============================================================================
        public async Task CompleteWorkflowStepAsync(string valuationId, string vehicleNumber, string applicantContact, int stepOrder)
        {
            var partitionKey = $"{vehicleNumber}|{applicantContact}";
            var rowKey = valuationId;

            try
            {
                var response = await _tableClient.GetEntityAsync<WorkflowEntity>(partitionKey, rowKey);
                var entity = response.Value;

                // ✅ CHANGED: Reset Status and RedFlag when completing a step
                // This ensures if it was "Returned", it goes back to "InProgress"
                entity.Status = "InProgress";
                entity.RedFlag = "false";
                entity.UpdatedAt = DateTime.UtcNow;

                await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new Exception("Valuation record not found.");
            }
        }
    }
}