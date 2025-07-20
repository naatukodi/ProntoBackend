// Models/UserEntity.cs
using Azure;
using Azure.Data.Tables;

namespace Valuation.Api.Models
{
    public class UserEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = "Users";
        public string RowKey { get; set; } = default!; // e.g. UserId

        // Existing user fields
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Whatsapp { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;

        // New branch/post‑office fields
        public string? Description { get; set; }
        public string BranchType { get; set; } = default!;
        public string ServiceStatus { get; set; } = "Servicable";
        public string Circle { get; set; } = default!;
        public string District { get; set; } = default!;
        public string Division { get; set; } = default!;
        public string Region { get; set; } = default!;
        public string Block { get; set; } = default!;
        public string State { get; set; } = default!;
        public string Country { get; set; } = default!;
        public string Pincode { get; set; } = default!;

        // Table‑storage housekeeping
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }
    }
}
