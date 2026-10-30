// Models/DeviceTokenEntity.cs
using Azure;
using Azure.Data.Tables;

namespace Valuation.Api.Models
{
    /// <summary>
    /// One staff-app install that can receive push notifications for a user
    /// (DeviceTokens table). A user signed in on two phones has two rows.
    /// </summary>
    public class DeviceTokenEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!; // UserId (the +91 phone number)
        public string RowKey { get; set; } = default!;       // SHA-256 of Token: tokens are long and may hold characters keys can't

        public string Token { get; set; } = default!;        // Firebase Cloud Messaging registration token
        public string? Platform { get; set; }                // "android"

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; } = ETag.All;
    }

    public class DeviceTokenRequest
    {
        public string Token { get; set; } = default!;
        public string? Platform { get; set; }
    }
}
