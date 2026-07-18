using Tranquility.Core.Cfdp;

namespace Tranquility.AcceptanceTests.FileTransfer;

/// <summary>
/// Glues a <see cref="CfdpSender"/> and <see cref="CfdpReceiver"/> over an
/// in-memory channel that drops every Nth file-data PDU, exercising class-2
/// NAK retransmission. Each engine is the other's oracle.
/// </summary>
public sealed class CfdpLoopbackHarness
{
    private readonly byte[] _content;
    private readonly int _dropEveryNth;
    private readonly Queue<Pdu> _toReceiver = new();
    private readonly Queue<Pdu> _toSender = new();
    private readonly Dictionary<long, byte[]> _received = new();
    private int _fileDataCount;
    private long _fileSize;
    private bool _committed;

    public CfdpLoopbackHarness(byte[] content, CfdpClass serviceClass, int dropEveryNth)
    {
        _content = content;
        _dropEveryNth = dropEveryNth;
        Sender = new CfdpSender(1, "buckets/out/file.bin", "incoming/file.bin", content, serviceClass);
        Receiver = new CfdpReceiver(1);
    }

    public CfdpSender Sender { get; }

    public CfdpReceiver Receiver { get; }

    public byte[]? DeliveredFile { get; private set; }

    public void RunToCompletion()
    {
        Pump(Sender.Start(), toReceiver: true);

        for (var step = 0; step < 100_000; step++)
        {
            if (_toReceiver.Count > 0)
            {
                Pump(Receiver.Handle(new PduReceived(_toReceiver.Dequeue())), toReceiver: false);
            }
            else if (_toSender.Count > 0)
            {
                Pump(Sender.Handle(new PduReceived(_toSender.Dequeue())), toReceiver: true);
            }
            else if (Sender.State is CfdpTransactionState.AwaitEofAck or CfdpTransactionState.AwaitFinished)
            {
                // No traffic in flight but sender still waiting: nudge its timer.
                Pump(Sender.Handle(new TimerExpired("eof")), toReceiver: true);
            }
            else
            {
                break;
            }
        }
    }

    private void Pump(IReadOnlyList<CfdpOutput> outputs, bool toReceiver)
    {
        foreach (var output in outputs)
        {
            switch (output)
            {
                case SendPdu send:
                    Deliver(send.Pdu, toReceiver);
                    break;
                case ReadFileSegment read:
                {
                    int take = (int)Math.Min(read.Length, _content.Length - read.Offset);
                    var data = _content.AsSpan((int)read.Offset, Math.Max(0, take)).ToArray();
                    bool isFinal = read.Offset + take >= _content.Length;
                    Pump(Sender.Handle(new FileSegmentRead(read.Offset, data, isFinal)), toReceiver: true);
                    break;
                }

                case WriteFileSegment write:
                    _received[write.Offset] = write.Data;
                    _fileSize = Math.Max(_fileSize, write.Offset + write.Data.Length);
                    break;
                case CommitFile:
                    Commit();
                    break;
                case DiscardFile:
                    _received.Clear();
                    break;
            }
        }
    }

    private void Deliver(Pdu pdu, bool toReceiver)
    {
        if (toReceiver && pdu is FileDataPdu)
        {
            _fileDataCount++;
            if (_dropEveryNth > 0 && _fileDataCount % _dropEveryNth == 0)
            {
                return; // dropped in flight
            }
        }

        (toReceiver ? _toReceiver : _toSender).Enqueue(pdu);
    }

    private void Commit()
    {
        if (_committed)
        {
            return;
        }

        _committed = true;
        var file = new byte[_fileSize];
        foreach (var (offset, data) in _received)
        {
            data.CopyTo(file, offset);
        }

        DeliveredFile = file;
    }
}
