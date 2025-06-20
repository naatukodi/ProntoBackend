// Services/PincodeTableService.cs
using Azure;
using Azure.Data.Tables;
using Valuation.Api.Models;
using Valuation.Api.Services.Entities;
using System.Net.Http.Json;

namespace Valuation.Api.Services
{
    public class PincodeTableService : IPincodeTableService
    {
        private const string TableName = "Pincodes";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(30);
        private readonly TableClient _tableClient;
        private readonly HttpClient _http;

        public PincodeTableService(IConfiguration config, IHttpClientFactory httpFactory)
        {
            var conn = config.GetConnectionString("TableStorage")
                       ?? throw new InvalidOperationException("TableStorage connection not set.");
            _tableClient = new TableServiceClient(conn)
                              .GetTableClient(TableName);
            _tableClient.CreateIfNotExists();

            _http = httpFactory.CreateClient(nameof(PincodeTableService));
        }

        public async Task<IReadOnlyList<PincodeModel>> GetByPincodeAsync(string pincode)
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(CacheDuration);

            // build a valid OData filter string
            // – PartitionKey must be in single quotes
            // – datetime literal must be datetime'<ISO8601-with-Z>'
            // – use lowercase 'and'
            var cutoffStr = cutoff.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var filter = $"PartitionKey eq '{pincode}' and Timestamp ge datetime'{cutoffStr}'";

            // query only fresh rows
            var cached = new List<PincodeModel>();
            await foreach (var ent in _tableClient.QueryAsync<PincodeEntity>(filter: filter))
            {
                cached.Add(new PincodeModel
                {
                    Name = ent.Name,
                    Block = ent.Block,
                    District = ent.District,
                    Division = ent.Division,
                    State = ent.State,
                    Country = ent.Country,
                    Pincode = ent.PartitionKey
                });
            }

            if (cached.Count > 0)
                return cached;

            // 2) Cache miss or stale → fetch external API
            var apiResp = await _http
                .GetFromJsonAsync<ExternalResponse[]>(
                   $"https://api.postalpincode.in/pincode/{pincode}")
                ?? Array.Empty<ExternalResponse>();

            var first = apiResp.FirstOrDefault();
            if (first?.Status != "Success")
                return Array.Empty<PincodeModel>();

            // 3) (Optional) Delete any old entries for this pincode
            await foreach (var old in _tableClient.QueryAsync<PincodeEntity>(
                filter: TableClient.CreateQueryFilter($"PartitionKey eq {pincode}")))
            {
                await _tableClient.DeleteEntityAsync(old.PartitionKey, old.RowKey);
            }

            // 4) Insert fresh records & return
            var fresh = new List<PincodeModel>();
            foreach (var office in first.PostOffice)
            {
                var entity = new PincodeEntity
                {
                    PartitionKey = pincode,
                    RowKey = office.Name,
                    Name = office.Name,
                    Block = office.Block,
                    District = office.District,
                    Division = office.Division,
                    State = office.State,
                    Country = office.Country
                };

                await _tableClient
                   .UpsertEntityAsync(entity, TableUpdateMode.Replace);

                fresh.Add(new PincodeModel
                {
                    Name = office.Name,
                    Block = office.Block,
                    District = office.District,
                    Division = office.Division,
                    State = office.State,
                    Country = office.Country,
                    Pincode = pincode
                });
            }

            return fresh;
        }
        // helper to bind JSON
        private class ExternalResponse
        {
            public string Status { get; set; } = default!;
            public PostOffice[] PostOffice { get; set; } = Array.Empty<PostOffice>();
        }
        private class PostOffice
        {
            public string Name { get; set; } = default!;
            public string Block { get; set; } = default!;
            public string District { get; set; } = default!;
            public string Division { get; set; } = default!;
            public string State { get; set; } = default!;
            public string Country { get; set; } = default!;
        }
    }
}
