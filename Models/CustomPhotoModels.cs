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
}