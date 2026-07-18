namespace Tranquility.Core.Cop1;

/// <summary>FOP-1 states per CCSDS 232.1-B (S1..S6).</summary>
public enum FopState
{
    Active = 1,
    RetransmitWithoutWait = 2,
    RetransmitWithWait = 3,
    InitializingWithoutBc = 4,
    InitializingWithBc = 5,
    Initial = 6,
}

/// <summary>
/// The declared baseline COP-1 mission profile (resolves TBD-006): sliding
/// window K=10, transmission limit 3, T1 timeout 5 s, alert on limit, and
/// unchecked AD initiation at link start.
/// </summary>
public sealed record Cop1Profile(
    int SlidingWindowK = 10,
    int TransmissionLimit = 3,
    long T1TimeoutMs = 5000);

/// <summary>Decoded Communications Link Control Word fields used by FOP-1.</summary>
public readonly record struct Clcw(byte ReportValue, bool RetransmitFlag, bool WaitFlag, bool LockoutFlag);

// ---- Events into the engine (time arrives as data: L2-QLT-002) ----

public abstract record FopEvent;

public sealed record TransmitAdRequest(long RequestId, byte[] FrameData) : FopEvent;

public sealed record ClcwReceived(Clcw Clcw) : FopEvent;

public sealed record TimerExpired : FopEvent;

// ---- Outputs from the engine (timers are outputs, not side effects) ----

public abstract record FopOutput;

public sealed record RadiateFrame(long RequestId, byte FrameSequenceNumber, byte[] FrameData) : FopOutput;

public sealed record PositiveConfirm(long RequestId) : FopOutput;

public sealed record NegativeConfirm(long RequestId, string Reason) : FopOutput;

public sealed record StartTimer(long DurationMs) : FopOutput;

public sealed record CancelTimer : FopOutput;

public sealed record Alert(string Reason) : FopOutput;

public sealed record RejectBusy(long RequestId) : FopOutput;

/// <summary>
/// Pure FOP-1 mailbox automaton (CCSDS 232.1-B): AD-frame transmission with
/// sliding window, CLCW-driven acknowledgement/retransmission, T1 timer via
/// events/outputs, mod-256 sequence arithmetic. Implements L2-CMD-003.
/// </summary>
public sealed class FopEngine(Cop1Profile profile)
{
    private readonly List<SentFrame> _sentQueue = [];
    private int _transmissionCount;

    public FopState State { get; private set; } = FopState.Active;

    /// <summary>Next frame sequence number V(S).</summary>
    public byte Vs { get; private set; }

    /// <summary>Expected acknowledgement frame sequence number NN(R).</summary>
    public byte NnR { get; private set; }

    public int SentQueueDepth => _sentQueue.Count;

    public int TransmissionCount => _transmissionCount;

    public Cop1Profile Profile { get; } = profile;

    /// <summary>Processes one event; returns the resulting protocol actions.</summary>
    public IReadOnlyList<FopOutput> Handle(FopEvent fopEvent)
    {
        var outputs = new List<FopOutput>();
        switch (fopEvent)
        {
            case TransmitAdRequest request:
                HandleAdRequest(request, outputs);
                break;
            case ClcwReceived clcw:
                HandleClcw(clcw.Clcw, outputs);
                break;
            case TimerExpired:
                HandleTimerExpiry(outputs);
                break;
        }

        return outputs;
    }

    private void HandleAdRequest(TransmitAdRequest request, List<FopOutput> outputs)
    {
        if (State != FopState.Active)
        {
            outputs.Add(new RejectBusy(request.RequestId));
            return;
        }

        // Sliding window: outstanding frames = V(S) - NN(R) (mod 256).
        int outstanding = (byte)(Vs - NnR);
        if (outstanding >= Profile.SlidingWindowK)
        {
            outputs.Add(new RejectBusy(request.RequestId));
            return;
        }

        var frame = new SentFrame(request.RequestId, Vs, request.FrameData);
        _sentQueue.Add(frame);
        outputs.Add(new RadiateFrame(request.RequestId, Vs, request.FrameData));
        Vs = (byte)(Vs + 1);

        if (_sentQueue.Count == 1)
        {
            _transmissionCount = 1;
            outputs.Add(new StartTimer(Profile.T1TimeoutMs));
        }
    }

    private void HandleClcw(Clcw clcw, List<FopOutput> outputs)
    {
        if (clcw.LockoutFlag)
        {
            Purge(outputs, "lockout detected");
            State = FopState.Initial;
            outputs.Add(new Alert("lockout"));
            return;
        }

        // Acknowledge frames with sequence number < N(R) (mod 256).
        int acknowledged = (byte)(clcw.ReportValue - NnR);
        if (acknowledged > 0 && acknowledged <= _sentQueue.Count)
        {
            for (int i = 0; i < acknowledged; i++)
            {
                outputs.Add(new PositiveConfirm(_sentQueue[i].RequestId));
            }

            _sentQueue.RemoveRange(0, acknowledged);
            NnR = clcw.ReportValue;
            _transmissionCount = 1;

            if (_sentQueue.Count == 0)
            {
                outputs.Add(new CancelTimer());
                State = FopState.Active;
                return;
            }

            outputs.Add(new StartTimer(Profile.T1TimeoutMs));
        }

        if (clcw.RetransmitFlag)
        {
            // Enter a retransmit state; radiate immediately when no wait is
            // required. The state persists until a clean CLCW acknowledges.
            State = clcw.WaitFlag ? FopState.RetransmitWithWait : FopState.RetransmitWithoutWait;
            if (!clcw.WaitFlag)
            {
                Retransmit(outputs);
            }
        }
        else if (acknowledged > 0)
        {
            // Clean acknowledgement with frames still outstanding: back to Active.
            State = FopState.Active;
        }
    }

    private void HandleTimerExpiry(List<FopOutput> outputs)
    {
        if (_sentQueue.Count == 0)
        {
            return;
        }

        _transmissionCount++;
        if (_transmissionCount <= Profile.TransmissionLimit)
        {
            Retransmit(outputs);
            outputs.Add(new StartTimer(Profile.T1TimeoutMs));
            return;
        }

        // Limit exceeded: alert, purge, negative-confirm all pending frames.
        outputs.Add(new Alert("T1 timeout: transmission limit exceeded"));
        Purge(outputs, "T1 timeout limit exceeded");
        State = FopState.Initial;
    }

    private void Retransmit(List<FopOutput> outputs)
    {
        foreach (var frame in _sentQueue)
        {
            outputs.Add(new RadiateFrame(frame.RequestId, frame.SequenceNumber, frame.FrameData));
        }
    }

    private void Purge(List<FopOutput> outputs, string reason)
    {
        foreach (var frame in _sentQueue)
        {
            outputs.Add(new NegativeConfirm(frame.RequestId, reason));
        }

        _sentQueue.Clear();
        _transmissionCount = 0;
        outputs.Add(new CancelTimer());
    }

    private readonly record struct SentFrame(long RequestId, byte SequenceNumber, byte[] FrameData);
}
