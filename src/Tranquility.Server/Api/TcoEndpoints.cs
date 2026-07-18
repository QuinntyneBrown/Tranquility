using System.Globalization;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Tco;
using Tranquility.Core.Time;

namespace Tranquility.Server.Api;

/// <summary>Documented time-correlation methods (L2-TIM-001/002/003).</summary>
public static class TcoEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tco/{instance}/{service}/status",
            (string instance, string service, TcoRegistry registry) =>
                Results.Ok(ToWire(registry.Get(instance, service).Status())));

        app.MapPost("/api/tco/{instance}/{service}/config",
            (string instance, string service, SetConfigRequest request, TcoRegistry registry) =>
            {
                registry.Get(instance, service).SetConfig(new TcoConfig(request.Accuracy, request.Validity));
                return Results.Ok(ToWire(registry.Get(instance, service).Status()));
            })
            .RequireAuthorization(SystemPrivileges.ControlTimeCorrelation);

        app.MapPost("/api/tco/{instance}/{service}/coefficients",
            (string instance, string service, SetCoefficientsRequest request, TcoRegistry registry) =>
            {
                registry.Get(instance, service).SetCoefficients(
                    new TcoCoefficients(request.Gradient, request.Offset, request.ObtEpoch));
                return Results.Ok(ToWire(registry.Get(instance, service).Status()));
            })
            .RequireAuthorization(SystemPrivileges.ControlTimeCorrelation);

        app.MapPost("/api/tco/{instance}/{service}/tof/intervals",
            (string instance, string service, TofIntervalRequest request, TcoRegistry registry) =>
            {
                registry.Get(instance, service).AddInterval(new TofInterval(
                    ParseTime(request.ErtStart), ParseTime(request.ErtStop), request.DelaySeconds));
                return Results.Ok(ToWire(registry.Get(instance, service).Status()));
            })
            .RequireAuthorization(SystemPrivileges.ControlTimeCorrelation);

        app.MapDelete("/api/tco/{instance}/{service}/tof/intervals",
            (string instance, string service, HttpContext http, TcoRegistry registry) =>
            {
                var ertStart = http.Request.Query["ertStart"].ToString();
                if (string.IsNullOrEmpty(ertStart))
                {
                    throw new BadRequestServiceException("ertStart query parameter is required");
                }

                if (!registry.Get(instance, service).RemoveInterval(ParseTime(ertStart)))
                {
                    throw new NotFoundServiceException($"No TOF interval starting at {ertStart}");
                }

                return Results.Ok(ToWire(registry.Get(instance, service).Status()));
            })
            .RequireAuthorization(SystemPrivileges.ControlTimeCorrelation);
    }

    private static object ToWire(TcoServiceStatus status) => new
    {
        coefficients = status.Coefficients is { } c
            ? new { gradient = c.Gradient, offset = c.Offset, obtEpoch = c.ObtEpochUs }
            : null,
        deviation = status.Deviation,
        sampleCount = status.SampleCount,
        config = new { accuracy = status.Config.AccuracyUs, validity = status.Config.ValidityUs },
        tofIntervals = status.TofIntervals.Select(i => new
        {
            ertStart = MicroTime.ToDateTimeOffset(i.ErtStartUs),
            ertStop = MicroTime.ToDateTimeOffset(i.ErtStopUs),
            delaySeconds = i.DelaySeconds,
        }).ToList(),
    };

    private static long ParseTime(string text) =>
        MicroTime.FromDateTimeOffset(DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    public sealed record SetConfigRequest(double Accuracy, double Validity);

    public sealed record SetCoefficientsRequest(double Gradient, double Offset, long ObtEpoch);

    public sealed record TofIntervalRequest(string ErtStart, string ErtStop, double DelaySeconds);
}
