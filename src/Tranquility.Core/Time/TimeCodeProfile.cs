namespace Tranquility.Core.Time;

/// <summary>
/// The single declared CCSDS 301.0-B time-code profile for mission time
/// decode/encode behaviour (L2-TIM-004, resolves TBD-009):
///
/// - Onboard time (OBT): CUC with 4 coarse + 2 fine octets against the
///   configured agency epoch (default TAI 1958-01-01), linear scale.
/// - Ground timestamps: CDS with 2-octet day segment + 4-octet ms-of-day +
///   2-octet microsecond-of-millisecond segment, TAI 1958-01-01 epoch.
/// </summary>
public static class TimeCodeProfile
{
    public const int CucCoarseOctets = 4;

    public const int CucFineOctets = 2;

    public const int CdsDayOctets = 2;

    public const int CdsSubMillisecondOctets = 2;

    /// <summary>Default agency epoch when a mission does not configure one.</summary>
    public static readonly DateTimeOffset DefaultEpoch = new(1958, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
