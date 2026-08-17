using System.Text;
using System.Text.Json;
using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    /// <summary>
    /// Google Gemini. Currently wired to nothing: the "Instant AI Value" screen it was
    /// written for now runs on ChatGptRepository (OpenAI) instead, because the Gemini key
    /// was rejected in production. Kept intact so the screen can be pointed back here
    /// once a working key is in place.
    ///
    /// Separate from ChatGptRepository by design: different vendor account, different
    /// key. Nothing here touches OpenAI, and nothing falls back between the two.
    /// </summary>
    public class GeminiRepository : IGeminiRepository
    {
        private readonly HttpClient _geminiClient;
        private readonly string? _apiKey;
        private readonly string _model;

        public GeminiRepository(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _geminiClient = httpClientFactory.CreateClient("Gemini");

            // Optional by design: a missing key surfaces as "not configured" from the
            // endpoint rather than stopping the app from starting, so the rest of the
            // API still runs without Gemini.
            _apiKey = configuration["Gemini:ApiKey"];

            // A rolling alias by default — pinned versions get retired and start
            // returning 404 for newly created API keys.
            _model = configuration["Gemini:Model"] ?? "gemini-flash-latest";
        }

        private const string MarketValueSystemPrompt =
            "You are an expert vehicle valuer for the Indian market. Your goal is to provide a realistic, " +
            "single-paragraph market value assessment for a used vehicle. State the estimated price range " +
            "clearly in Rupees. Do not use markdown or bullet points. Provide the answer in a concise, " +
            "professional paragraph.";

        /// <inheritdoc />
        public async Task<string> GetMarketValueAsync(MarketValueRequestDto d)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException(
                    "Gemini is not configured. Set `Gemini:ApiKey` in appsettings.Development.json " +
                    "or the `Gemini__ApiKey` environment variable.");

            var userPrompt =
                "Please provide the estimated market value for the following vehicle:\n" +
                $"- Vehicle Type: {d.VehicleType}\n" +
                $"- Make: {d.Make}\n" +
                $"- Model: {d.Model}\n" +
                $"- Manufacturing Year: {d.Year}\n" +
                $"- Kilometers Driven: {d.Kms} km\n" +
                $"- Location: {d.Location}, India";

            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = userPrompt } } }
                },
                systemInstruction = new
                {
                    parts = new[] { new { text = MarketValueSystemPrompt } }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // The key travels in a header, not the query string, so it stays out of
            // request logs and proxy traces.
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"v1beta/models/{_model}:generateContent")
            {
                Content = content
            };
            request.Headers.Add("x-goog-api-key", _apiKey);

            var resp = await _geminiClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Gemini returned {(int)resp.StatusCode}: {body}", null, resp.StatusCode);

            using var doc = JsonDocument.Parse(body);

            // A response can be well-formed yet carry no text — e.g. when the model
            // stops on a safety filter. Treat that as an empty result, not a crash.
            if (doc.RootElement.TryGetProperty("candidates", out var candidates)
                && candidates.GetArrayLength() > 0
                && candidates[0].TryGetProperty("content", out var contentEl)
                && contentEl.TryGetProperty("parts", out var parts)
                && parts.GetArrayLength() > 0
                && parts[0].TryGetProperty("text", out var textEl))
            {
                return textEl.GetString()?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
