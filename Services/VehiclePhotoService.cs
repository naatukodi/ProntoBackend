using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using SkiaSharp;
using System.IO.Compression;
using System.Text;
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
        // Company this request belongs to; stamped onto any case created here.
        private readonly IBrandContext _brand;

        public VehiclePhotoService(
            CosmosClient cosmosClient,
            BlobServiceClient blobServiceClient,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IBrandContext brand)
        {
            _brand = brand;
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
                    Brand = _brand.Current,
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

        /// <summary>
        /// Photos the company wordmark is never drawn on.
        ///
        /// These two are the evidence for the chassis number: the report leans on them
        /// to show the stamping is genuine, and the QC reader judges the punch off them.
        /// A mark laid over the characters weakens both, so they stay as captured.
        /// </summary>
        private static readonly HashSet<string> UnbrandedSlots =
            new(StringComparer.OrdinalIgnoreCase) { "ChassisVerification", "ChassisStencilTrace" };

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

            // Redraw whatever the photo already carries alongside the new note. If the
            // logo has been stamped, editing a note must not scrub it off.
            var logoApplied = isFixedSlot ? fixedMeta!.LogoApplied : customMatch!.LogoApplied;
            var annotatedBytes = ComposePhoto(
                baseBytes, noteTrimmed, logoApplied ? BrandContext.Of(doc.Brand) : null);

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

        /// <summary>
        /// Redraws a photo from its clean original with whichever layers the case
        /// currently wants: the AVO's note (bottom-right) and the company wordmark
        /// (bottom-left).
        ///
        /// Both layers go through this one pass on purpose. They are independent
        /// decisions over the same original, so stamping a logo has to redraw the note
        /// and vice versa — otherwise whichever ran second would silently drop the
        /// other. Drawing them together also means one decode and one JPEG encode, so
        /// adding a logo to an annotated photo does not re-compress it twice.
        ///
        /// Returns the input untouched if there is nothing to draw or anything fails.
        /// </summary>
        private static byte[] ComposePhoto(byte[] originalBytes, string? note, string? logoBrand)
        {
            var hasNote = !string.IsNullOrWhiteSpace(note);
            var hasLogo = !string.IsNullOrWhiteSpace(logoBrand);
            if (!hasNote && !hasLogo) return originalBytes;

            try
            {
                using var bitmap = SKBitmap.Decode(originalBytes);
                if (bitmap == null) return originalBytes;

                using var canvas = new SKCanvas(bitmap);

                if (hasLogo) DrawBrandLogo(canvas, bitmap, logoBrand!);
                if (hasNote) DrawNote(canvas, bitmap, note!);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                return data.ToArray();
            }
            catch
            {
                return originalBytes;
            }
        }

        // Decoded wordmarks, kept for the life of the process: every photo on every case
        // draws the same two files, and decoding a PNG per photo would dominate the cost
        // of stamping a 28-photo case. Drawing from a bitmap only reads it.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap?> BrandLogoCache = new();

        private static SKBitmap? LoadBrandLogo(string brand) =>
            BrandLogoCache.GetOrAdd(brand, key =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "png", $"{key}-logo-trimmed.png");
                return File.Exists(path) ? SKBitmap.Decode(path) : null;
            });

        /// <summary>
        /// Bottom-left wordmark, in the position and proportion the camera app used to
        /// burn in at capture time — 20% of the frame width, inset by 2%, over a white
        /// glow so it stays legible on a dark vehicle. Reproducing it here keeps photos
        /// taken before and after the capture-time logo was removed looking the same.
        /// </summary>
        private static void DrawBrandLogo(SKCanvas canvas, SKBitmap bitmap, string brand)
        {
            var logo = LoadBrandLogo(brand);
            if (logo == null) return;

            float pad = bitmap.Width * 0.02f;
            float width = bitmap.Width * 0.20f;
            float height = width * logo.Height / logo.Width;
            var dest = SKRect.Create(pad, bitmap.Height - height - pad, width, height);

            // Blur radius scales with the image so the glow reads the same on a 1920px
            // capture and on a smaller one, rather than being a fixed pixel count.
            float sigma = bitmap.Width * 0.003f;
            using var glow = new SKPaint
            {
                IsAntialias = true,
                ColorFilter = SKColorFilter.CreateBlendMode(SKColors.White, SKBlendMode.SrcATop),
                ImageFilter = SKImageFilter.CreateBlur(sigma, sigma),
            };
            // Twice, as the camera app did: one pass is too faint to separate a dark
            // logo from a dark photo.
            canvas.DrawBitmap(logo, dest, glow);
            canvas.DrawBitmap(logo, dest, glow);

            using var plain = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(logo, dest, plain);
        }

        // Draws white bold text with a dark stroked outline (no background box),
        // matching the existing camera-app capture-time watermark style. Positioned
        // bottom-right, above where the existing date/location stamp typically sits.
        private static void DrawNote(SKCanvas canvas, SKBitmap bitmap, string note)
        {
            {
                float textSize = bitmap.Width * 0.032f;
                using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);

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

        // -- Company wordmark (AVO stage) ----------------------------------------
        // The camera app captures without a logo: which company a case belongs to is
        // only settled when its vehicle number is looked up, and an offline capture
        // never settles it at all, so a mark burned in at capture time would be
        // permanent and sometimes wrong. It is stamped here instead, from the case's
        // own Brand, once the answer is certain.

        // Photos redrawn at once. Serial is far too slow for a 28-photo case (each is a
        // blob download, a composite and a blob upload); unbounded would hold the whole
        // case in memory. Matches the archive downloader's batch size.
        private const int LogoStampBatchSize = 4;

        public async Task<BrandLogoResult> ApplyBrandLogoAsync(
            string valuationId, string vehicleNumber, string applicantContact, bool apply)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
            var doc = response.Resource;

            // The case decides the brand, not the caller. A Pronto case gets the Pronto
            // wordmark even when a Vehga operator is the one clicking the button.
            var brand = BrandContext.Of(doc.Brand);
            var result = new BrandLogoResult { Brand = brand, Applied = apply };

            // One work item per photo, so fixed slots and custom photos share a code path.
            // Want is where each photo should end up: the requested state everywhere
            // except the chassis evidence slots, which are never marked. Carrying it per
            // photo rather than skipping those slots is what lets a case stamped before
            // they were exempted have the mark taken back off.
            var targets = new List<(string Key, string Displayed, string Original, string? Note, bool LogoApplied, bool Want)>();

            if (doc.PhotoUrls != null)
            {
                doc.PhotoMetadata ??= new Dictionary<string, PhotoMetadata>();
                foreach (var kv in doc.PhotoUrls)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    if (!doc.PhotoMetadata.TryGetValue(kv.Key, out var meta) || meta == null)
                    {
                        meta = new PhotoMetadata();
                        doc.PhotoMetadata[kv.Key] = meta;
                    }
                    var original = !string.IsNullOrWhiteSpace(meta.OriginalPhotoUrl) ? meta.OriginalPhotoUrl! : kv.Value;
                    var want = apply && !UnbrandedSlots.Contains(kv.Key);
                    targets.Add((kv.Key, kv.Value, original, meta.AnnotationNote, meta.LogoApplied, want));
                }
            }

            foreach (var photo in doc.CustomPhotos ?? new List<SavedCustomPhoto>())
            {
                if (string.IsNullOrWhiteSpace(photo.PhotoUrl)) continue;
                var original = !string.IsNullOrWhiteSpace(photo.OriginalPhotoUrl) ? photo.OriginalPhotoUrl! : photo.PhotoUrl;
                targets.Add((photo.Id, photo.PhotoUrl, original, photo.AnnotationNote, photo.LogoApplied, apply));
            }

            await RedrawTargetsAsync(doc, pk, container, brand, vehicleNumber, applicantContact, targets, result);
            return result;
        }

        /// <summary>
        /// Redraws every photo that is not already in its wanted state, writes the case,
        /// then drops the blobs it displaced. Shared by the AVO button and the chassis
        /// sweep so both get the same batching, the same "compose from the clean
        /// original" rule and the same delete-after-write ordering.
        /// </summary>
        private async Task RedrawTargetsAsync(
            ValuationDocument doc, PartitionKey pk, Container container, string brand,
            string vehicleNumber, string applicantContact,
            List<(string Key, string Displayed, string Original, string? Note, bool LogoApplied, bool Want)> targets,
            BrandLogoResult result)
        {
            // Already in the requested state: nothing to redraw. This is what makes the
            // button safe to press twice, and makes a second press after new photos
            // arrive stamp only those.
            var pending = targets.Where(t => t.LogoApplied != t.Want).ToList();
            if (pending.Count == 0) return;

            var httpClient = _httpClientFactory.CreateClient();
            var updates = new List<(string Key, string NewUrl, string Original, string Displaced, bool Want)>();

            for (var i = 0; i < pending.Count; i += LogoStampBatchSize)
            {
                var batch = pending.Skip(i).Take(LogoStampBatchSize).ToList();
                var done = await Task.WhenAll(batch.Select(async t =>
                {
                    try
                    {
                        var originalBytes = await httpClient.GetByteArrayAsync(t.Original);
                        var composed = ComposePhoto(originalBytes, t.Note, t.Want ? brand : null);

                        // Taking the logo off a photo with no note reproduces the clean
                        // original exactly, so point back at it rather than uploading a
                        // byte-identical copy and leaving the old one behind.
                        if (ReferenceEquals(composed, originalBytes))
                            return (t.Key, NewUrl: t.Original, t.Original, Displaced: t.Displayed, t.Want, Ok: true);

                        using var stream = new MemoryStream(composed);
                        var newUrl = await UploadBytesAndGenerateUrlAsync(
                            stream, ".jpg", "image/jpeg", vehicleNumber, applicantContact);
                        return (t.Key, NewUrl: newUrl, t.Original, Displaced: t.Displayed, t.Want, Ok: true);
                    }
                    catch
                    {
                        // One unreachable blob must not cost the operator the other 27.
                        return (t.Key, NewUrl: string.Empty, t.Original, Displaced: string.Empty, t.Want, Ok: false);
                    }
                }));

                foreach (var d in done)
                {
                    if (!d.Ok) { result.Failed++; continue; }
                    updates.Add((d.Key, d.NewUrl, d.Original, d.Displaced, d.Want));
                }
            }

            foreach (var (key, newUrl, original, _, want) in updates)
            {
                if (doc.PhotoUrls != null && doc.PhotoUrls.ContainsKey(key))
                {
                    doc.PhotoUrls[key] = newUrl;
                    var meta = doc.PhotoMetadata![key];
                    meta.OriginalPhotoUrl = original;
                    meta.LogoApplied = want;
                }
                else
                {
                    var custom = doc.CustomPhotos?.FirstOrDefault(p => p.Id == key);
                    if (custom == null) continue;
                    custom.PhotoUrl = newUrl;
                    custom.OriginalPhotoUrl = original;
                    custom.LogoApplied = want;
                }
                result.PhotoUrls[key] = newUrl;
                result.Changed++;
            }

            await container.UpsertItemAsync(doc, pk);

            // Only after the document points somewhere else. Deleting first would leave
            // the case showing a dead URL if the write failed. The clean original is
            // never deleted — it is what every future redraw starts from.
            foreach (var (_, newUrl, original, displaced, _) in updates)
            {
                if (string.IsNullOrWhiteSpace(displaced)) continue;
                if (string.Equals(displaced, original, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(displaced, newUrl, StringComparison.OrdinalIgnoreCase)) continue;
                await DeleteBlobByUrlAsync(displaced);
            }
        }

        /// <summary>
        /// One-shot repair: takes the company wordmark back off the chassis evidence on
        /// every case still carrying it.
        ///
        /// Those two slots were exempted after cases had already been stamped, and the
        /// exemption only stops the mark going on — a case marked before it landed would
        /// otherwise keep the mark until someone happened to press the button on it
        /// again. Unscoped by brand on purpose: the mark comes off by redrawing from the
        /// clean original, so whose wordmark it was does not change the work.
        /// </summary>
        public async Task<UnbrandSweepResult> StripChassisWordmarkAsync(bool dryRun)
        {
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);
            var outcome = new UnbrandSweepResult { DryRun = dryRun };

            // Cosmos treats a missing path as undefined rather than erroring, so a case
            // with no metadata for these slots simply does not match.
            var query = new QueryDefinition(@"
                SELECT c.id, c.VehicleNumber, c.ApplicantContact
                FROM c
                WHERE c.PhotoMetadata.ChassisVerification.LogoApplied = true
                   OR c.PhotoMetadata.ChassisStencilTrace.LogoApplied = true");

            var cases = new List<MarkedChassisCase>();
            using (var iterator = container.GetItemQueryIterator<MarkedChassisCase>(query))
            {
                while (iterator.HasMoreResults)
                    cases.AddRange((await iterator.ReadNextAsync()).Resource);
            }

            outcome.CasesMatched = cases.Count;

            foreach (var c in cases)
            {
                if (string.IsNullOrWhiteSpace(c.id) ||
                    string.IsNullOrWhiteSpace(c.VehicleNumber) ||
                    string.IsNullOrWhiteSpace(c.ApplicantContact))
                {
                    outcome.CasesFailed++;
                    continue;
                }

                outcome.Vehicles.Add(c.VehicleNumber!);
                if (dryRun) continue;

                try
                {
                    var pk = new PartitionKey($"{c.VehicleNumber}|{c.ApplicantContact}");
                    var doc = (await container.ReadItemAsync<ValuationDocument>(c.id!, pk)).Resource;
                    var brand = BrandContext.Of(doc.Brand);
                    var result = new BrandLogoResult { Brand = brand, Applied = false };

                    // Only the two evidence slots. Every other photo keeps the state the
                    // case already had it in — a sweep that also un-stamped the vehicle
                    // shots would undo work nobody asked to undo.
                    var targets = new List<(string Key, string Displayed, string Original, string? Note, bool LogoApplied, bool Want)>();
                    doc.PhotoMetadata ??= new Dictionary<string, PhotoMetadata>();

                    foreach (var slot in UnbrandedSlots)
                    {
                        if (doc.PhotoUrls == null ||
                            !doc.PhotoUrls.TryGetValue(slot, out var displayed) ||
                            string.IsNullOrWhiteSpace(displayed)) continue;
                        if (!doc.PhotoMetadata.TryGetValue(slot, out var meta) || meta == null) continue;

                        var original = !string.IsNullOrWhiteSpace(meta.OriginalPhotoUrl) ? meta.OriginalPhotoUrl! : displayed;
                        targets.Add((slot, displayed, original, meta.AnnotationNote, meta.LogoApplied, false));
                    }

                    await RedrawTargetsAsync(doc, pk, container, brand,
                        c.VehicleNumber!, c.ApplicantContact!, targets, result);

                    outcome.PhotosCleared += result.Changed;
                    outcome.PhotosFailed += result.Failed;
                    outcome.CasesChanged++;
                }
                catch
                {
                    // One bad case must not strand the rest of the backlog. It stays
                    // matched, so a second run picks it up again.
                    outcome.CasesFailed++;
                }
            }

            return outcome;
        }

        // Just enough of a case to reopen it by partition key.
        private class MarkedChassisCase
        {
            public string? id { get; set; }
            public string? VehicleNumber { get; set; }
            public string? ApplicantContact { get; set; }
        }

        // -- Photo archive (bulk download) ---------------------------------------
        // The AVO page cannot zip these in the browser: the blob container serves no
        // CORS headers, so a fetch() from the portal is refused and <a download> is
        // ignored cross-origin. Fetching server-side sidesteps both -- the same reason
        // annotation composites here rather than on a canvas.

        // How many photos are fetched at once. Serial fetches make a 28-photo case feel
        // broken; unbounded ones hold the whole case in memory at once.
        private const int ArchiveFetchBatchSize = 4;

        private static readonly string[] ArchiveImageExtensions =
            { ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".bmp", ".gif" };

        private static readonly string[] ArchiveVideoExtensions =
            { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".mpeg", ".mpg" };

        public async Task<List<PhotoArchiveEntry>?> GetPhotoArchiveEntriesAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            var pk = new PartitionKey($"{vehicleNumber}|{applicantContact}");
            var container = _cosmosClient.GetDatabase(_databaseName).GetContainer(_containerName);

            ValuationDocument doc;
            try
            {
                var response = await container.ReadItemAsync<ValuationDocument>(id: valuationId, partitionKey: pk);
                doc = response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            var entries = new List<PhotoArchiveEntry>();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string name, string? url, bool isVideo = false)
            {
                if (string.IsNullOrWhiteSpace(url)) return;

                var slug = ArchiveSlug(name);
                if (slug.Length == 0) slug = isVideo ? "video" : "photo";

                var extension = ArchiveExtension(url, isVideo);
                var fileName = slug + extension;
                for (var n = 2; !taken.Add(fileName); n++) fileName = $"{slug}-{n}{extension}";

                entries.Add(new PhotoArchiveEntry(fileName, url, isVideo));
            }

            // Fixed checklist slots. The key is the file name: "FrontLeftSide" reads as
            // front-left-side.jpg, so there is no label table here to fall out of step
            // with the ones the portal and the report already keep.
            if (doc.PhotoUrls != null)
                foreach (var kv in doc.PhotoUrls) Add(kv.Key, kv.Value);

            // Extra shots the AVO added, under the name they were given.
            if (doc.CustomPhotos != null)
                foreach (var photo in doc.CustomPhotos)
                    Add(string.IsNullOrWhiteSpace(photo.Name) ? photo.Id : photo.Name, photo.PhotoUrl);

            // The walkaround video. It is most of the download on its own, which is why
            // the writer streams it instead of holding it in memory like the photos.
            if (doc.VideoUrls != null)
                foreach (var kv in doc.VideoUrls) Add(kv.Key, kv.Value, isVideo: true);

            // Alphabetical, so unzipping the same case twice puts every file in the same
            // place.
            entries.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
            return entries;
        }

        public async Task WritePhotoArchiveAsync(IReadOnlyList<PhotoArchiveEntry> entries, Stream destination, CancellationToken ct = default)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var skipped = new List<string>();

            // Photos are small enough to fetch several at a time and hand over as bytes;
            // videos are not, so they are streamed afterwards, one at a time. That puts
            // the video last in the archive rather than in its sorted position, which no
            // file manager cares about -- they all sort by name on the way out.
            var photos = entries.Where(e => !e.IsVideo).ToList();
            var videos = entries.Where(e => e.IsVideo).ToList();

            // leaveOpen: the response body belongs to the caller. Disposal still has to
            // happen here though -- it is what writes the zip's central directory.
            using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

            for (var i = 0; i < photos.Count; i += ArchiveFetchBatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = photos.Skip(i).Take(ArchiveFetchBatchSize).ToList();
                var downloads = await Task.WhenAll(batch.Select(async entry =>
                {
                    try
                    {
                        return (Entry: entry, Bytes: (byte[]?)await httpClient.GetByteArrayAsync(entry.Url, ct));
                    }
                    catch (Exception) when (!ct.IsCancellationRequested)
                    {
                        // One dead blob must not cost the operator the other 27 photos.
                        return (Entry: entry, Bytes: (byte[]?)null);
                    }
                }));

                foreach (var (entry, bytes) in downloads)
                {
                    if (bytes == null)
                    {
                        skipped.Add(entry.FileName);
                        continue;
                    }

                    // Stored, not deflated: these are already JPEGs, so compressing them
                    // again burns CPU on every download to save almost nothing.
                    var zipEntry = archive.CreateEntry(entry.FileName, CompressionLevel.NoCompression);
                    using var zipStream = zipEntry.Open();
                    await zipStream.WriteAsync(bytes, ct);
                }
            }

            foreach (var entry in videos)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // Copied through rather than buffered: a walkaround video is allowed
                    // up to 100 MB, and holding one of those as a single byte[] per
                    // request is how a shared API runs out of memory. The stream is
                    // opened before the entry so a failed fetch -- which throws on the
                    // response status, before any bytes -- leaves no half-written file.
                    using var source = await httpClient.GetStreamAsync(entry.Url, ct);

                    var zipEntry = archive.CreateEntry(entry.FileName, CompressionLevel.NoCompression);
                    using var zipStream = zipEntry.Open();
                    await source.CopyToAsync(zipStream, ct);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    skipped.Add(entry.FileName);
                }
            }

            // The status code is long gone by the time a fetch fails, so a short archive
            // says so in a file rather than arriving silently incomplete.
            if (skipped.Count > 0)
            {
                var note = new StringBuilder()
                    .AppendLine("These photos could not be downloaded and are missing from this archive:")
                    .AppendLine();
                foreach (var name in skipped) note.AppendLine("  " + name);

                var errorEntry = archive.CreateEntry("MISSING-PHOTOS.txt", CompressionLevel.Optimal);
                using var errorStream = errorEntry.Open();
                await errorStream.WriteAsync(Encoding.UTF8.GetBytes(note.ToString()), ct);
            }
        }

        /// <summary>
        /// "FrontLeftSide" becomes "front-left-side", "Chassis imprint (rear)" becomes
        /// "chassis-imprint-rear". Splits PascalCase so slot keys read as file names, and
        /// drops anything a file system would rather not see.
        /// </summary>
        private static string ArchiveSlug(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            var previous = '\0';

            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (char.IsUpper(ch) && sb.Length > 0 && (char.IsLower(previous) || char.IsDigit(previous)))
                        sb.Append('-');
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (sb.Length > 0 && sb[^1] != '-')
                {
                    sb.Append('-');
                }
                previous = ch;
            }

            return sb.ToString().Trim('-');
        }

        /// <summary>
        /// Blob names keep the extension they were uploaded with. Anything unrecognised
        /// falls back to what that kind of capture normally is: .jpg for a photo, which is
        /// what the camera app and the annotator both produce, and .mp4 for a video.
        /// </summary>
        private static string ArchiveExtension(string url, bool isVideo)
        {
            var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
            var extension = Path.GetExtension(path).ToLowerInvariant();

            var allowed = isVideo ? ArchiveVideoExtensions : ArchiveImageExtensions;
            return allowed.Contains(extension) ? extension : (isVideo ? ".mp4" : ".jpg");
        }
    }
}