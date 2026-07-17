using Tranquility.Application.Abstractions;
using Tranquility.Application.Processing;

namespace Tranquility.Application.Commands;

// Documented processor lifecycle operations (L2-LIF-002/003/004).

public sealed record CreateProcessorCommand(
    string Instance, string Name, string Type, long? StartUs, long? StopUs,
    bool Paused, bool Persistent, double Speed, string Actor) : ICommand<ProcessorSnapshot>;

public sealed record EditProcessorCommand(string Instance, string Processor, double? Speed, string Actor)
    : ICommand<ProcessorSnapshot>;

public sealed record DeleteProcessorCommand(string Instance, string Processor, string Actor) : ICommand<bool>;

public sealed record PauseProcessorCommand(string Instance, string Processor, string Actor) : ICommand<ProcessorSnapshot>;

public sealed record ResumeProcessorCommand(string Instance, string Processor, string Actor) : ICommand<ProcessorSnapshot>;

public sealed class CreateProcessorCommandHandler(
    InstanceRegistry registry, SubscriptionHub hub, IArchive archive, IAuditLog audit, TimeProvider time)
    : ICommandHandler<CreateProcessorCommand, ProcessorSnapshot>
{
    public async Task<ProcessorSnapshot> Handle(CreateProcessorCommand command, CancellationToken cancellationToken)
    {
        if (!string.Equals(command.Type, "replay", StringComparison.Ordinal))
        {
            throw new BadRequestServiceException($"Unsupported processor type '{command.Type}'; only 'replay' processors can be created");
        }

        var instance = registry.Get(command.Instance);
        instance.RequireMdb();

        var processor = new ReplayProcessor(instance, hub, archive, command.Name,
            command.StartUs, command.StopUs, command.Persistent, command.Paused, command.Speed);
        instance.AddProcessor(processor);
        processor.Run();
        hub.PublishProcessor(new ProcessorStateEvent(instance.Name, processor.Snapshot()));
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "processor-create",
            $"{command.Instance}/processors/{command.Name}", "success", null), cancellationToken);
        return processor.Snapshot();
    }
}

public sealed class EditProcessorCommandHandler(InstanceRegistry registry, SubscriptionHub hub, IAuditLog audit, TimeProvider time)
    : ICommandHandler<EditProcessorCommand, ProcessorSnapshot>
{
    public async Task<ProcessorSnapshot> Handle(EditProcessorCommand command, CancellationToken cancellationToken)
    {
        var replay = RequireReplay(registry, command.Instance, command.Processor, "edited");
        if (command.Speed is { } speed)
        {
            replay.Speed = speed;
        }

        hub.PublishProcessor(new ProcessorStateEvent(command.Instance, replay.Snapshot()));
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "processor-edit",
            $"{command.Instance}/processors/{command.Processor}", "success", null), cancellationToken);
        return replay.Snapshot();
    }

    internal static ReplayProcessor RequireReplay(InstanceRegistry registry, string instance, string name, string operation)
    {
        var processor = registry.Get(instance).FindProcessor(name);
        return processor as ReplayProcessor
            ?? throw new ConflictServiceException($"The '{name}' processor cannot be {operation}");
    }
}

public sealed class DeleteProcessorCommandHandler(InstanceRegistry registry, SubscriptionHub hub, IAuditLog audit, TimeProvider time)
    : ICommandHandler<DeleteProcessorCommand, bool>
{
    public async Task<bool> Handle(DeleteProcessorCommand command, CancellationToken cancellationToken)
    {
        var replay = EditProcessorCommandHandler.RequireReplay(registry, command.Instance, command.Processor, "deleted");
        replay.Stop();
        registry.Get(command.Instance).RemoveProcessor(command.Processor);
        hub.PublishProcessor(new ProcessorStateEvent(command.Instance,
            replay.Snapshot() with { State = "DELETED" }));
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "processor-delete",
            $"{command.Instance}/processors/{command.Processor}", "success", null), cancellationToken);
        return true;
    }
}

public sealed class PauseProcessorCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<PauseProcessorCommand, ProcessorSnapshot>
{
    public async Task<ProcessorSnapshot> Handle(PauseProcessorCommand command, CancellationToken cancellationToken)
    {
        var replay = EditProcessorCommandHandler.RequireReplay(registry, command.Instance, command.Processor, "paused");
        replay.Pause();
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "processor-pause",
            $"{command.Instance}/processors/{command.Processor}", "success", null), cancellationToken);
        return replay.Snapshot();
    }
}

public sealed class ResumeProcessorCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<ResumeProcessorCommand, ProcessorSnapshot>
{
    public async Task<ProcessorSnapshot> Handle(ResumeProcessorCommand command, CancellationToken cancellationToken)
    {
        var replay = EditProcessorCommandHandler.RequireReplay(registry, command.Instance, command.Processor, "resumed");
        replay.Resume();
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "processor-resume",
            $"{command.Instance}/processors/{command.Processor}", "success", null), cancellationToken);
        return replay.Snapshot();
    }
}
