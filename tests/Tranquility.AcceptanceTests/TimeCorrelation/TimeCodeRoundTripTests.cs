using CsCheck;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Ccsds;
using Tranquility.Core.Time;
using Xunit;

namespace Tranquility.AcceptanceTests.TimeCorrelation;

/// <summary>
/// L2-TIM-004: GIVEN mission time values encoded per declared profile WHEN
/// decoded and re-encoded THEN round-trip behavior matches profile rules.
/// Declared profile (TimeCodeProfile): CUC 4+2 octets OBT, CDS 2-day +
/// 2-submillisecond ground timestamps.
/// </summary>
[Requirement("L2-TIM-004")]
public sealed class TimeCodeRoundTripTests
{
    private static readonly DateTimeOffset Epoch = TimeCodeProfile.DefaultEpoch;

    [Fact]
    public void Cuc_golden_vector_decodes_per_profile()
    {
        // coarse 0x12345678 s, fine 0x8000/0x10000 = 0.5 s
        byte[] tField = [0x12, 0x34, 0x56, 0x78, 0x80, 0x00];
        var time = CucTimeCodec.DecodeImplicit(
            tField, TimeCodeProfile.CucCoarseOctets, TimeCodeProfile.CucFineOctets, Epoch);

        Assert.Equal(0x12345678UL, time.CoarseSeconds);
        Assert.Equal(0.5, time.FineFraction, precision: 9);
    }

    [Fact]
    public void Cuc_round_trip_is_exact_for_every_representable_value()
    {
        Gen.Select(Gen.UInt, Gen.UShort).Sample(t =>
        {
            var (coarse, fine) = t;
            var buffer = new byte[6];
            var original = new CucTime(coarse, fine / 65536.0, Epoch);

            CucTimeCodec.EncodeImplicit(original, buffer,
                TimeCodeProfile.CucCoarseOctets, TimeCodeProfile.CucFineOctets);
            var decoded = CucTimeCodec.DecodeImplicit(buffer,
                TimeCodeProfile.CucCoarseOctets, TimeCodeProfile.CucFineOctets, Epoch);

            return decoded.CoarseSeconds == coarse
                && Math.Abs(decoded.FineFraction - fine / 65536.0) < 1e-12;
        }, iter: 500);
    }

    [Fact]
    public void Cds_round_trip_is_exact_for_every_representable_value()
    {
        var gen = Gen.Select(Gen.UShort, Gen.UInt[0, 86_399_999], Gen.UShort[0, 999]);
        gen.Sample(t =>
        {
            var (days, msOfDay, microseconds) = t;
            var buffer = new byte[2 + 4 + 2];
            var original = new CdsTime(days, msOfDay, microseconds,
                TimeCodeProfile.CdsSubMillisecondOctets, Epoch);

            CdsTimeCodec.EncodeImplicit(original, buffer,
                TimeCodeProfile.CdsDayOctets, TimeCodeProfile.CdsSubMillisecondOctets);
            var decoded = CdsTimeCodec.DecodeImplicit(buffer,
                TimeCodeProfile.CdsDayOctets, TimeCodeProfile.CdsSubMillisecondOctets, Epoch);

            return decoded.Days == days
                && decoded.MillisecondsOfDay == msOfDay
                && decoded.SubMilliseconds == microseconds;
        }, iter: 500);
    }

    [Fact]
    public void Cds_utc_mapping_matches_hand_computed_instant()
    {
        // 2000-01-01T00:00:00 TAI-epoch-relative: 15340 days after 1958-01-01.
        var time = new CdsTime(15340, 45_000_000, 500, 2, Epoch); // 12:30:00.000500
        Assert.Equal(
            new DateTimeOffset(2000, 1, 1, 12, 30, 0, TimeSpan.Zero).AddTicks(5000),
            time.ToDateTimeOffset());
    }
}
