using System.Collections.Generic;
using System.Threading.Tasks;
using Valuation.Api.Models;

public interface IRoleService
{
    // Role definitions
    Task<IEnumerable<RoleModel>> GetAllRolesAsync();
    Task CreateOrUpdateRoleAsync(RoleModel role);
    Task DeleteRoleAsync(string roleId);

    // User–role assignments
    Task<IEnumerable<string>> GetUserRolesAsync(string userId);
    Task AssignRoleToUserAsync(string userId, string roleId);
    Task RemoveRoleFromUserAsync(string userId, string roleId);
}
