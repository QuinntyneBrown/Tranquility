using Tranquility.Application.Abstractions;

namespace Tranquility.Application.Commands;

// Write-side handlers (CQRS split per L2-QLT-006). Link enable/disable are the
// documented link operations in scope for the vertical slice (L2-LNK-002).

public sealed record SetLinkEnabledCommand(string Instance, string Link, bool Enabled) : ICommand<bool>;

public sealed class LinkCommandHandlers : ICommandHandler<SetLinkEnabledCommand, bool>
{
    private readonly InstanceRegistry _registry;
    private readonly IAuditLog _audit;
    private readonly IClock _clock;

    public LinkCommandHandlers(InstanceRegistry registry, IAuditLog audit, IClock clock)
    {
        _registry = registry;
        _audit = audit;
        _clock = clock;
    }

    public Task<bool> Handle(SetLinkEnabledCommand command, CancellationToken cancellationToken)
    {
        if (!string.Equals(command.Instance, _registry.InstanceName, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        var link = _registry.FindLink(command.Link);
        if (link is null)
        {
            return Task.FromResult(false);
        }

        if (command.Enabled)
        {
            link.Enable();
        }
        else
        {
            link.Disable();
        }

        _audit.Record(new AuditEvent(
            _clock.UtcNow,
            Principal: "anonymous",
            Action: command.Enabled ? "link.enable" : "link.disable",
            Resource: $"{command.Instance}/links/{command.Link}",
            Success: true));

        return Task.FromResult(true);
    }
}
