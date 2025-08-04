using Azure;
using Azure.Data.Tables;
using Newtonsoft.Json;
namespace Valuation.Api.Models;

public class StateEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string StateName { get; set; }
    public int DistrictCount { get; set; }

    // The JSON‐serialized array of district names
    public string Districts { get; set; }

    // Helper – not stored in the table
    public List<string> DistrictList
        => string.IsNullOrWhiteSpace(Districts)
           ? new List<string>()
           : JsonConvert.DeserializeObject<List<string>>(Districts) ?? new List<string>();
}
