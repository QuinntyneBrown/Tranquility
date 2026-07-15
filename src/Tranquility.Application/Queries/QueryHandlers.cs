using Tranquility.Application.Abstractions;
using Tranquility.Application.Processing;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;

namespace Tranquility.Application.Queries;

// Read-side handlers for the externally documented resource families
// (instances, processors, links, MDB, parameter values). CQRS split per L2-QLT-006.
// Response shaping to the wire contract happens in the API layer.

public sealed record InstanceDto(string Name, string State);

public sealed record ProcessorDto(string Instance, string Name, string Type, string State);

public sealed record LinkDto(string Instance, string Name, string Type, string Status, bool Disabled, long DataInCount);

public sealed record ListInstancesQuery : IQuery<IReadOnlyList<InstanceDto>>;

public sealed record GetInstanceQuery(string Instance) : IQuery<InstanceDto?>;

public sealed record ListProcessorsQuery : IQuery<IReadOnlyList<ProcessorDto>>;

public sealed record ListLinksQuery(string Instance) : IQuery<IReadOnlyList<LinkDto>?>;

public sealed record ListParametersQuery(string Instance) : IQuery<IReadOnlyList<Parameter>?>;

public sealed record GetParameterValueQuery(string Instance, string Processor, string QualifiedName)
    : IQuery<ParameterValue?>;

/// <summary>Implements the read side for L2-API-001 and companions.</summary>
public sealed class InstanceQueryHandlers :
    IQueryHandler<ListInstancesQuery, IReadOnlyList<InstanceDto>>,
    IQueryHandler<GetInstanceQuery, InstanceDto?>,
    IQueryHandler<ListProcessorsQuery, IReadOnlyList<ProcessorDto>>,
    IQueryHandler<ListLinksQuery, IReadOnlyList<LinkDto>?>
{
    private readonly InstanceRegistry _registry;

    public InstanceQueryHandlers(InstanceRegistry registry)
    {
        _registry = registry;
    }

    public Task<IReadOnlyList<InstanceDto>> Handle(ListInstancesQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<InstanceDto> result = [ToDto(_registry)];
        return Task.FromResult(result);
    }

    public Task<InstanceDto?> Handle(GetInstanceQuery query, CancellationToken cancellationToken)
    {
        InstanceDto? result = string.Equals(query.Instance, _registry.InstanceName, StringComparison.Ordinal)
            ? ToDto(_registry)
            : null;
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ProcessorDto>> Handle(ListProcessorsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProcessorDto> result = _registry.Processors
            .Select(p => new ProcessorDto(p.Instance, p.Name, p.Type, p.State.ToString().ToUpperInvariant()))
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<LinkDto>?> Handle(ListLinksQuery query, CancellationToken cancellationToken)
    {
        if (!string.Equals(query.Instance, _registry.InstanceName, StringComparison.Ordinal))
        {
            return Task.FromResult<IReadOnlyList<LinkDto>?>(null);
        }

        IReadOnlyList<LinkDto> result = _registry.Links
            .Select(l => new LinkDto(
                _registry.InstanceName, l.Name, l.Type, l.Status.ToString().ToUpperInvariant(), !l.Enabled, l.DataInCount))
            .ToArray();
        return Task.FromResult<IReadOnlyList<LinkDto>?>(result);
    }

    private static InstanceDto ToDto(InstanceRegistry registry) =>
        new(registry.InstanceName, registry.State.ToString().ToUpperInvariant());
}

/// <summary>Implements the read side for MDB and parameter-value retrieval.</summary>
public sealed class ParameterQueryHandlers :
    IQueryHandler<ListParametersQuery, IReadOnlyList<Parameter>?>,
    IQueryHandler<GetParameterValueQuery, ParameterValue?>
{
    private readonly InstanceRegistry _registry;
    private readonly MissionDatabase _mdb;
    private readonly ParameterCache _cache;

    public ParameterQueryHandlers(InstanceRegistry registry, MissionDatabase mdb, ParameterCache cache)
    {
        _registry = registry;
        _mdb = mdb;
        _cache = cache;
    }

    public Task<IReadOnlyList<Parameter>?> Handle(ListParametersQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Parameter>? result = string.Equals(query.Instance, _registry.InstanceName, StringComparison.Ordinal)
            ? _mdb.Parameters.OrderBy(p => p.QualifiedName, StringComparer.Ordinal).ToArray()
            : null;
        return Task.FromResult(result);
    }

    public Task<ParameterValue?> Handle(GetParameterValueQuery query, CancellationToken cancellationToken)
    {
        if (!string.Equals(query.Instance, _registry.InstanceName, StringComparison.Ordinal))
        {
            return Task.FromResult<ParameterValue?>(null);
        }

        return Task.FromResult(_cache.TryGet(query.QualifiedName, out var value) ? value : null);
    }
}
