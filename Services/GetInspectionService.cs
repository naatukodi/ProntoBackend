using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Valuation.Api.Models;
using System.Net;

namespace Valuation.Api.Services
{
    public class GetInspectionService : IGetInspectionService
    {
        private readonly CosmosClient _cosmos;
        private readonly BlobServiceClient _blobService;
        private readonly string _dbId;
        private readonly string _containerId;
        private readonly string _blobContainerName;

        public GetInspectionService(
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
            doc.InspectionDetails = new InspectionDetails
            {
                VehicleInspectedBy = dto.VehicleInspectedBy,
                DateOfInspection = dto.DateOfInspection,
                InspectionLocation = dto.InspectionLocation,
                VehicleMoved = dto.VehicleMoved,
                EngineStarted = dto.EngineStarted,
                Odometer = dto.Odometer,
                VinPlate = dto.VinPlate,
                BodyType = dto.BodyType,
                OverallTyreCondition = dto.OverallTyreCondition,
                OtherAccessoryFitment = dto.OtherAccessoryFitment,
                WindshieldGlass = dto.WindshieldGlass,
                RoadWorthyCondition = dto.RoadWorthyCondition,
                EngineCondition = dto.EngineCondition,
                SuspensionSystem = dto.SuspensionSystem,
                SteeringAssy = dto.SteeringAssy,
                BrakeSystem = dto.BrakeSystem,
                ChassisCondition = dto.ChassisCondition,
                BodyCondition = dto.BodyCondition,
                BatteryCondition = dto.BatteryCondition,
                PaintWork = dto.PaintWork,
                ClutchSystem = dto.ClutchSystem,
                GearBoxAssy = dto.GearBoxAssy,
                PropellerShaft = dto.PropellerShaft,
                DifferentialAssy = dto.DifferentialAssy,
                Cabin = dto.Cabin,
                Dashboard = dto.Dashboard,
                Seats = dto.Seats,
                HeadLamps = dto.HeadLamps,
                ElectricAssembly = dto.ElectricAssembly,
                Radiator = dto.Radiator,
                Intercooler = dto.Intercooler,
                AllHosePipes = dto.AllHosePipes,
                Photos = photoUrls
            };

            // 4) Upsert
            await container.UpsertItemAsync(doc, pk);
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