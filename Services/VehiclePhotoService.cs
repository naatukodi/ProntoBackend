using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using SkiaSharp;
using System.Text.Json;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public class VehiclePhotoService : IVehiclePhotoService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _blobContainerName;
        private readonly string _databaseName;
        private readonly string _containerName;
        private readonly string _cdnEndpoint;

        public VehiclePhotoService(
            CosmosClient cosmosClient,
            BlobServiceClient blobServiceClient,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _cosmosClient = cosmosClient;
            _blobServiceClient = blobServiceClient;
            _httpClientFactory = httpClientFactory;
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
                if (!validation.Valid) throw new ArgumentException($"Invalid vehicle video: {validation.Error}");

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

        // Gallery page photo selection (used by QC + the PDF generator)
        public async Task<List<string>> GetGalleryPhotoSelectionAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
                return response.Resource.SelectedGalleryPhotos ?? new List<string>();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<string>();
            }
        }

        public async Task<List<string>> UpdateGalleryPhotoSelectionAsync(string valuationId, string vehicleNumber, string applicantContact, List<string> selectedKeys)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
            var doc = response.Resource;

            doc.SelectedGalleryPhotos = selectedKeys ?? new List<string>();
            await container.UpsertItemAsync(doc, pk);

            return doc.SelectedGalleryPhotos;
        }

        // Burns a text note onto a photo, in the same "white text, dark outline, no
        // background box" style the capture-time watermark already uses. Always redraws
        // from the untouched pre-annotation original (captured once, on first use) so
        // editing the note later never stacks old and new text on top of each other.
        public async Task<(string PhotoUrl, string Note)> AnnotatePhotoAsync(string valuationId, string vehicleNumber, string applicantContact, string photoKey, string note)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
            var doc = response.Resource;

            string? displayedUrl = null;
            bool isFixedSlot = doc.PhotoUrls != null && doc.PhotoUrls.TryGetValue(photoKey, out displayedUrl) && !string.IsNullOrWhiteSpace(displayedUrl);
            SavedCustomPhoto? customMatch = null;
            PhotoMetadata? fixedMeta = null;
            string? originalUrl;

            if (isFixedSlot)
            {
                doc.PhotoMetadata ??= new Dictionary<string, PhotoMetadata>();
                if (!doc.PhotoMetadata.TryGetValue(photoKey, out fixedMeta) || fixedMeta == null)
                {
                    fixedMeta = new PhotoMetadata();
                    doc.PhotoMetadata[photoKey] = fixedMeta;
                }
                originalUrl = !string.IsNullOrWhiteSpace(fixedMeta.OriginalPhotoUrl) ? fixedMeta.OriginalPhotoUrl : displayedUrl;
            }
            else
            {
                customMatch = doc.CustomPhotos?.FirstOrDefault(p => p.Id == photoKey);
                if (customMatch == null) throw new KeyNotFoundException($"Photo '{photoKey}' not found.");
                displayedUrl = customMatch.PhotoUrl;
                originalUrl = !string.IsNullOrWhiteSpace(customMatch.OriginalPhotoUrl) ? customMatch.OriginalPhotoUrl : displayedUrl;
            }

            var httpClient = _httpClientFactory.CreateClient();
            var baseBytes = await httpClient.GetByteArrayAsync(originalUrl);
            var noteTrimmed = note?.Trim() ?? string.Empty;
            var annotatedBytes = BurnNoteOntoImage(baseBytes, noteTrimmed);

            using var uploadStream = new MemoryStream(annotatedBytes);
            var newUrl = await UploadBytesAndGenerateUrlAsync(uploadStream, ".jpg", "image/jpeg", vehicleNumber, applicantContact);

            if (isFixedSlot)
            {
                doc.PhotoUrls![photoKey] = newUrl;
                fixedMeta!.OriginalPhotoUrl = originalUrl;
                fixedMeta.AnnotationNote = noteTrimmed;
            }
            else
            {
                customMatch!.PhotoUrl = newUrl;
                customMatch.OriginalPhotoUrl = originalUrl;
                customMatch.AnnotationNote = noteTrimmed;
            }

            await container.UpsertItemAsync(doc, pk);

            // Never delete the preserved clean original — only the superseded
            // previously-displayed (already-annotated) version, if different.
            if (!string.IsNullOrWhiteSpace(displayedUrl) && !string.Equals(displayedUrl, originalUrl, StringComparison.OrdinalIgnoreCase))
            {
                await DeleteBlobByUrlAsync(displayedUrl);
            }

            return (newUrl, noteTrimmed);
        }

        // Loaded once and reused. SKTypeface.FromFamilyName("Arial", ...) looks up a
        // font by name in the OS's font registry, which silently falls back to a
        // mismatched substitute on Azure's Linux hosting (no Arial there) and produces
        // scrambled glyphs. Loading a bundled TTF by file path instead parses its glyph
        // table directly, so rendering is identical regardless of what's installed on
        // the host. QuestPDF already ships this exact Lato family to the output/publish
        // directory for its own use, so it's guaranteed to be deployed alongside the app.
        private static readonly Lazy<SKTypeface?> s_noteTypeface = new(() =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "LatoFont", "Lato-Bold.ttf");
            return File.Exists(path) ? SKTypeface.FromFile(path) : SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        });

        // Draws white bold text with a dark stroked outline (no background box),
        // matching the existing camera-app capture-time watermark style. Positioned
        // bottom-right, above where the existing date/location stamp typically sits.
        // Falls back to the untouched original if anything goes wrong.
        private static byte[] BurnNoteOntoImage(byte[] imageBytes, string note)
        {
            if (string.IsNullOrWhiteSpace(note)) return imageBytes;

            try
            {
                using var bitmap = SKBitmap.Decode(imageBytes);
                if (bitmap == null) return imageBytes;

                using var canvas = new SKCanvas(bitmap);

                float textSize = bitmap.Width * 0.032f;
                var typeface = s_noteTypeface.Value;

                using var strokePaint = new SKPaint
                {
                    Color = new SKColor(0, 0, 0, 217),
                    IsAntialias = true,
                    Typeface = typeface,
                    TextSize = textSize,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = textSize * 0.12f,
                    StrokeJoin = SKStrokeJoin.Round,
                    TextAlign = SKTextAlign.Right
                };
                using var fillPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    Typeface = typeface,
                    TextSize = textSize,
                    Style = SKPaintStyle.Fill,
                    TextAlign = SKTextAlign.Right
                };

                float pad = bitmap.Width * 0.02f;
                float x = bitmap.Width - pad;
                // Clear the existing date/location stamp, which can run up to ~5 lines
                // (date/time + multi-part address). Tied to this text's own line height
                // rather than a flat percentage so it scales with font/image size.
                float lineHeight = textSize * 1.35f;
                float y = bitmap.Height - (lineHeight * 5f) - pad;

                canvas.DrawText(note, x, y, strokePaint);
                canvas.DrawText(note, x, y, fillPaint);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                return data.ToArray();
            }
            catch
            {
                return imageBytes;
            }
        }

        private async Task<string> UploadBytesAndGenerateUrlAsync(Stream stream, string extension, string contentType, string vehicleNumber, string applicantContact)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobName = $"{vehicleNumber}/{applicantContact}/{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blobClient.UploadAsync(stream, headers);

            return blobClient.Uri.ToString();
        }

        private async Task DeleteBlobByUrlAsync(string url)
        {
            try
            {
                var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
                var uri = new Uri(url);
                var absolutePath = uri.AbsolutePath.TrimStart('/');
                var prefix = _blobContainerName + "/";
                var blobName = absolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? absolutePath.Substring(prefix.Length) : absolutePath;

                var blobClient = blobContainer.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync();
            }
            catch
            {
                // Best-effort cleanup — the new photo is already saved either way.
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