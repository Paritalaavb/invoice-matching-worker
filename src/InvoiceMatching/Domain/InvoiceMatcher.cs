using System.Globalization;
using System.Text;

namespace InvoiceMatching.Domain;

public enum MatchOutcome
{
    Matched,
    Ambiguous,
    NoMatch,
    Unreadable
}

public sealed record MatchResult(
    MatchOutcome Outcome,
    string Reason,
    PurchaseOrder? Match = null,
    MatchMethod? Method = null)
{
    public IReadOnlyList<string> Candidates { get; init; } = [];
}

public static class InvoiceMatcher
{
    private const decimal AmountTolerance = 0.01m;

    public static MatchResult Match(ExtractedInvoice invoice, IReadOnlyCollection<PurchaseOrder> purchaseOrders)
    {
        if (invoice.IsEmpty)
        {
            return new MatchResult(MatchOutcome.Unreadable, "Extraction returned no fields");
        }

        return string.IsNullOrWhiteSpace(invoice.PONumber)
            ? MatchByVendorAndAmount(invoice, purchaseOrders)
            : MatchByPoNumber(invoice, purchaseOrders);
    }

    private static MatchResult MatchByPoNumber(ExtractedInvoice invoice, IReadOnlyCollection<PurchaseOrder> purchaseOrders)
    {
        var number = invoice.PONumber!.Trim();
        var po = purchaseOrders.FirstOrDefault(p =>
            string.Equals(p.PONumber, number, StringComparison.OrdinalIgnoreCase));

        if (po is null)
        {
            return NoMatch($"PO {number} was not found in the purchase order file");
        }

        if (po.Status != PoStatus.Open)
        {
            return NoMatch($"PO {po.PONumber} is {po.Status}");
        }

        if (invoice.Vendor is not null && !SameVendor(invoice.Vendor, po.Vendor))
        {
            return NoMatch($"Vendor '{invoice.Vendor}' does not match vendor '{po.Vendor}' on PO {po.PONumber}");
        }

        if (invoice.Amount is { } amount && Math.Abs(amount - po.Amount) > AmountTolerance)
        {
            return NoMatch(
                $"Invoice amount {Money(amount)} does not match amount {Money(po.Amount)} on PO {po.PONumber}");
        }

        return new MatchResult(MatchOutcome.Matched, "Matched on PO number", po, MatchMethod.PONumber);
    }

    private static MatchResult MatchByVendorAndAmount(ExtractedInvoice invoice, IReadOnlyCollection<PurchaseOrder> purchaseOrders)
    {
        if (invoice.Vendor is null || invoice.Amount is null)
        {
            return NoMatch("No PO number, and vendor or amount is missing so a fallback match is not possible");
        }

        var vendor = invoice.Vendor;
        var amount = invoice.Amount.Value;

        var candidates = purchaseOrders
            .Where(p => p.Status == PoStatus.Open
                        && SameVendor(vendor, p.Vendor)
                        && Math.Abs(amount - p.Amount) <= AmountTolerance)
            .ToList();

        if (candidates.Count == 0)
        {
            return NoMatch($"No open PO found for vendor '{vendor}' with amount {Money(amount)}");
        }

        if (candidates.Count == 1)
        {
            return new MatchResult(
                MatchOutcome.Matched,
                "Matched on vendor and amount (no PO number on invoice)",
                candidates[0],
                MatchMethod.VendorAndAmount);
        }

        var numbers = candidates.Select(c => c.PONumber).ToList();
        return new MatchResult(
            MatchOutcome.Ambiguous,
            $"{numbers.Count} open POs match vendor and amount: {string.Join(", ", numbers)}")
        {
            Candidates = numbers
        };
    }

    public static bool SameVendor(string a, string b) =>
        string.Equals(NormaliseVendor(a), NormaliseVendor(b), StringComparison.Ordinal);

    public static string NormaliseVendor(string vendor)
    {
        var sb = new StringBuilder(vendor.Length);
        foreach (var c in vendor.Trim())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (char.IsWhiteSpace(c) && sb.Length > 0 && sb[^1] != ' ')
            {
                sb.Append(' ');
            }
        }

        return sb.ToString().Trim();
    }

    private static MatchResult NoMatch(string reason) => new(MatchOutcome.NoMatch, reason);

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
