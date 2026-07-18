using Tranquility.Application.Abstractions;
using Tranquility.Application.Queries;

namespace Tranquility.Server.Api;

/// <summary>Documented audit query + integrity verification (L2-SEC-004).</summary>
public static class AuditEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit/records",
            async (HttpContext http, IQueryDispatcher queries, CancellationToken ct) =>
            {
                var records = await queries.Dispatch(new GetAuditRecordsQuery(http.Request.Query["service"]), ct);
                return Results.Ok(new
                {
                    records = records.Select(r => new
                    {
                        timestamp = r.Timestamp,
                        actor = r.Actor,
                        action = r.Action,
                        resource = r.Resource,
                        outcome = r.Outcome,
                        detail = r.Detail,
                    }).ToList(),
                });
            })
            .RequireAuthorization(SystemPrivileges.ReadAudit);

        app.MapPost("/api/audit:verify",
            async (IQueryDispatcher queries, CancellationToken ct) =>
            {
                var status = await queries.Dispatch(new VerifyAuditQuery(), ct);
                return Results.Ok(new { valid = status.Valid, count = status.Count, firstBrokenSeq = status.FirstBrokenSeq });
            })
            .RequireAuthorization(SystemPrivileges.ReadAudit);
    }
}
