namespace InvoiceMatching.Domain;

public enum PoStatus
{
    Open,
    Closed
}

public sealed record PurchaseOrder(string PONumber, string Vendor, decimal Amount, PoStatus Status);

public sealed record ExtractedInvoice(
    string? Vendor,
    decimal? Amount,
    DateOnly? InvoiceDate,
    string? PONumber,
    string? InvoiceNumber)
{
    public bool IsEmpty =>
        Vendor is null && Amount is null && InvoiceDate is null && PONumber is null && InvoiceNumber is null;
}

public enum InvoiceStatus
{
    Queued,
    Processing,
    Retrying,
    Matched,
    Exception
}

public enum MatchMethod
{
    PONumber,
    VendorAndAmount
}

public enum ExceptionKind
{
    Ambiguous,
    Unreadable,
    NoMatch,
    ProcessingError
}

public sealed record InvoiceRecord
{
    public required string Id { get; init; }
    public required string SourceFileRef { get; init; }
    public InvoiceStatus Status { get; init; } = InvoiceStatus.Queued;
    public int Attempts { get; init; }
    public ExtractedInvoice? Extracted { get; init; }
    public string? MatchedPONumber { get; init; }
    public MatchMethod? Method { get; init; }
    public ExceptionKind? Failure { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<string> CandidatePONumbers { get; init; } = [];
    public DateTimeOffset ReceivedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record WorkItem(string InvoiceId, string FilePath);
