using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;
using Valuation.Api.Services.Interfaces;

namespace Valuation.Api.Services
{
    /// <inheritdoc cref="ICaseSearchService"/>
    public class CaseSearchService : ICaseSearchService
    {
        private readonly CosmosClient _cosmos;
        private readonly IBrandContext _brand;
        private readonly string _dbId;
        private readonly string _containerId;

        public CaseSearchService(CosmosClient cosmos, IConfiguration configuration, IBrandContext brand)
        {
            _cosmos = cosmos;
            _brand = brand;
            _dbId = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
            _containerId = configuration["Cosmos:ContainerId"] ?? "Valuations";
        }

        private Container Container => _cosmos.GetDatabase(_dbId).GetContainer(_containerId);

        /// <summary>
        /// Below this, a search matches most of the database and is not a search.
        /// Three characters is enough to be a meaningful fragment of a registration.
        /// </summary>
        public const int MinimumQueryLength = 3;

        /// <summary>Never return more than this from one search, however loose the term.</summary>
        private const int MaxResults = 200;

        public async Task<IReadOnlyList<CaseSearchResultDto>> SearchAsync(
            string term, CancellationToken ct = default)
        {
            var q = (term ?? "").Trim().ToUpperInvariant();
            if (q.Length < MinimumQueryLength) return Array.Empty<CaseSearchResultDto>();

            // CONTAINS rather than equality: people search a fragment of a registration
            // or the tail of a chassis number far more often than the whole thing. It is
            // an unindexed scan, which is why the minimum length above exists.
            //
            // Searches every case, open or closed and however old — that is the point of
            // it. The dashboard's own 30-day cutoff on completed cases is a display rule
            // and is deliberately not applied here.
            var brandClause = _brand.IsUnscoped ? "" : $"AND {BrandContext.SqlFilter}";

            var query = new QueryDefinition($@"
                SELECT TOP {MaxResults}
                    c.id AS ValuationId,
                    c.ReferenceNumber,
                    c.VehicleNumber,
                    c.ApplicantContact,
                    c.Status,
                    c.Workflow,
                    c.Brand,
                    c.CreatedAt,
                    c.CompletedAt,
                    c.VehicleDetails.ChassisNumber,
                    c.VehicleDetails.EngineNumber,
                    IIF(IS_DEFINED(c.Stakeholder.Name), c.Stakeholder.Name, null) AS Name,
                    IIF(IS_DEFINED(c.Stakeholder.Applicant.Name), c.Stakeholder.Applicant.Name, null) AS ApplicantName,
                    IIF(IS_DEFINED(c.Stakeholder.ValuationType), c.Stakeholder.ValuationType, null) AS ValuationType
                FROM c
                WHERE (NOT IS_DEFINED(c.DeletedAt) OR IS_NULL(c.DeletedAt))
                {brandClause}
                AND (
                       CONTAINS(UPPER(c.ReferenceNumber ?? ''), @q)
                    OR CONTAINS(UPPER(c.VehicleNumber ?? ''), @q)
                    OR CONTAINS(UPPER(c.VehicleDetails.ChassisNumber ?? ''), @q)
                    OR CONTAINS(UPPER(c.VehicleDetails.EngineNumber ?? ''), @q)
                )
                ORDER BY c.CreatedAt DESC
            ").WithParameter("@q", q);

            if (!_brand.IsUnscoped) query = query.WithParameter(BrandContext.SqlParam, _brand.Current);

            var results = new List<CaseSearchResultDto>();
            using var iterator = Container.GetItemQueryIterator<SearchRow>(query);
            while (iterator.HasMoreResults)
            {
                foreach (var row in await iterator.ReadNextAsync(ct))
                {
                    // The step the case is actually on, derived the same way the workflow
                    // listing does it: the highest step that is not still pending.
                    var step = row.Workflow?
                        .Where(w => !string.Equals(w.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                        .Select(w => w.StepOrder)
                        .DefaultIfEmpty(1)
                        .Max() ?? 1;

                    results.Add(new CaseSearchResultDto
                    {
                        ValuationId = row.ValuationId ?? "",
                        ReferenceNumber = row.ReferenceNumber,
                        VehicleNumber = row.VehicleNumber,
                        ApplicantName = row.ApplicantName,
                        ApplicantContact = row.ApplicantContact,
                        ChassisNumber = row.ChassisNumber,
                        EngineNumber = row.EngineNumber,
                        Name = row.Name,
                        Status = row.Status,
                        Workflow = StepName(step),
                        WorkflowStepOrder = step,
                        ValuationType = row.ValuationType,
                        Brand = BrandContext.Of(row.Brand),
                        CreatedAt = row.CreatedAt,
                        CompletedAt = row.CompletedAt,
                    });
                }
            }

            return results;
        }

        /// <summary>Step order to the name the dashboard's stage pills use.</summary>
        private static string StepName(int step) => step switch
        {
            1 => "Stakeholder",
            2 => "BackEnd",
            3 => "AVO",
            4 => "QC",
            5 => "FinalReport",
            _ => "Stakeholder",
        };

        /// <summary>Shape the projection above comes back as.</summary>
        private sealed class SearchRow
        {
            public string? ValuationId { get; set; }
            public string? ReferenceNumber { get; set; }
            public string? VehicleNumber { get; set; }
            public string? ApplicantName { get; set; }
            public string? ApplicantContact { get; set; }
            public string? ChassisNumber { get; set; }
            public string? EngineNumber { get; set; }
            public string? Name { get; set; }
            public string? Status { get; set; }
            public string? ValuationType { get; set; }
            public string? Brand { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public List<WorkflowStep>? Workflow { get; set; }
        }
    }
}
