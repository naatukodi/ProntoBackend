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
            var chassisVerificationUrl = await UploadIf(dto.ChassisVerificationPhoto);
            if (chassisVerificationUrl != null)
                doc.InspectionDetails.ChassisVerificationPhotoUrl = chassisVerificationUrl;
            var chassisStencilTraceUrl = await UploadIf(dto.ChassisStencilTracePhoto);
            if (chassisStencilTraceUrl != null)
                doc.InspectionDetails.ChassisStencilTracePhotoUrl = chassisStencilTraceUrl;
            if (dto.Remarks != null)
                doc.InspectionDetails.Remarks = dto.Remarks;

            // --- Newly added fields ---
            if (dto.FuelSystem != null) doc.InspectionDetails.FuelSystem = dto.FuelSystem;
            if (dto.ExteriorCondition != null) doc.InspectionDetails.ExteriorCondition = dto.ExteriorCondition;
            if (dto.InteriorCondition != null) doc.InspectionDetails.InteriorCondition = dto.InteriorCondition;
            if (dto.DriveShafts != null) doc.InspectionDetails.DriveShafts = dto.DriveShafts;
            if (dto.SteeringSystem != null) doc.InspectionDetails.SteeringSystem = dto.SteeringSystem;
            if (dto.SteeringWheel != null) doc.InspectionDetails.SteeringWheel = dto.SteeringWheel;
            if (dto.SteeringColumn != null) doc.InspectionDetails.SteeringColumn = dto.SteeringColumn;
            if (dto.SteeringBox != null) doc.InspectionDetails.SteeringBox = dto.SteeringBox;
            if (dto.SteeringLinkages != null) doc.InspectionDetails.SteeringLinkages = dto.SteeringLinkages;
            if (dto.SteeringHandle != null) doc.InspectionDetails.SteeringHandle = dto.SteeringHandle;
            if (dto.FrontForkAssy != null) doc.InspectionDetails.FrontForkAssy = dto.FrontForkAssy;
            if (dto.Bonnet != null) doc.InspectionDetails.Bonnet = dto.Bonnet;
            if (dto.Bumpers != null) doc.InspectionDetails.Bumpers = dto.Bumpers;
            if (dto.Doors != null) doc.InspectionDetails.Doors = dto.Doors;
            if (dto.Fenders != null) doc.InspectionDetails.Fenders = dto.Fenders;
            if (dto.Mudguards != null) doc.InspectionDetails.Mudguards = dto.Mudguards;
            if (dto.AllGlasses != null) doc.InspectionDetails.AllGlasses = dto.AllGlasses;
            if (dto.FrontFairing != null) doc.InspectionDetails.FrontFairing = dto.FrontFairing;
            if (dto.RearCowls != null) doc.InspectionDetails.RearCowls = dto.RearCowls;
            if (dto.Boom != null) doc.InspectionDetails.Boom = dto.Boom;
            if (dto.Bucket != null) doc.InspectionDetails.Bucket = dto.Bucket;
            if (dto.ChainTrack != null) doc.InspectionDetails.ChainTrack = dto.ChainTrack;
            if (dto.HydraulicCylinders != null) doc.InspectionDetails.HydraulicCylinders = dto.HydraulicCylinders;
            if (dto.SwingUnit != null) doc.InspectionDetails.SwingUnit = dto.SwingUnit;
            if (dto.Upholstery != null) doc.InspectionDetails.Upholstery = dto.Upholstery;
            if (dto.InteriorTrims != null) doc.InspectionDetails.InteriorTrims = dto.InteriorTrims;
            if (dto.Front != null) doc.InspectionDetails.Front = dto.Front;
            if (dto.Rear != null) doc.InspectionDetails.Rear = dto.Rear;
            if (dto.Axles != null) doc.InspectionDetails.Axles = dto.Axles;
            if (dto.SpeedoMeter != null) doc.InspectionDetails.SpeedoMeter = dto.SpeedoMeter;
            if (dto.FrontAxles != null) doc.InspectionDetails.FrontAxles = dto.FrontAxles;
            if (dto.RearAxles != null) doc.InspectionDetails.RearAxles = dto.RearAxles;
            if (dto.AirConditioner != null) doc.InspectionDetails.AirConditioner = dto.AirConditioner;
            if (dto.Audio != null) doc.InspectionDetails.Audio = dto.Audio;
            if (dto.RightSideWing != null) doc.InspectionDetails.RightSideWing = dto.RightSideWing;
            if (dto.LeftSideWing != null) doc.InspectionDetails.LeftSideWing = dto.LeftSideWing;
            if (dto.TailGate != null) doc.InspectionDetails.TailGate = dto.TailGate;
            if (dto.LoadFloor != null) doc.InspectionDetails.LoadFloor = dto.LoadFloor;
            if (dto.ParkingBrake != null) doc.InspectionDetails.ParkingBrake = dto.ParkingBrake;
            if (dto.Abs != null) doc.InspectionDetails.Abs = dto.Abs;
            if (dto.TailLightsIndicators != null) doc.InspectionDetails.TailLightsIndicators = dto.TailLightsIndicators;
            if (dto.WiringAssy != null) doc.InspectionDetails.WiringAssy = dto.WiringAssy;
            if (dto.FrontCrashGuard != null) doc.InspectionDetails.FrontCrashGuard = dto.FrontCrashGuard;
            if (dto.RearCrashGuard != null) doc.InspectionDetails.RearCrashGuard = dto.RearCrashGuard;
            if (dto.AirBags != null) doc.InspectionDetails.AirBags = dto.AirBags;
            if (dto.SunRoof != null) doc.InspectionDetails.SunRoof = dto.SunRoof;
            if (dto.SideFenders != null) doc.InspectionDetails.SideFenders = dto.SideFenders;
            if (dto.HydraulicLift != null) doc.InspectionDetails.HydraulicLift = dto.HydraulicLift;
            if (dto.SideUnderRunProtection != null) doc.InspectionDetails.SideUnderRunProtection = dto.SideUnderRunProtection;
            if (dto.MainStand != null) doc.InspectionDetails.MainStand = dto.MainStand;
            if (dto.SideStand != null) doc.InspectionDetails.SideStand = dto.SideStand;
            if (dto.FrontMudGuard != null) doc.InspectionDetails.FrontMudGuard = dto.FrontMudGuard;
            if (dto.RearMudGuard != null) doc.InspectionDetails.RearMudGuard = dto.RearMudGuard;
            if (dto.FuelTankCondition != null) doc.InspectionDetails.FuelTankCondition = dto.FuelTankCondition;
            if (dto.ChainSprocket != null) doc.InspectionDetails.ChainSprocket = dto.ChainSprocket;
            if (dto.FrontBrakeCondition != null) doc.InspectionDetails.FrontBrakeCondition = dto.FrontBrakeCondition;
            if (dto.RearBrakeCondition != null) doc.InspectionDetails.RearBrakeCondition = dto.RearBrakeCondition;
            if (dto.HeadLight != null) doc.InspectionDetails.HeadLight = dto.HeadLight;
            if (dto.TailLight != null) doc.InspectionDetails.TailLight = dto.TailLight;
            if (dto.Indicators != null) doc.InspectionDetails.Indicators = dto.Indicators;
            if (dto.HornCondition != null) doc.InspectionDetails.HornCondition = dto.HornCondition;
            if (dto.MirrorCondition != null) doc.InspectionDetails.MirrorCondition = dto.MirrorCondition;
            if (dto.SeatCondition != null) doc.InspectionDetails.SeatCondition = dto.SeatCondition;
            if (dto.HandleBarGrips != null) doc.InspectionDetails.HandleBarGrips = dto.HandleBarGrips;
            if (dto.FootRest != null) doc.InspectionDetails.FootRest = dto.FootRest;
            if (dto.AlloyWheelRim != null) doc.InspectionDetails.AlloyWheelRim = dto.AlloyWheelRim;
            if (dto.Retarder != null) doc.InspectionDetails.Retarder = dto.Retarder;
            if (dto.DifferentialLock != null) doc.InspectionDetails.DifferentialLock = dto.DifferentialLock;
            if (dto.Pto != null) doc.InspectionDetails.Pto = dto.Pto;
            if (dto.HydraulicSystem != null) doc.InspectionDetails.HydraulicSystem = dto.HydraulicSystem;
            if (dto.BoomArm != null) doc.InspectionDetails.BoomArm = dto.BoomArm;
            if (dto.BucketCondition != null) doc.InspectionDetails.BucketCondition = dto.BucketCondition;
            if (dto.BladeCondition != null) doc.InspectionDetails.BladeCondition = dto.BladeCondition;
            if (dto.LiftingCapacity != null) doc.InspectionDetails.LiftingCapacity = dto.LiftingCapacity;
            if (dto.TyreConditionCe != null) doc.InspectionDetails.TyreConditionCe = dto.TyreConditionCe;
            if (dto.UnderCarriage != null) doc.InspectionDetails.UnderCarriage = dto.UnderCarriage;
            if (dto.CrawlerTracks != null) doc.InspectionDetails.CrawlerTracks = dto.CrawlerTracks;
            if (dto.SteelRims != null) doc.InspectionDetails.SteelRims = dto.SteelRims;
            if (dto.AttachmentCondition != null) doc.InspectionDetails.AttachmentCondition = dto.AttachmentCondition;
            if (dto.CabCondition != null) doc.InspectionDetails.CabCondition = dto.CabCondition;
            if (dto.CounterWeight != null) doc.InspectionDetails.CounterWeight = dto.CounterWeight;
            if (dto.RockBreaker != null) doc.InspectionDetails.RockBreaker = dto.RockBreaker;
            if (dto.CoachCondition != null) doc.InspectionDetails.CoachCondition = dto.CoachCondition;
            if (dto.PassengerSeats != null) doc.InspectionDetails.PassengerSeats = dto.PassengerSeats;
            if (dto.EmergencyExits != null) doc.InspectionDetails.EmergencyExits = dto.EmergencyExits;
            if (dto.LuggageCompartment != null) doc.InspectionDetails.LuggageCompartment = dto.LuggageCompartment;
            if (dto.AcSystem != null) doc.InspectionDetails.AcSystem = dto.AcSystem;
            if (dto.DestinationBoard != null) doc.InspectionDetails.DestinationBoard = dto.DestinationBoard;
            if (dto.SideMirrors != null) doc.InspectionDetails.SideMirrors = dto.SideMirrors;
            if (dto.RightIndividualBrakes != null) doc.InspectionDetails.RightIndividualBrakes = dto.RightIndividualBrakes;
            if (dto.LeftIndividualBrakes != null) doc.InspectionDetails.LeftIndividualBrakes = dto.LeftIndividualBrakes;
            if (dto.ThreePointLinkage != null) doc.InspectionDetails.ThreePointLinkage = dto.ThreePointLinkage;
            if (dto.PowerTakeOff != null) doc.InspectionDetails.PowerTakeOff = dto.PowerTakeOff;
            if (dto.HitchSystem != null) doc.InspectionDetails.HitchSystem = dto.HitchSystem;
            if (dto.HydraulicLiftFe != null) doc.InspectionDetails.HydraulicLiftFe = dto.HydraulicLiftFe;
            if (dto.FrontWeights != null) doc.InspectionDetails.FrontWeights = dto.FrontWeights;
            if (dto.RearWeights != null) doc.InspectionDetails.RearWeights = dto.RearWeights;
            if (dto.RopsCanopy != null) doc.InspectionDetails.RopsCanopy = dto.RopsCanopy;
            if (dto.FrontTyreCondition != null) doc.InspectionDetails.FrontTyreCondition = dto.FrontTyreCondition;
            if (dto.RearTyreCondition != null) doc.InspectionDetails.RearTyreCondition = dto.RearTyreCondition;
            if (dto.ImplementAttachments != null) doc.InspectionDetails.ImplementAttachments = dto.ImplementAttachments;
            if (dto.FuelTankFe != null) doc.InspectionDetails.FuelTankFe = dto.FuelTankFe;
            if (dto.FrontAxleFe != null) doc.InspectionDetails.FrontAxleFe = dto.FrontAxleFe;
            if (dto.RearDrawbar != null) doc.InspectionDetails.RearDrawbar = dto.RearDrawbar;

            // --- Excel-registry aligned fields ---
            if (dto.TyreCondition != null) doc.InspectionDetails.TyreCondition = dto.TyreCondition;
            if (dto.ElectricalSystem != null) doc.InspectionDetails.ElectricalSystem = dto.ElectricalSystem;
            if (dto.LoadBodyAssy != null) doc.InspectionDetails.LoadBodyAssy = dto.LoadBodyAssy;
            if (dto.BodyAssy != null) doc.InspectionDetails.BodyAssy = dto.BodyAssy;
            if (dto.CabinAssy != null) doc.InspectionDetails.CabinAssy = dto.CabinAssy;
            if (dto.FrontBrakes != null) doc.InspectionDetails.FrontBrakes = dto.FrontBrakes;
            if (dto.RearBrakes != null) doc.InspectionDetails.RearBrakes = dto.RearBrakes;
            if (dto.HeadLights != null) doc.InspectionDetails.HeadLights = dto.HeadLights;
            if (dto.FrontSuspension != null) doc.InspectionDetails.FrontSuspension = dto.FrontSuspension;
            if (dto.RearSuspension != null) doc.InspectionDetails.RearSuspension = dto.RearSuspension;
            if (dto.RightSideGate != null) doc.InspectionDetails.RightSideGate = dto.RightSideGate;
            if (dto.LeftSideGate != null) doc.InspectionDetails.LeftSideGate = dto.LeftSideGate;
            if (dto.FrontScoop != null) doc.InspectionDetails.FrontScoop = dto.FrontScoop;
            if (dto.RvMirrors != null) doc.InspectionDetails.RvMirrors = dto.RvMirrors;
            if (dto.LockSet != null) doc.InspectionDetails.LockSet = dto.LockSet;
            if (dto.SideCovers != null) doc.InspectionDetails.SideCovers = dto.SideCovers;
            if (dto.BellyPanels != null) doc.InspectionDetails.BellyPanels = dto.BellyPanels;
            if (dto.BrakeLeversFluid != null) doc.InspectionDetails.BrakeLeversFluid = dto.BrakeLeversFluid;
            if (dto.Silencer != null) doc.InspectionDetails.Silencer = dto.Silencer;
            if (dto.SilencerCover != null) doc.InspectionDetails.SilencerCover = dto.SilencerCover;
            if (dto.Accelerator != null) doc.InspectionDetails.Accelerator = dto.Accelerator;
            if (dto.HandleBar != null) doc.InspectionDetails.HandleBar = dto.HandleBar;
            if (dto.SteeringStem != null) doc.InspectionDetails.SteeringStem = dto.SteeringStem;
            if (dto.FrontShockAbsorber != null) doc.InspectionDetails.FrontShockAbsorber = dto.FrontShockAbsorber;
            if (dto.RearShockAbsorber != null) doc.InspectionDetails.RearShockAbsorber = dto.RearShockAbsorber;
            if (dto.LegGuard != null) doc.InspectionDetails.LegGuard = dto.LegGuard;
            if (dto.SareeGuard != null) doc.InspectionDetails.SareeGuard = dto.SareeGuard;
            if (dto.ChainGuard != null) doc.InspectionDetails.ChainGuard = dto.ChainGuard;
            if (dto.SelfStart != null) doc.InspectionDetails.SelfStart = dto.SelfStart;
            if (dto.Horn != null) doc.InspectionDetails.Horn = dto.Horn;
            if (dto.KickPedalFootRest != null) doc.InspectionDetails.KickPedalFootRest = dto.KickPedalFootRest;
            if (dto.FrontPanel != null) doc.InspectionDetails.FrontPanel = dto.FrontPanel;
            if (dto.FrontGlassFrame != null) doc.InspectionDetails.FrontGlassFrame = dto.FrontGlassFrame;
            if (dto.Switches != null) doc.InspectionDetails.Switches = dto.Switches;
            if (dto.LoadCarrier != null) doc.InspectionDetails.LoadCarrier = dto.LoadCarrier;
            if (dto.SteeringControlSystem != null) doc.InspectionDetails.SteeringControlSystem = dto.SteeringControlSystem;
            if (dto.CabinStructure != null) doc.InspectionDetails.CabinStructure = dto.CabinStructure;
            if (dto.DashboardControls != null) doc.InspectionDetails.DashboardControls = dto.DashboardControls;
            if (dto.GlassPanels != null) doc.InspectionDetails.GlassPanels = dto.GlassPanels;
            if (dto.BucketBlade != null) doc.InspectionDetails.BucketBlade = dto.BucketBlade;
            if (dto.PinsAndBushes != null) doc.InspectionDetails.PinsAndBushes = dto.PinsAndBushes;
            if (dto.ServiceBrake != null) doc.InspectionDetails.ServiceBrake = dto.ServiceBrake;
            if (dto.EmergencyStop != null) doc.InspectionDetails.EmergencyStop = dto.EmergencyStop;
            if (dto.Sensors != null) doc.InspectionDetails.Sensors = dto.Sensors;
            if (dto.SteeringControlLevers != null) doc.InspectionDetails.SteeringControlLevers = dto.SteeringControlLevers;
            if (dto.HydraulicSteeringPump != null) doc.InspectionDetails.HydraulicSteeringPump = dto.HydraulicSteeringPump;
            if (dto.SwivelJoints != null) doc.InspectionDetails.SwivelJoints = dto.SwivelJoints;
            if (dto.HydraulicOilCooler != null) doc.InspectionDetails.HydraulicOilCooler = dto.HydraulicOilCooler;
            if (dto.HydraulicPump != null) doc.InspectionDetails.HydraulicPump = dto.HydraulicPump;
            if (dto.HosesAndFittings != null) doc.InspectionDetails.HosesAndFittings = dto.HosesAndFittings;
            if (dto.SwingMechanism != null) doc.InspectionDetails.SwingMechanism = dto.SwingMechanism;
            if (dto.TrackChains != null) doc.InspectionDetails.TrackChains = dto.TrackChains;
            if (dto.Sprockets != null) doc.InspectionDetails.Sprockets = dto.Sprockets;
            if (dto.Rollers != null) doc.InspectionDetails.Rollers = dto.Rollers;
            if (dto.HourMeter != null) doc.InspectionDetails.HourMeter = dto.HourMeter;
            if (dto.BonnetGuard != null) doc.InspectionDetails.BonnetGuard = dto.BonnetGuard;
            if (dto.TorqueConverter != null) doc.InspectionDetails.TorqueConverter = dto.TorqueConverter;
            if (dto.FinalDrive != null) doc.InspectionDetails.FinalDrive = dto.FinalDrive;
            if (dto.BodyStructure != null) doc.InspectionDetails.BodyStructure = dto.BodyStructure;
            if (dto.DriverCabin != null) doc.InspectionDetails.DriverCabin = dto.DriverCabin;
            if (dto.BumpersAndGrilles != null) doc.InspectionDetails.BumpersAndGrilles = dto.BumpersAndGrilles;
            if (dto.SeatsAndBerths != null) doc.InspectionDetails.SeatsAndBerths = dto.SeatsAndBerths;
            if (dto.SideBodyPanels != null) doc.InspectionDetails.SideBodyPanels = dto.SideBodyPanels;
            if (dto.RearBodyPanels != null) doc.InspectionDetails.RearBodyPanels = dto.RearBodyPanels;
            if (dto.OperatorPlatform != null) doc.InspectionDetails.OperatorPlatform = dto.OperatorPlatform;
            if (dto.OperatorStation != null) doc.InspectionDetails.OperatorStation = dto.OperatorStation;
            if (dto.Canopy != null) doc.InspectionDetails.Canopy = dto.Canopy;
            if (dto.FrontGrilles != null) doc.InspectionDetails.FrontGrilles = dto.FrontGrilles;
            if (dto.BrakeEqualization != null) doc.InspectionDetails.BrakeEqualization = dto.BrakeEqualization;
            if (dto.FanAssy != null) doc.InspectionDetails.FanAssy = dto.FanAssy;
            if (dto.RearAxleFe != null) doc.InspectionDetails.RearAxleFe = dto.RearAxleFe;
            if (dto.TieRodsJoints != null) doc.InspectionDetails.TieRodsJoints = dto.TieRodsJoints;
            if (dto.Muffler != null) doc.InspectionDetails.Muffler = dto.Muffler;
            if (dto.AirFilter != null) doc.InspectionDetails.AirFilter = dto.AirFilter;
            if (dto.DropArm != null) doc.InspectionDetails.DropArm = dto.DropArm;
            if (dto.AttachmentHitch != null) doc.InspectionDetails.AttachmentHitch = dto.AttachmentHitch;

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