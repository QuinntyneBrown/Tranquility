using Tranquility.Application.Abstractions;
using Tranquility.Application.Commands;
using Tranquility.Application.Queries;
using Tranquility.Wire;

namespace Tranquility.Server.Api;

/// <summary>Documented processing methods (L2-LIF-002/003/004).</summary>
public static class ProcessorEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/processors",
            async (IQueryDispatcher queries, CancellationToken ct) =>
            {
                var processors = await queries.Dispatch(new ListProcessorsQuery(), ct);
                return Results.Ok(new ListProcessorsResponse(processors.Select(ToWire).ToList()));
            });

        app.MapGet("/api/processors/{instance}/{processor}",
            async (string instance, string processor, IQueryDispatcher queries, CancellationToken ct) =>
                Results.Ok(ToWire(await queries.Dispatch(new GetProcessorQuery(instance, processor), ct))));

        app.MapPost("/api/processors/{instance}",
            async (string instance, CreateProcessorRequest request, HttpContext http,
                ICommandDispatcher commands, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type))
                {
                    throw new BadRequestServiceException("name and type are required");
                }

                var snapshot = await commands.Dispatch(new CreateProcessorCommand(
                    instance, request.Name, request.Type,
                    ToUs(request.Start), ToUs(request.Stop),
                    request.Paused ?? false, request.Persistent ?? false, request.Speed ?? 0,
                    http.User.Identity!.Name!), ct);
                return Results.Ok(ToWire(new ProcessorListEntry(instance, snapshot)));
            })
            .RequireAuthorization(SystemPrivileges.ControlProcessor);

        app.MapPatch("/api/processors/{instance}/{processor}",
            async (string instance, string processor, EditProcessorRequest request, HttpContext http,
                ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(ToWire(new ProcessorListEntry(instance, await commands.Dispatch(
                    new EditProcessorCommand(instance, processor, request.Speed, http.User.Identity!.Name!), ct)))))
            .RequireAuthorization(SystemPrivileges.ControlProcessor);

        app.MapDelete("/api/processors/{instance}/{processor}",
            async (string instance, string processor, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
            {
                await commands.Dispatch(new DeleteProcessorCommand(instance, processor, http.User.Identity!.Name!), ct);
                return Results.Ok();
            })
            .RequireAuthorization(SystemPrivileges.ControlProcessor);

        app.MapPost("/api/processors/{instance}/{processor}:pause",
            async (string instance, string processor, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(ToWire(new ProcessorListEntry(instance, await commands.Dispatch(
                    new PauseProcessorCommand(instance, processor, http.User.Identity!.Name!), ct)))))
            .RequireAuthorization(SystemPrivileges.ControlProcessor);

        app.MapPost("/api/processors/{instance}/{processor}:resume",
            async (string instance, string processor, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(ToWire(new ProcessorListEntry(instance, await commands.Dispatch(
                    new ResumeProcessorCommand(instance, processor, http.User.Identity!.Name!), ct)))))
            .RequireAuthorization(SystemPrivileges.ControlProcessor);
    }

    private static ProcessorDetail ToWire(ProcessorListEntry entry) => new(
        entry.Instance,
        entry.Processor.Name,
        entry.Processor.Type,
        entry.Processor.State,
        entry.Processor.Persistent,
        entry.Processor.ReplayState,
        entry.Processor.Speed);

    private static long? ToUs(DateTimeOffset? t) =>
        t is { } value ? MicroTime.FromDateTimeOffset(value) : null;

    public sealed record CreateProcessorRequest(
        string? Name, string? Type, DateTimeOffset? Start, DateTimeOffset? Stop,
        bool? Paused, bool? Persistent, double? Speed);

    public sealed record EditProcessorRequest(double? Speed);
}
