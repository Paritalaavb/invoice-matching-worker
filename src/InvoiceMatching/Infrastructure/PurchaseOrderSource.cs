using System.Globalization;
using System.Text;
using InvoiceMatching.Domain;
using Microsoft.Extensions.Options;

namespace InvoiceMatching.Infrastructure;

public sealed class PurchaseOrderSource(IOptions<IntakeOptions> options, ILogger<PurchaseOrderSource> logger)
{
   
    public async Task<IReadOnlyList<PurchaseOrder>> LoadAsync(CancellationToken cancellationToken)
    {
        var path = options.Value.PurchaseOrdersPath;
        if (!File.Exists(path))
        {
            logger.LogWarning("Purchase order file not found at {Path}", path);
            return [];
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var orders = PurchaseOrderCsv.Parse(lines);

        var dataRows = lines.Skip(1).Count(l => !string.IsNullOrWhiteSpace(l));
        if (orders.Count < dataRows)
        {
            logger.LogWarning("Skipped {Count} malformed row(s) in {Path}", dataRows - orders.Count, path);
        }

        return orders;
    }
}

public static class PurchaseOrderCsv
{
    public static List<PurchaseOrder> Parse(IEnumerable<string> lines)
    {
        var orders = new List<PurchaseOrder>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var f = SplitLine(line);
            if (f.Count < 4
                || string.IsNullOrWhiteSpace(f[0])
                || !decimal.TryParse(f[2].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                || !Enum.TryParse<PoStatus>(f[3].Trim(), ignoreCase: true, out var status))
            {
                continue;
            }

            orders.Add(new PurchaseOrder(f[0].Trim(), f[1].Trim(), amount, status));
        }

        return orders;
    }

    public static List<string> SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
