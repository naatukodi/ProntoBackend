// Models/MediaUploadEntity.cs
using System;
using System.Collections.Generic;
using Azure;
using Azure.Data.Tables;

namespace Valuation.Api.Models
{
    /// <summary>
    /// One upload of photos/video from a phone, as the portal announces it
    /// (MediaUploads table). The camera app sends its photos and then its video in two
    /// requests, so a second upload to the same case soon after is folded into the
    /// same row rather than announced twice.
    /// </summary>
    public class MediaUploadEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!; // brand: "vehga" / "pronto"
        public string RowKey { get; set; } = default!;       // "{inverted ticks of the first upload:D19}_{valuationId}", so a query reads newest first

        public string ValuationId { get; set; } = default!;
        public string VehicleNumber { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;
        public string? ReferenceNumber { get; set; }
        public string? ValuationType { get; set; }

        public string Source { get; set; } = default!;       // "CameraApp", or "MobileApp" for a phone that did not say
        public string? UploadedBy { get; set; }              // the camera app's agency login

        public string Photos { get; set; } = "[]";           // JSON: on-screen names of the photos received
        public int CustomPhotoCount { get; set; }
        public bool VideoIncluded { get; set; }

        public bool MediaOptional { get; set; }              // a client that needs no photos (ClientRules)
        public string MissingMandatory { get; set; } = "[]"; // JSON: mandatory items the case still lacks after this upload

        public DateTimeOffset FirstUploadedAt { get; set; }
        public DateTimeOffset LastUploadedAt { get; set; }

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    /// <summary>What GET /api/media-uploads returns for one row.</summary>
    public class MediaUploadDto
    {
        public string Id { get; set; } = default!;
        public string ValuationId { get; set; } = default!;
        public string VehicleNumber { get; set; } = default!;
        public string ApplicantContact { get; set; } = default!;
        public string? ReferenceNumber { get; set; }
        public string? ValuationType { get; set; }
        public string Source { get; set; } = default!;
        public string? UploadedBy { get; set; }
        public List<string> Photos { get; set; } = new();
        public int CustomPhotoCount { get; set; }
        public bool VideoIncluded { get; set; }
        public bool MediaOptional { get; set; }
        public List<string> MissingMandatory { get; set; } = new();
        public DateTimeOffset FirstUploadedAt { get; set; }
        public DateTimeOffset LastUploadedAt { get; set; }
    }
}
