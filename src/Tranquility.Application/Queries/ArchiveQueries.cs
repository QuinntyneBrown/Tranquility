using Tranquility.Application.Abstractions;
using Tranquility.Application.Processing;
using Tranquility.Core.Ccsds;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Queries;

public enum HistorySource
{
    Archive,
    Replay,
}

public sealed record GetParameterHistoryQuery(
    string Instance,
    string Name,
    long? StartUs,
    long? StopUs,
    int Limit,
    bool Descending,
    HistorySource Source) : IQuery<IReadOnlyList<ArchivedParameterValue>>;

public sealed record ListPidsQuery(string Instance) : IQuery<IReadOnlyList<PidInfo>>;

public sealed record ListSegmentsQuery(string Instance, int Pid) : IQuery<IReadOnlyList<SegmentInfo>>;

/// <summary>
/// Parameter history retrieval (L2-ARC-001) with the documented replay source
/// option (L2-ARC-004): replay mode re-decommutates archived raw packets with
/// the ACTIVE mission database.
/// </summary>
public sealed class GetParameterHistoryQueryHandler(InstanceRegistry registry, IArchive archive)
    : IQueryHandler<GetParameterHistoryQuery, IReadOnlyList<ArchivedParameterValue>>
{
    public async Task<IReadOnlyList<ArchivedParameterValue>> Handle(
        GetParameterHistoryQuery query, CancellationToken cancellationToken)
    {
        var instance = registry.Get(query.Instance);
        var mdb = instance.RequireMdb();
        var parameter = mdb.ResolveParameter(query.Name) ?? mdb.ResolveParameter($"/{query.Name}")
            ?? throw new NotFoundServiceException($"Parameter '{query.Name}' not found");

        if (query.Source == HistorySource.Archive)
        {
            return await archive.GetParameterHistoryAsync(instance.Name, parameter.QualifiedName,
                query.StartUs, query.StopUs, query.Limit, query.Descending, cancellationToken);
        }

        // Replay source: reprocess stored packets through decommutation.
        var root = mdb.RootContainers.FirstOrDefault()
            ?? throw new NotFoundServiceException("Active mission database has no root container");
        var engine = new DecommutationEngine(mdb);
        var results = new List<ArchivedParameterValue>();
        await foreach (var packet in archive.ReadPacketsAsync(
            instance.Name, query.StartUs, query.StopUs, cancellationToken))
        {
            if (SpacePacketValidator.Validate(packet.Data) is not null ||
                SpacePacketHeader.Parse(packet.Data).IsIdle)
            {
                continue;
            }

            var decommutated = engine.Decommutate(packet.Data, root,
                MicroTime.ToDateTimeOffset(packet.GenTimeUs), MicroTime.ToDateTimeOffset(packet.RecTimeUs));
            foreach (var value in decommutated.Values)
            {
                if (value.Parameter.QualifiedName == parameter.QualifiedName)
                {
                    results.Add(new ArchivedParameterValue(
                        value.Parameter.QualifiedName, value.RawValue, value.EngValue,
                        packet.GenTimeUs, packet.RecTimeUs, MonitoringNames.Wire(value.Monitoring)));
                }
            }
        }

        var ordered = query.Descending
            ? results.OrderByDescending(v => v.GenTimeUs)
            : results.OrderBy(v => v.GenTimeUs);
        return ordered.Take(query.Limit).ToList();
    }
}

public sealed class ListPidsQueryHandler(InstanceRegistry registry, IArchive archive)
    : IQueryHandler<ListPidsQuery, IReadOnlyList<PidInfo>>
{
    public Task<IReadOnlyList<PidInfo>> Handle(ListPidsQuery query, CancellationToken cancellationToken)
    {
        registry.Get(query.Instance); // 404 for unknown instances
        return archive.ListPidsAsync(query.Instance, cancellationToken);
    }
}

public sealed class ListSegmentsQueryHandler(InstanceRegistry registry, IArchive archive)
    : IQueryHandler<ListSegmentsQuery, IReadOnlyList<SegmentInfo>>
{
    public Task<IReadOnlyList<SegmentInfo>> Handle(ListSegmentsQuery query, CancellationToken cancellationToken)
    {
        registry.Get(query.Instance);
        return archive.ListSegmentsAsync(query.Instance, query.Pid, cancellationToken);
    }
}
