using Tranquility.Application.Abstractions;
using Tranquility.Application.Queries;

namespace Tranquility.Application.Commands;

public sealed record LoadMissionDatabaseCommand(string Instance, string XtceRef, string Actor)
    : ICommand<MdbOverviewSnapshot>;

/// <summary>
/// Validates and activates a mission database (L2-MDB-001). Any error-level
/// diagnostic rejects activation with the exhaustive report; the previously
/// active model stays active.
/// </summary>
public sealed class LoadMissionDatabaseCommandHandler(
    InstanceRegistry registry,
    IMdbLoader loader,
    IAuditLog audit,
    TimeProvider time)
    : ICommandHandler<LoadMissionDatabaseCommand, MdbOverviewSnapshot>
{
    public async Task<MdbOverviewSnapshot> Handle(LoadMissionDatabaseCommand command, CancellationToken cancellationToken)
    {
        var instance = registry.Get(command.Instance);
        var result = loader.LoadReference(command.XtceRef);
        if (!result.Success)
        {
            await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "mdb-load-rejected",
                $"{command.Instance}:{command.XtceRef}", "rejected",
                $"{result.Errors.Count()} validation error(s)"), cancellationToken);
            throw new ValidationServiceException(
                $"XTCE document '{command.XtceRef}' failed validation with {result.Errors.Count()} error(s)",
                result.Diagnostics);
        }

        instance.ActivateMdb(result.Database!);
        await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), command.Actor, "mdb-load",
            $"{command.Instance}:{command.XtceRef}", "success", $"version {result.Database!.Version}"), cancellationToken);

        var mdb = result.Database!;
        return new MdbOverviewSnapshot(mdb.Version, mdb.Parameters.Count, mdb.ParameterTypes.Count,
            mdb.Containers.Count, CommandCount: 0, AlgorithmCount: 0);
    }
}
