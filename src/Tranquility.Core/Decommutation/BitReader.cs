namespace Tranquility.Core.Decommutation;

/// <summary>
/// Big-endian bit-field extraction over byte spans. Pure and allocation-free
/// (L2-QLT-002). Implements the extraction primitive for L2-SPP-003.
/// Source: OMG XTCE 1.3 (DataEncoding, most-significant-bit-first default).
/// </summary>
public static class BitReader
{
    /// <summary>
    /// Reads <paramref name="bitCount"/> bits (1..64) starting at absolute
    /// <paramref name="bitOffset"/> as a big-endian unsigned integer.
    /// </summary>
    public static ulong ReadUnsigned(ReadOnlySpan<byte> buffer, int bitOffset, int bitCount)
    {
        if (bitCount is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Bit counts of 1 to 64 are supported.");
        }

        if (bitOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitOffset));
        }

        int lastBit = bitOffset + bitCount;
        if (lastBit > buffer.Length * 8)
        {
            throw new ArgumentException(
                $"Reading {bitCount} bits at offset {bitOffset} exceeds the {buffer.Length * 8}-bit buffer.",
                nameof(buffer));
        }

        ulong result = 0;
        int bitsRemaining = bitCount;
        int byteIndex = bitOffset >> 3;
        int bitInByte = bitOffset & 7;

        while (bitsRemaining > 0)
        {
            int bitsAvailable = 8 - bitInByte;
            int bitsToTake = Math.Min(bitsAvailable, bitsRemaining);
            int shift = bitsAvailable - bitsToTake;
            ulong mask = (1UL << bitsToTake) - 1;
            ulong chunk = ((ulong)buffer[byteIndex] >> shift) & mask;

            result = (result << bitsToTake) | chunk;
            bitsRemaining -= bitsToTake;
            byteIndex++;
            bitInByte = 0;
        }

        return result;
    }

    /// <summary>
    /// Reads a two's-complement signed integer of <paramref name="bitCount"/> bits.
    /// </summary>
    public static long ReadSigned(ReadOnlySpan<byte> buffer, int bitOffset, int bitCount)
    {
        ulong raw = ReadUnsigned(buffer, bitOffset, bitCount);
        if (bitCount == 64)
        {
            return unchecked((long)raw);
        }

        ulong signBit = 1UL << (bitCount - 1);
        return (raw & signBit) == 0
            ? (long)raw
            : unchecked((long)(raw | ~((1UL << bitCount) - 1)));
    }

    /// <summary>Reads an IEEE 754 binary32 or binary64 value (big-endian).</summary>
    public static double ReadFloat(ReadOnlySpan<byte> buffer, int bitOffset, int bitCount)
    {
        return bitCount switch
        {
            32 => BitConverter.UInt32BitsToSingle((uint)ReadUnsigned(buffer, bitOffset, 32)),
            64 => BitConverter.UInt64BitsToDouble(ReadUnsigned(buffer, bitOffset, 64)),
            _ => throw new ArgumentOutOfRangeException(nameof(bitCount), "IEEE 754 floats are 32 or 64 bits."),
        };
    }
}
