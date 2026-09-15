using System.Net;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;
using Valuation.Api.Repositories;

namespace Valuation.Api.Services
{
    public class VehicleValuationService : IVehicleValuationService
    {
        private readonly Container _container;
        private readonly IChatGptRepository _chatGptRepo;
        private readonly ILogger<VehicleValuationService> _logger;

        public VehicleValuationService(
            CosmosClient cosmosClient,
            IChatGptRepository chatGptRepo,
            IConfiguration configuration,
            ILogger<VehicleValuationService> logger)
        {
            // Read the same configuration keys as every other service. This used to read
            // the bare environment variables "DatabaseId"/"ContainerId": the defaults
            // happened to match, so it worked, but any environment that overrode
            // Cosmos:ContainerId sent this service to a different container than the
            // approval page reads from.
            var databaseName = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
            var containerName = configuration["Cosmos:ContainerId"] ?? "Valuations";
            _container = cosmosClient.GetContainer(databaseName, containerName);
            _chatGptRepo = chatGptRepo;
            _logger = logger;
        }

        private PartitionKey GetPk(string vehicleNumber, string applicantContact) =>
            new PartitionKey($"{vehicleNumber}|{applicantContact}");

        public Task<VehicleValuation?> GetVehicleValuationAsync(
            string id, string vehicleNumber, string applicantContact) =>
            EnsureAsync(id, vehicleNumber, applicantContact, force: false);

        /// <summary>
        /// The stored market range, generating one on first use.
        ///
        /// Previously every call hit OpenAI, which is why the one caller that existed
        /// was a fire-and-forget on the AVO submit path — nobody wanted a page to wait
        /// on it. Returning what is already stored makes the endpoint cheap enough to
        /// call from the approval page itself, so a case whose warm-up failed is no
        /// longer stuck with no range for good.
        /// </summary>
        public async Task<VehicleValuation?> EnsureAsync(
            string id, string vehicleNumber, string applicantContact,
            bool force = false, CancellationToken ct = default)
        {
            var pk = GetPk(vehicleNumber, applicantContact);

            ValuationDocument doc;
            try
            {
                var resp = await _container.ReadItemAsync<ValuationDocument>(id, pk, cancellationToken: ct);
                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var stored = doc.ValuationResponse;
            if (!force && stored is { MidRange: > 0 })
            {
                return new VehicleValuation
                {
                    RawResponse = stored.RawResponse,
                    LowRange = stored.LowRange,
                    MidRange = stored.MidRange,
                    HighRange = stored.HighRange
                };
            }

            var vd = doc.VehicleDetails;

            // A model asked to value an unnamed vehicle will still return a number, and
            // structured output makes that invention look authoritative. Refuse instead.
            if (string.IsNullOrWhiteSpace(vd?.Make) && string.IsNullOrWhiteSpace(vd?.Model))
            {
                _logger.LogInformation(
                    "No make or model on {Valuation}; skipping the market-range call.", id);
                return null;
            }

            var detailsDto = new VehicleDetailsAIDto
            {
                RegistrationNumber = vd?.RegistrationNumber ?? doc.VehicleNumber ?? string.Empty,
                Make = vd?.Make,
                Model = vd?.Model,
                YearOfMfg = vd?.YearOfMfg,
                Colour = vd?.Colour,
                Fuel = vd?.Fuel,
                EngineCC = vd?.EngineCC,
                IDV = vd?.IDV,
                DateOfRegistration = vd?.DateOfRegistration,
                Odometer = doc.InspectionDetails?.Odometer
            };

            var ai = await _chatGptRepo.GetVehicleValuationAsync(detailsDto);
            if (ai is null)
            {
                _logger.LogWarning("The valuation model returned nothing for {Valuation}.", id);
                return null;
            }

            if (!IsPlausible(ai, out var why))
            {
                // Keep the raw answer so the failure is inspectable, but leave the numbers
                // null. A wrong range that looks right is worse than no range: the old
                // regex at least failed loudly to 0, whereas typed output has no tell.
                _logger.LogWarning("Discarding market range for {Valuation}: {Reason}", id, why);
                doc.ValuationResponse = new ValuationResponse { RawResponse = ai.Raw ?? string.Empty };
                await SaveAsync(doc, pk, ct);
                return null;
            }

            doc.ValuationResponse = new ValuationResponse
            {
                RawResponse = ai.Raw ?? string.Empty,
                LowRange = ai.LowRange,
                MidRange = ai.MidRange,
                HighRange = ai.HighRange
            };
            await SaveAsync(doc, pk, ct);

            return new VehicleValuation
            {
                RawResponse = doc.ValuationResponse.RawResponse,
                LowRange = ai.LowRange,
                MidRange = ai.MidRange,
                HighRange = ai.HighRange
            };
        }

        /// <summary>
        /// Whether a returned range is worth storing.
        ///
        /// The bands must be present, positive and in order, and the spread must be
        /// credible — a high more than five times the low is a decimal-place slip or a
        /// different vehicle, not a market range.
        /// </summary>
        private static bool IsPlausible(VehicleValuationAi ai, out string why)
        {
            if (ai.LowRange is not > 0 || ai.MidRange is not > 0 || ai.HighRange is not > 0)
            {
                why = "one or more bands missing or not positive";
                return false;
            }
            if (ai.LowRange > ai.MidRange || ai.MidRange > ai.HighRange)
            {
                why = $"bands out of order ({ai.LowRange}/{ai.MidRange}/{ai.HighRange})";
                return false;
            }
            if (ai.HighRange > ai.LowRange * 5m)
            {
                why = $"spread implausible ({ai.LowRange} to {ai.HighRange})";
                return false;
            }
            why = string.Empty;
            return true;
        }

        /// <summary>
        /// Writes the range back without taking the rest of the document with it.
        ///
        /// This can run from a background warm-up while a reviewer is saving the QC form,
        /// and an unconditional replace of a document read seconds earlier would put the
        /// stale copy back over their work.
        /// </summary>
        private async Task SaveAsync(ValuationDocument doc, PartitionKey pk, CancellationToken ct)
        {
            await _container.PatchItemAsync<ValuationDocument>(
                doc.id, pk,
                new[] { PatchOperation.Set("/ValuationResponse", doc.ValuationResponse) },
                cancellationToken: ct);
        }
    }
}
