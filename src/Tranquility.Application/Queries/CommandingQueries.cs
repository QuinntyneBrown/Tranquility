using Tranquility.Application.Abstractions;
using Tranquility.Application.Commanding;

namespace Tranquility.Application.Queries;

public sealed record QueueSnapshot(string Name, string State, IReadOnlyList<QueueEntry> Entries);

public sealed record ListQueuesQuery(string Instance, string Processor) : IQuery<IReadOnlyList<QueueSnapshot>>;

public sealed record GetCommandHistoryQuery(string Instance) : IQuery<IReadOnlyList<CommandRecord>>;

public sealed record GetCop1StatusQuery(string Instance, string Link) : IQuery<Cop1Status>;

public sealed record GetAuditRecordsQuery(string? Service) : IQuery<IReadOnlyList<AuditEntry>>;

public sealed record VerifyAuditQuery : IQuery<AuditChainStatus>;

public sealed class ListQueuesQueryHandler(InstanceRegistry registry)
    : IQueryHandler<ListQueuesQuery, IReadOnlyList<QueueSnapshot>>
{
    public Task<IReadOnlyList<QueueSnapshot>> Handle(ListQueuesQuery query, CancellationToken cancellationToken)
    {
        var commands = registry.Get(query.Instance).RequireCommands();
        return Task.FromResult<IReadOnlyList<QueueSnapshot>>(
            [new QueueSnapshot(CommandService.DefaultQueue, "ENABLED", commands.QueueEntries())]);
    }
}

public sealed class GetCommandHistoryQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetCommandHistoryQuery, IReadOnlyList<CommandRecord>>
{
    public Task<IReadOnlyList<CommandRecord>> Handle(GetCommandHistoryQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CommandRecord>>(
            registry.Get(query.Instance).RequireCommands().History
                .OrderBy(c => c.SequenceNumber).ToList());
}

public sealed class GetCop1StatusQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetCop1StatusQuery, Cop1Status>
{
    public Task<Cop1Status> Handle(GetCop1StatusQuery query, CancellationToken cancellationToken)
    {
        var instance = registry.Get(query.Instance);
        return Task.FromResult(instance.Cop1Services.TryGetValue(query.Link, out var service)
            ? service.Status()
            : throw new NotFoundServiceException($"No COP-1 session for link '{query.Link}'"));
    }
}

public sealed class GetAuditRecordsQueryHandler(IAuditQuery audit)
    : IQueryHandler<GetAuditRecordsQuery, IReadOnlyList<AuditEntry>>
{
    public Task<IReadOnlyList<AuditEntry>> Handle(GetAuditRecordsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(audit.Query(query.Service));
}

public sealed class VerifyAuditQueryHandler(IAuditQuery audit)
    : IQueryHandler<VerifyAuditQuery, AuditChainStatus>
{
    public Task<AuditChainStatus> Handle(VerifyAuditQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(audit.Verify());
}
