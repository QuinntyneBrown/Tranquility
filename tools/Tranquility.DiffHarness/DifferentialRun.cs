using System.Net.Sockets;

namespace Tranquility.DiffHarness;

/// <summary>A UDP + HTTP endpoint of a system under differential test.</summary>
public sealed record SystemEndpoint(string BaseUrl, string Host, int UdpPort);

/// <summary>Provides the observed JSON per surface for one system (external observation only).</summary>
public interface ISurfaceObserver
{
    Task<string> ObserveAsync(CompareSurface surface, CancellationToken cancellationToken);

    /// <summary>The SHA-256 of the corpus bytes this system received (ingest attestation).</summary>
    Task<string> IngestDigestAsync(CancellationToken cancellationToken);
}

/// <summary>The report produced by a differential run (L2-DIF-002/003).</summary>
public sealed record DifferentialReport(
    IngestAttestation Ingest,
    IReadOnlyList<SurfaceResult> Surfaces)
{
    public bool AllEquivalent => Surfaces.All(s => s.Equivalent);

    public bool GatePassed => Ingest.BytesIdentical
        && Surfaces.All(s => s.Equivalent || (s.Triage is not null and not TriageClass.Unclassified
            and not TriageClass.TranquilityDefect));
}

/// <summary>
/// Orchestrates a differential run: replays the corpus to both systems byte-
/// identically (L2-DIF-001), observes every surface, compares (L2-DIF-002),
/// and classifies divergences (L2-DIF-003). External observation only
/// (L2-DIF-004): drives both systems through documented UDP/HTTP interfaces.
/// </summary>
public sealed class DifferentialRun(TriageClassifier classifier)
{
    /// <summary>Replays the corpus to a UDP endpoint byte-identically in order.</summary>
    public static async Task ReplayAsync(Corpus corpus, SystemEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        foreach (var record in corpus.Records)
        {
            await udp.SendAsync(record.Bytes, endpoint.Host, endpoint.UdpPort, cancellationToken);
        }
    }

    public async Task<DifferentialReport> RunAsync(
        Corpus corpus, ISurfaceObserver sut, ISurfaceObserver reference,
        IReadOnlyList<CompareSurface> surfaces, CancellationToken cancellationToken)
    {
        var attestation = new IngestAttestation(
            corpus.CorpusId, corpus.Sha256,
            await sut.IngestDigestAsync(cancellationToken),
            await reference.IngestDigestAsync(cancellationToken));

        var results = new List<SurfaceResult>();
        foreach (var surface in surfaces)
        {
            var sutJson = await sut.ObserveAsync(surface, cancellationToken);
            var refJson = await reference.ObserveAsync(surface, cancellationToken);
            var result = SurfaceComparator.Compare(surface, sutJson, refJson);
            if (!result.Equivalent)
            {
                result = result with { Triage = classifier.Classify(result) };
            }

            results.Add(result);
        }

        return new DifferentialReport(attestation, results);
    }
}
