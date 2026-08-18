using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Valuation.Api.Models
{
    public class VehiclePhotosDto
    {
        public string ValuationId { get; set; } = string.Empty;
        public string VehicleNumber { get; set; } = string.Empty;
        public string ApplicantContact { get; set; } = string.Empty;

        public IFormFile? FrontLeftSide { get; set; }
        public IFormFile? FrontRightSide { get; set; }
        public IFormFile? RearLeftSide { get; set; }
        public IFormFile? RearRightSide { get; set; }
        public IFormFile? FrontViewGrille { get; set; }
        public IFormFile? RearViewTailgate { get; set; }
        public IFormFile? DriverSideProfile { get; set; }
        public IFormFile? PassengerSideProfile { get; set; }
        public IFormFile? Dashboard { get; set; }
        public IFormFile? InstrumentCluster { get; set; }
        public IFormFile? EngineBay { get; set; }
        public IFormFile? VinPlate { get; set; }
        public IFormFile? ChassisImprint { get; set; }
        public IFormFile? GearInterior { get; set; }
        public IFormFile? FrontSeat { get; set; }
        public IFormFile? RearSeat { get; set; }
        public IFormFile? DashboardCloseup { get; set; }
        public IFormFile? Odometer { get; set; }
        public IFormFile? SelfieWithVehicle { get; set; }
        public IFormFile? Underbody { get; set; }
        public IFormFile? TireFrontLeft { get; set; }
        public IFormFile? TireFrontRight { get; set; }
        public IFormFile? TireRearLeft { get; set; }
        public IFormFile? TireRearRight { get; set; }
        public IFormFile? VehicleVideo { get; set; }
        public IFormFile? ChassisVerification { get; set; }
        public IFormFile? ChassisStencilTrace { get; set; }
        public IFormFile? WorkingOperationPhoto { get; set; }

        // ✅ NEW: Properties for Custom Images
        public IList<IFormFile>? CustomImageFiles { get; set; }
        public string? CustomImagesMetadata { get; set; }
    }

    public class AnnotatePhotoRequest
    {
        public string Note { get; set; } = string.Empty;
    }
}