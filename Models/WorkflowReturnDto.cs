namespace Valuation.Api.Models
{
    public class WorkflowReturnDto // Class renamed
    {
        public string ValuationId { get; set; } = default!;
        public string VehicleNumber { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;
        public string CurrentStep { get; set; } = default!;
        
        // Renamed from RejectReason to ReturnReason
        public string ReturnReason { get; set; } = default!; 
        
        public string CurrentUserId { get; set; } = default!;
        public string CurrentUserName { get; set; } = default!;
        
        // Renamed from TargetRejectedStep to TargetReturnStep
        public string? TargetReturnStep { get; set; } // "AVO" or "Backend"
        public string? OverrideAssigneeId { get; set; }
    }
}