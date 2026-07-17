using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Tranquility.AcceptanceTests.Traceability;

/// <summary>
/// Meta-tests making requirement traceability itself ATDD-driven: every L2
/// requirement is either covered by a traced acceptance test or explicitly
/// listed in the open coverage baseline, and coverage can only move forward.
/// </summary>
public sealed class RequirementCoverageTests
{
    private static string BaselinePath =>
        Path.Combine(RepoPaths.Root, "tests", "Tranquility.AcceptanceTests", "Traceability", "CoverageBaseline.json");

    /// <summary>IDs carried by at least one [Requirement]-annotated test in this assembly.</summary>
    private static IReadOnlySet<string> TestedIds { get; } = CollectTestedIds();

    private static IReadOnlySet<string> OpenIds { get; } = ReadOpenBaseline();

    [Fact]
    public void Every_L2_requirement_is_tested_or_listed_open()
    {
        var unaccounted = SpecCatalog.L2Ids.Except(TestedIds).Except(OpenIds).ToList();
        Assert.True(unaccounted.Count == 0,
            "L2 requirements with neither a traced acceptance test nor an open-baseline entry: " +
            string.Join(", ", unaccounted));
    }

    [Fact]
    public void No_open_baseline_entry_has_a_test()
    {
        // The ratchet: once an ID gains a test its baseline entry must be
        // removed in the same commit, so coverage can never silently regress.
        var stale = OpenIds.Intersect(TestedIds).Order().ToList();
        Assert.True(stale.Count == 0,
            "IDs listed open in CoverageBaseline.json but already covered by tests " +
            "(remove them from the baseline): " + string.Join(", ", stale));
    }

    [Fact]
    public void Every_requirement_annotation_refers_to_a_real_requirement_id()
    {
        var phantom = TestedIds.Except(SpecCatalog.AllIds).Order().ToList();
        Assert.True(phantom.Count == 0,
            "[Requirement] annotations referencing IDs absent from docs/specs: " +
            string.Join(", ", phantom));
    }

    [Fact]
    public void Final_baseline_is_empty()
    {
        // Armed at M11 by deleting CoverageBaseline.json: from then on, every
        // L2 requirement must carry at least one traced acceptance test.
        if (File.Exists(BaselinePath))
        {
            return; // gate not yet armed
        }

        var untested = SpecCatalog.L2Ids.Except(TestedIds).ToList();
        Assert.True(untested.Count == 0,
            "Final gate: L2 requirements without a traced acceptance test: " +
            string.Join(", ", untested));
    }

    private static IReadOnlySet<string> CollectTestedIds()
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(RequirementCoverageTests).Assembly.GetTypes())
        {
            var typeLevel = type.GetCustomAttributes<RequirementAttribute>(inherit: true)
                .Select(a => a.Id)
                .ToList();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.GetCustomAttributes<FactAttribute>(inherit: true).Any())
                {
                    ids.UnionWith(typeLevel);
                    ids.UnionWith(method.GetCustomAttributes<RequirementAttribute>(inherit: true).Select(a => a.Id));
                }
            }
        }

        return ids;
    }

    private static IReadOnlySet<string> ReadOpenBaseline()
    {
        if (!File.Exists(BaselinePath))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(BaselinePath));
        return doc.RootElement.GetProperty("open").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
