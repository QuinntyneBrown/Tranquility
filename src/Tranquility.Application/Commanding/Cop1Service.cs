using System.Threading.Channels;
using Tranquility.Application.Abstractions;
using Tranquility.Core.Cltu;
using Tranquility.Core.Cop1;

namespace Tranquility.Application.Commanding;

public sealed record Cop1Status(string State, int Vs, int NnR, int SentQueueDepth);

/// <summary>
/// Hosts a <see cref="FopEngine"/> for one TC uplink link: a single-consumer
/// mailbox serializes all events (determinism), CLTU-encodes radiated frames,
/// feeds CLCW reports back in, and runs the T1 timer as a hosted timer.
/// Implements the runtime side of L2-CMD-003.
/// </summary>
public sealed class Cop1Service : IAsyncDisposable
{
    private readonly IUplinkLink _link;
    private readonly FopEngine _engine;
    private readonly Channel<FopEvent> _mailbox = Channel.CreateUnbounded<FopEvent>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _consumer;
    private readonly Lock _stateGate = new();
    private readonly Dictionary<long, TaskCompletionSource<bool>> _pending = new();
    private CancellationTokenSource? _timerCts;
    private long _nextRequestId;

    public Cop1Service(IUplinkLink link, Cop1Profile profile)
    {
        _link = link;
        _engine = new FopEngine(profile);
        _link.ClcwReceived += clcw => _mailbox.Writer.TryWrite(new ClcwReceived(clcw));
        _consumer = ConsumeAsync();
    }

    public Cop1Status Status()
    {
        lock (_stateGate)
        {
            return new Cop1Status(
                _engine.State == FopState.Active ? "ACTIVE" : _engine.State.ToString(),
                _engine.Vs, _engine.NnR, _engine.SentQueueDepth);
        }
    }

    /// <summary>Queues an AD frame for transfer; the task completes on confirm.</summary>
    public Task<bool> TransmitAsync(byte[] tcFrame)
    {
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_stateGate)
        {
            _pending[requestId] = completion;
        }

        _mailbox.Writer.TryWrite(new TransmitAdRequest(requestId, tcFrame));
        return completion.Task;
    }

    private async Task ConsumeAsync()
    {
        await foreach (var fopEvent in _mailbox.Reader.ReadAllAsync())
        {
            IReadOnlyList<FopOutput> outputs;
            lock (_stateGate)
            {
                outputs = _engine.Handle(fopEvent);
            }

            foreach (var output in outputs)
            {
                Apply(output);
            }
        }
    }

    private void Apply(FopOutput output)
    {
        switch (output)
        {
            case RadiateFrame radiate:
                _link.Radiate(CltuEncoder.Encode(radiate.FrameData, new CltuProfile()));
                break;
            case StartTimer timer:
                RestartTimer(timer.DurationMs);
                break;
            case CancelTimer:
                StopTimer();
                break;
            case PositiveConfirm confirm:
                Complete(confirm.RequestId, true);
                break;
            case NegativeConfirm confirm:
                Complete(confirm.RequestId, false);
                break;
        }
    }

    private void Complete(long requestId, bool result)
    {
        TaskCompletionSource<bool>? completion;
        lock (_stateGate)
        {
            _pending.Remove(requestId, out completion);
        }

        completion?.TrySetResult(result);
    }

    private void RestartTimer(long durationMs)
    {
        StopTimer();
        var cts = new CancellationTokenSource();
        _timerCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(durationMs), cts.Token);
                _mailbox.Writer.TryWrite(new TimerExpired());
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void StopTimer()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopTimer();
        _mailbox.Writer.TryComplete();
        await _consumer;
    }
}
