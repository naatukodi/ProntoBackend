// Models/StakeholderUpdateDto.cs
using Microsoft.AspNetCore.Http;

namespace Valuation.Api.Models
{
    public class StakeholderUpdateDto
    {
        public string ValuationId { get; set; } = default!;
        // Location fields
        public string? Pincode { get; set; }
        public string? LocationName { get; set; }
        public string? Block { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }

        // Stakeholder
        public string Name { get; set; } = default!;
        public string? Branch { get; set; }
        public string ExecutiveName { get; set; } = default!;
        public string ExecutiveContact { get; set; } = default!;
        public string? ExecutiveWhatsapp { get; set; }
        public string? ExecutiveEmail { get; set; }
        public string? ValuationType { get; set; }
        public string? VehicleSegment { get; set; }
        public string? VehicleNumber { get; set; }

        public string? District { get; set; } = default!;
        public string? Division { get; set; } = default!;

        public string ApplicantName { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;
        public string? ApplicantAlternativeContact { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? Remarks { get; set; }


        // Files
        public IFormFile? RcFile { get; set; }
        public IFormFile? InsuranceFile { get; set; }
        public IFormFileCollection? OtherFiles { get; set; }
    }
}