using Tranquility.DiffHarness;

// Differential conformance harness entry point (skeleton).
// Full corpus replay and comparison arrive in a later phase; see
// docs/specs/differential/L1.md and L2.md for the requirements this
// tool exists to verify.

Console.WriteLine("Tranquility differential conformance harness (skeleton)");
Console.WriteLine();
Console.WriteLine("Planned usage:");
Console.WriteLine("  diffharness --corpus <path> --left <url> --right <url> --out report.json");
Console.WriteLine();
Console.WriteLine("Comparison surfaces (L2-DIF-002):");
foreach (var surface in Enum.GetValues<ComparisonSurface>())
{
    Console.WriteLine($"  - {surface}");
}

Console.WriteLine();
Console.WriteLine("This harness observes external behavior only (L2-DIF-004).");
return 0;
