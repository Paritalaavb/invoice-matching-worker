using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using InvoiceMatching.Domain;

namespace InvoiceMatching.Extraction;

public sealed partial class MockInvoiceExtractor : IInvoiceExtractor
{
    public async Task<ExtractedInvoice> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var text = IsPdf(bytes) ? ReadPdfText(bytes) : Encoding.UTF8.GetString(bytes);
        return Parse(text);
    }

    public static ExtractedInvoice Parse(string text)
    {
        string? vendor = null;
        string? poNumber = null;
        string? invoiceNumber = null;
        decimal? amount = null;
        DateOnly? invoiceDate = null;

        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Replace(" ", "").Replace("_", "").Trim().ToLowerInvariant();
            var value = line[(separator + 1)..].Trim();
            if (value.Length == 0)
            {
                continue;
            }

            switch (key)
            {
                case "vendor":
                    vendor = value;
                    break;
                case "amount":
                    amount = ParseAmount(value);
                    break;
                case "invoicedate":
                    invoiceDate = DateOnly.TryParseExact(
                        value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        ? date
                        : null;
                    break;
                case "ponumber":
                    poNumber = value;
                    break;
                case "invoicenumber":
                    invoiceNumber = value;
                    break;
            }
        }

        return new ExtractedInvoice(vendor, amount, invoiceDate, poNumber, invoiceNumber);
    }

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length >= 5 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F' && bytes[4] == '-';

    private static string ReadPdfText(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var lines = new List<string>();

        foreach (Match match in ShowTextOperator().Matches(raw))
        {
            lines.Add(PdfEscape().Replace(match.Groups["t"].Value, "$1"));
        }

        return string.Join('\n', lines);
    }

    private static decimal? ParseAmount(string value)
    {
        var cleaned = value.Replace("$", "").Replace(",", "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    [GeneratedRegex(@"\((?<t>(?:\\.|[^\\)])*)\)\s*Tj")]
    private static partial Regex ShowTextOperator();

    [GeneratedRegex(@"\\(.)")]
    private static partial Regex PdfEscape();
}
