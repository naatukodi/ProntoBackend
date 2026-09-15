using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Valuation.Api.Models;
using Valuation.Api.Repositories;

namespace Valuation.Api.Services
{
    /// <summary>
    /// Turns a case's inspection photos into QC checklist verdicts.
    ///
    /// The vision model only reads characters off the images. Every match decision is
    /// made here, in code, against the RC data — so a verdict can be explained, and a
    /// misread cannot present itself as a pass. Where a value could not be read the
    /// check is left unresolved for the reviewer rather than guessed at, because on a
    /// document a bank lends against, a missing check must look missing.
    /// </summary>
    public interface IQcVisionAuditService
    {
        /// <param name="force">Read again even when a stored reading still matches the
        /// photos. For the reviewer who wants a second opinion; costs another call.</param>
        Task<QcAiAuditDto> AuditAsync(string valuationId, string vehicleNumber,
                                      string applicantContact, bool force = false,
                                      CancellationToken ct = default);
    }

    public class QcAiAuditDto
    {
        /// <summary>Checklist key to verdict. A key absent here stays unresolved.</summary>
        public Dictionary<string, string> Cl { get; set; } = new();

        /// <summary>Checklist key to the evidence behind the verdict. Always populated.</summary>
        public Dictionary<string, string> Why { get; set; } = new();

        /// <summary>
        /// Checklist key to "resolved" or "unresolved", for keys the reader looked at.
        ///
        /// An absent <see cref="Cl"/> entry used to mean two different things — the
        /// reader never examined this, or it examined it and could not decide — and the
        /// UI could not tell them apart, so an inconclusive read showed as an unchecked
        /// card. A key present here was looked at; the verdict says whether it settled.
        /// </summary>
        public Dictionary<string, string> Status { get; set; } = new();

        public List<string> Observations { get; set; } = new();

        /// <summary>What the reader saw, shown as-is beside the verdicts.</summary>
        public QcAiReadings? Readings { get; set; }

        /// <summary>When this reading was made. Null when nothing has been read.</summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>True when this came from the stored reading rather than a fresh call.</summary>
        public bool Cached { get; set; }

        /// <summary>Set when the audit could not run at all, so the UI can say why.</summary>
        public string? Error { get; set; }
    }

    public class QcVisionAuditService : IQcVisionAuditService
    {
        private readonly CosmosClient _cosmos;
        private readonly IChatGptRepository _ai;
        private readonly string _dbId;
        private readonly string _containerId;
        private readonly ILogger<QcVisionAuditService> _logger;
        private readonly int _minuteCeiling;

        /// <summary>
        /// Slots whose job is to carry CHARACTERS, in the order full detail is worth
        /// spending on them. Pass 1 promotes as many of these as its budget allows.
        ///
        /// This is a priority list, not a membership test: which slots actually go at
        /// high detail is decided per case by <see cref="PlanDetail"/>, because a fixed
        /// list is what let a 21-photo case reach 339k prompt tokens against a 200k
        /// limit. The order matters — the odometer and the chassis are what the checks
        /// turn on.
        /// </summary>
        private static readonly string[] IdentityDetailPriority =
        {
            "Odometer", "ChassisImprint", "ChassisStencilTrace", "ChassisVerification",
            "ChassisNumberPlate", "ChassisNumber", "Chassis", "VinPlate", "VIN",
            "InstrumentCluster", "DashboardCloseup", "Dashboard"
        };

        /// <summary>
        /// Slots that show the body, in the order full detail helps most.
        ///
        /// The three-quarter corner views come first even though the side profiles sound
        /// like the better bet. Two reasons, both measured: the corners reliably contain
        /// an exterior of the vehicle, while the profile slots held an interior cabin shot
        /// on two of the three cases checked by eye — one of them the same photo filed
        /// twice; and corrosion collects on the rear rails and tailgate, which the rear
        /// corners cover and a side profile often crops out.
        /// </summary>
        private static readonly string[] ConditionDetailPriority =
        {
            "RearLeftSide", "RearRightSide", "FrontLeftSide", "FrontRightSide",
            "RearViewTailgate", "DriverSideProfile", "PassengerSideProfile",
            "FrontViewGrille", "Underbody",
            "TiresAndRims", "TireFrontLeft", "TireFrontRight", "TireRearLeft",
            "TireRearRight", "EngineBay"
        };

        /// <summary>
        /// Slots that are only ever documents or paperwork, so Pass 2 has nothing to
        /// judge in them. Everything NOT named here goes to Pass 2, including slots this
        /// list has never heard of: a slot name is a hint about what was meant to be
        /// photographed, not a guarantee of what is in the frame, and an unknown slot
        /// might hold the one panel with the damage on it.
        /// </summary>
        private static readonly HashSet<string> DocumentOnlySlots = new(StringComparer.OrdinalIgnoreCase)
        {
            "ChassisImprint", "ChassisStencilTrace", "ChassisVerification",
            "ChassisNumberPlate", "ChassisNumber", "Chassis", "VinPlate", "VIN",
            "Odometer", "InstrumentCluster", "SelfieWithVehicle", "WorkingOperationPhoto",
            "RcBook", "Insurance", "InsuranceCopy", "PermitCopy", "FitnessCopy"
        };

        /// <summary>
        /// Slots with nothing on them for Pass 1 to read — no plate, no chassis, no dial.
        /// They still go to Pass 2 and to Pass 3, so every photo is still looked at; they
        /// are simply not paid for three times over.
        ///
        /// This is what makes the arithmetic work at 25 photos. With Pass 1 also sending
        /// everything, the three passes cost 195,204 tokens against a 200,000/minute limit
        /// with ZERO high detail — there would be no allocation left to cut.
        /// </summary>
        private static readonly HashSet<string> ConditionOnlySlots = new(StringComparer.OrdinalIgnoreCase)
        {
            "TireFrontLeft", "TireFrontRight", "TireRearLeft", "TireRearRight",
            "TiresAndRims", "Underbody", "FrontSeat", "RearSeat", "GearAndSeats",
            "EngineBay", "DriverSideProfile", "PassengerSideProfile"
        };

        // How the three requests are sized. gpt-4o-mini charges 2,833 tokens per image
        // plus 5,667 per 512px tile at high detail; a 1600x1200 or 1280x960 photo is
        // four tiles, which is what nearly every real inspection photo turns out to be.
        // Measured against the API's own usage field on ten real requests, this predicts
        // a request to within two tokens.
        //
        // What matters is the MINUTE, not the request. All three passes run back to back
        // for one case, so TripleCeiling budgets the case as a whole and leaves 12.5%
        // under the 200k/min limit — enough that one retried request's backoff landing
        // in the same window does not breach it.
        private const int LowPhotoTokens = 2844;    // 2,833 + the "[photo: Key]" label
        private const int HighPhotoExtra = 22668;   // 4 tiles x 5,667
        private const int PromptOverhead = 2500;    // prompt text, generously
        private const int TripleCeiling = 180000;   // all three requests, against 200k/min
        private const int MaxIdentityHigh = 2;      // odometer + one chassis is what pays
        private const int MaxConditionHigh = 2;

        // The provider bills tokens per minute, not per request, so a big case can
        // sit inside the limit request-by-request and still breach it as a whole.
        // TripleCeiling above only decides how much full detail is affordable; it
        // cannot shrink the low-detail floor, which grows with the photo count. A
        // 40-photo case projects to roughly 258k however the detail is allocated.
        //
        // ASSUMPTION, stated because the provider does not publish it to us: the
        // org allowance is 200,000 tokens/minute. Override it with
        // OpenAI:TokensPerMinute when the real figure is known. We pace against a
        // fraction of it so a retry landing in the same window cannot breach it.
        private const int DefaultTokensPerMinute = 200_000;
        private const double OperationalFraction = 0.85;

        /// <summary>Two photos further apart than this are not the same inspection site.</summary>
        // One shared client for the reachability probe below. Static because this
        // service is scoped, and a new HttpClient per request exhausts sockets.
        private static readonly HttpClient Probe = new() { Timeout = TimeSpan.FromSeconds(15) };

        private const double SameSiteMetres = 250;

        /// <summary>
        /// Reads currently running, by case.
        ///
        /// AVO submit starts a read and then sends the reviewer straight to QC, whose
        /// page reads on open. Without this the second caller would find nothing stored
        /// yet, start its own read of the same photos, and both would pay. Joining the
        /// running task instead costs one call and gives both callers the same answer.
        ///
        /// Static because the service is scoped — a per-instance dictionary would be a
        /// fresh empty one on every request. Process-local, so on a multi-instance
        /// deployment two instances can still double up; that is the old behaviour, not
        /// a regression, and it costs a duplicate call rather than a wrong answer.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<QcAiAuditDto>> InFlight = new();

        public QcVisionAuditService(CosmosClient cosmos, IChatGptRepository ai, IConfiguration config,
                                   ILogger<QcVisionAuditService> logger)
        {
            _cosmos = cosmos;
            _ai = ai;
            _logger = logger;
            _dbId = config["Cosmos:DatabaseId"] ?? "ValuationsDb";
            _containerId = config["Cosmos:ContainerId"] ?? "Valuations";
            var tpm = int.TryParse(config["OpenAI:TokensPerMinute"], out var t) && t > 0
                ? t : DefaultTokensPerMinute;
            _minuteCeiling = (int)(tpm * OperationalFraction);
        }

        private Container Container => _cosmos.GetDatabase(_dbId).GetContainer(_containerId);

        public Task<QcAiAuditDto> AuditAsync(string valuationId, string vehicleNumber,
                                             string applicantContact, bool force = false,
                                             CancellationToken ct = default)
        {
            // One read per case at a time. A caller arriving while another is running
            // waits for that one rather than starting a second read of the same photos.
            //
            // The shared read deliberately ignores the caller's cancellation token: it
            // is shared, so the first caller giving up — the AVO officer's browser
            // moving on to QC — must not abort the read the next caller is waiting on.
            // An abandoned read still finishes and stores its answer, which is the
            // point of starting it early.
            var key = $"{valuationId}|{vehicleNumber}|{applicantContact}";
            var task = InFlight.GetOrAdd(key, _ =>
                RunAuditAsync(valuationId, vehicleNumber, applicantContact, force, CancellationToken.None));
            return AwaitAndRelease(key, task);
        }

        private static async Task<QcAiAuditDto> AwaitAndRelease(string key, Task<QcAiAuditDto> task)
        {
            try { return await task; }
            finally { InFlight.TryRemove(key, out _); }
        }

        private async Task<QcAiAuditDto> RunAuditAsync(string valuationId, string vehicleNumber,
                                                       string applicantContact, bool force,
                                                       CancellationToken ct)
        {
            var outp = new QcAiAuditDto();

            ValuationDocument doc;
            try
            {
                var resp = await Container.ReadItemAsync<ValuationDocument>(
                    valuationId, new PartitionKey($"{vehicleNumber}|{applicantContact}"), cancellationToken: ct);
                doc = resp.Resource;
            }
            catch (CosmosException ex)
            {
                outp.Error = $"Case not found ({ex.StatusCode}).";
                return outp;
            }

            var photos = (doc.PhotoUrls ?? new Dictionary<string, string>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Value) && !IsVideo(p.Value))
                .ToDictionary(p => p.Key, p => p.Value);

            if (photos.Count == 0)
            {
                outp.Error = "No photos uploaded for this case.";
                return outp;
            }

            // Only the READING is cached. The verdicts below are recomputed every time,
            // because they compare the reading against the case's current data — and that
            // changes whenever an AVO edits the odometer or the vehicle details after a QC
            // bounce, without any photo changing. Recomputing is free; re-reading is not.
            var fingerprint = Fingerprint(photos);
            var stored = doc.QcAiAudit;

            // OpenAI fetches each image by URL and refuses the entire request if any one
            // of them 404s, so a single deleted blob used to cost the reviewer every
            // verdict on the case. Unreachable photos are dropped here and named in the
            // observations instead.
            var unreachable = new List<string>();
            var contentHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!force || true)
            {
                unreachable = await UnreachablePhotosAsync(photos, contentHashes, ct);
                foreach (var key in unreachable) photos.Remove(key);

                if (photos.Count == 0)
                {
                    outp.Error = "None of this case's photos could be downloaded.";
                    return outp;
                }
            }

            // Some cases upload the same file into several slots — one case had the same
            // image in eighteen of them. Identical bytes teach the model nothing the first
            // copy did not, so only one of each is sent and the reading is copied back onto
            // its twins afterwards. Every original slot still appears in the result.
            var duplicates = GroupByContent(photos, contentHashes);
            var distinct = photos
                .Where(p => !duplicates.ContainsKey(p.Key))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

            // Pass 1 skips slots that cannot carry characters. Pass 2 skips slots that are
            // unambiguously documents. A slot neither list has heard of goes to BOTH: its
            // name says what was meant to be photographed, not what is in the frame, and
            // more than one case has been found with an interior shot filed as a side
            // profile. Pass 3 takes every photo, so nothing goes unread either way.
            var identityPhotos = distinct
                .Where(p => !ConditionOnlySlots.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var conditionPhotos = distinct
                .Where(p => !DocumentOnlySlots.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var provenancePhotos = distinct;

            var (identityHigh, conditionHigh) =
                PlanDetail(identityPhotos.Keys, conditionPhotos.Keys, provenancePhotos.Count);

            // What this case is about to cost, before any of it is spent.
            var projected = Project(identityPhotos.Count, identityHigh.Count)
                          + Project(conditionPhotos.Count, conditionHigh.Count)
                          + Project(provenancePhotos.Count, 0);

            // One fingerprint per pass, over that pass's own photos. Swapping a damaged
            // panel re-runs condition and provenance and leaves identity alone.
            var idPrint = Fingerprint(identityPhotos);
            var condPrint = Fingerprint(conditionPhotos);
            var provPrint = Fingerprint(provenancePhotos);

            var idRec = Reusable(stored?.Identity, idPrint, force);
            var condRec = Reusable(stored?.Condition, condPrint, force);
            var provRec = Reusable(stored?.Provenance, provPrint, force);

            // Each pass owns its fields, so a pass that fails leaves the others' answers
            // untouched rather than blanking them. Identity is the exception that aborts:
            // with no plate, chassis or odometer there is nothing to check the case
            // against, and reporting "verified" off the other two would be a lie.
            var identity = idRec?.Reading;
            if (identity == null)
            {
                try
                {
                    await PaceAsync(Project(identityPhotos.Count, identityHigh.Count), ct);
                    identity = await _ai.ReadInspectionPhotosAsync(identityPhotos, identityHigh, ct);
                }
                catch (Exception ex)
                {
                    // A failed read must leave every check unresolved rather than passing
                    // or failing it — the reviewer needs to know nothing was verified.
                    outp.Error = $"Photo reading failed: {ex.Message}";
                    return outp;
                }

                if (identity == null)
                {
                    outp.Error = "The photo reader returned nothing.";
                    return outp;
                }
            }

            var condition = condRec?.Reading;
            var conditionNote = "";
            if (condition == null && conditionPhotos.Count > 0)
            {
                await PaceAsync(Project(conditionPhotos.Count, conditionHigh.Count), ct);
                (condition, conditionNote) = await TryPassAsync(
                    () => _ai.ReadVehicleConditionAsync(conditionPhotos, conditionHigh, ct),
                    "Condition could not be judged from the photos");
            }

            var provenance = provRec?.Reading;
            var provenanceNote = "";
            if (provenance == null && provenancePhotos.Count > 0)
            {
                // Pass 3 is the one that divides safely: every photo is read on its
                // own terms, so a chunk is a smaller Pass 3 rather than a partial
                // one. Identity and condition are never split - they compare photos
                // against each other and would lose that if broken up.
                var chunks = ChunkProvenance(provenancePhotos, projected > _minuteCeiling);
                if (chunks.Count > 1)
                    _logger.LogInformation(
                        "QC read {Case}: projected {Projected} tokens over the {Ceiling} "
                        + "operational ceiling, pass 3 split into {Chunks} chunks",
                        valuationId, projected, _minuteCeiling, chunks.Count);

                var notes = new List<string>();
                foreach (var chunk in chunks)
                {
                    await PaceAsync(Project(chunk.Count, 0), ct);
                    var (part, note) = await TryPassAsync(
                        () => _ai.ReadPhotoProvenanceAsync(chunk, ct),
                        "Photo capture places and times could not be read");
                    if (part != null)
                    {
                        provenance ??= new QcAiVisionResult();
                        provenance.PhotoStamps.AddRange(part.PhotoStamps ?? new List<QcAiPhotoStamp>());
                        if (part.Observations is { Count: > 0 })
                            provenance.Observations.AddRange(part.Observations);
                    }
                    else if (note.Length > 0) notes.Add(note);
                }
                // A chunk that failed costs its own photos their stamps, not the
                // whole pass. Those photos then read as "no capture overlay", so the
                // reviewer is told which ones were never actually looked at.
                if (notes.Count > 0)
                    provenanceNote = chunks.Count > 1
                        ? $"{notes.Count} of {chunks.Count} provenance chunk(s) failed. "
                          + "Photos in those chunks carry no capture place or time here "
                          + "and were not checked."
                        : notes[0];
            }

            var read = Merge(identity, condition, provenance);
            if (conditionNote.Length > 0) read.Observations.Add(conditionNote);
            if (provenanceNote.Length > 0) read.Observations.Add(provenanceNote);
            FanOutDuplicates(read, duplicates);

            var reused = idRec != null && condRec != null && provRec != null;
            if (reused)
            {
                outp.ReadAt = new[] { idRec!.ReadAt, condRec!.ReadAt, provRec!.ReadAt }.Max();
                outp.Cached = true;
            }

            outp.Observations = read.Observations;

            if (unreachable.Count > 0)
            {
                // Named rather than swallowed: a check that "passed" on eighteen of twenty
                // photos is a different statement from one that saw them all.
                outp.Observations.Add(
                    $"{unreachable.Count} photo(s) could not be downloaded and were not read: "
                    + string.Join(", ", unreachable.Select(Pretty)) + ".");
            }

            var vd = doc.VehicleDetails;
            var ins = doc.InspectionDetails;

            CompareText(outp, "accReg", "Number plate", read.RegistrationPlate,
                        vd?.RegistrationNumber ?? doc.VehicleNumber);

            CompareChassis(outp, read, vd?.ChassisNumber);

            CompareChassisAcrossPhotos(outp, read.ChassisReadings, vd?.ChassisNumber);

            // On most Indian vehicles the VIN plate is the chassis plate, so whichever
            // of the two the reader managed to make out stands in for it.
            CompareText(outp, "accVIN", "VIN plate",
                        string.IsNullOrWhiteSpace(read.VinPlate) ? read.ChassisNumber : read.VinPlate,
                        vd?.ChassisNumber, vinLike: true);

            CompareOdometer(outp, read.OdometerKm, ins?.Odometer);

            Judgement(outp, "accDaylight", read.Daylight,
                      "Photos are bright and clear enough to inspect.",
                      "Photos are too dark or unclear to inspect reliably.");

            Judgement(outp, "accPlate", read.PlateLegible,
                      "Number plate is legible in both the front and rear photos.",
                      "Number plate is obscured or unreadable in at least one of the front/rear photos.");

            CompareCondition(outp, "recExterior", "Exterior / body", read.ExteriorCondition,
                             ins?.ExteriorCondition ?? ins?.BodyCondition,
                             good: "good", average: "minor", poor: "major",
                             basis: read.ExteriorReason);

            // Deliberately scoped to the bay photo, and the card says so. A still image
            // shows leaks, corrosion and missing components; it cannot show whether the
            // engine starts or how it sounds, which is what "engine condition" normally
            // means to a reviewer. Judging that needs a dedicated engine-start recording
            // with audio and a model that can hear it — neither exists yet, so this
            // states what it actually looked at rather than implying more.
            CompareCondition(outp, "recEngine", "Engine bay", read.EngineCondition,
                             ins?.EngineCondition,
                             good: "good", average: "average", poor: "poor",
                             basis: "visible state of the bay only — the engine was not heard running");

            CompareCondition(outp, "recTyre", "Tyre", read.TyreCondition,
                             ins?.TyreCondition ?? ins?.OverallTyreCondition,
                             good: "good", average: "average", poor: "replacement");

            if (!string.IsNullOrWhiteSpace(read.ChassisPunch))
            {
                outp.Cl["docChassis"] = read.ChassisPunch!;
                outp.Why["docChassis"] = read.ChassisPunch switch
                {
                    "original"  => "Stamped characters look evenly spaced and consistent.",
                    "repunched" => "Stamped characters show signs of being re-punched — confirm against the RC.",
                    "tampered"  => "Stamped characters show signs of tampering — grinding, overstamping or mixed fonts.",
                    _           => "Chassis punch could not be judged from the photos."
                };
            }
            else
            {
                outp.Why["docChassis"] = "No chassis punch was legible in the photos.";
            }

            EvaluateStamps(outp, read.PhotoStamps, ins?.DateOfInspection, ins?.InspectionLocation,
                           photos.Keys.ToList());

            // Shown beside the verdicts: a wrong reading and a wrong vehicle produce the
            // same "fail" if only the conclusion is displayed, and the reviewer needs to
            // be able to tell those apart without opening every photo.
            outp.Readings = new QcAiReadings
            {
                RegistrationPlate = read.RegistrationPlate,
                ChassisNumber     = read.ChassisNumber,
                ChassisStencil    = read.ChassisStencil,
                VinPlate          = read.VinPlate,
                OdometerKm        = read.OdometerKm,
                Places = read.PhotoStamps.Select(s => (s.PlaceName ?? "").Trim())
                             .Where(p => p.Length > 0)
                             .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                CaptureDates = read.PhotoStamps.Select(s => ParseStamp(s.CapturedAt))
                                   .Where(d => d.HasValue)
                                   .Select(d => d!.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture))
                                   .Distinct().ToList(),
                StampedPhotos = read.PhotoStamps.Count,
                TotalPhotos   = photos.Count,
                // Findings, not verdicts. The portal turns these into the damage and
                // missing-parts cards, because comparing them against what the AVO
                // recorded needs the per-vehicle field registry, which lives there.
                DamageFound   = read.DamageFound,
                MissingParts  = read.MissingParts
            };
            // A reused reading keeps the timestamp it was made at; a fresh one is now.
            if (!reused) outp.ReadAt = DateTime.UtcNow;

            // Only a fresh reading is worth a write. The verdicts are derived, so storing
            // them would be storing a copy that goes stale the moment the case changes.
            if (!reused)
                _logger.LogInformation(
                    "QC read {Case}: model={Model} promptVersion={Version} photos={Total} "
                    + "distinct={Distinct} p1={P1} p2={P2} p3={P3} projected={Projected} "
                    + "ceiling={Ceiling} identityOk={IdOk} conditionOk={CondOk} provenanceOk={ProvOk}",
                    valuationId, _ai.ModelName, PromptVersion, photos.Count, distinct.Count,
                    identityPhotos.Count, conditionPhotos.Count, provenancePhotos.Count,
                    projected, _minuteCeiling, identity != null, condition != null,
                    provenance != null);

            if (!reused)
                await PersistAsync(doc, read!, fingerprint, outp.ReadAt!.Value, ct,
                    Keep(idRec, identity, idPrint, outp.ReadAt!.Value),
                    Keep(condRec, condition, condPrint, outp.ReadAt!.Value),
                    Keep(provRec, provenance, provPrint, outp.ReadAt!.Value));

            return outp;
        }

        /// <summary>
        /// Keeps the reading on the case. A failure here costs a repeat call next time
        /// but nothing else, so it never takes down an audit the reviewer can already see.
        ///
        /// Patches rather than replaces: this runs while a reviewer may be saving the QC
        /// form, and writing back a document read seconds earlier would put their work back
        /// as it was.
        /// </summary>
        private async Task PersistAsync(ValuationDocument doc, QcAiVisionResult reading,
                                        string fingerprint, DateTime readAt, CancellationToken ct,
                                        QcAiPassRecord? identity, QcAiPassRecord? condition,
                                        QcAiPassRecord? provenance)
        {
            try
            {
                var record = new QcAiAuditRecord
                {
                    Reading = reading,
                    PhotoFingerprint = fingerprint,
                    ReadAt = readAt,
                    Model = _ai.ModelName,
                    PromptVersion = PromptVersion,
                    Identity = identity,
                    Condition = condition,
                    Provenance = provenance
                };

                await Container.PatchItemAsync<ValuationDocument>(
                    doc.id, new PartitionKey(doc.CompositeKey),
                    new[] { PatchOperation.Set("/QcAiAudit", record) },
                    cancellationToken: ct);
            }
            catch (Exception)
            {
                // Swallowed on purpose: the reviewer already has the answer on screen, and
                // the only cost is that the next open reads the photos again.
            }
        }

        /// <summary>
        /// Version of the question put to the reader. Bump this whenever the prompt or
        /// the result schema changes, or stored readings keep answering the old one:
        /// the fingerprint covers the photo set, and the photos do not change just
        /// because we started asking about tyre wear.
        /// </summary>
        private const string PromptVersion = "v9-three-pass";

        /// <summary>
        /// Photo keys whose blob cannot be fetched.
        ///
        /// A HEAD is enough to tell a deleted blob from a live one and costs a fraction of
        /// a download. Run in parallel with a small gate so a twenty-photo case adds about
        /// a second rather than twenty. A photo that merely times out here is left IN: the
        /// reader gets its own, longer go at it, and dropping a slow-but-live photo would
        /// lose evidence.
        /// </summary>
        private async Task<List<string>> UnreachablePhotosAsync(
            IReadOnlyDictionary<string, string> photos,
            IDictionary<string, string> contentHashes,
            CancellationToken ct)
        {
            var dead = new System.Collections.Concurrent.ConcurrentBag<string>();
            var hashes = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            using var gate = new SemaphoreSlim(8);

            var checks = photos.Select(async kv =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, kv.Value);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    using var resp = await Probe.SendAsync(req, cts.Token);

                    // Only a definite "this is not there" removes a photo. A 5xx or a
                    // timeout is the storage account having a moment, and the reader may
                    // still fetch it perfectly.
                    if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone or HttpStatusCode.Forbidden)
                    {
                        dead.Add(kv.Key);
                    }
                    else if (resp.Content.Headers.ContentMD5 is { Length: > 0 } md5)
                    {
                        // Azure sets Content-MD5 at upload, so the HEAD we are already
                        // making tells us which slots hold byte-identical images without
                        // downloading anything. Absent on some blobs, in which case the
                        // photo is simply treated as unique.
                        hashes[kv.Key] = Convert.ToHexString(md5);
                    }
                }
                catch (Exception)
                {
                    // Unreachable from here does not mean unreachable from OpenAI.
                }
                finally { gate.Release(); }
            });

            await Task.WhenAll(checks);
            foreach (var (k, v) in hashes) contentHashes[k] = v;
            return dead.OrderBy(k => k, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Chooses which photos are worth full detail, sized from the photos this case
        /// actually has rather than from a fixed list.
        ///
        /// High detail costs about nine times low, so the number of high-detail photos
        /// — not the number of photos — is what decides whether a case fits the token
        /// limit. Both requests are budgeted together because Pass 2 follows Pass 1
        /// inside the same minute.
        /// </summary>
        private static (HashSet<string> Identity, HashSet<string> Condition) PlanDetail(
            IEnumerable<string> identityKeys, IEnumerable<string> conditionKeys, int provenanceCount)
        {
            var all = identityKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cond = conditionKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Pass 3 is every photo at low detail and is not negotiable, so it comes off
            // the top; what is left is what the other two can spend on detail.
            var floor = (3 * PromptOverhead)
                      + (all.Count * LowPhotoTokens)
                      + (cond.Count * LowPhotoTokens)
                      + (provenanceCount * LowPhotoTokens);
            var affordable = Math.Max(0, (TripleCeiling - floor) / HighPhotoExtra);

            // The units alternate between the two passes rather than identity draining
            // the pool. Both have a measured need for full detail and neither is worth
            // starving: the same plate read AP39UL1633 at low detail and AP39UL1499 at
            // high, and the same twelve photos yielded "surface rust along lower edge"
            // with two high-detail shots and "faded paint" with none.
            int wantId = 0, wantCond = 0;
            for (var spent = 0; spent < affordable; spent++)
            {
                if (spent % 2 == 0 && wantId < MaxIdentityHigh) wantId++;
                else if (wantCond < MaxConditionHigh) wantCond++;
                else if (wantId < MaxIdentityHigh) wantId++;
                else break;
            }

            var identity = IdentityDetailPriority
                .Where(all.Contains).Take(wantId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var condition = ConditionDetailPriority
                .Where(cond.Contains).Take(wantCond)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return (identity, condition);
        }

        /// <summary>
        /// Maps each duplicate photo slot to the one slot that will be sent for it.
        /// Slots with no content hash are never folded — unknown is not identical.
        /// </summary>
        private static Dictionary<string, string> GroupByContent(
            IReadOnlyDictionary<string, string> photos,
            IReadOnlyDictionary<string, string> hashes)
        {
            var representative = new Dictionary<string, string>(StringComparer.Ordinal);
            var folded = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var key in photos.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!hashes.TryGetValue(key, out var hash)) continue;
                if (representative.TryGetValue(hash, out var first)) folded[key] = first;
                else representative[hash] = key;
            }

            return folded;
        }

        /// <summary>
        /// Copies a representative photo's stamp and chassis reading onto the slots that
        /// held the same image, so the checks downstream see every slot the AVO uploaded.
        /// Without this a deduplicated case would look like one with missing overlays.
        /// </summary>
        private static void FanOutDuplicates(QcAiVisionResult read, Dictionary<string, string> duplicates)
        {
            if (duplicates.Count == 0) return;

            foreach (var (copy, original) in duplicates)
            {
                var stamp = read.PhotoStamps.FirstOrDefault(
                    p => string.Equals(p.PhotoKey, original, StringComparison.OrdinalIgnoreCase));
                if (stamp != null && !read.PhotoStamps.Any(
                        p => string.Equals(p.PhotoKey, copy, StringComparison.OrdinalIgnoreCase)))
                {
                    read.PhotoStamps.Add(new QcAiPhotoStamp
                    {
                        PhotoKey = copy,
                        Latitude = stamp.Latitude,
                        Longitude = stamp.Longitude,
                        PlaceName = stamp.PlaceName,
                        CapturedAt = stamp.CapturedAt
                    });
                }

                var chassis = read.ChassisReadings.FirstOrDefault(
                    c => string.Equals(c.PhotoKey, original, StringComparison.OrdinalIgnoreCase));
                if (chassis != null && !read.ChassisReadings.Any(
                        c => string.Equals(c.PhotoKey, copy, StringComparison.OrdinalIgnoreCase)))
                {
                    read.ChassisReadings.Add(new QcAiChassisReading
                    {
                        PhotoKey = copy,
                        Characters = chassis.Characters
                    });
                }
            }

            // Worth saying out loud: two slots holding one image is usually an upload
            // mistake, and the reviewer is the only one who can tell.
            var groups = duplicates.GroupBy(d => d.Value, StringComparer.Ordinal)
                .Select(g => $"{Pretty(g.Key)} = {string.Join(" = ", g.Select(x => Pretty(x.Key)))}");
            read.Observations.Add(
                "Some slots hold the same image, so it was read once and applied to each: "
                + string.Join("; ", groups) + ".");
        }


        /// <summary>What one request of this shape will cost, by the measured formula.</summary>
        private static int Project(int photos, int highDetail) =>
            PromptOverhead + (photos * LowPhotoTokens) + (highDetail * HighPhotoExtra);

        /// <summary>
        /// Splits Pass 3 only when the case as a whole is projected over the minute
        /// ceiling. Chunks are sized to half the ceiling so two can never collide in
        /// one window. No photo is dropped: every chunk is sent.
        /// </summary>
        private List<Dictionary<string, string>> ChunkProvenance(
            Dictionary<string, string> photos, bool split)
        {
            if (!split || photos.Count == 0)
                return new List<Dictionary<string, string>> { photos };

            var per = Math.Max(4, ((_minuteCeiling / 2) - PromptOverhead) / LowPhotoTokens);
            var chunks = new List<Dictionary<string, string>>();
            var current = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (k, v) in photos)
            {
                current[k] = v;
                if (current.Count < per) continue;
                chunks.Add(current);
                current = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            if (current.Count > 0) chunks.Add(current);
            return chunks;
        }

        /// <summary>
        /// Holds a call back until it fits inside the rolling minute, then books its
        /// tokens. Process-wide, because the allowance belongs to the org and not to
        /// one case: two cases reading at once share it.
        ///
        /// A single request larger than the whole ceiling is let through rather than
        /// waited on forever - it cannot ever fit, and the repository's own 429 retry
        /// is the right handler for it.
        /// </summary>
        private async Task PaceAsync(int tokens, CancellationToken ct)
        {
            await PaceGate.WaitAsync(ct);
            try
            {
                // A single request bigger than the whole ceiling can never fit inside a
                // window, so waiting for one to roll cannot help it. Book it and go; the
                // repository's own 429 retry is the right handler if the provider objects.
                if (tokens >= _minuteCeiling)
                {
                    Spent.Enqueue((DateTime.UtcNow, tokens));
                    return;
                }

                while (true)
                {
                    var now = DateTime.UtcNow;
                    while (Spent.Count > 0 && (now - Spent.Peek().At).TotalSeconds >= 60)
                        Spent.Dequeue();
                    if (Spent.Count == 0) break;
                    var used = Spent.Sum(s => s.Tokens);
                    if (used + tokens <= _minuteCeiling) break;
                    var wait = TimeSpan.FromSeconds(60) - (now - Spent.Peek().At);
                    if (wait <= TimeSpan.Zero) continue;
                    _logger.LogInformation(
                        "QC read pacing: {Used} tokens used this minute, {Want} wanted, "
                        + "waiting {Seconds:F0}s for the window to roll",
                        used, tokens, wait.TotalSeconds);
                    await Task.Delay(wait, ct);
                }
                Spent.Enqueue((DateTime.UtcNow, tokens));
            }
            finally { PaceGate.Release(); }
        }

        private static readonly SemaphoreSlim PaceGate = new(1, 1);
        private static readonly Queue<(DateTime At, int Tokens)> Spent = new();

        /// <summary>A pass record is reusable when it read the same photos and force is off.</summary>
        private static QcAiPassRecord? Reusable(QcAiPassRecord? rec, string fingerprint, bool force) =>
            !force && rec?.Reading != null && rec.Fingerprint == fingerprint ? rec : null;

        /// <summary>Keeps a reused record as it was, or wraps a fresh reading for storage.</summary>
        private static QcAiPassRecord? Keep(QcAiPassRecord? reused, QcAiVisionResult? fresh,
                                            string fingerprint, DateTime readAt) =>
            reused ?? (fresh == null ? null : new QcAiPassRecord
            {
                Reading = fresh,
                Fingerprint = fingerprint,
                ReadAt = readAt
            });

        /// <summary>
        /// Runs one of the secondary passes, turning a failure into a note rather than an
        /// exception. Losing the exterior band is worth far less than losing the chassis
        /// and odometer readings alongside it.
        /// </summary>
        private static async Task<(QcAiVisionResult?, string)> TryPassAsync(
            Func<Task<QcAiVisionResult?>> pass, string whatFailed)
        {
            try
            {
                var r = await pass();
                return r != null
                    ? (r, "")
                    : (null, $"{whatFailed}: the reader returned nothing for that pass. "
                           + "The other checks are unaffected.");
            }
            catch (Exception ex)
            {
                return (null, $"{whatFailed} ({ex.Message}). The other checks are unaffected.");
            }
        }

        /// <summary>
        /// Combines the three passes into the one result the checks below consume.
        ///
        /// Each field has exactly one owner, and a pass that returned nothing simply does
        /// not write: a missing condition pass leaves the exterior band null rather than
        /// overwriting a good identity reading with blanks, and vice versa. That is why
        /// this copies field by field instead of merging objects.
        /// </summary>
        private static QcAiVisionResult Merge(QcAiVisionResult identity,
                                              QcAiVisionResult? condition,
                                              QcAiVisionResult? provenance)
        {
            // Pass 1 owns identity, and is the base.
            var merged = new QcAiVisionResult
            {
                RegistrationPlate = identity.RegistrationPlate,
                ChassisNumber = identity.ChassisNumber,
                ChassisStencil = identity.ChassisStencil,
                VinPlate = identity.VinPlate,
                OdometerKm = identity.OdometerKm,
                Daylight = identity.Daylight,
                PlateLegible = identity.PlateLegible,
                ChassisPunch = identity.ChassisPunch,
                ChassisReadings = identity.ChassisReadings ?? new List<QcAiChassisReading>(),
                Observations = new List<string>(identity.Observations ?? new List<string>())
            };

            // Pass 2 owns condition.
            if (condition != null)
            {
                merged.ExteriorCondition = condition.ExteriorCondition;
                merged.EngineCondition = condition.EngineCondition;
                merged.TyreCondition = condition.TyreCondition;
                merged.ExteriorReason = condition.ExteriorReason;
                merged.DamageFound = condition.DamageFound ?? new List<string>();
                merged.MissingParts = condition.MissingParts ?? new List<string>();
                if (condition.Observations is { Count: > 0 })
                    merged.Observations.AddRange(condition.Observations);
            }

            // Pass 3 owns provenance.
            if (provenance != null)
            {
                merged.PhotoStamps = provenance.PhotoStamps ?? new List<QcAiPhotoStamp>();
                if (provenance.Observations is { Count: > 0 })
                    merged.Observations.AddRange(provenance.Observations);
            }

            return merged;
        }

        /// <summary>Identifies a photo set, so a changed or added photo forces a re-read.</summary>
        private static string Fingerprint(IReadOnlyDictionary<string, string> photos)
        {
            var joined = PromptVersion + "|" +
                         string.Join("|", photos.OrderBy(p => p.Key, StringComparer.Ordinal)
                                                .Select(p => $"{p.Key}={p.Value}"));
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(joined));
            return Convert.ToHexString(hash);
        }

        // ── Comparisons ───────────────────────────────────────────────────────

        /// <summary>Case, space and punctuation are noise on a stamped plate.</summary>
        private static string Norm(string? s) =>
            Regex.Replace((s ?? string.Empty).ToUpperInvariant(), "[^A-Z0-9]", "");

        /// <summary>
        /// Norm, plus the letters a VIN cannot contain folded onto the digits they
        /// look like. ISO 3779 excludes I, O and Q from vehicle identification
        /// numbers for exactly this reason, so a stored "O" is a typing slip, never
        /// a real character — treating it as a mismatch would flag good vehicles.
        /// Only ever applied to chassis and VIN, never to a number plate, where
        /// those letters are legitimate.
        /// </summary>
        private static string NormVin(string? s) =>
            Norm(s).Replace('I', '1').Replace('O', '0').Replace('Q', '0');

        /// <summary>True when two readings differ only by that I/O/Q confusion.</summary>
        private static bool DiffersOnlyByLookalike(string? a, string? b) =>
            Norm(a) != Norm(b) && NormVin(a) == NormVin(b);

        /// <summary>
        /// A near miss on a long identifier is ambiguous in a way a code path must not
        /// paper over. A swapped vehicle carries a wholly different number; a couple of
        /// characters out is equally consistent with tampering and with the reader
        /// fumbling a pencil rubbing shot at an angle. Those two readings call for
        /// opposite actions, and only a person looking at the photo can tell them
        /// apart — so a near miss is handed over unresolved, with both strings shown,
        /// rather than asserted as a failure the reviewer would take at face value.
        /// </summary>
        private static bool IsNearMiss(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return false;
            var d = Levenshtein(a, b);
            return d > 0 && d <= Math.Max(2, Math.Max(a.Length, b.Length) / 5);
        }

        private static int Levenshtein(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        private const string NearMissNote =
            " That is only a character or two apart, which a misread of a hard photo produces " +
            "as readily as tampering does — open the photo and compare it yourself before deciding.";

        private const string LookalikeNote =
            " The two differ only where I/O/Q could be read as 1/0 — a VIN never contains " +
            "those letters, so this is a typing slip in the record, not a different vehicle.";

        private static void CompareText(QcAiAuditDto o, string key, string label,
                                        string? readValue, string? expected, bool vinLike = false)
        {
            var r = vinLike ? NormVin(readValue) : Norm(readValue);
            var e = vinLike ? NormVin(expected)  : Norm(expected);

            if (e.Length == 0)
            {
                o.Why[key] = $"No {label.ToLowerInvariant()} on record to compare against.";
                return;
            }
            if (r.Length == 0)
            {
                o.Why[key] = $"{label} could not be read from the photos — compare it by eye.";
                return;
            }

            if (r == e)
            {
                o.Cl[key] = "pass";
                o.Why[key] = $"{label} reads {readValue} in the photos, matching the RC."
                           + (vinLike && DiffersOnlyByLookalike(readValue, expected) ? LookalikeNote : "");
            }
            else if (vinLike)
            {
                // A VIN is 17 characters of stamped metal, often shot at an angle or as
                // a pencil rubbing, and this reader gets them wrong often enough that a
                // mismatch here is far more likely to be its error than a swapped
                // vehicle. It reports what it saw; the reviewer makes the call.
                o.Why[key] = $"{label} reads {readValue} in the photos but the RC says {expected}."
                           + (IsNearMiss(r, e) ? NearMissNote
                                               : " Chassis and VIN readings are unreliable on stamped "
                                                 + "or rubbed surfaces — open the photo and compare it yourself.");
            }
            else
            {
                o.Cl[key] = "fail";
                o.Why[key] = $"{label} reads {readValue} in the photos but the RC says {expected}.";
            }
        }

        /// <summary>
        /// The chassis plate and the stencil are read separately and checked against
        /// each other as well as the RC: a plate that matches while the stencil does
        /// not is exactly the pattern re-stamping produces.
        /// </summary>
        private static void CompareChassis(QcAiAuditDto o, QcAiVisionResult read, string? expected)
        {
            const string key = "accChassis";
            var e = NormVin(expected);
            var plate = NormVin(read.ChassisNumber);
            var stencil = NormVin(read.ChassisStencil);

            if (e.Length == 0) { o.Why[key] = "No chassis number on record to compare against."; return; }
            if (plate.Length == 0 && stencil.Length == 0)
            {
                o.Why[key] = "Neither the chassis plate nor the stencil was legible — compare them by eye.";
                return;
            }

            var plateOk = plate.Length > 0 && plate == e;
            var stencilOk = stencil.Length > 0 && stencil == e;
            var seen = new List<string>();
            if (plate.Length > 0) seen.Add($"plate {read.ChassisNumber}");
            if (stencil.Length > 0) seen.Add($"stencil {read.ChassisStencil}");
            var seenText = string.Join(", ", seen);

            if ((plate.Length == 0 || plateOk) && (stencil.Length == 0 || stencilOk))
            {
                var slip = DiffersOnlyByLookalike(read.ChassisNumber, expected)
                        || DiffersOnlyByLookalike(read.ChassisStencil, expected);
                o.Cl[key] = "pass";
                o.Why[key] = $"Read {seenText} — matches RC {expected}." + (slip ? LookalikeNote : "");
            }
            else
            {
                // A chassis mismatch is never asserted, only reported. Measured against
                // real cases this reader gets stamped and rubbed numbers wrong far more
                // often than right, and when it is wrong it tends to be wrong the same
                // way on every photo — so the plate and the stencil agreeing is the same
                // systematic error twice, not corroboration. The check is deliberately
                // one-sided: a match is worth trusting, because hallucinating exactly the
                // right seventeen characters essentially never happens, while a mismatch
                // is far more likely to be a misread than a swapped vehicle. So it passes
                // on agreement and hands over everything it saw on disagreement.
                var agree = plate.Length > 0 && stencil.Length > 0 && plate == stencil;
                o.Why[key] = $"Read {seenText} — RC says {expected}. "
                           + (agree
                                ? "Both photos read the same, but that is one reader making one mistake twice as easily as it is a real difference. "
                                : "The two photos did not even read the same as each other, so the reading is unreliable. ")
                           + "Chassis numbers are the least reliable thing here — open the photos and compare them yourself.";
            }
        }

        /// <summary>
        /// Whether every photo showing a chassis number shows the SAME chassis number.
        ///
        /// This is a different question from "does it match the RC", and it is the one
        /// that catches a photo of another vehicle. A misread is wrong in small ways and
        /// tends to be wrong the same way on every photo, because it is one reader making
        /// one mistake; a photo of a different vehicle disagrees with its neighbours
        /// wholesale. So agreement is uninformative, and disagreement is worth naming.
        ///
        /// Like CompareChassis this never asserts a swap — it names which photo differs
        /// and what each one read, and leaves the conclusion to the reviewer.
        /// </summary>
        private static void CompareChassisAcrossPhotos(
            QcAiAuditDto o, List<QcAiChassisReading> readings, string? expected)
        {
            const string key = "accChassisPhotos";

            // A chassis/VIN is 17 characters. A reading materially shorter than the RC's
            // is a partial read of a dirty or half-lit stamping, not a different vehicle,
            // and comparing it would manufacture a mismatch out of a bad photo. Those are
            // reported but never used to accuse the case.
            var expectedLen = NormVin(expected).Length;
            var minComplete = expectedLen > 0 ? Math.Max(expectedLen - 2, 10) : 15;

            var legible = (readings ?? new List<QcAiChassisReading>())
                .Where(r => !string.IsNullOrWhiteSpace(r.PhotoKey) && NormVin(r.Characters).Length > 0)
                .ToList();

            var complete = legible.Where(r => NormVin(r.Characters).Length >= minComplete).ToList();
            var partial = legible.Where(r => NormVin(r.Characters).Length < minComplete).ToList();

            var partialNote = partial.Count == 0
                ? ""
                : " Partial reads not used for the comparison: "
                  + string.Join("; ", partial.Select(r => $"{Pretty(r.PhotoKey)} ({r.Characters!.Trim()})")) + ".";

            if (complete.Count == 0)
            {
                Unresolved(o, key,
                    "No photo showed a chassis number completely enough to cross-check." + partialNote);
                return;
            }

            if (complete.Count == 1)
            {
                Unresolved(o, key,
                    $"Only one photo ({Pretty(complete[0].PhotoKey)}) showed a full chassis number, "
                    + "so there is nothing to cross-check it against." + partialNote);
                return;
            }

            // Group by the normalised characters: lookalike folding is applied here too,
            // so O/0 and I/1 differences do not read as two different vehicles.
            var groups = complete
                .GroupBy(r => NormVin(r.Characters))
                .OrderByDescending(g => g.Count())
                .ToList();

            var detail = string.Join("; ", complete.Select(
                r => $"{Pretty(r.PhotoKey)} reads {r.Characters!.Trim()}"));

            if (groups.Count == 1)
            {
                var matchesRc = expectedLen > 0 && groups[0].Key == NormVin(expected);
                o.Cl[key] = "pass";
                o.Status[key] = "resolved";
                o.Why[key] = $"All {complete.Count} photos showing a full chassis number read the same"
                           + (expectedLen > 0
                                ? matchesRc ? $", and it matches RC {expected}." : $", though RC says {expected}."
                                : ".")
                           + $" {detail}." + partialNote;
                return;
            }

            // More than one distinct FULL number across the photos. Name the minority
            // ones: those are the photos to open first.
            var odd = groups.Skip(1).SelectMany(g => g).Select(r => Pretty(r.PhotoKey)).ToList();

            o.Cl[key] = "fail";
            o.Status[key] = "resolved";
            o.Why[key] = $"Photos disagree on the chassis number \u2014 {string.Join(" and ", odd)} "
                       + $"do{(odd.Count == 1 ? "es" : "")} not match the others"
                       + (expectedLen > 0 ? $" (RC says {expected})" : "")
                       + $". {detail}. One of these photos may be of a different vehicle \u2014 open them "
                       + "and compare before approving." + partialNote;
        }

        /// <summary>
        /// Whether two overlay place names describe the same site.
        ///
        /// Not string equality: an overlay prints a multi-line address, so one yard
        /// yields "Mandapeta Road, Nizamabad" on one photo and "Nizamabad, Telangana"
        /// on the next. Any shared word of four characters or more is taken as the same
        /// place \u2014 loose on purpose, because the cost of flagging an honest case is
        /// higher than the cost of missing a subtle relabelling, and a genuinely
        /// different town shares no such word.
        /// </summary>
        private static bool SamePlace(string a, string b)
        {
            var ta = PlaceTokens(a);
            var tb = PlaceTokens(b);
            if (ta.Count == 0 || tb.Count == 0) return true;   // nothing to compare on
            return ta.Overlaps(tb);
        }

        /// <summary>
        /// Words that two overlay place names can share without being the same place.
        ///
        /// The state name is the important entry: "Hyderabad, Telangana" and
        /// "Nizamabad, Telangana" share it, and matching on that alone reported two
        /// towns 150 km apart as the same site. Districts and states are listed as their
        /// individual words because the tokeniser splits on spaces.
        /// </summary>
        private static readonly HashSet<string> PlaceNoise =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Generic address furniture
                "road", "street", "cross", "main", "nagar", "colony", "layout", "phase",
                "india", "state", "district", "near", "opposite", "circle", "chowk",
                "village", "mandal", "taluk", "tehsil", "post", "block", "sector",

                // States and union territories, so a shared state is not a shared place
                "andhra", "pradesh", "arunachal", "assam", "bihar", "chhattisgarh", "goa",
                "gujarat", "haryana", "himachal", "jharkhand", "karnataka", "kerala",
                "madhya", "maharashtra", "manipur", "meghalaya", "mizoram", "nagaland",
                "odisha", "orissa", "punjab", "rajasthan", "sikkim", "tamil", "nadu",
                "telangana", "tripura", "uttar", "uttarakhand", "bengal", "west",
                "andaman", "nicobar", "chandigarh", "dadra", "haveli", "daman", "diu",
                "delhi", "jammu", "kashmir", "ladakh", "lakshadweep", "puducherry",
            };

        private static HashSet<string> PlaceTokens(string place) =>
            Regex.Split(place ?? "", @"[^A-Za-z0-9]+")
                 .Where(t => t.Length >= 4 && !PlaceNoise.Contains(t))
                 .Select(t => t.ToUpperInvariant())
                 .ToHashSet();

        /// <summary>"ChassisStencilTrace" -> "chassis stencil trace", for evidence text.</summary>
        private static string Pretty(string? photoKey)
        {
            if (string.IsNullOrWhiteSpace(photoKey)) return "an unnamed photo";
            var spaced = Regex.Replace(photoKey!, "(?<=[a-z0-9])(?=[A-Z])", " ");
            return spaced.ToLowerInvariant();
        }

        private static void CompareOdometer(QcAiAuditDto o, long? readKm, double? recorded)
        {
            const string key = "accOdo";
            if (recorded is null or <= 0) { Unresolved(o, key, "AVO recorded no odometer reading to compare against."); return; }
            if (readKm is null) { Unresolved(o, key, $"Odometer could not be read from the photo — AVO recorded {recorded:N0} km."); return; }

            var declared = (long)Math.Round(recorded.Value);

            // The two ways this reading goes wrong both produce a number far shorter
            // than the truth: picking the trip meter or clock off a cluster that shows
            // four numbers at once, or truncating a glare-washed dial part way through
            // (156 off a dial reading 156361). Tampering does not look like that — a
            // wound-back odometer still shows a full, plausible figure for the
            // vehicle's age. Treating a fragment as a rollback accuses a sound case of
            // fraud, which is the more expensive error by far, so a reading that
            // short is reported as unread with what it saw shown rather than judged.
            var readDigits = readKm.Value.ToString().Length;
            var declaredDigits = declared.ToString().Length;
            var isPrefix = declared.ToString().StartsWith(readKm.Value.ToString(), StringComparison.Ordinal);

            if (readKm.Value == 0 || (readDigits <= declaredDigits - 2) || (isPrefix && readKm.Value != declared))
            {
                Unresolved(o, key,
                    $"Odometer photo appears to read {readKm.Value:N0} km against the " +
                    $"{declared:N0} km AVO recorded — too few digits to be the lifetime total, " +
                    "so this is a partial read of the dial or another number on the cluster " +
                    "rather than a rollback. Check the odometer photo yourself.");
                return;
            }

            if (readKm.Value == declared)
            {
                o.Cl[key] = "pass";
                o.Status[key] = "resolved";
                o.Why[key] = $"Odometer photo reads {readKm.Value:N0} km, matching the {declared:N0} km AVO recorded.";
            }
            else
            {
                o.Cl[key] = "fail";
                o.Status[key] = "resolved";
                o.Why[key] = $"Odometer photo reads {readKm.Value:N0} km but AVO recorded {declared:N0} km. " +
                             "A single mis-read digit looks the same as a rollback here — check the photo before acting.";
            }
        }

        private static void Judgement(QcAiAuditDto o, string key, string? verdict, string passWhy, string failWhy)
        {
            if (verdict == "pass") { o.Cl[key] = "pass"; o.Status[key] = "resolved"; o.Why[key] = passWhy; }
            else if (verdict == "fail") { o.Cl[key] = "fail"; o.Status[key] = "resolved"; o.Why[key] = failWhy; }
            else Unresolved(o, key, "Could not be judged from the photos — open them to confirm.");
        }

        /// <summary>
        /// A condition verdict taken from the photos rather than the AVO's typed answer.
        ///
        /// These cards used to restate what the AVO had entered, which told a reviewer
        /// nothing they could not already see on the inspection page. The reader now
        /// judges the panels, the bay and the tyres, and the AVO's entry is quoted
        /// beside it — so a disagreement is visible instead of invisible.
        ///
        /// Each card has its own vocabulary (minor/major on exterior, replacement on
        /// tyres), hence the three band arguments rather than a shared enum.
        /// </summary>
        private static void CompareCondition(QcAiAuditDto o, string key, string label,
                                             string? seen, string? recorded,
                                             string good, string average, string poor,
                                             string? basis = null)
        {
            var avo = (recorded ?? "").Trim();
            var avoNote = avo.Length > 0
                ? $" AVO recorded {avo.ToUpperInvariant()}."
                : " AVO recorded nothing for it.";

            var verdict = seen switch
            {
                "good"    => good,
                "average" => average,
                "poor"    => poor,
                _         => null
            };

            if (verdict is null)
            {
                Unresolved(o, key,
                    $"{label} condition could not be judged from the photos \u2014 open them and decide.{avoNote}");
                return;
            }

            o.Cl[key] = verdict;
            o.Status[key] = "resolved";

            var seenWord = seen!.ToUpperInvariant();
            var agrees = avo.Length > 0 &&
                         string.Equals(NormaliseBand(avo), seen, StringComparison.OrdinalIgnoreCase);

            // What the band was actually based on, so "AVERAGE" is not an assertion the
            // reviewer has to take on trust.
            var basisNote = string.IsNullOrWhiteSpace(basis) ? "" : $" Seen: {basis!.Trim().TrimEnd('.')}.";

            o.Why[key] = avo.Length == 0
                ? $"Photos show {label.ToLowerInvariant()} condition as {seenWord}.{avoNote}{basisNote}"
                : agrees
                    ? $"Photos show {label.ToLowerInvariant()} condition as {seenWord}, matching the AVO's {avo.ToUpperInvariant()}.{basisNote}"
                    : $"Photos show {label.ToLowerInvariant()} condition as {seenWord}, against the AVO's {avo.ToUpperInvariant()} \u2014 check the photos before approving.{basisNote}";
        }

        /// <summary>Maps an AVO free-text condition onto the reader's three bands.</summary>
        private static string? NormaliseBand(string value) =>
            value.Trim().ToUpperInvariant() switch
            {
                "GOOD" or "EXCELLENT" => "good",
                "AVERAGE" or "FAIR"   => "average",
                "POOR" or "BAD" or "DAMAGED" => "poor",
                _ => null
            };

        /// <summary>
        /// The reader looked at this and could not settle it. Distinct from saying
        /// nothing at all, which the UI reads as "not checked yet".
        /// </summary>
        private static void Unresolved(QcAiAuditDto o, string key, string why)
        {
            o.Status[key] = "unresolved";
            o.Why[key] = why;
        }

        // ── Stamp consistency ─────────────────────────────────────────────────

        private void EvaluateStamps(QcAiAuditDto o, List<QcAiPhotoStamp> stamps,
                                    DateTime? declaredDate, string? declaredPlace,
                                    IReadOnlyCollection<string> allPhotoKeys)
        {
            var located = stamps.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
            var places = stamps.Select(s => (s.PlaceName ?? "").Trim())
                               .Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Photos carrying no overlay at all. These used to drop out of both checks
            // silently, so a case could pass on nineteen consistent photos while the
            // twentieth \u2014 the one with no stamp \u2014 was never mentioned.
            var stamped = stamps.Select(s => (s.PhotoKey ?? "").Trim())
                                .Where(k => k.Length > 0)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unstamped = allPhotoKeys.Where(k => !stamped.Contains(k)).ToList();
            var unstampedNote = unstamped.Count == 0
                ? ""
                : $" {unstamped.Count} photo(s) carry no capture overlay and cannot be placed: "
                  + $"{string.Join(", ", unstamped.Take(6).Select(Pretty))}"
                  + (unstamped.Count > 6 ? $", +{unstamped.Count - 6} more." : ".");

            // Outliers are measured against the biggest cluster, not against whichever
            // photo happened to be first: anchoring on photo one made the odd photo the
            // reference whenever it sorted first, and reported every honest photo as far
            // from it.
            // Outliers are measured against the biggest cluster, not against whichever
            // photo happened to be first: anchoring on photo one made the odd photo the
            // reference whenever it sorted first, and reported every honest photo as far
            // from it.
            var outliers = new List<string>();
            var outlierKeys = new List<string>();
            double worst = 0;
            if (located.Count >= 2)
            {
                var anchor = located
                    .OrderByDescending(a => located.Count(b =>
                        Haversine(a.Latitude!.Value, a.Longitude!.Value,
                                  b.Latitude!.Value, b.Longitude!.Value) <= SameSiteMetres))
                    .First();

                foreach (var p in located)
                {
                    var d = Haversine(anchor.Latitude!.Value, anchor.Longitude!.Value,
                                      p.Latitude!.Value, p.Longitude!.Value);
                    worst = Math.Max(worst, d);
                    if (d > SameSiteMetres)
                    {
                        outliers.Add(Pretty(p.PhotoKey));
                        outlierKeys.Add((p.PhotoKey ?? "").Trim());
                    }
                }
            }

            var majorityPlace = stamps.Select(st => (st.PlaceName ?? "").Trim())
                                      .Where(p => p.Length > 0)
                                      .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
                                      .OrderByDescending(g => g.Count())
                                      .Select(g => g.Key)
                                      .FirstOrDefault();

            var placeOutliers = stamps
                .Where(st => !string.IsNullOrWhiteSpace(st.PlaceName)
                          && majorityPlace != null
                          && !SamePlace(st.PlaceName!, majorityPlace))
                .Select(st => $"{Pretty(st.PhotoKey)} (\"{st.PlaceName!.Trim()}\")")
                .ToList();

            // Does a coordinate outlier also NAME somewhere else? A photo genuinely taken
            // elsewhere disagrees on both; a mistyped digit disagrees only on the number.
            var outlierAlsoNamesElsewhere = outlierKeys.Any(k =>
                stamps.Any(st => string.Equals((st.PhotoKey ?? "").Trim(), k, StringComparison.OrdinalIgnoreCase)
                              && !string.IsNullOrWhiteSpace(st.PlaceName)
                              && majorityPlace != null
                              && !SamePlace(st.PlaceName!, majorityPlace)));

            var declaredMismatch = !string.IsNullOrWhiteSpace(declaredPlace)
                                && majorityPlace != null
                                && !SamePlace(majorityPlace, declaredPlace!);

            if (outliers.Count > 0 && outlierAlsoNamesElsewhere)
            {
                // Both the coordinates and the place name disagree. That is the shape of a
                // photo taken somewhere else.
                o.Cl["accPhotoLoc"] = "fail";
                o.Status["accPhotoLoc"] = "resolved";
                o.Why["accPhotoLoc"] =
                    $"{string.Join(", ", outliers)} were captured up to {worst / 1000:F1} km from the rest"
                    + (places.Count > 1 ? $" ({string.Join(", ", places)})." : ".")
                    + unstampedNote;
            }
            else if (outliers.Count > 0)
            {
                // The coordinates disagree but every photo names the same place. A single
                // mistyped digit moves a photo kilometres, so this is reported for a human
                // to settle rather than failed \u2014 the coordinates are transcribed off the
                // image by the reader, not measured, and are not reliable enough to accuse
                // a case on their own.
                Unresolved(o, "accPhotoLoc",
                    $"{string.Join(", ", outliers)} carry coordinates up to {worst / 1000:F1} km from the "
                    + $"rest, but every photo names the same place (\"{majorityPlace}\"). The coordinates "
                    + "are read off the image and a single wrong digit moves a photo kilometres, so open "
                    + "them and confirm." + unstampedNote);
            }
            else if (located.Count >= 2 && placeOutliers.Count > 0)
            {
                // Coordinates put every photo at the same spot, so a differing label is a
                // naming quirk \u2014 one yard is labelled several ways by a map provider.
                o.Cl["accPhotoLoc"] = "pass";
                o.Status["accPhotoLoc"] = "resolved";
                o.Why["accPhotoLoc"] =
                    $"All {located.Count} stamped photos are within {worst:F0} m of each other, so they are "
                    + $"the same site, though {string.Join(", ", placeOutliers)} label it differently."
                    + unstampedNote;
            }
            else if (placeOutliers.Count > 0)
            {
                // No coordinates to settle it either way.
                Unresolved(o, "accPhotoLoc",
                    $"{string.Join(", ", placeOutliers)} name a different place from the rest "
                    + $"(\"{majorityPlace}\"), and no coordinates were readable to settle whether that is "
                    + "the same site labelled differently. Compare the backgrounds yourself."
                    + unstampedNote);
            }
            else if (declaredMismatch)
            {
                o.Cl["accPhotoLoc"] = "fail";
                o.Status["accPhotoLoc"] = "resolved";
                o.Why["accPhotoLoc"] =
                    $"Photos were captured at \"{majorityPlace}\" but the inspection was declared at "
                    + $"\"{declaredPlace}\" \u2014 confirm where this vehicle was actually seen."
                    + unstampedNote;
            }
            else if (located.Count >= 2)
            {
                o.Cl["accPhotoLoc"] = "pass";
                o.Status["accPhotoLoc"] = "resolved";
                o.Why["accPhotoLoc"] = $"All {located.Count} stamped photos are within {worst:F0} m of each other"
                                     + (majorityPlace != null ? $" at \"{majorityPlace}\"" : "")
                                     + (string.IsNullOrWhiteSpace(declaredPlace) ? "." : $", matching the declared \"{declaredPlace}\".")
                                     + unstampedNote;
            }
            else if (places.Count == 1)
            {
                o.Cl["accPhotoLoc"] = "pass";
                o.Status["accPhotoLoc"] = "resolved";
                o.Why["accPhotoLoc"] = $"Every stamped photo shows \"{places[0]}\""
                                     + (string.IsNullOrWhiteSpace(declaredPlace) ? "." : $" (declared: \"{declaredPlace}\").")
                                     + unstampedNote;
            }
            else
            {
                Unresolved(o, "accPhotoLoc",
                    "Location could not be verified: no photo carries a readable capture location."
                    + unstampedNote);
            }

            // ── Timestamps ────────────────────────────────────────────────────
            var times = stamps.Select(s => ParseStamp(s.CapturedAt))
                              .Where(d => d.HasValue).Select(d => d!.Value).ToList();

            if (!declaredDate.HasValue)
            {
                o.Why["accGPS"] = "AVO declared no inspection date to compare the photo timestamps against.";
            }
            else if (times.Count == 0)
            {
                o.Why["accGPS"] = $"AVO declared {declaredDate:dd MMM yyyy} but no photo carries a readable timestamp.";
            }
            else
            {
                var off = times.Where(t => t.Date != declaredDate.Value.Date).ToList();
                if (off.Count == 0)
                {
                    var span = times.Max() - times.Min();
                    var unreadable = stamps.Count - times.Count;
                    o.Cl["accGPS"] = "pass";
                    o.Status["accGPS"] = "resolved";
                    o.Why["accGPS"] = $"All {times.Count} stamped photos were captured on " +
                                      $"{declaredDate:dd MMM yyyy}, within {span.TotalMinutes:F0} minutes of each other." +
                                      // Photos whose time could not be read are not evidence of anything, and
                                      // saying "all of them" while quietly excluding some was the flaw here.
                                      (unreadable > 0
                                          ? $" {unreadable} further photo(s) carry no readable timestamp."
                                          : "");
                }
                else
                {
                    var days = string.Join(", ", off.Select(d => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).Distinct());
                    o.Cl["accGPS"] = "fail";
                    o.Status["accGPS"] = "resolved";
                    o.Why["accGPS"] = $"{off.Count} of {times.Count} photos were captured on {days}, " +
                                      $"not the declared inspection date {declaredDate:dd MMM yyyy}.";
                }
            }
        }

        /// <summary>Overlay text varies by camera app, so several shapes are accepted.</summary>
        private static DateTime? ParseStamp(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var cleaned = Regex.Replace(s, @"\s*GMT\s*[+-]\d{2}:?\d{2}\s*$", "").Trim();

            // Drop a leading weekday before parsing.
            //
            // .NET validates a "dddd" token against the date, so "Friday, 14.07.2026"
            // is rejected outright because 14 July 2026 is a Tuesday. The reader adds a
            // weekday of its own accord even where the overlay prints none, and gets it
            // wrong, which was silently discarding every timestamp on those cases. The
            // numeric date is unambiguous on its own, so the weekday is simply removed
            // rather than being allowed to veto it.
            cleaned = Regex.Replace(
                cleaned,
                @"^(mon|tues|wednes|thurs|fri|satur|sun)day\s*,?\s*",
                "", RegexOptions.IgnoreCase).Trim();
            cleaned = Regex.Replace(
                cleaned,
                @"^(mon|tue|wed|thu|fri|sat|sun)\s*,?\s+",
                "", RegexOptions.IgnoreCase).Trim();

            // Overlay wording varies by camera app: dotted and slashed dates, an
            // optional weekday, 12- or 24-hour clocks, seconds present or not.
            string[] formats =
            {
                "dd/MM/yyyy hh:mm:ss tt", "dd/MM/yyyy hh:mm tt", "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                "dd.MM.yyyy hh:mm:ss tt", "dd.MM.yyyy hh:mm tt", "dd.MM.yyyy HH:mm:ss",
                "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
                "dd-MM-yyyy hh:mm:ss tt", "dd-MM-yyyy hh:mm tt", "dd-MM-yyyy",
                // Kept for overlays whose weekday IS correct; the strip above means
                // nothing depends on them any more.
                "dddd, dd.MM.yyyy hh:mm:ss tt", "dddd, dd.MM.yyyy hh:mm tt", "dddd, dd.MM.yyyy",
                "dddd, dd/MM/yyyy hh:mm:ss tt", "dddd, dd/MM/yyyy hh:mm tt", "dddd, dd/MM/yyyy",
                "MMM dd, yyyy hh:mm:ss tt", "MMM dd, yyyy hh:mm tt", "MMM dd, yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd"
            };
            // Day-first shapes are matched before any general parse: these overlays are
            // Indian, and invariant culture would read 06.08.2026 as 8 June rather than
            // 6 August — a swap that would silently move a photo to the wrong day.
            if (DateTime.TryParseExact(cleaned, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out var exact)) return exact;

            // Only unambiguous shapes (ISO, month-name) reach the general parser.
            return Regex.IsMatch(cleaned, @"^\d{4}-\d{2}-\d{2}") || Regex.IsMatch(cleaned, @"[A-Za-z]{3}")
                ? DateTime.TryParse(cleaned, CultureInfo.InvariantCulture,
                      DateTimeStyles.AllowWhiteSpaces, out var loose) ? loose : null
                : null;
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6_371_000;
            double Rad(double d) => d * Math.PI / 180;
            var dLat = Rad(lat2 - lat1);
            var dLon = Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static bool IsVideo(string url)
        {
            var u = url.ToLowerInvariant();
            return u.Contains(".mp4") || u.Contains(".mov") || u.Contains(".avi")
                || u.Contains(".mkv") || u.Contains(".webm");
        }
    }
}
