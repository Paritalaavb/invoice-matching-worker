namespace InvoiceMatching.Infrastructure;

public sealed class IntakeOptions
{
    public const string Section = "Intake";

    public string InboxPath { get; set; } = "inbox";
    public string PurchaseOrdersFileName { get; set; } = "purchase-orders.csv";
    public int PollSeconds { get; set; } = 3;
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;

    public string PurchaseOrdersPath => Path.Combine(InboxPath, PurchaseOrdersFileName);
}
