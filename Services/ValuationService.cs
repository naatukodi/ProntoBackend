using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Valuation.Api.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using Polly;

namespace Valuation.Api.Services;

public class ValuationService : IValuationService
{
    private readonly CosmosClient _cosmos;
    private readonly BlobServiceClient _blobService;
    private readonly string _dbId;
    private readonly string _containerId;
    private readonly string _blobContainerName;
    private readonly HttpClient _httpClient;
    private readonly string _basicAuthHeader;
    private readonly string _attestrUrl;
    private readonly string _attestrToken;
    private readonly IWorkflowTableService _workflowTableService;


    public ValuationService(
        CosmosClient cosmos,
        BlobServiceClient blobService,
        IConfiguration configuration,
        IWorkflowTableService workflowTableService,
        HttpClient httpClient)
    {
        _cosmos = cosmos;
        _blobService = blobService;
        _dbId = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
        _containerId = configuration["Cosmos:ContainerId"] ?? "Valuations";
        _blobContainerName = configuration["Blob:ContainerName"] ?? "vehicle-documents";
        _httpClient = httpClient;
        _basicAuthHeader = configuration["BasicAuth:Header"] ?? "";
        _attestrUrl = configuration["Attestr:Url"] ?? "https://api.attestr.com/api/v2/public/checkx/rc";
        _attestrToken = configuration["Attestr:Token"] ?? "";
        _workflowTableService = workflowTableService;
    }



    private Container Container =>
        _cosmos.GetDatabase(_dbId).GetContainer(_containerId);

    private PartitionKey GetPk(string vehicleNumber, string applicantContact) =>
        new($"{vehicleNumber}|{applicantContact}");  // use colon delimiter for composite key

    public async Task<VehicleDetailsDto?> GetVehicleDetailsAsync(
        string valuationId, string vehicleNumber, string applicantContact)
    {
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: GetPk(vehicleNumber, applicantContact));

            var doc = resp.Resource;
            if (doc.VehicleDetails is null)
                return new VehicleDetailsDto();

            return new VehicleDetailsDto
            {
                RegistrationNumber = doc.VehicleDetails.RegistrationNumber,
                Make = doc.VehicleDetails.Make,
                Model = doc.VehicleDetails.Model,
                MonthOfMfg = doc.VehicleDetails.DateOfRegistration?.Month ?? 0,
                YearOfMfg = doc.VehicleDetails.DateOfRegistration?.Year ?? 0,
                BodyType = doc.VehicleDetails.BodyType,
                ChassisNumber = doc.VehicleDetails.ChassisNumber,
                EngineNumber = doc.VehicleDetails.EngineNumber,
                Colour = doc.VehicleDetails.Colour,
                Fuel = doc.VehicleDetails.Fuel,
                OwnerName = doc.VehicleDetails.OwnerName,
                PresentAddress = doc.VehicleDetails.PresentAddress,
                PermanentAddress = doc.VehicleDetails.PermanentAddress,
                Hypothecation = doc.VehicleDetails.Hypothecation,
                Insurer = doc.VehicleDetails.Insurer,
                DateOfRegistration = doc.VehicleDetails.DateOfRegistration,
                ClassOfVehicle = doc.VehicleDetails.ClassOfVehicle,
                EngineCC = doc.VehicleDetails.EngineCC,
                GrossVehicleWeight = doc.VehicleDetails.GrossVehicleWeight,
                OwnerSerialNo = doc.VehicleDetails.OwnerSerialNo,
                SeatingCapacity = doc.VehicleDetails.SeatingCapacity,
                InsurancePolicyNo = doc.VehicleDetails.InsurancePolicyNo,
                InsuranceValidUpTo = doc.VehicleDetails.InsuranceValidUpTo,
                IDV = doc.VehicleDetails.IDV,
                PermitNo = doc.VehicleDetails.PermitNo,
                PermitValidUpTo = doc.VehicleDetails.PermitValidUpTo,
                FitnessNo = doc.VehicleDetails.FitnessNo,
                FitnessValidTo = doc.VehicleDetails.FitnessValidTo,
                BacklistStatus = doc.VehicleDetails.BacklistStatus,
                RcStatus = doc.VehicleDetails.RcStatus,
                StencilTrace = null,          // not used in GET
                ChassisNoPhoto = null,        // not used in GET
                StencilTraceUrl = doc.VehicleDetails.StencilTraceUrl,
                ChassisNoPhotoUrl = doc.VehicleDetails.ChassisNoPhotoUrl,
                Remarks = doc.VehicleDetails.Remarks
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new VehicleDetailsDto()
            {
                RegistrationNumber = vehicleNumber,
                Make = "",
                Model = "",
                MonthOfMfg = 0,
                YearOfMfg = 0,
                BodyType = "",
                ChassisNumber = "",
                EngineNumber = "",
                Colour = "",
                Fuel = "",
                OwnerName = "",
                PresentAddress = "",
                PermanentAddress = "",
                Hypothecation = false,
                Insurer = "",
                DateOfRegistration = null,
                ClassOfVehicle = "",
                EngineCC = null,
                GrossVehicleWeight = null,
                OwnerSerialNo = null,
                SeatingCapacity = null,
                InsurancePolicyNo = null,
                InsuranceValidUpTo = null,
                IDV = null,
                PermitNo = null,
                PermitValidUpTo = null,
                FitnessNo = null,
                FitnessValidTo = null,
                BacklistStatus = false,
                RcStatus = false,
                Remarks = null
            };
        }
    }

    public async Task<VehicleDetailsDto?> GetVehicleDetailsWithRcCheckAsync(
        string valuationId,
        string registrationNumber,
        string applicantContact)
    {
        // 1) fetch existing
        VehicleDetailsDto? dto = await GetVehicleDetailsAsync(valuationId, registrationNumber, applicantContact);

        // 2) call Attestr API
        var api = await GetVehicleInfoAsync(registrationNumber);
        if (api == null) return dto;

        // 3) map into DTO
        if (dto == null)
            dto = new VehicleDetailsDto();
        MapAttestrToDto(api, dto);

        dto.RegistrationNumber = registrationNumber;

        // 4) Update Cosmos DB
        var pk = GetPk(registrationNumber, applicantContact);
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(
            id: valuationId,
            partitionKey: pk);
            var doc = resp.Resource;
            doc.VehicleDetails = dto;
            await Container.UpsertItemAsync(doc, pk);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException(
            $"No valuation doc with id '{valuationId}' for vehicle '{registrationNumber}' and applicant '{applicantContact}'.");
        }

        return dto;
    }

    public async Task<ValuationDocument?> GetValuationDocumentAsync(
        string valuationId, string vehicleNumber, string applicantContact)
    {
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: GetPk(vehicleNumber, applicantContact));
            return resp.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ActionResult> CompleteValuationDocumentAsync(
        string valuationId,
        string vehicleNumber,
        string applicantContact,
        CompleteValuationRequestDto request)
    {
        var pk = GetPk(vehicleNumber, applicantContact);

        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: pk);

            var doc = resp.Resource;

            doc.Status = request.Status ?? doc.Status;
            doc.CompletedAt = request.CompletedAt ?? DateTime.UtcNow;
            doc.CompletedBy = request.CompletedBy;
            doc.CompletedByPhoneNumber = request.CompletedByPhoneNumber;
            doc.CompletedByEmail = request.CompletedByEmail;
            doc.CompletedByWhatsapp = request.CompletedByWhatsapp;

            doc.PaymentStatus = request.PaymentStatus;
            doc.PaymentReference = request.PaymentReference;
            doc.PaymentDate = request.PaymentDate;
            doc.PaymentMethod = request.PaymentMethod;
            doc.PaymentAmount = request.PaymentAmount;
            doc.Remarks = request.Remarks;

            // ✅ STORE FINAL MANUAL VALUATION
            if (request.FinalValuationAmount.HasValue)
            {
                doc.FinalValuationAmount = request.FinalValuationAmount.Value;
            }

            await Container.UpsertItemAsync(doc, pk);

            await _workflowTableService.CompleteFinalReportWFAsync(
                valuationId,
                vehicleNumber,
                applicantContact,
                new AssignmentDto
                {
                    AssignedTo = request.CompletedBy ?? "",
                    AssignedToPhoneNumber = request.CompletedByPhoneNumber ?? "",
                    AssignedToEmail = request.CompletedByEmail ?? "",
                    AssignedToWhatsapp = request.CompletedByWhatsapp ?? ""
                }
            );

            return new OkResult();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException(
                $"No valuation doc with id '{valuationId}' for vehicle '{vehicleNumber}'.");
        }
    }


    public async Task updateAssignmentAsync(
        string valuationId, string vehicleNumber, string applicantContact,
        string? assignedTo, string? assignedToPhoneNumber, string? assignedToEmail, string? assignedToWhatsapp)
    {
        var pk = GetPk(vehicleNumber, applicantContact);
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: pk);
            var doc = resp.Resource;

            if (doc == null)
            {
                doc = new ValuationDocument
                {
                    id = valuationId,
                    CompositeKey = $"{vehicleNumber}|{applicantContact}",
                    VehicleNumber = vehicleNumber,
                    ApplicantContact = applicantContact,
                    CreatedAt = DateTime.UtcNow,
                    VehicleDetails = new VehicleDetailsDto
                    {
                        RegistrationNumber = vehicleNumber
                    }
                };
            }

            doc.AssignedTo = assignedTo;
            doc.AssignedToPhoneNumber = assignedToPhoneNumber;
            doc.AssignedToEmail = assignedToEmail;
            doc.AssignedToWhatsapp = assignedToWhatsapp;

            await Container.UpsertItemAsync(doc, pk);
            // Update the workflow table as well
            await _workflowTableService.FinalReportWFUpdateAssignmentAsync(
                valuationId,
                vehicleNumber,
                applicantContact,
                assignedTo ?? string.Empty,
                assignedToPhoneNumber ?? string.Empty,
                assignedToEmail ?? string.Empty,
                assignedToWhatsapp ?? string.Empty
            );
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException(
                $"No valuation doc with id '{valuationId}' for vehicle '{vehicleNumber}' and applicant '{applicantContact}'.");
        }
    }
    public async Task<CheckXResponse?> GetVehicleInfoAsync(string registration)
    {

        //var regNumber = registration.Replace(" ", "").ToUpper();
        var regNumber = "TN12XX2345"; // Hardcoded for testing

        using var client = new HttpClient();

        // Set up Authorization and Accept headers
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _attestrToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Prepare JSON payload
        var payload = new { reg = regNumber };
        var json = JsonSerializer.Serialize(payload);

        // Create content WITHOUT charset in Content-Type
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            Console.WriteLine($"Sending POST to {_attestrUrl} with payload: {json}");
            var response = await client.PostAsync(_attestrUrl, content);

            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
            Console.WriteLine("Response Body:");
            Console.WriteLine(body);
            return JsonSerializer.Deserialize<CheckXResponse>(body);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error while calling Attestr API:");
            Console.Error.WriteLine(ex);
        }
        return null;
    }


    private void MapAttestrToDto(CheckXResponse api, VehicleDetailsDto dto)
    {
        // ── Dates ────────────────────────────────────────────────────────────
        if (DateTime.TryParse(api.Registered, out var regDt))
        {
            dto.DateOfRegistration = regDt;
            dto.YearOfMfg = regDt.Year;
            dto.MonthOfMfg = regDt.Month;
        }

        if (!string.IsNullOrWhiteSpace(api.Manufactured))
        {
            var parts = api.Manufactured.Split('/');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var m)
                && int.TryParse(parts[1], out var y))
            {
                dto.ManufacturedDate = new DateTime(y, m, 1);
            }
        }

        if (DateTime.TryParse(api.PollutionCertificateUpto, out var pollUpto))
            dto.PollutionCertificateUpto = pollUpto;

        if (DateTime.TryParse(api.PermitIssued, out var permitIssued))
            dto.PermitIssued = permitIssued;
        if (DateTime.TryParse(api.PermitFrom, out var permitFrom))
            dto.PermitFrom = permitFrom;
        if (DateTime.TryParse(api.TaxUpto, out var taxUpto))
            dto.TaxUpto = taxUpto;

        // ── Numerics ─────────────────────────────────────────────────────────
        if (double.TryParse(api.CubicCapacity, out var cc))
            dto.EngineCC = Convert.ToInt32(Math.Round(cc));

        if (double.TryParse(api.GrossWeight, out var gw))
            dto.GrossVehicleWeight = gw;

        if (int.TryParse(api.SeatingCapacity, out var sc))
            dto.SeatingCapacity = sc;

        // ── Simple field copies ───────────────────────────────────────────────
        dto.Rto = api.Rto;
        dto.Lender = api.Lender;
        dto.ExShowroomPrice = api.ExShowroomPrice;
        dto.CategoryCode = api.Category;
        dto.ClassOfVehicle = api.CategoryDescription ?? dto.ClassOfVehicle;
        dto.NormsType = api.NormsType;
        dto.MakerVariant = api.MakerVariant;
        dto.PollutionCertificateNumber = api.PollutionCertificateNumber;
        dto.PermitType = api.PermitType;
        dto.TaxPaidUpto = api.TaxPaidUpto;

        // ── Boolean business logic ───────────────────────────────────────────
        dto.RcStatus = api.Valid;
        dto.BacklistStatus = !string.IsNullOrWhiteSpace(api.BlacklistStatus);

        // ── overwrite a few core fields if API gives better data ─────────────
        dto.Make = api.MakerDescription ?? dto.Make;
        dto.Model = api.MakerModel ?? dto.Model;
        dto.ChassisNumber = api.ChassisNumber ?? dto.ChassisNumber;
        dto.EngineNumber = api.EngineNumber ?? dto.EngineNumber;
        dto.Colour = api.ColorType ?? dto.Colour;
        dto.Fuel = api.FuelType ?? dto.Fuel;
        dto.OwnerName = api.Owner ?? dto.OwnerName;
        dto.PresentAddress = api.CurrentAddress ?? dto.PresentAddress;
        dto.PermanentAddress = api.PermanentAddress ?? dto.PermanentAddress;
        dto.Hypothecation = api.Financed;
        dto.Insurer = api.InsuranceProvider ?? dto.Insurer;
        dto.InsurancePolicyNo = api.InsurancePolicyNumber ?? dto.InsurancePolicyNo;
        if (DateTime.TryParse(api.InsuranceUpto, out var insUpto))
            dto.InsuranceValidUpTo = insUpto;
        // PRESERVE remarks so user edits are not lost
        if (string.IsNullOrWhiteSpace(dto.Remarks))
        dto.Remarks = dto.Remarks;
    }

    public async Task<List<OpenValuationDto>> GetOpenValuationsAsync()
    {
        var query = new QueryDefinition(@"
            SELECT 
                c.id,
                c.VehicleNumber,
                c.VehicleDetails.EngineNumber,
                c.VehicleDetails.ChassisNumber,
                c.Status,
                c.CreatedAt,
                ARRAY(
                    SELECT VALUE wf
                    FROM wf IN c.Workflow
                    WHERE wf.Status = 'InProgress'
                ) AS inProgressWorkflow
            FROM c
            WHERE c.Status = 'Open'
        ");

        var result = new List<OpenValuationDto>();
        using var iterator = Container.GetItemQueryIterator<OpenValuationDto>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            result.AddRange(response.Resource);
        }
        return result;
    }

    public async Task UpdateVehicleDetailsAsync(
        string valuationId,
        VehicleDetailsDto dto,
        string registrationNumber,
        string applicantContact
        )
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
            throw new ArgumentException("Registration number is required.", nameof(registrationNumber));
        
        //  PRESERVE remarks BEFORE any RC enrichment
        var preservedRemarks = dto.Remarks;

        // 1) Fetch existing & RC‐enriched DTO
        // Retry fetching RC-enriched DTO with Polly
        VehicleDetailsDto? updatedDto = null;
        var retryPolicy = Polly.Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        await retryPolicy.ExecuteAsync(async () =>
        {
            updatedDto = await GetVehicleDetailsWithRcCheckAsync(
            valuationId, registrationNumber, applicantContact);
        });
        if (updatedDto == null)
            throw new InvalidOperationException("Could not fetch vehicle details DTO.");

        // Preserve RC data by copying non-null values from updatedDto to dto
        foreach (var prop in typeof(VehicleDetailsDto).GetProperties())
        {
            var updatedValue = prop.GetValue(updatedDto);
            if (updatedValue != null)
            {
                prop.SetValue(dto, updatedValue);
            }
        }

        //  RESTORE remarks after all updates
        dto.Remarks = preservedRemarks;

        // 2) Compute your partition key
        var pk = GetPk(registrationNumber, applicantContact);

        // 3) Read the existing Cosmos document
        ValuationDocument doc;
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: pk);
            doc = resp.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException(
                $"No valuation doc with id '{valuationId}' for vehicle '{registrationNumber}' and applicant '{applicantContact}'.");
        }

        // 4) Initialize workflow if missing
        if (doc.Workflow == null)
        {
            doc.Workflow = new List<WorkflowStep>
            {
                new() { StepOrder = 1, TemplateStepId = 1, AssignedToRole = "Stakeholder", Status = "InProgress" },
                new() { StepOrder = 2, TemplateStepId = 2, AssignedToRole = "BackEnd",     Status = "Pending"    },
                new() { StepOrder = 3, TemplateStepId = 3, AssignedToRole = "AVO",         Status = "Pending"    },
                new() { StepOrder = 4, TemplateStepId = 4, AssignedToRole = "QC",          Status = "Pending"    },
                new() { StepOrder = 5, TemplateStepId = 5, AssignedToRole = "FinalReport", Status = "Pending"    },
            };
        }

        // 5) Upload images (if any) and update DTO URLs
        dto.StencilTraceUrl = await UploadIfAsync(dto.StencilTrace, registrationNumber, applicantContact);
        dto.ChassisNoPhotoUrl = await UploadIfAsync(dto.ChassisNoPhoto, registrationNumber, applicantContact);

        // 6) Patch the document’s VehicleDetails
        doc.VehicleDetails = dto;

        // 7) Upsert back into Cosmos
        await Container.UpsertItemAsync(doc, pk);

        // 8) Update the workflow table as well
        // Retry the workflow update with Polly
        var policy = Polly.Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        await policy.ExecuteAsync(async () =>
        {
            await _workflowTableService.BackendWFUpdateAssignmentAsync(
                valuationId,
                registrationNumber,
                applicantContact,
                dto.AssignedTo ?? "",
                dto.AssignedToPhoneNumber ?? "",
                dto.AssignedToEmail ?? "",
                dto.AssignedToWhatsapp ?? ""
            );
        });
    }

    public async Task UpdateAssignmentAsync(string valuationId, string vehicleNumber, string applicantContact, string? assignedTo, string? assignedToPhoneNumber, string? assignedToEmail, string? assignedToWhatsapp)
    {
        var databaseName = Environment.GetEnvironmentVariable("Cosmos:DatabaseId") ?? "ValuationsDb";
        var containerName = Environment.GetEnvironmentVariable("Cosmos:ContainerId") ?? "Valuations";
        var container = _cosmos.GetDatabase(databaseName).GetContainer(containerName);
        var compositeKey = $"{vehicleNumber}|{applicantContact}";
        var pk = new PartitionKey(compositeKey);

        ValuationDocument doc;
        try
        {
            var resp = await container.ReadItemAsync<ValuationDocument>(valuationId, pk);
            doc = resp.Resource;

            if (doc.VehicleDetails == null)
            {
                doc.VehicleDetails = new VehicleDetailsDto
                {
                    RegistrationNumber = vehicleNumber
                };
            }

            doc.VehicleDetails.AssignedTo = assignedTo;
            doc.VehicleDetails.AssignedToPhoneNumber = Uri.UnescapeDataString(assignedToPhoneNumber ?? "");
            doc.VehicleDetails.AssignedToEmail = Uri.UnescapeDataString(assignedToEmail ?? "");
            doc.VehicleDetails.AssignedToWhatsapp = Uri.UnescapeDataString(assignedToWhatsapp ?? "");
            doc.UpdatedAt = DateTime.UtcNow;

            // Ensure CreatedAt is set if not already
            if (doc.CreatedAt == default)
                doc.CreatedAt = DateTime.UtcNow;

            await container.UpsertItemAsync(doc, pk);

        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // If not found, create a new document
            doc = new ValuationDocument
            {
                id = valuationId,
                CompositeKey = compositeKey,
                VehicleNumber = vehicleNumber,
                ApplicantContact = applicantContact,
                CreatedAt = DateTime.UtcNow
            };
        }

        if (doc.VehicleDetails == null)
        {
            doc.VehicleDetails = new VehicleDetailsDto
            {
                RegistrationNumber = vehicleNumber
            };
        }

        // Ensure CreatedAt is set if not already
        if (doc.CreatedAt == default)
            doc.CreatedAt = DateTime.UtcNow;

        await container.UpsertItemAsync(doc, pk);

        // Update the workflow table as well
        // Retry the workflow update with Polly
        var policy = Polly.Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        await policy.ExecuteAsync(async () =>
        {
            await _workflowTableService.BackendWFUpdateAssignmentAsync(
                valuationId,
                vehicleNumber,
                applicantContact,
                assignedTo ?? "",
                Uri.UnescapeDataString(assignedToPhoneNumber ?? ""),
                Uri.UnescapeDataString(assignedToEmail ?? ""),
                Uri.UnescapeDataString(assignedToWhatsapp ?? "")
            );
        });
    }


    private async Task<string?> UploadIfAsync(IFormFile? file, string reg, string contact)
    {
        if (file == null) return null;

        var containerClient = _blobService.GetBlobContainerClient(_blobContainerName);
        var blobName = $"{reg}/{contact}/{Guid.NewGuid()}-{file.FileName}";
        var blobClient = containerClient.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders { ContentType = file.ContentType };

        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, headers);
        return blobClient.Uri.ToString();
    }
    public async Task DeleteVehicleDetailsAsync(
        string valuationId, string vehicleNumber, string applicantContact)
    {
        var pk = GetPk(vehicleNumber, applicantContact);
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(valuationId, pk);
            var doc = resp.Resource;
            doc.VehicleDetails = null;
            await Container.UpsertItemAsync(doc, pk);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // nothing to delete
        }
    }

    public async Task<VehicleDuplicateCheckResponse> CheckDuplicateVehicleAsync(
        string? vehicleNumber,
        string? engineNumber,
        string? chassisNumber)
    {
        var response = new VehicleDuplicateCheckResponse
        {
            IsVehicleNumberExists = false,
            IsEngineNumberExists = false,
            IsChassisNumberExists = false,
            ExistingRecords = new List<ExistingVehicleRecord>(),
            Messages = new List<string>()
        };

        if (string.IsNullOrWhiteSpace(vehicleNumber) &&
            string.IsNullOrWhiteSpace(engineNumber) &&
            string.IsNullOrWhiteSpace(chassisNumber))
        {
            return response;
        }

        try
        {
            var recordsDict = new Dictionary<string, ExistingVehicleRecord>();

            async Task ExecuteQuery(QueryDefinition query, string matchedField)
            {
                using var iterator = Container.GetItemQueryIterator<dynamic>(query);

                while (iterator.HasMoreResults)
                {
                    var batch = await iterator.ReadNextAsync();

                    foreach (var item in batch)
                    {
                        string valId = item.id;

                        if (!recordsDict.ContainsKey(valId))
                        {
                            // ✅ SAFE DECIMAL CONVERSION
                            decimal? valuationAmount = null;
                            if (item.ValuationAmount != null)
                            {
                                try
                                {
                                    valuationAmount = Convert.ToDecimal(item.ValuationAmount);
                                }
                                catch
                                {
                                    valuationAmount = null;
                                }
                            }

                            // ✅ SAFE DATETIME CONVERSION
                            DateTime createdDate = DateTime.MinValue;
                            if (item.CreatedAt != null)
                            {
                                try
                                {
                                    createdDate = Convert.ToDateTime(item.CreatedAt);
                                }
                                catch
                                {
                                    createdDate = DateTime.MinValue;
                                }
                            }

                            recordsDict[valId] = new ExistingVehicleRecord
                            {
                                ValuationId = valId,
                                VehicleNumber = item.VehicleNumber,
                                EngineNumber = item.EngineNumber,
                                ChassisNumber = item.ChassisNumber,
                                Status = item.Status ?? "Unknown",
                                CreatedDate = createdDate,
                                MatchedField = matchedField,
                                // ✅ SAFE STRING CONVERSION
                                Company = item.Company != null ? item.Company.ToString() : null,
                                ValuationAmount = valuationAmount
                            };
                        }
                        else
                        {
                            if (!recordsDict[valId].MatchedField.Contains(matchedField))
                            {
                                recordsDict[valId].MatchedField += ", " + matchedField;
                            }
                        }
                    }
                }
            }

            // ================= VEHICLE NUMBER =================
            // ✅ PROPER ALIASING AND COALESCE DEFAULT
            if (!string.IsNullOrWhiteSpace(vehicleNumber))
            {
                var vehicleQuery = new QueryDefinition(@"
                    SELECT 
                        c.id,
                        c.VehicleNumber,
                        c.VehicleDetails.EngineNumber AS EngineNumber,
                        c.VehicleDetails.ChassisNumber AS ChassisNumber,
                        c.Status,
                        c.CreatedAt,
                        IIF(IS_DEFINED(c.Stakeholder.Name), c.Stakeholder.Name, null) AS Company,
                        COALESCE(c.FinalValuationAmount, c.ValuationResponse.MidRange, 0) AS ValuationAmount
                    FROM c
                    WHERE IS_NULL(c.DeletedAt)
                    AND UPPER(c.VehicleNumber) = @vehicleNumber
                ").WithParameter("@vehicleNumber", vehicleNumber.Trim().ToUpper());

                await ExecuteQuery(vehicleQuery, "Vehicle Number");

                response.IsVehicleNumberExists =
                    recordsDict.Values.Any(r => r.MatchedField.Contains("Vehicle Number"));
            }

            // ================= ENGINE NUMBER =================
            // ✅ PROPER ALIASING AND COALESCE DEFAULT
            if (!string.IsNullOrWhiteSpace(engineNumber))
            {
                var engineQuery = new QueryDefinition(@"
                    SELECT 
                        c.id,
                        c.VehicleNumber,
                        c.VehicleDetails.EngineNumber AS EngineNumber,
                        c.VehicleDetails.ChassisNumber AS ChassisNumber,
                        c.Status,
                        c.CreatedAt,
                        IIF(IS_DEFINED(c.Stakeholder.Name), c.Stakeholder.Name, null) AS Company,
                        COALESCE(c.FinalValuationAmount, c.ValuationResponse.MidRange, 0) AS ValuationAmount
                    FROM c
                    WHERE IS_NULL(c.DeletedAt)
                    AND IS_DEFINED(c.VehicleDetails.EngineNumber)
                    AND UPPER(c.VehicleDetails.EngineNumber) = @engineNumber
                ").WithParameter("@engineNumber", engineNumber.Trim().ToUpper());

                await ExecuteQuery(engineQuery, "Engine Number");

                response.IsEngineNumberExists =
                    recordsDict.Values.Any(r => r.MatchedField.Contains("Engine Number"));
            }

            // ================= CHASSIS NUMBER =================
            // ✅ PROPER ALIASING AND COALESCE DEFAULT
            if (!string.IsNullOrWhiteSpace(chassisNumber))
            {
                var chassisQuery = new QueryDefinition(@"
                    SELECT 
                        c.id,
                        c.VehicleNumber,
                        c.VehicleDetails.EngineNumber AS EngineNumber,
                        c.VehicleDetails.ChassisNumber AS ChassisNumber,
                        c.Status,
                        c.CreatedAt,
                        IIF(IS_DEFINED(c.Stakeholder.Name), c.Stakeholder.Name, null) AS Company,
                        COALESCE(c.FinalValuationAmount, c.ValuationResponse.MidRange, 0) AS ValuationAmount
                    FROM c
                    WHERE IS_NULL(c.DeletedAt)
                    AND IS_DEFINED(c.VehicleDetails.ChassisNumber)
                    AND UPPER(c.VehicleDetails.ChassisNumber) = @chassisNumber
                ").WithParameter("@chassisNumber", chassisNumber.Trim().ToUpper());

                await ExecuteQuery(chassisQuery, "Chassis Number");

                response.IsChassisNumberExists =
                    recordsDict.Values.Any(r => r.MatchedField.Contains("Chassis Number"));
            }

            response.ExistingRecords = recordsDict.Values.ToList();

            // ================= MESSAGES =================
            if (response.IsVehicleNumberExists)
            {
                response.Messages.Add(
                    $"Vehicle number '{vehicleNumber}' found in {response.ExistingRecords.Count(r => r.MatchedField.Contains("Vehicle Number"))} existing record(s)");
            }

            if (response.IsEngineNumberExists)
            {
                response.Messages.Add(
                    $"Engine number '{engineNumber}' found in {response.ExistingRecords.Count(r => r.MatchedField.Contains("Engine Number"))} existing record(s)");
            }

            if (response.IsChassisNumberExists)
            {
                response.Messages.Add(
                    $"Chassis number '{chassisNumber}' found in {response.ExistingRecords.Count(r => r.MatchedField.Contains("Chassis Number"))} existing record(s)");
            }

            response.IsDuplicate =
                response.IsVehicleNumberExists ||
                response.IsEngineNumberExists ||
                response.IsChassisNumberExists;

            response.TotalDuplicatesFound = response.ExistingRecords.Count;

            // ================= AVERAGE CALCULATION =================
            var amounts = response.ExistingRecords
                .Where(r => r.ValuationAmount.HasValue && r.ValuationAmount.Value > 0) // Ignore the '0' defaults if they throw off the average
                .Select(r => r.ValuationAmount!.Value)
                .ToList();

            if (amounts.Any())
            {
                response.AverageValuationAmount = amounts.Average();
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error checking duplicates: {ex.Message}");
            throw;
        }
    }
}