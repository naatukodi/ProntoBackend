// Models/ValidatePhotosResponse.cs

namespace Valuation.Api.Models
{
    /// <summary>
    /// Response model for photo validation endpoint
    /// </summary>
    public class ValidatePhotosResponse
    {
        /// <summary>
        /// True if all 16 mandatory photos and the vehicle video are uploaded,
        /// or always when the client needs none (MediaOptional)
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// List of display names for missing mandatory photos
        /// </summary>
        public List<string> MissingPhotos { get; set; } = new();

        /// <summary>
        /// True when the case's client needs no photos or video at all
        /// (ClientRules__MediaOptionalClients), so nothing on the photo page is required
        /// </summary>
        public bool MediaOptional { get; set; }
    }
}
