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
    public FopState State { get; private set; } = FopState.Active;

    /// <summary>Next frame sequence number V(S).</summary>
    public byte Vs { get; private set; }

    /// <summary>Expected acknowledgement frame sequence number NN(R).</summary>
    public byte NnR { get; private set; }

    public int SentQueueDepth => throw new NotImplementedException();

    public int TransmissionCount => throw new NotImplementedException();

    public Cop1Profile Profile { get; } = profile;

    /// <summary>Processes one event; returns the resulting protocol actions.</summary>
    public IReadOnlyList<FopOutput> Handle(FopEvent fopEvent)
    {
        throw new NotImplementedException();
    }
}
