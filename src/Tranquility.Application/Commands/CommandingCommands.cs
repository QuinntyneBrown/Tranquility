using Tranquility.Application.Abstractions;
using Tranquility.Application.Commanding;

namespace Tranquility.Application.Commands;

public sealed record IssueCommand(
    string Instance, string Processor, string CommandName,
    IReadOnlyDictionary<string, string> Args, bool Privileged, string Actor) : ICommand<CommandRecord>;

public sealed record AcceptQueueEntryCommand(string Instance, string Processor, string Queue, string EntryId, string Actor)
    : ICommand<bool>;

public sealed record RejectQueueEntryCommand(string Instance, string Processor, string Queue, string EntryId, string Actor)
    : ICommand<bool>;

public sealed class IssueCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<IssueCommand, CommandRecord>
{
    public async Task<CommandRecord> Handle(IssueCommand command, CancellationToken cancellationToken)
    {
        var instance = registry.Get(command.Instance);
        var record = instance.RequireCommands().Issue(command.CommandName, command.Args, command.Actor);
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "command-issue",
            $"{command.Instance}/commands/{record.CommandName}", "success", record.Id), cancellationToken);
        return record;
    }
}

public sealed class AcceptQueueEntryCommandHandler(InstanceRegistry registry)
    : ICommandHandler<AcceptQueueEntryCommand, bool>
{
    public async Task<bool> Handle(AcceptQueueEntryCommand command, CancellationToken cancellationToken)
    {
        await registry.Get(command.Instance).RequireCommands()
            .AcceptAsync(command.EntryId, command.Actor, cancellationToken);
        return true;
    }
}

public sealed class RejectQueueEntryCommandHandler(InstanceRegistry registry)
    : ICommandHandler<RejectQueueEntryCommand, bool>
{
    public async Task<bool> Handle(RejectQueueEntryCommand command, CancellationToken cancellationToken)
    {
        await registry.Get(command.Instance).RequireCommands()
            .RejectAsync(command.EntryId, command.Actor, cancellationToken);
        return true;
    }
}
