using Tranquility.Application;
using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure.Cfdp;

/// <summary>
/// Local bucket-rooted filestore under <c>&lt;DataDirectory&gt;/buckets</c>.
/// Commit writes to a temp file then atomically moves it into place.
/// </summary>
public sealed class LocalFilestore(TranquilityOptions options) : IFilestore
{
    private string Root => Path.Combine(
        options.DataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data"), "buckets");

    public byte[] Read(string bucket, string objectName)
    {
        var path = PathFor(bucket, objectName);
        if (!File.Exists(path))
        {
            throw new NotFoundServiceException($"Object '{bucket}/{objectName}' not found");
        }

        return File.ReadAllBytes(path);
    }

    public void Commit(string bucket, string objectName, byte[] content)
    {
        var path = PathFor(bucket, objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    public bool Exists(string bucket, string objectName) => File.Exists(PathFor(bucket, objectName));

    private string PathFor(string bucket, string objectName)
    {
        if (bucket.Contains("..", StringComparison.Ordinal) || objectName.Contains("..", StringComparison.Ordinal))
        {
            throw new BadRequestServiceException("bucket and objectName must not contain traversal segments");
        }

        return Path.Combine(Root, bucket, objectName);
    }
}
