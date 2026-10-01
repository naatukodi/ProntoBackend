using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Valuation.Api.Services
{
    /// <summary>
    /// Nightly copy of every Azure Table the backend uses (workflows, users, roles, states,
    /// lead history, ...) into the private "backups" blob container. Azure has no built-in
    /// backup for Table storage; the cases themselves live in Cosmos, which has its own
    /// continuous 7-day point-in-time restore.
    ///
    /// Layout, one folder per night:
    ///   backups/tables/{yyyy-MM-dd}/{storage account}/{table}.jsonl   one entity per line
    ///   backups/tables/{yyyy-MM-dd}/_SUCCESS                          row counts; written last
    /// Every property is stored with its Edm type ("Edm.Int64", "Edm.DateTime", ...), so a
    /// restore can write back exactly what was read. Folders older than RetentionDays are
    /// deleted after each successful night.
    ///
    /// It wakes every hour and runs when it is past 02:00 India time and today's _SUCCESS is
    /// missing, so a restart at the wrong moment only delays a copy, never skips it — and the
    /// first copy happens a few minutes after a deploy.
    ///
    /// Everything is caught and logged: a failed night must never take the API down.
    /// </summary>
    public sealed class TableBackupService : BackgroundService
    {
        private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);
        private const int RunAfterHourIst = 2;
        private const string Prefix = "tables/";
        private const string SuccessMarker = "_SUCCESS";

        private readonly string[] _sourceConnections;
        private readonly BlobContainerClient _container;
        private readonly int _retentionDays;
        private readonly ILogger<TableBackupService> _logger;

        public TableBackupService(IConfiguration config, ILogger<TableBackupService> logger)
        {
            _logger = logger;

            // The tables are spread over two storage accounts: the workflow/user tables on
            // the TableStorage connection, and States on the blob account's connection.
            _sourceConnections = new[]
                {
                    config.GetConnectionString("TableStorage"),
                    config["Blob:ConnectionString"],
                }
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .ToArray();

            var blobConn = config["Blob:ConnectionString"]
                ?? throw new InvalidOperationException("Missing Blob:ConnectionString");
            _container = new BlobContainerClient(blobConn, config["TableBackup:Container"] ?? "backups");
            _retentionDays = config.GetValue("TableBackup:RetentionDays", 30);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the API finish starting before the first check.
            if (!await DelayAsync(TimeSpan.FromMinutes(5), stoppingToken)) return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunIfDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Table backup failed; it will be retried within the hour.");
                }

                if (!await DelayAsync(TimeSpan.FromHours(1), stoppingToken)) return;
            }
        }

        private async Task RunIfDueAsync(CancellationToken ct)
        {
            var nowIst = DateTimeOffset.UtcNow.ToOffset(IndiaOffset);
            if (nowIst.Hour < RunAfterHourIst) return;

            var day = nowIst.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var marker = _container.GetBlobClient($"{Prefix}{day}/{SuccessMarker}");

            // Private: the storage account allows public containers (for case photos), so the
            // backup container must say so explicitly.
            await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            if (await marker.ExistsAsync(ct)) return;

            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var seenAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var connection in _sourceConnections)
            {
                var service = new TableServiceClient(connection);
                if (!seenAccounts.Add(service.AccountName)) continue;

                await foreach (var table in service.QueryAsync(cancellationToken: ct))
                {
                    var rows = await CopyTableAsync(service, table.Name, $"{Prefix}{day}/{service.AccountName}/{table.Name}.jsonl", ct);
                    counts[$"{service.AccountName}/{table.Name}"] = rows;
                }
            }

            var summary = JsonSerializer.Serialize(new
            {
                completedAtUtc = DateTimeOffset.UtcNow,
                tables = counts,
            }, new JsonSerializerOptions { WriteIndented = true });
            await marker.UploadAsync(BinaryData.FromString(summary), overwrite: true, ct);

            _logger.LogInformation("Table backup {Day} done: {Tables} tables, {Rows} rows.",
                day, counts.Count, counts.Values.Sum());

            await DeleteOldCopiesAsync(DateOnly.FromDateTime(nowIst.Date).AddDays(-_retentionDays), ct);
        }

        private async Task<int> CopyTableAsync(TableServiceClient service, string tableName, string blobName, CancellationToken ct)
        {
            var table = service.GetTableClient(tableName);
            var rows = 0;

            using var buffer = new MemoryStream();
            using (var writer = new StreamWriter(buffer, new UTF8Encoding(false), leaveOpen: true))
            {
                await foreach (var entity in table.QueryAsync<TableEntity>(cancellationToken: ct))
                {
                    await writer.WriteLineAsync(Serialize(entity));
                    rows++;
                }
            }

            buffer.Position = 0;
            await _container.GetBlobClient(blobName).UploadAsync(buffer, overwrite: true, ct);
            return rows;
        }

        private async Task DeleteOldCopiesAsync(DateOnly cutoff, CancellationToken ct)
        {
            await foreach (var blob in _container.GetBlobsAsync(prefix: Prefix, cancellationToken: ct))
            {
                var folder = blob.Name.Substring(Prefix.Length).Split('/')[0];
                if (DateOnly.TryParseExact(folder, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    && date < cutoff)
                {
                    await _container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: ct);
                }
            }
        }

        /// <summary>One entity as a JSON line: keys, timestamp, and each property as {"t": Edm type, "v": value}.</summary>
        internal static string Serialize(TableEntity entity)
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteString("PartitionKey", entity.PartitionKey);
                w.WriteString("RowKey", entity.RowKey);
                if (entity.Timestamp is { } timestamp) w.WriteString("Timestamp", timestamp);

                w.WriteStartObject("Properties");
                foreach (var (name, value) in entity)
                {
                    if (name is "PartitionKey" or "RowKey" or "Timestamp" or "odata.etag") continue;
                    w.WriteStartObject(name);
                    WriteTyped(w, value);
                    w.WriteEndObject();
                }
                w.WriteEndObject();

                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteTyped(Utf8JsonWriter w, object? value)
        {
            switch (value)
            {
                case null:
                    w.WriteNull("v");
                    break;
                case string s:
                    w.WriteString("t", "Edm.String");
                    w.WriteString("v", s);
                    break;
                case bool b:
                    w.WriteString("t", "Edm.Boolean");
                    w.WriteBoolean("v", b);
                    break;
                case int i:
                    w.WriteString("t", "Edm.Int32");
                    w.WriteNumber("v", i);
                    break;
                case long l:
                    // As text, like the Table service itself, so no reader rounds it through a double.
                    w.WriteString("t", "Edm.Int64");
                    w.WriteString("v", l.ToString(CultureInfo.InvariantCulture));
                    break;
                case double d:
                    w.WriteString("t", "Edm.Double");
                    if (double.IsFinite(d)) w.WriteNumber("v", d);
                    else w.WriteString("v", d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case DateTimeOffset dto:
                    w.WriteString("t", "Edm.DateTime");
                    w.WriteString("v", dto);
                    break;
                case DateTime dt:
                    w.WriteString("t", "Edm.DateTime");
                    w.WriteString("v", dt);
                    break;
                case Guid g:
                    w.WriteString("t", "Edm.Guid");
                    w.WriteString("v", g);
                    break;
                case byte[] bytes:
                    w.WriteString("t", "Edm.Binary");
                    w.WriteBase64String("v", bytes);
                    break;
                case BinaryData data:
                    w.WriteString("t", "Edm.Binary");
                    w.WriteBase64String("v", data.ToArray());
                    break;
                default:
                    w.WriteString("t", value.GetType().FullName);
                    w.WriteString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
        {
            try
            {
                await Task.Delay(delay, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
