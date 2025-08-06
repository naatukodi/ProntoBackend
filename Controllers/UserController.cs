using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

// Controllers/UserController.cs
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly TableRoleService _roleService;

    public UserController(TableRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] UserModel model)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(model.UserId) ||
            string.IsNullOrEmpty(model.Name) ||
            string.IsNullOrEmpty(model.RoleId))
        {
            return BadRequest("UserId, Name, and RoleId are required.");
        }

        await _roleService.CreateUserAsync(model);
        return NoContent();
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _roleService.GetAllUsersAsync();
        if (users == null || !users.Any())
            return NotFound("No users found.");

        return Ok(users);
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        var user = await _roleService.GetUserAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpGet("roles/{roleId}")]
    public async Task<IActionResult> GetUsersByRole(string roleId)
    {
        var users = await _roleService.GetUsersByRoleAsync(roleId);
        if (users == null || !users.Any())
            return NotFound("No users found for this role.");

        return Ok(users);
    }

    [HttpGet("{userId}/States")]
    public async Task<IActionResult> GetUserStates(string userId)
    {
        var states = await _roleService.GetUserStatesAsync(userId);
        if (states == null || !states.Any())
            return NotFound("No states found for this user.");

        return Ok(states);
    }
    [HttpGet("{userId}/Districts")]
    public async Task<IActionResult> GetUserDistricts(string userId)
    {
        var districts = await _roleService.GetUserDistrictsAsync(userId);
        if (districts == null || !districts.Any())
            return NotFound("No districts found for this user.");

        return Ok(districts);
    }

    [HttpPost("{userId}/States")]
    public async Task<IActionResult> AppendStateToUser(string userId, [FromBody] string state)
    {
        await _roleService.AppendStateToUserAsync(userId, state);
        return NoContent();
    }

    [HttpPost("{userId}/Districts")]
    public async Task<IActionResult> AppendDistrictToUser(string userId, [FromBody] string district)
    {
        await _roleService.AppendDistrictToUserAsync(userId, district);
        return NoContent();
    }

    [HttpDelete("{userId}/States/{stateKey}")]
    public async Task<IActionResult> DeleteStateFromUser(string userId, string stateKey)
    {
        await _roleService.DeleteStateFromUserAsync(userId, stateKey);
        return NoContent();
    }

    [HttpDelete("{userId}/Districts/{districtKey}")]
    public async Task<IActionResult> DeleteDistrictFromUser(string userId, string districtKey)
    {
        await _roleService.DeleteDistrictFromUserAsync(userId, districtKey);
        return NoContent();
    }

}
