using Tranquility.Application;
using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure.Security;

/// <summary>
/// Configuration-seeded identity store (M1). The role→privilege model defined
/// here is the production default; M9 swaps the backing store to the SQLite
/// IAM database behind the same <see cref="IIdentityStore"/> contract.
/// </summary>
public sealed class ConfigIdentityStore(TranquilityOptions options) : IIdentityStore
{
    /// <summary>Built-in role definitions until IAM CRUD (L2-SEC-001) manages them.</summary>
    private static readonly Dictionary<string, string[]> RolePrivileges = new(StringComparer.Ordinal)
    {
        ["Administrator"] = [.. SystemPrivileges.All],
        ["Operator"] =
        [
            SystemPrivileges.ControlProcessor,
            SystemPrivileges.ControlLinks,
            SystemPrivileges.CommandIssue,
            SystemPrivileges.CommandQueueControl,
            SystemPrivileges.ControlFileTransfers,
        ],
        ["Observer"] = [],
    };

    public Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = options.Security.Users
            .FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.Ordinal));
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        var privileges = user.Roles
            .SelectMany(r => RolePrivileges.TryGetValue(r, out var p) ? p : [])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<AuthenticatedUser?>(
            new AuthenticatedUser(user.Username, user.Roles, privileges, user.Superuser));
    }
}
