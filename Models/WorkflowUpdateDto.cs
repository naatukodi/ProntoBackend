using System.ComponentModel.DataAnnotations;

namespace Valuation.Api.Models
{
    public class WorkflowUpdateDto
    {
        // We set this from the route; the client does not fill it.
        public string ValuationId { get; set; } = default!;

        [Required]
        public string VehicleNumber { get; set; } = default!;

        public string? ApplicantName { get; set; } = default!;

        [Required]
        public string ApplicantContact { get; set; } = default!;

        [Required]
        public string Workflow { get; set; } = default!;

        [Required]
        public int WorkflowStepOrder { get; set; }

        public string? Status { get; set; } = default!;

        // CreatedAt is handled server-side; do not supply.
        // If  null, leave as-is (for updates). If new record, service stamps UtcNow.
        public DateTime? CreatedAt { get; set; }

        // Client can set CompletedAt when they finish a step.
        public DateTime? CompletedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string? AssignedTo { get; set; } = default!;

        public string? Location { get; set; } = default!;

        public string? State { get; set; } = default!;
        public string? District { get; set; } = default!;
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }

        public string? StakeholderAssignedTo { get; set; } = default!;
        public string? StakeholderAssignedToPhoneNumber { get; set; }
        public string? StakeholderAssignedToEmail { get; set; }
        public string? StakeholderAssignedToWhatsapp { get; set; }
        public string? BackEndAssignedTo { get; set; } = default!;
        public string? BackEndAssignedToPhoneNumber { get; set; }
        public string? BackEndAssignedToEmail { get; set; }
        public string? BackEndAssignedToWhatsapp { get; set; }

        public string? AVOAssignedTo { get; set; } = default!;
        public string? AVOAssignedToPhoneNumber { get; set; }
        public string? AVOAssignedToEmail { get; set; }
        public string? AVOAssignedToWhatsapp { get; set; }

        public string? QualityControlAssignedTo { get; set; } = default!;
        public string? QualityControlAssignedToPhoneNumber { get; set; }
        public string? QualityControlAssignedToEmail { get; set; }
        public string? QualityControlAssignedToWhatsapp { get; set; }

        public string? FinalReportAssignedTo { get; set; } = default!;
        public string? FinalReportAssignedToPhoneNumber { get; set; }
        public string? FinalReportAssignedToEmail { get; set; }
        public string? FinalReportAssignedToWhatsapp { get; set; }

        public string? RedFlag { get; set; }
        public string? Remarks { get; set; }
        public string? Name { get; set; } = default!;
        public string? ValuationType { get; set; } = default!;
    }
}
