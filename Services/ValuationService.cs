using System.Net;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Valuation.Api.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;

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


    public ValuationService(
        CosmosClient cosmos,
        BlobServiceClient blobService,
        IConfiguration configuration,
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
                ChassisNoPhotoUrl = doc.VehicleDetails.ChassisNoPhotoUrl
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
                RcStatus = false
            };
        }
    }

    // Returns all valuations which are open
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
    public async Task<CheckXResponse?> GetVehicleInfoAsync(string registration)
    {

        var regNumber = "TN12XX2345";

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
    }

    public async Task<List<OpenValuationDto>> GetOpenValuationsAsync()
    {
        var query = new QueryDefinition(@"
            SELECT
                c.id,
                c.VehicleNumber,
                c.Stakeholder.Applicant.Name     AS applicantName,
                c.Stakeholder.Applicant.Contact  AS applicantContact,
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

        // 1) Fetch existing & RC‐enriched DTO
        var updatedDto = await GetVehicleDetailsWithRcCheckAsync(
            valuationId, registrationNumber, applicantContact);
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
}
