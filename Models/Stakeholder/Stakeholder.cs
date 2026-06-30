




// Models/Stakeholder.cs
namespace Valuation.Api.Models
{
    public class Stakeholder
    {
        public string Name { get; set; } = default!;
        public string? Branch { get; set; }
        public string ExecutiveName { get; set; } = default!;
        public string ExecutiveContact { get; set; } = default!;
        public string? ExecutiveWhatsapp { get; set; }
        public string? ExecutiveEmail { get; set; }
        public string? ValuationType { get; set; }
        public string? VehicleSegment { get; set; }
        public VehicleLocation VehicleLocation { get; set; } = new VehicleLocation();
        public Applicant Applicant { get; set; } = new Applicant();
        public List<Document> Documents { get; set; } = new List<Document>();

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? Remarks { get; set; }


        
    }
}
