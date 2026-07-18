// Performance verification harness (L2-QLT-004). Measures the declared
// metrics and exits non-zero if any falls below its threshold, so the perf CI
// job gates on the declared baseline.
using System.Diagnostics;
using System.Text.Json;
using Tranquility.Core.Decommutation;
using Tranquility.Infrastructure.Xtce;

var thresholdsPath = Path.Combine(AppContext.BaseDirectory, "thresholds.json");
if (!File.Exists(thresholdsPath))
{
    thresholdsPath = Path.Combine(Directory.GetCurrentDirectory(), "thresholds.json");
}

using var doc = JsonDocument.Parse(File.ReadAllText(thresholdsPath));
var thresholds = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());

var mdbPath = args.Length > 0 ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures", "xtce", "SampleSat.xml");
var mdb = new XtceLoader(Path.GetFullPath(mdbPath)).Load();
var engine = new DecommutationEngine(mdb);
var root = mdb.FindContainer("/SampleSat/Root")!;
byte[] packet = [0x00, 0x64, 0xC0, 0x05, 0x00, 0x07, 0x01, 0x02, 0x40, 0x02, 0x3F, 0xC0, 0x00, 0x00];
var t0 = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

for (var i = 0; i < 5000; i++)
{
    engine.Decommutate(packet, root, t0, t0);
}

const int iterations = 500_000;
var stopwatch = Stopwatch.StartNew();
long updates = 0;
for (var i = 0; i < iterations; i++)
{
    updates += engine.Decommutate(packet, root, t0, t0).Values.Count;
}

stopwatch.Stop();
var seconds = stopwatch.Elapsed.TotalSeconds;
var pktPerSec = iterations / seconds;
var updPerSec = updates / seconds;

Console.WriteLine($"ingest-packets-per-second    : {pktPerSec:N0} (target {thresholds["ingest-packets-per-second"]:N0})");
Console.WriteLine($"parameter-updates-per-second : {updPerSec:N0} (target {thresholds["parameter-updates-per-second"]:N0})");

var failed = pktPerSec < thresholds["ingest-packets-per-second"]
    || updPerSec < thresholds["parameter-updates-per-second"];
Console.WriteLine(failed ? "PERF GATE: FAILED" : "PERF GATE: PASSED");
return failed ? 1 : 0;
