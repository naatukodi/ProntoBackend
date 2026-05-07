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
    }

    // Stores metadata for a single photo (date + location text)
    public class PhotoMetadata
    {
        public string? CapturedDate { get; set; }
        public string? LocationText { get; set; }
    }

    // DTO received from client when updating photo metadata
    public class PhotoMetadataUpdateDto
    {
        public string? CapturedDate { get; set; }
        public string? LocationText { get; set; }
    }
}