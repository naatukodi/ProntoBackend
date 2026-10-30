using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

// The staff app's push-notification tokens, one per phone a user is signed in on.
// See PushNotificationService for what is sent to them.
[ApiController]
[Route("api/users/{userId}/devices")]
public class UserDevicesController : ControllerBase
{
    private readonly IPushNotificationService _push;
    public UserDevicesController(IPushNotificationService push) => _push = push;

    // Called after every login and whenever Firebase hands the app a new token.
    [HttpPut]
    public async Task<IActionResult> Register(string userId, [FromBody] DeviceTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Token) || request.Token.Length > 4096)
            return BadRequest("A device token is required.");

        await _push.RegisterDeviceAsync(userId, request.Token.Trim(), request.Platform);
        return NoContent();
    }

    // Called on logout, so a phone handed to someone else stops getting this user's notices.
    [HttpDelete]
    public async Task<IActionResult> Unregister(string userId, [FromBody] DeviceTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Token))
            return BadRequest("A device token is required.");

        await _push.UnregisterDeviceAsync(userId, request.Token.Trim());
        return NoContent();
    }
}
