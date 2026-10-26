using TestService.Api.Emulators.Outcomes;
using TestService.Api.Emulators.Transactions;

namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// The case table of the Praxis "Test E-Wallet" (method <c>altbankwire</c>) as data
/// (docs.praxis.tech/docs/testing, corrected by the sandbox captures of 2026-10-22):
/// what a sale does when its page opens, what each page button does (with the amount 11-46
/// overrides of Approve), the payout table and the deadline follow-ups. Amounts are minor units.
/// </summary>
public static class PraxisEWalletCases
{
    /// <summary>Notification delay after a state change (QA2: about 12 s).</summary>
    public static readonly TimeSpan NotificationDelay = TimeSpan.FromSeconds(12);

    /// <summary>A sale whose page is never submitted, and a Pending sale, time out after this (QA2: about 450 s).</summary>
    public static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(450);

    /// <summary>The 334 copies are notified 12, 17 and 22 s after the page opens (documented: 5 s apart).</summary>
    public static readonly TimeSpan CopyNotificationGap = TimeSpan.FromSeconds(5);

    public const string SaleCaseName = "apm-sale";

    /// <summary>Deadline of a sale whose payment page was never opened: times out from <c>pending_async</c> [qa2].</summary>
    public const string PageAbandoned = "apm-page-abandoned";

    /// <summary>Deadline of a sale whose buttons page was shown but never answered: times out from <c>pending</c> [sbx].</summary>
    public const string PageOpenedAbandoned = "apm-page-opened-abandoned";

    public const string PendingTimeout = "apm-pending-timeout";

    public const string NoStatusDetails = "No status details";
    private const string TimedOut = "Transaction timed out. Old status pending. New status declined";
    private const string TimedOutPendingAsync = "Transaction timed out. Old status pending_async. New status declined";
    private const string PayoutSuccess = "[TEST] Payout capture successfully";

    /// <summary>The page buttons, in page order.</summary>
    public static readonly IReadOnlyList<(string Status, string Label)> Buttons =
    [
        ("approved", "Approve"),
        ("pending", "Pending"),
        ("declined", "Declined"),
        ("cancelled", "Cancelled"),
        ("verification", "Verification")
    ];

    private static StatusChange Change(string code, string details, string session) => new(code, details, session);

    /// <summary>A new sale: initialized, <c>No status details</c>, waiting for the page (deadline: page abandoned).</summary>
    public static IOutcome ForSale() => new AwaitPageOutcome(Change("0", NoStatusDetails, "open"), PageTimeout, PageAbandoned);

    /// <summary>The outcome applied when the page of a sale of this amount opens; null when the page shows the buttons.</summary>
    public static IOutcome? PageOpen(long amount) => amount switch
    {
        48700 => new DeclineOutcome(Change("NoMoneyCode", "test Insufficient funds msg APM!", "failed"), NotificationDelay),
        99900 => new DeclineOutcome(Change("0", "[TEST] Insufficient funds [TEST CASE - amount 999]", "failed"), NotificationDelay),
        107800 => PaymentWallet(new ApproveOutcome(Change("0", "[TEST] Change amount less than 0.4%", "successful")
            with { FinalAmount = 108000, ProcessedAmount = 108000 }, NotificationDelay)),
        107900 => PaymentWallet(new ApproveOutcome(Change("0", "[TEST] Change amount between 0.4% and 10%", "successful")
            with { FinalAmount = 115400, ProcessedAmount = 115400 }, NotificationDelay)),
        33400 => PaymentWallet(new NotificationSequenceOutcome(
            // Praxis notifies approved (133.60), declined (334), approved (133.60), 5 s apart [doc]; the transaction
            // itself ends in verification 26720 without a notification of its own [sbx].
            new NotifyCopyOutcome(new ApproveOutcome(Change("0", NoStatusDetails, "successful")
                with { FinalAmount = 13360, ProcessedAmount = 13360 }, NotificationDelay)),
            new NotifyCopyOutcome(new DeclineOutcome(Change("0", NoStatusDetails, "failed"), NotificationDelay + CopyNotificationGap)),
            new NotifyCopyOutcome(new ApproveOutcome(Change("0", NoStatusDetails, "successful")
                with { FinalAmount = 13360, ProcessedAmount = 13360 }, NotificationDelay + CopyNotificationGap + CopyNotificationGap)),
            // The session keeps the copies' 133.60 [sbx].
            new VerificationOutcome(Change("0", NoStatusDetails, "open")
                with { FinalAmount = 26720, SessionAmount = 13360, SessionProcessedAmount = 13360 }))),
        _ => null
    };

    /// <summary>The outcome of a page button for a sale of this amount; null for an unknown status.</summary>
    public static IOutcome? Button(long amount, string? status) => status switch
    {
        "approved" => ButtonWallet(Approve(amount)),
        "pending" => new PendingThenTimeoutOutcome(Change("0", "[TEST] Transaction status: pending. Reason: Awaiting authorization", "open"),
            PageTimeout, PendingTimeout),
        "declined" => new DeclineOutcome(Change("0", "[TEST] Transaction status: declined. Reason: Insufficient funds", "failed"), NotificationDelay),
        "cancelled" => new CancelOutcome(Change("0", "[TEST] Transaction status: cancelled", "failed"), NotificationDelay),
        "verification" => new VerificationOutcome(Change("0", "[TEST] Transaction status: verification", "open"), NotificationDelay),
        _ => null
    };

    private static IOutcome Approve(long amount)
    {
        IOutcome Approved(int trigger, string details, long final) => new ApproveOutcome(
            Change(trigger.ToString(), $"[TEST CASE - amount {trigger}] {details}", "successful") with { FinalAmount = final, ProcessedAmount = final },
            NotificationDelay);
        // The transaction's processed values are null; the session shows them [sbx].
        IOutcome Verification(int trigger, string details, long final, long sessionProcessed, string? sessionCurrency = null,
            string? conversionRate = null) => new VerificationOutcome(
            Change(trigger.ToString(), $"[TEST CASE - amount {trigger}] {details}", "open") with
            {
                FinalAmount = final, SessionProcessedAmount = sessionProcessed, SessionProcessedCurrency = sessionCurrency, ConversionRate = conversionRate
            },
            NotificationDelay);

        return amount switch
        {
            1100 => Approved(11, "Changing final amount to 10", 1000),
            1200 => new ApproveOutcome(Change("12", "[TEST CASE - amount 12] Changing final currency", "successful")
                with { FinalAmount = 1068, ProcessedAmount = 1200, ProcessedCurrency = "USD", ConversionRate = "0.890315" }, NotificationDelay),
            1300 => Verification(13, "Changing final currency and amount", 1625, 2600, "AUD", "0.624810"),
            1400 => Verification(14, "Changing final amount to x100", amount * 100, amount * 100),
            1900 => Approved(19, "Changing final amount to 20", 2000),
            4600 => Verification(46, "Changing final amount to x2", amount * 2, amount * 2),
            _ => new ApproveOutcome(Change("0", "[TEST] Transaction status: approved", "successful"), NotificationDelay)
        };
    }

    /// <summary>
    /// The outcome of an e-wallet payout (<c>direct/apm</c> payout or <c>direct/wallet-token</c>) of this amount.
    /// Payouts never notify; <paramref name="redirects"/> is true for the payout-page case (300).
    /// </summary>
    public static (string Name, IOutcome Outcome, bool Redirects) ForPayout(long amount)
    {
        (string, IOutcome, bool) Approved(string name, string code, long final, long processed) => (name,
            WithdrawalWallet(new ApproveOutcome(Change(code, PayoutSuccess, "successful") with { FinalAmount = final, ProcessedAmount = processed })), false);

        return amount switch
        {
            1100 => Approved("payout-amount-11", "11", 1000, 1100),
            1200 => Approved("payout-amount-12", "12", 1200, 1200),
            1300 => Approved("payout-amount-13", "13", 2600, 1300),
            1400 => Approved("payout-amount-14", "14", amount * 100, amount),
            1900 => Approved("payout-amount-19", "19", 2000, 1900),
            4600 => Approved("payout-amount-46", "46", amount * 2, amount),
            5000 => ("payout-amount-50", new ErrorOutcome(Change("200", "[TEST] Incorrect processor response for payout [TEST CASE - amount 50]", "failed")), false),
            20000 => ("payout-amount-200", new DeclineOutcome(Change("NoMoneyCode", "test Insufficient payout funds msg APM!", "failed")), false),
            30000 => ("payout-amount-300", new AwaitPageOutcome(Change("300", "[TEST] Redirect for payout initiated [TEST CASE - amount 300]", "open")), true),
            33200 or 33300 => ($"payout-amount-{amount / 100}", new PendingUntilStatusCheckOutcome(
                Change("333", $"[TEST] Payout capture pending [TEST CASE - amount {amount / 100}.00]", "open"), followUp: null), false),
            60000 => ("payout-amount-600", new DeclineOutcome(Change("600", "[TEST] Payout capture failed [TEST CASE - amount 600]", "failed")), false),
            70000 => ("payout-amount-700", new PendingUntilStatusCheckOutcome(
                Change("50", "[TEST] Payout capture error [TEST CASE - amount 700]", "open"), followUp: null), false),
            _ => Approved("payout-other", "0", amount, amount)
        };
    }

    /// <summary>The outcome of an e-wallet deadline follow-up; null for a name this table does not know.</summary>
    public static IOutcome? FollowUp(string? name) => name switch
    {
        PageAbandoned => new DeclineOutcome(Change("0", TimedOutPendingAsync, "failed"), NotificationDelay),
        PageOpenedAbandoned or PendingTimeout => new DeclineOutcome(Change("0", TimedOut, "failed"), NotificationDelay),
        _ => null
    };

    private static IOutcome ButtonWallet(IOutcome inner) => new AttachWalletOutcome(PraxisWalletData.AttachButtonWallet, inner);

    private static IOutcome PaymentWallet(IOutcome inner) => new AttachWalletOutcome(PraxisWalletData.AttachPaymentWallet, inner);

    private static IOutcome WithdrawalWallet(IOutcome inner) => new AttachWalletOutcome(PraxisWalletData.AttachWithdrawalWallet, inner);
}
