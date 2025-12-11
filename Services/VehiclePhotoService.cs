// src/Valuation.Api/Services/VehiclePhotoService.cs
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public class VehiclePhotoService : IVehiclePhotoService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _blobContainerName;
        private readonly string _databaseName;
        private readonly string _containerName;
        private readonly string _cdnEndpoint;

        public VehiclePhotoService(
            CosmosClient cosmosClient,
            BlobServiceClient blobServiceClient,
            IConfiguration configuration)
        {
            _cosmosClient = cosmosClient;
            _blobServiceClient = blobServiceClient;
            _blobContainerName = configuration["Blob:ContainerName"]
                ?? "documents";
            _cdnEndpoint = configuration["Blob:CdnEndpointHostname"]
                ?? "https://prontomoto.azureedge.net";
            _databaseName = configuration["Cosmos:DatabaseId"]
                ?? "ValuationsDb";
            _containerName = configuration["Cosmos:ContainerId"]
                ?? "Valuations";
        }

        /// <summary>
        /// ✅ Validate video file (size and format)
        /// </summary>
        private (bool Valid, string Error) ValidateVideoFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return (false, "No file provided");

            // Check size (100MB max)
            const long maxSize = 100 * 1024 * 1024;
            if (file.Length > maxSize)
            {
                var sizeMB = (file.Length / 1024.0 / 1024.0).ToString("F2");
                return (false, $"File size exceeds 100MB ({sizeMB}MB)");
            }

            // Check format
            var allowedVideoMimeTypes = new[]
            {
                "video/mp4",
                "video/quicktime",           // .mov
                "video/x-msvideo",           // .avi
                "video/x-matroska",          // .mkv
                "video/webm",
                "video/mpeg"
            };

            if (!allowedVideoMimeTypes.Contains(file.ContentType))
            {
                return (false, $"Unsupported format: {file.ContentType}. Allowed: MP4, MOV, AVI, MKV, WebM, MPEG");
            }

            return (true, "");
        }

        /// <summary>
        /// ✅ Process and upload video to blob storage (similar to photos)
        /// </summary>
        private async Task<string?> ProcessAndUploadVideoAsync(
            IFormFile videoFile,
            string fieldKey)
        {
            if (videoFile == null)
                return null;

            // Ensure container exists with PublicAccessType.Blob
            var containerClient = _blobServiceClient
                .GetBlobContainerClient(_blobContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // Build blob name for video: videos/{guid}-{file}
            var sanitizedName = videoFile.FileName.Replace(" ", "_");
            var blobName = $"videos/{Guid.NewGuid()}-{sanitizedName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders { ContentType = videoFile.ContentType };
            using var stream = videoFile.OpenReadStream();
            await blobClient.UploadAsync(stream, headers);

            // Build and return the public CDN URL
            var cdnBase = _cdnEndpoint.TrimEnd('/');
            var container = containerClient.Name;
            return $"{cdnBase}/{container}/{blobName}";
        }

        /// <summary>
        /// Uploads any non‐null files in the DTO and returns a dictionary of fieldKey → public CDN URL.
        /// </summary>
        public async Task<Dictionary<string, string>> UpdatePhotosAsync(VehiclePhotosDto dto)
        {
            var compositeKey = $"{dto.VehicleNumber}|{dto.ApplicantContact}";
            var pk = new PartitionKey(compositeKey);

            // 1) Read or create ValuationDocument
            var database = _cosmosClient.GetDatabase(_databaseName);
            var container = database.GetContainer(_containerName);

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
                // Create a fresh document
                doc = new ValuationDocument
                {
                    id = dto.ValuationId,
                    CompositeKey = compositeKey,
                    VehicleNumber = dto.VehicleNumber,
                    ApplicantContact = dto.ApplicantContact,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    PhotoUrls = new Dictionary<string, string>(),
                    VideoUrls = new Dictionary<string, string>(),
                    Workflow = new List<WorkflowStep>
                    {
                        new() { StepOrder = 1, TemplateStepId = 1, AssignedToRole = "Stakeholder", Status = "InProgress" },
                        new() { StepOrder = 2, TemplateStepId = 2, AssignedToRole = "BackEnd", Status = "Pending" },
                        new() { StepOrder = 3, TemplateStepId = 3, AssignedToRole = "AVO", Status = "Pending" },
                        new() { StepOrder = 4, TemplateStepId = 4, AssignedToRole = "QC", Status = "Pending" },
                        new() { StepOrder = 5, TemplateStepId = 5, AssignedToRole = "FinalReport", Status = "Pending" }
                    }
                };
            }

            // 2) Helper to upload each photo file and return a public CDN URL
            async Task<string> UploadAndGenerateUrlAsync(IFormFile file)
            {
                if (file == null)
                    throw new ArgumentNullException(nameof(file));

                var containerClient = _blobServiceClient
                    .GetBlobContainerClient(_blobContainerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                var sanitizedName = file.FileName.Replace(" ", "_");
                var blobName = $"{dto.VehicleNumber}/{dto.ApplicantContact}/{Guid.NewGuid()}-{sanitizedName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                var headers = new BlobHttpHeaders { ContentType = file.ContentType };
                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, headers);

                var cdnBase = _cdnEndpoint.TrimEnd('/');
                var containerName = containerClient.Name; // "documents"
                return $"{cdnBase}/{containerName}/{blobName}";
            }

            // 3) Iterate over each DTO property; upload if non‐null (photos)
            var fieldsToCheck = new Dictionary<string, IFormFile?>
            {
                { nameof(dto.FrontLeftSide), dto.FrontLeftSide },
                { nameof(dto.FrontRightSide), dto.FrontRightSide },
                { nameof(dto.RearLeftSide), dto.RearLeftSide },
                { nameof(dto.RearRightSide), dto.RearRightSide },
                { nameof(dto.FrontViewGrille), dto.FrontViewGrille },
                { nameof(dto.RearViewTailgate), dto.RearViewTailgate },
                { nameof(dto.DriverSideProfile), dto.DriverSideProfile },
                { nameof(dto.PassengerSideProfile), dto.PassengerSideProfile },
                { nameof(dto.Dashboard), dto.Dashboard },
                { nameof(dto.InstrumentCluster), dto.InstrumentCluster },
                { nameof(dto.EngineBay), dto.EngineBay },
                { nameof(dto.ChassisNumberPlate), dto.ChassisNumberPlate },
                { nameof(dto.ChassisImprint), dto.ChassisImprint },
                { nameof(dto.GearAndSeats), dto.GearAndSeats },
                { nameof(dto.DashboardCloseup), dto.DashboardCloseup },
                { nameof(dto.Odometer), dto.Odometer },
                { nameof(dto.SelfieWithVehicle), dto.SelfieWithVehicle },
                { nameof(dto.Underbody), dto.Underbody },
                { nameof(dto.TiresAndRims), dto.TiresAndRims }
            };

            foreach (var kv in fieldsToCheck)
            {
                var fieldKey = kv.Key;
                var file = kv.Value;
                if (file != null)
                {
                    var publicUrl = await UploadAndGenerateUrlAsync(file);
                    doc.PhotoUrls[fieldKey] = publicUrl;
                }
            }

            // ✅ Process vehicle video (MANDATORY)
            if (dto.VehicleVideo != null)
            {
                var validation = ValidateVideoFile(dto.VehicleVideo);
                if (!validation.Valid)
                {
                    throw new Exception($"Invalid vehicle video: {validation.Error}");
                }

                var videoUrl = await ProcessAndUploadVideoAsync(
                    dto.VehicleVideo,
                    nameof(dto.VehicleVideo));

                if (videoUrl != null)
                {
                    if (doc.VideoUrls == null)
                        doc.VideoUrls = new Dictionary<string, string>();

                    doc.VideoUrls["VehicleVideo"] = videoUrl;
                }
            }

            // 4) Upsert document (create or replace)
            doc.CompositeKey = compositeKey;
            await container.UpsertItemAsync(doc, pk);

            // 5) Return the updated PhotoUrls + VideoUrls dictionary
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (doc.PhotoUrls != null)
            {
                foreach (var kv in doc.PhotoUrls)
                {
                    result[kv.Key] = kv.Value;
                }
            }

            if (doc.VideoUrls != null)
            {
                foreach (var kv in doc.VideoUrls)
                {
                    result[kv.Key] = kv.Value; // "VehicleVideo"
                }
            }

            return result;
        }

        /// <summary>
        /// Retrieves existing PhotoUrls (and video URLs) and returns their public CDN URLs.
        /// </summary>
        public async Task<Dictionary<string, string>?> GetPhotoUrlsAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact)
        {
            var compositeKey = $"{vehicleNumber}|{applicantContact}";
            var pk = new PartitionKey(compositeKey);

            var database = _cosmosClient.GetDatabase(_databaseName);
            var container = database.GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(
                    id: valuationId,
                    partitionKey: pk);
                var doc = response.Resource;
                var storedMap = doc.PhotoUrls;

                var updatedMap = new Dictionary<string, string>();
                var blobContainer = _blobServiceClient
                    .GetBlobContainerClient(_blobContainerName);

                // Photos (rebuild via CDN endpoint)
                if (storedMap != null)
                {
                    foreach (var kv in storedMap)
                    {
                        var fieldKey = kv.Key;
                        var existingUrl = kv.Value;

                        var uri = new Uri(existingUrl);
                        var absolutePath = uri.AbsolutePath.TrimStart('/');
                        var prefix = _blobContainerName + "/"; // "documents/"
                        var blobName = absolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            ? absolutePath.Substring(prefix.Length)
                            : absolutePath;

                        var cdnBase = _cdnEndpoint.TrimEnd('/');
                        var containerName = blobContainer.Name; // "documents"
                        updatedMap[fieldKey] = $"{cdnBase}/{containerName}/{blobName}";
                    }
                }

                // ✅ Add video URLs directly (already CDN-style from ProcessAndUploadVideoAsync)
                if (doc.VideoUrls != null)
                {
                    foreach (var kv in doc.VideoUrls)
                    {
                        updatedMap[kv.Key] = kv.Value; // "VehicleVideo"
                    }
                }

                return updatedMap;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// ✅ Get all video URLs from database
        /// </summary>
        public async Task<Dictionary<string, string>?> GetVideoUrlsAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact)
        {
            var compositeKey = $"{vehicleNumber}|{applicantContact}";
            var pk = new PartitionKey(compositeKey);

            var database = _cosmosClient.GetDatabase(_databaseName);
            var container = database.GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(
                    id: valuationId,
                    partitionKey: pk);
                var doc = response.Resource;

                return doc.VideoUrls ?? new Dictionary<string, string>();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes all blobs and clears PhotoUrls for this valuation.
        /// </summary>
        public async Task DeletePhotosAsync(
            string valuationId,
            string vehicleNumber,
            string applicantContact)
        {
            var compositeKey = $"{vehicleNumber}|{applicantContact}";
            var pk = new PartitionKey(compositeKey);

            var database = _cosmosClient.GetDatabase(_databaseName);
            var container = database.GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(
                    id: valuationId,
                    partitionKey: pk);
                var doc = response.Resource;

                var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
                foreach (var kv in doc.PhotoUrls)
                {
                    var url = kv.Value;
                    var uri = new Uri(url);
                    var absolutePath = uri.AbsolutePath.TrimStart('/');
                    var prefix = _blobContainerName + "/"; // "documents/"
                    var blobName = absolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        ? absolutePath.Substring(prefix.Length)
                        : absolutePath;

                    var blobClient = blobContainer.GetBlobClient(blobName);
                    await blobClient.DeleteIfExistsAsync();
                }

                doc.PhotoUrls.Clear();
                await container.UpsertItemAsync(doc, pk);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Nothing to do if document not found
            }
        }
    }
}
