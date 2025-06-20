// Models/Document.cs
namespace Valuation.Api.Models
{
    public class Document
    {
        public string Type { get; set; } = default!;
        public string FilePath { get; set; } = default!;
        public DateTime UploadedAt { get; set; }
    }
}