using System.Net;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;

namespace Valuation.Api.Services;

public class QualityControlService : IQualityControlService
{
    private readonly Container Container;

    public QualityControlService(CosmosClient cosmosClient)
    {
        var databaseName = Environment.GetEnvironmentVariable("DatabaseId") ?? "ValuationsDb";
        var containerName = Environment.GetEnvironmentVariable("ContainerId") ?? "Valuations";
        Container = cosmosClient.GetContainer(databaseName, containerName);
    }
    private PartitionKey GetPk(string vehicleNumber, string applicantContact) =>
        new($"{vehicleNumber}|{applicantContact}");

    public async Task<QualityControl?> GetQualityControlAsync(string id, string vehicleNumber, string applicantContact)
    {
        var pk = GetPk(vehicleNumber, applicantContact);
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(id, pk);
            return resp.Resource.QualityControl ?? new QualityControl
            {
                OverallRating = "",
                ValuationAmount = 0,
                ChassisPunch = "",
                Remarks = null
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new QualityControl
            {
                OverallRating = "",
                ValuationAmount = 0,
                ChassisPunch = "",
                Remarks = null
            };
        }
    }

    public async Task UpdateQualityControlAsync(string id, QualityControlDto dto, string vehicleNumber, string applicantContact)
    {
        var pk = GetPk(vehicleNumber, applicantContact);

        // 1) Read
        var resp = await Container.ReadItemAsync<ValuationDocument>(id, pk);
        var doc = resp.Resource;

        // 2) Patch
        doc.QualityControl = new QualityControl
        {
            OverallRating = dto.OverallRating,
            ValuationAmount = dto.ValuationAmount,
            ChassisPunch = dto.ChassisPunch,
            Remarks = dto.Remarks
        };

        // 3) Upsert
        await Container.UpsertItemAsync(doc, pk);
    }

    public async Task DeleteQualityControlAsync(string id, string vehicleNumber, string applicantContact)
    {
        var pk = new PartitionKey($"{vehicleNumber}:{applicantContact}");
        try
        {
            var resp = await Container.ReadItemAsync<ValuationDocument>(id, pk);
            var doc = resp.Resource;
            doc.QualityControl = null;
            await Container.UpsertItemAsync(doc, pk);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignore
        }
    }
}
