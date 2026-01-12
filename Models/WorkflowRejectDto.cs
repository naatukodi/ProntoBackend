namespace Valuation.Api.Models
{
    public class WorkflowRejectDto
    {
        public string ValuationId { get; set; } = default!;
        public string VehicleNumber { get; set; } = default!;     
        public string ApplicantContact { get; set; } = default!;  
        public string CurrentStep { get; set; } = default!;       
        public string RejectReason { get; set; } = default!;
        public string CurrentUserId { get; set; } = default!;     
        public string CurrentUserName { get; set; } = default!;   
        public string? TargetRejectedStep { get; set; } // "AVO" or "Backend"
        public string? OverrideAssigneeId { get; set; } 
    }
}