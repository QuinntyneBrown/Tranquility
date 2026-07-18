using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Tranquility.Application.Abstractions;
using Tranquility.Server.Api;

namespace Tranquility.Server.Security;

public static class AuthorizationSetup
{
    /// <summary>One named policy per system privilege; superusers always pass.</summary>
    public static void AddPrivilegePolicies(AuthorizationOptions options)
    {
        foreach (var privilege in SystemPrivileges.All)
        {
            options.AddPolicy(privilege, policy => policy.RequireAssertion(context =>
                context.User.HasClaim(TokenService.SuperuserClaim, "true") ||
                context.User.HasClaim(TokenService.PrivilegeClaim, privilege)));
        }
    }
}

/// <summary>
/// Writes authorization failures in the documented exception envelope
/// (401 UnauthorizedException / 403 ForbiddenException) and records every
/// denial centrally (L2-SEC-004), so denial auditing cannot be forgotten
/// per-handler.
/// </summary>
public sealed class EnvelopeAuthorizationResultHandler(IAuditLog audit, TimeProvider time)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await Audit(context, "anonymous", "authn-required");
            await ApiResults.WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "UnauthorizedException", "Authentication required");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await Audit(context, context.User.Identity?.Name ?? "anonymous", "authz-denied");
            await ApiResults.WriteErrorAsync(context, StatusCodes.Status403Forbidden,
                "ForbiddenException", "Insufficient privileges");
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    private Task Audit(HttpContext context, string actor, string action) =>
        audit.AppendAsync(new AuditEntry(time.GetUtcNow(), actor, action,
            $"{context.Request.Method} {context.Request.Path}", "denied", null), context.RequestAborted);
}
