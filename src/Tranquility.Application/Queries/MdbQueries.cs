using Tranquility.Application.Abstractions;
using Tranquility.Core.Mdb;

namespace Tranquility.Application.Queries;

public sealed record MdbOverviewSnapshot(
    string Version,
    int ParameterCount,
    int ParameterTypeCount,
    int ContainerCount,
    int CommandCount,
    int AlgorithmCount);

public sealed record SpaceSystemNode(
    string Name,
    string QualifiedName,
    int ParameterCount,
    int ContainerCount,
    IReadOnlyList<SpaceSystemNode> Children);

public sealed record MdbParameterSnapshot(
    string Name,
    string QualifiedName,
    IReadOnlyList<ParameterAlias> Aliases,
    string TypeName,
    string EngType);

public sealed record GetMdbOverviewQuery(string Instance) : IQuery<MdbOverviewSnapshot>;

public sealed record GetSpaceSystemsQuery(string Instance) : IQuery<IReadOnlyList<SpaceSystemNode>>;

public sealed record GetMdbParameterQuery(string Instance, string Name) : IQuery<MdbParameterSnapshot>;

public sealed class GetMdbOverviewQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetMdbOverviewQuery, MdbOverviewSnapshot>
{
    public Task<MdbOverviewSnapshot> Handle(GetMdbOverviewQuery query, CancellationToken cancellationToken)
    {
        var mdb = registry.Get(query.Instance).RequireMdb();
        return Task.FromResult(new MdbOverviewSnapshot(
            mdb.Version,
            mdb.Parameters.Count,
            mdb.ParameterTypes.Count,
            mdb.Containers.Count,
            CommandCount: mdb.Commands.Count,
            AlgorithmCount: 0));
    }
}

public sealed class GetSpaceSystemsQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetSpaceSystemsQuery, IReadOnlyList<SpaceSystemNode>>
{
    public Task<IReadOnlyList<SpaceSystemNode>> Handle(GetSpaceSystemsQuery query, CancellationToken cancellationToken)
    {
        var mdb = registry.Get(query.Instance).RequireMdb();
        return Task.FromResult<IReadOnlyList<SpaceSystemNode>>([ToNode(mdb.Root)]);
    }

    private static SpaceSystemNode ToNode(SpaceSystem system) => new(
        system.Name,
        system.QualifiedName,
        system.Parameters.Count,
        system.Containers.Count,
        system.Children.Select(ToNode).ToList());
}

public sealed class GetMdbParameterQueryHandler(InstanceRegistry registry)
    : IQueryHandler<GetMdbParameterQuery, MdbParameterSnapshot>
{
    public Task<MdbParameterSnapshot> Handle(GetMdbParameterQuery query, CancellationToken cancellationToken)
    {
        var mdb = registry.Get(query.Instance).RequireMdb();

        // Accept "/A/B", "A/B", or a bare alias (L2-MDB-003).
        var parameter = mdb.ResolveParameter(query.Name)
            ?? mdb.ResolveParameter($"/{query.Name}")
            ?? throw new NotFoundServiceException($"Parameter '{query.Name}' not found");

        return Task.FromResult(new MdbParameterSnapshot(
            parameter.Name,
            parameter.QualifiedName,
            parameter.Aliases,
            parameter.Type.Name,
            EngTypeOf(parameter.Type)));
    }

    private static string EngTypeOf(ParameterType type) => type switch
    {
        IntegerParameterType { Calibrator: not null } or FloatParameterType => "FLOAT",
        IntegerParameterType { Signed: true } => "SINT64",
        IntegerParameterType => "UINT64",
        EnumeratedParameterType => "ENUMERATED",
        _ => "UNKNOWN",
    };
}
