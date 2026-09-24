using System.Net;
using System.Text;
using System.Text.Json;
using Valuation.Api.Models;

namespace Valuation.Api.Repositories
{
    public class ChatGptRepository : IChatGptRepository
    {
        private readonly HttpClient _openAiClient;
        private readonly string? _openAiApiKey;
        private readonly ILogger<ChatGptRepository> _logger;
        private const int MaxRetries = 5;

        // We now inject IConfiguration so we can read the keys from appsettings.json (or environment variables).
        public ChatGptRepository(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ChatGptRepository> logger)
        {
            _logger = logger;
            _openAiClient = httpClientFactory.CreateClient("OpenAI");

            // The named client already carries the key as its Authorization header;
            // this copy only exists so a missing key can be reported as "not
            // configured" instead of coming back from OpenAI as a bare 401.
            _openAiApiKey = configuration["OpenAI:ApiKey"];
        }

        private const string Model = "gpt-4o-mini";

        /// <inheritdoc />
        public string ModelName => Model;

        public async Task<VehicleValuationAi?> GetVehicleValuationAsync(VehicleDetailsAIDto d)
        {
            // 1) Build system prompt.
            //    Whole rupees as integers, deliberately: asking for "₹7.5 L" put a
            //    lakh/crore abbreviation between the model and the number, which then had
            //    to be parsed back. Structured output removes the parsing step entirely.
            var system = new
            {
                role = "system",
                content =
                    "You are a vehicle-valuation assistant for the Indian market. " +
                    "Given vehicle details, return three resale price points for the Indian " +
                    "used-vehicle market: low, mid and high. " +
                    "Give each as a whole number of rupees, e.g. 750000 — never a lakh or " +
                    "crore abbreviation, never a range, never a currency symbol. " +
                    "low <= mid <= high. Add a short rationale. " +
                    "If the details are too thin to value the vehicle, return nulls rather " +
                    "than a guess."
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

            // 3) Assemble request.
            //    temperature 0 because this is a lookup, not a composition, and 200 tokens
            //    used to truncate the answer mid-sentence — which the old regex then read
            //    as "no ranges found" and stored as three zeros.
            var payload = new
            {
                model = Model,
                messages = new[] { system, user },
                temperature = 0,
                max_tokens = 800,
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "vehicle_valuation",
                        strict = true,
                        schema = ValuationSchema()
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _openAiClient.PostAsync("/v1/chat/completions", content);
            resp.EnsureSuccessStatusCode();

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var raw = doc.RootElement
                         .GetProperty("choices")[0]
                         .GetProperty("message")
                         .GetProperty("content")
                         .GetString();

            if (string.IsNullOrWhiteSpace(raw)) return null;

            var parsed = JsonSerializer.Deserialize<VehicleValuationAi>(
                raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed is null) return null;

            parsed.Raw = raw;
            return parsed;
        }

        /// <summary>
        /// How long to wait before trying the reader again.
        ///
        /// Honours the server's own Retry-After where it sends one, otherwise backs off
        /// 2s, 5s. Capped so a stuck reader cannot hold a QC page open indefinitely.
        /// </summary>
        private static TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter)
        {
            if (retryAfter is { TotalSeconds: > 0 and <= 30 }) return retryAfter.Value;
            return TimeSpan.FromSeconds(attempt == 1 ? 2 : 5);
        }

        /// <summary>
        /// Strict-mode schema for the valuation. Same rules as VisionSchema: every
        /// property listed in `required`, `additionalProperties` false, and nullables
        /// typed ["number","null"] rather than being left out.
        /// </summary>
        private static object ValuationSchema() => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "lowRange", "midRange", "highRange", "rationale" },
            properties = new Dictionary<string, object>
            {
                ["lowRange"]  = new { type = new[] { "number", "null" } },
                ["midRange"]  = new { type = new[] { "number", "null" } },
                ["highRange"] = new { type = new[] { "number", "null" } },
                ["rationale"] = new { type = new[] { "string", "null" } }
            }
        };


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
        /// PASS 1 — identity and provenance. See IChatGptRepository.
        /// </summary>
        public Task<QcAiVisionResult?> ReadInspectionPhotosAsync(
            IReadOnlyDictionary<string, string> photos,
            IReadOnlySet<string> closeUpKeys,
            CancellationToken ct = default) =>
            ReadPhotosAsync("identity", BuildIdentityPrompt(photos.Keys),
                            "qc_photo_reading", IdentitySchema(), photos, closeUpKeys, ct);

        /// <summary>
        /// PASS 2 — condition, damage, missing parts, tyres, engine bay.
        /// See IChatGptRepository for why this is a second request and not more prompt.
        /// </summary>
        public Task<QcAiVisionResult?> ReadVehicleConditionAsync(
            IReadOnlyDictionary<string, string> photos,
            IReadOnlySet<string> closeUpKeys,
            CancellationToken ct = default) =>
            ReadPhotosAsync("condition", BuildConditionPrompt(photos.Keys),
                            "qc_condition_reading", ConditionSchema(), photos, closeUpKeys, ct);

        /// <summary>
        /// PASS 3 — photo provenance: the place, coordinates and capture time printed
        /// into each image by the camera app. See IChatGptRepository.
        /// </summary>
        public Task<QcAiVisionResult?> ReadPhotoProvenanceAsync(
            IReadOnlyDictionary<string, string> photos,
            CancellationToken ct = default) =>
            ReadPhotosAsync("provenance", BuildProvenancePrompt(photos.Keys),
                            "qc_photo_provenance", ProvenanceSchema(), photos,
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase), ct);

        /// <summary>
        /// Shared transport for all three passes: labels each image, applies the per-photo
        /// detail map, and carries the retry rules. Only the prompt and the schema differ.
        /// </summary>
        private async Task<QcAiVisionResult?> ReadPhotosAsync(
            string pass, string prompt, string schemaName, object schema,
            IReadOnlyDictionary<string, string> photos,
            IReadOnlySet<string> closeUpKeys,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
                throw new InvalidOperationException("OpenAI API key is not configured.");
            if (photos.Count == 0)
                return null;

            var content = new List<object>
            {
                new { type = "text", text = prompt }
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
                        name = schemaName,
                        strict = true,
                        schema
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);

            // A rate limit or a slow image fetch is a moment's delay, not a verdict.
            // Without this a burst of cases left reviewers looking at "Photo reading
            // failed" on a case that would have read perfectly a few seconds later.
            // A 400 is not retried: a dead or unreachable image will still be dead.
            const int MaxAttempts = 3;
            HttpResponseMessage? resp = null;
            string raw = "";

            for (var attempt = 1; ; attempt++)
            {
                using var body = new StringContent(json, Encoding.UTF8, "application/json");
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(TimeSpan.FromSeconds(120));   // 20 images is not a fast call

                try
                {
                    resp?.Dispose();
                    resp = await _openAiClient.PostAsync("/v1/chat/completions", body, linked.Token);
                    raw = await resp.Content.ReadAsStringAsync(linked.Token);
                }
                catch (Exception ex) when (attempt < MaxAttempts
                                        && ex is TaskCanceledException or HttpRequestException
                                        && !ct.IsCancellationRequested)
                {
                    await Task.Delay(RetryDelay(attempt, null), ct);
                    continue;
                }

                if (resp.IsSuccessStatusCode) break;

                // A 400 is usually permanent, with one exception that matters here:
                // OpenAI fetches each image itself, and when a blob is slow it returns
                // 400 invalid_image_url saying it could not download in time. That is a
                // transient storage hiccup and the same case reads perfectly moments
                // later. The 404 form of the same error is permanent \u2014 the blob is gone,
                // and it is dropped before the call rather than retried into.
                var slowImage = resp.StatusCode == HttpStatusCode.BadRequest
                             && raw.Contains("invalid_image_url", StringComparison.OrdinalIgnoreCase)
                             && raw.Contains("before the timeout", StringComparison.OrdinalIgnoreCase);

                var retryable = resp.StatusCode == HttpStatusCode.TooManyRequests
                             || (int)resp.StatusCode >= 500
                             || slowImage;
                if (!retryable || attempt >= MaxAttempts)
                    throw new HttpRequestException($"OpenAI returned {(int)resp.StatusCode}: {raw}", null, resp.StatusCode);

                await Task.Delay(RetryDelay(attempt, resp.Headers.RetryAfter?.Delta), ct);
            }

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(text)) return null;

            var result = JsonSerializer.Deserialize<QcAiVisionResult>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Token usage goes to the log, not to Observations. Observations is
            // reviewer-facing text stored on the case and shown in the QC notes —
            // an accounting line has no business there, and it was surfacing as
            // "[usage] prompt=205150 …" on the QC page. Cost stays measurable.
            if (result != null && doc.RootElement.TryGetProperty("usage", out var usage))
            {
                _logger.LogInformation(
                    "QC photo read [{Pass}]: prompt={Prompt} completion={Completion} images={Images} highDetail={CloseUp}",
                    pass,
                    usage.GetProperty("prompt_tokens").GetInt32(),
                    usage.GetProperty("completion_tokens").GetInt32(),
                    photos.Count,
                    closeUpKeys.Count(k => photos.ContainsKey(k)));
            }

            return result;
        }

        private static string BuildIdentityPrompt(IEnumerable<string> photoKeys)
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
            sb.AppendLine("For EVERY photo that shows a chassis or VIN number \u2014 stamped into metal, on a");
            sb.AppendLine("rivetted plate, or rubbed onto paper \u2014 return a chassisReadings entry with that");
            sb.AppendLine("photo's key and the characters you read from THAT photo alone. Do not copy a");
            sb.AppendLine("value between photos and do not fill one in from another: if two photos show");
            sb.AppendLine("different characters, that difference is the point of this list. Use null for");
            sb.AppendLine("characters you cannot make out, and omit photos showing no number at all.");
            sb.AppendLine();
            sb.AppendLine("Each image is preceded by a [photo: KEY] label. Use those keys verbatim.");
            sb.AppendLine("The key says what the slot is MEANT to hold and is only a hint \u2014 slots are");
            sb.AppendLine("sometimes filled with the wrong picture. Read what is actually in the image.");
            sb.AppendLine("Photo keys in this case: " + string.Join(", ", photoKeys));
            return sb.ToString();
        }

        /// <summary>
        /// Strict structured-output schema. Strict mode requires every property to be
        /// listed in "required" and every object to set additionalProperties:false, so
        /// nullable fields are typed as ["string","null"] rather than being optional.
        /// An open dictionary cannot be expressed here at all.
        /// </summary>
        private static object IdentitySchema()
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
                    "chassisReadings", "observations"
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
                    ["chassisReadings"] = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "photoKey", "characters" },
                            properties = new Dictionary<string, object>
                            {
                                ["photoKey"]   = new { type = "string" },
                                ["characters"] = Nullable("string"),
                            }
                        }
                    },
                    ["observations"] = new { type = "array", items = new { type = "string" } }
                }
            };
        }

        /// <summary>
        /// The Pass 3 prompt. Nothing but provenance, and that is the whole point: asked
        /// alongside the identity readings this same instruction returned 2 stamps out of
        /// 20, and on its own it returns 17 — same photos, same low detail. Enumerating
        /// every photo is the first thing the model drops when a request has other work.
        ///
        /// The wording below is carried over unchanged because it is already validated:
        /// it stopped the reader taking landmark labels out of the map thumbnail, and
        /// stopped it inventing coordinates for camera apps that print none.
        /// </summary>
        private static string BuildProvenancePrompt(IEnumerable<string> photoKeys)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are reading the burned-in camera overlay on photographs from a vehicle");
            sb.AppendLine("inspection in India. That is the only thing you are being asked to do: do not");
            sb.AppendLine("read number plates or chassis numbers, and do not judge the vehicle.");
            sb.AppendLine();
            sb.AppendLine("Report ONLY what is printed into the image itself. Never infer a place, a");
            sb.AppendLine("coordinate or a date from the scenery, the buildings, the vegetation, the");
            sb.AppendLine("language on a signboard, or the vehicle. If a photo carries no overlay, omit it");
            sb.AppendLine("rather than guessing where it was taken.");
            sb.AppendLine();
            sb.AppendLine("Work through the photos ONE AT A TIME, in the order given, and do not stop");
            sb.AppendLine("early: a case has as many stamps as it has stamped photos, and a photo left out");
            sb.AppendLine("is read as one with no overlay. Every entry must name the photoKey it came from,");
            sb.AppendLine("copied verbatim from that photo's [photo: KEY] label.");
            sb.AppendLine();
            sb.AppendLine("Many photos carry a burned-in camera overlay showing a place, sometimes");
            sb.AppendLine("latitude/longitude, and a capture date/time. For every photo that has one,");
            sb.AppendLine("return a photoStamps entry with its photoKey and what the overlay states.");
            sb.AppendLine("Omit photos with no overlay.");
            sb.AppendLine();
            sb.AppendLine("The overlay's TEXT BLOCK is a run of consecutive lines sitting together with the");
            sb.AppendLine("date, the compass bearing, the altitude and the speed. Everything below is read");
            sb.AppendLine("from that block.");
            sb.AppendLine();
            sb.AppendLine("Some overlays ALSO embed a small map thumbnail: a rectangular picture of a map,");
            sb.AppendLine("with a pin, carrying its own printed labels for nearby landmarks \u2014 a school, a");
            sb.AppendLine("junction, a shop. Those labels are places NEAR the location, not the capture");
            sb.AppendLine("location itself. Never read anything from inside the map thumbnail.");
            sb.AppendLine();
            sb.AppendLine("placeName: the place named in the TEXT BLOCK, e.g. \"Nizamabad, Telangana\".");
            sb.AppendLine("latitude/longitude: ONLY if the TEXT BLOCK literally prints a coordinate line such");
            sb.AppendLine("  as \"16.79186°N, 81.1212°E\". Copy those digits. If you cannot point at such");
            sb.AppendLine("  a line, return null for both. Many camera apps print a place, an altitude and a");
            sb.AppendLine("  speed but no coordinates at all \u2014 that is normal, and null is the right answer.");
            sb.AppendLine("  Do NOT derive them from the place name, the scenery, or the map thumbnail: a");
            sb.AppendLine("  number here is taken as fact by the checks that follow.");
            sb.AppendLine("capturedAt: the date and the time from the TEXT BLOCK. They are often on separate");
            sb.AppendLine("  lines, and there may be both a local and a GMT time \u2014 combine the date with the");
            sb.AppendLine("  LOCAL time. Copy the digits exactly. Do NOT add a weekday and do not work one");
            sb.AppendLine("  out; if the overlay prints one, copy it, otherwise leave it out.");
            sb.AppendLine();
            sb.AppendLine("Each image is preceded by a [photo: KEY] label. Use those keys verbatim.");
            sb.AppendLine("Photo keys in this case: " + string.Join(", ", photoKeys));
            return sb.ToString();
        }

        /// <summary>Pass 3's schema: the provenance fields of QcAiVisionResult, nothing else.</summary>
        private static object ProvenanceSchema()
        {
            static object Nullable(string t) => new { type = new[] { t, "null" } };

            return new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "photoStamps", "observations" },
                properties = new Dictionary<string, object>
                {
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
        /// The Pass 2 prompt. Short and single-purpose on purpose: the corrosion
        /// instruction below was first tried inside the identity prompt and measurably
        /// did not survive there.
        /// </summary>
        private static string BuildConditionPrompt(IEnumerable<string> photoKeys)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are inspecting the exterior of a used vehicle in India from inspection photographs.");
            sb.AppendLine("That is the only thing you are being asked to do. Do not read number plates, chassis");
            sb.AppendLine("numbers or odometers, and do not comment on the camera overlay: separate passes handle");
            sb.AppendLine("those, and attending to them here costs you attention the bodywork needs.");
            sb.AppendLine();
            sb.AppendLine("Look at every photograph and examine the body panels, the load body or cargo bed, the");
            sb.AppendLine("tailgate, the doors, the cab, the bumpers, the wheel arches, the sills, and the lower");
            sb.AppendLine("edges and rails.");
            sb.AppendLine();
            sb.AppendLine("Report what you can actually see, and keep these five things SEPARATE. Do not merge");
            sb.AppendLine("them and do not substitute one for another:");
            sb.AppendLine();
            sb.AppendLine("1. RUST / CORROSION \u2014 oxidised metal. Reddish-brown, orange-brown or dark brown");
            sb.AppendLine("   staining, flaking, scaling, pitting, bubbling under paint, or corroded fasteners,");
            sb.AppendLine("   hinges, latches, rails and edges.");
            sb.AppendLine("2. FADED PAINT \u2014 paint that has lost colour, gone chalky or sun-bleached, but where");
            sb.AppendLine("   the metal underneath is still sound.");
            sb.AppendLine("3. DIRT \u2014 mud, dust, grime or stains that would wash off.");
            sb.AppendLine("4. SCRATCHES \u2014 lines, scuffs or abrasions through the surface.");
            sb.AppendLine("5. PAINT DAMAGE \u2014 chipped, peeling, flaking or missing paint that is not rust.");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT \u2014 you MUST report visible rust or corrosion whenever it is present, INCLUDING");
            sb.AppendLine("when the vehicle is brightly painted and the corrosion sits on a yellow, red, orange,");
            sb.AppendLine("blue or otherwise coloured surface. Rust on a yellow or a red panel is still rust. Do");
            sb.AppendLine("not dismiss reddish-brown or orange-brown corrosion as \"faded paint\", as \"paint damage\",");
            sb.AppendLine("as \"dirt\" or as part of the vehicle's decoration or colour scheme simply because it is a");
            sb.AppendLine("similar colour to the paint around it. Judge by texture and by where it sits \u2014 corrosion");
            sb.AppendLine("gathers on edges, seams, welds, rails, hinges, latches, the bottoms of panels and around");
            sb.AppendLine("fasteners, and it streaks downward with rain.");
            sb.AppendLine();
            sb.AppendLine("Equally, do not invent rust that is not there. If the metal is sound, say so.");
            sb.AppendLine();
            sb.AppendLine("Work through the five categories in turn and, for each, note the specific locations you");
            sb.AppendLine("can see it, naming the panel or part. A category you cannot see at all simply has no");
            sb.AppendLine("entries \u2014 never invent one to fill it.");
            sb.AppendLine();
            sb.AppendLine("Then report what you found:");
            sb.AppendLine();
            sb.AppendLine("- damageFound: every location you noted above, one entry each, written as");
            sb.AppendLine("  \"part - what is wrong\", e.g. \"load body - rust along the lower rails\",");
            sb.AppendLine("  \"rear bumper - cracked\". Name which of the five categories it is. Nothing you noted");
            sb.AppendLine("  may be left out, and an empty list is the right answer for a sound, clean body.");
            sb.AppendLine("- missingParts: parts a photo shows to be ABSENT, e.g. \"left wing mirror\" where the");
            sb.AppendLine("  mounting point is visibly empty. Externally visible parts only: mirrors, lights,");
            sb.AppendLine("  bumpers, wheels, handles, badges, wipers. A part merely out of frame, or hidden by");
            sb.AppendLine("  angle or shadow, is NOT missing.");
            sb.AppendLine("- exteriorCondition: 'good', 'average' or 'poor' for the body as a whole, not its worst");
            sb.AppendLine("  square inch. good = straight panels and even paint; average = scratches, small dents,");
            sb.AppendLine("  faded or mismatched paint, surface rust; poor = crumpled or holed panels, rust");
            sb.AppendLine("  through, or a missing panel.");
            sb.AppendLine("- exteriorReason: one short line naming the panels and defects the band rests on.");
            sb.AppendLine("- tyreCondition: 'good', 'average' or 'poor' for the WORST tyre you can see. good =");
            sb.AppendLine("  clear tread depth; average = visibly worn but tread still present; poor = bald, cord");
            sb.AppendLine("  showing, cracked sidewall or a cut. Null if no tyre is legible.");
            sb.AppendLine("- engineCondition: the VISIBLE state of the engine bay, from the bay shot only. These");
            sb.AppendLine("  are still images and you cannot hear anything, so judge nothing about whether the");
            sb.AppendLine("  engine starts or how it runs. good = dry and tidy; average = oil film, grime or");
            sb.AppendLine("  perished hoses; poor = active leaks, heavy corrosion or missing components. Null if");
            sb.AppendLine("  no engine bay photo is legible.");
            sb.AppendLine("- observations: anything else a reviewer should go and look at themselves.");
            sb.AppendLine();
            sb.AppendLine("Judge only what a photo actually shows. Never infer mechanical or hidden damage, and");
            sb.AppendLine("return null for any band the images cannot support. These drive a report a lender");
            sb.AppendLine("relies on.");
            sb.AppendLine();
            sb.AppendLine("Each image is preceded by a [photo: KEY] label. The key says what the slot is MEANT to");
            sb.AppendLine("hold and is only a hint: slots are sometimes filled with the wrong picture. Trust the");
            sb.AppendLine("image over its key and judge what is actually in front of you.");
            sb.AppendLine("Photo keys in this case: " + string.Join(", ", photoKeys));
            return sb.ToString();
        }

        /// <summary>
        /// Pass 2's schema: only the condition fields of QcAiVisionResult. Everything
        /// else deserialises to its default and is filled from Pass 1 by the caller.
        /// </summary>
        private static object ConditionSchema()
        {
            static object Nullable(string t) => new { type = new[] { t, "null" } };

            return new
            {
                type = "object",
                additionalProperties = false,
                required = new[]
                {
                    "exteriorCondition", "engineCondition", "tyreCondition",
                    "exteriorReason", "damageFound", "missingParts", "observations"
                },
                properties = new Dictionary<string, object>
                {
                    ["exteriorCondition"] = new { type = new[] { "string", "null" }, @enum = new object?[] { "good", "average", "poor", null } },
                    ["engineCondition"]   = new { type = new[] { "string", "null" }, @enum = new object?[] { "good", "average", "poor", null } },
                    ["tyreCondition"]     = new { type = new[] { "string", "null" }, @enum = new object?[] { "good", "average", "poor", null } },
                    ["exteriorReason"]    = Nullable("string"),
                    ["damageFound"]       = new { type = "array", items = new { type = "string" } },
                    ["missingParts"]      = new { type = "array", items = new { type = "string" } },
                    ["observations"]      = new { type = "array", items = new { type = "string" } }
                }
            };
        }

    }
}
