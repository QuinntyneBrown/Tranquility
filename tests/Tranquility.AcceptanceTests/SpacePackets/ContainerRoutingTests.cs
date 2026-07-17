using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;
using Tranquility.Infrastructure.Xtce;
using Xunit;

namespace Tranquility.AcceptanceTests.SpacePackets;

/// <summary>
/// L2-SPP-002: GIVEN a packet mapped in MDB WHEN decommutation executes THEN
/// the expected container and parameter set is produced.
/// </summary>
[Requirement("L2-SPP-002")]
public sealed class ContainerRoutingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mapped_packet_routes_to_the_derived_container_with_expected_parameters()
    {
        var mdb = LoadSampleSat();
        var engine = new DecommutationEngine(mdb);
        var root = mdb.FindContainer("/SampleSat/Root")!;

        var result = engine.Decommutate(HeaderDecodeTests.GoldenPacket, root, T0, T0);

        Assert.Equal("/SampleSat/SciPacket", result.MatchedContainer.QualifiedName);

        var byName = result.Values.ToDictionary(v => v.Parameter.Name);
        Assert.Equal(100UL, byName["Apid"].RawValue);
        Assert.Equal(258UL, byName["Counter"].RawValue);
        Assert.Equal("SCIENCE", byName["Mode"].EngValue);
        Assert.Equal(1.5, Assert.IsType<double>(byName["BusVoltage"].EngValue), precision: 6);
    }

    [Fact]
    public void Unmapped_packet_stays_on_the_root_container()
    {
        var mdb = LoadSampleSat();
        var engine = new DecommutationEngine(mdb);
        var root = mdb.FindContainer("/SampleSat/Root")!;

        // APID 200 matches no restriction criteria.
        byte[] packet = [0x00, 0xC8, 0xC0, 0x05, 0x00, 0x07, 0, 0, 0, 0, 0, 0, 0, 0];
        var result = engine.Decommutate(packet, root, T0, T0);

        Assert.Equal("/SampleSat/Root", result.MatchedContainer.QualifiedName);
    }

    internal static Core.Mdb.MissionDatabase LoadSampleSat() =>
        new XtceLoader(Path.Combine(TestConfig.XtceFixtureDirectory, "SampleSat.xml")).Load();
}
