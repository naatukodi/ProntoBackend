using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    /// <summary>
    /// Push notifications to the staff app (Vehga Inspections) through Firebase Cloud
    /// Messaging, on the app's own Firebase project, prontomoto-d4547 — not the portal's.
    ///
    /// The app sends its device token after every login (PUT /api/users/{userId}/devices)
    /// and withdraws it on logout. Tokens live in the DeviceTokens table, one row per user
    /// and phone, so the backend can reach "everyone who approves final reports" while the
    /// app is closed.
    ///
    /// Today the only notice is "ready for final report", sent when QC completes step 4 —
    /// from the portal or the app alike, since both go through WorkflowController.
    /// </summary>
    public interface IPushNotificationService
    {
        /// <summary>Remembers this phone for the user. A token moves with the person who
        /// last signed in on that phone, so a shared phone never notifies two people.</summary>
        Task RegisterDeviceAsync(string userId, string token, string? platform);

        Task UnregisterDeviceAsync(string userId, string token);

        /// <summary>Tells everyone who can approve the case's company's final reports that
        /// QC has finished with it. Returns the number of phones it reached.</summary>
        Task<int> NotifyFinalReportReadyAsync(string valuationId, string vehicleNumber, string applicantContact);
    }

    /// <summary>
    /// The Firebase Admin client, built once. Sending needs a service-account key for
    /// prontomoto-d4547 (Firebase console → Project settings → Service accounts), which is
    /// an App Service setting, never a file in the repo:
    ///
    ///   Fcm__ServiceAccountJson = { "type": "service_account", ... }   (the whole file)
    ///
    /// Locally, Fcm:ServiceAccountFile can name the downloaded file instead. With neither,
    /// tokens are still stored and every send is skipped with a log line, so this can be
    /// deployed before the key is in place.
    /// </summary>
    public sealed class FcmClient
    {
        public const string JsonKey = "Fcm:ServiceAccountJson";
        public const string FileKey = "Fcm:ServiceAccountFile";

        public FirebaseMessaging? Messaging { get; }

        public FcmClient(IConfiguration configuration, ILogger<FcmClient> logger)
        {
            var json = configuration[JsonKey];
            var file = configuration[FileKey];
            if (string.IsNullOrWhiteSpace(json) && string.IsNullOrWhiteSpace(file))
            {
                logger.LogWarning("No {Key} setting: staff-app push notifications are off.", JsonKey);
                return;
            }

            try
            {
                var credential = !string.IsNullOrWhiteSpace(json)
                    ? CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential()
                    : CredentialFactory.FromFile<ServiceAccountCredential>(file!).ToGoogleCredential();

                // Named, so it can never clash with a default app something else creates.
                var app = FirebaseApp.GetInstance("staff-app")
                          ?? FirebaseApp.Create(new AppOptions { Credential = credential }, "staff-app");
                Messaging = FirebaseMessaging.GetMessaging(app);
            }
            catch (Exception ex)
            {
                // A bad key must not take the API down with it; it only costs the notices.
                logger.LogError(ex, "The {Key} setting could not be read: staff-app push notifications are off.", JsonKey);
            }
        }
    }

    public class PushNotificationService : IPushNotificationService
    {
        // Who may approve a final report, as the portal's canDecideFinalReport() has it.
        private static readonly HashSet<string> ApproverRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "CanCreateFinalReport", "CanEditFinalReport", "Admin", "StateAdmin", "SuperAdmin"
        };

        // The app shows these in a channel of their own ("Final report approvals"),
        // created in MainActivity.kt.
        private const string AndroidChannel = "final_reports";

        private readonly TableClient _devices;
        private readonly TableClient _users;
        private readonly TableClient _userRoles;
        private readonly IWorkflowTableService _workflows;
        private readonly FcmClient _fcm;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(IConfiguration config, IWorkflowTableService workflows,
                                       FcmClient fcm, ILogger<PushNotificationService> logger)
        {
            var client = new TableServiceClient(config.GetConnectionString("TableStorage")!);
            _devices = client.GetTableClient("DeviceTokens");
            _users = client.GetTableClient("Users");
            _userRoles = client.GetTableClient("UserRoles");
            _devices.CreateIfNotExists();

            _workflows = workflows;
            _fcm = fcm;
            _logger = logger;
        }

        public async Task RegisterDeviceAsync(string userId, string token, string? platform)
        {
            var rowKey = Hash(token);

            // Whoever signed in on this phone before (no logout, or the app was cleared)
            // must stop getting their notices on it.
            await foreach (var old in _devices.QueryAsync<DeviceTokenEntity>(filter: $"RowKey eq '{rowKey}'"))
            {
                if (old.PartitionKey != userId)
                    await DeleteQuietlyAsync(old.PartitionKey, rowKey);
            }

            await _devices.UpsertEntityAsync(new DeviceTokenEntity
            {
                PartitionKey = userId,
                RowKey = rowKey,
                Token = token,
                Platform = platform
            });
        }

        public Task UnregisterDeviceAsync(string userId, string token) =>
            DeleteQuietlyAsync(userId, Hash(token));

        public async Task<int> NotifyFinalReportReadyAsync(string valuationId, string vehicleNumber, string applicantContact)
        {
            if (_fcm.Messaging is null) return 0;

            var wf = await _workflows.GetAsync(valuationId, vehicleNumber, applicantContact);
            var brand = BrandContext.Of(wf?.Brand);

            var recipients = await ApproversForAsync(brand);
            var tokens = new List<DeviceTokenEntity>();
            await foreach (var d in _devices.QueryAsync<DeviceTokenEntity>())
            {
                if (recipients.Contains(d.PartitionKey)) tokens.Add(d);
            }
            if (tokens.Count == 0)
            {
                _logger.LogInformation("Final report notice for {Valuation}: no approver has the app signed in.", valuationId);
                return 0;
            }

            var company = brand == BrandContext.Pronto ? "Pronto Moto" : "Vehga";
            var reached = 0;

            // FCM takes at most 500 tokens per call.
            foreach (var batch in tokens.Chunk(500))
            {
                var response = await _fcm.Messaging.SendEachForMulticastAsync(new MulticastMessage
                {
                    // FirebaseAdmin 3.7 marks Tokens deprecated in favour of installation IDs
                    // (Fids), but the app's firebase_messaging hands out registration tokens,
                    // and those are what FCM still delivers to.
#pragma warning disable CS0618
                    Tokens = batch.Select(d => d.Token).ToList(),
#pragma warning restore CS0618
                    Notification = new Notification
                    {
                        Title = "Ready for final report",
                        Body = $"{vehicleNumber} ({company}) has passed QC and is waiting for approval."
                    },
                    // What the app needs to open the case straight from the notice.
                    Data = new Dictionary<string, string>
                    {
                        ["type"] = "final_report_ready",
                        ["valuationId"] = valuationId,
                        ["vehicleNumber"] = vehicleNumber,
                        ["applicantContact"] = applicantContact,
                        ["applicantName"] = wf?.ApplicantName ?? "",
                        ["valuationType"] = wf?.ValuationType ?? "",
                        ["brand"] = brand
                    },
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        // Same tag per case: a case sent back and passed again replaces
                        // its earlier notice instead of stacking a second one.
                        Notification = new AndroidNotification { ChannelId = AndroidChannel, Tag = valuationId }
                    }
                });

                reached += response.SuccessCount;
                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var error = response.Responses[i].Exception?.MessagingErrorCode;
                    // The app was uninstalled or its data cleared: the token is dead for good.
                    if (error is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                        await DeleteQuietlyAsync(batch[i].PartitionKey, batch[i].RowKey);
                    else if (response.Responses[i].Exception is { } ex)
                        _logger.LogWarning(ex, "Final report notice for {Valuation} not delivered to one phone of {User}.",
                                           valuationId, batch[i].PartitionKey);
                }
            }

            _logger.LogInformation("Final report notice for {Valuation} ({Brand}) reached {Reached} of {Total} phones.",
                                   valuationId, brand, reached, tokens.Count);
            return reached;
        }

        /// <summary>UserIds allowed to approve this company's final reports. Both tables
        /// are a few dozen rows, so reading them whole beats a query per role.</summary>
        private async Task<HashSet<string>> ApproversForAsync(string brand)
        {
            var approvers = new HashSet<string>();
            await foreach (var row in _userRoles.QueryAsync<UserRoleEntity>())
            {
                if (ApproverRoles.Contains(row.RowKey)) approvers.Add(row.PartitionKey);
            }

            var allowed = new HashSet<string>();
            await foreach (var user in _users.QueryAsync<UserEntity>())
            {
                // The job role on the user row counts too: the app sends an Admin there
                // to the all-cases dashboard even without a matching UserRoles row.
                var approves = approvers.Contains(user.RowKey) ||
                               (user.RoleId is not null && ApproverRoles.Contains(user.RoleId));
                if (approves && WorksFor(user.AllowedBrands, brand)) allowed.Add(user.RowKey);
            }
            return allowed;
        }

        // Same rule as the login company picker: no AllowedBrands means Vehga only.
        private static bool WorksFor(string? allowedBrands, string brand)
        {
            if (string.IsNullOrWhiteSpace(allowedBrands)) return brand == BrandContext.Vehga;
            return allowedBrands.Split(',')
                .Any(b => string.Equals(BrandContext.Normalise(b), brand, StringComparison.Ordinal));
        }

        private async Task DeleteQuietlyAsync(string userId, string rowKey)
        {
            try { await _devices.DeleteEntityAsync(userId, rowKey); }
            catch (RequestFailedException ex) when (ex.Status == 404) { }
        }

        private static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
