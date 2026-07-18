using Tranquility.Application.Queries;
using Tranquility.Core.Alarms;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Processing;

public sealed record ParameterBatch(string Instance, string Processor, IReadOnlyList<ParameterValue> Values);

public sealed record LinkStateEvent(string Instance, LinkSnapshot Link);

public sealed record ProcessorStateEvent(string Instance, ProcessorSnapshot Processor);

public sealed record AlarmEvent(string Instance, AlarmTransition Transition);

public sealed record TransferEvent(string Instance, Cfdp.TransferRecord Transfer);

/// <summary>
/// Synchronous in-process fan-out for realtime updates (L2-RTS-002/003,
/// L2-PAR-004). Dispatch happens on the publisher's thread in subscription
/// order; subscribers must enqueue, never block.
/// </summary>
public sealed class SubscriptionHub
{
    private readonly Lock _gate = new();
    private readonly List<Action<ParameterBatch>> _parameters = [];
    private readonly List<Action<LinkStateEvent>> _links = [];
    private readonly List<Action<ProcessorStateEvent>> _processors = [];
    private readonly List<Action<AlarmEvent>> _alarms = [];
    private readonly List<Action<TransferEvent>> _transfers = [];

    public IDisposable SubscribeParameters(Action<ParameterBatch> handler) => Add(_parameters, handler);

    public IDisposable SubscribeLinks(Action<LinkStateEvent> handler) => Add(_links, handler);

    public IDisposable SubscribeProcessors(Action<ProcessorStateEvent> handler) => Add(_processors, handler);

    public IDisposable SubscribeAlarms(Action<AlarmEvent> handler) => Add(_alarms, handler);

    public IDisposable SubscribeTransfers(Action<TransferEvent> handler) => Add(_transfers, handler);

    public void PublishParameters(ParameterBatch batch) => Dispatch(_parameters, batch);

    public void PublishLink(LinkStateEvent e) => Dispatch(_links, e);

    public void PublishProcessor(ProcessorStateEvent e) => Dispatch(_processors, e);

    public void PublishAlarm(AlarmEvent e) => Dispatch(_alarms, e);

    public void PublishTransfer(TransferEvent e) => Dispatch(_transfers, e);

    private IDisposable Add<T>(List<Action<T>> list, Action<T> handler)
    {
        lock (_gate)
        {
            list.Add(handler);
        }

        return new Subscription<T>(this, list, handler);
    }

    private void Dispatch<T>(List<Action<T>> list, T item)
    {
        Action<T>[] snapshot;
        lock (_gate)
        {
            snapshot = [.. list];
        }

        foreach (var handler in snapshot)
        {
            handler(item);
        }
    }

    private sealed class Subscription<T>(SubscriptionHub hub, List<Action<T>> list, Action<T> handler) : IDisposable
    {
        public void Dispose()
        {
            lock (hub._gate)
            {
                list.Remove(handler);
            }
        }
    }
}
