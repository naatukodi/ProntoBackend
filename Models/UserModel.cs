using Azure;
using Azure.Data.Tables;

namespace Valuation.Api.Models;

// Models/UserModel.cs
public class UserModel
{
    public string UserId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string RoleId { get; set; } = default!;
    public string whatsapp { get; set; } = default!;
    public string phoneNumber { get; set; } = default!;
}

// Models/UserEntity.cs
public class UserEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Users";
    public string RowKey { get; set; } = default!;
    public string Name { get; set; }
    public string Email { get; set; }
    public string whatsapp { get; set; } = default!;
    public string phoneNumber { get; set; } = default!;
    public ETag ETag { get; set; } = ETag.All;
    public DateTimeOffset? Timestamp { get; set; }
}
