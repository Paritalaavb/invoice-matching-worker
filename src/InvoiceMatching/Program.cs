using System.Text.Json.Serialization;
using InvoiceMatching.Api;
using InvoiceMatching.Extraction;
using InvoiceMatching.Health;
using InvoiceMatching.Infrastructure;
using InvoiceMatching.Workers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<IntakeOptions>(builder.Configuration.GetSection(IntakeOptions.Section));
builder.Services.PostConfigure<IntakeOptions>(o => o.InboxPath = Path.GetFullPath(o.InboxPath));

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<InvoiceStore>();
builder.Services.AddSingleton<WorkQueue>();
builder.Services.AddSingleton<WorkerStatus>();
builder.Services.AddSingleton<PurchaseOrderSource>();
builder.Services.AddSingleton<IInvoiceExtractor, MockInvoiceExtractor>();

builder.Services.AddHostedService<InboxScanner>();
builder.Services.AddHostedService<InvoiceProcessor>();

builder.Services.AddHealthChecks().AddCheck<WorkerHealthCheck>("worker");

var app = builder.Build();

app.MapInvoiceEndpoints();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var worker = report.Entries["worker"];
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            description = worker.Description,
            details = worker.Data
        });
    }
});

const string homePage = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Invoice Matching</title>
<style>
  body { font-family: Segoe UI, Arial, sans-serif; max-width: 640px; margin: 40px auto; padding: 0 16px; color: #222; }
  h1 { font-size: 22px; }
  li { margin: 6px 0; }
  code { background: #eee; padding: 1px 5px; border-radius: 3px; }
</style>
</head>
<body>
<h1>Invoice Matching Worker and API</h1>
<p>Drop PDF invoices into the <code>inbox</code> folder and check the results here.</p>
<ul>
  <li><a href="/invoices/summary">/invoices/summary</a> counts</li>
  <li><a href="/invoices">/invoices</a> all invoices</li>
  <li><a href="/invoices?status=matched">/invoices?status=matched</a></li>
  <li><a href="/invoices?status=unmatched">/invoices?status=unmatched</a> not finished yet</li>
  <li><a href="/invoices?status=exception">/invoices?status=exception</a></li>
  <li><a href="/health">/health</a> queue depth and last successful run</li>
</ul>
</body>
</html>
""";

app.MapGet("/", () => Results.Content(homePage, "text/html"));

app.Run();
