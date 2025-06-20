// Models/StakeholderUpdateDto.cs
using Microsoft.AspNetCore.Http;

namespace Valuation.Api.Models
{
    public class StakeholderUpdateDto
    {
        public string ValuationId { get; set; } = default!;
        // Location fields
        public string Pincode { get; set; } = default!;
        public string LocationName { get; set; } = default!;
        public string Block { get; set; } = default!;
        public string State { get; set; } = default!;
        public string Country { get; set; } = default!;

        // Stakeholder
        public string Name { get; set; } = default!;
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

        // Files
        public IFormFile? RcFile { get; set; }
        public IFormFile? InsuranceFile { get; set; }
        public IFormFileCollection? OtherFiles { get; set; }
    }
}