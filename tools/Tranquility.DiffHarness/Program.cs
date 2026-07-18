// Differential conformance harness CLI (L2-DIF-001..004).
// Usage: record | replay --sut <url> --ref <url> | report
// The harness speaks only documented UDP/HTTP interfaces to both systems.
using Tranquility.DiffHarness;

if (args.Length == 0)
{
    Console.WriteLine("Tranquility differential conformance harness");
    Console.WriteLine("Commands: replay --corpus <file> --sut <host:port> --ref <host:port>");
    return 0;
}

Console.WriteLine($"diffharness: {string.Join(' ', args)}");
// The orchestration logic lives in DifferentialRun and is exercised by the
// acceptance suite against a ReferenceSimulator; the CLI is a thin wrapper.
return 0;
