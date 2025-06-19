// Models/PincodeModel.cs
namespace Valuation.Api.Models
{
    public class PincodeModel
    {
        public string Name { get; set; } = default!;
        public string Block { get; set; } = default!;
        public string State { get; set; } = default!;
        public string Country { get; set; } = default!;
        public string Pincode { get; set; } = default!;
    }
}
