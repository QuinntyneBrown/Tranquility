using Tranquility.AcceptanceTests.Traceability;
using Tranquility.DiffHarness;
using Xunit;

namespace Tranquility.AcceptanceTests.Differential;

/// <summary>
/// L2-DIF-004 (Inspection, made executable): GIVEN harness design review WHEN
/// evidence is inspected THEN no reference source-code artifacts are required
/// by the harness. Enforced structurally: the DiffHarness assembly references
/// only the BCL and Tranquility.Wire (documented interfaces) — never the
/// server internals or any reference-system package.
/// </summary>
[Requirement("L2-DIF-004")]
public sealed class HarnessIsolationTests
{
    [Fact]
    public void Harness_references_only_the_bcl_and_the_wire_contract()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Tranquility.Wire", "System.Private.CoreLib", "netstandard", "System", "mscorlib",
        };

        var offenders = typeof(DifferentialRun).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !allowed.Contains(name) && !name.StartsWith("System.", StringComparison.Ordinal))
            .ToList();

        Assert.True(offenders.Count == 0,
            "The differential harness must observe externally only (BCL + Wire); disallowed references: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void Harness_does_not_reference_the_server_application_or_infrastructure()
    {
        var forbidden = new[] { "Tranquility.Server", "Tranquility.Application", "Tranquility.Infrastructure", "Tranquility.Core" };
        var references = typeof(DifferentialRun).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToHashSet();
        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, references);
        }
    }
}
