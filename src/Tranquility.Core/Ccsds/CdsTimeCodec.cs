using System.Buffers.Binary;

namespace Tranquility.Core.Ccsds;

/// <summary>
/// CCSDS Day Segmented Time Code (CDS) decoding.
/// Implements: L2-TIM-001. Source: CCSDS 301.0-B (Time Code Formats).
///
/// Leap-second handling is out of scope for this decoder: the epoch is treated as a
/// continuous time scale and mapped linearly onto <see cref="DateTimeOffset"/>.
/// See TRQ-OPEN-QUESTIONS.md.
/// </summary>
public static class CdsTimeCodec
{
    /// <summary>CCSDS-recommended TAI epoch: 1958-01-01 (CCSDS 301.0-B).</summary>
    public static readonly DateTimeOffset TaiEpoch = new(1958, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Decodes a CDS time code that carries a preamble (P-field) octet.
    /// </summary>
    /// <param name="buffer">P-field octet followed by the T-field octets.</param>
    /// <param name="agencyEpoch">Epoch to use when the P-field indicates an agency-defined epoch.</param>
    public static CdsTime Decode(ReadOnlySpan<byte> buffer, DateTimeOffset? agencyEpoch = null)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException("CDS buffer is empty.", nameof(buffer));
        }

        byte pField = buffer[0];
        if ((pField & 0x80) != 0)
        {
            throw new NotSupportedException("Extended CDS P-fields are not supported.");
        }

        int timeCodeId = (pField >> 4) & 0x7;
        if (timeCodeId != 0b100)
        {
            throw new ArgumentException($"P-field time code identification {timeCodeId} is not CDS (4).", nameof(buffer));
        }

        bool agencyDefinedEpoch = ((pField >> 3) & 0x1) != 0;
        DateTimeOffset epoch = agencyDefinedEpoch
            ? agencyEpoch ?? throw new ArgumentException("P-field indicates an agency-defined epoch but none was supplied.", nameof(agencyEpoch))
            : TaiEpoch;

        int dayOctets = ((pField >> 2) & 0x1) == 0 ? 2 : 3;
        int subMillisecondOctets = (pField & 0x3) switch
        {
            0b00 => 0,
            0b01 => 2, // microseconds of millisecond
            0b10 => 4, // picoseconds of millisecond
            _ => throw new NotSupportedException("Reserved CDS submillisecond length."),
        };

        return DecodeImplicit(buffer[1..], dayOctets, subMillisecondOctets, epoch);
    }

    /// <summary>
    /// Decodes a CDS T-field whose layout is known a priori (no P-field on the wire).
    /// </summary>
    public static CdsTime DecodeImplicit(ReadOnlySpan<byte> tField, int dayOctets, int subMillisecondOctets, DateTimeOffset epoch)
    {
        if (dayOctets is not (2 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(dayOctets), "CDS day segment is 2 or 3 octets.");
        }

        if (subMillisecondOctets is not (0 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(subMillisecondOctets), "CDS submillisecond segment is 0, 2, or 4 octets.");
        }

        int required = dayOctets + 4 + subMillisecondOctets;
        if (tField.Length < required)
        {
            throw new ArgumentException(
                $"T-field of {tField.Length} octets is shorter than the declared {required} octets.",
                nameof(tField));
        }

        uint days = 0;
        for (int i = 0; i < dayOctets; i++)
        {
            days = (days << 8) | tField[i];
        }

        uint millisecondsOfDay = BinaryPrimitives.ReadUInt32BigEndian(tField[dayOctets..]);

        ulong subMilliseconds = 0;
        for (int i = 0; i < subMillisecondOctets; i++)
        {
            subMilliseconds = (subMilliseconds << 8) | tField[dayOctets + 4 + i];
        }

        return new CdsTime(days, millisecondsOfDay, subMilliseconds, subMillisecondOctets, epoch);
    }

    /// <summary>
    /// Encodes a CDS T-field with the given layout (no P-field), the inverse
    /// of <see cref="DecodeImplicit"/> for the declared profile (L2-TIM-004).
    /// </summary>
    public static void EncodeImplicit(CdsTime time, Span<byte> tField, int dayOctets, int subMillisecondOctets)
    {
        if (dayOctets is not (2 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(dayOctets), "CDS day segment is 2 or 3 octets.");
        }

        if (subMillisecondOctets is not (0 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(subMillisecondOctets), "CDS submillisecond segment is 0, 2, or 4 octets.");
        }

        int required = dayOctets + 4 + subMillisecondOctets;
        if (tField.Length < required)
        {
            throw new ArgumentException($"T-field of {tField.Length} octets cannot hold {required} octets.", nameof(tField));
        }

        if (dayOctets == 2 && time.Days > 0xFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(time), $"Day count {time.Days} does not fit 2 octets.");
        }

        for (int i = dayOctets - 1; i >= 0; i--)
        {
            tField[i] = (byte)(time.Days >> (8 * (dayOctets - 1 - i)));
        }

        BinaryPrimitives.WriteUInt32BigEndian(tField[dayOctets..], time.MillisecondsOfDay);

        for (int i = 0; i < subMillisecondOctets; i++)
        {
            tField[dayOctets + 4 + i] = (byte)(time.SubMilliseconds >> (8 * (subMillisecondOctets - 1 - i)));
        }
    }
}

/// <summary>A decoded CDS time value. Source: CCSDS 301.0-B.</summary>
public readonly record struct CdsTime(
    uint Days,
    uint MillisecondsOfDay,
    ulong SubMilliseconds,
    int SubMillisecondOctets,
    DateTimeOffset Epoch)
{
    public DateTimeOffset ToDateTimeOffset()
    {
        long ticks = Days * TimeSpan.TicksPerDay
            + MillisecondsOfDay * TimeSpan.TicksPerMillisecond;
        ticks += SubMillisecondOctets switch
        {
            2 => (long)SubMilliseconds * TimeSpan.TicksPerMillisecond / 1_000, // microseconds
            4 => (long)(SubMilliseconds * (double)TimeSpan.TicksPerMillisecond / 1_000_000_000_000), // picoseconds
            _ => 0,
        };
        return Epoch + TimeSpan.FromTicks(ticks);
    }
}
