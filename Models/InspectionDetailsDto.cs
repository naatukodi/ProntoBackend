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
    }
}
