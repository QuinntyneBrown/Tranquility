using Tranquility.Application.Abstractions;

namespace Tranquility.Application.Queries;

public sealed record LinkSnapshot(
    string Name,
    string Type,
    LinkStatus Status,
    bool Disabled,
    long DataInCount,
    long DataOutCount,
    string DetailedStatus,
    int? BoundPort,
    IReadOnlyList<LinkActionSpec> Actions);

public sealed record ListLinksQuery(string Instance) : IQuery<IReadOnlyList<LinkSnapshot>>;

public sealed class ListLinksQueryHandler(InstanceRegistry registry)
    : IQueryHandler<ListLinksQuery, IReadOnlyList<LinkSnapshot>>
{
    public Task<IReadOnlyList<LinkSnapshot>> Handle(ListLinksQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LinkSnapshot>>(
            registry.Get(query.Instance).Links.Select(Snapshot).ToList());

    internal static LinkSnapshot Snapshot(ILink link) => new(
        link.Name, link.Type, link.Status, !link.Enabled,
        link.DataInCount, link.DataOutCount, link.DetailedStatus, link.BoundPort, link.Actions);
}
