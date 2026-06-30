// Models/VehicleLocation.cs
namespace Valuation.Api.Models
{
    public class VehicleLocation
    {
        public string? Pincode { get; set; }
        public string? Name { get; set; }
        public string? Block { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? District { get; set; }
        public string? Division { get; set; }
    }
}