using System.Text.RegularExpressions;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Quality;

/// <summary>
/// L2-QLT-005: SDLS, expanded PUS coverage, and HA replication stay outside
/// this baseline. This guard scans production sources for type or namespace
/// declarations that would introduce those capability areas.
/// </summary>
[Requirement("L2-QLT-005")]
public sealed partial class ScopeGuardTests
{
    // Word-anchored so legitimate identifiers (e.g. "Corpus") never match.
    [GeneratedRegex(@"\b(?:namespace|class|record|struct|interface|enum)\s+(?<name>\w*(?:Sdls|HighAvailability|FailoverReplication)\w*|Pus[A-Z0-9]\w*|\w*Replication)\b")]
    private static partial Regex ForbiddenDeclaration();

    [Fact]
    public void Production_code_declares_no_deferred_capability_types()
    {
        var srcRoot = Path.Combine(RepoPaths.Root, "src");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in ForbiddenDeclaration().Matches(File.ReadAllText(file)))
            {
                offenders.Add($"{Path.GetRelativePath(RepoPaths.Root, file)}: {match.Value}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Deferred capability areas (SDLS / expanded PUS / HA replication) must not " +
            "be implemented in this baseline (L2-QLT-005):\n" + string.Join('\n', offenders));
    }
}
