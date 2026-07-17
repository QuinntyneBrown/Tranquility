using System.Globalization;
using Google.Protobuf;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Queries;
using Tranquility.Server.WebSockets;
using Tranquility.Wire.Proto;

namespace Tranquility.Server.Api;

/// <summary>Documented archive methods (L2-ARC-001..004).</summary>
public static class ArchiveEndpoints
{
    private static readonly JsonFormatter Formatter =
        new(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/archive/{instance}/parameters/{**name}",
            async (string instance, string name, HttpContext http, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var q = http.Request.Query;
                var values = await queries.Dispatch(new GetParameterHistoryQuery(
                    instance, name,
                    ParseTime(q["start"]), ParseTime(q["stop"]),
                    int.TryParse(q["limit"], out var limit) ? limit : 100,
                    string.Equals(q["order"], "desc", StringComparison.OrdinalIgnoreCase),
                    string.Equals(q["source"], "replay", StringComparison.OrdinalIgnoreCase)
                        ? HistorySource.Replay
                        : HistorySource.Archive), ct);
                return Results.Text(Formatter.Format(ToProto(values)), "application/json");
            });

        app.MapPost("/api/archive/{instance}:streamParameterValues",
            async (string instance, StreamParametersRequest request, HttpContext http, IArchive archive,
                Tranquility.Application.InstanceRegistry registry, CancellationToken ct) =>
            {
                var mdb = registry.Get(instance).RequireMdb();
                var names = (request.Ids ?? [])
                    .Select(i => (mdb.ResolveParameter(i.Name ?? "") ?? mdb.ResolveParameter($"/{i.Name}"))?.QualifiedName
                        ?? throw new NotFoundServiceException($"Parameter '{i.Name}' not found"))
                    .ToList();

                http.Response.ContentType = "application/x-ndjson";
                await foreach (var batch in archive.StreamParameterValuesAsync(
                    instance, names, ParseTime(request.Start), ParseTime(request.Stop), ct))
                {
                    await http.Response.WriteAsync(Formatter.Format(ToProto(batch)), ct);
                    await http.Response.WriteAsync("\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
            });

        app.MapGet("/api/parameter-archive/{instance}/pids",
            async (string instance, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var pids = await queries.Dispatch(new ListPidsQuery(instance), ct);
                return Results.Ok(new
                {
                    pids = pids.Select(p => new { pid = p.Pid, name = p.QualifiedName }).ToList(),
                });
            });

        app.MapGet("/api/parameter-archive/{instance}/pids/{pid:int}/segments",
            async (string instance, int pid, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var segments = await queries.Dispatch(new ListSegmentsQuery(instance, pid), ct);
                return Results.Ok(new
                {
                    segments = segments.Select(s => new
                    {
                        start = MicroTime.ToDateTimeOffset(s.StartUs),
                        end = MicroTime.ToDateTimeOffset(s.EndUs),
                        count = s.Count,
                    }).ToList(),
                });
            });
    }

    private static ParameterData ToProto(IReadOnlyList<ArchivedParameterValue> values)
    {
        var data = new ParameterData();
        foreach (var value in values)
        {
            data.Values.Add(ProtoMapper.ToProto(value));
        }

        return data;
    }

    private static long? ParseTime(string? text) =>
        string.IsNullOrEmpty(text)
            ? null
            : MicroTime.FromDateTimeOffset(DateTimeOffset.Parse(
                text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    public sealed record StreamParametersRequest(List<StreamId>? Ids, string? Start, string? Stop);

    public sealed record StreamId(string? Name);
}
