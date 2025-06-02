// src/Valuation.Api/Models/VehiclePhotosDto.cs
namespace Valuation.Api.Models
{
    /// <summary>
    /// Contains up to 19 optional IFormFile properties (one for each required / optional photo).
    /// </summary>
    public class VehiclePhotosDto
    {
        public string ValuationId { get; set; } = null!;     // GUID as string
        public string VehicleNumber { get; set; } = null!;
        public string ApplicantContact { get; set; } = null!;

        // 1) Exterior angles
        public IFormFile? FrontLeftSide { get; set; }
        public IFormFile? FrontRightSide { get; set; }
        public IFormFile? RearLeftSide { get; set; }
        public IFormFile? RearRightSide { get; set; }
        public IFormFile? FrontViewGrille { get; set; }
        public IFormFile? RearViewTailgate { get; set; }
        public IFormFile? DriverSideProfile { get; set; }
        public IFormFile? PassengerSideProfile { get; set; }

        // 2) Interior & engine
        public IFormFile? Dashboard { get; set; }
        public IFormFile? InstrumentCluster { get; set; }
        public IFormFile? EngineBay { get; set; }
        public IFormFile? GearAndSeats { get; set; }
        public IFormFile? DashboardCloseup { get; set; }
        public IFormFile? Odometer { get; set; }

        // 3) Identification
        public IFormFile? ChassisNumberPlate { get; set; }
        public IFormFile? ChassisImprint { get; set; }

        // 4) Miscellaneous
        public IFormFile? SelfieWithVehicle { get; set; }
        public IFormFile? Underbody { get; set; }       // optional
        public IFormFile? TiresAndRims { get; set; }
    }
}
