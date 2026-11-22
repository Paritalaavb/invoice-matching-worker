# Invoice Matching Worker and API

This is a .NET 8 background worker with a small web API. It watches a folder for invoice files, reads the main fields from each one (this part is mocked), and matches the invoice to a purchase order from a CSV file. The results can be viewed through the API.

## How to run

You need the .NET 8 SDK. If you have a newer SDK, change `TargetFramework` in both projects.

```
cd src\InvoiceMatching
dotnet run
```

You can also open the project in Visual Studio and press F5.

The API runs at http://localhost:5080. The app watches the `inbox` folder in the repo root, which already has `purchase-orders.csv` in it.

To run the tests:

```
dotnet test tests\InvoiceMatching.Tests
```

## How to try it

Happy path. Copy the three files from `samples\happy-path` into the `inbox` folder, then open http://localhost:5080/invoices?status=matched in the browser.

Contoso and Fabrikam match on the PO number. Northwind has no PO number, so it matches on vendor and amount.

Edge case. Copy the two files from `samples\edge-case` into the `inbox` folder.

- http://localhost:5080/invoices?status=unmatched shows them while they are being retried
- http://localhost:5080/invoices?status=exception shows them after the third attempt, about 5 seconds later
- http://localhost:5080/health shows the queue depth and the last successful run

Duplicate check. Copy any file again with a different name. The log says the content was already received and nothing new is added.

Note: the sample `.pdf` files are plain text with a `.pdf` extension, so they do not open in a PDF viewer. Open them in Notepad to see the content. They stand in for the text of a real PDF.

## API

| Method | Path | What it does |
| --- | --- | --- |
| GET | /invoices?status=matched, unmatched or exception | Lists invoices. Unmatched means not finished yet (queued, processing or retrying) |
| GET | /invoices/summary | Count of matched, unmatched and exception invoices |
| GET | /invoices/{id} | One invoice. The id is the SHA-256 hash of the file content |
| POST | /invoices/{id}/reprocess | Tries an exception invoice again on the same record |
| GET | /purchase-orders/{poNumber} | A purchase order and the invoices matched to it |
| GET | /health | Queue depth, retrying count, exception count and last successful run |

## How it works

A scanner checks the inbox folder every few seconds. Each new file gets an id from its content hash. If that id is already known, the file is skipped. Otherwise it goes into a queue.

One worker reads from the queue. It extracts the fields, reads the PO file, and runs the matching. If the invoice does not match for any reason (no match, ambiguous, unreadable or an error), it is tried again after a short delay. After 3 attempts it moves to the exception state with the reason, the possible PO numbers if there are any, and the original file path.

The matching rules:

1. If no fields were extracted, the invoice is unreadable.
2. If the invoice has a PO number, that PO must exist, be open, and match the vendor and amount. If the PO number is not found, there is no fallback.
3. If the invoice has no PO number, I look for open POs with the same vendor and amount. One result is a match. More than one is ambiguous.

The extraction reads `Key: Value` lines from the file. To use a real extraction service, replace the
