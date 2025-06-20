// Services/StakeholderService.cs
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;
using System.Net;

namespace Valuation.Api.Services
{
    public class StakeholderService : IStakeholderService
    {
        private readonly CosmosClient _cosmos;
        private readonly BlobServiceClient _blobService;
        private readonly string _blobContainerName;
        private readonly IWorkflowTableService _workflowTableService;

        public StakeholderService(
            CosmosClient cosmos,
            BlobServiceClient blobService,
            IWorkflowTableService workflowTableService,
            IConfiguration configuration)
        {
            _cosmos = cosmos;
            _blobService = blobService;
            _workflowTableService = workflowTableService;
            _blobContainerName = configuration["Blob:ContainerName"]!
                ?? throw new InvalidOperationException("Blob:ContainerName not configured");
        }

        public async Task<Stakeholder?> GetAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var databaseName = Environment.GetEnvironmentVariable("Cosmos:Database") ?? "ValuationsDb";
            var containerName = Environment.GetEnvironmentVariable("Cosmos:Container") ?? "Valuations";

            var container = _cosmos.GetDatabase(databaseName).GetContainer(containerName);
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            try
            {
                var resp = await container.ReadItemAsync<ValuationDocument>(valuationId, pk);
                return resp.Resource.Stakeholder ?? new Stakeholder
                {
                    Name = "",
                    ExecutiveName = "",
                    ExecutiveContact = "",
                    ExecutiveWhatsapp = "",
                    ExecutiveEmail = "",
                    Applicant = new Applicant { Name = "", Contact = "" },
                    Documents = new List<Document>()
                };
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new Stakeholder
                {
                    Name = "",
                    ExecutiveName = "",
                    ExecutiveContact = "",
                    ExecutiveWhatsapp = "",
                    ExecutiveEmail = "",
                    Applicant = new Applicant { Name = "", Contact = "" },
                    Documents = new List<Document>()
                };
            }
        }

        public async Task DeleteAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var databaseName = Environment.GetEnvironmentVariable("Cosmos:Database") ?? "ValuationsDb";
            var containerName = Environment.GetEnvironmentVariable("Cosmos:Container") ?? "Valuations";
            var container = _cosmos.GetDatabase(databaseName).GetContainer(containerName);
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            try
            {
                var resp = await container.ReadItemAsync<ValuationDocument>(valuationId, pk);
                var doc = resp.Resource;
                doc.Stakeholder = null;
                await container.UpsertItemAsync(doc, pk);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
        }

        public async Task UpdateAsync(StakeholderUpdateDto dto)
        {
            var databaseName = Environment.GetEnvironmentVariable("Cosmos:DatabaseId") ?? "ValuationsDb";
            var containerName = Environment.GetEnvironmentVariable("Cosmos:ContainerId") ?? "Valuations";

            var container = _cosmos.GetDatabase(databaseName).GetContainer(containerName);
            // 1) Compute composite PK
            var compositeKey = $"{dto.VehicleNumber}|{dto.ApplicantContact}";
            var pk = new PartitionKey(compositeKey);
            //var pk = new PartitionKey($"{dto.VehicleNumber}|{dto.ApplicantContact}");
            ValuationDocument doc;
            try
            {
                var resp = await container.ReadItemAsync<ValuationDocument>(dto.ValuationId, pk);
                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                doc = new ValuationDocument
                {
                    id = dto.ValuationId,
                    CompositeKey = pk.ToString(),
                    VehicleNumber = dto.VehicleNumber,
                    ApplicantContact = dto.ApplicantContact
                };

                doc.Status = "Open";
                doc.CreatedAt = DateTime.UtcNow;
            }



            // Upload files
            async Task<string?> Upload(IFormFile? file)
            {
                if (file == null) return null;
                var containerClient = _blobService.GetBlobContainerClient(_blobContainerName);
                var blobName = $"{dto.VehicleNumber}/{dto.ApplicantContact}/{Guid.NewGuid()}-{file.FileName}";
                var client = containerClient.GetBlobClient(blobName);
                await client.UploadAsync(file.OpenReadStream(), new BlobHttpHeaders { ContentType = file.ContentType });
                return client.Uri.ToString();
            }

            // Initialize workflow if missing
            if (doc.Workflow == null)
            {
                doc.Workflow = new List<WorkflowStep>
            {
                new(){ StepOrder=1, TemplateStepId=1, AssignedToRole="Stakeholder",  Status="InProgress" },
                new(){ StepOrder=2, TemplateStepId=2, AssignedToRole="BackEnd",      Status="Pending" },
                new(){ StepOrder=3, TemplateStepId=3, AssignedToRole="AVO",          Status="Pending" },
                new(){ StepOrder=4, TemplateStepId=4, AssignedToRole="QC",           Status="Pending" },
                new(){ StepOrder=5, TemplateStepId=5, AssignedToRole="FinalReport",  Status="Pending" },
            };
            }

            // Map location
            doc.Stakeholder = new Stakeholder
            {
                Name = dto.Name,
                ExecutiveName = dto.ExecutiveName,
                ExecutiveContact = dto.ExecutiveContact,
                ExecutiveWhatsapp = dto.ExecutiveWhatsapp,
                ExecutiveEmail = dto.ExecutiveEmail,
                ValuationType = dto.ValuationType,
                VehicleSegment = dto.VehicleSegment,
                VehicleLocation = new VehicleLocation
                {
                    Pincode = dto.Pincode,
                    Name = dto.LocationName,
                    District = dto.District,
                    Division = dto.Division,
                    Block = dto.Block,
                    State = dto.State,
                    Country = dto.Country

                },
                Applicant = new Applicant
                {
                    Name = dto.ApplicantName,
                    Contact = dto.ApplicantContact
                }
            };

            // Upload documents
            var docs = new List<Document>();
            if (await Upload(dto.RcFile) is string rcUrl)
                docs.Add(new Document { Type = "RC", FilePath = rcUrl, UploadedAt = DateTime.UtcNow });
            if (await Upload(dto.InsuranceFile) is string insUrl)
                docs.Add(new Document { Type = "Insurance", FilePath = insUrl, UploadedAt = DateTime.UtcNow });
            if (dto.OtherFiles != null)
                foreach (var f in dto.OtherFiles)
                    if (await Upload(f) is string u)
                        docs.Add(new Document { Type = "Other", FilePath = u, UploadedAt = DateTime.UtcNow });
            doc.Stakeholder.Documents = docs;

            // 5) Ensure CompositeKey is set
            doc.CompositeKey = compositeKey;

            await container.UpsertItemAsync(doc, pk);

            // Mirror to Table
            var workflowDto = new WorkflowUpdateDto
            {
                ValuationId = dto.ValuationId,
                VehicleNumber = dto.VehicleNumber,
                ApplicantName = dto.ApplicantName,
                ApplicantContact = dto.ApplicantContact,
                Location = dto.LocationName,
                Workflow = "Stakeholder",
                WorkflowStepOrder = 1,
                Status = "InProgress",
                CreatedAt = doc.CreatedAt,
                RedFlag = doc.RedFlag,
                Remarks = doc.Remarks,
                AssignedToPhoneNumber = doc.AssignedToPhoneNumber ?? "",
                AssignedToEmail = doc.AssignedToEmail ?? "",
                AssignedToWhatsapp = doc.AssignedToWhatsapp ?? "",
                Name = doc.Stakeholder?.Name ?? "",
                ValuationType = doc.Stakeholder?.ValuationType ?? ""
            };
            await _workflowTableService.UpdateAsync(workflowDto);
        }

        public Task UpdateDocumentsAsync(StakeholderUpdateDto dto)
        {
            // Could implement separate logic to only upload docs
            return UpdateAsync(dto);
        }
    }
}