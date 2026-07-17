using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Tranquility.Application;
using Tranquility.Application.Abstractions;

namespace Tranquility.Server.Security;

/// <summary>
/// Issues and validates HS256 bearer tokens. The signing key comes from
/// configuration; when absent an ephemeral key is generated at startup
/// (tokens then do not survive restarts).
/// </summary>
public sealed class TokenService
{
    public const string PrivilegeClaim = "privilege";
    public const string SuperuserClaim = "superuser";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    private readonly SymmetricSecurityKey _key;
    private readonly TimeProvider _time;

    public TokenService(TranquilityOptions options, TimeProvider time)
    {
        _time = time;
        var keyBytes = options.Security.SigningKey is { Length: > 0 } configured
            ? Convert.FromBase64String(configured)
            : RandomNumberGenerator.GetBytes(32);
        _key = new SymmetricSecurityKey(keyBytes);
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _key,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.FromSeconds(30),
    };

    public (string Token, int ExpiresInSeconds) Issue(AuthenticatedUser user)
    {
        var now = _time.GetUtcNow();
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Username) };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(user.Privileges.Select(p => new Claim(PrivilegeClaim, p)));
        if (user.Superuser)
        {
            claims.Add(new Claim(SuperuserClaim, "true"));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.Add(TokenLifetime).UtcDateTime,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), (int)TokenLifetime.TotalSeconds);
    }
}
