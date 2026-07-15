using System.Xml.Linq;
using Tranquility.Core.Mdb;
using Tranquility.Infrastructure.Xtce;

namespace Tranquility.Application.Tests;

/// <summary>Verifies L2-MDB-001/002/003 against the SampleSat XTCE fixture.</summary>
public class XtceLoaderTests
{
    private static MissionDatabase LoadSample() =>
        new XtceLoader(Path.Combine(AppContext.BaseDirectory, "SampleSat.xml")).Load();

    [Fact]
    public void Load_SampleSat_IndexesAllParameters()
    {
        var mdb = LoadSample();

        Assert.Equal(11, mdb.Parameters.Count);
        Assert.NotNull(mdb.FindParameter("/SampleSat/Temperature"));
    }

    [Fact]
    public void Load_SampleSat_BuildsCalibrator()
    {
        var mdb = LoadSample();
        var type = Assert.IsType<FloatParameterType>(mdb.FindParameter("/SampleSat/Temperature")!.Type);

        Assert.NotNull(type.Calibrator);
        Assert.Equal([-20.0, 0.05], type.Calibrator!.Coefficients);
        Assert.Equal(31.2, type.Calibrator.Apply(1024), precision: 10);
    }

    [Fact]
    public void Load_SampleSat_BuildsAlarmRanges()
    {
        var mdb = LoadSample();
        var type = Assert.IsType<FloatParameterType>(mdb.FindParameter("/SampleSat/Temperature")!.Type);

        Assert.NotNull(type.DefaultAlarm);
        Assert.Equal(new Core.Alarms.AlarmRange(-10, 30), type.DefaultAlarm!.WarningRange);
        Assert.Equal(new Core.Alarms.AlarmRange(-30, 40), type.DefaultAlarm.CriticalRange);
    }

    [Fact]
    public void Load_SampleSat_BuildsEnumerationLabels()
    {
        var mdb = LoadSample();
        var type = Assert.IsType<EnumeratedParameterType>(mdb.FindParameter("/SampleSat/Mode")!.Type);

        Assert.Equal("SCIENCE", type.GetLabel(2));
    }

    [Fact]
    public void Load_SampleSat_WiresContainerInheritance()
    {
        var mdb = LoadSample();
        var sci = mdb.FindContainer("/SampleSat/SciPacket")!;

        Assert.Equal("/SampleSat/Root", sci.BaseContainer!.QualifiedName);
        var criterion = Assert.Single(sci.RestrictionCriteria);
        Assert.Equal("/SampleSat/Apid", criterion.Parameter.QualifiedName);
        Assert.Equal("100", criterion.Value);
        Assert.False(criterion.UseCalibratedValue);
    }

    [Fact]
    public void Load_SampleSat_RootContainerIsAbstractWithSevenEntries()
    {
        var mdb = LoadSample();
        var root = mdb.FindContainer("/SampleSat/Root")!;

        Assert.True(root.IsAbstract);
        Assert.Equal(7, root.Entries.Count);
    }

    [Fact]
    public void Parse_UnknownParameterTypeRef_ThrowsWithContext()
    {
        var doc = XDocument.Parse("""
            <SpaceSystem name="X">
              <TelemetryMetaData>
                <ParameterTypeSet />
                <ParameterSet>
                  <Parameter name="P" parameterTypeRef="missing" />
                </ParameterSet>
              </TelemetryMetaData>
            </SpaceSystem>
            """);

        var ex = Assert.Throws<XtceLoadException>(() => XtceLoader.Parse(doc));
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnknownContainerParameterRef_Throws()
    {
        var doc = XDocument.Parse("""
            <SpaceSystem name="X">
              <TelemetryMetaData>
                <ParameterTypeSet />
                <ParameterSet />
                <ContainerSet>
                  <SequenceContainer name="C">
                    <EntryList>
                      <ParameterRefEntry parameterRef="nope" />
                    </EntryList>
                  </SequenceContainer>
                </ContainerSet>
              </TelemetryMetaData>
            </SpaceSystem>
            """);

        Assert.Throws<XtceLoadException>(() => XtceLoader.Parse(doc));
    }

    [Fact]
    public void Parse_MissingEncoding_Throws()
    {
        var doc = XDocument.Parse("""
            <SpaceSystem name="X">
              <TelemetryMetaData>
                <ParameterTypeSet>
                  <IntegerParameterType name="broken" />
                </ParameterTypeSet>
              </TelemetryMetaData>
            </SpaceSystem>
            """);

        Assert.Throws<XtceLoadException>(() => XtceLoader.Parse(doc));
    }
}
