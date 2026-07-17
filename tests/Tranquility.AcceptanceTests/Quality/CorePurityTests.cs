using System.Text.RegularExpressions;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Quality;

/// <summary>
/// L2-QLT-002: GIVEN decommutation test inputs WHEN executed in isolation THEN
/// outputs are reproducible without network or filesystem dependencies.
/// Enforced structurally: the Core assembly references only the BCL, has zero
/// package dependencies, and its sources never touch clocks, I/O, randomness,
/// or threading.
/// </summary>
[Requirement("L2-QLT-002")]
public sealed partial class CorePurityTests
{
    [Fact]
    public void Core_assembly_references_only_the_base_class_library()
    {
        var offenders = typeof(Core.Ccsds.SpacePacketHeader).Assembly
            .GetReferencedAssemblies()
            .Where(a => a.Name is not null
                && !a.Name.StartsWith("System.", StringComparison.Ordinal)
                && a.Name is not ("System" or "netstandard" or "mscorlib"))
            .Select(a => a.Name)
            .ToList();
        Assert.True(offenders.Count == 0,
            "Tranquility.Core must depend on the BCL only: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Core_project_declares_no_package_or_project_references()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepoPaths.Root, "src", "Tranquility.Core", "Tranquility.Core.csproj"));
        Assert.DoesNotContain("<PackageReference", csproj);
        Assert.DoesNotContain("<ProjectReference", csproj);
    }

    [GeneratedRegex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b|\bFile\.\w|\bDirectory\.\w|\bSocket\b|\bHttpClient\b|\bEnvironment\.\w|\bnew\s+Random\b|\bRandom\.Shared\b|\bStopwatch\b|\bTask\.(Run|Delay)\b|\bThread\.\w|\bGuid\.NewGuid\b")]
    private static partial Regex ImpureApi();

    [Fact]
    public void Core_sources_never_touch_clocks_io_randomness_or_threading()
    {
        var coreRoot = Path.Combine(RepoPaths.Root, "src", "Tranquility.Core");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            foreach (Match match in ImpureApi().Matches(File.ReadAllText(file)))
            {
                offenders.Add($"{Path.GetFileName(file)}: {match.Value}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Impure API usage in Tranquility.Core (time must arrive as data, I/O stays outside):\n" +
            string.Join('\n', offenders));
    }
}
