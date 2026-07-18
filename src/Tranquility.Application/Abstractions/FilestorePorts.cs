namespace Tranquility.Application.Abstractions;

/// <summary>
/// Bucket-rooted filestore for CFDP (L1-FDP-001). File I/O lives here so the
/// CFDP engines stay pure; the local adapter commits atomically.
/// </summary>
public interface IFilestore
{
    byte[] Read(string bucket, string objectName);

    /// <summary>Writes to a temp object and atomically commits it.</summary>
    void Commit(string bucket, string objectName, byte[] content);

    bool Exists(string bucket, string objectName);
}
