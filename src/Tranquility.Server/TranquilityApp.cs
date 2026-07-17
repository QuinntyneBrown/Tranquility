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
        builder.Services.AddSingleton<InstanceRegistry>();
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
        app.UseAuthentication();
        app.UseMiddleware<MutationAuthenticationMiddleware>();
        app.UseAuthorization();

        AuthEndpoints.Map(app);
        InstanceEndpoints.Map(app);
        MdbEndpoints.Map(app);

        // Unmatched routes still answer in the documented envelope (L2-API-004).
        app.MapFallback(IResult () => throw new NotFoundServiceException("No such resource"));

        return app;
    }
}
