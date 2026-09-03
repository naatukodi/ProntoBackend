using System.Globalization;
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

        /// <summary>
        /// Slots where fine characters have to be read, so they justify full detail.
        /// Everything else is sent at low detail: enough for the overlay stamp and
        /// overall lighting, which is what the remaining checks need. This is what
        /// keeps cost roughly flat whether a case has 17 photos or 30.
        /// </summary>
        private static readonly HashSet<string> CloseUpSlots = new(StringComparer.OrdinalIgnoreCase)
        {
            "Odometer", "InstrumentCluster", "Dashboard", "DashboardCloseup",
            "ChassisVerification", "ChassisStencilTrace", "ChassisImprint",
            "ChassisNumberPlate", "ChassisNumber", "Chassis",
            "VinPlate", "VIN", "RearViewTailgate", "FrontViewGrille"
        };

        /// <summary>Two photos further apart than this are not the same inspection site.</summary>
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

        public QcVisionAuditService(CosmosClient cosmos, IChatGptRepository ai, IConfiguration config)
        {
            _cosmos = cosmos;
            _ai = ai;
            _dbId = config["Cosmos:DatabaseId"] ?? "ValuationsDb";
            _containerId = config["Cosmos:ContainerId"] ?? "Valuations";
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

            // The page reads on open, so without this every visit would pay to read the
            // same twenty images again. The fingerprint covers the photo set: change a
            // photo and the stored answer no longer applies, so it is read afresh.
            var fingerprint = Fingerprint(photos);
            var stored = doc.QcAiAudit;
            if (!force && stored != null && stored.PhotoFingerprint == fingerprint)
            {
                outp.Cl = stored.Cl;
                outp.Why = stored.Why;
                outp.Observations = stored.Observations;
                outp.Readings = stored.Readings;
                outp.ReadAt = stored.ReadAt;
                outp.Cached = true;
                return outp;
            }

            QcAiVisionResult? read;
            try
            {
                read = await _ai.ReadInspectionPhotosAsync(photos, CloseUpSlots, ct);
            }
            catch (Exception ex)
            {
                // A failed read must leave every check unresolved rather than passing
                // or failing it — the reviewer needs to know nothing was verified.
                outp.Error = $"Photo reading failed: {ex.Message}";
                return outp;
            }

            if (read == null)
            {
                outp.Error = "The photo reader returned nothing.";
                return outp;
            }

            outp.Observations = read.Observations;

            var vd = doc.VehicleDetails;
            var ins = doc.InspectionDetails;

            CompareText(outp, "accReg", "Number plate", read.RegistrationPlate,
                        vd?.RegistrationNumber ?? doc.VehicleNumber);

            CompareChassis(outp, read, vd?.ChassisNumber);

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

            EvaluateStamps(outp, read.PhotoStamps, ins?.DateOfInspection, ins?.InspectionLocation);

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
                TotalPhotos   = photos.Count
            };
            outp.ReadAt = DateTime.UtcNow;

            await PersistAsync(doc, outp, fingerprint, ct);

            return outp;
        }

        /// <summary>
        /// Keeps the reading on the case. A failure here costs a repeat call next time
        /// but nothing else, so it never takes down an audit the reviewer can already see.
        /// </summary>
        private async Task PersistAsync(ValuationDocument doc, QcAiAuditDto outp,
                                        string fingerprint, CancellationToken ct)
        {
            try
            {
                doc.QcAiAudit = new QcAiAuditRecord
                {
                    Cl = outp.Cl,
                    Why = outp.Why,
                    Observations = outp.Observations,
                    Readings = outp.Readings,
                    PhotoFingerprint = fingerprint,
                    ReadAt = outp.ReadAt ?? DateTime.UtcNow
                };

                await Container.ReplaceItemAsync(doc, doc.id,
                    new PartitionKey(doc.CompositeKey), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                outp.Observations.Add($"[note] Reading could not be saved, so the next open will read again: {ex.Message}");
            }
        }

        /// <summary>Identifies a photo set, so a changed or added photo forces a re-read.</summary>
        private static string Fingerprint(IReadOnlyDictionary<string, string> photos)
        {
            var joined = string.Join("|", photos.OrderBy(p => p.Key, StringComparer.Ordinal)
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

        private static void CompareOdometer(QcAiAuditDto o, long? readKm, double? recorded)
        {
            const string key = "accOdo";
            if (recorded is null or <= 0) { o.Why[key] = "AVO recorded no odometer reading to compare against."; return; }
            if (readKm is null) { o.Why[key] = $"Odometer could not be read from the photo — AVO recorded {recorded:N0} km."; return; }

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
                o.Why[key] = $"Odometer photo appears to read {readKm.Value:N0} km against the " +
                             $"{declared:N0} km AVO recorded — too few digits to be the lifetime total, " +
                             "so this is a partial read of the dial or another number on the cluster " +
                             "rather than a rollback. Check the odometer photo yourself.";
                return;
            }

            if (readKm.Value == declared)
            {
                o.Cl[key] = "pass";
                o.Why[key] = $"Odometer photo reads {readKm.Value:N0} km, matching the {declared:N0} km AVO recorded.";
            }
            else
            {
                o.Cl[key] = "fail";
                o.Why[key] = $"Odometer photo reads {readKm.Value:N0} km but AVO recorded {declared:N0} km. " +
                             "A single mis-read digit looks the same as a rollback here — check the photo before acting.";
            }
        }

        private static void Judgement(QcAiAuditDto o, string key, string? verdict,
                                      string passWhy, string failWhy)
        {
            if (verdict == "pass") { o.Cl[key] = "pass"; o.Why[key] = passWhy; }
            else if (verdict == "fail") { o.Cl[key] = "fail"; o.Why[key] = failWhy; }
            else o.Why[key] = "Could not be judged from the photos — open them to confirm.";
        }

        // ── Stamp consistency ─────────────────────────────────────────────────

        private void EvaluateStamps(QcAiAuditDto o, List<QcAiPhotoStamp> stamps,
                                    DateTime? declaredDate, string? declaredPlace)
        {
            var located = stamps.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
            var places = stamps.Select(s => (s.PlaceName ?? "").Trim())
                               .Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (located.Count >= 2)
            {
                // Coordinates rather than place names: the same yard can be labelled
                // two different ways by the map provider, which would read as a
                // mismatch it is not.
                var far = 0;
                double worst = 0;
                for (var i = 1; i < located.Count; i++)
                {
                    var d = Haversine(located[0].Latitude!.Value, located[0].Longitude!.Value,
                                      located[i].Latitude!.Value, located[i].Longitude!.Value);
                    worst = Math.Max(worst, d);
                    if (d > SameSiteMetres) far++;
                }

                if (far == 0)
                {
                    o.Cl["accPhotoLoc"] = "pass";
                    o.Why["accPhotoLoc"] = $"All {located.Count} stamped photos are within {worst:F0} m of each other" +
                                           (places.Count == 1 ? $" at \"{places[0]}\"." : ".");
                }
                else
                {
                    o.Cl["accPhotoLoc"] = "fail";
                    o.Why["accPhotoLoc"] = $"{far} of {located.Count} stamped photos were captured up to " +
                                           $"{worst / 1000:F1} km away from the others" +
                                           (places.Count > 1 ? $" ({string.Join(", ", places)})." : ".");
                }
            }
            else if (places.Count > 1)
            {
                // Without coordinates this cannot be settled. An overlay prints a
                // multi-line address and one yard legitimately yields "Mandapeta Road"
                // on one photo and the locality name on the next, so differing strings
                // are not evidence of differing places. Show them and let the reviewer
                // judge rather than failing a case on a naming quirk.
                o.Why["accPhotoLoc"] = "Photos are stamped with different place names — " +
                                       $"{string.Join(", ", places)}. No coordinates were readable, and " +
                                       "one site can be labelled several ways, so compare the backgrounds yourself.";
            }
            else if (places.Count == 1)
            {
                o.Cl["accPhotoLoc"] = "pass";
                o.Why["accPhotoLoc"] = $"Every stamped photo shows \"{places[0]}\"" +
                                       (string.IsNullOrWhiteSpace(declaredPlace) ? "." : $" (declared: \"{declaredPlace}\").");
            }
            else
            {
                o.Why["accPhotoLoc"] = "No photo carries a capture location — compare the backgrounds visually.";
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
                    o.Cl["accGPS"] = "pass";
                    o.Why["accGPS"] = $"All {times.Count} stamped photos were captured on " +
                                      $"{declaredDate:dd MMM yyyy}, within {span.TotalMinutes:F0} minutes of each other.";
                }
                else
                {
                    var days = string.Join(", ", off.Select(d => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).Distinct());
                    o.Cl["accGPS"] = "fail";
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

            // Overlay wording varies by camera app: dotted and slashed dates, an
            // optional weekday, 12- or 24-hour clocks, seconds present or not.
            string[] formats =
            {
                "dd/MM/yyyy hh:mm:ss tt", "dd/MM/yyyy hh:mm tt", "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                "dd.MM.yyyy hh:mm:ss tt", "dd.MM.yyyy hh:mm tt", "dd.MM.yyyy HH:mm:ss",
                "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
                "dd-MM-yyyy hh:mm:ss tt", "dd-MM-yyyy hh:mm tt", "dd-MM-yyyy",
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
