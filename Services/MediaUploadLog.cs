using System.Reflection;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Valuation.Api.Models;
using Valuation.Api.Services.Interfaces;

namespace Valuation.Api.Services
{
    /// <summary>
    /// Records photos and video arriving from a phone, for the portal's "photos uploaded"
    /// messages: the header bell, the pop-up and the dashboard badge. Until these, the
    /// office only found out by opening each case.
    /// </summary>
    public interface IMediaUploadLog
    {
        /// <summary>
        /// Records one upload to <paramref name="dto"/>'s case. <paramref name="caseMedia"/>
        /// is every photo and video URL the case holds after it, as the upload replies.
        /// Never throws: a failed log must not fail the upload it describes.
        /// </summary>
        Task RecordAsync(VehiclePhotosDto dto, string source, IReadOnlyDictionary<string, string> caseMedia);

        /// <summary>This company's uploads from the last <paramref name="days"/> days, latest first.</summary>
        Task<List<MediaUploadDto>> GetRecentAsync(int days);
    }

    public class MediaUploadLog : IMediaUploadLog
    {
        /// <summary>
        /// A second upload to a case within this long of the last one is the same visit.
        /// The camera app sends its photos, then its video in a request of its own that can
        /// run for many minutes, and the office wants one message for both.
        /// </summary>
        private static readonly TimeSpan MergeWindow = TimeSpan.FromMinutes(30);

        /// <summary>How far back to look for a row to fold an upload into.</summary>
        private static readonly TimeSpan MergeLookBack = TimeSpan.FromDays(1);

        private const int MaxRows = 200;

        /// <summary>The DTO's photo slots, in declaration order (the video and custom photos are counted apart).</summary>
        private static readonly PropertyInfo[] PhotoSlots = typeof(VehiclePhotosDto)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(IFormFile) && p.Name != nameof(VehiclePhotosDto.VehicleVideo))
            .ToArray();

        private static volatile bool _tableReady;

        private readonly TableClient _table;
        private readonly IBrandContext _brand;
        private readonly IReferenceNumberService _references;
        private readonly IWorkflowTableService _workflows;
        private readonly IVehiclePhotoService _photos;
        private readonly IClientRules _clientRules;
        private readonly ILogger<MediaUploadLog> _log;

        public MediaUploadLog(IConfiguration config, IBrandContext brand, IReferenceNumberService references,
                              IWorkflowTableService workflows, IVehiclePhotoService photos,
                              IClientRules clientRules, ILogger<MediaUploadLog> log)
        {
            var client = new TableServiceClient(config.GetConnectionString("TableStorage")!);
            _table = client.GetTableClient("MediaUploads");
            _brand = brand;
            _references = references;
            _workflows = workflows;
            _photos = photos;
            _clientRules = clientRules;
            _log = log;
        }

        public async Task RecordAsync(VehiclePhotosDto dto, string source, IReadOnlyDictionary<string, string> caseMedia)
        {
            try
            {
                var photos = PhotoSlots
                    .Where(p => p.GetValue(dto) is IFormFile)
                    .Select(p => MandatoryMedia.DisplayName(p.Name))
                    .ToList();
                var customCount = dto.CustomImageFiles?.Count(f => f.Length > 0) ?? 0;
                var video = dto.VehicleVideo != null;
                if (photos.Count == 0 && customCount == 0 && !video) return;

                await EnsureTableAsync();
                var brand = _brand.Current;
                var now = DateTimeOffset.UtcNow;

                var clientName = await _photos.GetClientNameAsync(dto.ValuationId, dto.VehicleNumber, dto.ApplicantContact);
                var mediaOptional = _clientRules.IsMediaOptional(clientName);
                var missing = mediaOptional ? new List<string>() : MandatoryMedia.Missing(caseMedia, caseMedia);

                var row = await FindSameVisitAsync(brand, dto.ValuationId, now);
                if (row == null)
                {
                    var workflow = await _workflows.GetAsync(dto.ValuationId, dto.VehicleNumber, dto.ApplicantContact);
                    row = new MediaUploadEntity
                    {
                        PartitionKey = brand,
                        RowKey = $"{InvertedTicks(now)}_{dto.ValuationId}",
                        ValuationId = dto.ValuationId,
                        VehicleNumber = dto.VehicleNumber,
                        ApplicantContact = dto.ApplicantContact,
                        ReferenceNumber = await _references.EnsureAsync(dto.ValuationId, dto.VehicleNumber, dto.ApplicantContact),
                        ValuationType = workflow?.ValuationType,
                        Source = source,
                        FirstUploadedAt = now
                    };
                }

                row.Photos = JsonSerializer.Serialize(ReadList(row.Photos).Union(photos).ToList());
                row.CustomPhotoCount += customCount;
                row.VideoIncluded |= video;
                row.MediaOptional = mediaOptional;
                row.MissingMandatory = JsonSerializer.Serialize(missing);
                row.UploadedBy ??= string.IsNullOrWhiteSpace(dto.UploadedBy) ? null : dto.UploadedBy.Trim();
                // A row first written by an older APK learns it was the camera app.
                if (source == "CameraApp") row.Source = source;
                row.LastUploadedAt = now;

                await _table.UpsertEntityAsync(row, TableUpdateMode.Replace);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Could not record the media upload for case {ValuationId}", dto.ValuationId);
            }
        }

        public async Task<List<MediaUploadDto>> GetRecentAsync(int days)
        {
            await EnsureTableAsync();
            var since = DateTimeOffset.UtcNow.AddDays(-days);
            var filter = TableClient.CreateQueryFilter(
                $"PartitionKey eq {_brand.Current} and RowKey lt {InvertedTicks(since)}");

            var rows = new List<MediaUploadEntity>();
            await foreach (var row in _table.QueryAsync<MediaUploadEntity>(filter))
            {
                rows.Add(row);
                if (rows.Count >= MaxRows) break;
            }

            return rows
                .OrderByDescending(r => r.LastUploadedAt)
                .Select(r => new MediaUploadDto
                {
                    Id = r.RowKey,
                    ValuationId = r.ValuationId,
                    VehicleNumber = r.VehicleNumber,
                    ApplicantContact = r.ApplicantContact,
                    ReferenceNumber = r.ReferenceNumber,
                    ValuationType = r.ValuationType,
                    Source = r.Source,
                    UploadedBy = r.UploadedBy,
                    Photos = ReadList(r.Photos),
                    CustomPhotoCount = r.CustomPhotoCount,
                    VideoIncluded = r.VideoIncluded,
                    MediaOptional = r.MediaOptional,
                    MissingMandatory = ReadList(r.MissingMandatory),
                    FirstUploadedAt = r.FirstUploadedAt,
                    LastUploadedAt = r.LastUploadedAt
                })
                .ToList();
        }

        /// <summary>The case's row from the same visit, if it had an upload within the merge window.</summary>
        private async Task<MediaUploadEntity?> FindSameVisitAsync(string brand, string valuationId, DateTimeOffset now)
        {
            var filter = TableClient.CreateQueryFilter(
                $"PartitionKey eq {brand} and RowKey lt {InvertedTicks(now - MergeLookBack)} and ValuationId eq {valuationId} and LastUploadedAt ge {now - MergeWindow}");

            await foreach (var row in _table.QueryAsync<MediaUploadEntity>(filter))
                return row;
            return null;
        }

        private async Task EnsureTableAsync()
        {
            if (_tableReady) return;
            await _table.CreateIfNotExistsAsync();
            _tableReady = true;
        }

        /// <summary>Ticks counted down from the end of time, so newer rows sort first.</summary>
        private static string InvertedTicks(DateTimeOffset at) =>
            (DateTimeOffset.MaxValue.UtcTicks - at.UtcTicks).ToString("D19");

        private static List<string> ReadList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch (JsonException) { return new List<string>(); }
        }
    }
}
