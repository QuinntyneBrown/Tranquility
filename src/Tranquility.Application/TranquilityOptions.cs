namespace Tranquility.Application;

/// <summary>Bound from the <c>Tranquility</c> configuration section.</summary>
public sealed class TranquilityOptions
{
    public const string SectionName = "Tranquility";

    public List<InstanceOptions> Instances { get; init; } = [];

    /// <summary>Directory operator-supplied XTCE references resolve inside.</summary>
    public string? MdbDirectory { get; init; }

    public SecurityOptions Security { get; init; } = new();

    public WebSocketOptions WebSocket { get; init; } = new();
}

public sealed class WebSocketOptions
{
    /// <summary>
    /// Per-session outbound buffer capacity; overflow drops messages with an
    /// observable seq discontinuity (L2-RTS-004).
    /// </summary>
    public int SessionBufferSize { get; init; } = 4096;
}

public sealed class InstanceOptions
{
    public required string Name { get; init; }

    /// <summary>XTCE document activated for this instance at boot.</summary>
    public string? MdbPath { get; init; }

    public List<LinkOptions> Links { get; init; } = [];
}

public sealed class LinkOptions
{
    public required string Name { get; init; }

    /// <summary>Adapter type, e.g. <c>udp-packet</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Local port for socket links; 0 binds ephemerally.</summary>
    public int Port { get; init; }
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
