using System.Net;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Valuation.Api.Models;

namespace Valuation.Api.Services;

public class ValuationService : IValuationService
{
    private readonly CosmosClient _cosmos;
    private readonly BlobServiceClient _blobService;
    private readonly string _dbId;
    private readonly string _containerId;
    private readonly string _blobContainerName;

    public ValuationService(
        CosmosClient cosmos,
        BlobServiceClient blobService,
        IConfiguration configuration)
    {
        _cosmos = cosmos;
        _blobService = blobService;
        _dbId = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
        _containerId = configuration["Cosmos:ContainerId"] ?? "Valuations";
        _blobContainerName = configuration["Blob:ContainerName"] ?? "vehicle-documents";
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
                return null;

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
            return null;
        }
    }

    public async Task UpdateVehicleDetailsAsync(
        string valuationId,
        VehicleDetailsDto dto,
        string applicantContact)
    {
        if (string.IsNullOrWhiteSpace(dto.RegistrationNumber))
            throw new ArgumentException("Registration number is required.", nameof(dto.RegistrationNumber));

        var pk = GetPk(dto.RegistrationNumber, applicantContact);

        // Read the existing document
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
                $"No valuation doc with id '{valuationId}' for Vehicle '{dto.RegistrationNumber}' and Applicant '{applicantContact}'");
        }

        // Initialize workflow if missing
        if (doc.Workflow == null)
        {
            doc.Workflow = new List<WorkflowStep>
            {
                new(){ StepOrder=1, TemplateStepId=1, AssignedToRole="Stakeholder",  Status="Inprogress" },
                new(){ StepOrder=2, TemplateStepId=2, AssignedToRole="BackEnd",      Status="Pending" },
                new(){ StepOrder=3, TemplateStepId=3, AssignedToRole="AVO",          Status="Pending" },
                new(){ StepOrder=4, TemplateStepId=4, AssignedToRole="QC",           Status="Pending" },
                new(){ StepOrder=5, TemplateStepId=5, AssignedToRole="FinalReport",  Status="Pending" },
            };
        }

        // Upload images if provided
        async Task<string?> UploadIfAsync(IFormFile? file)
        {
            if (file == null) return null;
            var containerClient = _blobService.GetBlobContainerClient(_blobContainerName);
            var blobName = $"{dto.RegistrationNumber}/{applicantContact}/{Guid.NewGuid()}-{file.FileName}";
            var blobClient = containerClient.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = file.ContentType };
            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, headers);
            return blobClient.Uri.ToString();
        }

        var stencilUrl = await UploadIfAsync(dto.StencilTrace);
        var chassisPhotoUrl = await UploadIfAsync(dto.ChassisNoPhoto);

        // Patch VehicleDetails
        doc.VehicleDetails = new VehicleDetailsDto
        {
            RegistrationNumber = dto.RegistrationNumber,
            Make = dto.Make,
            Model = dto.Model,
            DateOfRegistration = dto.DateOfRegistration,
            ClassOfVehicle = dto.ClassOfVehicle,
            EngineCC = dto.EngineCC,
            GrossVehicleWeight = dto.GrossVehicleWeight,
            Hypothecation = dto.Hypothecation,
            Insurer = dto.Insurer,
            PresentAddress = dto.PresentAddress,
            PermanentAddress = dto.PermanentAddress,
            ChassisNumber = dto.ChassisNumber,
            EngineNumber = dto.EngineNumber,
            Colour = dto.Colour,
            Fuel = dto.Fuel,
            OwnerName = dto.OwnerName,
            OwnerSerialNo = dto.OwnerSerialNo,
            SeatingCapacity = dto.SeatingCapacity,
            InsurancePolicyNo = dto.InsurancePolicyNo,
            InsuranceValidUpTo = dto.InsuranceValidUpTo,
            IDV = dto.IDV,
            PermitNo = dto.PermitNo,
            PermitValidUpTo = dto.PermitValidUpTo,
            FitnessNo = dto.FitnessNo,
            FitnessValidTo = dto.FitnessValidTo,
            BacklistStatus = dto.BacklistStatus,
            RcStatus = dto.RcStatus,
            StencilTraceUrl = stencilUrl,
            ChassisNoPhotoUrl = chassisPhotoUrl
        };

        // Upsert back
        await Container.UpsertItemAsync(doc, pk);
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
