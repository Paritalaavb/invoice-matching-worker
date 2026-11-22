using InvoiceMatching.Domain;

namespace InvoiceMatching.Extraction;

public interface IInvoiceExtractor
{
    Task<ExtractedInvoice> ExtractAsync(string filePath, CancellationToken cancellationToken);
}
