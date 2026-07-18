using System.Security.Cryptography;

namespace Tranquility.DiffHarness;

/// <summary>One recorded input record: an offset-stamped byte payload for a stream.</summary>
public sealed record CorpusRecord(long OffsetNanos, byte StreamId, byte[] Bytes);

/// <summary>
/// A recorded input corpus with an integrity manifest. Replayed byte-identical
/// and in order to both systems (L2-DIF-001).
/// </summary>
public sealed class Corpus
{
    public Corpus(string corpusId, IReadOnlyList<CorpusRecord> records)
    {
        CorpusId = corpusId;
        Records = records;
        Sha256 = ComputeHash(records);
    }

    public string CorpusId { get; }

    public IReadOnlyList<CorpusRecord> Records { get; }

    /// <summary>Hex SHA-256 over the ordered record bytes (integrity manifest).</summary>
    public string Sha256 { get; }

    public int RecordCount => Records.Count;

    public static string ComputeHash(IReadOnlyList<CorpusRecord> records)
    {
        using var sha = SHA256.Create();
        foreach (var record in records)
        {
            sha.TransformBlock(record.Bytes, 0, record.Bytes.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(sha.Hash!);
    }
}

/// <summary>Attestation that both systems consumed the same bytes in the same order (L2-DIF-001).</summary>
public sealed record IngestAttestation(string CorpusId, string ExpectedSha256, string SutSha256, string ReferenceSha256)
{
    public bool BytesIdentical => ExpectedSha256 == SutSha256 && ExpectedSha256 == ReferenceSha256;
}
