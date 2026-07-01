using Azure;
using Azure.Data.Tables;

namespace Valuation.Api.Models
{
    public class WorkflowEntity : ITableEntity
    {
        // ITableEntity members
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // These are the properties we want to persist:
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
        public string? Location { get; set; } = default!;
        public string? AssignedDistricts { get; set; } = default!;
        public string? AssignedStates { get; set; } = default!;
        public string? State { get; set; } = default!;
        public string? District { get; set; } = default!;

        public string? AssignedToRole { get; set; } = default!;
        public string? CompletedBy { get; set; } = default!;
        public string? CreatedBy { get; set; } = default!;
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? RedFlag { get; set; }
        public string? Remarks { get; set; }
        public string? Name { get; set; } = default!;
        public string? ValuationType { get; set; } = default!;

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

        // ================= PAYMENT =================
        public string? PaymentStatus { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal? PaymentAmount { get; set; }
        public string? PaymentNotes { get; set; }
        public string? PaymentSavedBy { get; set; }
        public DateTime? PaymentSavedAt { get; set; }


    }
}