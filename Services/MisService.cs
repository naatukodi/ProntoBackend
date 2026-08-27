using System.Globalization;
using Azure.Data.Tables;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public class MisService : IMisService
    {
        private readonly Container _container;
        private readonly TableClient? _workflowsTable;
        private readonly TableClient? _completedWorkflowsTable;

        // Company this request belongs to; the MIS report is narrowed to it.
        private readonly IBrandContext _brand;

        public MisService(CosmosClient client, IConfiguration cfg, IBrandContext brand)
        {
            _brand = brand;
            _container = client
                .GetDatabase(cfg["Cosmos:DatabaseId"] ?? "ValuationsDb")
                .GetContainer(cfg["Cosmos:ContainerId"] ?? "Valuations");

            // Payments are saved by the payment popup into Table Storage, not Cosmos.
            // Completed cases are moved from "Workflows" to "CompletedWorkflows", so read both.
            var conn = cfg.GetConnectionString("TableStorage");
            if (!string.IsNullOrWhiteSpace(conn))
            {
                var svc = new TableServiceClient(conn);
                _workflowsTable = svc.GetTableClient("Workflows");
                _completedWorkflowsTable = svc.GetTableClient("CompletedWorkflows");
            }
        }

        /// <summary>
        /// One pass over both workflow tables → payment info keyed by valuationId (RowKey).
        /// Avoids a per-case lookup when building the report.
        /// </summary>
        private async Task<Dictionary<string, WorkflowEntity>> LoadPaymentsAsync()
        {
            var map = new Dictionary<string, WorkflowEntity>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in new[] { _workflowsTable, _completedWorkflowsTable })
            {
                if (table is null) continue;
                try
                {
                    await foreach (var e in table.QueryAsync<WorkflowEntity>())
                    {
                        if (string.IsNullOrWhiteSpace(e.RowKey)) continue;
                        // A case present in both tables keeps whichever row carries payment data.
                        if (map.TryGetValue(e.RowKey, out var existing)
                            && !string.IsNullOrWhiteSpace(existing.PaymentStatus)
                            && string.IsNullOrWhiteSpace(e.PaymentStatus))
                            continue;
                        map[e.RowKey] = e;
                    }
                }
                catch
                {
                    // A missing/unreachable table must not break the whole report.
                }
            }

            return map;
        }

        // ── Client name → short code, used to build the UNIQ ID ──────────────
        // TODO: populate from the client's official short-code list.
        private static readonly Dictionary<string, string> ClientCodes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["TVS CREDIT SERVICES"] = "TVS",
            };

        private static string ClientCode(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            if (ClientCodes.TryGetValue(name.Trim(), out var code)) return code;

            // Fallback: first word, letters/digits only, uppercased, max 4 chars.
            var first = name.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "";
            var clean = new string(first.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return clean.Length > 4 ? clean[..4] : clean;
        }

        public async Task<List<MisRowDto>> GetMisAsync(
            DateTime? from,
            DateTime? to,
            string? status,
            string? client,
            string? state)
        {
            var sql = "SELECT * FROM c WHERE 1=1";
            if (from.HasValue) sql += " AND c.CreatedAt >= @from";
            if (to.HasValue) sql += " AND c.CreatedAt <= @to";
            // One company's MIS must never include the other's cases.
            if (!_brand.IsUnscoped) sql += $" AND {BrandContext.SqlFilter}";

            var qd = new QueryDefinition(sql);
            if (from.HasValue) qd = qd.WithParameter("@from", from.Value);
            if (to.HasValue) qd = qd.WithParameter("@to", to.Value);
            if (!_brand.IsUnscoped) qd = qd.WithParameter(BrandContext.SqlParam, _brand.Current);

            var payments = await LoadPaymentsAsync();
            var rows = new List<MisRowDto>();

            using var iter = _container.GetItemQueryIterator<ValuationDocument>(qd);
            while (iter.HasMoreResults)
            {
                var page = await iter.ReadNextAsync();
                foreach (var doc in page)
                {
                    payments.TryGetValue(doc.id ?? "", out var pay);
                    rows.Add(Map(doc, pay));
                }
            }

            // Secondary filters (small result set after the date range).
            IEnumerable<MisRowDto> q = rows;
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(r => string.Equals(r.LeadStatus, status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(client))
                q = q.Where(r => r.ClientName.Contains(client.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(state))
                q = q.Where(r => r.ClientState.Contains(state.Trim(), StringComparison.OrdinalIgnoreCase));

            return q
                .OrderByDescending(r => r.LeadCreationDateTime)
                .ToList();
        }

        private static MisRowDto Map(ValuationDocument doc, WorkflowEntity? pay)
        {
            var sh = doc.Stakeholder;
            var vd = doc.VehicleDetails;
            var insp = doc.InspectionDetails;

            var segment = (doc.VehicleSegment ?? sh?.VehicleSegment ?? "")
                .Replace(" ", "").ToUpperInvariant();

            var step5 = doc.Workflow?.FirstOrDefault(w => w.StepOrder == 5);
            DateTime? approvedAt = step5?.CompletedAt ?? doc.CompletedAt;
            bool isApproved = step5?.Status == "Completed" || doc.CompletedAt.HasValue;

            var created = doc.CreatedAt;

            // UNIQ ID: {segment}{clientCode}{DDMMYY of creation}{HHMM of approved time}
            var uniq = segment
                + ClientCode(sh?.Name)
                + created.ToString("ddMMyy", CultureInfo.InvariantCulture)
                + (approvedAt?.ToString("HHmm", CultureInfo.InvariantCulture) ?? "");

            // TAT: creation → approval (or "now" while open).
            var end = approvedAt ?? DateTime.UtcNow;
            var tat = FormatTat(end - created, isApproved);

            var status = isApproved
                ? "CLOSED"
                : (doc.Status ?? "OPEN").ToUpperInvariant();

            // VALUATION PRICE — the figure that matters is the QC-approved amount.
            // ValuationResponse.MidRange is only the AI *estimate* and is often a
            // placeholder, so it is the last resort, never preferred over QC.
            var qcAmount = doc.QualityControl?.ValuationAmount;
            decimal? valuationPrice =
                doc.FinalValuationAmount
                ?? (qcAmount > 0 ? qcAmount : null)
                ?? doc.ValuationResponse?.MidRange;

            // PAYMENT — prefer the Table Storage row (written by the payment popup);
            // fall back to the payment fields stamped onto the Cosmos doc at completion.
            var payStatus = Pick(pay?.PaymentStatus, doc.PaymentStatus);
            var payMode = Pick(pay?.PaymentMethod, doc.PaymentMethod);
            var payRef = Pick(pay?.PaymentReference, doc.PaymentReference);
            var payDate = pay?.PaymentDate ?? doc.PaymentDate;
            decimal? payAmount = pay?.PaymentAmount is double d
                ? (decimal)d
                : decimal.TryParse(doc.PaymentAmount, out var parsed) ? parsed : null;

            // Every text column is reported in CAPITALS so the on-screen MIS table and
            // the Excel export read the same way, regardless of how it was typed in.
            return new MisRowDto
            {
                UniqId = Up(uniq),
                ClientName = Up(sh?.Name),
                ClientState = Up(sh?.VehicleLocation?.State),
                Branch = Up(sh?.Branch),
                InspectionType = Up(sh?.ValuationType),
                LeadCreationDateTime = Fmt(created),
                InspectionDateTime = Fmt(insp?.DateOfInspection),
                ApprovedDateTime = Fmt(approvedAt),
                LeadStatus = Up(status),
                Tat = Up(tat),
                VehicleNo = Up(doc.VehicleNumber),
                OwnerName = Up(vd?.OwnerName),
                ApplicantName = Up(sh?.Applicant?.Name),
                MobileNo = Up(doc.ApplicantContact ?? sh?.Applicant?.Contact),
                Make = Up(vd?.Make),
                Model = Up(vd?.Model),
                Variant = Up(vd?.MakerVariant),
                VehicleCategory = Up(doc.VehicleSegment ?? sh?.VehicleSegment),
                Inspector = Up(insp?.VehicleInspectedBy ?? doc.AssignedTo),
                Year = vd?.YearOfMfg?.ToString() ?? "",
                VehicleClass = Up(vd?.ClassOfVehicle),
                ValuationPrice = valuationPrice,
                ExecutiveName = Up(sh?.ExecutiveName),
                ExecutiveMobile = Up(sh?.ExecutiveContact),

                PaymentStatus = Up(payStatus),
                PaymentMode = Up(payMode),
                PaymentReference = Up(payRef),
                PaymentAmount = payAmount,
                PaymentDate = Fmt(payDate),
            };
        }

        /// <summary>
        /// Turnaround time: hours up to a day, then days (with the leftover hours).
        /// Cases still awaiting approval are marked "(OPEN)".
        /// </summary>
        private static string FormatTat(TimeSpan span, bool isApproved)
        {
            var totalHours = Math.Max(0, (int)Math.Round(span.TotalHours));

            string text;
            if (totalHours < 24)
            {
                text = $"{totalHours} {(totalHours == 1 ? "HR" : "HRS")}";
            }
            else
            {
                var days = totalHours / 24;
                var hours = totalHours % 24;
                text = $"{days} {(days == 1 ? "DAY" : "DAYS")}";
                if (hours > 0) text += $" {hours} {(hours == 1 ? "HR" : "HRS")}";
            }

            return isApproved ? text : $"{text} (OPEN)";
        }

        /// <summary>Trimmed, uppercased text for MIS output ("" when absent).</summary>
        private static string Up(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpperInvariant();

        /// <summary>First non-blank of the candidates, or "".</summary>
        private static string Pick(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";

        private static string Fmt(DateTime? dt) =>
            dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";
    }
}
