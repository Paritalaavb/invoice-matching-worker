using InvoiceMatching.Extraction;

namespace InvoiceMatching.Tests;

public class ExtractorTests
{
    private static string WritePdf(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "%PDF-1.4\nstream\n" + content + "\nendstream\n%%EOF\n");
        return path;
    }

    [Fact]
    public async Task Reads_fields_from_the_text_layer_of_a_pdf()
    {
        var path = WritePdf(
            "BT (Vendor: Contoso \\(UK\\) Ltd) Tj ET\nBT (PONumber: PO-1001) Tj ET\nBT (Amount: 1,250.00) Tj ET");

        var result = await new MockInvoiceExtractor().ExtractAsync(path, CancellationToken.None);

        Assert.Equal("Contoso (UK) Ltd", result.Vendor);
        Assert.Equal("PO-1001", result.PONumber);
        Assert.Equal(1250.00m, result.Amount);
    }

    [Fact]
    public async Task Pdf_without_text_gives_an_empty_extraction()
    {
        var path = WritePdf("0.9 g 40 40 515 760 re f");

        var result = await new MockInvoiceExtractor().ExtractAsync(path, CancellationToken.None);

        Assert.True(result.IsEmpty);
    }
}
