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
/// (401 UnauthorizedException / 403 ForbiddenException) instead of the
/// framework's empty challenge responses.
/// </summary>
public sealed class EnvelopeAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            return ApiResults.WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "UnauthorizedException", "Authentication required");
        }

        if (authorizeResult.Forbidden)
        {
            return ApiResults.WriteErrorAsync(context, StatusCodes.Status403Forbidden,
                "ForbiddenException", "Insufficient privileges");
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
