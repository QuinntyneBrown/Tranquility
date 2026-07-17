using Tranquility.Core.Mdb;

namespace Tranquility.Application.Abstractions;

/// <summary>
/// Mission database loading port (L1-MDB-001). The Infrastructure XTCE loader
/// implements it; references are resolved inside the configured MDB directory.
/// </summary>
public interface IMdbLoader
{
    /// <summary>Loads a trusted absolute path (boot-time configuration).</summary>
    MdbLoadResult LoadPath(string absolutePath);

    /// <summary>
    /// Loads an operator-supplied reference, constrained to the configured MDB
    /// directory (no rooted paths, no traversal).
    /// </summary>
    MdbLoadResult LoadReference(string xtceRef);
}

/// <summary>
/// Activation failure carrying the exhaustive validation report
/// (L2-MDB-001/L2-MDB-004): serialized as the documented envelope plus a
/// <c>validationReport</c> array.
/// </summary>
public sealed class ValidationServiceException(string message, IReadOnlyList<XtceDiagnostic> diagnostics)
    : ServiceException(message)
{
    public override int StatusCode => 422;

    public override string WireType => "ValidationException";

    public IReadOnlyList<XtceDiagnostic> Diagnostics { get; } = diagnostics;
}
