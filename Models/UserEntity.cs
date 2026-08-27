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

        public string? assignedStates { get; set; } // Comma-separated list of state keys
        public string? assignedDistricts { get; set; } // Comma-separated list of district keys

        // Which companies this user may work for, comma-separated ("vehga", "pronto"),
        // following the same convention as assignedStates. Empty/null means Vehga only,
        // which is every user that predates multi-brand. The login brand picker offers
        // exactly these, and a user with one entry never sees the picker.
        public string? AllowedBrands { get; set; }

        // Credential for camera-app agency login
        public string? Password { get; set; }

        // Additional metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Table‑storage housekeeping
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }
    }
}
