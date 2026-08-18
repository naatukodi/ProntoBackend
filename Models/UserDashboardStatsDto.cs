namespace Valuation.Api.Models
{
    public class UserDashboardStatsDto
    {
        public int OpenCount { get; set; }
        public int AgedCount { get; set; }
        public int CompletedCount { get; set; }
        public double AvgTatHours { get; set; }
        public List<WorkflowModel> OpenCases { get; set; } = new();
        public List<WorkflowModel> CompletedCases { get; set; } = new();
    }
}
