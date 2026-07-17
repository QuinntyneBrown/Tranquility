using System.Globalization;
using System.Text;
using CsCheck;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;
using Xunit;

namespace Tranquility.AcceptanceTests.SpacePackets;

/// <summary>
/// L2-SPP-003 (Analysis, made executable): GIVEN repeated runs of the same
/// packet corpus with the same MDB WHEN outputs are compared THEN extracted
/// values and timestamps are identical. Also covers L2-QLT-002's
/// reproducibility clause: two independently loaded models must behave
/// byte-identically.
/// </summary>
[Requirement("L2-SPP-003")]
public sealed class DeterminismTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Random_science_packets_decommutate_identically_across_independent_mdb_loads()
    {
        var mdbA = ContainerRoutingTests.LoadSampleSat();
        var mdbB = ContainerRoutingTests.LoadSampleSat();
        var engineA = new DecommutationEngine(mdbA);
        var engineB = new DecommutationEngine(mdbB);

        // Random 8-octet science payloads behind the golden APID-100 header.
        Gen.Byte.Array[8].Sample(payload =>
        {
            var packet = new byte[14];
            HeaderDecodeTests.GoldenPacket.AsSpan(0, 6).CopyTo(packet);
            payload.CopyTo(packet, 6);

            var a = Serialize(engineA.Decommutate(packet, mdbA.FindContainer("/SampleSat/Root")!, T0, T0));
            var b = Serialize(engineB.Decommutate(packet, mdbB.FindContainer("/SampleSat/Root")!, T0, T0));
            return a == b;
        }, iter: 200);
    }

    [Fact]
    public void Same_packet_re_run_produces_byte_identical_serialized_output()
    {
        var mdb = ContainerRoutingTests.LoadSampleSat();
        var engine = new DecommutationEngine(mdb);
        var root = mdb.FindContainer("/SampleSat/Root")!;

        var first = Serialize(engine.Decommutate(HeaderDecodeTests.GoldenPacket, root, T0, T0));
        for (var run = 0; run < 50; run++)
        {
            var again = Serialize(engine.Decommutate(HeaderDecodeTests.GoldenPacket, root, T0, T0));
            Assert.Equal(first, again);
        }
    }

    private static string Serialize(DecommutationResult result)
    {
        var sb = new StringBuilder();
        sb.Append(result.MatchedContainer.QualifiedName).Append('\n');
        foreach (var value in result.Values)
        {
            sb.Append(value.Parameter.QualifiedName).Append('=')
              .Append(Convert.ToString(value.RawValue, CultureInfo.InvariantCulture)).Append('|')
              .Append(Convert.ToString(value.EngValue, CultureInfo.InvariantCulture)).Append('|')
              .Append(value.Monitoring).Append('|')
              .Append(value.GenerationTime.UtcTicks).Append('|')
              .Append(value.AcquisitionTime.UtcTicks).Append('\n');
        }

        return sb.ToString();
    }
}
