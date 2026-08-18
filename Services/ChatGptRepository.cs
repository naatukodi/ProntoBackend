using System.Net;
using System.Text;
using System.Text.Json;
using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    public class ChatGptRepository : IChatGptRepository
    {
        private readonly HttpClient _openAiClient;
        private readonly HttpClient _googleCseClient;
        private readonly string? _openAiApiKey;
        private readonly string? _googleApiKey;
        private readonly string? _googleCseId;
        private const int MaxRetries = 5;

        // We now inject IConfiguration so we can read the keys from appsettings.json (or environment variables).
        public ChatGptRepository(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _openAiClient = httpClientFactory.CreateClient("OpenAI");
            _googleCseClient = httpClientFactory.CreateClient("GoogleCSE");

            // The named client already carries the key as its Authorization header;
            // this copy only exists so a missing key can be reported as "not
            // configured" instead of coming back from OpenAI as a bare 401.
            _openAiApiKey = configuration["OpenAI:ApiKey"];

            // Read Google credentials from configuration. Missing values are checked
            // in the CSE call itself rather than here — every other feature on this
            // repository (valuation, market value) works without them, and failing
            // in the constructor would take those down too.
            _googleApiKey = configuration["GoogleCSE:ApiKey"];
            _googleCseId = configuration["GoogleCSE:CseId"];
        }

        private const string Model = "gpt-4o-mini";

        public async Task<string> GetVehicleValuationAsync(VehicleDetailsAIDto d)
        {
            // 1) Build system prompt
            var system = new
            {
                role = "system",
                content =
                    "You are a vehicle-valuation assistant for the Indian market. " +
                    "Given vehicle details, return EXACTLY three INR price ranges: low, mid, and high, " +
                    "each formatted like “₹7.5 L – ₹8 L”, plus a 1–2 sentence rationale for each."
            };

            // 2) Build a single user message embedding all fields
            var userSb = new StringBuilder();
            userSb.AppendLine("Here are the vehicle details:");
            userSb.AppendLine($"- RegistrationNumber: {d.RegistrationNumber}");
            userSb.AppendLine($"- Make: {d.Make}");
            userSb.AppendLine($"- Model: {d.Model}");
            userSb.AppendLine($"- YearOfMfg: {d.YearOfMfg}");
            userSb.AppendLine($"- Colour: {d.Colour}");
            userSb.AppendLine($"- Fuel: {d.Fuel}");
            userSb.AppendLine($"- EngineCC: {d.EngineCC}");
            userSb.AppendLine($"- IDV: {d.IDV}");
            userSb.AppendLine($"- DateOfRegistration: {d.DateOfRegistration:yyyy-MM-dd}");
            userSb.AppendLine($"- City: {d.City}");
            userSb.AppendLine($"- Odometer: {d.Odometer}");
            userSb.AppendLine();
            userSb.AppendLine("Please deliver:");

            var user = new
            {
                role = "user",
                content = userSb.ToString()
            };

            // 3) Assemble request
            var payload = new
            {
                model = Model,
                messages = new[] { system, user },
                temperature = 0.2,
                max_tokens = 200   // adjust upward if you need longer rationale
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _openAiClient.PostAsync("/v1/chat/completions", content);
            resp.EnsureSuccessStatusCode();

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString()!
                      .Trim();
        }


        private const string MarketValueSystemPrompt =
            "You are an expert vehicle valuer for the Indian market. Your goal is to provide a realistic, " +
            "single-paragraph market value assessment for a used vehicle. State the estimated price range " +
            "clearly in Rupees. Do not use markdown or bullet points. Provide the answer in a concise, " +
            "professional paragraph.";

        /// <inheritdoc />
        public async Task<string> GetMarketValueAsync(MarketValueRequestDto d)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
                throw new InvalidOperationException(
                    "OpenAI is not configured. Set `OpenAI:ApiKey` in appsettings.Development.json " +
                    "or the `OpenAI__ApiKey` environment variable.");

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
                model = Model,
                messages = new[]
                {
                    new { role = "system", content = MarketValueSystemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2,
                max_tokens = 400
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 30s ceiling, matching what this screen has always enforced. It is scoped
            // to this call rather than set on the shared "OpenAI" client so the QC
            // valuation path keeps its own (longer) default timeout.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var resp = await _openAiClient.PostAsync("/v1/chat/completions", content, cts.Token);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"OpenAI returned {(int)resp.StatusCode}: {body}", null, resp.StatusCode);

            using var doc = JsonDocument.Parse(body);

            // A response can be well-formed yet carry no text — e.g. when the model
            // stops on a content filter. Treat that as an empty result, not a crash.
            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var textEl))
            {
                return textEl.GetString()?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Calls Google Custom Search JSON API and returns up to top 3 results (title, snippet, link).
        /// </summary>
        private async Task<List<GoogleResult>> GetTopGoogleSnippetsAsync(VehicleDetailsAIDto details)
        {
            if (string.IsNullOrWhiteSpace(_googleApiKey))
                throw new InvalidOperationException(
                    "Missing Google API Key. Please set `GoogleCSE:ApiKey` in appsettings.json or as an environment variable.");
            if (string.IsNullOrWhiteSpace(_googleCseId))
                throw new InvalidOperationException(
                    "Missing Google CSE ID. Please set `GoogleCSE:CseId` in appsettings.json or as an environment variable.");

            // 1) Build a query string from VehicleDetailsAIDto
            //    e.g. "2018 Honda City Mumbai resale value"
            var query = $"{details.YearOfMfg} {details.Model} {details.Make} {details.Odometer}  india resale value";

            // 2) Call Google CSE endpoint:
            //    GET /customsearch/v1?key={API_KEY}&cx={CSE_ID}&q={query}&num=3
            var requestUri = $"customsearch/v1?key={WebUtility.UrlEncode(_googleApiKey)}" +
                             $"&cx={WebUtility.UrlEncode(_googleCseId)}" +
                             $"&q={WebUtility.UrlEncode(query)}" +
                             $"&num=3";

            var response = await _googleCseClient.GetAsync(requestUri);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var results = new List<GoogleResult>();
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var title = item.GetProperty("title").GetString() ?? string.Empty;
                    var snippet = item.GetProperty("snippet").GetString() ?? string.Empty;
                    var link = item.GetProperty("link").GetString() ?? string.Empty;

                    results.Add(new GoogleResult
                    {
                        Title = title,
                        Snippet = snippet,
                        Link = link
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Simple DTO for holding Google CSE output
        /// </summary>
        private class GoogleResult
        {
            public string Title { get; set; } = string.Empty;
            public string Snippet { get; set; } = string.Empty;
            public string Link { get; set; } = string.Empty;
        }
    }
}
