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
        /// Reads a case's inspection photos. See IChatGptRepository for why this
        /// returns readings rather than verdicts.
        /// </summary>
        public async Task<QcAiVisionResult?> ReadInspectionPhotosAsync(
            IReadOnlyDictionary<string, string> photos,
            IReadOnlySet<string> closeUpKeys,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
                throw new InvalidOperationException("OpenAI API key is not configured.");
            if (photos.Count == 0)
                return null;

            var content = new List<object>
            {
                new { type = "text", text = BuildVisionPrompt(photos.Keys) }
            };

            foreach (var (key, url) in photos)
            {
                // Label each image so the model can attribute stamps to the right slot;
                // the API sends images in order but gives them no names of their own.
                content.Add(new { type = "text", text = $"[photo: {key}]" });
                content.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url,
                        // "high" tiles the image and costs proportionally more, so it is
                        // spent only where fine characters must be read. Everything else
                        // is needed for the stamp and overall lighting, which survive
                        // "low" — that keeps cost flat as the photo count grows.
                        detail = closeUpKeys.Contains(key) ? "high" : "low"
                    }
                });
            }

            var payload = new
            {
                model = Model,
                temperature = 0,          // transcription, not creativity
                max_tokens = 2000,
                messages = new[] { new { role = "user", content } },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "qc_photo_reading",
                        strict = true,
                        schema = VisionSchema()
                    }
                }
            };

            using var body = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(120));   // 20 images is not a fast call

            var resp = await _openAiClient.PostAsync("/v1/chat/completions", body, linked.Token);
            var raw = await resp.Content.ReadAsStringAsync(linked.Token);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"OpenAI returned {(int)resp.StatusCode}: {raw}", null, resp.StatusCode);

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(text)) return null;

            var result = JsonSerializer.Deserialize<QcAiVisionResult>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Token usage is logged so the real per-case cost is measurable instead of
            // estimated — the first live case settles it better than any arithmetic.
            if (result != null && doc.RootElement.TryGetProperty("usage", out var usage))
            {
                result.Observations.Add(
                    $"[usage] prompt={usage.GetProperty("prompt_tokens").GetInt32()} " +
                    $"completion={usage.GetProperty("completion_tokens").GetInt32()} " +
                    $"images={photos.Count} closeUp={closeUpKeys.Count(k => photos.ContainsKey(k))}");
            }

            return result;
        }

        private static string BuildVisionPrompt(IEnumerable<string> photoKeys)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are reading photographs from a vehicle inspection in India.");
            sb.AppendLine("Report ONLY what is legible in the images. Do not infer, complete or guess.");
            sb.AppendLine("If something is unreadable, blurred or absent, return null for it.");
            sb.AppendLine("Returning null is always better than returning a plausible-looking value.");
            sb.AppendLine();
            sb.AppendLine("Read, where visible:");
            sb.AppendLine("- registrationPlate: the number plate characters (e.g. TS15UD1953), no spaces.");
            sb.AppendLine("- chassisNumber: characters on the chassis/VIN plate.");
            sb.AppendLine("- chassisStencil: characters in the stencil or punch imprint on the metal.");
            sb.AppendLine("  This is usually a pencil rubbing on paper and is very often photographed");
            sb.AppendLine("  sideways or upside down — work out the orientation first and read it that");
            sb.AppendLine("  way. Rubbings are faint and easy to get wrong: if you cannot make out every");
            sb.AppendLine("  character with confidence, return null rather than a partial reading.");
            sb.AppendLine("  Read chassisNumber and chassisStencil independently — do NOT copy one to");
            sb.AppendLine("  the other. They are compared against each other to detect tampering.");
            sb.AppendLine("- vinPlate: the VIN, usually the line labelled VIN or Chassis No. on the");
            sb.AppendLine("  manufacturer's data plate. On most Indian vehicles this is the same plate");
            sb.AppendLine("  as chassisNumber — fill both from it rather than leaving vinPlate null.");
            sb.AppendLine("  A VIN never contains the letters I, O or Q, so read those shapes as 1 and 0.");
            sb.AppendLine("  A zero is often struck through; that slash is not a character.");
            sb.AppendLine("- odometerKm: the lifetime odometer total in km, digits only.");
            sb.AppendLine("  A cluster shows several numbers. The total is the longest run of digits and");
            sb.AppendLine("  has no decimal point. Anything with a decimal point is a trip meter or a fuel");
            sb.AppendLine("  average, a value like 01:53 is the clock, and a bare 0 or 0.0 is never the");
            sb.AppendLine("  total on a vehicle being valued. If the total is glared out, dirty or partly");
            sb.AppendLine("  hidden, return null — do NOT substitute another number from the display.");
            sb.AppendLine("  Read the total right to the last digit. A used vehicle reads five or six");
            sb.AppendLine("  digits, so 156 where the dial shows 156361 is a truncated read, not a low");
            sb.AppendLine("  reading. If glare hides the trailing digits, return null for the whole value");
            sb.AppendLine("  rather than the part you could make out.");
            sb.AppendLine();
            sb.AppendLine("Judge:");
            sb.AppendLine("- daylight: 'pass' if photos are bright and clear enough to inspect, else 'fail'.");
            sb.AppendLine("- plateLegible: 'pass' if the number plate is unobstructed and readable in both");
            sb.AppendLine("  the front and rear shots, else 'fail'.");
            sb.AppendLine("- chassisPunch: 'original', 'repunched' or 'tampered' from the stamped characters —");
            sb.AppendLine("  look for uneven spacing, mixed fonts, grinding or overstamping. Use null when");
            sb.AppendLine("  no punch is visible.");
            sb.AppendLine();
            sb.AppendLine("Many photos carry a burned-in camera overlay showing place, latitude/longitude");
            sb.AppendLine("and a capture date/time. For every photo that has one, return a photoStamps entry");
            sb.AppendLine("with its photoKey and whatever the overlay states. Omit photos with no overlay.");
            sb.AppendLine("Transcribe the overlay exactly; never estimate coordinates from scenery.");
            sb.AppendLine("The date and the time are often on separate lines, and there may be both a");
            sb.AppendLine("local and a GMT time — combine the date with the LOCAL time into capturedAt,");
            sb.AppendLine("keeping the overlay's own wording (e.g. \"Friday, 14.08.2026 06:02:01 PM\").");
            sb.AppendLine();
            sb.AppendLine("Each image is preceded by a [photo: KEY] label. Use those keys verbatim.");
            sb.AppendLine("Photo keys in this case: " + string.Join(", ", photoKeys));
            return sb.ToString();
        }

        /// <summary>
        /// Strict structured-output schema. Strict mode requires every property to be
        /// listed in "required" and every object to set additionalProperties:false, so
        /// nullable fields are typed as ["string","null"] rather than being optional.
        /// An open dictionary cannot be expressed here at all.
        /// </summary>
        private static object VisionSchema()
        {
            static object Nullable(string t) => new { type = new[] { t, "null" } };

            return new
            {
                type = "object",
                additionalProperties = false,
                required = new[]
                {
                    "registrationPlate", "chassisNumber", "chassisStencil", "vinPlate",
                    "odometerKm", "daylight", "plateLegible", "chassisPunch",
                    "photoStamps", "observations"
                },
                properties = new Dictionary<string, object>
                {
                    ["registrationPlate"] = Nullable("string"),
                    ["chassisNumber"]     = Nullable("string"),
                    ["chassisStencil"]    = Nullable("string"),
                    ["vinPlate"]          = Nullable("string"),
                    ["odometerKm"]        = Nullable("integer"),
                    ["daylight"]          = new { type = new[] { "string", "null" }, @enum = new object?[] { "pass", "fail", null } },
                    ["plateLegible"]      = new { type = new[] { "string", "null" }, @enum = new object?[] { "pass", "fail", null } },
                    ["chassisPunch"]      = new { type = new[] { "string", "null" }, @enum = new object?[] { "original", "repunched", "tampered", null } },
                    ["photoStamps"] = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "photoKey", "latitude", "longitude", "placeName", "capturedAt" },
                            properties = new Dictionary<string, object>
                            {
                                ["photoKey"]   = Nullable("string"),
                                ["latitude"]   = Nullable("number"),
                                ["longitude"]  = Nullable("number"),
                                ["placeName"]  = Nullable("string"),
                                ["capturedAt"] = Nullable("string")
                            }
                        }
                    },
                    ["observations"] = new { type = "array", items = new { type = "string" } }
                }
            };
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
