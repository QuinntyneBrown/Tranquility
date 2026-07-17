using Tranquility.Application.Abstractions;
using Tranquility.Application.Commands;
using Tranquility.Application.Queries;
using Tranquility.Wire;

namespace Tranquility.Server.Api;

/// <summary>Documented link methods (L2-LNK-001..004).</summary>
public static class LinkEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/links/{instance}",
            async (string instance, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var links = await queries.Dispatch(new ListLinksQuery(instance), ct);
                return Results.Ok(new ListLinksResponse(links.Select(ToWire).ToList()));
            });

        app.MapPost("/api/links/{instance}/{link}:enable",
            (string instance, string link, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                SetEnabled(instance, link, true, http, commands, ct))
            .RequireAuthorization(SystemPrivileges.ControlLinks);

        app.MapPost("/api/links/{instance}/{link}:disable",
            (string instance, string link, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                SetEnabled(instance, link, false, http, commands, ct))
            .RequireAuthorization(SystemPrivileges.ControlLinks);

        app.MapPost("/api/links/{instance}/{link}:resetCounters",
            async (string instance, string link, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(ToWire(await commands.Dispatch(
                    new ResetLinkCountersCommand(instance, link, http.User.Identity!.Name!), ct))))
            .RequireAuthorization(SystemPrivileges.ControlLinks);

        app.MapPost("/api/links/{instance}/{link}/actions/{action}",
            async (string instance, string link, string action, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
                Results.Ok(await commands.Dispatch(
                    new RunLinkActionCommand(instance, link, action, http.User.Identity!.Name!), ct)))
            .RequireAuthorization(SystemPrivileges.ControlLinks);
    }

    private static async Task<IResult> SetEnabled(
        string instance, string link, bool enabled, HttpContext http, ICommandDispatcher commands, CancellationToken ct) =>
        Results.Ok(ToWire(await commands.Dispatch(
            new SetLinkEnabledCommand(instance, link, enabled, http.User.Identity!.Name!), ct)));

    private static LinkInfo ToWire(LinkSnapshot s) => new(
        s.Name, s.Type, s.Status.ToString().ToUpperInvariant(), s.Disabled,
        s.DataInCount, s.DataOutCount, s.DetailedStatus, s.BoundPort,
        s.Actions.Select(a => new LinkActionInfo(a.Id, a.Label, a.Enabled)).ToList());
}
