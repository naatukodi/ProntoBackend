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
    private readonly string _cdnEndpoint;
    private readonly HttpClient _httpClient;
    private readonly string _basicAuthHeader;
    private readonly string _surepassUrl;
    private readonly string _surepassToken;
    private readonly IWorkflowTableService _workflowTableService;
    // Company this request belongs to; stamped onto any case created here.
    private readonly IBrandContext _brand;

    public ValuationService(
        CosmosClient cosmos,
        BlobServiceClient blobService,
        IConfiguration configuration,
        IWorkflowTableService workflowTableService,
        HttpClient httpClient,
        IBrandContext brand)
    {
        _brand = brand;
        _cosmos = cosmos;
        _blobService = blobService;
        _dbId = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
        _containerId = configuration["Cosmos:ContainerId"] ?? "Valuations";
        _blobContainerName = configuration["Blob:ContainerName"] ?? "documents";
        _cdnEndpoint = configuration["Blob:CdnEndpointHostname"] ?? "https://vehgablobs.blob.core.windows.net";
        _httpClient = httpClient;
        _basicAuthHeader = configuration["BasicAuth:Header"] ?? "";
        _surepassUrl = configuration["Surepass:Url"] ?? "https://kyc-api.surepass.io/api/v1/rc/rc-text";
        _surepassToken = configuration["Surepass:Token"] ?? "";
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

            // Return the stored DTO as-is so every field (Rto, Lender, permit,
            // pollution, tax, etc.) survives the round-trip; a hand-written
            // field-by-field copy here kept drifting out of date.
            var dto = doc.VehicleDetails;

            // File streams are never returned on GET
            dto.StencilTrace = null;
            dto.ChassisNoPhoto = null;

            // Older documents may lack mfg month/year — derive from registration date
            if ((dto.YearOfMfg is null or 0) && dto.DateOfRegistration.HasValue)
            {
                dto.YearOfMfg = dto.DateOfRegistration.Value.Year;
                dto.MonthOfMfg = dto.DateOfRegistration.Value.Month;
            }

            return dto;
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
        catch (CosmosException)
        {
            return new VehicleDetailsDto { RegistrationNumber = vehicleNumber };
        }
    }

    public async Task<VehicleDetailsDto?> GetVehicleDetailsWithRcCheckAsync(
        string valuationId,
        string registrationNumber,
        string applicantContact)
    {
        // 1) fetch existing
        VehicleDetailsDto? dto = await GetVehicleDetailsAsync(valuationId, registrationNumber, applicantContact);

        // 2) call Surepass API
        var api = await GetVehicleInfoAsync(registrationNumber);
        if (api == null) return dto;

        // 3) map into DTO
        if (dto == null)
            dto = new VehicleDetailsDto();
        MapSurepassToDto(api, dto);

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
            var pk = GetPk(vehicleNumber, applicantContact);
            var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: pk);
            var doc = resp.Resource;

            // Heal records where ranges were stored as 0 but RawResponse exists
            var vr = doc.ValuationResponse;
            if (vr != null
                && !string.IsNullOrWhiteSpace(vr.RawResponse)
                && (vr.LowRange == null || vr.LowRange == 0)
                && (vr.MidRange == null || vr.MidRange == 0)
                && (vr.HighRange == null || vr.HighRange == 0))
            {
                var parsed = VehicleValuationParser.ParseRanges(vr.RawResponse);
                if (parsed.LowRange != 0 || parsed.MidRange != 0 || parsed.HighRange != 0)
                {
                    vr.LowRange  = parsed.LowRange;
                    vr.MidRange  = parsed.MidRange;
                    vr.HighRange = parsed.HighRange;
                    doc.ValuationResponse = vr;
                    await Container.UpsertItemAsync(doc, pk);
                }
            }

            // Photo URLs are stored with their correct host in Cosmos DB — no rewriting needed.

            return doc;
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
                    Brand = _brand.Current,
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

    public async Task<SurepassRcData?> GetVehicleInfoAsync(string registration)
    {
        var regNumber = registration.Replace(" ", "").ToUpper();

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _surepassToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new { id_number = regNumber };
        var json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            Console.WriteLine($"Sending POST to {_surepassUrl} with payload: {json}");
            var response = await client.PostAsync(_surepassUrl, content);

            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
            Console.WriteLine("Response Body:");
            Console.WriteLine(body);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var wrapper = JsonSerializer.Deserialize<SurepassRcResponse>(body, options);
            return wrapper?.Success == true ? wrapper.Data : null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error while calling Surepass API:");
            Console.Error.WriteLine(ex);
        }
        return null;
    }


    // Surepass sometimes returns invalid dates like "2099-09-00" (day=0) or "2099-00-04" (month=0).
    // This helper replaces zeroed parts with 01 so DateTime.TryParse succeeds.
    private static DateTime? SafeParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        var parts = dateStr.Split('-');
        if (parts.Length == 3)
        {
            if (parts[1] == "00") parts[1] = "01";
            if (parts[2] == "00") parts[2] = "01";
            dateStr = string.Join("-", parts);
        }
        return DateTime.TryParse(dateStr, out var dt) ? dt : null;
    }

    private void MapSurepassToDto(SurepassRcData api, VehicleDetailsDto dto)
    {
        // ── Registration date ─────────────────────────────────────────────────
        var regDt = SafeParseDate(api.RegistrationDate);
        if (regDt.HasValue)
        {
            dto.DateOfRegistration = regDt;
            dto.YearOfMfg = regDt.Value.Year;
            dto.MonthOfMfg = regDt.Value.Month;
        }

        // ── Manufacturing date ("M/YYYY" format) ──────────────────────────────
        if (!string.IsNullOrWhiteSpace(api.ManufacturingDate))
        {
            var parts = api.ManufacturingDate.Split('/');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var mfgMonth)
                && int.TryParse(parts[1], out var mfgYear)
                && mfgMonth >= 1 && mfgMonth <= 12
                && mfgYear > 1900)
            {
                dto.ManufacturedDate = new DateTime(mfgYear, mfgMonth, 1);
            }
        }

        // ── Other dates ───────────────────────────────────────────────────────
        dto.PollutionCertificateUpto = SafeParseDate(api.PuccUpto);
        dto.PermitIssued             = SafeParseDate(api.PermitIssueDate);
        dto.PermitFrom               = SafeParseDate(api.PermitValidFrom);
        dto.PermitValidUpTo          = SafeParseDate(api.PermitValidUpto);
        dto.InsuranceValidUpTo       = SafeParseDate(api.InsuranceUpto);
        dto.FitnessValidTo           = SafeParseDate(api.FitUpTo);
        dto.TaxUpto                  = SafeParseDate(api.TaxUpto);

        // ── Numerics ──────────────────────────────────────────────────────────
        if (double.TryParse(api.CubicCapacity, out var cc))
            dto.EngineCC = Convert.ToInt32(Math.Round(cc));

        if (double.TryParse(api.VehicleGrossWeight, out var gw))
            dto.GrossVehicleWeight = gw;

        if (int.TryParse(api.SeatCapacity, out var sc))
            dto.SeatingCapacity = sc;

        // ── Simple field copies ───────────────────────────────────────────────
        dto.Rto                      = api.RegisteredAt;
        dto.Lender                   = api.Financer;
        dto.CategoryCode             = api.VehicleCategory;
        dto.ClassOfVehicle           = api.VehicleCategoryDescription ?? dto.ClassOfVehicle;
        dto.NormsType                = api.NormsType;
        dto.MakerVariant             = api.Variant;
        dto.PollutionCertificateNumber = api.PuccNumber;
        dto.PermitType               = api.PermitType;
        dto.PermitNo                 = api.PermitNumber;
        dto.TaxPaidUpto              = api.TaxPaidUpto;
        dto.OwnerSerialNo            = api.OwnerNumber;

        // ── Boolean business logic ────────────────────────────────────────────
        // Surepass does not return a direct valid bool; use success from the wrapper (already filtered before this call)
        dto.RcStatus      = true;
        dto.BacklistStatus = !string.IsNullOrWhiteSpace(api.BlacklistStatus);

        // ── Core fields — only overwrite if Surepass returns a value ──────────
        dto.Make             = api.MakerDescription      ?? dto.Make;
        dto.Model            = api.MakerModel            ?? dto.Model;
        dto.BodyType         = api.BodyType              ?? dto.BodyType;
        dto.ChassisNumber    = api.VehicleChasisNumber   ?? dto.ChassisNumber;
        dto.EngineNumber     = api.VehicleEngineNumber   ?? dto.EngineNumber;
        dto.Colour           = api.Color                 ?? dto.Colour;
        dto.Fuel             = api.FuelType              ?? dto.Fuel;
        dto.OwnerName        = api.OwnerName             ?? dto.OwnerName;
        dto.PresentAddress   = api.PresentAddress        ?? dto.PresentAddress;
        dto.PermanentAddress = api.PermanentAddress      ?? dto.PermanentAddress;
        dto.Hypothecation    = api.Financed;
        dto.Insurer          = api.InsuranceCompany      ?? dto.Insurer;
        dto.InsurancePolicyNo = api.InsurancePolicyNumber ?? dto.InsurancePolicyNo;
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
        " + (_brand.IsUnscoped ? "" : $" AND {BrandContext.SqlFilter}"));

        // Open cases are the main dashboard list — the one place the two companies
        // would most visibly bleed into each other.
        if (!_brand.IsUnscoped) query = query.WithParameter(BrandContext.SqlParam, _brand.Current);

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

        // Fill in blanks from RC/existing data; user-supplied values always win
        foreach (var prop in typeof(VehicleDetailsDto).GetProperties())
        {
            if (!prop.CanWrite) continue;
            if (prop.PropertyType == typeof(IFormFile)) continue;

            var userValue = prop.GetValue(dto);
            var rcValue   = prop.GetValue(updatedDto);

            bool userProvided = userValue != null &&
                                !(userValue is string s && string.IsNullOrEmpty(s));
            if (!userProvided && rcValue != null)
                prop.SetValue(dto, rcValue);
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

        // 5) Upload images only if a new file was supplied; never null-out an existing URL
        var newStencilUrl = await UploadIfAsync(dto.StencilTrace, registrationNumber, applicantContact);
        if (newStencilUrl != null) dto.StencilTraceUrl = newStencilUrl;

        var newChassisUrl = await UploadIfAsync(dto.ChassisNoPhoto, registrationNumber, applicantContact);
        if (newChassisUrl != null) dto.ChassisNoPhotoUrl = newChassisUrl;

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
                Brand = _brand.Current,
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

    // ✅ NEW DTO CLASS: Prevents crashing when Cosmos DB returns missing properties
    private class DuplicateQueryResult
    {
        public string id { get; set; }
        public string? VehicleNumber { get; set; }
        public string? EngineNumber { get; set; }
        public string? ChassisNumber { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Company { get; set; }
        public double? ValuationAmount { get; set; } 
    }

    public async Task<VehicleDuplicateCheckResponse> CheckDuplicateVehicleAsync(
        string? vehicleNumber,
        string? engineNumber,
        string? chassisNumber,
        string? excludeId = null)
    {
        // ✅ CATCH FRONTEND BUG: Stop Angular from searching for literal "undefined"
        string? CleanStr(string? val) =>
            string.IsNullOrWhiteSpace(val) || val == "undefined" || val == "null" ? null : val;

        vehicleNumber = CleanStr(vehicleNumber);
        engineNumber = CleanStr(engineNumber);
        chassisNumber = CleanStr(chassisNumber);

        var response = new VehicleDuplicateCheckResponse
        {
            ExistingRecords = new List<ExistingVehicleRecord>(),
            Messages = new List<string>()
        };

        if (vehicleNumber == null && engineNumber == null && chassisNumber == null)
        {
            return response;
        }

        try
        {
            var recordsDict = new Dictionary<string, ExistingVehicleRecord>();

            // ✅ SAFE ITERATOR: Uses DuplicateQueryResult instead of <dynamic>
            async Task ExecuteQuery(QueryDefinition query, string matchedField)
            {
                using var iterator = Container.GetItemQueryIterator<DuplicateQueryResult>(query);

                while (iterator.HasMoreResults)
                {
                    var batch = await iterator.ReadNextAsync();

                    foreach (var item in batch)
                    {
                        string valId = item.id;

                        if (!recordsDict.ContainsKey(valId))
                        {
                            recordsDict[valId] = new ExistingVehicleRecord
                            {
                                ValuationId = valId,
                                VehicleNumber = item.VehicleNumber,
                                EngineNumber = item.EngineNumber,
                                ChassisNumber = item.ChassisNumber,
                                Status = item.Status ?? "Unknown",
                                CreatedDate = item.CreatedAt ?? DateTime.MinValue,
                                MatchedField = matchedField,
                                Company = item.Company,
                                ValuationAmount = item.ValuationAmount.HasValue ? (decimal)item.ValuationAmount.Value : null
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

            // ================= SAFE VALUATION EXPRESSION =================
            string valuationExpression = @"
                IIF(
                    IS_DEFINED(c.QualityControl) AND NOT IS_NULL(c.QualityControl) AND IS_DEFINED(c.QualityControl.ValuationAmount) AND NOT IS_NULL(c.QualityControl.ValuationAmount) AND c.QualityControl.ValuationAmount > 0,
                    c.QualityControl.ValuationAmount,
                    IIF(
                        IS_DEFINED(c.FinalValuationAmount) AND NOT IS_NULL(c.FinalValuationAmount) AND c.FinalValuationAmount > 0,
                        c.FinalValuationAmount,
                        IIF(
                            IS_DEFINED(c.ValuationResponse) AND NOT IS_NULL(c.ValuationResponse) AND IS_DEFINED(c.ValuationResponse.MidRange) AND NOT IS_NULL(c.ValuationResponse.MidRange) AND c.ValuationResponse.MidRange > 0,
                            c.ValuationResponse.MidRange,
                            null
                        )
                    )
                ) AS ValuationAmount";

            string excludeClause = string.IsNullOrWhiteSpace(excludeId) ? "" : "AND c.id != @excludeId";

            // Duplicate detection is per-company. The same vehicle legitimately has a case
            // in both Vehga and Pronto, and without this a Pronto case is flagged as a
            // duplicate of a Vehga one and surfaces in the other company's dedupe list.
            string brandClause = _brand.IsUnscoped ? "" : $"AND {BrandContext.SqlFilter}";

            QueryDefinition WithBrand(QueryDefinition q) =>
                _brand.IsUnscoped ? q : q.WithParameter(BrandContext.SqlParam, _brand.Current);

            // ================= VEHICLE NUMBER =================
            if (vehicleNumber != null)
            {
                var vehicleQuery = new QueryDefinition($@"
                    SELECT
                        c.id,
                        c.VehicleNumber,
                        c.VehicleDetails.EngineNumber AS EngineNumber,
                        c.VehicleDetails.ChassisNumber AS ChassisNumber,
                        c.Status,
                        c.CreatedAt,
                        IIF(IS_DEFINED(c.Stakeholder.Name), c.Stakeholder.Name, null) AS Company,
                        {valuationExpression}
                    FROM c
                    WHERE (NOT IS_DEFINED(c.DeletedAt) OR IS_NULL(c.DeletedAt))
                    AND UPPER(c.VehicleNumber) = @vehicleNumber
                    {brandClause}
                {excludeClause}
                ").WithParameter("@vehicleNumber", vehicleNumber.Trim().ToUpper());
                if (!string.IsNullOrWhiteSpace(excludeId)) vehicleQuery = vehicleQuery.WithParameter("@excludeId", excludeId);

                vehicleQuery = WithBrand(vehicleQuery);
                await ExecuteQuery(vehicleQuery, "Vehicle Number");
            }

            // ================= ENGINE NUMBER =================
            if (engineNumber != null)
            {
                var engineQuery = new QueryDefinition($@"
                    SELECT
                        c.id,
                        c.VehicleNumber,
                        c.VehicleDetails.EngineNumber AS EngineNumber,
                        c.VehicleDetails.ChassisNumber AS ChassisNumber,
                        c.Status,
                        c.CreatedAt,
                        IIF(IS_DEFINED(c.Stakeholder.Name), c.Stakeholder.Name, null) AS Company,
                        {valuationExpression}
                    FROM c
                    WHERE (NOT IS_DEFINED(c.DeletedAt) OR IS_NULL(c.DeletedAt))
                    AND IS_DEFINED(c.VehicleDetails.EngineNumber)
                    AND UPPER(c.VehicleDetails.EngineNumber) = @engineNumber
                    {brandClause}
                {excludeClause}
                ").WithParameter("@engineNumber", engineNumber.Trim().ToUpper());
                if (!string.IsNullOrWhiteSpace(excludeId)) engineQuery = engineQuery.WithParameter("@excludeId", excludeId);

                engineQuery = WithBrand(engineQuery);
                await ExecuteQuery(engineQuery, "Engine Number");
            }

            // ================= CHASSIS NUMBER =================
            if (chassisNumber != null)
            {
                var chassisQuery = new QueryDefinition($@"
                    SELECT
                        c.id,
                        c.VehicleNumber,
                        c.VehicleDetails.EngineNumber AS EngineNumber,
                        c.VehicleDetails.ChassisNumber AS ChassisNumber,
                        c.Status,
                        c.CreatedAt,
                        IIF(IS_DEFINED(c.Stakeholder.Name), c.Stakeholder.Name, null) AS Company,
                        {valuationExpression}
                    FROM c
                    WHERE (NOT IS_DEFINED(c.DeletedAt) OR IS_NULL(c.DeletedAt))
                    AND IS_DEFINED(c.VehicleDetails.ChassisNumber)
                    AND UPPER(c.VehicleDetails.ChassisNumber) = @chassisNumber
                    {brandClause}
                {excludeClause}
                ").WithParameter("@chassisNumber", chassisNumber.Trim().ToUpper());
                if (!string.IsNullOrWhiteSpace(excludeId)) chassisQuery = chassisQuery.WithParameter("@excludeId", excludeId);

                chassisQuery = WithBrand(chassisQuery);
                await ExecuteQuery(chassisQuery, "Chassis Number");
            }

            response.ExistingRecords = recordsDict.Values.ToList();
            response.TotalDuplicatesFound = response.ExistingRecords.Count;

            response.IsVehicleNumberExists =
                response.ExistingRecords.Any(r => r.MatchedField.Contains("Vehicle Number"));

            response.IsEngineNumberExists =
                response.ExistingRecords.Any(r => r.MatchedField.Contains("Engine Number"));

            response.IsChassisNumberExists =
                response.ExistingRecords.Any(r => r.MatchedField.Contains("Chassis Number"));

            response.IsDuplicate =
                response.IsVehicleNumberExists ||
                response.IsEngineNumberExists ||
                response.IsChassisNumberExists;

            // ================= AVERAGE =================
            var amounts = response.ExistingRecords
                .Where(r => r.ValuationAmount.HasValue && r.ValuationAmount.Value > 0)
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