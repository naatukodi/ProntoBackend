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
        private readonly string _googleApiKey;
        private readonly string _googleCseId;
        private const int MaxRetries = 5;

        // We now inject IConfiguration so we can read the keys from appsettings.json (or environment variables).
        public ChatGptRepository(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _openAiClient = httpClientFactory.CreateClient("OpenAI");
            _googleCseClient = httpClientFactory.CreateClient("GoogleCSE");

            // Read Google credentials from configuration
            _googleApiKey = configuration["GoogleCSE:ApiKey"];
            _googleCseId = configuration["GoogleCSE:CseId"];

            if (string.IsNullOrWhiteSpace(_googleApiKey))
                throw new InvalidOperationException(
                    "Missing Google API Key. Please set `GoogleCSE:ApiKey` in appsettings.json or as an environment variable.");
            if (string.IsNullOrWhiteSpace(_googleCseId))
                throw new InvalidOperationException(
                    "Missing Google CSE ID. Please set `GoogleCSE:CseId` in appsettings.json or as an environment variable.");
        }

        public async Task<string> GetVehicleValuationResponseAsync(VehicleDetailsAIDto details)
        {
            // 1) Query Google Custom Search to get top-3 snippets
            var searchResults = await GetTopGoogleSnippetsAsync(details);

            // 2) Build the “chat” payload including those snippets
            var systemMessage = new
            {
                role = "system",
                content = "You are a vehicle-valuation assistant. " +
                          "When given a set of web-search snippets about a vehicle, you will return EXACTLY three INR ranges: low, mid, and high."
            };

            // Build a “user” message that embeds the snippets
            var userContent = new StringBuilder();
            userContent.AppendLine("Here are the top Google search results for this vehicle:");
            userContent.AppendLine();

            for (int i = 0; i < searchResults.Count; i++)
            {
                var item = searchResults[i];
                userContent.AppendLine($"{i + 1}. Title: {item.Title}");
                userContent.AppendLine($"   Snippet: {item.Snippet}");
                userContent.AppendLine($"   URL: {item.Link}");
                userContent.AppendLine();
            }

            userContent.AppendLine(
                "Based on these results, please provide:\n" +
                "1. A fair market INR price range for low, mid, and high (e.g., “₹7.5 L – ₹8 L” for low, “₹8 L – ₹8.5 L” for mid, etc.).\n" +
                "2. A brief rationale (1–2 sentences) explaining each range."
            );

            var userMessage = new
            {
                role = "user",
                content = userContent.ToString()
            };

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[] { systemMessage, userMessage },
                temperature = 0.2,
                max_tokens = 200
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);

            // 3) Call OpenAI with retry-on-429 logic
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _openAiClient.PostAsync("/v1/chat/completions", content);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    // Extract Retry-After header if present
                    TimeSpan delay;
                    if (response.Headers.RetryAfter != null)
                    {
                        if (response.Headers.RetryAfter.Delta.HasValue)
                        {
                            delay = response.Headers.RetryAfter.Delta.Value;
                        }
                        else if (response.Headers.RetryAfter.Date.HasValue)
                        {
                            var retryDate = response.Headers.RetryAfter.Date.Value;
                            var now = DateTimeOffset.UtcNow;
                            delay = retryDate > now
                                ? retryDate - now
                                : TimeSpan.FromSeconds(1);
                        }
                        else
                        {
                            delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                        }
                    }
                    else
                    {
                        delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    }

                    if (attempt == MaxRetries)
                        response.EnsureSuccessStatusCode(); // Throw on final attempt

                    await Task.Delay(delay);
                    continue;
                }

                // For 4xx/5xx (non-429), this will throw
                response.EnsureSuccessStatusCode();

                // Parse the successful response
                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var chatContent = doc
                    .RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                return chatContent.Trim();
            }

            throw new InvalidOperationException("Exceeded maximum OpenAI retry attempts.");
        }

        /// <summary>
        /// Calls Google Custom Search JSON API and returns up to top 3 results (title, snippet, link).
        /// </summary>
        private async Task<List<GoogleResult>> GetTopGoogleSnippetsAsync(VehicleDetailsAIDto details)
        {
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
