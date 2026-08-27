using Microsoft.AspNetCore.Http;

namespace Valuation.Api.Services
{
    /// <summary>
    /// The company a request belongs to. Vehga Inspections and Pronto Moto share this
    /// backend, but their cases must never mix, so every case is stamped with a brand at
    /// creation and every case listing is filtered by one.
    ///
    /// SECURITY NOTE: the brand currently arrives in the X-Brand header, which the client
    /// controls. That is deliberate for now and no weaker than the rest of the API —
    /// Program.cs calls UseAuthentication() without registering a scheme and no controller
    /// carries [Authorize], so nothing is authenticated today. Once Firebase token
    /// validation is added, resolve the brand from the caller's UserEntity.AllowedBrands
    /// instead and reject a header that asks for a brand the user is not entitled to.
    /// Everything else can stay as it is — that is why this lives behind one interface.
    /// </summary>
    public interface IBrandContext
    {
        /// <summary>Brand for the current request; "vehga" when unspecified.</summary>
        string Current { get; }

        /// <summary>True when the caller did not name a brand, so a listing should not be
        /// narrowed. Lets older clients (mobile apps, camera app) keep working unchanged.</summary>
        bool IsUnscoped { get; }
    }

    public class BrandContext : IBrandContext
    {
        public const string HeaderName = "X-Brand";
        public const string Vehga  = "vehga";
        public const string Pronto = "pronto";

        private static readonly string[] Known = { Vehga, Pronto };

        private readonly string? _requested;

        public BrandContext(IHttpContextAccessor accessor)
        {
            var raw = accessor.HttpContext?.Request.Headers[HeaderName].ToString();
            _requested = Normalise(raw);
        }

        public string Current => _requested ?? Vehga;
        public bool IsUnscoped => _requested is null;

        /// <summary>Maps any input to a known brand, or null when it names none.
        /// Null and unrecognised values both mean Vehga: every document written before
        /// multi-brand belongs to Vehga, and an unknown brand must not silently create
        /// a third bucket that no listing would ever show.</summary>
        public static string? Normalise(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var v = raw.Trim().ToLowerInvariant();
            return Array.IndexOf(Known, v) >= 0 ? v : Vehga;
        }

        /// <summary>Brand a stored document belongs to. Null/blank is Vehga by definition.</summary>
        public static string Of(string? stored) => Normalise(stored) ?? Vehga;

        /// <summary>Whether a stored document belongs to the given brand.</summary>
        public static bool Matches(string? stored, string brand) =>
            string.Equals(Of(stored), brand, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Cosmos predicate restricting a case listing to one brand. Documents written
        /// before multi-brand have no Brand property at all, so Vehga has to match
        /// missing/null/empty as well as the literal value — otherwise switching this on
        /// would make every historical case vanish from Vehga's dashboard.
        /// Pair with <see cref="SqlParam"/>.
        /// </summary>
        public const string SqlFilter =
            "(LOWER(c.Brand) = @brand OR (@brand = 'vehga' AND (NOT IS_DEFINED(c.Brand) OR IS_NULL(c.Brand) OR c.Brand = '')))";

        public const string SqlParam = "@brand";
    }
}
