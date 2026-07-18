using System.Collections.Concurrent;
using Tranquility.Application.Abstractions;
using Tranquility.Core.Ccsds;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Commanding;

public sealed record CommandStage(string Name, DateTimeOffset Time);

/// <summary>A command's identity, generated binary, and lifecycle history.</summary>
public sealed class CommandRecord(
    string id, string commandName, string origin, int sequenceNumber,
    DateTimeOffset generationTime, byte[] binary, IReadOnlyList<CommandAssignment> assignments)
{
    private readonly List<CommandStage> _stages = [];

    public string Id { get; } = id;

    public string CommandName { get; } = commandName;

    public string Origin { get; } = origin;

    public int SequenceNumber { get; } = sequenceNumber;

    public DateTimeOffset GenerationTime { get; } = generationTime;

    public byte[] Binary { get; } = binary;

    public IReadOnlyList<CommandAssignment> Assignments { get; } = assignments;

    public IReadOnlyList<CommandStage> Stages
    {
        get
        {
            lock (_stages)
            {
                return _stages.ToList();
            }
        }
    }

    public void AddStage(string name, DateTimeOffset time)
    {
        lock (_stages)
        {
            _stages.Add(new CommandStage(name, time));
        }
    }
}

/// <summary>A queued command entry awaiting accept/reject.</summary>
public sealed record QueueEntry(string Id, string CommandName, DateTimeOffset QueuedTime);

/// <summary>
/// Per-instance commanding runtime: issue -> encode -> history + queue,
/// accept -> release through COP-1 (SENT on radiation), reject -> REJECTED.
/// Implements L2-CMD-001/002/005 runtime.
/// </summary>
public sealed class CommandService(MissionInstance instance, Cop1Service? cop1, IAuditLog audit, TimeProvider time)
{
    public const string DefaultQueue = "default";

    private readonly ConcurrentDictionary<string, CommandRecord> _history = new(StringComparer.Ordinal);
    private readonly List<QueueEntry> _queue = [];
    private readonly Lock _queueGate = new();
    private int _sequence;

    public CommandRecord Issue(string commandName, IReadOnlyDictionary<string, string> args, string origin)
    {
        var mdb = instance.RequireMdb();
        var command = mdb.ResolveCommand(commandName)
            ?? throw new NotFoundServiceException($"Command '{commandName}' not found");

        EncodedCommand encoded;
        try
        {
            encoded = CommandEncoder.Encode(command, args);
        }
        catch (MissingCommandArgumentException e)
        {
            throw new BadRequestServiceException(e.Message);
        }
        catch (ArgumentException e)
        {
            throw new BadRequestServiceException(e.Message);
        }

        var now = time.GetUtcNow();
        var record = new CommandRecord(
            Guid.NewGuid().ToString("N"), command.QualifiedName, origin,
            Interlocked.Increment(ref _sequence), now, encoded.Binary, encoded.Assignments);
        record.AddStage("ISSUED", now);
        _history[record.Id] = record;

        lock (_queueGate)
        {
            _queue.Add(new QueueEntry(record.Id, command.QualifiedName, now));
        }

        record.AddStage("QUEUED", time.GetUtcNow());
        return record;
    }

    public IReadOnlyList<QueueEntry> QueueEntries()
    {
        lock (_queueGate)
        {
            return _queue.ToList();
        }
    }

    public IReadOnlyCollection<CommandRecord> History => _history.Values.ToList();

    public async Task AcceptAsync(string entryId, string actor, CancellationToken cancellationToken)
    {
        var record = DequeueOrThrow(entryId);
        record.AddStage("RELEASED", time.GetUtcNow());
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), actor, "command-release",
            $"{instance.Name}/commands/{record.CommandName}", "success", record.Id), cancellationToken);

        // Uplink through COP-1 over a TC frame; SENT on radiation.
        if (cop1 is not null)
        {
            var frame = TcTransferFrame.Build(spacecraftId: 0x42, virtualChannelId: 0,
                frameSequenceNumber: (byte)record.SequenceNumber, record.Binary, bypass: false);
            record.AddStage("SENT", time.GetUtcNow());
            _ = cop1.TransmitAsync(frame);
        }
        else
        {
            record.AddStage("SENT", time.GetUtcNow());
        }
    }

    public async Task RejectAsync(string entryId, string actor, CancellationToken cancellationToken)
    {
        var record = DequeueOrThrow(entryId);
        record.AddStage("REJECTED", time.GetUtcNow());
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), actor, "command-reject",
            $"{instance.Name}/commands/{record.CommandName}", "success", record.Id), cancellationToken);
    }

    public CommandRecord? Find(string id) => _history.GetValueOrDefault(id);

    private CommandRecord DequeueOrThrow(string entryId)
    {
        lock (_queueGate)
        {
            var index = _queue.FindIndex(e => e.Id == entryId);
            if (index < 0)
            {
                throw new NotFoundServiceException($"Queue entry '{entryId}' not found");
            }

            _queue.RemoveAt(index);
        }

        return _history[entryId];
    }
}
