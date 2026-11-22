using InvoiceMatching.Domain;
using InvoiceMatching.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvoiceMatching.Health;

public sealed class WorkerHealthCheck(WorkQueue queue, InvoiceStore store, WorkerStatus status) : IHealthCheck
{
    private static readonly TimeSpan StallThreshold = TimeSpan.FromMinutes(2);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var depth = queue.Depth;
        var lastRun = status.LastSuccessfulRun;

        var data = new Dictionary<string, object>
        {
            ["queueDepth"] = depth,
            ["retrying"] = store.Count(r => r.Status == InvoiceStatus.Retrying),
            ["deadLettered"] = store.Count(r => r.Status == InvoiceStatus.Exception),
            ["lastSuccessfulRun"] = lastRun?.ToString("O") ?? "never"
        };

        var reference = lastRun ?? status.StartedAt;
        var stalled = depth > 0 && DateTimeOffset.UtcNow - reference > StallThreshold;

        return Task.FromResult(stalled
            ? HealthCheckResult.Degraded("Items are waiting but nothing has been processed recently", data: data)
            : HealthCheckResult.Healthy("Worker is running", data));
    }
}
