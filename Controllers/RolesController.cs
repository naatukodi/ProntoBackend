using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;

[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _svc;
    public RolesController(IRoleService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleModel>>> GetAll()
        => Ok(await _svc.GetAllRolesAsync());

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] RoleModel role)
    {
        await _svc.CreateOrUpdateRoleAsync(role);
        return NoContent();
    }

    [HttpDelete("{roleId}")]
    public async Task<IActionResult> Delete(string roleId)
    {
        await _svc.DeleteRoleAsync(roleId);
        return NoContent();
    }
}
