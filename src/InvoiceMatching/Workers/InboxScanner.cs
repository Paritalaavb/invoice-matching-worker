using System.Security.Cryptography;
using InvoiceMatching.Domain;
using InvoiceMatching.Infrastructure;
using Microsoft.Extensions.Options;

namespace InvoiceMatching.Workers;

public sealed class InboxScanner(
    IOptions<IntakeOptions> options,
    InvoiceStore store,
    WorkQueue queue,
    ILogger<InboxScanner> logger) : BackgroundService
{
    private readonly Dictionary<string, (long Length, DateTime LastWriteUtc)> _seen = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        Directory.CreateDirectory(settings.InboxPath);
        logger.LogInformation("Watching {Inbox} for invoices", settings.InboxPath);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds));
        try
        {
            do
            {
                try
                {
                    await ScanAsync(settings.InboxPath, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Inbox scan failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ScanAsync(string inbox, CancellationToken ct)
    {
        foreach (var path in Directory.EnumerateFiles(inbox, "*.pdf", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var info = new FileInfo(path);
            var signature = (info.Length, info.LastWriteTimeUtc);
            if (_seen.TryGetValue(path, out var known) && known == signature)
            {
                continue;
            }

            string hash;
            try
            {
                hash = await ComputeHashAsync(path, ct);
            }
            catch (IOException)
            {
                logger.LogDebug("{File} is still being written, will retry on next scan", info.Name);
                continue;
            }

            _seen[path] = signature;

            var now = DateTimeOffset.UtcNow;
            var record = new InvoiceRecord
            {
                Id = hash,
                SourceFileRef = path,
                ReceivedAt = now,
                UpdatedAt = now
            };

            if (!store.TryAdd(record))
            {
                logger.LogInformation("Skipping {File}: identical content was already received", info.Name);
                continue;
            }

            queue.Enqueue(new WorkItem(hash, path));
            logger.LogInformation("Queued {File}", info.Name);
        }
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
