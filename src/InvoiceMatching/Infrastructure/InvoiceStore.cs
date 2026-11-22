using InvoiceMatching.Domain;

namespace InvoiceMatching.Infrastructure;

public sealed class InvoiceStore
{
    private readonly Dictionary<string, InvoiceRecord> _items = new();
    private readonly object _gate = new();

 
    public bool TryAdd(InvoiceRecord record)
    {
        lock (_gate)
        {
            return _items.TryAdd(record.Id, record);
        }
    }

    public InvoiceRecord? Get(string id)
    {
        lock (_gate)
        {
            return _items.GetValueOrDefault(id);
        }
    }

    public InvoiceRecord Update(string id, Func<InvoiceRecord, InvoiceRecord> change)
    {
        lock (_gate)
        {
            var updated = change(_items[id]) with { UpdatedAt = DateTimeOffset.UtcNow };
            _items[id] = updated;
            return updated;
        }
    }

    public IReadOnlyList<InvoiceRecord> List()
    {
        lock (_gate)
        {
            return _items.Values.OrderBy(r => r.ReceivedAt).ToList();
        }
    }

    public int Count(Func<InvoiceRecord, bool> predicate)
    {
        lock (_gate)
        {
            return _items.Values.Count(predicate);
        }
    }
}
