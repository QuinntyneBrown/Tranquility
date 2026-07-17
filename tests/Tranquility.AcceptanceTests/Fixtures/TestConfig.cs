using Tranquility.Infrastructure.Security;

namespace Tranquility.AcceptanceTests.Fixtures;

/// <summary>
/// Shared seeded configuration for acceptance fixtures: one instance
/// (<c>sim</c>) and three principals with fixed test passwords. The seeded
/// role→privilege model is the production default from M1 on; M9 swaps the
/// backing store, never these semantics.
/// </summary>
public static class TestConfig
{
    public const string Instance = "sim";

    public const string AdminUser = "admin";
    public const string AdminPassword = "admin-test-password";
    public const string OperatorUser = "operator";
    public const string OperatorPassword = "operator-test-password";
    public const string ObserverUser = "observer";
    public const string ObserverPassword = "observer-test-password";

    /// <summary>Fixed HS256 signing key so tokens verify across fixtures.</summary>
    public const string SigningKey = "dGVzdC1zaWduaW5nLWtleS0zMi1ieXRlcy1sb25nISE=";

    public static Dictionary<string, string?> Settings() => new()
    {
        ["Tranquility:Instances:0:Name"] = Instance,
        ["Tranquility:Security:SigningKey"] = SigningKey,

        ["Tranquility:Security:Users:0:Username"] = AdminUser,
        ["Tranquility:Security:Users:0:PasswordHash"] = PasswordHasher.Hash(AdminPassword, iterations: 1_000),
        ["Tranquility:Security:Users:0:Superuser"] = "true",
        ["Tranquility:Security:Users:0:Roles:0"] = "Administrator",

        ["Tranquility:Security:Users:1:Username"] = OperatorUser,
        ["Tranquility:Security:Users:1:PasswordHash"] = PasswordHasher.Hash(OperatorPassword, iterations: 1_000),
        ["Tranquility:Security:Users:1:Roles:0"] = "Operator",

        ["Tranquility:Security:Users:2:Username"] = ObserverUser,
        ["Tranquility:Security:Users:2:PasswordHash"] = PasswordHasher.Hash(ObserverPassword, iterations: 1_000),
        ["Tranquility:Security:Users:2:Roles:0"] = "Observer",
    };
}
