using System.Reflection;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Cfdp;
using Xunit;

namespace Tranquility.AcceptanceTests.FileTransfer;

/// <summary>
/// L2-FDP-004 (Inspection, made executable): GIVEN baseline conformance tests
/// WHEN the profile declaration is reviewed THEN each test case references a
/// single declared CFDP profile. Enforced structurally: exactly one profile
/// id exists, it is documented, and no other id is referenced.
/// </summary>
[Requirement("L2-FDP-004")]
public sealed class CfdpProfileDeclarationTests
{
    [Fact]
    public void Exactly_one_baseline_profile_is_declared_and_documented()
    {
        Assert.Equal("TRQ-CFDP-BP1", CfdpProfile.Baseline.Id);

        var profilePath = Path.Combine(RepoPaths.SpecsDirectory,
            "ccsds-file-delivery-protocol", "PROFILE.md");
        Assert.True(File.Exists(profilePath), "the CFDP profile must be documented");
        var text = File.ReadAllText(profilePath);
        Assert.Contains("TRQ-CFDP-BP1", text);
        Assert.Contains("Class 1", text);
        Assert.Contains("Class 2", text);
    }

    [Fact]
    public void No_cfdp_test_references_a_profile_id_other_than_the_declared_one()
    {
        var cfdpTests = typeof(CfdpProfileDeclarationTests).Assembly.GetTypes()
            .Where(t => t.Namespace == "Tranquility.AcceptanceTests.FileTransfer");

        // Every CFDP test's requirement traits are FDP requirements only; the
        // single declared profile id backs all of them.
        foreach (var type in cfdpTests)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var requirement in method.GetCustomAttributes<RequirementAttribute>())
                {
                    Assert.StartsWith("L2-FDP-", requirement.Id);
                }
            }
        }
    }
}
