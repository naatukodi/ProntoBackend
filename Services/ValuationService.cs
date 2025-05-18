using System.Net;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;
// Add the correct using directive if VehicleDetails is in a different namespace
// using Valuation.Api.Entities; // Uncomment and adjust if needed

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

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
        new($"{vehicleNumber}|{applicantContact}");

    public async Task<VehicleDetailsDto?> GetVehicleDetailsAsync(
        string valuationId, string vehicleNumber, string applicantContact)
    {
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: GetPk(vehicleNumber, applicantContact));

            var doc = resp.Resource;
            return doc.VehicleDetails is null ? null : new VehicleDetailsDto
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
                Documents = doc.VehicleDetails.Documents
                // … map the rest of properties …
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpdateVehicleDetailsAsync(string valuationId, VehicleDetailsDto dto, string applicantContact)
    {
        // Ensure dto.RegistrationNumber and applicantContact are set
        if (string.IsNullOrWhiteSpace(dto.RegistrationNumber))
            throw new ArgumentException("Registration number is required.", nameof(dto.RegistrationNumber));
        //if (string.IsNullOrWhiteSpace(applicantContact))
        //    throw new ArgumentException("Applicant contact is required.", nameof(applicantContact));

        // 1) Get the container
        // 2) Build composite PK
        // 3) Read existing — do NOT create new if missing
        // 4) Upload images if provided
        // 5) Patch VehicleDetails sub‐document
        // 6) Ensure CompositeKey stays in sync
        // 7) Overwrite (upsert) the full document
        {

            // 1) Build composite PK
            //var compositeKey = $"{dto.RegistrationNumber}|{applicantContact}";
            var pk = GetPk(dto.RegistrationNumber, applicantContact);

            // 2) Read existing — do NOT create new if missing
            ValuationDocument doc;
            try
            {
                var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: valuationId,
                partitionKey: GetPk(dto.RegistrationNumber, applicantContact));

                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException(
                    $"No valuation doc with id '{valuationId}' for Vehicle '{dto.RegistrationNumber}' and Applicant '{applicantContact}'");
            }

            // 3) Upload images if provided
            async Task<string?> UploadIf(IFormFile? file)
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

            var stencilUrl = await UploadIf(dto.StencilTrace);
            var chassisPhotoUrl = await UploadIf(dto.ChassisNoPhoto);

            // 4) Patch VehicleDetails sub‐document
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

            // 5) Ensure CompositeKey stays in sync
            //doc.CompositeKey = GetPk(dto.RegistrationNumber, applicantContact).ToString();

            // 6) Overwrite (upsert) the full document
            await Container.UpsertItemAsync(doc, GetPk(dto.RegistrationNumber, applicantContact));
        }
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
