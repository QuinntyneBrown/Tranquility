using Tranquility.Application.Abstractions;

namespace Tranquility.Application.Queries;

public sealed record GetInstancesQuery : IQuery<IReadOnlyList<InstanceSnapshot>>;

public sealed record GetInstanceQuery(string Instance) : IQuery<InstanceSnapshot>;

public sealed class GetInstancesQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetInstancesQuery, IReadOnlyList<InstanceSnapshot>>
{
    public Task<IReadOnlyList<InstanceSnapshot>> Handle(GetInstancesQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InstanceSnapshot>>(
            registry.Instances.Select(i => i.Snapshot()).OrderBy(s => s.Name, StringComparer.Ordinal).ToList());
}

public sealed class GetInstanceQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetInstanceQuery, InstanceSnapshot>
{
    public Task<InstanceSnapshot> Handle(GetInstanceQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(registry.Get(query.Instance).Snapshot());
}
