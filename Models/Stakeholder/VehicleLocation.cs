// Models/VehicleLocation.cs
namespace Valuation.Api.Models
{
    public class VehicleLocation
    {
        public string Pincode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Block { get; set; } = default!;
        public string State { get; set; } = default!;
        public string Country { get; set; } = default!;
        public string District { get; set; } = default!;
        public string Division { get; set; } = default!;
    }
}