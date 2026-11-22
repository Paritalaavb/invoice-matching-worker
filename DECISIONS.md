# Decision log

- I used a Channel with a BackgroundService for the queue. It gives me a proper producer and consumer without installing anything extra. RabbitMQ or Service Bus would take too much time for 3.5 hours.
- I scan the inbox folder every few seconds instead of using FileSystemWatcher. The watcher can fire while the file is still being copied, and it can miss events. Polling is simpler and I can trust it.
- The idempotency key is the SHA-256 hash of the file content. If the same file comes again with a different name, it is skipped. File name is not reliable, so I did not use it.
- If an invoice does not match, I retry it up to 3 times and then move it to the exception state. The PO file is read again on every attempt, so a PO added later can still match. I kept ambiguous and unreadable invoices in the same retry flow to follow the 3 attempts rule.
- If the invoice has a PO number that is not in the file, I do not fall back to vendor and amount. The fallback is only for invoices with no PO number. A wrong PO number should go for review, not be guessed.
- The matching logic is one static method with no file or database access. This made it easy to unit test all the cases.
- I kept the data in memory behind a small store class. It is enough for this task and can be replaced with EF Core later. The drawback is that data is lost when the app restarts.
- The health check shows degraded only when items are waiting and nothing has been processed for 2 minutes. Exception items are shown as a count, not as unhealthy, because they are a normal result.
