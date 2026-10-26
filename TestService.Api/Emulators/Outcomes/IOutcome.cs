using TestService.Api.Emulators.Transactions;

namespace TestService.Api.Emulators.Outcomes;

/// <summary>
/// One emulated behaviour: it moves a transaction through aggregate methods and records the
/// notifications to send, each with its delay. Behaviour lives in C# classes, not in a rule language.
/// </summary>
public interface IOutcome
{
    void Apply(EmulatedTransaction transaction, OutcomeContext context);
}

/// <summary>A notification to send: a snapshot of the transaction when it was recorded, and its (scaled) delay.</summary>
public sealed record ScheduledNotification(EmulatedTransaction Snapshot, TimeSpan Delay);

/// <summary>Clock and time scale for one outcome application, and the notifications it records.</summary>
public sealed class OutcomeContext
{
    public OutcomeContext(DateTime now, double timeScale)
    {
        Now = now;
        TimeScale = timeScale;
    }

    public DateTime Now { get; }

    /// <summary>The merchant's <c>timeScale</c>: multiplies every delay.</summary>
    public double TimeScale { get; }

    public List<ScheduledNotification> Notifications { get; } = new();

    public TimeSpan Scale(TimeSpan delay) => TimeSpan.FromTicks((long)(delay.Ticks * TimeScale));

    /// <summary>Records a notification of the transaction's current state, sent after the scaled <paramref name="after"/>.</summary>
    public void Notify(EmulatedTransaction transaction, TimeSpan? after)
    {
        if (after is { } delay)
        {
            Notifications.Add(new ScheduledNotification(transaction.Clone(), Scale(delay)));
        }
    }

    /// <summary>A deadline due after the scaled <paramref name="after"/>.</summary>
    public TransactionDeadline Deadline(TimeSpan after, string followUp) =>
        new() { DueAt = Now + Scale(after), FollowUp = followUp };
}
