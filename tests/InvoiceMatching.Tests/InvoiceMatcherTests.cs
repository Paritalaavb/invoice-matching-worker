using InvoiceMatching.Domain;

namespace InvoiceMatching.Tests;

public class InvoiceMatcherTests
{
    private static readonly PurchaseOrder[] Orders =
    [
        new("PO-1", "Contoso Supplies Ltd", 1250.00m, PoStatus.Open),
        new("PO-2", "Northwind Traders", 2150.75m, PoStatus.Open),
        new("PO-3", "Adventure Works Cycles", 3300.00m, PoStatus.Open),
        new("PO-4", "Adventure Works Cycles", 3300.00m, PoStatus.Open),
        new("PO-5", "Litware Inc", 980.00m, PoStatus.Closed)
    ];

    private static ExtractedInvoice Invoice(
        string? vendor = null, decimal? amount = null, string? po = null) =>
        new(vendor, amount, null, po, "INV-1");

    [Fact]
    public void Matches_on_po_number()
    {
        var result = InvoiceMatcher.Match(Invoice("Contoso Supplies Ltd", 1250.00m, "PO-1"), Orders);

        Assert.Equal(MatchOutcome.Matched, result.Outcome);
        Assert.Equal("PO-1", result.Match!.PONumber);
        Assert.Equal(MatchMethod.PONumber, result.Method);
    }

    [Fact]
    public void Falls_back_to_vendor_and_amount_when_po_number_is_absent()
    {
        var result = InvoiceMatcher.Match(Invoice("Northwind Traders", 2150.75m), Orders);

        Assert.Equal(MatchOutcome.Matched, result.Outcome);
        Assert.Equal("PO-2", result.Match!.PONumber);
        Assert.Equal(MatchMethod.VendorAndAmount, result.Method);
    }

    [Fact]
    public void Two_open_pos_with_same_vendor_and_amount_are_ambiguous()
    {
        var result = InvoiceMatcher.Match(Invoice("Adventure Works Cycles", 3300.00m), Orders);

        Assert.Equal(MatchOutcome.Ambiguous, result.Outcome);
        Assert.Equal(new[] { "PO-3", "PO-4" }, result.Candidates);
    }

    [Fact]
    public void Extraction_with_no_fields_is_unreadable()
    {
        var result = InvoiceMatcher.Match(new ExtractedInvoice(null, null, null, null, null), Orders);

        Assert.Equal(MatchOutcome.Unreadable, result.Outcome);
    }

    [Fact]
    public void Closed_po_is_not_matched()
    {
        var result = InvoiceMatcher.Match(Invoice("Litware Inc", 980.00m, "PO-5"), Orders);

        Assert.Equal(MatchOutcome.NoMatch, result.Outcome);
        Assert.Contains("Closed", result.Reason);
    }

    [Fact]
    public void Unknown_po_number_does_not_fall_back_to_vendor_and_amount()
    {
        var result = InvoiceMatcher.Match(Invoice("Northwind Traders", 2150.75m, "PO-999"), Orders);

        Assert.Equal(MatchOutcome.NoMatch, result.Outcome);
    }

    [Fact]
    public void Amount_mismatch_on_stated_po_is_rejected()
    {
        var result = InvoiceMatcher.Match(Invoice("Contoso Supplies Ltd", 1300.00m, "PO-1"), Orders);

        Assert.Equal(MatchOutcome.NoMatch, result.Outcome);
    }

    [Fact]
    public void Vendor_comparison_ignores_case_and_punctuation()
    {
        var result = InvoiceMatcher.Match(Invoice("contoso supplies, ltd.", 1250.00m), Orders);

        Assert.Equal(MatchOutcome.Matched, result.Outcome);
        Assert.Equal("PO-1", result.Match!.PONumber);
    }
}
