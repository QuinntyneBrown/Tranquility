using Tranquility.Core.Alarms;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;

namespace Tranquility.Core.Tests.Decommutation;

/// <summary>
/// Verifies L2-SPP-003, L2-MDB-002, L2-PAR-001/002: container matching,
/// extraction, calibration, enumeration, and limit monitoring.
/// </summary>
public class DecommutationEngineTests
{
    private readonly MissionDatabase _mdb;
    private readonly DecommutationEngine _engine;

    public DecommutationEngineTests()
    {
        _mdb = BuildSampleMdb();
        _engine = new DecommutationEngine(_mdb);
    }

    /// <summary>
    /// Sample packet, hand-assembled:
    /// header: APID 100, unsegmented, seq 5, data length field 7 (8-octet data field)
    /// data:   counter u16 = 258, temp u12 raw = 1024, mode u4 = 2, volts f32 = 1.5
    /// </summary>
    private static readonly byte[] GoldenPacket =
    [
        0x00, 0x64, 0xC0, 0x05, 0x00, 0x07,
        0x01, 0x02,
        0x40, 0x02,
        0x3F, 0xC0, 0x00, 0x00,
    ];

    [Fact]
    public void Decommutate_GoldenPacket_MatchesDerivedContainer()
    {
        var result = Run(GoldenPacket);
        Assert.Equal("/SampleSat/SciPacket", result.MatchedContainer.QualifiedName);
    }

    [Fact]
    public void Decommutate_GoldenPacket_ExtractsHeaderParameters()
    {
        var result = Run(GoldenPacket);
        Assert.Equal(100ul, Value(result, "/SampleSat/Apid").RawValue);
    }

    [Fact]
    public void Decommutate_GoldenPacket_ExtractsAndCalibratesTemperature()
    {
        var temp = Value(Run(GoldenPacket), "/SampleSat/Temperature");

        Assert.Equal(1024ul, temp.RawValue);
        Assert.Equal(31.2, Assert.IsType<double>(temp.EngValue), precision: 10);
    }

    [Fact]
    public void Decommutate_GoldenPacket_TemperatureViolatesWarningRange()
    {
        var temp = Value(Run(GoldenPacket), "/SampleSat/Temperature");
        Assert.Equal(MonitoringResult.Warning, temp.Monitoring);
    }

    [Fact]
    public void Decommutate_GoldenPacket_MapsEnumerationLabel()
    {
        var mode = Value(Run(GoldenPacket), "/SampleSat/Mode");

        Assert.Equal(2L, mode.RawValue);
        Assert.Equal("SCIENCE", mode.EngValue);
    }

    [Fact]
    public void Decommutate_GoldenPacket_DecodesIeee754Float()
    {
        var volts = Value(Run(GoldenPacket), "/SampleSat/BusVoltage");
        Assert.Equal(1.5, Assert.IsType<double>(volts.EngValue));
    }

    [Fact]
    public void Decommutate_UnknownApid_StaysOnRootContainer()
    {
        byte[] packet = (byte[])GoldenPacket.Clone();
        packet[1] = 0x65; // APID 101

        var result = Run(packet);

        Assert.Equal("/SampleSat/Root", result.MatchedContainer.QualifiedName);
        Assert.Equal(4, result.Values.Count); // header fields only
    }

    private DecommutationResult Run(byte[] packet)
    {
        var root = _mdb.FindContainer("/SampleSat/Root")!;
        var now = DateTimeOffset.UnixEpoch;
        return _engine.Decommutate(packet, root, now, now);
    }

    private static ParameterValue Value(DecommutationResult result, string qualifiedName) =>
        result.Values.Single(v => v.Parameter.QualifiedName == qualifiedName);

    internal static MissionDatabase BuildSampleMdb()
    {
        var system = new SpaceSystem("SampleSat");

        var u3 = new IntegerParameterType("u3", "/SampleSat/u3", new IntegerDataEncoding(3));
        var u1 = new IntegerParameterType("u1", "/SampleSat/u1", new IntegerDataEncoding(1));
        var u11 = new IntegerParameterType("u11", "/SampleSat/u11", new IntegerDataEncoding(11));
        var u16 = new IntegerParameterType("u16", "/SampleSat/u16", new IntegerDataEncoding(16));
        var tempType = new FloatParameterType(
            "temp12", "/SampleSat/temp12", new IntegerDataEncoding(12),
            new PolynomialCalibrator([-20.0, 0.05]),
            new StaticAlarmRanges
            {
                WarningRange = new AlarmRange(-10, 30),
                CriticalRange = new AlarmRange(-30, 40),
            });
        var modeType = new EnumeratedParameterType(
            "mode4", "/SampleSat/mode4", new IntegerDataEncoding(4),
            new Dictionary<long, string> { [0] = "SAFE", [1] = "NOMINAL", [2] = "SCIENCE" });
        var f32 = new FloatParameterType("f32", "/SampleSat/f32", new FloatDataEncoding(32));
        system.ParameterTypes.AddRange([u3, u1, u11, u16, tempType, modeType, f32]);

        var version = new Parameter("Version", "/SampleSat/Version", u3);
        var type = new Parameter("Type", "/SampleSat/Type", u1);
        var secHdr = new Parameter("SecHdrFlag", "/SampleSat/SecHdrFlag", u1);
        var apid = new Parameter("Apid", "/SampleSat/Apid", u11);
        var counter = new Parameter("Counter", "/SampleSat/Counter", u16);
        var temperature = new Parameter("Temperature", "/SampleSat/Temperature", tempType);
        var mode = new Parameter("Mode", "/SampleSat/Mode", modeType);
        var busVoltage = new Parameter("BusVoltage", "/SampleSat/BusVoltage", f32);
        system.Parameters.AddRange([version, type, secHdr, apid, counter, temperature, mode, busVoltage]);

        var root = new SequenceContainer("Root", "/SampleSat/Root", isAbstract: true);
        root.Entries.AddRange(
        [
            new ParameterEntry(version),
            new ParameterEntry(type),
            new ParameterEntry(secHdr),
            new ParameterEntry(apid),
        ]);

        var sci = new SequenceContainer(
            "SciPacket", "/SampleSat/SciPacket",
            baseContainer: root,
            restrictionCriteria: [new RestrictionCriterion(apid, ComparisonOperator.Equal, "100", useCalibratedValue: false)]);
        sci.Entries.AddRange(
        [
            new ParameterEntry(counter, absoluteBitOffset: 48),
            new ParameterEntry(temperature),
            new ParameterEntry(mode),
            new ParameterEntry(busVoltage),
        ]);

        system.Containers.AddRange([root, sci]);
        return new MissionDatabase(system);
    }
}
