using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

// Photos and video that arrived from a phone, newest first, for the portal's
// "photos uploaded" messages. Scoped to the company named in X-Brand.
[ApiController]
[Route("api/media-uploads")]
public class MediaUploadsController : ControllerBase
{
    private readonly IMediaUploadLog _log;
    public MediaUploadsController(IMediaUploadLog log) => _log = log;

    [HttpGet]
    public async Task<ActionResult<List<MediaUploadDto>>> GetRecent([FromQuery] int days = 7)
        => Ok(await _log.GetRecentAsync(Math.Clamp(days, 1, 30)));
}
