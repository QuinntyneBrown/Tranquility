namespace Tranquility.Core.Cltu;

/// <summary>
/// The declared baseline CLTU coding/synchronization profile (resolves
/// TBD-007): BCH(63,56) code blocks with complemented parity, EB90 start
/// sequence, C5...79 tail sequence, 0x55 fill, randomization off.
/// </summary>
public sealed record CltuProfile(bool RandomizationEnabled = false);

/// <summary>
/// CLTU generation per CCSDS 231.0-B: frames the TC transfer frame into
/// BCH(63,56)-protected code blocks between start and tail sequences.
/// Implements: L2-CMD-004.
/// </summary>
public static class CltuEncoder
{
    public static readonly byte[] StartSequence = [0xEB, 0x90];

    public static readonly byte[] TailSequence = [0xC5, 0xC5, 0xC5, 0xC5, 0xC5, 0xC5, 0xC5, 0x79];

    public const byte FillOctet = 0x55;

    private const int DataOctetsPerBlock = 7;

    public static byte[] Encode(ReadOnlySpan<byte> frame, CltuProfile profile)
    {
        int blocks = (frame.Length + DataOctetsPerBlock - 1) / DataOctetsPerBlock;
        var output = new byte[StartSequence.Length + blocks * 8 + TailSequence.Length];

        StartSequence.CopyTo(output.AsSpan());
        int pos = StartSequence.Length;

        Span<byte> block = stackalloc byte[DataOctetsPerBlock];
        for (int b = 0; b < blocks; b++)
        {
            block.Fill(FillOctet);
            int offset = b * DataOctetsPerBlock;
            int take = Math.Min(DataOctetsPerBlock, frame.Length - offset);
            frame.Slice(offset, take).CopyTo(block);

            if (profile.RandomizationEnabled)
            {
                throw new NotSupportedException("Randomization is disabled in the baseline CLTU profile.");
            }

            block.CopyTo(output.AsSpan(pos));
            output[pos + DataOctetsPerBlock] = BchCodec.ComputeParity(block);
            pos += 8;
        }

        TailSequence.CopyTo(output.AsSpan(pos));
        return output;
    }
}

/// <summary>
/// BCH(63,56) codec per CCSDS 231.0-B: generator x^7+x^6+x^2+1, parity
/// complemented, filler bit 0. Encode produces the parity octet for 7 data
/// octets; Verify checks a full 8-octet code block.
/// </summary>
public static class BchCodec
{
    public static byte ComputeParity(ReadOnlySpan<byte> dataOctets)
    {
        // LFSR over the 56 data bits, MSB first, generator x^7+x^6+x^2+1.
        int register = 0;
        foreach (var octet in dataOctets)
        {
            for (int bit = 7; bit >= 0; bit--)
            {
                int input = (octet >> bit) & 1;
                int feedback = ((register >> 6) & 1) ^ input;
                register = (register << 1) & 0x7F;
                if (feedback != 0)
                {
                    register ^= 0x45; // x^6 + x^2 + 1 (x^7 is the shifted-out bit)
                }
            }
        }

        // 7 parity bits complemented into the high bits, filler bit 0.
        return (byte)((~register & 0x7F) << 1);
    }

    public static bool VerifyBlock(ReadOnlySpan<byte> block)
    {
        if (block.Length != 8)
        {
            return false;
        }

        return ComputeParity(block[..7]) == block[7];
    }
}
