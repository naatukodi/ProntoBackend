using Azure;
using Azure.Data.Tables;
using System;

public class RoleEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Roles";
    public string RowKey { get; set; }     // the RoleId
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    // payload
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
}

public class UserRoleEntity : ITableEntity
{
    public string PartitionKey { get; set; }   // the UserId
    public string RowKey { get; set; }   // the RoleId
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}
