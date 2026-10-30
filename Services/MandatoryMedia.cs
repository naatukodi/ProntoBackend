using System;
using System.Collections.Generic;

namespace Valuation.Api.Services
{
    /// <summary>
    /// The photos (and the video) a case must have before the AVO step can move on,
    /// and what each photo field is called on screen. Shared by the photo check behind
    /// the AVO page's Save and by the upload log that tells the office what arrived.
    /// </summary>
    public static class MandatoryMedia
    {
        // The video is checked on its own. It used to be in this list as well, so a case
        // without one reported it twice ("VehicleVideo" and "Vehicle Video") and the AVO
        // was told 18 items were missing, not 17.
        public static readonly IReadOnlyList<string> PhotoFields = new[]
        {
            "FrontLeftSide", "FrontRightSide", "RearLeftSide", "RearRightSide",
            "FrontViewGrille", "RearViewTailgate", "DriverSideProfile", "PassengerSideProfile",
            "EngineBay", "VinPlate", "ChassisImprint", "Odometer",
            "SelfieWithVehicle",
            "ChassisVerification", "ChassisStencilTrace", "WorkingOperationPhoto"
        };

        public const string VideoField = "VehicleVideo";

        private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "FrontLeftSide", "Front Left Side" }, { "FrontRightSide", "Front Right Side" },
            { "RearLeftSide", "Rear Left Side" }, { "RearRightSide", "Rear Right Side" },
            { "FrontViewGrille", "Front View (Grille)" }, { "RearViewTailgate", "Rear View (Tailgate)" },
            { "DriverSideProfile", "Driver's Side Profile" }, { "PassengerSideProfile", "Passenger Side Profile" },
            { "Dashboard", "Dashboard" }, { "InstrumentCluster", "Instrument Cluster" },
            { "EngineBay", "Engine Bay" }, { "VinPlate", "VIN Plate" },
            { "ChassisImprint", "Chassis Imprint" },
            { "GearInterior", "Gear (Interior)" }, { "FrontSeat", "Front Seat" }, { "RearSeat", "Rear Seat" },
            { "DashboardCloseup", "Dashboard Close-up" }, { "Odometer", "Odometer" },
            { "SelfieWithVehicle", "Selfie with Vehicle" },
            { "TireFrontLeft", "Tire Front Left" }, { "TireFrontRight", "Tire Front Right" },
            { "TireRearLeft", "Tire Rear Left" }, { "TireRearRight", "Tire Rear Right" },
            { "Underbody", "Under Body" },
            { "ChassisVerification", "Chassis Verification" }, { "ChassisStencilTrace", "Chassis Stencil Trace" },
            { "WorkingOperationPhoto", "Working / Operation Photo" },
            { VideoField, "Vehicle Video" }
        };

        public static string DisplayName(string field) =>
            DisplayNames.TryGetValue(field, out var name) ? name : field;

        /// <summary>
        /// On-screen names of the mandatory items the case lacks, photos in checklist
        /// order and then the video. Both maps are field name → URL; the photo service
        /// keeps photos and video apart, while an upload's reply merges them, so the
        /// same map may be passed twice.
        /// </summary>
        public static List<string> Missing(IReadOnlyDictionary<string, string>? photoUrls,
                                           IReadOnlyDictionary<string, string>? videoUrls)
        {
            var photos = Normalise(photoUrls);
            var videos = Normalise(videoUrls);

            var missing = new List<string>();
            foreach (var field in PhotoFields)
            {
                if (!photos.TryGetValue(field, out var url) || string.IsNullOrWhiteSpace(url))
                    missing.Add(DisplayName(field));
            }

            if (!videos.TryGetValue(VideoField, out var video) || string.IsNullOrWhiteSpace(video))
                missing.Add(DisplayName(VideoField));

            return missing;
        }

        private static Dictionary<string, string> Normalise(IReadOnlyDictionary<string, string>? map)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (map != null) foreach (var kvp in map) result[kvp.Key] = kvp.Value;
            return result;
        }
    }
}
