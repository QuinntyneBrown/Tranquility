namespace Tranquility.Server.Api;

// Error envelope per docs/specs/TRQ-ICD-API.md §3: non-2xx responses carry
// {"exception":{"type":"...","msg":"..."}}. Implements: L2-API-002.

public sealed record ExceptionDto(string Type, string Msg);

public sealed record ErrorEnvelope(ExceptionDto Exception);

public static class ApiResults
{
    public static IResult Error(int statusCode, string type, string msg) =>
        Results.Json(new ErrorEnvelope(new ExceptionDto(type, msg)), WireMapper.JsonOptions, statusCode: statusCode);

    public static IResult NotFound(string msg) => Error(StatusCodes.Status404NotFound, "NotFoundException", msg);

    public static IResult BadRequest(string msg) => Error(StatusCodes.Status400BadRequest, "BadRequestException", msg);

    public static IResult Unauthorized(string msg) => Error(StatusCodes.Status401Unauthorized, "UnauthorizedException", msg);

    public static IResult Json<T>(T value) => Results.Json(value, WireMapper.JsonOptions);
}

/// <summary>Maps unhandled exceptions to the documented error envelope (L2-API-002).</summary>
public sealed class ErrorEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorEnvelopeMiddleware> _logger;

    public ErrorEnvelopeMiddleware(RequestDelegate next, ILogger<ErrorEnvelopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new ErrorEnvelope(new ExceptionDto("InternalServerErrorException", ex.Message)),
                WireMapper.JsonOptions,
                cancellationToken: context.RequestAborted);
        }
    }
}
