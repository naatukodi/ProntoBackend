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
    Task<UserModel?> ValidateAgencyLoginAsync(string userId, string password);


    // User–role assignments
    Task<IEnumerable<string>> GetUserRolesAsync(string userId);
    Task<IEnumerable<UserModel>> GetUsersByRoleAsync(string roleId);
    Task AssignRoleToUserAsync(string userId, string roleId);
    Task RemoveRoleFromUserAsync(string userId, string roleId);

    // Assign State, Districts to User

    Task<List<string>> GetUserStatesAsync(string userId);
    Task<List<string>> GetUserDistrictsAsync(string userId);
    Task AppendStateToUserAsync(string userId, string stateKey);
    Task AppendDistrictToUserAsync(string userId, string districtKey);

    Task DeleteStateFromUserAsync(string userId, string stateKey);
    Task DeleteDistrictFromUserAsync(string userId, string districtKey);
}
