using Tranquility.Application.Abstractions;
using Tranquility.Server.Api;

namespace Tranquility.Server.Middleware;

/// <summary>
/// Maps every failure onto the documented exception envelope (L2-API-004):
/// typed <see cref="ServiceException"/>s carry their wire type and status;
/// anything else becomes a 500 InternalServerErrorException.
/// </summary>
public sealed class ExceptionEnvelopeMiddleware(RequestDelegate next, ILogger<ExceptionEnvelopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationServiceException exception) when (!context.Response.HasStarted)
        {
            // Envelope plus the exhaustive validation report (L2-MDB-001/004).
            context.Response.StatusCode = exception.StatusCode;
            context.Response.ContentType = "application/json";
            await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, new
            {
                exception = new Wire.ExceptionInfo(exception.WireType, exception.Message),
                validationReport = exception.Diagnostics
                    .Select(d => new Wire.ValidationFinding(
                        d.Severity.ToString().ToUpperInvariant(), d.Message, d.Construct, d.Line))
                    .ToList(),
            }, Wire.Json.WireJson.Options, context.RequestAborted);
        }
        catch (ServiceException exception) when (!context.Response.HasStarted)
        {
            await ApiResults.WriteErrorAsync(context, exception.StatusCode, exception.WireType, exception.Message);
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            await ApiResults.WriteErrorAsync(context, exception.StatusCode, "BadRequestException", exception.Message);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception, "Unhandled API exception");
            await ApiResults.WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "InternalServerErrorException", "An internal server error occurred");
        }
    }
}

/// <summary>
/// Rejects unauthenticated state-changing requests (L2-SEC-002) before they
/// reach any endpoint, and records the denial through the audit port.
/// </summary>
public sealed class MutationAuthenticationMiddleware(
    RequestDelegate next,
    IAuditLog audit,
    TimeProvider time)
{
    /// <summary>Routes that are anonymous by documented design.</summary>
    private static readonly string[] AnonymousByDesign = ["/auth/token"];

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var isMutation = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
        if (isMutation &&
            !AnonymousByDesign.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase) &&
            context.User.Identity?.IsAuthenticated != true)
        {
            await audit.AppendAsync(new Application.Abstractions.AuditEntry(
                time.GetUtcNow(), "anonymous", "auth-reject",
                $"{method} {context.Request.Path}", "rejected", null), context.RequestAborted);
            await ApiResults.WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "UnauthorizedException", "Authentication required for state-changing operations");
            return;
        }

        await next(context);
    }
}
