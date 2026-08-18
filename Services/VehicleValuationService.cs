using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;
using Valuation.Api.Repositories;

namespace Valuation.Api.Services
{
    public class VehicleValuationService : IVehicleValuationService
    {
        private readonly Container _container;
        private readonly IChatGptRepository _chatGptRepo;

        public VehicleValuationService(
            CosmosClient cosmosClient,
            IChatGptRepository chatGptRepo)
        {
            var databaseName = Environment.GetEnvironmentVariable("DatabaseId") ?? "ValuationsDb";
            var containerName = Environment.GetEnvironmentVariable("ContainerId") ?? "Valuations";
            _container = cosmosClient.GetContainer(databaseName, containerName);
            _chatGptRepo = chatGptRepo;
        }

        private PartitionKey GetPk(string vehicleNumber, string applicantContact) =>
            new PartitionKey($"{vehicleNumber}|{applicantContact}");

        public async Task<VehicleValuation?> GetVehicleValuationAsync(
            string id,
            string vehicleNumber,
            string applicantContact)
        {
            var pk = GetPk(vehicleNumber, applicantContact);

            ValuationDocument doc;
            try
            {
                var resp = await _container.ReadItemAsync<ValuationDocument>(id, pk);
                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            // Map stored document to VehicleDetailsAIDto
            var detailsDto = new VehicleDetailsAIDto
            {
                RegistrationNumber = doc.VehicleDetails.RegistrationNumber,
                Make = doc.VehicleDetails.Make,
                Model = doc.VehicleDetails.Model,
                YearOfMfg = doc.VehicleDetails.YearOfMfg,
                Colour = doc.VehicleDetails.Colour,
                Fuel = doc.VehicleDetails.Fuel,
                EngineCC = doc.VehicleDetails.EngineCC,
                IDV = doc.VehicleDetails.IDV,
                DateOfRegistration = doc.VehicleDetails.DateOfRegistration,
                Odometer = doc.InspectionDetails.Odometer
                // …copy any other fields you need
            };

            // 1) Call ChatGPT to do the “web search” valuation
            //var rawResponse = await _chatGptRepo.GetVehicleValuationResponseAsync(detailsDto);
            var rawResponse = await _chatGptRepo.GetVehicleValuationAsync(detailsDto);
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return new VehicleValuation { RawResponse = string.Empty };
            }

            // Parse ranges and update document
            var valuation = ParseRanges(rawResponse);
            doc.ValuationResponse = new ValuationResponse
            {
                RawResponse = rawResponse,
                LowRange = valuation.LowRange,
                MidRange = valuation.MidRange,
                HighRange = valuation.HighRange
            };

            // Update document in Cosmos
            await _container.UpsertItemAsync(doc, pk);

            return valuation;
        }

        private VehicleValuation ParseRanges(string text) =>
            VehicleValuationParser.ParseRanges(text);
    }
}
