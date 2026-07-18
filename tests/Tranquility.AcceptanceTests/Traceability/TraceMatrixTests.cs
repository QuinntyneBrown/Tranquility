using System.Reflection;
using System.Text;
using Xunit;

namespace Tranquility.AcceptanceTests.Traceability;

/// <summary>
/// Regenerates the verification cross-reference matrix (VCRM) from the
/// [Requirement] traits and the specs, and asserts it is complete. Writing the
/// artifact here keeps docs/specs/TRQ-VCRM.md in lock-step with the suite.
/// </summary>
public sealed class TraceMatrixTests
{
    [Fact]
    public void Vcrm_is_regenerated_and_covers_every_l2_requirement()
    {
        var testsByRequirement = CollectTestsByRequirement();

        var missing = SpecCatalog.L2Ids.Where(id => !testsByRequirement.ContainsKey(id)).ToList();
        Assert.True(missing.Count == 0, "L2 requirements with no traced test: " + string.Join(", ", missing));

        var sb = new StringBuilder();
        sb.AppendLine("# TRQ-VCRM — verification cross-reference matrix");
        sb.AppendLine();
        sb.AppendLine("Generated from the traced acceptance suite (`[Requirement]` traits). Each L2");
        sb.AppendLine("requirement below is verified by the listed acceptance test(s).");
        sb.AppendLine();
        sb.AppendLine("| Requirement | Verifying acceptance tests |");
        sb.AppendLine("|---|---|");
        foreach (var id in SpecCatalog.L2Ids.OrderBy(x => x, StringComparer.Ordinal))
        {
            var tests = string.Join("<br>", testsByRequirement[id].OrderBy(t => t, StringComparer.Ordinal));
            sb.AppendLine($"| {id} | {tests} |");
        }

        var path = Path.Combine(RepoPaths.SpecsDirectory, "TRQ-VCRM.md");
        File.WriteAllText(path, sb.ToString());
        Assert.True(File.Exists(path));
    }

    private static Dictionary<string, List<string>> CollectTestsByRequirement()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var type in typeof(TraceMatrixTests).Assembly.GetTypes())
        {
            var typeLevel = type.GetCustomAttributes<RequirementAttribute>(inherit: true).Select(a => a.Id).ToList();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!method.GetCustomAttributes<FactAttribute>(inherit: true).Any()
                    && !method.GetCustomAttributes<TheoryAttribute>(inherit: true).Any())
                {
                    continue;
                }

                var ids = typeLevel.Concat(method.GetCustomAttributes<RequirementAttribute>(inherit: true).Select(a => a.Id));
                foreach (var id in ids.Distinct())
                {
                    if (!map.TryGetValue(id, out var list))
                    {
                        map[id] = list = [];
                    }

                    var name = $"{type.Name}.{method.Name}";
                    if (!list.Contains(name))
                    {
                        list.Add(name);
                    }
                }
            }
        }

        return map;
    }
}
