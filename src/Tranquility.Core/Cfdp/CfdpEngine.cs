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
    private const int SegmentSize = 1024;

    private readonly long _transactionId;
    private readonly string _sourceName;
    private readonly string _destName;
    private readonly byte[] _file;
    private readonly bool _acknowledged;
    private readonly uint _checksum;
    private long _nextOffset;
    private bool _cancelled;

    public CfdpSender(long transactionId, string sourceName, string destName, byte[] fileContent, CfdpClass serviceClass)
    {
        _transactionId = transactionId;
        _sourceName = sourceName;
        _destName = destName;
        _file = fileContent;
        _acknowledged = serviceClass == CfdpClass.Acknowledged;
        _checksum = ModularChecksum.Compute(fileContent);
        State = CfdpTransactionState.Idle;
    }

    public CfdpTransactionState State { get; private set; }

    public IReadOnlyList<CfdpOutput> Start()
    {
        State = CfdpTransactionState.Sending;
        var outputs = new List<CfdpOutput>
        {
            new SendPdu(new MetadataPdu(_transactionId, _sourceName, _destName, _file.Length, _acknowledged)),
        };
        PumpSegments(outputs);
        return outputs;
    }

    public IReadOnlyList<CfdpOutput> Handle(CfdpEvent cfdpEvent)
    {
        var outputs = new List<CfdpOutput>();
        switch (cfdpEvent)
        {
            case CancelRequested:
                _cancelled = true;
                State = CfdpTransactionState.Cancelled;
                outputs.Add(new SendPdu(new EofPdu(_transactionId, ConditionCode.CancelRequestReceived, _checksum, _file.Length)));
                outputs.Add(new TransactionFinished(ConditionCode.CancelRequestReceived, false));
                break;

            case PduReceived { Pdu: AckPdu { AcknowledgedType: PduType.Eof } } when State == CfdpTransactionState.AwaitEofAck:
                State = CfdpTransactionState.AwaitFinished;
                break;

            case PduReceived { Pdu: NakPdu nak }:
                // Retransmit the requested segments (deferred NAK).
                foreach (var gap in nak.Gaps)
                {
                    for (long offset = gap.Start; offset < gap.End; offset += SegmentSize)
                    {
                        int len = (int)Math.Min(SegmentSize, gap.End - offset);
                        outputs.Add(new SendPdu(new FileDataPdu(_transactionId, offset,
                            _file.AsSpan((int)offset, len).ToArray())));
                    }
                }

                // Re-assert EOF so the receiver re-checks completeness.
                outputs.Add(new SendPdu(new EofPdu(_transactionId, ConditionCode.NoError, _checksum, _file.Length)));
                break;

            case PduReceived { Pdu: FinishedPdu finished }:
                State = finished.Delivery == FinishedDeliveryCode.Complete
                    ? CfdpTransactionState.Complete
                    : CfdpTransactionState.Failed;
                outputs.Add(new SendPdu(new AckPdu(_transactionId, PduType.Finished)));
                outputs.Add(new TransactionFinished(finished.Condition, State == CfdpTransactionState.Complete));
                break;

            case TimerExpired when State == CfdpTransactionState.AwaitEofAck && !_cancelled:
                // EOF-ACK timeout: re-send EOF (bounded retries handled by the host).
                outputs.Add(new SendPdu(new EofPdu(_transactionId, ConditionCode.NoError, _checksum, _file.Length)));
                break;
        }

        return outputs;
    }

    private void PumpSegments(List<CfdpOutput> outputs)
    {
        while (_nextOffset < _file.Length)
        {
            int len = (int)Math.Min(SegmentSize, _file.Length - _nextOffset);
            outputs.Add(new SendPdu(new FileDataPdu(_transactionId, _nextOffset,
                _file.AsSpan((int)_nextOffset, len).ToArray())));
            _nextOffset += len;
        }

        outputs.Add(new SendPdu(new EofPdu(_transactionId, ConditionCode.NoError, _checksum, _file.Length)));

        if (_acknowledged)
        {
            State = CfdpTransactionState.AwaitEofAck;
        }
        else
        {
            State = CfdpTransactionState.Complete;
            outputs.Add(new TransactionFinished(ConditionCode.NoError, true));
        }
    }
}

/// <summary>
/// Class 1/2 CFDP receiver automaton (TRQ-CFDP-BP1). Buffers out-of-order file
/// data in an <see cref="IntervalSet"/>, generates deferred NAKs on gaps at
/// EOF, verifies the modular checksum, and (class 2) sends Finished.
/// </summary>
public sealed class CfdpReceiver
{
    private readonly long _transactionId;
    private readonly IntervalSet _received = new();
    private readonly Dictionary<long, byte[]> _segments = new();
    private long _fileSize = -1;
    private uint _expectedChecksum;
    private bool _acknowledged;
    private bool _committed;

    public CfdpReceiver(long transactionId)
    {
        _transactionId = transactionId;
        State = CfdpTransactionState.Idle;
    }

    public CfdpTransactionState State { get; private set; }

    public string? SourceName { get; private set; }

    public string? DestName { get; private set; }

    public IReadOnlyList<CfdpOutput> Handle(CfdpEvent cfdpEvent)
    {
        var outputs = new List<CfdpOutput>();
        switch (cfdpEvent)
        {
            case PduReceived { Pdu: MetadataPdu metadata }:
                SourceName = metadata.SourceName;
                DestName = metadata.DestName;
                _fileSize = metadata.FileSize;
                _acknowledged = metadata.Acknowledged;
                State = CfdpTransactionState.Receiving;
                break;

            case PduReceived { Pdu: FileDataPdu fileData }:
                if (_segments.TryAdd(fileData.Offset, fileData.Data))
                {
                    _received.Add(fileData.Offset, fileData.Data.Length);
                    outputs.Add(new WriteFileSegment(fileData.Offset, fileData.Data));
                }

                break;

            case PduReceived { Pdu: EofPdu eof }:
                _expectedChecksum = eof.Checksum;
                _fileSize = eof.FileSize;
                if (eof.Condition == ConditionCode.CancelRequestReceived)
                {
                    State = CfdpTransactionState.Cancelled;
                    outputs.Add(new DiscardFile());
                    break;
                }

                if (_acknowledged)
                {
                    outputs.Add(new SendPdu(new AckPdu(_transactionId, PduType.Eof)));
                }

                CheckCompletion(outputs);
                break;
        }

        return outputs;
    }

    private void CheckCompletion(List<CfdpOutput> outputs)
    {
        if (_fileSize < 0)
        {
            return;
        }

        if (_received.IsComplete(_fileSize))
        {
            var checksum = ComputeChecksum();
            var condition = checksum == _expectedChecksum ? ConditionCode.NoError : ConditionCode.ChecksumFailure;
            if (condition == ConditionCode.NoError)
            {
                if (!_committed)
                {
                    _committed = true;
                    outputs.Add(new CommitFile());
                }

                State = CfdpTransactionState.Complete;
            }
            else
            {
                State = CfdpTransactionState.Failed;
                outputs.Add(new DiscardFile());
            }

            if (_acknowledged)
            {
                outputs.Add(new SendPdu(new FinishedPdu(_transactionId, condition,
                    condition == ConditionCode.NoError ? FinishedDeliveryCode.Complete : FinishedDeliveryCode.Incomplete)));
            }

            outputs.Add(new TransactionFinished(condition, condition == ConditionCode.NoError));
        }
        else if (_acknowledged)
        {
            // Deferred NAK: request the missing ranges.
            State = CfdpTransactionState.NakActive;
            outputs.Add(new SendPdu(new NakPdu(_transactionId, _received.Gaps(_fileSize))));
        }
    }

    private uint ComputeChecksum()
    {
        uint sum = 0;
        foreach (var (offset, data) in _segments.OrderBy(s => s.Key))
        {
            sum = ModularChecksum.Accumulate(sum, offset, data);
        }

        return sum;
    }
}
