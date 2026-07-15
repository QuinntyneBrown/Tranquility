using Tranquility.Application;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Commands;
using Tranquility.Application.Processing;
using Tranquility.Application.Queries;
using Tranquility.Core.Alarms;
using Tranquility.Core.Mdb;
using Tranquility.Infrastructure;
using Tranquility.Infrastructure.Links;
using Tranquility.Infrastructure.Security;
using Tranquility.Infrastructure.Xtce;
using Tranquility.Server.Api;
using Tranquility.Server.Hosting;
using Tranquility.Server.Security;
using Tranquility.Server.WebSockets;

// Tranquility server host. Endpoints per docs/specs/TRQ-ICD-API.md.
// JSON wire format only; protobuf content negotiation deferred (TBD-012).

var builder = WebApplication.CreateBuilder(args);

string instanceName = builder.Configuration["Tranquility:Instance"] ?? "sample";
string mdbPath = builder.Configuration["Tranquility:MdbPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "SampleSat.xml");
int udpPort = builder.Configuration.GetValue("Tranquility:UdpPort", 10015);

var securityOptions = new SecurityOptions();
builder.Configuration.GetSection(SecurityOptions.SectionName).Bind(securityOptions);

builder.Services.AddSingleton(securityOptions);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IAuditLog, InMemoryAuditLog>();
builder.Services.AddSingleton<IMdbSource>(new XtceLoader(mdbPath));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMdbSource>().Load());
builder.Services.AddSingleton<ParameterCache>();
builder.Services.AddSingleton<SubscriptionManager>();
builder.Services.AddSingleton<AlarmStateTracker>();
builder.Services.AddSingleton(sp =>
{
    var mdb = sp.GetRequiredService<MissionDatabase>();
    var registry = new InstanceRegistry(instanceName);
    registry.AddLink(new UdpPacketLink("udp-in", udpPort));

    var rootContainer = mdb.RootContainers.First();
    registry.AddProcessor(new TelemetryProcessor(
        instanceName,
        "realtime",
        mdb,
        rootContainer,
        registry.Links,
        sp.GetRequiredService<ParameterCache>(),
        sp.GetRequiredService<SubscriptionManager>(),
        sp.GetRequiredService<AlarmStateTracker>(),
        sp.GetRequiredService<IClock>()));
    return registry;
});
builder.Services.AddSingleton<InstanceQueryHandlers>();
builder.Services.AddSingleton<ParameterQueryHandlers>();
builder.Services.AddSingleton<LinkCommandHandlers>();
builder.Services.AddSingleton<WebSocketApiHandler>();
builder.Services.AddHostedService<TelemetryHostedService>();

var app = builder.Build();

app.UseMiddleware<ErrorEnvelopeMiddleware>();
app.UseWebSockets();

// --- Instances (L2-API-001) ---

app.MapGet("/api/instances", async (InstanceQueryHandlers handlers, CancellationToken ct) =>
    ApiResults.Json(new InstancesResponse(
        (await handlers.Handle(new ListInstancesQuery(), ct)).Select(i => new InstanceInfoDto(i.Name, i.State)).ToArray())));

app.MapGet("/api/instances/{instance}", async (string instance, InstanceQueryHandlers handlers, CancellationToken ct) =>
{
    var result = await handlers.Handle(new GetInstanceQuery(instance), ct);
    return result is null
        ? ApiResults.NotFound($"Instance '{instance}' not found.")
        : ApiResults.Json(new InstanceInfoDto(result.Name, result.State));
});

// --- Processors ---

app.MapGet("/api/processors", async (InstanceQueryHandlers handlers, CancellationToken ct) =>
    ApiResults.Json(new ProcessorsResponse(
        (await handlers.Handle(new ListProcessorsQuery(), ct))
            .Select(p => new ProcessorInfoDto(p.Instance, p.Name, p.Type, p.State)).ToArray())));

// --- Links (L2-LNK-002) ---

app.MapGet("/api/links/{instance}", async (string instance, InstanceQueryHandlers handlers, CancellationToken ct) =>
{
    var result = await handlers.Handle(new ListLinksQuery(instance), ct);
    return result is null
        ? ApiResults.NotFound($"Instance '{instance}' not found.")
        : ApiResults.Json(new LinksResponse(
            result.Select(l => new LinkInfoDto(l.Instance, l.Name, l.Type, l.Status, l.Disabled, l.DataInCount)).ToArray()));
});

app.MapPost("/api/links/{instance}/{link}:enable", async (string instance, string link, LinkCommandHandlers handlers, CancellationToken ct) =>
        await handlers.Handle(new SetLinkEnabledCommand(instance, link, Enabled: true), ct)
            ? Results.Ok()
            : ApiResults.NotFound($"Link '{link}' not found on instance '{instance}'."))
    .AddEndpointFilter<PrivilegedEndpointFilter>();

app.MapPost("/api/links/{instance}/{link}:disable", async (string instance, string link, LinkCommandHandlers handlers, CancellationToken ct) =>
        await handlers.Handle(new SetLinkEnabledCommand(instance, link, Enabled: false), ct)
            ? Results.Ok()
            : ApiResults.NotFound($"Link '{link}' not found on instance '{instance}'."))
    .AddEndpointFilter<PrivilegedEndpointFilter>();

// --- MDB (L2-MDB-001) ---

app.MapGet("/api/mdb/{instance}/parameters", async (string instance, ParameterQueryHandlers handlers, CancellationToken ct) =>
{
    var result = await handlers.Handle(new ListParametersQuery(instance), ct);
    return result is null
        ? ApiResults.NotFound($"Instance '{instance}' not found.")
        : ApiResults.Json(new ParametersResponse(result.Select(WireMapper.ToDto).ToArray()));
});

app.MapGet("/api/mdb/{instance}/parameters/{**name}", async (string instance, string name, ParameterQueryHandlers handlers, MissionDatabase mdb, CancellationToken ct) =>
{
    var parameters = await handlers.Handle(new ListParametersQuery(instance), ct);
    if (parameters is null)
    {
        return ApiResults.NotFound($"Instance '{instance}' not found.");
    }

    var parameter = mdb.FindParameter($"/{name}");
    return parameter is null
        ? ApiResults.NotFound($"Parameter '/{name}' not found.")
        : ApiResults.Json(WireMapper.ToDto(parameter));
});

// --- Parameter values (L2-PAR-001) ---

app.MapGet("/api/processors/{instance}/{processor}/parameters/{**name}", async (string instance, string processor, string name, ParameterQueryHandlers handlers, CancellationToken ct) =>
{
    var value = await handlers.Handle(new GetParameterValueQuery(instance, processor, $"/{name}"), ct);
    return value is null
        ? ApiResults.NotFound($"No value for parameter '/{name}'.")
        : ApiResults.Json(WireMapper.ToDto(value));
});

// --- WebSocket subscription API (L2-API-003, L2-RTS-001..003) ---

app.Map("/api/websocket", (HttpContext context, WebSocketApiHandler handler) => handler.HandleAsync(context));

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
