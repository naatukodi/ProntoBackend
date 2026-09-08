namespace Valuation.Api.Models
{
    public class WorkflowModel
    {
        public string ValuationId { get; set; } = default!;
        public string VehicleNumber { get; set; } = default!;
        public string? ApplicantName { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;
        public string Workflow { get; set; } = default!;
        public int WorkflowStepOrder { get; set; }
        public string? Status { get; set; } = default!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? AssignedTo { get; set; } = default!;
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? RedFlag { get; set; }
        public string? Remarks { get; set; }
        public string? Location { get; set; } = default!;
        public string? AssignedDistricts { get; set; } = default!;
        public string? AssignedStates { get; set; } = default!;
        public string? State { get; set; } = default!;
        public string? District { get; set; } = default!;
        public string? Name { get; set; } = default!;
        public string? ValuationType { get; set; } = default!;

        // Company the case belongs to, carried straight from WorkflowEntity.Brand.
        // The camera app has no brand of its own: it looks a vehicle number up in the
        // unscoped open-case list and takes the brand from whichever case comes back,
        // so this is how a capture ends up filed under the right company.
        // Null on rows written before multi-brand, which is Vehga by definition.
        public string? Brand { get; set; }
    }
}
