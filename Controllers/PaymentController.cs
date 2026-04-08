using Microsoft.AspNetCore.Mvc;
using Valuation.Api.Models;
using Valuation.Api.Services;

namespace Valuation.Api.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IWorkflowTableService _workflowService;

        public PaymentController(IWorkflowTableService workflowService)
        {
            _workflowService = workflowService;
        }

        [HttpPut]
        public async Task<IActionResult> SavePayment([FromBody] PaymentDto dto)
        {
            await _workflowService.SavePaymentAsync(dto);
            return Ok(new { message = "Payment saved successfully." });
        }

        [HttpGet("{valuationId}")]
        public async Task<IActionResult> GetPayment(string valuationId)
        {
            var payment = await _workflowService.GetPaymentAsync(valuationId);

            if (payment == null)
                return NotFound();

            return Ok(payment);
        }
    }
}
