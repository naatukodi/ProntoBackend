using System;

namespace Valuation.Api.Models
{
    // Used to parse the incoming JSON string from Angular
    public class CustomImageMetadataInput
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Date { get; set; }
        public string? Location { get; set; }
    }

    // Used to save the final data into your Cosmos DB document
    public class SavedCustomPhoto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public string? DateCaptured { get; set; }
        public string? Location { get; set; }

        // Annotation note burned onto the photo, and the clean pre-annotation
        // image it's redrawn from each time (so edits never stack).
        public string? AnnotationNote { get; set; }
        public string? OriginalPhotoUrl { get; set; }

        // Whether the case's company wordmark is burned onto the displayed photo.
        // The note and the logo are two layers over the same clean original, so both
        // are recorded rather than inferred: adding one has to redraw the other, or
        // stamping a logo would silently wipe the AVO's note.
        public bool LogoApplied { get; set; }
    }

    // Stores metadata for a single photo (date + location text)
    public class PhotoMetadata
    {
        public string? CapturedDate { get; set; }
        public string? LocationText { get; set; }

        // Annotation note burned onto the photo, and the clean pre-annotation
        // image it's redrawn from each time (so edits never stack).
        public string? AnnotationNote { get; set; }
        public string? OriginalPhotoUrl { get; set; }

        // Whether the case's company wordmark is burned onto the displayed photo.
        // The note and the logo are two layers over the same clean original, so both
        // are recorded rather than inferred: adding one has to redraw the other, or
        // stamping a logo would silently wipe the AVO's note.
        public bool LogoApplied { get; set; }
    }

    // DTO received from client when updating photo metadata
    public class PhotoMetadataUpdateDto
    {
        public string? CapturedDate { get; set; }
        public string? LocationText { get; set; }
    }

    // What the AVO page asks for when it stamps the case's company wordmark onto the
    // photos. Apply=false takes it back off, which costs nothing because every photo is
    // redrawn from its clean original rather than edited in place.
    public class BrandLogoRequest
    {
        public bool Apply { get; set; } = true;
    }

    // Outcome of stamping a case's photos, so the AVO page can refresh the thumbnails
    // it is already showing instead of reloading the whole case.
    public class BrandLogoResult
    {
        // "vehga" or "pronto", taken from the case itself rather than from the caller.
        public string Brand { get; set; } = string.Empty;

        public bool Applied { get; set; }

        // Photos actually redrawn. Photos already in the requested state are skipped,
        // so running this twice is not an error and costs nothing the second time.
        public int Changed { get; set; }

        public int Failed { get; set; }

        // New URL per photo key (fixed slot name, or custom photo id).
        public Dictionary<string, string> PhotoUrls { get; set; } = new();
    }
}