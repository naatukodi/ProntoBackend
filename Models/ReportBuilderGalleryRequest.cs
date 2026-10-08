namespace Valuation.Api.Models
{
    /// <summary>
    /// The photos of a report made in the standalone report builder, for the web gallery
    /// its cover's IMAGE LINK opens. The builder holds them only in the browser, so they
    /// come up with the request rather than as links to photos already stored.
    /// </summary>
    public class ReportBuilderGalleryRequest
    {
        /// <summary>Printed in the gallery's header. Falls back to the reference number.</summary>
        public string? RegistrationNumber { get; set; }

        /// <summary>In the order the gallery shows them.</summary>
        public List<ReportBuilderGalleryPhoto>? Photos { get; set; }
    }

    public class ReportBuilderGalleryPhoto
    {
        /// <summary>The caption, e.g. FRONT VIEW.</summary>
        public string? Label { get; set; }

        /// <summary>The image as a data URL (data:image/jpeg;base64,…) or bare base64.</summary>
        public string? Data { get; set; }
    }
}
