namespace Tranquility.Core.Mdb;

public enum XtceDiagnosticSeverity
{
    Warning,
    Error,
}

/// <summary>
/// One finding from XTCE validation: a broken reference (L2-MDB-001) or an
/// unsupported construct outside the approved support matrix (L2-MDB-004),
/// with document location.
/// </summary>
public sealed record XtceDiagnostic(
    XtceDiagnosticSeverity Severity,
    string Message,
    string? Construct,
    int? Line);

/// <summary>
/// Outcome of an XTCE load: an activatable model, or the exhaustive
/// diagnostic report that rejected it.
/// </summary>
public sealed record MdbLoadResult(
    MissionDatabase? Database,
    IReadOnlyList<XtceDiagnostic> Diagnostics)
{
    public bool Success => Database is not null;

    public IEnumerable<XtceDiagnostic> Errors =>
        Diagnostics.Where(d => d.Severity == XtceDiagnosticSeverity.Error);
}
