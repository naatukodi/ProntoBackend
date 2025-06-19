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
        public string ApplicantName { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;
        public string Workflow { get; set; } = default!;
        public int WorkflowStepOrder { get; set; }
        public string Status { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string AssignedTo { get; set; } = default!;
        public string Location { get; set; } = default!;

        public string? AssignedToRole { get; set; } = default!;
        public string? CompletedBy { get; set; } = default!;
        public string? CreatedBy { get; set; } = default!;
        public string? AssignedToPhoneNumber { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? AssignedToWhatsapp { get; set; }
        public string? RedFlag { get; set; }
        public string? Remarks { get; set; }
        public string Name { get; set; } = default!;
        public string? ValuationType { get; set; } = default!;
    }
}