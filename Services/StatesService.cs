using Azure.Data.Tables;
using Azure;
using Valuation.Api.Models;

namespace Valuation.Api.Services;

public class StatesService: IStateService
{
    private readonly TableClient _tableClient;

    public StatesService(TableClient tableClient)
        => _tableClient = tableClient;

    /// <summary>
    /// Returns all states as before...
    /// </summary>
    public async Task<List<StateModel>> GetAllStatesAsync()
    {
        var results = new List<StateModel>();

        try
        {
            // Only fetch rows where PartitionKey == "STATE"
            string filter = TableClient.CreateQueryFilter<StateEntity>(e => e.PartitionKey == "STATE");

            await foreach (var entity in _tableClient.QueryAsync<StateEntity>(filter: filter)
                                                     .ConfigureAwait(false))
            {
                results.Add(new StateModel
                {
                    Key = entity.RowKey,
                    Name = entity.StateName,
                    DistrictCount = entity.DistrictCount
                });
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table not found or no entities—just return empty list
        }
        catch (Exception ex)
        {
            // Bubble up any unexpected errors
            throw new InvalidOperationException("Error querying states table", ex);
        }

        return results;
    }


    /// <summary>
    /// Fetches the list of districts for a given state key (RowKey).
    /// </summary>
    public async Task<List<string>> GetDistrictsByStateAsync(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            throw new ArgumentException("State key must be provided", nameof(stateKey));

        try
        {
            // This will throw RequestFailedException(404) if no such entity
            var response = await _tableClient.GetEntityAsync<StateEntity>(
                                   partitionKey: "STATE",
                                   rowKey: stateKey)
                               .ConfigureAwait(false);

            var entity = response.Value;
            return entity.DistrictList;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Either the table or that row doesn't exist
            return new List<string>();
        }
    }
}
