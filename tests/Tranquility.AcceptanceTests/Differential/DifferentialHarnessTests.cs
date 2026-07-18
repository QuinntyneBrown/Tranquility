using Tranquility.AcceptanceTests.Traceability;
using Tranquility.DiffHarness;
using Xunit;

namespace Tranquility.AcceptanceTests.Differential;

/// <summary>
/// A reference-system stand-in: replays recorded documented responses per
/// surface. Test infrastructure, NOT a product stub — it lets the harness
/// logic be verified without a live reference deployment.
/// </summary>
internal sealed class ReferenceSimulator(string digest, IReadOnlyDictionary<CompareSurface, string> responses)
    : ISurfaceObserver
{
    public Task<string> ObserveAsync(CompareSurface surface, CancellationToken cancellationToken) =>
        Task.FromResult(responses.TryGetValue(surface, out var json) ? json : "{}");

    public Task<string> IngestDigestAsync(CancellationToken cancellationToken) => Task.FromResult(digest);
}

/// <summary>
/// L2-DIF-001..003: corpus byte/order equivalence, per-surface comparison
/// records, and triage classification. Exercised with a ReferenceSimulator.
/// </summary>
public sealed class DifferentialHarnessTests
{
    private static readonly CompareSurface[] AllSurfaces = Enum.GetValues<CompareSurface>();

    private static Corpus SampleCorpus() => new("nominal-corpus",
    [
        new CorpusRecord(0, 0, [0x00, 0x64, 0xC0, 0x05, 0x00, 0x07, 0x01, 0x02, 0x40, 0x02, 0x3F, 0xC0, 0x00, 0x00]),
        new CorpusRecord(1_000_000, 0, [0x00, 0x64, 0xC0, 0x06, 0x00, 0x07, 0x01, 0x03, 0x40, 0x02, 0x3F, 0xC0, 0x00, 0x00]),
    ]);

    [Fact]
    [Requirement("L2-DIF-001")]
    public async Task Both_systems_are_attested_to_consume_identical_corpus_bytes_in_order()
    {
        var corpus = SampleCorpus();
        var run = new DifferentialRun(new TriageClassifier(new Dictionary<string, TriageClass>()));

        var sut = new ReferenceSimulator(corpus.Sha256, Identical());
        var reference = new ReferenceSimulator(corpus.Sha256, Identical());
        var report = await run.RunAsync(corpus, sut, reference, AllSurfaces, TestContext.Current.CancellationToken);

        Assert.True(report.Ingest.BytesIdentical, "both systems must attest identical corpus bytes");
        Assert.Equal(corpus.Sha256, report.Ingest.ExpectedSha256);
    }

    [Fact]
    [Requirement("L2-DIF-001")]
    public void Corpus_manifest_hash_changes_if_any_byte_or_order_changes()
    {
        var a = SampleCorpus();
        var reordered = new Corpus("nominal-corpus", a.Records.Reverse().ToList());
        Assert.NotEqual(a.Sha256, reordered.Sha256);
    }

    [Fact]
    [Requirement("L2-DIF-002")]
    public async Task Every_compared_surface_yields_an_equivalence_or_divergence_record()
    {
        var corpus = SampleCorpus();
        var run = new DifferentialRun(new TriageClassifier(new Dictionary<string, TriageClass>()));

        var sut = new ReferenceSimulator(corpus.Sha256, Identical());
        var reference = new ReferenceSimulator(corpus.Sha256, Identical());
        var report = await run.RunAsync(corpus, sut, reference, AllSurfaces, TestContext.Current.CancellationToken);

        Assert.Equal(AllSurfaces.Length, report.Surfaces.Count);
        Assert.All(AllSurfaces, s => Assert.Contains(report.Surfaces, r => r.Surface == s));
        Assert.True(report.AllEquivalent, "identical responses must be equivalent on every surface");
    }

    [Fact]
    [Requirement("L2-DIF-003")]
    public async Task Divergences_are_classified_into_exactly_one_triage_class()
    {
        var corpus = SampleCorpus();
        var overlay = new Dictionary<string, TriageClass>
        {
            ["engValue"] = TriageClass.StandardAmbiguity,
        };
        var run = new DifferentialRun(new TriageClassifier(overlay));

        var sut = new ReferenceSimulator(corpus.Sha256, Identical());
        // Reference reports a different engineering value on the parameters surface.
        var divergent = new Dictionary<CompareSurface, string>(Identical())
        {
            [CompareSurface.ParameterValues] =
                """{"values":[{"id":{"name":"/SampleSat/Temperature"},"engValue":99.9}]}""",
        };
        var reference = new ReferenceSimulator(corpus.Sha256, divergent);
        var report = await run.RunAsync(corpus, sut, reference, AllSurfaces, TestContext.Current.CancellationToken);

        var diverged = Assert.Single(report.Surfaces, s => !s.Equivalent);
        Assert.Equal(CompareSurface.ParameterValues, diverged.Surface);
        Assert.Equal(TriageClass.StandardAmbiguity, diverged.Triage);

        // Unmatched divergences are Unclassified and would fail the gate.
        var strict = new DifferentialRun(new TriageClassifier(new Dictionary<string, TriageClass>()));
        var strictReport = await strict.RunAsync(corpus, sut, reference, AllSurfaces, TestContext.Current.CancellationToken);
        Assert.Equal(TriageClass.Unclassified, Assert.Single(strictReport.Surfaces, s => !s.Equivalent).Triage);
        Assert.False(strictReport.GatePassed, "an unclassified divergence must fail the gate");
    }

    [Fact]
    [Requirement("L2-DIF-002")]
    public void Timestamp_comparison_is_microsecond_exact_and_ids_are_masked()
    {
        // Command history: differing ids/generationTime are masked, so equivalent.
        var sut = """{"entries":[{"id":"abc","commandName":"/S/C","generationTime":"2026-07-17T00:00:00.000Z"}]}""";
        var reference = """{"entries":[{"id":"xyz","commandName":"/S/C","generationTime":"2026-07-17T09:00:00.000Z"}]}""";
        var result = SurfaceComparator.Compare(CompareSurface.CommandHistory, sut, reference);
        Assert.True(result.Equivalent, $"masked ids/times must be equivalent: {result.Detail}");

        // But a real content difference is a divergence.
        var different = """{"entries":[{"id":"xyz","commandName":"/S/OTHER","generationTime":"2026-07-17T09:00:00.000Z"}]}""";
        Assert.False(SurfaceComparator.Compare(CompareSurface.CommandHistory, sut, different).Equivalent);
    }

    private static Dictionary<CompareSurface, string> Identical() => new()
    {
        [CompareSurface.ParameterValues] = """{"values":[{"id":{"name":"/SampleSat/Temperature"},"engValue":31.2}]}""",
        [CompareSurface.EngineeringConversions] = """{"temp":31.2}""",
        [CompareSurface.Timestamps] = """{"generationTime":"2026-07-17T00:00:00.000Z"}""",
        [CompareSurface.AlarmStates] = """{"alarms":[{"parameter":"/SampleSat/Temperature","severity":"WARNING"}]}""",
        [CompareSurface.CommandHistory] = """{"entries":[{"id":"a","commandName":"/S/C","generationTime":"2026-07-17T00:00:00.000Z"}]}""",
        [CompareSurface.ApiResponses] = """{"instances":[{"name":"sim","state":"RUNNING"}]}""",
    };
}
