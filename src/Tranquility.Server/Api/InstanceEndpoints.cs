using Tranquility.Application;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Commands;
using Tranquility.Application.Queries;
using Tranquility.Wire;

namespace Tranquility.Server.Api;

/// <summary>Documented Instances methods (L2-API-001, L2-LIF-001, L1-LIF-001).</summary>
public static class InstanceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/instances",
            async (IQueryDispatcher queries, CancellationToken ct) =>
            {
                var snapshots = await queries.Dispatch(new GetInstancesQuery(), ct);
                return Results.Ok(new ListInstancesResponse(snapshots.Select(WireMapper.ToWire).ToList()));
            });

        app.MapGet("/api/instances/{instance}",
            async (string instance, IQueryDispatcher queries, CancellationToken ct) =>
                Results.Ok(WireMapper.ToWire(await queries.Dispatch(new GetInstanceQuery(instance), ct))));

        app.MapPost("/api/instances/{instance}:start",
            async (string instance, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(WireMapper.ToWire(await commands.Dispatch(
                    new StartInstanceCommand(instance, http.User.Identity!.Name!), ct))))
            .RequireAuthorization(SystemPrivileges.ControlProcessor);

        app.MapPost("/api/instances/{instance}:stop",
            async (string instance, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(WireMapper.ToWire(await commands.Dispatch(
                    new StopInstanceCommand(instance, http.User.Identity!.Name!), ct))))
            .RequireAuthorization(SystemPrivileges.ControlProcessor);

        app.MapPost("/api/instances/{instance}:restart",
            async (string instance, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(WireMapper.ToWire(await commands.Dispatch(
                    new RestartInstanceCommand(instance, http.User.Identity!.Name!), ct))))
            .RequireAuthorization(SystemPrivileges.ControlProcessor);
    }
}

/// <summary>Application snapshot → documented wire shape.</summary>
public static class WireMapper
{
    public static InstanceInfo ToWire(InstanceSnapshot snapshot) => new(
        snapshot.Name,
        snapshot.State.ToString().ToUpperInvariant(),
        snapshot.MissionTime,
        snapshot.Processors.Select(p => new Wire.ProcessorInfo(p.Name, p.Type, p.State)).ToList());
}
