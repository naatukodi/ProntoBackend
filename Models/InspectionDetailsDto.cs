// Models/InspectionDetailsDto.cs
using Microsoft.AspNetCore.Http;
namespace Valuation.Api.Models
{
    public class InspectionDetailsDto
    {
        // --- Original fields ---
        public string VehicleInspectedBy { get; set; } = default!;
        public DateTime? DateOfInspection { get; set; }
        public string? InspectionLocation { get; set; }
        public bool? VehicleMoved { get; set; }
        public bool? EngineStarted { get; set; }
        public long? Odometer { get; set; }
        public bool? VinPlate { get; set; }
        public string? BodyType { get; set; }
        public string? OverallTyreCondition { get; set; }
        public bool? OtherAccessoryFitment { get; set; }
        public string? WindshieldGlass { get; set; }
        public bool? RoadWorthyCondition { get; set; }

        public string? EngineCondition { get; set; }
        public string? SuspensionSystem { get; set; }
        public string? SteeringAssy { get; set; }
        public string? BrakeSystem { get; set; }
        public string? ChassisCondition { get; set; }
        public string? BodyCondition { get; set; }
        public string? BatteryCondition { get; set; }
        public string? PaintWork { get; set; }

        public string? ClutchSystem { get; set; }
        public string? GearBoxAssy { get; set; }
        public string? PropellerShaft { get; set; }
        public string? DifferentialAssy { get; set; }

        public string? Cabin { get; set; }
        public string? Dashboard { get; set; }
        public string? Seats { get; set; }

        public string? HeadLamps { get; set; }
        public string? ElectricAssembly { get; set; }

        public string? Radiator { get; set; }
        public string? Intercooler { get; set; }
        public string? AllHosePipes { get; set; }

        // Multiple uploads
        public IList<IFormFile>? Photos { get; set; }


        // --- Newly requested additional fields ---
        public IFormFile? FrontPhoto { get; set; }

        public string? FuelSystem { get; set; }
        public string? ExteriorCondition { get; set; }
        public string? InteriorCondition { get; set; }
        public string? DriveShafts { get; set; }
        public string? SteeringSystem { get; set; }
        public string? SteeringWheel { get; set; }
        public string? SteeringColumn { get; set; }
        public string? SteeringBox { get; set; }
        public string? SteeringLinkages { get; set; }
        public string? SteeringHandle { get; set; }
        public string? FrontForkAssy { get; set; }
        public string? Bonnet { get; set; }
        public string? Bumpers { get; set; }
        public string? Doors { get; set; }
        public string? Fenders { get; set; }
        public string? Mudguards { get; set; }
        public string? AllGlasses { get; set; }
        public string? FrontFairing { get; set; }
        public string? RearCowls { get; set; }
        public string? Boom { get; set; }
        public string? Bucket { get; set; }
        public string? ChainTrack { get; set; }
        public string? HydraulicCylinders { get; set; }
        public string? SwingUnit { get; set; }
        public string? Upholstery { get; set; }
        public string? InteriorTrims { get; set; }
        public string? SpeedoMeter { get; set; }
        public string? FrontAxles { get; set; }
        public string? RearAxles { get; set; }
        public string? AirConditioner { get; set; }
        public string? Audio { get; set; }
        public string? RightSideWing { get; set; }
        public string? LeftSideWing { get; set; }
        public string? TailGate { get; set; }
        public string? LoadFloor { get; set; }

        // Brakes Additional
        public string? ParkingBrake { get; set; }
        public string? Abs { get; set; }

        // Electrical Additional
        public string? TailLightsIndicators { get; set; }
        public string? WiringAssy { get; set; }

        // Crash Guards
        public string? FrontCrashGuard { get; set; }
        public string? RearCrashGuard { get; set; }

        // 4W Specific
        public string? AirBags { get; set; }
        public string? SunRoof { get; set; }
        public string? SideFenders { get; set; }

        // CV Specific
        public string? HydraulicLift { get; set; }
        public string? SideUnderRunProtection { get; set; }

        // 2W Specific
        public string? MainStand { get; set; }
        public string? SideStand { get; set; }
        public string? FrontMudGuard { get; set; }
        public string? RearMudGuard { get; set; }
        public string? FuelTankCondition { get; set; }
        public string? ChainSprocket { get; set; }
        public string? FrontBrakeCondition { get; set; }
        public string? RearBrakeCondition { get; set; }
        public string? HeadLight { get; set; }
        public string? TailLight { get; set; }
        public string? Indicators { get; set; }
        public string? HornCondition { get; set; }
        public string? MirrorCondition { get; set; }
        public string? SeatCondition { get; set; }
        public string? HandleBarGrips { get; set; }
        public string? FootRest { get; set; }
        public string? AlloyWheelRim { get; set; }

        // CE Specific
        public string? Retarder { get; set; }
        public string? DifferentialLock { get; set; }
        public string? Pto { get; set; }
        public string? HydraulicSystem { get; set; }
        public string? BoomArm { get; set; }
        public string? BucketCondition { get; set; }
        public string? BladeCondition { get; set; }
        public string? LiftingCapacity { get; set; }
        public string? TyreConditionCe { get; set; }
        public string? UnderCarriage { get; set; }
        public string? CrawlerTracks { get; set; }
        public string? SteelRims { get; set; }
        public string? AttachmentCondition { get; set; }
        public string? CabCondition { get; set; }
        public string? CounterWeight { get; set; }
        public string? RockBreaker { get; set; }

        // BUS Specific
        public string? CoachCondition { get; set; }
        public string? PassengerSeats { get; set; }
        public string? EmergencyExits { get; set; }
        public string? LuggageCompartment { get; set; }
        public string? AcSystem { get; set; }
        public string? DestinationBoard { get; set; }
        public string? SideMirrors { get; set; }

        // FE Specific
        public string? RightIndividualBrakes { get; set; }
        public string? LeftIndividualBrakes { get; set; }
        public string? ThreePointLinkage { get; set; }
        public string? PowerTakeOff { get; set; }
        public string? HitchSystem { get; set; }
        public string? HydraulicLiftFe { get; set; }
        public string? FrontWeights { get; set; }
        public string? RearWeights { get; set; }
        public string? RopsCanopy { get; set; }
        public string? FrontTyreCondition { get; set; }
        public string? RearTyreCondition { get; set; }
        public string? ImplementAttachments { get; set; }
        public string? FuelTankFe { get; set; }
        public string? FrontAxleFe { get; set; }
        public string? RearDrawbar { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? Remarks { get; set; }
    }
}
