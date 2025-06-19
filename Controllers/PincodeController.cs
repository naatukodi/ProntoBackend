// Controllers/PincodeController.cs
using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PincodesController : ControllerBase
    {
        private readonly IPincodeTableService _svc;

        public PincodesController(IPincodeTableService svc)
            => _svc = svc;

        /// <summary>
        /// GET api/pincodes/{pincode}
        /// </summary>
        [HttpGet("{pincode}")]
        public async Task<ActionResult<IReadOnlyList<PincodeModel>>> Get(string pincode)
        {
            var list = await _svc.GetByPincodeAsync(pincode);
            if (list == null || list.Count == 0)
                return NotFound();

            return Ok(list);
        }
    }
}
