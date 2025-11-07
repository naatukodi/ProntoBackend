using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Valuation.Api.Models;
using System.Net;
using Polly;

namespace Valuation.Api.Services
{
    public class GetInspectionService : IGetInspectionService
    {
        private readonly CosmosClient _cosmos;
        private readonly BlobServiceClient _blobService;
        private readonly string _dbId;
        private readonly string _containerId;
        private readonly string _blobContainerName;

        private readonly IWorkflowTableService _workflowTableService;

        public GetInspectionService(
            CosmosClient cosmos,
            BlobServiceClient blobService,
            IConfiguration configuration,
            IWorkflowTableService workflowTableService)
        {
            _cosmos = cosmos;
            _blobService = blobService;
            _dbId = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
            _containerId = configuration["Cosmos:ContainerId"] ?? "Valuations";
            _workflowTableService = workflowTableService;
            _blobContainerName = configuration["Blob:ContainerName"] ?? "vehicle-documents";
        }
        private Container Container =>
            _cosmos.GetDatabase(_dbId).GetContainer(_containerId);
        private PartitionKey GetPk(string vehicleNumber, string applicantContact) =>
            new($"{vehicleNumber}|{applicantContact}");
        public async Task<InspectionDetails?> GetInspectionAsync(
            string id, string vehicleNumber, string applicantContact)
        {
            try
            {
                var resp = await Container.ReadItemAsync<ValuationDocument>(
                    id: id,
                    partitionKey: GetPk(vehicleNumber, applicantContact));

                var doc = resp.Resource;
                return doc.InspectionDetails ?? new InspectionDetails();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new InspectionDetails();
            }
        }

        public async Task UpdateInspectionAsync(string id, InspectionDetailsDto dto, string vehicleNumber, string applicantContact)
        {
            var container = _cosmos
                .GetDatabase(_dbId)
                .GetContainer(_containerId);
            // 1) Compute composite PK
            var compositeKey = $"{vehicleNumber}|{applicantContact}";
            var pk = new PartitionKey(compositeKey);

            // 2) Try to read existing, or create new
            ValuationDocument doc;
            try
            {
                var resp = await Container.ReadItemAsync<ValuationDocument>(
                id: id,
                partitionKey: GetPk(vehicleNumber, applicantContact));

                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                doc = new ValuationDocument
                {
                    id = id,
                    CompositeKey = compositeKey,
                    VehicleNumber = vehicleNumber,
                    ApplicantContact = applicantContact
                };
            }

            // 2) Upload photos
            async Task<string?> UploadIf(IFormFile? file)
            {
                if (file == null) return null;

                var containerClient = _blobService.GetBlobContainerClient(_blobContainerName);
                var blobName = $"{vehicleNumber}/{applicantContact}/{Guid.NewGuid()}-{file.FileName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                var headers = new BlobHttpHeaders { ContentType = file.ContentType };
                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, headers);
                return blobClient.Uri.ToString();
            }
            var photoUrls = new List<string>();
            if (dto.Photos != null)
                foreach (var f in dto.Photos)
                    if (await UploadIf(f) is string u)
                        photoUrls.Add(u);

            // 3) Patch sub‐document
            if (doc.InspectionDetails == null)
                doc.InspectionDetails = new InspectionDetails();

            if (dto.VehicleInspectedBy != null)
                doc.InspectionDetails.VehicleInspectedBy = dto.VehicleInspectedBy;
            if (dto.DateOfInspection != null)
                doc.InspectionDetails.DateOfInspection = dto.DateOfInspection;
            if (dto.InspectionLocation != null)
                doc.InspectionDetails.InspectionLocation = dto.InspectionLocation;
            if (dto.VehicleMoved != null)
                doc.InspectionDetails.VehicleMoved = dto.VehicleMoved;
            if (dto.EngineStarted != null)
                doc.InspectionDetails.EngineStarted = dto.EngineStarted;
            if (dto.Odometer != null)
                doc.InspectionDetails.Odometer = dto.Odometer;
            if (dto.VinPlate != null)
                doc.InspectionDetails.VinPlate = dto.VinPlate;
            if (dto.BodyType != null)
                doc.InspectionDetails.BodyType = dto.BodyType;
            if (dto.OverallTyreCondition != null)
                doc.InspectionDetails.OverallTyreCondition = dto.OverallTyreCondition;
            if (dto.OtherAccessoryFitment != null)
                doc.InspectionDetails.OtherAccessoryFitment = dto.OtherAccessoryFitment;
            if (dto.WindshieldGlass != null)
                doc.InspectionDetails.WindshieldGlass = dto.WindshieldGlass;
            if (dto.RoadWorthyCondition != null)
                doc.InspectionDetails.RoadWorthyCondition = dto.RoadWorthyCondition;
            if (dto.EngineCondition != null)
                doc.InspectionDetails.EngineCondition = dto.EngineCondition;
            if (dto.SuspensionSystem != null)
                doc.InspectionDetails.SuspensionSystem = dto.SuspensionSystem;
            if (dto.SteeringAssy != null)
                doc.InspectionDetails.SteeringAssy = dto.SteeringAssy;
            if (dto.BrakeSystem != null)
                doc.InspectionDetails.BrakeSystem = dto.BrakeSystem;
            if (dto.ChassisCondition != null)
                doc.InspectionDetails.ChassisCondition = dto.ChassisCondition;
            if (dto.BodyCondition != null)
                doc.InspectionDetails.BodyCondition = dto.BodyCondition;
            if (dto.BatteryCondition != null)
                doc.InspectionDetails.BatteryCondition = dto.BatteryCondition;
            if (dto.PaintWork != null)
                doc.InspectionDetails.PaintWork = dto.PaintWork;
            if (dto.ClutchSystem != null)
                doc.InspectionDetails.ClutchSystem = dto.ClutchSystem;
            if (dto.GearBoxAssy != null)
                doc.InspectionDetails.GearBoxAssy = dto.GearBoxAssy;
            if (dto.PropellerShaft != null)
                doc.InspectionDetails.PropellerShaft = dto.PropellerShaft;
            if (dto.DifferentialAssy != null)
                doc.InspectionDetails.DifferentialAssy = dto.DifferentialAssy;
            if (dto.Cabin != null)
                doc.InspectionDetails.Cabin = dto.Cabin;
            if (dto.Dashboard != null)
                doc.InspectionDetails.Dashboard = dto.Dashboard;
            if (dto.Seats != null)
                doc.InspectionDetails.Seats = dto.Seats;
            if (dto.HeadLamps != null)
                doc.InspectionDetails.HeadLamps = dto.HeadLamps;
            if (dto.ElectricAssembly != null)
                doc.InspectionDetails.ElectricAssembly = dto.ElectricAssembly;
            if (dto.Radiator != null)
                doc.InspectionDetails.Radiator = dto.Radiator;
            if (dto.Intercooler != null)
                doc.InspectionDetails.Intercooler = dto.Intercooler;
            if (dto.AllHosePipes != null)
                doc.InspectionDetails.AllHosePipes = dto.AllHosePipes;
            if (photoUrls.Count > 0)
                doc.InspectionDetails.Photos = photoUrls;
            if (dto.Remarks != null)
                doc.InspectionDetails.Remarks = dto.Remarks;


            // 4) Upsert
            await container.UpsertItemAsync(doc, pk);

        }

        public async Task UpdateAssignmentAsync(
            string valuationId, string vehicleNumber, string applicantContact,
            string? assignedTo, string? assignedToPhoneNumber, string? assignedToEmail, string? assignedToWhatsapp)
        {
            var pk = GetPk(vehicleNumber, applicantContact);
            try
            {
                var resp = await Container.ReadItemAsync<ValuationDocument>(valuationId, pk);
                var doc = resp.Resource;

                doc.AssignedTo = assignedTo;
                if (doc.InspectionDetails == null)
                {
                    doc.InspectionDetails = new InspectionDetails();
                }
                doc.InspectionDetails.AssignedTo = Uri.UnescapeDataString(assignedTo ?? "");
                doc.InspectionDetails.AssignedToPhoneNumber = Uri.UnescapeDataString(assignedToPhoneNumber ?? "");
                doc.InspectionDetails.AssignedToEmail = Uri.UnescapeDataString(assignedToEmail ?? "");
                doc.InspectionDetails.AssignedToWhatsapp = Uri.UnescapeDataString(assignedToWhatsapp ?? "");

                await Container.UpsertItemAsync(doc, pk);

                // Update the workflow table as well
                // Retry the workflow update with Polly
                var policy = Polly.Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
                await policy.ExecuteAsync(async () =>
                {
                    await _workflowTableService.AVOWFUpdateAssignmentAsync(
                        valuationId,
                        vehicleNumber,
                        applicantContact,
                        Uri.UnescapeDataString(assignedTo ?? string.Empty),
                        Uri.UnescapeDataString(assignedToPhoneNumber ?? string.Empty),
                        Uri.UnescapeDataString(assignedToEmail ?? string.Empty),
                        Uri.UnescapeDataString(assignedToWhatsapp ?? string.Empty));

                });
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // If not found, create a new document
                var newDoc = new ValuationDocument
                {
                    id = valuationId,
                    CompositeKey = $"{vehicleNumber}|{applicantContact}",
                    VehicleNumber = vehicleNumber,
                    ApplicantContact = applicantContact,
                    InspectionDetails = new InspectionDetails()
                };

                newDoc.InspectionDetails.AssignedTo = assignedTo;
                newDoc.InspectionDetails.AssignedToPhoneNumber = assignedToPhoneNumber;
                newDoc.InspectionDetails.AssignedToEmail = assignedToEmail;
                newDoc.InspectionDetails.AssignedToWhatsapp = assignedToWhatsapp;

                await Container.UpsertItemAsync(newDoc, pk);

                // Update the workflow table as well
                var policy = Polly.Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
                await policy.ExecuteAsync(async () =>
                {
                    await _workflowTableService.AVOWFUpdateAssignmentAsync(
                        valuationId,
                        vehicleNumber,
                        applicantContact,
                        assignedTo ?? string.Empty,
                        assignedToPhoneNumber ?? string.Empty,
                        assignedToEmail ?? string.Empty,
                        assignedToWhatsapp ?? string.Empty);
                });

            }
        }

        public async Task DeleteInspectionAsync(string id, string vehicleNumber, string applicantContact)
        {
            var pk = GetPk(vehicleNumber, applicantContact);
            try
            {
                var resp = await Container.ReadItemAsync<ValuationDocument>(id, pk);
                var doc = resp.Resource;
                doc.InspectionDetails = null;
                await Container.UpsertItemAsync(doc, pk);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // nothing to delete
            }
        }
    }
}