namespace Valuation.Api.Models
{
    /// <summary>
    /// One row of a case search.
    ///
    /// Deliberately not the full <see cref="ValuationDocument"/>: a search may return
    /// every case a company has, and the dashboard only needs enough to list a row and
    /// open it. It also carries the identifiers that were searched on, so a reviewer can
    /// see WHICH of them matched rather than guessing why a row came back.
    /// </summary>
    public class CaseSearchResultDto
    {
        public string ValuationId { get; set; } = "";

        /// <summary>The case reference, e.g. VG-519499-K. Null on cases that predate it.</summary>
        public string? ReferenceNumber { get; set; }

        public string? VehicleNumber { get; set; }
        public string? ApplicantName { get; set; }
        public string? ApplicantContact { get; set; }
        public string? ChassisNumber { get; set; }
        public string? EngineNumber { get; set; }

        /// <summary>Stakeholder company, matching the dashboard's own column.</summary>
        public string? Name { get; set; }

        public string? Status { get; set; }
        public string? Workflow { get; set; }
        public int WorkflowStepOrder { get; set; }
        public string? ValuationType { get; set; }
        public string? Brand { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
