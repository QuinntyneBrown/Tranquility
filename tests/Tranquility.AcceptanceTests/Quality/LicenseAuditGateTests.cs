using System.Diagnostics;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Quality;

/// <summary>
/// L2-QLT-003: GIVEN a dependency graph containing a disallowed license WHEN
/// the CI license audit runs THEN pipeline status is failed. The static test
/// pins the gate's presence and policy; the negative control proves the gate
/// can actually fail by auditing against an over-strict allowlist.
/// </summary>
[Requirement("L2-QLT-003")]
public sealed class LicenseAuditGateTests
{
    private static string CiPath => Path.Combine(RepoPaths.Root, ".github", "workflows", "ci.yml");

    [Fact]
    public void Ci_has_a_failing_license_audit_gate_with_an_explicit_allowlist()
    {
        var ci = File.ReadAllText(CiPath);
        Assert.Contains("license-audit", ci);
        Assert.Contains("nuget-license", ci);
        Assert.Contains("Apache-2.0", ci);
        Assert.Contains("MIT", ci);
        Assert.Contains("build/license-url-mappings.json", ci);
    }

    [Fact]
    [Trait("Category", "LicenseAudit")]
    public async Task Audit_fails_when_the_allowlist_excludes_a_license_in_use()
    {
        // Negative control: this test project depends on MIT-licensed packages
        // (Microsoft.NET.Test.Sdk et al.), so an Apache-2.0-only allowlist must
        // make the tool fail. Scoped to one project to keep the control fast.
        var project = Path.Combine(RepoPaths.Root, "tests", "Tranquility.AcceptanceTests", "Tranquility.AcceptanceTests.csproj");
        var exitCode = await RunNugetLicenseAsync("-i", project, "-a", "Apache-2.0",
            "-mapping", Path.Combine(RepoPaths.Root, "build", "license-url-mappings.json"));
        if (exitCode is null)
        {
            Assert.Skip("nuget-license tool not installed; the CI license-audit job runs this control.");
        }

        Assert.True(exitCode != 0,
            "nuget-license exited 0 with an Apache-2.0-only allowlist even though MIT dependencies exist; " +
            "the license gate cannot fail and is therefore not a gate.");
    }

    private static async Task<int?> RunNugetLicenseAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo("nuget-license")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepoPaths.Root,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(startInfo)!;
            // Drain both pipes concurrently; an undrained redirected pipe
            // deadlocks the child once the buffer fills.
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromMinutes(3));
            try
            {
                await process.WaitForExitAsync(bound.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("nuget-license did not complete within 3 minutes.");
            }

            await Task.WhenAll(stdout, stderr);
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // tool not installed
        }
    }
}
