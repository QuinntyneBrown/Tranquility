namespace Tranquility.Wire;

/// <summary>Documented link resource (L2-LNK-001, L2-LNK-004).</summary>
public sealed record LinkInfo(
    string Name,
    string Type,
    string Status,
    bool Disabled,
    long DataInCount,
    long DataOutCount,
    string DetailedStatus,
    int? BoundPort,
    IReadOnlyList<LinkActionInfo> Actions);

public sealed record LinkActionInfo(string Id, string Label, bool Enabled);

public sealed record ListLinksResponse(IReadOnlyList<LinkInfo> Links);
