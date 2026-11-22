using InvoiceMatching.Domain;
using InvoiceMatching.Extraction;
using InvoiceMatching.Infrastructure;
using Microsoft.Extensions.Options;

namespace InvoiceMatching.Workers;

public sealed class InvoiceProcessor(
    WorkQueue queue,
    InvoiceStore store,
    IInvoiceExtractor extractor,
    PurchaseOrderSource purchaseOrders,
    WorkerStatus status,
    IOptions<IntakeOptions> options,
    ILogger<InvoiceProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(item, stoppingToken);
                    status.MarkSuccess();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error while processing {File}", item.FilePath);
                    RecordNonMatch(item, ExceptionKind.ProcessingError, $"Processing error: {ex.Message}", [], stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessAsync(WorkItem item, CancellationToken ct)
    {
        var attempt = store.Update(
            item.InvoiceId,
            r => r with { Status = InvoiceStatus.Processing, Attempts = r.Attempts + 1 }).Attempts;
        var fileName = Path.GetFileName(item.FilePath);
        logger.LogInformation("Processing {File} (attempt {Attempt})", fileName, attempt);

        var extracted = await extractor.ExtractAsync(item.FilePath, ct);
        store.Update(item.InvoiceId, r => r with { Extracted = extracted });

        var orders = await purchaseOrders.LoadAsync(ct);
        var result = InvoiceMatcher.Match(extracted, orders);

        if (result.Outcome == MatchOutcome.Matched)
        {
            store.Update(item.InvoiceId, r => r with
            {
                Status = InvoiceStatus.Matched,
                MatchedPONumber = result.Match!.PONumber,
                Method = result.Method,
                Reason = result.Reason,
                Failure = null,
                CandidatePONumbers = []
            });
            logger.LogInformation("Matched {File} to {PO}", fileName, result.Match!.PONumber);
            return;
        }

        var kind = result.Outcome switch
        {
            MatchOutcome.Ambiguous => ExceptionKind.Ambiguous,
            MatchOutcome.Unreadable => ExceptionKind.Unreadable,
            _ => ExceptionKind.NoMatch
        };
        RecordNonMatch(item, kind, result.Reason, result.Candidates, ct);
    }

    private void RecordNonMatch(
        WorkItem item,
        ExceptionKind kind,
        string reason,
        IReadOnlyList<string> candidates,
        CancellationToken ct)
    {
        if (store.Get(item.InvoiceId) is not { } current)
        {
            return;
        }

        if (current.Attempts >= options.Value.MaxAttempts)
        {
            store.Update(item.InvoiceId, r => r with
            {
                Status = InvoiceStatus.Exception,
                Failure = kind,
                Reason = $"{reason} (gave up after {r.Attempts} attempts)",
                CandidatePONumbers = candidates
            });
            logger.LogWarning(
                "{File} moved to exceptions: {Reason}", Path.GetFileName(item.FilePath), reason);
            return;
        }

        store.Update(item.InvoiceId, r => r with
        {
            Status = InvoiceStatus.Retrying,
            Failure = kind,
            Reason = reason,
            CandidatePONumbers = candidates
        });
        ScheduleRetry(item, ct);
    }

    private void ScheduleRetry(WorkItem item, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(options.Value.RetryDelaySeconds);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, ct);
                queue.Enqueue(item);
            }
            catch (OperationCanceledException)
            {
            }
        }, CancellationToken.None);
    }
}
