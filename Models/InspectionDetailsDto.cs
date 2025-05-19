// Models/InspectionDetailsDto.cs
namespace Valuation.Api.Models
{
    public class InspectionDetailsDto
    {
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
    }
}
