using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
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
            _blobContainerName = configuration["Blob:ContainerName"] ?? "documents";
            _cdnEndpoint = configuration["Blob:CdnEndpointHostname"] ?? "https://vehgablobs.blob.core.windows.net";
            _databaseName = configuration["Cosmos:DatabaseId"] ?? "ValuationsDb";
            _containerName = configuration["Cosmos:ContainerId"] ?? "Valuations";
        }

        private (bool Valid, string Error) ValidateVideoFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return (false, "No file provided");

            const long maxSize = 100 * 1024 * 1024;
            if (file.Length > maxSize)
            {
                var sizeMB = (file.Length / 1024.0 / 1024.0).ToString("F2");
                return (false, $"File size exceeds 100MB ({sizeMB}MB)");
            }

            var allowedExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".mpeg", ".mpg" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
            {
                return (false, $"Unsupported format: {ext}. Allowed: MP4, MOV, AVI, MKV, WebM, MPEG");
            }

            return (true, "");
        }

        private async Task<string?> ProcessAndUploadVideoAsync(IFormFile videoFile, string fieldKey)
        {
            if (videoFile == null) return null;

            var containerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // ✅ Optimized Name Sanitization
            var extension = Path.GetExtension(videoFile.FileName);
            var blobName = $"videos/{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders { ContentType = videoFile.ContentType };
            using var stream = videoFile.OpenReadStream();
            await blobClient.UploadAsync(stream, headers);

            return blobClient.Uri.ToString();
        }

        public async Task<Dictionary<string, string>> UpdatePhotosAsync(VehiclePhotosDto dto)
        {
            var compositeKey = $"{dto.VehicleNumber}|{dto.ApplicantContact}";
            var pk = new PartitionKey(compositeKey);

            var database = _cosmosClient.GetDatabase(_databaseName);
            var container = database.GetContainer(_containerName);

            ValuationDocument doc;
            try
            {
                var resp = await container.ReadItemAsync<ValuationDocument>(id: dto.ValuationId, partitionKey: pk);
                doc = resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
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
                    CustomPhotos = new List<SavedCustomPhoto>(), // Init list
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

            async Task<string> UploadAndGenerateUrlAsync(IFormFile file)
            {
                if (file == null) throw new ArgumentNullException(nameof(file));

                var containerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                // ✅ Optimized Name Sanitization
                var extension = Path.GetExtension(file.FileName);
                var blobName = $"{dto.VehicleNumber}/{dto.ApplicantContact}/{Guid.NewGuid()}{extension}";
                var blobClient = containerClient.GetBlobClient(blobName);

                var headers = new BlobHttpHeaders { ContentType = file.ContentType };
                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, headers);

                return blobClient.Uri.ToString();
            }

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
                { nameof(dto.VinPlate), dto.VinPlate },
                { nameof(dto.ChassisImprint), dto.ChassisImprint },
                { nameof(dto.GearInterior), dto.GearInterior },
                { nameof(dto.FrontSeat), dto.FrontSeat },
                { nameof(dto.RearSeat), dto.RearSeat },
                { nameof(dto.DashboardCloseup), dto.DashboardCloseup },
                { nameof(dto.Odometer), dto.Odometer },
                { nameof(dto.SelfieWithVehicle), dto.SelfieWithVehicle },
                { nameof(dto.Underbody), dto.Underbody },
                { nameof(dto.TireFrontLeft), dto.TireFrontLeft },
                { nameof(dto.TireFrontRight), dto.TireFrontRight },
                { nameof(dto.TireRearLeft), dto.TireRearLeft },
                { nameof(dto.TireRearRight), dto.TireRearRight },
                { nameof(dto.ChassisVerification), dto.ChassisVerification },
                { nameof(dto.ChassisStencilTrace), dto.ChassisStencilTrace },
                { nameof(dto.WorkingOperationPhoto), dto.WorkingOperationPhoto }
            };

            foreach (var kv in fieldsToCheck)
            {
                if (kv.Value != null)
                {
                    var publicUrl = await UploadAndGenerateUrlAsync(kv.Value);
                    doc.PhotoUrls[kv.Key] = publicUrl;
                }
            }

            // ✅ NEW: Process Custom Images dynamically
            if (dto.CustomImageFiles != null && dto.CustomImageFiles.Count > 0)
            {
                var parsedMetadata = new List<CustomImageMetadataInput>();
                if (!string.IsNullOrWhiteSpace(dto.CustomImagesMetadata))
                {
                    parsedMetadata = JsonSerializer.Deserialize<List<CustomImageMetadataInput>>(
                        dto.CustomImagesMetadata, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    ) ?? new List<CustomImageMetadataInput>();
                }

                if (doc.CustomPhotos == null) doc.CustomPhotos = new List<SavedCustomPhoto>();

                for (int i = 0; i < dto.CustomImageFiles.Count; i++)
                {
                    var customFile = dto.CustomImageFiles[i];
                    var meta = parsedMetadata.FirstOrDefault(m => m.Index == i);

                    if (customFile.Length > 0)
                    {
                        var publicUrl = await UploadAndGenerateUrlAsync(customFile);
                        
                        doc.CustomPhotos.Add(new SavedCustomPhoto
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = meta?.Name ?? "Custom Image",
                            PhotoUrl = publicUrl,
                            DateCaptured = meta?.Date,
                            Location = meta?.Location
                        });
                    }
                }
            }

            if (dto.VehicleVideo != null)
            {
                var validation = ValidateVideoFile(dto.VehicleVideo);
                if (!validation.Valid) throw new Exception($"Invalid vehicle video: {validation.Error}");

                var videoUrl = await ProcessAndUploadVideoAsync(dto.VehicleVideo, nameof(dto.VehicleVideo));

                if (videoUrl != null)
                {
                    if (doc.VideoUrls == null) doc.VideoUrls = new Dictionary<string, string>();
                    doc.VideoUrls["VehicleVideo"] = videoUrl;
                }
            }

            doc.CompositeKey = compositeKey;
            await container.UpsertItemAsync(doc, pk);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (doc.PhotoUrls != null) foreach (var kv in doc.PhotoUrls) result[kv.Key] = kv.Value;
            if (doc.VideoUrls != null) foreach (var kv in doc.VideoUrls) result[kv.Key] = kv.Value;

            return result;
        }

        public async Task<Dictionary<string, string>?> GetPhotoUrlsAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
                var doc = response.Resource;
                var updatedMap = new Dictionary<string, string>();

                if (doc.PhotoUrls != null)
                    foreach (var kv in doc.PhotoUrls) updatedMap[kv.Key] = kv.Value;

                if (doc.VideoUrls != null)
                    foreach (var kv in doc.VideoUrls) updatedMap[kv.Key] = kv.Value;

                return updatedMap;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<Dictionary<string, string>?> GetVideoUrlsAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
                return response.Resource.VideoUrls ?? new Dictionary<string, string>();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }
        
        // ✅ NEW: Retrieve custom photos for the PDF
        public async Task<List<SavedCustomPhoto>> GetCustomPhotosAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
                return response.Resource.CustomPhotos ?? new List<SavedCustomPhoto>();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<SavedCustomPhoto>();
            }
        }

        public async Task DeletePhotosAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
                var doc = response.Resource;
                var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobContainerName);

                foreach (var kv in doc.PhotoUrls)
                {
                    var uri = new Uri(kv.Value);
                    var absolutePath = uri.AbsolutePath.TrimStart('/');
                    var prefix = _blobContainerName + "/";
                    var blobName = absolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        ? absolutePath.Substring(prefix.Length) : absolutePath;

                    var blobClient = blobContainer.GetBlobClient(blobName);
                    await blobClient.DeleteIfExistsAsync();
                }

                doc.PhotoUrls.Clear();
                await container.UpsertItemAsync(doc, pk);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        }

        // ✅ UPDATED: Fixed Cross-Partition Query by using ReadItemAsync with PartitionKey
        public async Task<PhotoMetadata> UpdatePhotoMetadataAsync(string valuationId, string vehicleNumber, string applicantContact, string photoType, PhotoMetadataUpdateDto input)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
            var doc = response.Resource;

            if (doc.PhotoMetadata == null) doc.PhotoMetadata = new Dictionary<string, PhotoMetadata>();
            if (!doc.PhotoMetadata.ContainsKey(photoType)) doc.PhotoMetadata[photoType] = new PhotoMetadata();

            doc.PhotoMetadata[photoType].CapturedDate = input.CapturedDate;
            doc.PhotoMetadata[photoType].LocationText = input.LocationText;

            await container.UpsertItemAsync(doc, pk);

            return doc.PhotoMetadata[photoType];
        }

        // ✅ UPDATED: Fixed Cross-Partition Query
        public async Task<Dictionary<string, PhotoMetadata>> GetPhotoMetadataAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
                return response.Resource.PhotoMetadata ?? new Dictionary<string, PhotoMetadata>();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new Dictionary<string, PhotoMetadata>();
            }
        }
    }
}