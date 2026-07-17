using Tranquility.Application;
using Tranquility.Application.Abstractions;
using Tranquility.Core.Mdb;

namespace Tranquility.Infrastructure.Xtce;

/// <summary>
/// File-system <see cref="IMdbLoader"/>: boot paths are trusted configuration;
/// operator references are confined to the configured MDB directory.
/// </summary>
public sealed class XtceFileMdbLoader(TranquilityOptions options) : IMdbLoader
{
    public MdbLoadResult LoadPath(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return new MdbLoadResult(null,
                [new XtceDiagnostic(XtceDiagnosticSeverity.Error, $"XTCE document '{absolutePath}' not found", null, null)]);
        }

        return new XtceLoader(absolutePath).LoadWithDiagnostics();
    }

    public MdbLoadResult LoadReference(string xtceRef)
    {
        var directory = options.MdbDirectory
            ?? throw new BadRequestServiceException("No MDB directory is configured for reference-based loads");
        if (Path.IsPathRooted(xtceRef) || xtceRef.Contains("..", StringComparison.Ordinal))
        {
            throw new BadRequestServiceException("xtceRef must be a plain file name inside the MDB directory");
        }

        var path = Path.Combine(directory, xtceRef);
        if (!File.Exists(path))
        {
            throw new NotFoundServiceException($"XTCE document '{xtceRef}' not found in the MDB directory");
        }

        return new XtceLoader(path).LoadWithDiagnostics();
    }
}
