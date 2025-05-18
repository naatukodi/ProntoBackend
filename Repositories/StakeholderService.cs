using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Valuation.Api.Models;
using System.Net;

namespace Valuation.Api.Services
{
    public class StakeholderService : IStakeholderService
    {
        private readonly CosmosClient _cosmos;
        private readonly BlobServiceClient _blobService;
        private readonly string _blobContainerName;

        public StakeholderService(
            CosmosClient cosmos,
            BlobServiceClient blobService,
            IConfiguration configuration)
        {
            _cosmos = cosmos;
            _blobService = blobService;
            _blobContainerName = configuration["Blob:ContainerName"]
                                 ?? throw new InvalidOperationException("Blob:ContainerName not configured");
        }

        public async Task<Stakeholder?> GetAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var container = _cosmos.GetDatabase("ValuationsDb").GetContainer("Valuations");
            var composite = $"{vehicleNumber}|{applicantContact}";
            var pk = new PartitionKey(composite);

            try
            {
                var resp = await container.ReadItemAsync<ValuationDocument>(
                    id: valuationId, partitionKey: pk);
                return resp.Resource.Stakeholder;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var container = _cosmos.GetDatabase("ValuationsDb").GetContainer("Valuations");
            var composite = $"{vehicleNumber}|{applicantContact}";
            var pk = new PartitionKey(composite);

            // Read & remove the stakeholder section, then upsert
            try
            {
                var resp = await container.ReadItemAsync<ValuationDocument>(
                    id: valuationId, partitionKey: pk);
                var doc = resp.Resource;
                doc.Stakeholder = null;
                await container.UpsertItemAsync(doc, pk);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Nothing to delete
            }
        }


        public async Task UpdateAsync(StakeholderUpdateDto dto)
        {
            var database = _cosmos.GetDatabase("ValuationsDb");
            var container = database.GetContainer("Valuations");

            // 1) Compute composite PK
            var compositeKey = $"{dto.VehicleNumber}|{dto.ApplicantContact}";
            var pk = new PartitionKey(compositeKey);

            // 2) Try to read existing, or create new
            ValuationDocument doc;
            try
            {
                var resp = await container.ReadItemAsync<ValuationDocument>(
                    id: dto.ValuationId,
                    partitionKey: pk);
                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                doc = new ValuationDocument
                {
                    id = dto.ValuationId,
                    CompositeKey = compositeKey,
                    VehicleNumber = dto.VehicleNumber,
                    ApplicantContact = dto.ApplicantContact
                };
            }

            // 3) Upload files
            async Task<string?> UploadIf(IFormFile? file)
            {
                if (file == null) return null;

                var containerClient = _blobService.GetBlobContainerClient(_blobContainerName);
                var blobName = $"{dto.VehicleNumber}/{dto.ApplicantContact}/{Guid.NewGuid()}-{file.FileName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                var headers = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                };

                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, headers);
                return blobClient.Uri.ToString();
            }

            var rcUrl = await UploadIf(dto.RcFile);
            var insUrl = await UploadIf(dto.InsuranceFile);
            var otherUrls = new List<string>();
            if (dto.OtherFiles != null)
            {
                foreach (var f in dto.OtherFiles)
                    if (await UploadIf(f) is string u)
                        otherUrls.Add(u);
            }

            // 4) Patch the Stakeholder sub-document
            doc.Stakeholder = new Stakeholder
            {
                Name = dto.Name,
                ExecutiveName = dto.ExecutiveName,
                ExecutiveContact = dto.ExecutiveContact,
                ExecutiveWhatsapp = dto.ExecutiveWhatsapp,
                ExecutiveEmail = dto.ExecutiveEmail,
                Applicant = new Applicant { Name = dto.ApplicantName, Contact = dto.ApplicantContact },
                Documents = new List<Document>
                {
                    new Document { Type = "RC",        FilePath = rcUrl     ?? "", UploadedAt = DateTime.UtcNow },
                    new Document { Type = "Insurance", FilePath = insUrl    ?? "", UploadedAt = DateTime.UtcNow },
                    // If you want to surface others, you could append them here
                }
            };

            // 5) Ensure CompositeKey is set
            doc.CompositeKey = compositeKey;

            // 6) Upsert (creates or replaces)
            await container.UpsertItemAsync(doc, pk);
        }
    }
}
