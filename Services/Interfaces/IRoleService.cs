using System.Collections.Generic;
using System.Threading.Tasks;
using Valuation.Api.Models;

public interface IRoleService
{
    // Role definitions
    Task<IEnumerable<RoleModel>> GetAllRolesAsync();
    Task CreateOrUpdateRoleAsync(RoleModel role);
    Task DeleteRoleAsync(string roleId);

    // User definitions
    Task<UserModel?> GetUserAsync(string userId);
    Task<IEnumerable<UserModel>> GetAllUsersAsync();


    // User–role assignments
    Task<IEnumerable<string>> GetUserRolesAsync(string userId);
    Task<IEnumerable<UserModel>> GetUsersByRoleAsync(string roleId);
    Task AssignRoleToUserAsync(string userId, string roleId);
    Task RemoveRoleFromUserAsync(string userId, string roleId);
}
