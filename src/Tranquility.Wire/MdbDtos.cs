namespace Tranquility.Wire;

/// <summary>Documented MDB overview (L2-MDB-002).</summary>
public sealed record MdbOverview(
    string Version,
    int ParameterCount,
    int ParameterTypeCount,
    int ContainerCount,
    int CommandCount,
    int AlgorithmCount);

public sealed record SpaceSystemInfo(
    string Name,
    string QualifiedName,
    int ParameterCount,
    int ContainerCount,
    IReadOnlyList<SpaceSystemInfo> Children);

public sealed record SpaceSystemsResponse(IReadOnlyList<SpaceSystemInfo> SpaceSystems);

public sealed record AliasInfo(string Namespace, string Name);

public sealed record MdbParameterInfo(
    string Name,
    string QualifiedName,
    IReadOnlyList<AliasInfo> Aliases,
    MdbParameterTypeInfo Type);

public sealed record MdbParameterTypeInfo(string Name, string EngType);

public sealed record LoadMdbRequest(string? XtceRef);

/// <summary>One finding in a rejected-activation validation report (L2-MDB-001/004).</summary>
public sealed record ValidationFinding(string Severity, string Message, string? Construct, int? Line);
