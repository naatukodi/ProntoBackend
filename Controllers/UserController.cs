using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;

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
}
