using System;

namespace Valuation.Api.Models
{
    public class WorkflowModel
    {
        public string ValuationId { get; set; } = default!;
        public string VehicleNumber { get; set; } = default!;
        public string ApplicantName { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;
        public string Workflow { get; set; } = default!;
        public int WorkflowStepOrder { get; set; }
        public string Status { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string AssignedTo { get; set; } = default!;
        public string Location { get; set; } = default!;
    }
}
