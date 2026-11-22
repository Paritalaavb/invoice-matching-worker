namespace InvoiceMatching.Infrastructure;

public sealed class WorkerStatus
{
    private long _lastSuccessTicks;

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSuccessfulRun
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSuccessTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void MarkSuccess() =>
        Interlocked.Exchange(ref _lastSuccessTicks, DateTimeOffset.UtcNow.UtcTicks);
}
