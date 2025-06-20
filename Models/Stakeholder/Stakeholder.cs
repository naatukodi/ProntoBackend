




// Models/Stakeholder.cs
namespace Valuation.Api.Models
{
    public class Stakeholder
    {
        public string Name { get; set; } = default!;
        public string ExecutiveName { get; set; } = default!;
        public string ExecutiveContact { get; set; } = default!;
        public string? ExecutiveWhatsapp { get; set; }
        public string? ExecutiveEmail { get; set; }
        public string? ValuationType { get; set; }
        public string? VehicleSegment { get; set; }
        public VehicleLocation VehicleLocation { get; set; } = new VehicleLocation();
        public Applicant Applicant { get; set; } = new Applicant();
        public List<Document> Documents { get; set; } = new List<Document>();
    }
}
