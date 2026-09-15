using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;
using Valuation.Api.Services.Interfaces;

namespace Valuation.Api.Services
{
    /// <inheritdoc cref="IReferenceNumberService"/>
    public class ReferenceNumberService : IReferenceNumberService
    {
        private readonly CosmosClient _cosmos;
        private readonly string _dbId;
        private readonly string _containerId;
        private readonly ILogger<ReferenceNumberService> _logger;

        public ReferenceNumberService(CosmosClient cosmos, IConfiguration config,
                                      ILogger<ReferenceNumberService> logger)
        {
            _cosmos = cosmos;
            _dbId = config["Cosmos:DatabaseId"] ?? "ValuationsDb";
            _containerId = config["Cosmos:ContainerId"] ?? "Valuations";
            _logger = logger;
        }

        private Container Container => _cosmos.GetDatabase(_dbId).GetContainer(_containerId);

        // "PM" for Pronto Moto, "VG" for Vehga. Kept identical to
        // PdfReportService.BrandPrefix — a case must not be able to acquire one prefix
        // here and a different one there.
        private static string BrandPrefix(string? brand) =>
            string.Equals(brand, "pronto", StringComparison.OrdinalIgnoreCase) ? "PM" : "VG";

        /// <summary>
        /// The reference for a document, as a pure function of its identity.
        ///
        /// A direct copy of PdfReportService.ComposeReference, with one deliberate
        /// difference: the seed falls back to the document's VehicleNumber when
        /// VehicleDetails has not been fetched yet. Minting now happens at registration,
        /// which is before the RC lookup runs, so the PDF's expression alone would seed
        /// every new case on a null. The GUID in the seed keeps it unique either way;
        /// the fallback only keeps it meaningful.
        /// </summary>
        private static string ComposeReference(ValuationDocument doc, int attempt)
        {
            var vehicle = doc.VehicleDetails?.RegistrationNumber;
            if (string.IsNullOrWhiteSpace(vehicle)) vehicle = doc.VehicleNumber;

            var seed = $"{vehicle}|{doc.CreatedAt:yyyyMMdd}|{doc.id}";
            if (attempt > 0) seed += $"|{attempt}";

            var h = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            var num = ((h[0] << 16) | (h[1] << 8) | h[2]) % 1_000_000;
            var letter = (char)('A' + h[3] % 26);
            return $"{BrandPrefix(doc.Brand)}-{num:D6}-{letter}";
        }

        /// <summary>
        /// Whether another case already holds this reference.
        ///
        /// The space is only 26 million (6 digits x 1 letter), so by the birthday bound
        /// a collision is roughly 1% likely at ~720 cases and 50% at ~6,000 — and a
        /// collision means two reports share a blob path and one QR serves the wrong
        /// vehicle. Hence the check, exactly as the PDF service does it.
        /// </summary>
        private async Task<bool> ReferenceTakenAsync(string reference, string excludeDocId,
                                                     CancellationToken ct)
        {
            var query = new QueryDefinition(
                    "SELECT VALUE COUNT(1) FROM c WHERE c.ReferenceNumber = @ref AND c.id != @id")
                .WithParameter("@ref", reference)
                .WithParameter("@id", excludeDocId);

            using var iterator = Container.GetItemQueryIterator<int>(query);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(ct);
                if (page.FirstOrDefault() > 0) return true;
            }
            return false;
        }

        public async Task<string?> EnsureAsync(string valuationId, string vehicleNumber,
                                               string applicantContact, CancellationToken ct = default)
        {
            var pk = $"{vehicleNumber}|{applicantContact}";

            ValuationDocument doc;
            try
            {
                var resp = await Container.ReadItemAsync<ValuationDocument>(
                    valuationId, new PartitionKey(pk), cancellationToken: ct);
                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("No case {Valuation} under {Pk}; no reference assigned.", valuationId, pk);
                return null;
            }

            // Never recompute. The QR on any report already issued encodes this value.
            if (!string.IsNullOrWhiteSpace(doc.ReferenceNumber)) return doc.ReferenceNumber;

            var reference = ComposeReference(doc, 0);
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                if (!await ReferenceTakenAsync(reference, doc.id, ct)) break;
                reference = ComposeReference(doc, attempt);
            }

            doc.ReferenceNumber = reference;

            // Patch rather than replace: this runs in the background alongside the
            // stakeholder save, and replacing the whole document would take the version
            // read a moment ago and write it back over anything saved in between.
            await Container.PatchItemAsync<ValuationDocument>(
                doc.id, new PartitionKey(pk),
                new[] { PatchOperation.Set("/ReferenceNumber", reference) },
                cancellationToken: ct);

            _logger.LogInformation("Assigned reference {Reference} to {Valuation}.", reference, valuationId);
            return reference;
        }
    }
}
