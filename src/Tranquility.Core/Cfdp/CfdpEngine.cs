namespace Tranquility.Core.Cfdp;

// ---- Events into the engines (time and I/O arrive as data) ----

public abstract record CfdpEvent;

public sealed record PduReceived(Pdu Pdu) : CfdpEvent;

public sealed record FileSegmentRead(long Offset, byte[] Data, bool IsFinal) : CfdpEvent;

public sealed record SuspendRequested : CfdpEvent;

public sealed record ResumeRequested : CfdpEvent;

public sealed record CancelRequested : CfdpEvent;

public sealed record TimerExpired(string TimerId) : CfdpEvent;

// ---- Outputs from the engines (I/O and timers as outputs) ----

public abstract record CfdpOutput;

public sealed record SendPdu(Pdu Pdu) : CfdpOutput;

public sealed record ReadFileSegment(long Offset, int Length) : CfdpOutput;

public sealed record WriteFileSegment(long Offset, byte[] Data) : CfdpOutput;

public sealed record CommitFile : CfdpOutput;

public sealed record DiscardFile : CfdpOutput;

public sealed record TransactionFinished(ConditionCode Condition, bool Success) : CfdpOutput;

public enum CfdpTransactionState
{
    Idle,
    Sending,
    AwaitEofAck,
    AwaitFinished,
    Receiving,
    NakActive,
    Complete,
    Cancelled,
    Failed,
}

/// <summary>
/// Class 1/2 CFDP sender automaton (TRQ-CFDP-BP1). Pure: file I/O and timers
/// are events/outputs. Drives Metadata -> File Data -> EOF; for class 2, awaits
/// EOF-ACK, NAK-driven retransmission, and Finished.
/// </summary>
public sealed class CfdpSender
{
    public CfdpSender(long transactionId, string sourceName, string destName, byte[] fileContent, CfdpClass serviceClass)
    {
        throw new NotImplementedException();
    }

    public CfdpTransactionState State => throw new NotImplementedException();

    public IReadOnlyList<CfdpOutput> Start() => throw new NotImplementedException();

    public IReadOnlyList<CfdpOutput> Handle(CfdpEvent cfdpEvent) => throw new NotImplementedException();
}

/// <summary>
/// Class 1/2 CFDP receiver automaton (TRQ-CFDP-BP1). Buffers out-of-order file
/// data in an <see cref="IntervalSet"/>, generates deferred NAKs on gaps at
/// EOF, verifies the modular checksum, and (class 2) sends Finished.
/// </summary>
public sealed class CfdpReceiver
{
    public CfdpReceiver(long transactionId)
    {
        throw new NotImplementedException();
    }

    public CfdpTransactionState State => throw new NotImplementedException();

    public string? SourceName => throw new NotImplementedException();

    public string? DestName => throw new NotImplementedException();

    public IReadOnlyList<CfdpOutput> Handle(CfdpEvent cfdpEvent) => throw new NotImplementedException();
}
