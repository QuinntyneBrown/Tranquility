using Tranquility.Application.Abstractions;

namespace Tranquility.Application.Commands;

public sealed record StartInstanceCommand(string Instance, string Actor) : ICommand<InstanceSnapshot>;

public sealed record StopInstanceCommand(string Instance, string Actor) : ICommand<InstanceSnapshot>;

public sealed record RestartInstanceCommand(string Instance, string Actor) : ICommand<InstanceSnapshot>;

public sealed class StartInstanceCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<StartInstanceCommand, InstanceSnapshot>
{
    public async Task<InstanceSnapshot> Handle(StartInstanceCommand command, CancellationToken cancellationToken)
    {
        var snapshot = registry.Get(command.Instance).Start();
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "instance-start",
            command.Instance, "success", null), cancellationToken);
        return snapshot;
    }
}

public sealed class StopInstanceCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<StopInstanceCommand, InstanceSnapshot>
{
    public async Task<InstanceSnapshot> Handle(StopInstanceCommand command, CancellationToken cancellationToken)
    {
        var snapshot = registry.Get(command.Instance).Stop();
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "instance-stop",
            command.Instance, "success", null), cancellationToken);
        return snapshot;
    }
}

public sealed class RestartInstanceCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<RestartInstanceCommand, InstanceSnapshot>
{
    public async Task<InstanceSnapshot> Handle(RestartInstanceCommand command, CancellationToken cancellationToken)
    {
        var snapshot = registry.Get(command.Instance).Restart();
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "instance-restart",
            command.Instance, "success", null), cancellationToken);
        return snapshot;
    }
}
