using Tranquility.Core.Ccsds;

namespace Tranquility.Core.Tests.Ccsds;

/// <summary>Verifies L2-TIM-001 against hand-computed CCSDS 301.0-B vectors.</summary>
public class TimeCodecTests
{
    [Fact]
    public void Cuc_TaiEpoch_FourCoarseOneFine_Decodes()
    {
        // P-field 0x1D: TAI epoch (1958), 4 coarse octets, 1 fine octet.
        // Coarse = 1 s, fine = 0x80 = 128/256 = 0.5 s.
        var t = CucTimeCodec.Decode([0x1D, 0x00, 0x00, 0x00, 0x01, 0x80]);

        Assert.Equal(1UL, t.CoarseSeconds);
        Assert.Equal(0.5, t.FineFraction, precision: 10);
        Assert.Equal(new DateTimeOffset(1958, 1, 1, 0, 0, 1, 500, TimeSpan.Zero), t.ToDateTimeOffset());
    }

    [Fact]
    public void Cuc_AgencyEpoch_FourCoarseTwoFine_Decodes()
    {
        // P-field 0x2E: agency epoch, 4 coarse octets, 2 fine octets.
        // Coarse = 256 s, fine = 0x8000/0x10000 = 0.5 s.
        var epoch = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t = CucTimeCodec.Decode([0x2E, 0x00, 0x00, 0x01, 0x00, 0x80, 0x00], epoch);

        Assert.Equal(new DateTimeOffset(2000, 1, 1, 0, 4, 16, 500, TimeSpan.Zero), t.ToDateTimeOffset());
    }

    [Fact]
    public void Cuc_AgencyEpochWithoutEpochArgument_Throws()
    {
        Assert.Throws<ArgumentException>(() => CucTimeCodec.Decode([0x2E, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Cuc_Implicit_DecodesWithoutPField()
    {
        var epoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t = CucTimeCodec.DecodeImplicit([0x00, 0x00, 0x00, 0x3C], coarseOctets: 4, fineOctets: 0, epoch);

        Assert.Equal(epoch.AddSeconds(60), t.ToDateTimeOffset());
    }

    [Fact]
    public void Cds_1958Epoch_16BitDays_NoSubMs_Decodes()
    {
        // P-field 0x40: CDS, 1958 epoch, 16-bit day segment, no submilliseconds.
        // Days = 1, ms of day = 60000 (one minute).
        var t = CdsTimeCodec.Decode([0x40, 0x00, 0x01, 0x00, 0x00, 0xEA, 0x60]);

        Assert.Equal(1u, t.Days);
        Assert.Equal(60_000u, t.MillisecondsOfDay);
        Assert.Equal(new DateTimeOffset(1958, 1, 2, 0, 1, 0, TimeSpan.Zero), t.ToDateTimeOffset());
    }

    [Fact]
    public void Cds_MicrosecondSegment_Decodes()
    {
        // P-field 0x41: CDS, 1958 epoch, 16-bit days, 16-bit microsecond segment.
        // Days = 0, ms = 1, sub-ms = 500 us -> 1.5 ms after epoch.
        var t = CdsTimeCodec.Decode([0x41, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0xF4]);

        Assert.Equal(CdsTimeCodec.TaiEpoch + TimeSpan.FromTicks(15_000), t.ToDateTimeOffset());
    }

    [Fact]
    public void Cds_NonCdsTimeCodeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => CdsTimeCodec.Decode([0x1D, 0, 0, 0, 0, 0, 0]));
    }
}
