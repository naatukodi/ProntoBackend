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
                return doc.InspectionDetails is null ? null : new InspectionDetails
                {
                    VehicleInspectedBy = doc.InspectionDetails.VehicleInspectedBy,
                    DateOfInspection = doc.InspectionDetails.DateOfInspection,
                    InspectionLocation = doc.InspectionDetails.InspectionLocation,
                    VehicleMoved = doc.InspectionDetails.VehicleMoved,
                    EngineStarted = doc.InspectionDetails.EngineStarted,
                    Odometer = doc.InspectionDetails.Odometer,
                    VinPlate = doc.InspectionDetails.VinPlate,
                    BodyType = doc.InspectionDetails.BodyType,
                    OverallTyreCondition = doc.InspectionDetails.OverallTyreCondition,
                    OtherAccessoryFitment = doc.InspectionDetails.OtherAccessoryFitment,
                    WindshieldGlass = doc.InspectionDetails.WindshieldGlass,
                    RoadWorthyCondition = doc.InspectionDetails.RoadWorthyCondition,
                    EngineCondition = doc.InspectionDetails.EngineCondition,
                    SuspensionSystem = doc.InspectionDetails.SuspensionSystem,
                    SteeringAssy = doc.InspectionDetails.SteeringAssy,
                    BrakeSystem = doc.InspectionDetails.BrakeSystem,
                    ChassisCondition = doc.InspectionDetails.ChassisCondition,
                    BodyCondition = doc.InspectionDetails.BodyCondition,
                    BatteryCondition = doc.InspectionDetails.BatteryCondition,
                    PaintWork = doc.InspectionDetails.PaintWork,
                    ClutchSystem = doc.InspectionDetails.ClutchSystem,
                    GearBoxAssy = doc.InspectionDetails.GearBoxAssy,
                    PropellerShaft = doc.InspectionDetails.PropellerShaft,
                    DifferentialAssy = doc.InspectionDetails.DifferentialAssy,
                    Cabin = doc.InspectionDetails.Cabin,
                    Dashboard = doc.InspectionDetails.Dashboard,
                    Seats = doc.InspectionDetails.Seats,
                    HeadLamps = doc.InspectionDetails.HeadLamps,
                    ElectricAssembly = doc.InspectionDetails.ElectricAssembly,
                    Radiator = doc.InspectionDetails.Radiator,
                    Intercooler = doc.InspectionDetails.Intercooler,
                    AllHosePipes = doc.InspectionDetails.AllHosePipes,
                    Photos = doc.InspectionDetails.Photos
                };
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
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