using Tranquility.Application.Abstractions;

namespace Tranquility.Application.Queries;

public sealed record ProcessorListEntry(string Instance, ProcessorSnapshot Processor);

public sealed record ListProcessorsQuery : IQuery<IReadOnlyList<ProcessorListEntry>>;

public sealed record GetProcessorQuery(string Instance, string Processor) : IQuery<ProcessorListEntry>;

public sealed class ListProcessorsQueryHandler(InstanceRegistry registry)
    : IQueryHandler<ListProcessorsQuery, IReadOnlyList<ProcessorListEntry>>
{
    public Task<IReadOnlyList<ProcessorListEntry>> Handle(ListProcessorsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProcessorListEntry>>(registry.Instances
            .SelectMany(i => i.Processors.Select(p => new ProcessorListEntry(i.Name, p.Snapshot())))
            .OrderBy(e => e.Instance, StringComparer.Ordinal)
            .ThenBy(e => e.Processor.Name, StringComparer.Ordinal)
            .ToList());
}

public sealed class GetProcessorQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetProcessorQuery, ProcessorListEntry>
{
    public Task<ProcessorListEntry> Handle(GetProcessorQuery query, CancellationToken cancellationToken)
    {
        var instance = registry.Get(query.Instance);
        return Task.FromResult(new ProcessorListEntry(
            instance.Name, instance.FindProcessor(query.Processor).Snapshot()));
    }
}
