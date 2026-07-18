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

    public static byte[] Encode(ReadOnlySpan<byte> frame, CltuProfile profile)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// BCH(63,56) codec per CCSDS 231.0-B: generator x^7+x^6+x^2+1, parity
/// complemented, filler bit 0. Encode appends the parity octet for 7 data
/// octets; Verify checks a full 8-octet code block.
/// </summary>
public static class BchCodec
{
    public static byte ComputeParity(ReadOnlySpan<byte> dataOctets)
    {
        throw new NotImplementedException();
    }

    public static bool VerifyBlock(ReadOnlySpan<byte> block)
    {
        throw new NotImplementedException();
    }
}
