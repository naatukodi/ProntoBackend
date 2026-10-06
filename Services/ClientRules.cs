namespace Valuation.Api.Services
{
    /// <summary>
    /// Rules that depend on who the client (the stakeholder's name) is.
    ///
    /// Some clients send only a few photos, and sometimes no video at all, so their cases
    /// cannot be held at AVO until the standard set is uploaded: nothing is mandatory on
    /// them. Which clients those are is an App Service setting, not code, so adding one is
    /// a settings change rather than a release:
    ///
    ///   ClientRules__MediaOptionalClients = IndusInd Bank;Sakthi Finance Limited
    ///
    /// Names are matched whole, ignoring case and extra spaces, as the stakeholder
    /// dropdown spells them. Deliberately not a substring match: "Bank of India" must not
    /// catch "State Bank of India (SBI)". Unset means no client is exempt.
    ///
    /// The PDF service reads the same setting to leave the cover's video button off these
    /// cases when there is no video, so both apps must carry the same value.
    /// </summary>
    public interface IClientRules
    {
        /// <summary>True when no photo or video is required on this client's cases.</summary>
        bool IsMediaOptional(string? clientName);
    }

    public class ClientRules : IClientRules
    {
        public const string MediaOptionalClientsKey = "ClientRules:MediaOptionalClients";

        private readonly HashSet<string> _mediaOptional;

        // Read once: App Service restarts the app whenever a setting changes.
        public ClientRules(IConfiguration configuration)
        {
            _mediaOptional = new HashSet<string>(
                (configuration[MediaOptionalClientsKey] ?? "")
                    .Split(';')
                    .Select(Normalize)
                    .Where(n => n.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool IsMediaOptional(string? clientName) =>
            _mediaOptional.Contains(Normalize(clientName));

        private static string Normalize(string? name) =>
            string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
