using System.Collections.Concurrent;
using System.Threading.Channels;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Processing;
using Tranquility.Core.Cfdp;

namespace Tranquility.Application.Cfdp;

public enum TransferState
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>Observable transfer record (documented TransferInfo, L2-FDP-001/003).</summary>
public sealed class TransferRecord(string id, string direction, string bucket, string objectName, string remotePath, long totalSize, bool reliable, DateTimeOffset startTime)
{
    public string Id { get; } = id;

    public string Direction { get; } = direction;

    public string Bucket { get; } = bucket;

    public string ObjectName { get; } = objectName;

    public string RemotePath { get; } = remotePath;

    public long TotalSize { get; } = totalSize;

    public bool Reliable { get; } = reliable;

    public DateTimeOffset StartTime { get; } = startTime;

    public volatile TransferState State = TransferState.Queued;

    public long SizeTransferred;

    public string? FailureReason;
}

/// <summary>
/// Per-instance/serviceName CFDP runtime. Each transfer runs a
/// <see cref="CfdpSender"/> and <see cref="CfdpReceiver"/> in-process (loopback
/// entity roles) over a mailbox, honouring pause/resume/cancel and publishing
/// status changes to subscribers (L2-FDP-001/002/003).
/// </summary>
public sealed class TransferService(
    string instance, IFilestore filestore, SubscriptionHub hub, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, TransferRecord> _transfers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TransferRunner> _runners = new(StringComparer.Ordinal);
    private long _nextTransactionId;

    public IReadOnlyCollection<TransferRecord> Transfers => _transfers.Values.ToList();

    public TransferRecord? Find(string id) => _transfers.GetValueOrDefault(id);

    public TransferRecord Create(
        string id, string direction, string bucket, string objectName, string remotePath,
        byte[] content, bool reliable, bool paused)
    {
        var record = new TransferRecord(id, direction, bucket, objectName, remotePath,
            content.Length, reliable, time.GetUtcNow());
        _transfers[id] = record;

        var runner = new TransferRunner(
            Interlocked.Increment(ref _nextTransactionId), record, content, filestore, this, paused);
        _runners[id] = runner;
        runner.Start();
        return record;
    }

    public void Pause(string id) => Runner(id).Pause();

    public void Resume(string id) => Runner(id).Resume();

    public void Cancel(string id) => Runner(id).Cancel();

    internal void Publish(TransferRecord record) =>
        hub.PublishTransfer(new TransferEvent(instance, record));

    private TransferRunner Runner(string id) =>
        _runners.TryGetValue(id, out var runner)
            ? runner
            : throw new NotFoundServiceException($"Transfer '{id}' not found");
}

/// <summary>Drives one transfer's sender+receiver over an in-process channel.</summary>
internal sealed class TransferRunner
{
    private readonly TransferRecord _record;
    private readonly byte[] _content;
    private readonly IFilestore _filestore;
    private readonly TransferService _service;
    private readonly CfdpSender _sender;
    private readonly CfdpReceiver _receiver;
    private readonly Channel<CfdpDirected> _mailbox = Channel.CreateUnbounded<CfdpDirected>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<long, byte[]> _received = new();
    private readonly SemaphoreSlim _pauseGate = new(1, 1);
    private long _fileSize;
    private volatile bool _cancelled;

    public TransferRunner(long transactionId, TransferRecord record, byte[] content,
        IFilestore filestore, TransferService service, bool paused)
    {
        _record = record;
        _content = content;
        _filestore = filestore;
        _service = service;
        _sender = new CfdpSender(transactionId, $"{record.Bucket}/{record.ObjectName}", record.RemotePath, content,
            record.Reliable ? CfdpClass.Acknowledged : CfdpClass.Unacknowledged);
        _receiver = new CfdpReceiver(transactionId);
        if (paused)
        {
            _pauseGate.Wait();
            _record.State = TransferState.Paused;
        }
    }

    public void Start()
    {
        _ = RunAsync();
    }

    public void Pause()
    {
        if (_record.State is TransferState.Completed or TransferState.Failed or TransferState.Cancelled)
        {
            return;
        }

        if (_pauseGate.CurrentCount > 0)
        {
            _pauseGate.Wait();
        }

        _record.State = TransferState.Paused;
        _service.Publish(_record);
    }

    public void Resume()
    {
        if (_record.State == TransferState.Paused)
        {
            _record.State = TransferState.Running;
            _pauseGate.Release();
            _service.Publish(_record);
        }
    }

    public void Cancel()
    {
        _cancelled = true;
        if (_record.State == TransferState.Paused)
        {
            _pauseGate.Release();
        }

        _mailbox.Writer.TryWrite(new CfdpDirected(true, new CancelRequested()));
    }

    private async Task RunAsync()
    {
        // Let the create response observe the initial (QUEUED/PAUSED) state
        // before the in-process transfer advances.
        await Task.Yield();
        try
        {
            _record.State = _record.State == TransferState.Paused ? TransferState.Paused : TransferState.Running;
            _service.Publish(_record);

            await WaitIfPausedAsync();
            Pump(_sender.Start(), fromReceiver: false);

            await foreach (var directed in _mailbox.Reader.ReadAllAsync())
            {
                await WaitIfPausedAsync();
                var outputs = directed.ToReceiver
                    ? _receiver.Handle(directed.Event)
                    : _sender.Handle(directed.Event);
                Pump(outputs, directed.ToReceiver);

                if (IsTerminal())
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            _record.State = TransferState.Failed;
            _record.FailureReason = e.Message;
            _service.Publish(_record);
        }
    }

    private async Task WaitIfPausedAsync()
    {
        await _pauseGate.WaitAsync();
        _pauseGate.Release();
    }

    private void Pump(IReadOnlyList<CfdpOutput> outputs, bool fromReceiver)
    {
        foreach (var output in outputs)
        {
            switch (output)
            {
                case SendPdu send:
                    _mailbox.Writer.TryWrite(new CfdpDirected(!fromReceiver, new PduReceived(send.Pdu)));
                    break;
                case WriteFileSegment write:
                    _received[write.Offset] = write.Data;
                    _fileSize = Math.Max(_fileSize, write.Offset + write.Data.Length);
                    Interlocked.Exchange(ref _record.SizeTransferred, _received.Values.Sum(v => (long)v.Length));
                    break;
                case CommitFile:
                    CommitReceived();
                    break;
                case DiscardFile:
                    _received.Clear();
                    break;
                case TransactionFinished finished when fromReceiver:
                    Finalize(finished);
                    break;
            }
        }
    }

    private void CommitReceived()
    {
        var file = new byte[_fileSize];
        foreach (var (offset, data) in _received)
        {
            data.CopyTo(file, offset);
        }

        // Downlinked file lands in the incoming bucket at the remote path.
        _filestore.Commit("incoming", _record.RemotePath, file);
    }

    private void Finalize(TransactionFinished finished)
    {
        _record.State = _cancelled
            ? TransferState.Cancelled
            : finished.Success ? TransferState.Completed : TransferState.Failed;
        if (!finished.Success && !_cancelled)
        {
            _record.FailureReason = finished.Condition.ToString();
        }

        Interlocked.Exchange(ref _record.SizeTransferred, _record.State == TransferState.Completed
            ? _record.TotalSize
            : _record.SizeTransferred);
        _service.Publish(_record);
    }

    private bool IsTerminal()
    {
        if (_cancelled)
        {
            _record.State = TransferState.Cancelled;
            _service.Publish(_record);
            return true;
        }

        return _record.State is TransferState.Completed or TransferState.Failed or TransferState.Cancelled;
    }

    private readonly record struct CfdpDirected(bool ToReceiver, CfdpEvent Event);
}
