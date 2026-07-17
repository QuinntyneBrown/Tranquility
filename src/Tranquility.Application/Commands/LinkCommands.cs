using Tranquility.Application.Abstractions;
using Tranquility.Application.Queries;

namespace Tranquility.Application.Commands;

// Documented link control operations (L2-LNK-002, L2-LNK-003).

public sealed record SetLinkEnabledCommand(string Instance, string Link, bool Enabled, string Actor)
    : ICommand<LinkSnapshot>;

public sealed record ResetLinkCountersCommand(string Instance, string Link, string Actor)
    : ICommand<LinkSnapshot>;

public sealed record RunLinkActionCommand(string Instance, string Link, string Action, string Actor)
    : ICommand<object>;

public sealed class SetLinkEnabledCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<SetLinkEnabledCommand, LinkSnapshot>
{
    public async Task<LinkSnapshot> Handle(SetLinkEnabledCommand command, CancellationToken cancellationToken)
    {
        var link = registry.Get(command.Instance).FindLink(command.Link);
        if (command.Enabled)
        {
            link.Enable();
        }
        else
        {
            link.Disable();
        }

        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor,
            command.Enabled ? "link-enable" : "link-disable",
            $"{command.Instance}/links/{command.Link}", "success", null), cancellationToken);
        return ListLinksQueryHandler.Snapshot(link);
    }
}

public sealed class ResetLinkCountersCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<ResetLinkCountersCommand, LinkSnapshot>
{
    public async Task<LinkSnapshot> Handle(ResetLinkCountersCommand command, CancellationToken cancellationToken)
    {
        var link = registry.Get(command.Instance).FindLink(command.Link);
        link.ResetCounters();
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "link-reset-counters",
            $"{command.Instance}/links/{command.Link}", "success", null), cancellationToken);
        return ListLinksQueryHandler.Snapshot(link);
    }
}

public sealed class RunLinkActionCommandHandler(InstanceRegistry registry, IAuditLog audit, TimeProvider time)
    : ICommandHandler<RunLinkActionCommand, object>
{
    public async Task<object> Handle(RunLinkActionCommand command, CancellationToken cancellationToken)
    {
        var link = registry.Get(command.Instance).FindLink(command.Link);
        var result = await link.RunActionAsync(command.Action, cancellationToken);
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "link-run-action",
            $"{command.Instance}/links/{command.Link}/actions/{command.Action}", "success", null), cancellationToken);
        return result;
    }
}
