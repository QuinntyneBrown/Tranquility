using System.Text.Json;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Commanding;
using Tranquility.Application.Commands;
using Tranquility.Application.Queries;

namespace Tranquility.Server.Api;

/// <summary>Documented commanding + queue + COP-1 methods (L2-CMD-001..006, API-002).</summary>
public static class CommandingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/processors/{instance}/{processor}/commands/{**name}",
            async (string instance, string processor, string name, HttpContext http, JsonElement body,
                ICommandDispatcher commands, CancellationToken ct) =>
            {
                var (args, privileged) = ParseIssueBody(body);

                // Privileged options require CommandOverride (L2-CMD-006).
                if (privileged && !HasPrivilege(http, SystemPrivileges.CommandOverride))
                {
                    throw new ForbiddenServiceException("Privileged command options require the CommandOverride privilege");
                }

                var record = await commands.Dispatch(new IssueCommand(
                    instance, processor, name, args, privileged, http.User.Identity!.Name!), ct);
                return Results.Ok(ToWire(record));
            })
            .RequireAuthorization(SystemPrivileges.CommandIssue);

        app.MapGet("/api/processors/{instance}/{processor}/queues",
            async (string instance, string processor, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var queues = await queries.Dispatch(new ListQueuesQuery(instance, processor), ct);
                return Results.Ok(new
                {
                    queues = queues.Select(q => new
                    {
                        name = q.Name,
                        state = q.State,
                        entries = q.Entries.Select(e => new { id = e.Id, commandName = e.CommandName, e.QueuedTime }).ToList(),
                    }).ToList(),
                });
            });

        app.MapPost("/api/processors/{instance}/{processor}/queues/{queue}/entries/{entryId}:accept",
            async (string instance, string processor, string queue, string entryId, HttpContext http,
                ICommandDispatcher commands, CancellationToken ct) =>
            {
                await commands.Dispatch(new AcceptQueueEntryCommand(instance, processor, queue, entryId, http.User.Identity!.Name!), ct);
                return Results.Ok();
            })
            .RequireAuthorization(SystemPrivileges.CommandQueueControl);

        app.MapPost("/api/processors/{instance}/{processor}/queues/{queue}/entries/{entryId}:reject",
            async (string instance, string processor, string queue, string entryId, HttpContext http,
                ICommandDispatcher commands, CancellationToken ct) =>
            {
                await commands.Dispatch(new RejectQueueEntryCommand(instance, processor, queue, entryId, http.User.Identity!.Name!), ct);
                return Results.Ok();
            })
            .RequireAuthorization(SystemPrivileges.CommandQueueControl);

        app.MapGet("/api/archive/{instance}/commands",
            async (string instance, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var history = await queries.Dispatch(new GetCommandHistoryQuery(instance), ct);
                return Results.Ok(new { entries = history.Select(ToHistoryEntry).ToList() });
            });

        app.MapGet("/api/cop1/{instance}/{link}/status",
            async (string instance, string link, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var status = await queries.Dispatch(new GetCop1StatusQuery(instance, link), ct);
                return Results.Ok(new
                {
                    state = status.State,
                    vS = status.Vs,
                    nnR = status.NnR,
                    sentQueueDepth = status.SentQueueDepth,
                });
            });
    }

    private static (Dictionary<string, string> Args, bool Privileged) ParseIssueBody(JsonElement body)
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("args", out var argsElement)
            && argsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var arg in argsElement.EnumerateObject())
            {
                args[arg.Name] = arg.Value.ValueKind == JsonValueKind.String
                    ? arg.Value.GetString()!
                    : arg.Value.GetRawText();
            }
        }

        bool privileged = body.ValueKind == JsonValueKind.Object &&
            ((body.TryGetProperty("disableVerifiers", out var dv) && dv.ValueKind == JsonValueKind.True) ||
             (body.TryGetProperty("disableTransmissionConstraints", out var dtc) && dtc.ValueKind == JsonValueKind.True));
        return (args, privileged);
    }

    private static bool HasPrivilege(HttpContext http, string privilege) =>
        http.User.HasClaim(Security.TokenService.SuperuserClaim, "true") ||
        http.User.HasClaim(Security.TokenService.PrivilegeClaim, privilege);

    private static object ToWire(CommandRecord record) => new
    {
        id = record.Id,
        commandName = record.CommandName,
        origin = record.Origin,
        sequenceNumber = record.SequenceNumber,
        generationTime = record.GenerationTime,
        binary = Convert.ToBase64String(record.Binary),
        assignments = record.Assignments.Select(a => new { a.Name, a.Value }).ToList(),
    };

    private static object ToHistoryEntry(CommandRecord record) => new
    {
        id = record.Id,
        commandName = record.CommandName,
        origin = record.Origin,
        generationTime = record.GenerationTime,
        binary = Convert.ToBase64String(record.Binary),
        attributes = record.Stages.Select(s => new { name = s.Name, time = s.Time }).ToList(),
    };
}
