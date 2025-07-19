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
        if (string.IsNullOrEmpty(model.UserId) ||
            string.IsNullOrEmpty(model.Name) ||
            string.IsNullOrEmpty(model.RoleId))
        {
            return BadRequest("UserId, Name, and RoleId are required");
        }

        await _roleService.CreateUserAsync(model);
        return Ok("User created and role assigned");
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
