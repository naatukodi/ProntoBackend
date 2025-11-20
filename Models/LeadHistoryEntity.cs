using Azure;
using Azure.Data.Tables;
using System;

namespace Valuation.Api.Models;

public class LeadHistoryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;  // ValuationId
    public string RowKey { get; set; } = default!;        // Unique ID for each history event

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Lead history fields
    public DateTime DateTime { get; set; }
    public bool FirstUpdate { get; set; }
    public DateTime? FirstDateTime { get; set; }
    public string? Action { get; set; }
    public bool StatusChange { get; set; }
    public DateTime StatusChangedDateTime { get; set; }
    public string? Remarks { get; set; }
    public string? PreviousStatus { get; set; }
    public string? CurrentStatus { get; set; }
    public int CurrentTat { get; set; }
    public int TotalTat { get; set; }
}
