using System.Xml.Linq;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Quality;

/// <summary>
/// L2-QLT-001: GIVEN release metadata WHEN reviewed THEN target runtime and
/// OS support include current .NET LTS and Linux.
/// </summary>
[Requirement("L2-QLT-001")]
public sealed class RuntimeTargetTests
{
    [Fact]
    public void Solution_targets_a_current_lts_dotnet_runtime()
    {
        var props = XDocument.Load(Path.Combine(RepoPaths.Root, "Directory.Build.props"));
        var tfm = props.Descendants("TargetFramework").Single().Value;

        Assert.Matches(@"^net\d+\.0$", tfm);
        var major = int.Parse(tfm[3..tfm.IndexOf('.')]);
        Assert.True(major % 2 == 0,
            $"Target framework {tfm} is not an LTS release (.NET LTS releases have even major versions).");
        Assert.True(major >= 8, $"Target framework {tfm} is out of support.");
    }

    [Fact]
    public void Ci_builds_and_tests_on_linux_as_the_merge_gate()
    {
        var ci = File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "workflows", "ci.yml"));
        Assert.Contains("runs-on: ubuntu-latest", ci);
        Assert.Contains("dotnet test", ci);
    }
}
