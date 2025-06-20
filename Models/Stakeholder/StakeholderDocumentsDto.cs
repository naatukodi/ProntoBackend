using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Valuation.Api.Models
{
    /// <summary>
    /// DTO for the multipart/form-data upload of stakeholder documents.
    /// </summary>
    public class StakeholderDocumentsDto
    {
        [Required]
        public IFormFile RcFile { get; set; } = default!;

        [Required]
        public IFormFile InsuranceFile { get; set; } = default!;

        public IFormFileCollection? OtherFiles { get; set; }
    }
}