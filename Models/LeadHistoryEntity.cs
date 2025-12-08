using Azure;
using Azure.Data.Tables;

namespace Valuation.Api.Models;

public class LeadHistoryEntity : ITableEntity
{
    // Azure Table Storage keys
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Core fields
    public DateTime DateTime { get; set; }                    // ← NEW
    public string? Action { get; set; }
    public string? Remarks { get; set; }
    
    // User tracking
    public string? PerformedByUserId { get; set; }           // ← NEW
    public string? PerformedByUserName { get; set; }         // ← NEW
    
    // Status transition
    public string? StatusFrom { get; set; }                  // ← NEW
    public string? StatusTo { get; set; }                    // ← NEW
    
    // TAT tracking
    public int CurrentTat { get; set; }
    public int TotalTat { get; set; }
    
    // Additional tracking (existing)
    public DateTime? FirstDateTime { get; set; }
    public bool FirstUpdate { get; set; }
    public bool StatusChange { get; set; }
    public DateTime StatusChangedDateTime { get; set; }
    public string? PreviousStatus { get; set; }
    public string? CurrentStatus { get; set; }
}
