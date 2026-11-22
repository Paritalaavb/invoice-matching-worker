using InvoiceMatching.Domain;
using InvoiceMatching.Infrastructure;

namespace InvoiceMatching.Api;

public sealed record MatchDto(string PONumber, MatchMethod? Method, string Reason, string Link);

public sealed record ProblemDto(ExceptionKind? Kind, string? Reason, IReadOnlyList<string> CandidatePOs);

public sealed record InvoiceDto(
    string Id,
    InvoiceStatus Status,
    string SourceFileRef,
    int Attempts,
    ExtractedInvoice? Extracted,
    MatchDto? Match,
    ProblemDto? Problem,
    DateTimeOffset ReceivedAt,
    DateTimeOffset UpdatedAt);

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/invoices", (string? status, InvoiceStore store) =>
        {
            IEnumerable<InvoiceRecord> items = store.List();

            if (!string.IsNullOrWhiteSpace(status))
            {
                IEnumerable<InvoiceRecord>? filtered = status.ToLowerInvariant() switch
                {
                    "matched" => items.Where(r => r.Status == InvoiceStatus.Matched),
                    "exception" or "exceptions" => items.Where(r => r.Status == InvoiceStatus.Exception),
                    "unmatched" or "pending" => items.Where(r => IsPending(r)),
                    _ => null
                };

                if (filtered is null)
                {
                    return Results.BadRequest(new { error = "status must be one of: matched, unmatched, exception" });
                }

                items = filtered;
            }

            return Results.Ok(items.Select(ToDto).ToList());
        });

        app.MapGet("/invoices/summary", (InvoiceStore store) => Results.Ok(new
        {
            matched = store.Count(r => r.Status == InvoiceStatus.Matched),
            unmatched = store.Count(IsPending),
            exceptions = store.Count(r => r.Status == InvoiceStatus.Exception)
        }));

        app.MapGet("/invoices/{id}", (string id, InvoiceStore store) =>
            store.Get(id) is { } record ? Results.Ok(ToDto(record)) : Results.NotFound());

        app.MapPost("/invoices/{id}/reprocess", (string id, InvoiceStore store, WorkQueue queue) =>
        {
            var record = store.Get(id);
            if (record is null)
            {
                return Results.NotFound();
            }

            if (record.Status != InvoiceStatus.Exception)
            {
                return Results.Conflict(new { error = "Only invoices in the exception state can be reprocessed." });
            }

            store.Update(id, r => r with
            {
                Status = InvoiceStatus.Queued,
                Attempts = 0,
                Failure = null,
                Reason = null,
                CandidatePONumbers = []
            });
            queue.Enqueue(new WorkItem(id, record.SourceFileRef));
            return Results.Accepted($"/invoices/{id}");
        });

        app.MapGet("/purchase-orders/{poNumber}", async (
            string poNumber, PurchaseOrderSource source, InvoiceStore store, CancellationToken ct) =>
        {
            var orders = await source.LoadAsync(ct);
            var po = orders.FirstOrDefault(p =>
                string.Equals(p.PONumber, poNumber, StringComparison.OrdinalIgnoreCase));
            if (po is null)
            {
                return Results.NotFound();
            }

            var invoiceIds = store.List()
                .Where(r => string.Equals(r.MatchedPONumber, po.PONumber, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Id)
                .ToList();

            return Results.Ok(new
            {
                po.PONumber,
                po.Vendor,
                po.Amount,
                Status = po.Status.ToString(),
                MatchedInvoiceIds = invoiceIds
            });
        });

        return app;
    }

    private static bool IsPending(InvoiceRecord r) =>
        r.Status is InvoiceStatus.Queued or InvoiceStatus.Processing or InvoiceStatus.Retrying;

    private static InvoiceDto ToDto(InvoiceRecord r)
    {
        var match = r.Status == InvoiceStatus.Matched && r.MatchedPONumber is not null
            ? new MatchDto(r.MatchedPONumber, r.Method, r.Reason ?? "", $"/purchase-orders/{r.MatchedPONumber}")
            : null;

        var problem = r.Failure is not null && r.Status is InvoiceStatus.Retrying or InvoiceStatus.Exception
            ? new ProblemDto(r.Failure, r.Reason, r.CandidatePONumbers)
            : null;

        return new InvoiceDto(
            r.Id, r.Status, r.SourceFileRef, r.Attempts, r.Extracted, match, problem, r.ReceivedAt, r.UpdatedAt);
    }
}
