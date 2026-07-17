using Tranquility.Application.Abstractions;
using Tranquility.Application.Commands;
using Tranquility.Application.Queries;
using Tranquility.Wire;

namespace Tranquility.Server.Api;

/// <summary>Documented MDB resource methods (L2-MDB-001..004).</summary>
public static class MdbEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/mdb/{instance}",
            async (string instance, IQueryDispatcher queries, CancellationToken ct) =>
                Results.Ok(ToWire(await queries.Dispatch(new GetMdbOverviewQuery(instance), ct))));

        app.MapGet("/api/mdb/{instance}/space-systems",
            async (string instance, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var nodes = await queries.Dispatch(new GetSpaceSystemsQuery(instance), ct);
                return Results.Ok(new SpaceSystemsResponse(nodes.Select(ToWire).ToList()));
            });

        app.MapGet("/api/mdb/{instance}/parameters/{**name}",
            async (string instance, string name, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var p = await queries.Dispatch(new GetMdbParameterQuery(instance, name), ct);
                return Results.Ok(new MdbParameterInfo(
                    p.Name, p.QualifiedName,
                    p.Aliases.Select(a => new AliasInfo(a.Namespace, a.Name)).ToList(),
                    new MdbParameterTypeInfo(p.TypeName, p.EngType)));
            });

        app.MapPost("/api/mdb/{instance}/load",
            async (string instance, LoadMdbRequest request, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.XtceRef))
                {
                    throw new BadRequestServiceException("xtceRef is required");
                }

                var overview = await commands.Dispatch(
                    new LoadMissionDatabaseCommand(instance, request.XtceRef, http.User.Identity!.Name!), ct);
                return Results.Ok(ToWire(overview));
            })
            .RequireAuthorization(SystemPrivileges.ControlProcessor);
    }

    private static MdbOverview ToWire(MdbOverviewSnapshot s) => new(
        s.Version, s.ParameterCount, s.ParameterTypeCount, s.ContainerCount, s.CommandCount, s.AlgorithmCount);

    private static SpaceSystemInfo ToWire(SpaceSystemNode n) => new(
        n.Name, n.QualifiedName, n.ParameterCount, n.ContainerCount, n.Children.Select(ToWire).ToList());
}
