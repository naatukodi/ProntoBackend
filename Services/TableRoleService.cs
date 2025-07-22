using Azure.Data.Tables;
using Azure; // Add this for RequestFailedException
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Valuation.Api.Models;

public class TableRoleService : IRoleService
{
    private readonly TableClient _rolesTable;
    private readonly TableClient _userRolesTable;
    private readonly TableClient _usersTable;

    public TableRoleService(IConfiguration config)
    {
        var conn = config.GetConnectionString("TableStorage")!;
        var client = new TableServiceClient(conn);

        _rolesTable = client.GetTableClient("Roles");
        _userRolesTable = client.GetTableClient("UserRoles");
        _usersTable = client.GetTableClient("Users");

        _rolesTable.CreateIfNotExists();
        _userRolesTable.CreateIfNotExists();
        _usersTable.CreateIfNotExists();
    }

    public async Task CreateUserAsync(UserModel user)
    {
        var userEntity = new UserEntity
        {
            RowKey = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Whatsapp = user.Whatsapp,
            PhoneNumber = user.PhoneNumber,
            Description = user.Description,
            BranchType = user.BranchType,
            ServiceStatus = user.ServiceStatus,
            Circle = user.Circle,
            District = user.District,
            Division = user.Division,
            Region = user.Region,
            Block = user.Block,
            State = user.State,
            Country = user.Country,
            Pincode = user.Pincode
        };

        await _usersTable.UpsertEntityAsync(userEntity);

        if (!string.IsNullOrWhiteSpace(user.RoleId))
        {
            await AssignRoleToUserAsync(user.UserId, user.RoleId);
        }
    }

    public async Task<IEnumerable<UserModel>> GetAllUsersAsync()
    {
        var users = new List<UserModel>();
        await foreach (var entity in _usersTable.QueryAsync<UserEntity>())
        {
            var roles = await GetUserRolesAsync(entity.RowKey);
            users.Add(new UserModel
            {
                UserId = entity.RowKey,
                Name = entity.Name,
                Email = entity.Email,
                Whatsapp = entity.Whatsapp,
                PhoneNumber = entity.PhoneNumber,
                Description = entity.Description,
                BranchType = entity.BranchType,
                ServiceStatus = entity.ServiceStatus,
                Circle = entity.Circle,
                District = entity.District,
                Division = entity.Division,
                Region = entity.Region,
                Block = entity.Block,
                State = entity.State,
                Country = entity.Country,
                Pincode = entity.Pincode,
                RoleId = roles.FirstOrDefault() ?? string.Empty // assuming single role
            });
        }
        return users;
    }

    public async Task<UserModel?> GetUserAsync(string userId)
    {
        try
        {
            var entity = await _usersTable.GetEntityAsync<UserEntity>("Users", userId);
            var roles = await GetUserRolesAsync(userId);
            return new UserModel
            {
                UserId = userId,
                Name = entity.Value.Name,
                Email = entity.Value.Email,
                RoleId = roles.FirstOrDefault() ?? string.Empty, // assuming single role
                Whatsapp = entity.Value.Whatsapp,
                PhoneNumber = entity.Value.PhoneNumber,
                Description = entity.Value.Description,
                BranchType = entity.Value.BranchType,
                ServiceStatus = entity.Value.ServiceStatus,
                Circle = entity.Value.Circle,
                District = entity.Value.District,
                Division = entity.Value.Division,
                Region = entity.Value.Region,
                Block = entity.Value.Block,
                State = entity.Value.State,
                Country = entity.Value.Country,
                Pincode = entity.Value.Pincode
            };
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<IEnumerable<RoleModel>> GetAllRolesAsync()
    {
        var roles = new List<RoleModel>();
        await foreach (var ent in _rolesTable.QueryAsync<RoleEntity>())
        {
            roles.Add(new RoleModel
            {
                RoleId = ent.RowKey,
                Name = ent.Name,
                Description = ent.Description
            });
        }
        return roles;
    }

    public Task CreateOrUpdateRoleAsync(RoleModel role)
    {
        var entity = new RoleEntity
        {
            PartitionKey = "Roles",
            RowKey = role.RoleId,
            Name = role.Name,
            Description = role.Description
        };
        return _rolesTable.UpsertEntityAsync(entity);
    }

    public Task DeleteRoleAsync(string roleId)
        => _rolesTable.DeleteEntityAsync("Roles", roleId);

    public async Task<IEnumerable<string>> GetUserRolesAsync(string userId)
    {
        var roles = new List<string>();
        await foreach (var ent in _userRolesTable.QueryAsync<UserRoleEntity>(
            filter: $"PartitionKey eq '{userId}'"))
        {
            roles.Add(ent.RowKey);
        }
        return roles;
    }

    public Task AssignRoleToUserAsync(string userId, string roleId)
    {
        var ent = new UserRoleEntity
        {
            PartitionKey = userId,
            RowKey = roleId
        };
        return _userRolesTable.UpsertEntityAsync(ent);
    }

    public Task RemoveRoleFromUserAsync(string userId, string roleId)
        => _userRolesTable.DeleteEntityAsync(userId, roleId);
}
