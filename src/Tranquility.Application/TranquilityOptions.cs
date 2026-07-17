namespace Tranquility.Application;

/// <summary>Bound from the <c>Tranquility</c> configuration section.</summary>
public sealed class TranquilityOptions
{
    public const string SectionName = "Tranquility";

    public List<InstanceOptions> Instances { get; init; } = [];

    public SecurityOptions Security { get; init; } = new();
}

public sealed class InstanceOptions
{
    public required string Name { get; init; }
}

public sealed class SecurityOptions
{
    /// <summary>Base64 HS256 signing key; generated at startup when absent.</summary>
    public string? SigningKey { get; init; }

    public List<SeededUserOptions> Users { get; init; } = [];
}

/// <summary>
/// Configuration-seeded principal (M1). Replaced as the backing store by the
/// SQLite IAM database in M9 without changing authentication semantics.
/// </summary>
public sealed class SeededUserOptions
{
    public required string Username { get; init; }

    public required string PasswordHash { get; init; }

    public bool Superuser { get; init; }

    public List<string> Roles { get; init; } = [];
}
