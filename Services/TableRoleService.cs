using Azure.Data.Tables;
using Azure; // Add this for RequestFailedException
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Valuation.Api.Models;

namespace Valuation.Api.Services;

public class TableRoleService : IRoleService
{
    private readonly TableClient _rolesTable;
    private readonly TableClient _userRolesTable;
    private readonly TableClient _usersTable;
    private readonly IStateService _stateService;

    public TableRoleService(IConfiguration config, IStateService stateService)
    {
        var conn = config.GetConnectionString("TableStorage")!;
        _stateService = stateService;
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

    public async Task<List<string>> GetUserStatesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID must be provided", nameof(userId));

        try
        {
            var response = await _usersTable.GetEntityAsync<UserEntity>("Users", userId);
            var userEntity = response.Value;

            if (!string.IsNullOrWhiteSpace(userEntity.assignedStates))
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(userEntity.assignedStates) ?? new List<string>();
            }
            return new List<string>();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // User not found
            return new List<string>();
        }
    }

    public async Task<List<string>> GetUserDistrictsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID must be provided", nameof(userId));

        try
        {
            var response = await _usersTable.GetEntityAsync<UserEntity>("Users", userId);
            var userEntity = response.Value;

            if (!string.IsNullOrWhiteSpace(userEntity.assignedDistricts))
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(userEntity.assignedDistricts) ?? new List<string>();
            }
            return new List<string>();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // User not found
            return new List<string>();
        }
    }

    public async Task AppendStateToUserAsync(string userId, string state)
    {
        try
        {
            var response = await _usersTable.GetEntityAsync<UserEntity>("Users", userId);
            var userEntity = response.Value;

            // Parse existing states if any
            var existingStates = new List<string>();
            if (!string.IsNullOrWhiteSpace(userEntity.assignedStates))
            {
                existingStates = System.Text.Json.JsonSerializer.Deserialize<List<string>>(userEntity.assignedStates) ?? new List<string>();
            }

            // Add new state, avoiding duplicates
            if (!existingStates.Contains(state))
            {
                existingStates.Add(state);
            }

            // Serialize back to JSON
            userEntity.assignedStates = System.Text.Json.JsonSerializer.Serialize(existingStates);

            await _usersTable.UpdateEntityAsync(userEntity, userEntity.ETag, Azure.Data.Tables.TableUpdateMode.Replace);
        }
        catch (RequestFailedException)
        {
            // Handle not found or other errors as needed
            throw;
        }
    }

    public async Task DeleteStateFromUserAsync(string userId, string state)
    {
        try
        {
            var response = await _usersTable.GetEntityAsync<UserEntity>("Users", userId);
            var userEntity = response.Value;

            // Parse existing states if any
            var existingStates = new List<string>();
            if (!string.IsNullOrWhiteSpace(userEntity.assignedStates))
            {
                existingStates = System.Text.Json.JsonSerializer.Deserialize<List<string>>(userEntity.assignedStates) ?? new List<string>();
            }

            // Remove state if it exists
            existingStates.Remove(state);

            // Remove districts belonging to the removed state from assignedDistricts
            var districtsToRemove = await _stateService.GetDistrictsByStateAsync(state);
            foreach (var district in districtsToRemove)
            {
                await DeleteDistrictFromUserAsync(userId, district);
            }

            // Serialize back to JSON
            userEntity.assignedStates = System.Text.Json.JsonSerializer.Serialize(existingStates);

            await _usersTable.UpdateEntityAsync(userEntity, userEntity.ETag, Azure.Data.Tables.TableUpdateMode.Replace);
        }
        catch (RequestFailedException)
        {
            // Handle not found or other errors as needed
            throw;
        }
    }

    public async Task DeleteDistrictFromUserAsync(string userId, string district)
    {
        try
        {
            var response = await _usersTable.GetEntityAsync<UserEntity>("Users", userId);
            var userEntity = response.Value;

            // Parse existing districts if any
            var existingDistricts = new List<string>();
            if (!string.IsNullOrWhiteSpace(userEntity.assignedDistricts))
            {
                existingDistricts = System.Text.Json.JsonSerializer.Deserialize<List<string>>(userEntity.assignedDistricts) ?? new List<string>();
            }

            // Remove district if it exists
            existingDistricts.Remove(district);

            // Serialize back to JSON
            userEntity.assignedDistricts = System.Text.Json.JsonSerializer.Serialize(existingDistricts);

            await _usersTable.UpdateEntityAsync(userEntity, userEntity.ETag, Azure.Data.Tables.TableUpdateMode.Replace);
        }
        catch (RequestFailedException)
        {
            // Handle not found or other errors as needed
            throw;
        }
    }

    public async Task AppendDistrictToUserAsync(string userId, string district)
    {
        try
        {
            var response = await _usersTable.GetEntityAsync<UserEntity>("Users", userId);
            var userEntity = response.Value;

            // Parse existing districts if any
            var existingDistricts = new List<string>();
            if (!string.IsNullOrWhiteSpace(userEntity.assignedDistricts))
            {
                existingDistricts = System.Text.Json.JsonSerializer.Deserialize<List<string>>(userEntity.assignedDistricts) ?? new List<string>();
            }

            // Add new district, avoiding duplicates
            if (!existingDistricts.Contains(district))
            {
                existingDistricts.Add(district);
            }

            // Serialize back to JSON
            userEntity.assignedDistricts = System.Text.Json.JsonSerializer.Serialize(existingDistricts);

            await _usersTable.UpdateEntityAsync(userEntity, userEntity.ETag, Azure.Data.Tables.TableUpdateMode.Replace);
        }
        catch (RequestFailedException)
        {
            // Handle not found or other errors as needed
            throw;
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
                AssignedStates = entity.assignedStates ?? "[]",
                AssignedDistricts = entity.assignedDistricts ?? "[]",
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
                Pincode = entity.Value.Pincode,
                AssignedDistricts = entity.Value.assignedDistricts ?? "[]",
                AssignedStates = entity.Value.assignedStates ?? "[]"
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

    public async Task<IEnumerable<UserModel>> GetUsersByRoleAsync(string roleId)
    {
        var users = new List<UserModel>();
        await foreach (var userRole in _userRolesTable.QueryAsync<UserRoleEntity>(
            filter: $"RowKey eq '{roleId}'"))
        {
            var user = await GetUserAsync(userRole.PartitionKey);
            if (user != null)
            {
                users.Add(user);
            }
        }
        return users;
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
