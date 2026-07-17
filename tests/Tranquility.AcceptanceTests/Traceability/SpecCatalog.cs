using System.Text.RegularExpressions;

namespace Tranquility.AcceptanceTests.Traceability;

/// <summary>
/// Parses <c>docs/specs/*/L1.md</c> and <c>L2.md</c> into the authoritative
/// requirement-ID sets. The markdown specs remain the single source of truth;
/// nothing here is hardcoded.
/// </summary>
public static partial class SpecCatalog
{
    [GeneratedRegex(@"^##\s+(L[12]-[A-Z]{3}-\d{3})\s*$", RegexOptions.Multiline)]
    private static partial Regex RequirementHeading();

    private static readonly Lazy<(IReadOnlySet<string> L1, IReadOnlySet<string> L2)> Ids =
        new(Parse);

    /// <summary>All L2 requirement IDs in the baseline (the ATDD coverage target).</summary>
    public static IReadOnlySet<string> L2Ids => Ids.Value.L2;

    /// <summary>All L1 and L2 requirement IDs (valid targets for [Requirement]).</summary>
    public static IReadOnlySet<string> AllIds =>
        Ids.Value.L1.Concat(Ids.Value.L2).ToHashSet();

    private static (IReadOnlySet<string>, IReadOnlySet<string>) Parse()
    {
        var l1 = new SortedSet<string>(StringComparer.Ordinal);
        var l2 = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(RepoPaths.SpecsDirectory, "L*.md", SearchOption.AllDirectories))
        {
            foreach (Match match in RequirementHeading().Matches(File.ReadAllText(file)))
            {
                var id = match.Groups[1].Value;
                (id.StartsWith("L1-", StringComparison.Ordinal) ? l1 : l2).Add(id);
            }
        }

        if (l2.Count == 0)
        {
            throw new InvalidOperationException($"No L2 requirement IDs found under {RepoPaths.SpecsDirectory}.");
        }

        return (l1, l2);
    }
}
