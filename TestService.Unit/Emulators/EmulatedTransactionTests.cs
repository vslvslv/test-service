using TestService.Api.Emulators.Outcomes;
using TestService.Api.Emulators.Transactions;

namespace TestService.Unit.Emulators;

[TestFixture]
public class EmulatedTransactionTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly StatusChange Change = new("0", "details", "open");

    private static readonly Dictionary<string, Action<EmulatedTransaction>> Transitions = new()
    {
        ["MarkPending"] = t => t.MarkPending(Change, Now),
        ["Approve"] = t => t.Approve(Change, Now),
        ["Reject"] = t => t.Reject(Change, Now),
        ["Fail"] = t => t.Fail(Change, Now),
        ["Authorize"] = t => t.Authorize(Change, Now),
        ["PartiallyApprove"] = t => t.PartiallyApprove(Change, Now),
        ["RequestVerification"] = t => t.RequestVerification(Change, Now),
        ["Cancel"] = t => t.Cancel(Change, Now),
        ["AwaitAuthentication"] = t => t.AwaitAuthentication(Change, new TransactionDeadline { DueAt = Now, FollowUp = "x" }, Now),
        ["SetDeadline"] = t => t.SetDeadline(new TransactionDeadline { DueAt = Now, FollowUp = "x" }),
        ["SetStatusCheckFollowUp"] = t => t.SetStatusCheckFollowUp("x")
    };

    /// <summary>How to bring a fresh transaction into each status.</summary>
    private static readonly Dictionary<TransactionStatus, Action<EmulatedTransaction>> Into = new()
    {
        [TransactionStatus.Initialized] = _ => { },
        [TransactionStatus.Pending] = t => t.MarkPending(Change, Now),
        [TransactionStatus.Approved] = t => t.Approve(Change, Now),
        [TransactionStatus.Rejected] = t => t.Reject(Change, Now),
        [TransactionStatus.Error] = t => t.Fail(Change, Now),
        [TransactionStatus.Authorized] = t => t.Authorize(Change, Now),
        [TransactionStatus.PartialApproved] = t => t.PartiallyApprove(Change, Now),
        [TransactionStatus.Verification] = t => t.RequestVerification(Change, Now),
        [TransactionStatus.Cancelled] = t => t.Cancel(Change, Now)
    };

    private static readonly Dictionary<string, TransactionStatus[]> Legal = new()
    {
        ["MarkPending"] = [TransactionStatus.Initialized],
        ["Approve"] = [TransactionStatus.Initialized, TransactionStatus.Pending, TransactionStatus.Authorized],
        ["Reject"] = [TransactionStatus.Initialized, TransactionStatus.Pending, TransactionStatus.Authorized],
        ["Fail"] = [TransactionStatus.Initialized, TransactionStatus.Pending, TransactionStatus.Authorized],
        ["Authorize"] = [TransactionStatus.Initialized],
        ["PartiallyApprove"] = [TransactionStatus.Initialized],
        ["RequestVerification"] = [TransactionStatus.Initialized, TransactionStatus.Approved],
        ["Cancel"] = [TransactionStatus.Initialized, TransactionStatus.Pending],
        ["AwaitAuthentication"] = [TransactionStatus.Initialized],
        ["SetDeadline"] = [TransactionStatus.Initialized, TransactionStatus.Pending],
        ["SetStatusCheckFollowUp"] = [TransactionStatus.Pending]
    };

    private static IEnumerable<TestCaseData> AllPairs() =>
        from transition in Transitions.Keys
        from status in Enum.GetValues<TransactionStatus>()
        select new TestCaseData(status, transition, Legal[transition].Contains(status)).SetName($"{transition}_From{status}");

    private static EmulatedTransaction NewTransaction() => EmulatedTransaction.Open("praxis", "env", "m", "sale", "USD", 4900, Now);

    [TestCaseSource(nameof(AllPairs))]
    public void Transition_IsLegalOnlyFromItsSourceStatuses(TransactionStatus from, string transition, bool legal)
    {
        var tx = NewTransaction();
        Into[from](tx);

        if (legal)
        {
            Assert.DoesNotThrow(() => Transitions[transition](tx));
        }
        else
        {
            var ex = Assert.Throws<InvalidTransactionTransitionException>(() => Transitions[transition](tx));
            Assert.Multiple(() =>
            {
                Assert.That(ex!.From, Is.EqualTo(from));
                Assert.That(ex.Transition, Is.EqualTo(transition));
                Assert.That(tx.Status, Is.EqualTo(from), "an illegal transition changes nothing");
            });
        }
    }

    [Test]
    public void AwaitAuthentication_OnlyOnceRightAfterCreation()
    {
        var tx = NewTransaction();
        tx.AwaitAuthentication(Change, new TransactionDeadline { DueAt = Now, FollowUp = "x" }, Now);

        Assert.Throws<InvalidTransactionTransitionException>(() =>
            tx.AwaitAuthentication(Change, new TransactionDeadline { DueAt = Now, FollowUp = "y" }, Now));
    }

    [Test]
    public void Transitions_SetProcessedAmountsAmountAndHistory()
    {
        var tx = NewTransaction();
        tx.MarkPending(new StatusChange("1", "p", "open"), Now);
        Assert.Multiple(() =>
        {
            Assert.That(tx.ProcessedAmount, Is.EqualTo(4900));
            Assert.That(tx.ProcessedCurrency, Is.EqualTo("USD"));
        });

        tx.Approve(new StatusChange("11", "a", "successful") { FinalAmount = 1000 }, Now.AddSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(tx.Status, Is.EqualTo(TransactionStatus.Approved));
            Assert.That(tx.Amount, Is.EqualTo(1000));
            Assert.That(tx.ProcessedAmount, Is.EqualTo(4900), "processed stays the requested amount");
            Assert.That(tx.StatusCode, Is.EqualTo("11"));
            Assert.That(tx.SessionStatus, Is.EqualTo("successful"));
            Assert.That(tx.UpdatedAt, Is.EqualTo(Now.AddSeconds(1)));
            Assert.That(tx.History.Select(h => (h.From, h.To)), Is.EqualTo(new[]
            {
                (TransactionStatus.Initialized, TransactionStatus.Pending),
                (TransactionStatus.Pending, TransactionStatus.Approved)
            }));
        });

        tx.RequestVerification(new StatusChange("530", "v", "failed"), Now);
        Assert.That(tx.ProcessedAmount, Is.Null);
    }

    [Test]
    public void Deadline_TakenOnlyWhenDueAndClearedByTransitions()
    {
        var tx = NewTransaction();
        tx.SetDeadline(new TransactionDeadline { DueAt = Now.AddSeconds(10), FollowUp = "f" });

        Assert.Multiple(() =>
        {
            Assert.That(tx.TakeDueDeadline(Now.AddSeconds(9)), Is.Null);
            Assert.That(tx.TakeDueDeadline(Now.AddSeconds(10)), Is.EqualTo("f"));
            Assert.That(tx.Deadline, Is.Null);
        });

        tx.MarkPending(Change, Now);
        tx.SetStatusCheckFollowUp("check");
        tx.SetDeadline(new TransactionDeadline { DueAt = Now, FollowUp = "g" });
        tx.Approve(Change, Now);
        Assert.Multiple(() =>
        {
            Assert.That(tx.Deadline, Is.Null);
            Assert.That(tx.OnStatusCheck, Is.Null);
            Assert.That(tx.TakeStatusCheckFollowUp(), Is.Null);
        });
    }

    [Test]
    public void Clone_IsDeep()
    {
        var tx = NewTransaction();
        tx.ProviderData["k"] = "v";
        tx.SetDeadline(new TransactionDeadline { DueAt = Now, FollowUp = "f" });
        tx.MarkPending(Change, Now);
        tx.SetDeadline(new TransactionDeadline { DueAt = Now, FollowUp = "f" });

        var copy = tx.Clone();
        copy.ProviderData["k"] = "changed";
        copy.Approve(Change, Now);

        Assert.Multiple(() =>
        {
            Assert.That(tx.Data("k"), Is.EqualTo("v"));
            Assert.That(tx.Data("missing"), Is.Null);
            Assert.That(tx.Status, Is.EqualTo(TransactionStatus.Pending));
            Assert.That(tx.History, Has.Count.EqualTo(1));
            Assert.That(tx.Deadline, Is.Not.Null);
        });
    }

    [Test]
    public void StatusNames_AreTheWireNames()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TransactionStatus.PartialApproved.ToWire(), Is.EqualTo("partial_approved"));
            Assert.That(Enum.GetValues<TransactionStatus>().Select(s => s.ToWire()), Is.Unique);
            Assert.Throws<ArgumentOutOfRangeException>(() => ((TransactionStatus)99).ToWire());
        });
    }

    [Test]
    public void StatusJson_UsesWireNames()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { TransactionStatus.PartialApproved, TransactionStatus.Approved });

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo("""["partial_approved","approved"]"""));
            Assert.That(System.Text.Json.JsonSerializer.Deserialize<TransactionStatus[]>(json),
                Is.EqualTo(new[] { TransactionStatus.PartialApproved, TransactionStatus.Approved }));
            Assert.Throws<System.Text.Json.JsonException>(() =>
                System.Text.Json.JsonSerializer.Deserialize<TransactionStatus>("\"nope\""));
        });
    }

    [Test]
    public void OutcomeContext_ScalesDelaysAndSnapshotsNotifications()
    {
        var context = new OutcomeContext(Now, 0.5);
        var tx = NewTransaction();

        context.Notify(tx, TimeSpan.FromSeconds(10));
        context.Notify(tx, null);
        tx.Approve(Change, Now);
        var deadline = context.Deadline(TimeSpan.FromSeconds(540), "f");

        Assert.Multiple(() =>
        {
            Assert.That(context.Notifications, Has.Count.EqualTo(1));
            Assert.That(context.Notifications[0].Delay, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(context.Notifications[0].Snapshot.Status, Is.EqualTo(TransactionStatus.Initialized));
            Assert.That(deadline.DueAt, Is.EqualTo(Now.AddSeconds(270)));
            Assert.That(deadline.FollowUp, Is.EqualTo("f"));
        });
    }

    [Test]
    public void Cancel_ClearsProcessedValuesAndDeadline_WireNameCancelled()
    {
        var tx = NewTransaction();
        tx.MarkPending(Change, Now);
        tx.SetDeadline(new TransactionDeadline { DueAt = Now.AddMinutes(1), FollowUp = "x" });

        tx.Cancel(new StatusChange("0", "cancelled", "failed"), Now);

        Assert.Multiple(() =>
        {
            Assert.That(tx.Status, Is.EqualTo(TransactionStatus.Cancelled));
            Assert.That(tx.Status.ToWire(), Is.EqualTo("cancelled"));
            Assert.That(tx.ProcessedAmount, Is.Null);
            Assert.That(tx.ProcessedCurrency, Is.Null);
            Assert.That(tx.Deadline, Is.Null);
            Assert.That(System.Text.Json.JsonSerializer.Serialize(tx.Status), Is.EqualTo("\"cancelled\""));
            Assert.That(System.Text.Json.JsonSerializer.Deserialize<TransactionStatus>("\"cancelled\""), Is.EqualTo(TransactionStatus.Cancelled));
        });
    }

    [Test]
    public void Approve_WithProcessedCurrencyOverride_KeepsItAndTheProcessedAmount()
    {
        var tx = EmulatedTransaction.Open("praxis", "env", "m", "sale", "EUR", 1200, Now);

        tx.Approve(new StatusChange("12", "d", "successful") { FinalAmount = 1068, ProcessedAmount = 1200, ProcessedCurrency = "USD" }, Now);

        Assert.Multiple(() =>
        {
            Assert.That(tx.Amount, Is.EqualTo(1068));
            Assert.That(tx.ProcessedAmount, Is.EqualTo(1200));
            Assert.That(tx.ProcessedCurrency, Is.EqualTo("USD"));
            Assert.That(tx.Currency, Is.EqualTo("EUR"));
        });
    }

    [Test]
    public void AwaitAuthentication_WithoutDeadline_StaysInitializedWithNoDeadline()
    {
        var tx = NewTransaction();

        tx.AwaitAuthentication(new StatusChange("300", "redirect", "open"), deadline: null, Now);

        Assert.Multiple(() =>
        {
            Assert.That(tx.Status, Is.EqualTo(TransactionStatus.Initialized));
            Assert.That(tx.StatusCode, Is.EqualTo("300"));
            Assert.That(tx.Deadline, Is.Null);
            Assert.That(tx.History, Has.Count.EqualTo(1));
        });
        Assert.Throws<InvalidTransactionTransitionException>(() => tx.AwaitAuthentication(Change, null, Now), "awaited only once");
    }

    [Test]
    public void AwaitPageOutcome_ZeroScaledTimeout_SetsNoDeadline_PositiveSetsOne()
    {
        var immediate = NewTransaction();
        new AwaitPageOutcome(Change, TimeSpan.FromSeconds(450), "follow").Apply(immediate, new OutcomeContext(Now, timeScale: 0));
        var scaled = NewTransaction();
        new AwaitPageOutcome(Change, TimeSpan.FromSeconds(450), "follow").Apply(scaled, new OutcomeContext(Now, timeScale: 0.5));
        var none = NewTransaction();
        new AwaitPageOutcome(Change).Apply(none, new OutcomeContext(Now, timeScale: 1));

        Assert.Multiple(() =>
        {
            Assert.That(immediate.Deadline, Is.Null);
            Assert.That(scaled.Deadline!.DueAt, Is.EqualTo(Now.AddSeconds(225)));
            Assert.That(scaled.Deadline.FollowUp, Is.EqualTo("follow"));
            Assert.That(none.Deadline, Is.Null);
            Assert.That(none.Status, Is.EqualTo(TransactionStatus.Initialized));
        });
    }

    [Test]
    public void NotifyCopyOutcome_NotifiesTheCopyAndLeavesTheTransactionUntouched()
    {
        var tx = NewTransaction();
        var context = new OutcomeContext(Now, timeScale: 1);

        new NotifyCopyOutcome(new ApproveOutcome(new StatusChange("0", "copy", "successful") { FinalAmount = 100 }, TimeSpan.FromSeconds(3)))
            .Apply(tx, context);

        Assert.Multiple(() =>
        {
            Assert.That(tx.Status, Is.EqualTo(TransactionStatus.Initialized));
            Assert.That(tx.Amount, Is.EqualTo(4900));
            Assert.That(tx.History, Is.Empty);
            Assert.That(context.Notifications, Has.Count.EqualTo(1));
            Assert.That(context.Notifications[0].Snapshot.Status, Is.EqualTo(TransactionStatus.Approved));
            Assert.That(context.Notifications[0].Snapshot.Amount, Is.EqualTo(100));
            Assert.That(context.Notifications[0].Delay, Is.EqualTo(TimeSpan.FromSeconds(3)));
        });
    }

    [Test]
    public void AttachWalletOutcome_AttachesBeforeTheInnerOutcomeNotifies()
    {
        var tx = NewTransaction();
        var context = new OutcomeContext(Now, timeScale: 1);

        new AttachWalletOutcome(t => t.ProviderData["wallet"] = "{}", new CancelOutcome(Change, TimeSpan.Zero)).Apply(tx, context);

        Assert.Multiple(() =>
        {
            Assert.That(tx.Status, Is.EqualTo(TransactionStatus.Cancelled));
            Assert.That(context.Notifications.Single().Snapshot.Data("wallet"), Is.EqualTo("{}"));
        });
    }
}
