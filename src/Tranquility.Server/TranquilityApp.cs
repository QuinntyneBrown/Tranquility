using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Tranquility.Application;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Commands;
using Tranquility.Application.Queries;
using Tranquility.Infrastructure.Security;
using Tranquility.Server.Api;
using Tranquility.Server.Middleware;
using Tranquility.Server.Security;
using Tranquility.Wire.Json;

namespace Tranquility.Server;

/// <summary>
/// Composition root shared by <c>Program</c> and the Kestrel-based acceptance
/// fixtures. Building through one method keeps the tested pipeline identical
/// to the deployed pipeline.
/// </summary>
public static class TranquilityApp
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        var options = new TranquilityOptions();
        builder.Configuration.GetSection(TranquilityOptions.SectionName).Bind(options);
        builder.Services.AddSingleton(options);

        // Documented JSON wire conventions (camelCase, RFC 3339 UTC, UPPERCASE enums).
        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = WireJson.Options.PropertyNamingPolicy;
            json.SerializerOptions.DefaultIgnoreCondition = WireJson.Options.DefaultIgnoreCondition;
            foreach (var converter in WireJson.Options.Converters)
            {
                json.SerializerOptions.Converters.Add(converter);
            }
        });

        // Core services
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IMdbLoader, Infrastructure.Xtce.XtceFileMdbLoader>();
        builder.Services.AddSingleton<ILinkFactory, Infrastructure.Links.LinkFactory>();
        builder.Services.AddSingleton<InstanceRegistry>();
        builder.Services.AddSingleton<Application.Processing.SubscriptionHub>();
        builder.Services.AddSingleton<IArchive, Infrastructure.Sqlite.SqliteArchive>();
        builder.Services.AddSingleton<WebSockets.WebSocketApiHandler>();
        builder.Services.AddHostedService<Hosting.TelemetryHostedService>();
        builder.Services.AddSingleton<IAuditLog, InMemoryAuditLog>();
        builder.Services.AddSingleton<IIdentityStore, ConfigIdentityStore>();
        builder.Services.AddSingleton<TokenService>();

        // CQRS (L2-QLT-006): one dispatcher per path, handlers by convention.
        builder.Services.AddSingleton<Dispatcher>();
        builder.Services.AddSingleton<ICommandDispatcher>(sp => sp.GetRequiredService<Dispatcher>());
        builder.Services.AddSingleton<IQueryDispatcher>(sp => sp.GetRequiredService<Dispatcher>());
        builder.Services.AddSingleton<IQueryHandler<GetInstancesQuery, IReadOnlyList<InstanceSnapshot>>, GetInstancesQueryHandler>();
        builder.Services.AddSingleton<IQueryHandler<GetInstanceQuery, InstanceSnapshot>, GetInstanceQueryHandler>();
        builder.Services.AddSingleton<ICommandHandler<StartInstanceCommand, InstanceSnapshot>, StartInstanceCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<StopInstanceCommand, InstanceSnapshot>, StopInstanceCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<RestartInstanceCommand, InstanceSnapshot>, RestartInstanceCommandHandler>();
        builder.Services.AddSingleton<IQueryHandler<GetMdbOverviewQuery, MdbOverviewSnapshot>, GetMdbOverviewQueryHandler>();
        builder.Services.AddSingleton<IQueryHandler<GetSpaceSystemsQuery, IReadOnlyList<SpaceSystemNode>>, GetSpaceSystemsQueryHandler>();
        builder.Services.AddSingleton<IQueryHandler<GetMdbParameterQuery, MdbParameterSnapshot>, GetMdbParameterQueryHandler>();
        builder.Services.AddSingleton<ICommandHandler<LoadMissionDatabaseCommand, MdbOverviewSnapshot>, LoadMissionDatabaseCommandHandler>();
        builder.Services.AddSingleton<IQueryHandler<ListLinksQuery, IReadOnlyList<LinkSnapshot>>, ListLinksQueryHandler>();
        builder.Services.AddSingleton<ICommandHandler<SetLinkEnabledCommand, LinkSnapshot>, SetLinkEnabledCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<ResetLinkCountersCommand, LinkSnapshot>, ResetLinkCountersCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<RunLinkActionCommand, object>, RunLinkActionCommandHandler>();
        builder.Services.AddSingleton<IQueryHandler<GetParameterHistoryQuery, IReadOnlyList<ArchivedParameterValue>>, GetParameterHistoryQueryHandler>();
        builder.Services.AddSingleton<IQueryHandler<ListPidsQuery, IReadOnlyList<PidInfo>>, ListPidsQueryHandler>();
        builder.Services.AddSingleton<IQueryHandler<ListSegmentsQuery, IReadOnlyList<SegmentInfo>>, ListSegmentsQueryHandler>();
        builder.Services.AddSingleton<IQueryHandler<ListProcessorsQuery, IReadOnlyList<ProcessorListEntry>>, ListProcessorsQueryHandler>();
        builder.Services.AddSingleton<IQueryHandler<GetProcessorQuery, ProcessorListEntry>, GetProcessorQueryHandler>();
        builder.Services.AddSingleton<ICommandHandler<CreateProcessorCommand, ProcessorSnapshot>, CreateProcessorCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<EditProcessorCommand, ProcessorSnapshot>, EditProcessorCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<DeleteProcessorCommand, bool>, DeleteProcessorCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<PauseProcessorCommand, ProcessorSnapshot>, PauseProcessorCommandHandler>();
        builder.Services.AddSingleton<ICommandHandler<ResumeProcessorCommand, ProcessorSnapshot>, ResumeProcessorCommandHandler>();

        // AuthN/AuthZ from day one (L2-SEC-002, L2-SEC-003).
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                var tokens = new TokenService(options, TimeProvider.System);
                bearer.TokenValidationParameters = tokens.ValidationParameters;
            });
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, EnvelopeAuthorizationResultHandler>();
        builder.Services.AddAuthorization(AuthorizationSetup.AddPrivilegePolicies);

        var app = builder.Build();

        app.UseMiddleware<ExceptionEnvelopeMiddleware>();
        app.UseWebSockets();
        app.UseAuthentication();
        app.UseMiddleware<MutationAuthenticationMiddleware>();
        app.UseAuthorization();

        app.MapGet("/api/websocket",
            (HttpContext context, WebSockets.WebSocketApiHandler handler) => handler.HandleAsync(context));

        AuthEndpoints.Map(app);
        InstanceEndpoints.Map(app);
        MdbEndpoints.Map(app);
        LinkEndpoints.Map(app);
        ArchiveEndpoints.Map(app);
        ProcessorEndpoints.Map(app);

        // Unmatched routes still answer in the documented envelope (L2-API-004).
        app.MapFallback(IResult () => throw new NotFoundServiceException("No such resource"));

        return app;
    }
}
