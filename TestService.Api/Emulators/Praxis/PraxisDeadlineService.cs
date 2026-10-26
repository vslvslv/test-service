namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// Applies due emulator deadlines (abandoned 3DS pages, timeouts) every second, so their
/// notifications are sent even when nobody asks for the transaction. Requests apply due deadlines
/// lazily too; optimistic versioning makes whichever saves first win.
/// </summary>
public class PraxisDeadlineService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly PraxisEmulator _emulator;
    private readonly TimeProvider _time;
    private readonly ILogger<PraxisDeadlineService> _logger;

    public PraxisDeadlineService(PraxisEmulator emulator, TimeProvider time, ILogger<PraxisDeadlineService> logger)
    {
        _emulator = emulator;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            try
            {
                await Task.Delay(Interval, _time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One pass; never throws except on cancellation.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _emulator.ApplyDueDeadlinesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Applying due Praxis emulator deadlines failed");
            return 0;
        }
    }
}
