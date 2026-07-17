using Tranquility.Application.Abstractions;
using Tranquility.Server.Security;
using Tranquility.Wire;

namespace Tranquility.Server.Api;

/// <summary>Documented bearer token issue (POST /auth/token).</summary>
public static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", async (
            TokenRequest request,
            IIdentityStore identity,
            TokenService tokens,
            IAuditLog audit,
            TimeProvider time,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                throw new BadRequestServiceException("username and password are required");
            }

            var user = await identity.AuthenticateAsync(request.Username, request.Password, ct);
            if (user is null)
            {
                await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), request.Username,
                    "token-issue", "/auth/token", "denied", "invalid credentials"), ct);
                throw new UnauthorizedServiceException("Invalid username or password");
            }

            var (token, expiresIn) = tokens.Issue(user);
            await audit.AppendAsync(new AuditEntry(time.GetUtcNow(), user.Username,
                "token-issue", "/auth/token", "success", null), ct);
            return Results.Ok(new TokenResponse(token, "bearer", expiresIn));
        }).AllowAnonymous();
    }
}
