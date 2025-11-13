// Models/ValidatePhotosResponse.cs

namespace Valuation.Api.Models
{
    /// <summary>
    /// Response model for photo validation endpoint
    /// </summary>
    public class ValidatePhotosResponse
    {
        /// <summary>
        /// True if all 18 mandatory photos are uploaded
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// List of display names for missing mandatory photos
        /// </summary>
        public List<string> MissingPhotos { get; set; } = new();
    }
}
