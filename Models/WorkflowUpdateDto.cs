using System.ComponentModel.DataAnnotations;

namespace Valuation.Api.Models
{
    public class WorkflowUpdateDto
    {
        // We set this from the route; the client does not fill it.
        public string ValuationId { get; set; } = default!;

        [Required]
        public string VehicleNumber { get; set; } = default!;

        [Required]
        public string ApplicantName { get; set; } = default!;

        [Required]
        public string ApplicantContact { get; set; } = default!;

        [Required]
        public string Workflow { get; set; } = default!;

        [Required]
        public int WorkflowStepOrder { get; set; }

        [Required]
        public string Status { get; set; } = default!;

        // CreatedAt is handled server-side; do not supply.
        // If  null, leave as-is (for updates). If new record, service stamps UtcNow.
        public DateTime? CreatedAt { get; set; }

        // Client can set CompletedAt when they finish a step.
        public DateTime? CompletedAt { get; set; }

        [Required]
        public string AssignedTo { get; set; } = default!;

        [Required]
        public string Location { get; set; } = default!;
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? RedFlag { get; set; }
        public string? Remarks { get; set; }
    }
}
