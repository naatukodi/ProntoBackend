// Services/Entities/PincodeEntity.cs
using Azure;
using Azure.Data.Tables;

namespace Valuation.Api.Services.Entities
{
    public class PincodeEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;    // will be the pincode
        public string RowKey { get; set; } = default!;          // unique per office, e.g. Name
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // cached fields
        public string Name { get; set; } = default!;
        public string Block { get; set; } = default!;
        public string State { get; set; } = default!;
        public string Country { get; set; } = default!;
    }
}
