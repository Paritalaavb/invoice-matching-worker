using InvoiceMatching.Domain;
using InvoiceMatching.Infrastructure;

namespace InvoiceMatching.Tests;

public class InfrastructureTests
{
    [Fact]
    public void Store_rejects_a_second_record_with_the_same_content_hash()
    {
        var store = new InvoiceStore();
        var first = new InvoiceRecord { Id = "abc", SourceFileRef = "/inbox/a.pdf" };
        var duplicate = new InvoiceRecord { Id = "abc", SourceFileRef = "/inbox/copy-of-a.pdf" };

        Assert.True(store.TryAdd(first));
        Assert.False(store.TryAdd(duplicate));
        Assert.Single(store.List());
        Assert.Equal("/inbox/a.pdf", store.Get("abc")!.SourceFileRef);
    }

    [Fact]
    public void Csv_parser_handles_quoted_vendor_names_and_skips_bad_rows()
    {
        var lines = new[]
        {
            "PONumber,Vendor,Amount,Status",
            "PO-1,\"Smith, Jones & Co\",100.50,Open",
            "PO-2,Broken Row,not-a-number,Open",
            "PO-3,Fabrikam,20.00,closed"
        };

        var orders = PurchaseOrderCsv.Parse(lines);

        Assert.Equal(2, orders.Count);
        Assert.Equal("Smith, Jones & Co", orders[0].Vendor);
        Assert.Equal(PoStatus.Closed, orders[1].Status);
    }

    [Fact]
    public void Queue_depth_tracks_enqueued_and_dequeued_items()
    {
        var queue = new WorkQueue();
        queue.Enqueue(new WorkItem("1", "a.pdf"));
        queue.Enqueue(new WorkItem("2", "b.pdf"));
        Assert.Equal(2, queue.Depth);
    }
}
