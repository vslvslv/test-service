using TestService.Api.Emulators.Outcomes;
using TestService.Api.Emulators.Transactions;

namespace TestService.Api.Emulators.Praxis;

/// <summary>How a card sale starts: its test case, outcome and flow flags.</summary>
public sealed record PraxisSaleCase(string Name, IOutcome Outcome, bool Is3d = false, bool RequiresAuthentication = false, bool Redirects = false);

/// <summary>
/// The case table of the Praxis "Test Card Processor" as data (docs.praxis.tech/docs/testing,
/// corrected by the sandbox captures): trigger (CVV, amount in minor units, transaction type)
/// -> outcome, plus the follow-ups (deadlines, status checks) and the 3DS page decisions.
/// Lookup order for a sale: CVV + amount rows, then the amount rows (which apply to CVV 568),
/// then CVV rows, then the default decline.
/// </summary>
public static class PraxisTestCases
{
    public static readonly TimeSpan NotificationDelay = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan DuplicateNotificationGap = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan AuthenticationTimeout = TimeSpan.FromSeconds(540);

    public const string ThreeDsAbandoned = "3ds-abandoned";
    public const string ThreeDsPendingTimeout = "3ds-pending-timeout";
    public const string Timeout713 = "timeout-713";
    public const string StatusCheck30 = "status-check-amount-30";
    public const string StatusCheckRefund332 = "status-check-refund-332";
    public const string StatusCheckRefund333 = "status-check-refund-333";

    private const string Declined = "[TEST] Transaction status: declined";
    private const string Approved = "[TEST] Transaction status: approved";
    private const string InternalError = "[TEST] Transaction status: error. Reason: Internal server error";
    private const string AwaitingAuthorization = "[TEST] Transaction status: pending. Reason: Awaiting authorization";
    private const string Authorized = "[TEST] Transaction status: authorized";
    private const string AuthenticationRequired = "[TEST] Transaction status: pending_async. Reason: 3DS authentication required";
    private const string TimedOutPendingAsync = "Transaction timed out. Old status pending_async. New status declined";

    private static StatusChange Change(string code, string details, string session) => new(code, details, session);

    private static IOutcome ApprovedSale => new ApproveOutcome(Change("0", Approved, "successful"), NotificationDelay);

    private static IOutcome Decline(string code, string details) => new DeclineOutcome(Change(code, details, "failed"));

    private static IOutcome AmountCase(int trigger, string details, Func<long, long> finalAmount) =>
        new ChangeAmountOutcome(finalAmount, trigger.ToString(), $"[TEST CASE - amount {trigger}] {details}");

    private static PraxisSaleCase ThreeDs(string name) => new(name,
        new AwaitDecisionOutcome(Change("530", AuthenticationRequired, "open"), AuthenticationTimeout, ThreeDsAbandoned),
        Is3d: true, RequiresAuthentication: true, Redirects: true);

    private sealed record SaleRow(string[]? Cvvs, long? Amount, Func<PraxisSaleCase> Case);

    private static readonly SaleRow[] SaleRows =
    [
        // CVV + amount
        new(["333"], 33400, () => ThreeDs("3ds-duplicate-status")),
        new(["333"], 33500, () => ThreeDs("3ds-duplicate-amount")),
        new(["454"], 49800, () => ThreeDs("3ds-partial-at-psp")),
        new(["588"], 71300, () => new PraxisSaleCase("timeout",
            new AwaitDecisionOutcome(Change("0", AuthenticationRequired, "open"), AuthenticationTimeout, Timeout713),
            RequiresAuthentication: true)),
        // An amount case: approved without a notification, as amount 339.
        new(["801"], 2020, () => new PraxisSaleCase("amount-20.20-cvv-801", new ApproveOutcome(Change("0", Approved, "successful")))),
        new(["801"], 3000, () => new PraxisSaleCase("amount-30-cvv-801",
            new PendingUntilStatusCheckOutcome(Change("0", "[TEST] Transaction status: pending", "open"), StatusCheck30))),
        // Amount (with the approving CVV 568)
        new(["568"], 1100, () => new PraxisSaleCase("amount-11", AmountCase(11, "Changing final amount to 10", _ => 1000))),
        new(["568"], 1200, () => new PraxisSaleCase("amount-12", AmountCase(12, "Changing final currency", a => a))),
        new(["568"], 1300, () => new PraxisSaleCase("amount-13", AmountCase(13, "Changing final currency and amount", _ => 2600))),
        new(["568"], 1400, () => new PraxisSaleCase("amount-14", AmountCase(14, "Changing final currency to x100", a => a * 100))),
        new(["568"], 1900, () => new PraxisSaleCase("amount-19", AmountCase(19, "Changing final amount to 20", _ => 2000))),
        new(["568"], 4600, () => new PraxisSaleCase("amount-46", AmountCase(46, "Changing final amount to x2", a => a * 2))),
        new(["568"], 33900, () => new PraxisSaleCase("amount-339", new ApproveOutcome(Change("0", Approved, "successful")))),
        // CVV
        new(["333", "300"], null, () => ThreeDs("3ds")),
        new(["568"], null, () => new PraxisSaleCase("approved", ApprovedSale)),
        new(["000"], null, () => new PraxisSaleCase("declined",
            new DeclineOutcome(Change("0", Declined, "failed"), NotificationDelay))),
        new(["353"], null, () => new PraxisSaleCase("partial",
            new PartialApproveOutcome(Change("0", "[TEST] Transaction status: partial_approved. Reason: Notification with partial approved", "open"), NotificationDelay))),
        new(["444"], null, () => new PraxisSaleCase("error", new ErrorOutcome(Change("0", InternalError, "failed")))),
        new(["555"], null, () => new PraxisSaleCase("authorized", new AuthorizeOutcome(Change("0", Authorized, "action required")))),
        new(["801"], null, () => new PraxisSaleCase("insufficient-funds", Decline("GE-006", "Insufficient funds"))),
        new(["900"], null, () => new PraxisSaleCase("lost", Decline("0", "Card has been lost"))),
        new(["901"], null, () => new PraxisSaleCase("stolen", Decline("0", "Card has been stolen"))),
        new(["902"], null, () => new PraxisSaleCase("fraud", Decline("0", "Declined due to fraud"))),
        new(["903"], null, () => new PraxisSaleCase("do-not-honor", Decline("0", "Do not honor"))),
        new(["904"], null, () => new PraxisSaleCase("general-decline", Decline("0", "Transaction declined"))),
        new(["905"], null, () => new PraxisSaleCase("non-3d-decline", Decline("GE-007", "Transaction failed. Please contact our support team")))
    ];

    /// <summary>
    /// The CVV that selects the case: an AMEX card (number starting with 3) sends the trigger CVV with a fourth digit 1
    /// (568 -> 5681) [doc]; every other card sends it as is.
    /// </summary>
    public static string TriggerCvv(string cardNumber, string cvv) =>
        cardNumber.StartsWith('3') && cvv.Length == 4 && cvv[3] == '1' ? cvv[..3] : cvv;

    /// <summary>The case of a card sale with this CVV and amount (minor units).</summary>
    public static PraxisSaleCase ForSale(string cvv, long amount)
    {
        var row = SaleRows.FirstOrDefault(r => r.Amount is not null && r.Cvvs!.Contains(cvv) && r.Amount == amount)
                  ?? SaleRows.FirstOrDefault(r => r.Amount is null && r.Cvvs!.Contains(cvv));
        return row?.Case() ?? new PraxisSaleCase("declined-other", new DeclineOutcome(Change("0", Declined, "failed"), NotificationDelay));
    }

    /// <summary>The outcome of a card payout of this amount (minor units).</summary>
    public static (string Name, IOutcome Outcome) ForPayout(long amount) => amount switch
    {
        1400 => ("payout-amount-14", AmountCase(14, "Changing final currency to x100", a => a * 100)),
        4600 => ("payout-amount-46", AmountCase(46, "Changing final amount to x2", a => a * 2)),
        4700 => ("payout-amount-47", Decline("0", Declined)),
        4800 => ("payout-amount-48", new ErrorOutcome(Change("0", InternalError, "failed"))),
        33200 or 33300 => ($"payout-amount-{amount / 100}", new PendingUntilStatusCheckOutcome(
            Change("0", AwaitingAuthorization, "open"), followUp: null)),
        _ => ("payout-other", new ApproveOutcome(Change("0", Approved, "successful")))
    };

    /// <summary>The outcome of a card refund of this amount (minor units).</summary>
    public static (string Name, IOutcome Outcome) ForRefund(long amount) => amount switch
    {
        4700 => ("refund-amount-47", Decline("0", Declined)),
        4800 => ("refund-amount-48", new ErrorOutcome(Change("0", InternalError, "failed"))),
        33200 => ("refund-amount-332", new PendingUntilStatusCheckOutcome(Change("0", AwaitingAuthorization, "open"), StatusCheckRefund332)),
        33300 => ("refund-amount-333", new PendingUntilStatusCheckOutcome(Change("0", AwaitingAuthorization, "open"), StatusCheckRefund333)),
        _ => ("refund-other", new ApproveOutcome(Change("0", Approved, "successful")))
    };

    /// <summary>The outcome of <c>capture-authorization</c> on an authorized sale of this amount; status details stay "authorized".</summary>
    public static IOutcome ForCapture(long amount) => amount switch
    {
        4700 => new DeclineOutcome(Change("0", Authorized, "action required")),
        4800 => new ErrorOutcome(Change("0", Authorized, "action required")),
        _ => new ApproveOutcome(Change("0", Authorized, "action required"))
    };

    /// <summary>The outcome of <c>cancel-authorization</c>.</summary>
    public static IOutcome ForCancel() => new DeclineOutcome(Change("0", "Transaction status: Cancelled", "action required"));

    /// <summary>
    /// The outcome of a deadline or status-check follow-up (card or e-wallet, <see cref="PraxisEWalletCases.FollowUp"/>);
    /// null for an unknown (or empty) name.
    /// </summary>
    public static IOutcome? FollowUp(string? name) => name switch
    {
        ThreeDsAbandoned => new DeclineOutcome(Change("530", TimedOutPendingAsync, "failed"), NotificationDelay),
        ThreeDsPendingTimeout => new DeclineOutcome(Change("1", "Transaction timed out. Old status pending. New status declined", "failed"), NotificationDelay),
        Timeout713 => new DeclineOutcome(Change("0", TimedOutPendingAsync, "failed"), NotificationDelay),
        StatusCheck30 => new ApproveOutcome(Change("30.00", "[TEST CASE - amount 30.00] transaction status changed from pending to approved", "successful")),
        StatusCheckRefund332 => new DeclineOutcome(Change("332.00", "[TEST CASE - amount 332] transaction status changed from pending to rejected", "failed")),
        StatusCheckRefund333 => new ApproveOutcome(Change("333.00", "[TEST CASE - amount 333] transaction status changed from pending to approved", "successful")),
        _ => PraxisEWalletCases.FollowUp(name)
    };

    /// <summary>The 3DS page buttons: <c>approved</c>, <c>declined</c>, <c>pending</c>, <c>verification</c>.</summary>
    public static readonly IReadOnlyList<string> ThreeDsStatuses = ["approved", "declined", "pending", "verification"];

    /// <summary>The decline codes of the 3DS page (value, label).</summary>
    public static readonly IReadOnlyList<(string Code, string Label)> ThreeDsDeclineCodes =
    [
        ("903", "Do not honor"),
        ("906", "Insufficient funds"),
        ("904", "Without message"),
        ("900", "Card has been lost"),
        ("901", "Card has been  stolen"),
        ("902", "Declined due to fraud"),
        ("912", "Declined due to suspected fraud")
    ];

    private static readonly Dictionary<string, (string Code, string Details)> ThreeDsDeclines = new()
    {
        ["903"] = ("GE-006", "Do not honor"),
        ["906"] = ("GE-006", ". Reason: Insufficient funds"),
        ["904"] = ("904", "No status details"),
        ["900"] = ("900", "Card has been lost"),
        ["901"] = ("901", "Card has been  stolen"),
        ["902"] = ("902", "Declined due to fraud"),
        ["912"] = ("912", "Declined due to suspected-fraud")
    };

    /// <summary>
    /// The outcome of a 3DS page button for a transaction of the given case; null for an unknown status.
    /// An unknown or missing decline code declines as 903, the page's default choice.
    /// </summary>
    public static IOutcome? ThreeDsDecision(string caseName, string? status, string? errorCode)
    {
        switch (status)
        {
            case "approved":
                return caseName switch
                {
                    "3ds-duplicate-status" => new NotificationSequenceOutcome(
                        new ApproveOutcome(Change("530", "Approved", "successful"), NotificationDelay),
                        new VerificationOutcome(Change("530", Declined, "failed"), NotificationDelay + DuplicateNotificationGap)),
                    "3ds-duplicate-amount" => new NotificationSequenceOutcome(
                        new ApproveOutcome(Change("530", "Approved", "successful"), NotificationDelay),
                        // The session keeps the approved amount 33500 [sbx].
                        new VerificationOutcome(Change("530", Approved, "successful") with { FinalAmount = 35000, SessionAmount = 33500 },
                            NotificationDelay + DuplicateNotificationGap)),
                    "3ds-partial-at-psp" => new ApproveOutcome(
                        Change("530", "TEST amount 498, 3D partial approved with amount 400", "successful") with { FinalAmount = 40000, ProcessedAmount = 40000 },
                        NotificationDelay),
                    _ => new ApproveOutcome(Change("530", "Approved", "successful"), NotificationDelay)
                };
            case "declined":
                var (code, details) = ThreeDsDeclines.TryGetValue(errorCode ?? string.Empty, out var decline) ? decline : ThreeDsDeclines["903"];
                return new DeclineOutcome(Change(code, details, "failed"), NotificationDelay);
            case "pending":
                return new PendingThenTimeoutOutcome(Change("1", "Transaction in progress.", "open"),
                    AuthenticationTimeout, ThreeDsPendingTimeout, NotificationDelay);
            case "verification":
                return new VerificationOutcome(Change("530", "Transaction set to verification status", "open"), NotificationDelay);
            default:
                return null;
        }
    }
}
