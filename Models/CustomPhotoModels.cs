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
    }

    // DTO received from client when updating photo metadata
    public class PhotoMetadataUpdateDto
    {
        public string? CapturedDate { get; set; }
        public string? LocationText { get; set; }
    }
}