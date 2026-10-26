using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using TestService.Api.Emulators;
using TestService.Api.Emulators.Praxis;

namespace TestService.Tests.Praxis;

/// <summary>
/// Reality check: replays each redacted sandbox example (<c>Examples/sandbox/&lt;case&gt;</c>) step by step
/// against <see cref="PraxisEmulator"/> (in-memory stores, a manual clock moved by the recorded
/// timestamps) and compares the recursive key set (into arrays, element by element), <c>status</c>,
/// <c>transaction.transaction_status</c> and <c>transaction.status_details</c> (for errors, <c>description</c>)
/// and the values of <c>amount</c>, <c>processed_amount</c>, <c>processed_currency</c>, <c>status_code</c> and
/// every <c>session</c> field (see <see cref="AssertSameValues"/>). The qa2 card examples
/// (<c>registration-response.json</c>, <c>notification-*.json</c>) are compared by key set; the qa2 e-wallet
/// examples by key set and, for the raw Praxis bodies, by value. No Praxis host is ever called.
/// </summary>
[TestFixture]
public class ExampleShapeTests
{
    private const string Environment = "shape";
    private const string Secret = "shape-test-secret";
    private const long RedactedTid = 9999999;
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private static string ExamplesRoot => Path.Combine(TestContext.CurrentContext.TestDirectory, "Praxis", "Examples");

    private static string SandboxRoot => Path.Combine(ExamplesRoot, "sandbox");

    private static string Qa2Root => Path.Combine(ExamplesRoot, "qa2");

    /// <summary>One emulator with the merchant the redacted examples name.</summary>
    private sealed class Replay
    {
        public Replay()
        {
            Emulator = new PraxisEmulator(Merchants, Transactions, Delivery, Time, NullLogger<PraxisEmulator>.Instance);
            Merchant = Merchants.CreateAsync(new PraxisMerchant
            {
                Environment = Environment,
                MerchantId = "<merchant_id>",
                ApplicationKey = "<application_key>",
                Secret = Secret,
                Gateways =
                [
                    new PraxisGateway { Id = PraxisRequestBuilder.CardGateway, Kind = "card" },
                    new PraxisGateway { Id = PraxisRequestBuilder.EWalletGateway, Kind = "ewallet" }
                ],
                TimeScale = 1
            }).GetAwaiter().GetResult();
        }

        public ManualTimeProvider Time { get; } = new(Start);
        public InMemoryPraxisMerchantRepository Merchants { get; } = new();
        public InMemoryTransactionRepository Transactions { get; } = new();
        public RecordingCallbackDelivery Delivery { get; } = new();
        public PraxisEmulator Emulator { get; }
        public PraxisMerchant Merchant { get; }

        public long? LastTid { get; set; }
        public string? CardToken { get; set; }
        public string? CustomerToken { get; set; }
        public string? RedirectPath { get; set; }

        /// <summary>Every transaction the replay created: the example's request timestamp, the tid issued and its type.</summary>
        public List<(long RequestTimestamp, long Tid, string Type)> Created { get; } = new();

        /// <summary>Page recordings (<c>*.meta.json</c>) already replayed.</summary>
        public HashSet<string> DrivenPages { get; } = new(StringComparer.Ordinal);

        public long Now => Time.GetUtcNow().ToUnixTimeSeconds();
    }

    private sealed record Step(string File, string Name, JsonObject Example)
    {
        public long Timestamp => Example["request"]!["timestamp"]!.GetValue<long>();
        public string Path => Example["path"]!.GetValue<string>();
    }

    // ------------------------------------------------------------------ sandbox

    private static IEnumerable<TestCaseData> SandboxCases() =>
        Directory.GetDirectories(SandboxRoot).Select(System.IO.Path.GetFileName).Order(StringComparer.Ordinal)
            .Select(name => new TestCaseData(name).SetName($"Sandbox_{name}"));

    [TestCaseSource(nameof(SandboxCases))]
    public async Task SandboxExample_ReplayedStepByStep_MatchesShapeAndStatus(string folder)
    {
        var steps = LoadSteps(folder);
        Assert.That(steps, Is.Not.Empty, $"{folder} has no steps");

        var replay = new Replay();
        var first = steps.Min(s => s.Timestamp);
        var cvv = CvvOf(folder);

        foreach (var step in steps)
        {
            var at = Start + TimeSpan.FromSeconds(step.Timestamp - first);
            if (step.Name == "find-transaction-after-page.json")
            {
                await PressPageAsync(replay, folder, at - TimeSpan.FromSeconds(1));
                await DriveEWalletPagesAsync(replay, folder, at - TimeSpan.FromSeconds(1), "sale-redirect-page", "apm-page", "apm-response");
            }
            if (step.Name == "find-transaction-after-button.json")
            {
                await DriveEWalletPagesAsync(replay, folder, at - TimeSpan.FromSeconds(1), "sale-2-redirect-page", "apm-page", "apm-page-");
            }
            if (step.Path == "agent/find-transaction")
            {
                await DriveEWalletPagesAsync(replay, folder, at - TimeSpan.FromSeconds(1), "payout-redirect-page", "payout-page");
            }
            replay.Time.Now = at;

            var (reply, actual) = await SendStepAsync(replay, step, cvv);
            AssertMatches(step, reply, actual);
            Remember(replay, step, actual);
        }
    }

    private static List<Step> LoadSteps(string folder)
    {
        IEnumerable<string> files = Directory.GetFiles(Path.Combine(SandboxRoot, folder), "*.json");
        var seed = folder switch
        {
            _ when folder.StartsWith("card-payout-", StringComparison.Ordinal) && folder != "card-payout-seed" => "card-payout-seed",
            _ when folder.StartsWith("card-refund-", StringComparison.Ordinal) && folder != "card-refund-seed" => "card-refund-seed",
            _ when folder.StartsWith("ewallet-refund-", StringComparison.Ordinal) && folder != "ewallet-refund-seed" => "ewallet-refund-seed",
            "errors" => "errors-seed",
            _ => null
        };
        if (seed is not null)
        {
            files = files.Concat(Directory.GetFiles(Path.Combine(SandboxRoot, seed), "*.json"));
        }

        return files
            .Where(f => !f.EndsWith(".meta.json", StringComparison.Ordinal))
            .Select(f => new Step(f, System.IO.Path.GetFileName(f), JsonNode.Parse(File.ReadAllText(f))!.AsObject()))
            .OrderBy(s => s.Timestamp)
            .ToList();
    }

    /// <summary>The case's CVV: from the folder name, 555 for capture/cancel, otherwise 568 (as the capture script used).</summary>
    private static string CvvOf(string folder)
    {
        var match = Regex.Match(folder, @"cvv-(\d{3})");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        if (folder == "card-sale-cvv-other-declined")
        {
            return "123";
        }
        return folder.StartsWith("card-capture-", StringComparison.Ordinal) || folder == "card-authorization-cancel" ? "555" : "568";
    }

    private static async Task<(EmulatorReply Reply, JsonNode Actual)> SendStepAsync(Replay replay, Step step, string folderCvv)
    {
        var request = step.Example["request"]!.DeepClone().AsObject();
        request["timestamp"] = replay.Now;

        // Seeds always approve; the case's CVV applies to the case's own sales.
        var isSeed = step.File.Contains("-seed", StringComparison.Ordinal);
        if (request["card_data"] is JsonObject)
        {
            request["card_data"] = PraxisRequestBuilder.CardData(Secret, replay.Now, isSeed ? "568" : folderCvv);
        }
        if (request.ContainsKey("card_token"))
        {
            request["card_token"] = replay.CardToken;
            request["customer_token"] = replay.CustomerToken;
        }
        foreach (var key in new[] { "tid", "reference_id", "withdrawal_request_id" })
        {
            if (request[key] is JsonValue value && value.GetValue<long>() == RedactedTid)
            {
                request[key] = RedactedTarget(replay, step);
            }
        }

        string? signature = null;
        switch (step.Name)
        {
            case "find-transaction-unknown-merchant.json":
                request["merchant_id"] = "unknown-merchant";
                break;
            case "find-transaction-wrong-signature.json":
                signature = "0000";
                break;
            case "find-transaction-expired-timestamp.json":
                request["timestamp"] = replay.Now - 130;
                break;
        }

        var reply = await replay.Emulator.HandleAsync(
            PraxisRequestBuilder.Request(Environment, step.Path, request, Secret, signature));
        return (reply, reply.Json()!);
    }

    /// <summary>
    /// The tid a redacted id stands for: in <c>find-transaction-after-&lt;N&gt;s</c> the transaction whose request
    /// timestamp is closest to the step's timestamp minus N (the capture script re-read remembered transactions
    /// N seconds after creating them); otherwise the last created transaction.
    /// </summary>
    private static long? RedactedTarget(Replay replay, Step step)
    {
        var after = Regex.Match(step.Name, @"^find-transaction-after-(\d+)s\.json$");
        if (!after.Success || replay.Created.Count == 0)
        {
            return replay.LastTid;
        }
        var createdAt = step.Timestamp - long.Parse(after.Groups[1].Value, CultureInfo.InvariantCulture);
        return replay.Created.MinBy(c => Math.Abs(c.RequestTimestamp - createdAt)).Tid;
    }

    private static void Remember(Replay replay, Step step, JsonNode actual)
    {
        if (step.Path is not ("api/direct-process" or "direct/refund" or "direct/apm") || PraxisSigner.Text(actual, "status") != "0")
        {
            return;
        }

        replay.LastTid = actual["transaction"]!["tid"]!.GetValue<long>();
        replay.Created.Add((step.Timestamp, replay.LastTid.Value, PraxisSigner.Text(actual, "transaction.transaction_type")));
        if (PraxisSigner.Text(actual, "transaction.transaction_type") == "sale")
        {
            replay.CardToken = PraxisSigner.Text(actual, "transaction.card.card_token");
            replay.CustomerToken = PraxisSigner.Text(actual, "customer.customer_token");
            replay.RedirectPath = actual["redirect_url"] is JsonValue redirect
                ? new Uri(redirect.GetValue<string>()).AbsolutePath
                : null;
        }
    }

    /// <summary>Replays the 3DS page: GET the page, then press the recorded button (status and error_code from the meta file).</summary>
    private static async Task PressPageAsync(Replay replay, string folder, DateTimeOffset at)
    {
        var meta = Directory.GetFiles(Path.Combine(SandboxRoot, folder), "3ds-page-*.meta.json").SingleOrDefault();
        if (meta is null)
        {
            return;
        }
        replay.Time.Now = at;

        Assert.That(replay.RedirectPath, Is.Not.Null, $"{folder}: the sale gave no redirect_url");
        var pagePath = replay.RedirectPath!.Split($"/emulators/praxis/{Environment}/", 2)[1];
        var page = await replay.Emulator.HandleAsync(new EmulatorRequest { Environment = Environment, Path = pagePath, Method = "GET" });
        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(200), $"{folder}: 3DS page");
            Assert.That(page.Body, Does.Contain("sandbox_3ds_form"));
        });

        var recorded = JsonNode.Parse(File.ReadAllText(meta))!;
        var status = Regex.Match(recorded["url"]!.GetValue<string>(), @"status=(\w+)").Groups[1].Value;
        var errorCode = PraxisSigner.Text(recorded, "form.error_code");
        var submit = await replay.Emulator.HandleAsync(new EmulatorRequest
        {
            Environment = Environment,
            Path = pagePath + "/submit",
            Method = "POST",
            Query = new Dictionary<string, string> { ["status"] = status },
            Form = new Dictionary<string, string> { ["error_code"] = errorCode }
        });
        Assert.That(submit.StatusCode, Is.EqualTo(recorded["http_status"]!.GetValue<int>()), $"{folder}: 3DS submit {status}");
    }

    /// <summary>
    /// Replays the recorded e-wallet pages of <paramref name="folder"/> whose names match <paramref name="names"/>
    /// (in that order, each once; a name ending in '-' is a prefix) against the emulator's pages of the latest sale or
    /// payout, and asserts the recorded HTTP status. Mapping: a sale's redirect page and any recorded "Please wait"
    /// page -> <c>pages/apm-request/{tid}</c>; <c>apm-page</c> (buttons) and <c>apm-response</c> (page-open
    /// outcome, 302) -> <c>pages/apm/{tid}</c>; <c>apm-page-&lt;status&gt;</c> -> <c>pages/apm/{tid}/response?status=</c>;
    /// <c>payout-redirect-page</c> and <c>payout-page</c> -> <c>pages/payout/{tid}</c> (the emulator's
    /// <c>redirect_url</c> points straight at the payout page, so the sandbox's 302 hop ends there with 200).
    /// </summary>
    private static async Task DriveEWalletPagesAsync(Replay replay, string folder, DateTimeOffset at, params string[] names)
    {
        if (!folder.StartsWith("ewallet-", StringComparison.Ordinal))
        {
            return;
        }
        var metas = Directory.GetFiles(Path.Combine(SandboxRoot, folder), "*.meta.json")
            .Select(f => System.IO.Path.GetFileName(f)[..^".meta.json".Length])
            .ToList();
        foreach (var wanted in names)
        {
            var matches = metas
                .Where(m => wanted.EndsWith('-') ? m.StartsWith(wanted, StringComparison.Ordinal) : m == wanted)
                .Order(StringComparer.Ordinal);
            foreach (var name in matches)
            {
                if (replay.DrivenPages.Add(name))
                {
                    replay.Time.Now = at;
                    await DriveEWalletPageAsync(replay, folder, name);
                }
            }
        }
    }

    private static async Task DriveEWalletPageAsync(Replay replay, string folder, string name)
    {
        var dir = Path.Combine(SandboxRoot, folder);
        var recorded = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, name + ".meta.json")))!;
        var htmlFile = Path.Combine(dir, name + ".html");
        var html = File.Exists(htmlFile) ? File.ReadAllText(htmlFile) : string.Empty;
        var expectedStatus = recorded["http_status"]!.GetValue<int>();
        var isPayout = name.StartsWith("payout-", StringComparison.Ordinal);
        var tid = replay.Created.Last(c => c.Type == (isPayout ? "payout" : "sale")).Tid;

        string path;
        var query = new Dictionary<string, string>();
        if (isPayout)
        {
            path = $"pages/payout/{tid}";
            expectedStatus = 200;
        }
        else if (name.EndsWith("redirect-page", StringComparison.Ordinal) || html.Contains("Please wait", StringComparison.Ordinal))
        {
            path = $"pages/apm-request/{tid}";
        }
        else if (name.StartsWith("apm-page-", StringComparison.Ordinal))
        {
            path = $"pages/apm/{tid}/response";
            query["status"] = Regex.Match(recorded["url"]!.GetValue<string>(), @"status=(\w+)").Groups[1].Value;
        }
        else
        {
            path = $"pages/apm/{tid}";
        }

        var page = await replay.Emulator.HandleAsync(new EmulatorRequest
        {
            Environment = Environment, Path = path, Method = "GET", Query = query
        });
        Assert.That(page.StatusCode, Is.EqualTo(expectedStatus), $"{folder}: page {name} -> {path} answered {page.Body}");
    }

    private static void AssertMatches(Step step, EmulatorReply reply, JsonNode actual)
    {
        var expected = step.Example["response"]!;
        var label = $"{System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(step.File))}/{step.Name}";
        AssertSameKeys(expected, actual, label);
        AssertSameValues(expected, actual, label, ExcludedValues.GetValueOrDefault(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(step.File))!));

        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.Text(actual, "status"), Is.EqualTo(PraxisSigner.Text(expected, "status")), $"{label}: status");
            if (expected["transaction"] is JsonObject)
            {
                Assert.That(PraxisSigner.Text(actual, "transaction.transaction_status"),
                    Is.EqualTo(PraxisSigner.Text(expected, "transaction.transaction_status")), $"{label}: transaction_status");
                Assert.That(PraxisSigner.Text(actual, "transaction.status_details"),
                    Is.EqualTo(PraxisSigner.Text(expected, "transaction.status_details")), $"{label}: status_details");
            }
            else
            {
                Assert.That(PraxisSigner.Text(actual, "description"), Is.EqualTo(PraxisSigner.Text(expected, "description")), $"{label}: description");
            }

            var expectsSignature = step.Example["response_headers"]!.AsObject()
                .Any(h => string.Equals(h.Key, PraxisSigner.HeaderName, StringComparison.OrdinalIgnoreCase));
            Assert.That(reply.Headers.ContainsKey(PraxisSigner.HeaderName), Is.EqualTo(expectsSignature), $"{label}: GT-Authentication header");
        });
    }

    // ------------------------------------------------------------------ qa2

    private static IEnumerable<TestCaseData> Qa2Files() =>
        Directory.GetDirectories(Qa2Root).Order(StringComparer.Ordinal)
            .Where(dir => !IsEWalletFolder(dir))
            .SelectMany(dir => Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal))
            .Where(f => System.IO.Path.GetFileName(f) is var name && (name == "registration-response.json" || name.StartsWith("notification-", StringComparison.Ordinal)))
            .Select(f => System.IO.Path.GetRelativePath(Qa2Root, f))
            .Select(f => new TestCaseData(f).SetName($"Qa2_{f.Replace('\\', '/')}"));

    [TestCaseSource(nameof(Qa2Files))]
    public async Task Qa2Example_HasTheEmulatorKeySet(string file)
    {
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(Qa2Root, file)))!;
        var folder = System.IO.Path.GetDirectoryName(file)!;
        var replay = new Replay();

        JsonNode actual;
        if (expected["merchant_id"] is not null)
        {
            // Notification-shaped: compare with the notification the emulator sends for an approved sale.
            await SaleAsync(replay, folder, "568", notificationUrl: "https://merchant.example/notification");
            actual = JsonNode.Parse(replay.Delivery.Jobs.Single().Body!)!;
        }
        else if (expected["transaction"]?["redirect_url"] is not null || expected["transaction"]?.AsObject().ContainsKey("redirect_url") == true)
        {
            actual = await SaleAsync(replay, folder, "333");
        }
        else
        {
            actual = await SaleAsync(replay, folder, folder.StartsWith("extra-", StringComparison.Ordinal) ? "568" : CvvOf(folder));
        }

        AssertSameKeys(expected, actual, file);
    }

    /// <summary>
    /// Acceptance: BillingServer's own field set (qa2 <c>registration-request.json</c>) with a fresh timestamp, card data
    /// re-encrypted from the test card with CVV 568 and a fresh signature is approved, signed, has the example
    /// response's key set, and a signed approved notification goes to its <c>notification_url</c>.
    /// </summary>
    [Test]
    public async Task Qa2RegistrationRequest_Cvv568_ApprovedSignedWithExampleKeySetAndSignedNotification()
    {
        var replay = new Replay();
        var reply = await replay.Emulator.HandleAsync(Qa2SaleRequest(replay, "card-sale-cvv-568-approved", "568", null));
        var response = reply.Json()!;
        var example = JsonNode.Parse(File.ReadAllText(Path.Combine(Qa2Root, "card-sale-cvv-568-approved", "registration-response.json")))!;
        var job = replay.Delivery.Jobs.Single();
        var notification = JsonNode.Parse(job.Body!)!;

        AssertSameKeys(example, response, "qa2 568 registration-response");
        Assert.Multiple(() =>
        {
            Assert.That(PraxisSigner.Text(response, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(reply.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForResponse(response, Secret)));
            Assert.That(job.Url, Is.EqualTo("https://merchant.example/notification"));
            Assert.That(PraxisSigner.Text(notification, "transaction.transaction_status"), Is.EqualTo("approved"));
            Assert.That(job.Headers[PraxisSigner.HeaderName], Is.EqualTo(PraxisSigner.ForNotification(notification, Secret)));
        });
    }

    private static async Task<JsonNode> SaleAsync(Replay replay, string folder, string cvv, string? notificationUrl = null)
    {
        var reply = await replay.Emulator.HandleAsync(Qa2SaleRequest(replay, folder, cvv, notificationUrl));
        Assert.That(PraxisSigner.Text(reply.Json(), "status"), Is.EqualTo("0"), $"{folder}: {reply.Body}");
        return reply.Json()!;
    }

    /// <summary>The folder's qa2 request when it is a real request (has card_data); otherwise the builder's sale.</summary>
    private static EmulatorRequest Qa2SaleRequest(Replay replay, string folder, string cvv, string? notificationUrl)
    {
        var path = Path.Combine(Qa2Root, folder, "registration-request.json");
        var template = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        JsonObject request;
        if (template?["card_data"] is JsonObject)
        {
            request = template;
            request["timestamp"] = replay.Now;
            request["card_data"] = PraxisRequestBuilder.CardData(Secret, replay.Now, cvv);
            request["amount"] = 4900;
            if (notificationUrl is not null)
            {
                request["notification_url"] = notificationUrl;
            }
        }
        else
        {
            var amount = Regex.Match(folder, @"amount-(\d+)") is { Success: true } m ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 100 : 4900;
            request = PraxisRequestBuilder.Sale(replay.Merchant, replay.Now, cvv, amount, notificationUrl);
        }
        return PraxisRequestBuilder.Request(Environment, "api/direct-process", request, Secret);
    }

    // ------------------------------------------------------------------ qa2 e-wallet

    private static bool IsEWalletFolder(string dir) => System.IO.Path.GetFileName(dir).Contains("ewallet-", StringComparison.Ordinal);

    private static IEnumerable<TestCaseData> Qa2EWalletFiles() =>
        Directory.GetDirectories(Qa2Root).Where(IsEWalletFolder).Order(StringComparer.Ordinal)
            .SelectMany(dir => Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal))
            .Where(f => System.IO.Path.GetFileName(f) is var name
                        && (name == "deposit-response.json"
                            || name.StartsWith("find-transaction-response-", StringComparison.Ordinal)
                            || name.StartsWith("notification-", StringComparison.Ordinal)))
            .Select(f => System.IO.Path.GetRelativePath(Qa2Root, f))
            .Select(f => new TestCaseData(f).SetName($"Qa2EWallet_{f.Replace('\\', '/')}"));

    /// <summary>
    /// The qa2 e-wallet files by key set: BillingServer's <c>deposit-request.json</c> (fresh timestamp and signature)
    /// is sent to <c>direct/apm</c>, the folder's outcome is driven on the page (approve -> wallet; decline,
    /// cancelled, timeouts -> no wallet), then find-transaction runs. <c>deposit-response.json</c> and
    /// <c>notification-1.json</c> are raw Praxis bodies and must match exactly. <c>find-transaction-response-*.json</c>
    /// are BillingServer re-serializations (its <c>PraxisTransaction</c> model: no <c>edited_by</c>,
    /// <c>withdrawal_request_id</c>, <c>is_3d</c>, and an <c>is_async</c> it writes even when Praxis omits it), so
    /// every key of the file except <c>transaction.is_async</c> must be present in the emulator's reply.
    /// </summary>
    [TestCaseSource(nameof(Qa2EWalletFiles))]
    public async Task Qa2EWalletExample_HasTheEmulatorKeySet(string file)
    {
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(Qa2Root, file)))!;
        var folder = System.IO.Path.GetDirectoryName(file)!;
        var name = System.IO.Path.GetFileName(file);
        var replay = new Replay();

        var request = JsonNode.Parse(File.ReadAllText(Path.Combine(Qa2Root, folder, "deposit-request.json")))!.AsObject();
        request["timestamp"] = replay.Now;
        var deposit = await replay.Emulator.HandleAsync(PraxisRequestBuilder.Request(Environment, "direct/apm", request, Secret));
        var depositResponse = deposit.Json()!;
        Assert.That(PraxisSigner.Text(depositResponse, "status"), Is.EqualTo("0"), $"{folder}: {deposit.Body}");
        var tid = depositResponse["transaction"]!["tid"]!.GetValue<long>();

        var (button, timesOut) = folder switch
        {
            _ when folder.EndsWith("-approve", StringComparison.Ordinal) || folder.EndsWith("-no-notification", StringComparison.Ordinal) => ("approved", false),
            _ when folder.EndsWith("-decline", StringComparison.Ordinal) => ("declined", false),
            _ when folder.EndsWith("-cancelled", StringComparison.Ordinal) => ("cancelled", false),
            _ when folder.EndsWith("-pending", StringComparison.Ordinal) => ("pending", true),
            _ => ((string?)null, true)
        };
        if (button is not null)
        {
            var pressed = await replay.Emulator.HandleAsync(new EmulatorRequest
            {
                Environment = Environment, Path = $"pages/apm/{tid}/response", Method = "GET",
                Query = new Dictionary<string, string> { ["status"] = button }
            });
            Assert.That(pressed.StatusCode, Is.EqualTo(302), $"{folder}: {button}");
        }
        if (timesOut)
        {
            replay.Time.Advance(PraxisEWalletCases.PageTimeout + TimeSpan.FromSeconds(1));
        }
        var find = (await replay.Emulator.HandleAsync(PraxisRequestBuilder.Request(Environment, "agent/find-transaction",
            PraxisRequestBuilder.Find(replay.Merchant, replay.Now, tid), Secret))).Json()!;

        if (name == "deposit-response.json")
        {
            AssertSameKeys(expected, depositResponse, file);
            AssertSameValues(expected, depositResponse, file);
            AssertSameStatus(expected, depositResponse, file);
        }
        else if (name.StartsWith("notification-", StringComparison.Ordinal))
        {
            Assert.That(replay.Delivery.Jobs, Is.Not.Empty, $"{folder}: no notification");
            var notification = JsonNode.Parse(replay.Delivery.Jobs[0].Body!)!;
            AssertSameKeys(expected, notification, file);
            AssertSameValues(expected, notification, file);
            AssertSameStatus(expected, notification, file);
        }
        else
        {
            var missing = Keys(expected).Where(k => k != "transaction.is_async").Except(Keys(find)).ToList();
            Assert.That(missing, Is.Empty, $"{file}: keys BillingServer reads are missing. Actual body: {find.ToJsonString()}");
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Every key path; array elements are walked by index (<c>gateway_list[1].gateway</c>).</summary>
    private static SortedSet<string> Keys(JsonNode? node, string prefix = "")
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    keys.Add(prefix + key);
                    keys.UnionWith(Keys(value, prefix + key + "."));
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    keys.UnionWith(Keys(array[i], $"{prefix.TrimEnd('.')}[{i}]."));
                }
                break;
        }
        return keys;
    }

    private static readonly HashSet<string> ComparedKeys = new(StringComparer.Ordinal)
    {
        "amount", "processed_amount", "processed_currency", "status_code"
    };

    /// <summary>Values the emulator does not reproduce, by sandbox example folder.</summary>
    private static readonly Dictionary<string, string[]> ExcludedValues = new(StringComparer.Ordinal)
    {
        // CVV 900 / 901: Praxis answers with gateway 24UUrAzzzySSeayknvZCMGRDDIEU3N26, not the requested one; the emulator echoes the request's.
        ["card-sale-cvv-900-lost"] = ["session.gateway"],
        ["card-sale-cvv-901-stolen"] = ["session.gateway"]
    };

    private static void AssertSameStatus(JsonNode expected, JsonNode actual, string label) => Assert.Multiple(() =>
    {
        Assert.That(PraxisSigner.Text(actual, "transaction.transaction_status"),
            Is.EqualTo(PraxisSigner.Text(expected, "transaction.transaction_status")), $"{label}: transaction_status");
        Assert.That(PraxisSigner.Text(actual, "transaction.status_details"),
            Is.EqualTo(PraxisSigner.Text(expected, "transaction.status_details")), $"{label}: status_details");
    });

    /// <summary>
    /// The values of <see cref="ComparedKeys"/> (anywhere, arrays included) and of every <c>session</c> field must equal
    /// the example's. Skipped: redacted placeholders (<c>&lt;...&gt;</c>), masked values (only <c>9</c>, <c>x</c> and
    /// <c>-</c>), and ids, tokens, URLs and timestamps, which the replay issues itself.
    /// </summary>
    private static void AssertSameValues(JsonNode expected, JsonNode actual, string label, string[]? excluded = null)
    {
        var differences = new List<string>();
        Compare(expected, actual, "", inSession: false, differences);
        differences.RemoveAll(d => excluded?.Any(path => d.StartsWith(path + ":", StringComparison.Ordinal)) == true);
        Assert.That(differences, Is.Empty, $"{label}: values differ: {string.Join("; ", differences)}. Actual body: {actual.ToJsonString()}");
    }

    private static void Compare(JsonNode? expected, JsonNode? actual, string path, bool inSession, List<string> differences)
    {
        switch (expected)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    Compare(value, (actual as JsonObject)?[key], path.Length == 0 ? key : $"{path}.{key}", inSession || (path.Length == 0 && key == "session"),
                        differences);
                }
                return;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    Compare(array[i], actual is JsonArray other && i < other.Count ? other[i] : null, $"{path}[{i}]", inSession, differences);
                }
                return;
        }

        var name = path[(path.LastIndexOf('.') + 1)..];
        if (!(inSession || ComparedKeys.Contains(name)) || IsIssuedByTheReplay(name) || IsRedacted(expected))
        {
            return;
        }
        var expectedText = expected?.ToJsonString() ?? "null";
        var actualText = actual?.ToJsonString() ?? "null";
        if (expectedText != actualText)
        {
            differences.Add($"{path}: expected {expectedText}, actual {actualText}");
        }
    }

    private static bool IsIssuedByTheReplay(string key) =>
        key is "timestamp" or "tid" or "cid" or "auth_token"
        || key.EndsWith("_id", StringComparison.Ordinal)
        || key.EndsWith("_token", StringComparison.Ordinal)
        || key.EndsWith("_url", StringComparison.Ordinal);

    private static bool IsRedacted(JsonNode? value) =>
        value is JsonValue v && v.TryGetValue<string>(out var text)
        && (text.StartsWith('<') && text.EndsWith('>') || Regex.IsMatch(text, "^[9x-]{6,}$"));

    private static void AssertSameKeys(JsonNode expected, JsonNode actual, string label)
    {
        var expectedKeys = Keys(expected);
        var actualKeys = Keys(actual);
        var missing = expectedKeys.Except(actualKeys).ToList();
        var extra = actualKeys.Except(expectedKeys).ToList();
        Assert.That(missing.Count + extra.Count, Is.Zero,
            $"{label}: key set differs. Missing: [{string.Join(", ", missing)}]. Extra: [{string.Join(", ", extra)}]. Actual body: {actual.ToJsonString()}");
    }
}
