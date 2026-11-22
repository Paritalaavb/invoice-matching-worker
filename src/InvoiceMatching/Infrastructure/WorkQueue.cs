using System.Runtime.CompilerServices;
using System.Threading.Channels;
using InvoiceMatching.Domain;

namespace InvoiceMatching.Infrastructure;

public sealed class WorkQueue
{
    private readonly Channel<WorkItem> _channel =
        Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });

    private int _depth;

    public int Depth => Volatile.Read(ref _depth);

    public void Enqueue(WorkItem item)
    {
        Interlocked.Increment(ref _depth);
        if (!_channel.Writer.TryWrite(item))
        {
            Interlocked.Decrement(ref _depth);
            throw new InvalidOperationException("The work queue is closed.");
        }
    }

    public async IAsyncEnumerable<WorkItem> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _depth);
            yield return item;
        }
    }
}
