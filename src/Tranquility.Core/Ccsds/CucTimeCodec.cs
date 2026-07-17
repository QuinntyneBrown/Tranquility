namespace Tranquility.Core.Ccsds;

/// <summary>
/// CCSDS Unsegmented Time Code (CUC) decoding.
/// Implements: L2-TIM-001. Source: CCSDS 301.0-B (Time Code Formats).
///
/// Leap-second handling is out of scope for this decoder: the epoch is treated as a
/// continuous time scale and mapped linearly onto <see cref="DateTimeOffset"/>.
/// See TRQ-OPEN-QUESTIONS.md.
/// </summary>
public static class CucTimeCodec
{
    /// <summary>CCSDS-recommended TAI epoch: 1958-01-01 (CCSDS 301.0-B).</summary>
    public static readonly DateTimeOffset TaiEpoch = new(1958, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Decodes a CUC time code that carries a preamble (P-field) octet.
    /// </summary>
    /// <param name="buffer">P-field octet followed by the T-field octets.</param>
    /// <param name="agencyEpoch">Epoch to use when the P-field indicates an agency-defined epoch.</param>
    public static CucTime Decode(ReadOnlySpan<byte> buffer, DateTimeOffset? agencyEpoch = null)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException("CUC buffer is empty.", nameof(buffer));
        }

        byte pField = buffer[0];
        if ((pField & 0x80) != 0)
        {
            throw new NotSupportedException("Extended CUC P-fields are not supported.");
        }

        int timeCodeId = (pField >> 4) & 0x7;
        DateTimeOffset epoch = timeCodeId switch
        {
            0b001 => TaiEpoch,
            0b010 => agencyEpoch
                ?? throw new ArgumentException("P-field indicates an agency-defined epoch but none was supplied.", nameof(agencyEpoch)),
            _ => throw new NotSupportedException($"Unsupported CUC time code identification {timeCodeId}."),
        };

        int coarseOctets = ((pField >> 2) & 0x3) + 1;
        int fineOctets = pField & 0x3;
        return DecodeImplicit(buffer[1..], coarseOctets, fineOctets, epoch);
    }

    /// <summary>
    /// Decodes a CUC T-field whose layout is known a priori (no P-field on the wire).
    /// </summary>
    public static CucTime DecodeImplicit(ReadOnlySpan<byte> tField, int coarseOctets, int fineOctets, DateTimeOffset epoch)
    {
        if (coarseOctets is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(coarseOctets), "CUC coarse time is 1 to 4 octets.");
        }

        if (fineOctets is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(fineOctets), "CUC fine time is 0 to 3 octets.");
        }

        if (tField.Length < coarseOctets + fineOctets)
        {
            throw new ArgumentException(
                $"T-field of {tField.Length} octets is shorter than the declared {coarseOctets + fineOctets} octets.",
                nameof(tField));
        }

        ulong coarse = 0;
        for (int i = 0; i < coarseOctets; i++)
        {
            coarse = (coarse << 8) | tField[i];
        }

        ulong fine = 0;
        for (int i = 0; i < fineOctets; i++)
        {
            fine = (fine << 8) | tField[coarseOctets + i];
        }

        double fraction = fineOctets == 0 ? 0.0 : fine / Math.Pow(2, 8 * fineOctets);
        return new CucTime(coarse, fraction, epoch);
    }

    /// <summary>
    /// Encodes a CUC T-field with the given layout (no P-field), the inverse
    /// of <see cref="DecodeImplicit"/> for the declared profile (L2-TIM-004).
    /// </summary>
    public static void EncodeImplicit(CucTime time, Span<byte> tField, int coarseOctets, int fineOctets)
    {
        if (coarseOctets is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(coarseOctets), "CUC coarse time is 1 to 4 octets.");
        }

        if (fineOctets is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(fineOctets), "CUC fine time is 0 to 3 octets.");
        }

        if (tField.Length < coarseOctets + fineOctets)
        {
            throw new ArgumentException(
                $"T-field of {tField.Length} octets cannot hold {coarseOctets + fineOctets} octets.", nameof(tField));
        }

        if (coarseOctets < 8 && time.CoarseSeconds >= 1UL << (8 * coarseOctets))
        {
            throw new ArgumentOutOfRangeException(nameof(time),
                $"Coarse seconds {time.CoarseSeconds} do not fit {coarseOctets} octets.");
        }

        for (int i = coarseOctets - 1; i >= 0; i--)
        {
            tField[i] = (byte)(time.CoarseSeconds >> (8 * (coarseOctets - 1 - i)));
        }

        if (fineOctets > 0)
        {
            ulong scale = 1UL << (8 * fineOctets);
            ulong fine = (ulong)Math.Round(time.FineFraction * scale);
            if (fine >= scale)
            {
                fine = scale - 1;
            }

            for (int i = 0; i < fineOctets; i++)
            {
                tField[coarseOctets + i] = (byte)(fine >> (8 * (fineOctets - 1 - i)));
            }
        }
    }
}

/// <summary>A decoded CUC time value. Source: CCSDS 301.0-B.</summary>
public readonly record struct CucTime(ulong CoarseSeconds, double FineFraction, DateTimeOffset Epoch)
{
    public DateTimeOffset ToDateTimeOffset() =>
        Epoch + TimeSpan.FromTicks((long)(CoarseSeconds * (double)TimeSpan.TicksPerSecond)
            + (long)Math.Round(FineFraction * TimeSpan.TicksPerSecond));
}
