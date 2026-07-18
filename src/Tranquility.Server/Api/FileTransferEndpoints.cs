using Tranquility.Application.Abstractions;
using Tranquility.Application.Cfdp;

namespace Tranquility.Server.Api;

/// <summary>Documented CFDP file transfer methods (L2-FDP-001/002/003).</summary>
public static class FileTransferEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/filetransfer/{instance}/{service}/transfers",
            (string instance, string service, TransferRegistry registry) =>
                Results.Ok(new { transfers = registry.Get(instance, service).Transfers.Select(ToWire).ToList() }));

        app.MapGet("/api/filetransfer/{instance}/{service}/transfers/{id}",
            (string instance, string service, string id, TransferRegistry registry) =>
            {
                var record = registry.Get(instance, service).Find(id)
                    ?? throw new NotFoundServiceException($"Transfer '{id}' not found");
                return Results.Ok(ToWire(record));
            });

        app.MapPost("/api/filetransfer/{instance}/{service}/transfers",
            (string instance, string service, CreateTransferRequest request, TransferRegistry registry, IFilestore filestore) =>
            {
                if (string.IsNullOrWhiteSpace(request.ObjectName) || string.IsNullOrWhiteSpace(request.RemotePath))
                {
                    throw new BadRequestServiceException("objectName and remotePath are required");
                }

                var content = string.IsNullOrEmpty(request.Content)
                    ? []
                    : Convert.FromBase64String(request.Content);

                // For uplinks the source object is taken from the request content
                // (or the filestore if already staged).
                var id = Guid.NewGuid().ToString("N");
                var record = registry.Get(instance, service).Create(
                    id, request.Direction ?? "UPLOAD", request.Bucket ?? "default",
                    request.ObjectName, request.RemotePath, content,
                    request.Reliable ?? true, request.Paused ?? false);
                return Results.Ok(ToWire(record));
            })
            .RequireAuthorization(SystemPrivileges.ControlFileTransfers);

        app.MapPost("/api/filetransfer/{instance}/{service}/transfers/{id}:pause",
            (string instance, string service, string id, TransferRegistry registry) =>
            {
                registry.Get(instance, service).Pause(id);
                return Results.Ok();
            })
            .RequireAuthorization(SystemPrivileges.ControlFileTransfers);

        app.MapPost("/api/filetransfer/{instance}/{service}/transfers/{id}:resume",
            (string instance, string service, string id, TransferRegistry registry) =>
            {
                registry.Get(instance, service).Resume(id);
                return Results.Ok();
            })
            .RequireAuthorization(SystemPrivileges.ControlFileTransfers);

        app.MapPost("/api/filetransfer/{instance}/{service}/transfers/{id}:cancel",
            (string instance, string service, string id, TransferRegistry registry) =>
            {
                registry.Get(instance, service).Cancel(id);
                return Results.Ok();
            })
            .RequireAuthorization(SystemPrivileges.ControlFileTransfers);
    }

    internal static object ToWire(TransferRecord r) => new
    {
        id = r.Id,
        startTime = r.StartTime,
        creationTime = r.StartTime,
        state = r.State.ToString().ToUpperInvariant(),
        bucket = r.Bucket,
        objectName = r.ObjectName,
        remotePath = r.RemotePath,
        direction = r.Direction,
        totalSize = r.TotalSize,
        sizeTransferred = Interlocked.Read(ref r.SizeTransferred),
        reliable = r.Reliable,
        transferType = "CFDP",
        failureReason = r.FailureReason,
    };

    public sealed record CreateTransferRequest(
        string? Direction, string? Bucket, string? ObjectName, string? RemotePath,
        string? Content, bool? Reliable, bool? Paused);
}
