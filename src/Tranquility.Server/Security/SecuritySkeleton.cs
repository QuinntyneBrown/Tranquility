using Tranquility.Server.Api;

namespace Tranquility.Server.Security;

// Security skeleton for the vertical slice.
// Implements hooks for: L2-SEC-001 (authentication), L2-SEC-002 (authorization
// on privileged paths), L2-SEC-004 (audit trail — see IAuditLog usage in
// Application command handlers). Full authN/authZ is a later phase.

public sealed class SecurityOptions
{
    public const string SectionName = "Tranquility:Security";

    /// <summary>
    /// When true, privileged endpoints reject unauthenticated callers.
    /// Default false until an authentication scheme is configured (skeleton).
    /// </summary>
    public bool RequireAuthentication { get; set; }
}

/// <summary>
/// Endpoint filter marking a privileged path (link control now; command uplink
/// later). Rejects unauthenticated callers when security is enabled.
/// </summary>
public sealed class PrivilegedEndpointFilter : IEndpointFilter
{
    private readonly SecurityOptions _options;

    public PrivilegedEndpointFilter(SecurityOptions options)
    {
        _options = options;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (_options.RequireAuthentication &&
            context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return ApiResults.Unauthorized("Authentication is required for this operation.");
        }

        return await next(context);
    }
}
