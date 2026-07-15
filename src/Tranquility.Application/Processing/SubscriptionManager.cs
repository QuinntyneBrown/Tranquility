using System.Collections.Concurrent;
using System.Threading.Channels;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Processing;

/// <summary>
/// Fan-out of parameter updates to real-time subscribers.
/// Implements: L2-RTS-002 (parameters subscription topic), L2-RTS-004
/// (bounded channels make drops observable via sequence gaps at the transport).
/// </summary>
public sealed class SubscriptionManager
{
    private readonly ConcurrentDictionary<int, ParameterSubscription> _subscriptions = new();
    private int _nextId;

    /// <summary>
    /// Creates a subscription for the given qualified names, or all parameters when null.
    /// </summary>
    public ParameterSubscription Subscribe(IReadOnlyCollection<string>? qualifiedNames = null, int capacity = 256)
    {
        int id = Interlocked.Increment(ref _nextId);
        var subscription = new ParameterSubscription(id, this, qualifiedNames, capacity);
        _subscriptions[id] = subscription;
        return subscription;
    }

    public void Publish(IReadOnlyList<ParameterValue> values)
    {
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Offer(values);
        }
    }

    public int ActiveCount => _subscriptions.Count;

    internal void Remove(int id) => _subscriptions.TryRemove(id, out _);
}

/// <summary>A single client subscription to parameter updates.</summary>
public sealed class ParameterSubscription : IDisposable
{
    private readonly SubscriptionManager _owner;
    private readonly HashSet<string>? _names;
    private readonly Channel<IReadOnlyList<ParameterValue>> _channel;

    /// <summary>Number of update batches dropped because the subscriber was too slow.</summary>
    private long _dropped;

    internal ParameterSubscription(int id, SubscriptionManager owner, IReadOnlyCollection<string>? names, int capacity)
    {
        Id = id;
        _owner = owner;
        _names = names is null ? null : new HashSet<string>(names, StringComparer.Ordinal);
        _channel = Channel.CreateBounded<IReadOnlyList<ParameterValue>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });
    }

    public int Id { get; }

    public long DroppedBatches => Interlocked.Read(ref _dropped);

    public ChannelReader<IReadOnlyList<ParameterValue>> Reader => _channel.Reader;

    internal void Offer(IReadOnlyList<ParameterValue> values)
    {
        IReadOnlyList<ParameterValue> filtered = _names is null
            ? values
            : values.Where(v => _names.Contains(v.Parameter.QualifiedName)).ToArray();

        if (filtered.Count == 0)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(filtered))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    public void Dispose()
    {
        _owner.Remove(Id);
        _channel.Writer.TryComplete();
    }
}
