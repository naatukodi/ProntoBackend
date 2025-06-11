using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;

[ApiController]
[Route("api/users/{userId}/roles")]
public class UserRolesController : ControllerBase
{
    private readonly IRoleService _svc;
    public UserRolesController(IRoleService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> Get(string userId)
        => Ok(await _svc.GetUserRolesAsync(userId));

    [HttpPost("{roleId}")]
    public async Task<IActionResult> Assign(string userId, string roleId)
    {
        await _svc.AssignRoleToUserAsync(userId, roleId);
        return NoContent();
    }

    [HttpDelete("{roleId}")]
    public async Task<IActionResult> Remove(string userId, string roleId)
    {
        await _svc.RemoveRoleFromUserAsync(userId, roleId);
        return NoContent();
    }
}
