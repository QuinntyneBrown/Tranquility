using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Cltu;
using Xunit;

namespace Tranquility.AcceptanceTests.Commanding;

/// <summary>
/// L2-CMD-004: GIVEN a validated TC frame WHEN CLTU generation executes THEN
/// output conforms to the selected CLTU profile (BCH(63,56), EB90 start,
/// C5...79 tail, 0x55 fill, randomization off). The BCH oracle here is an
/// independent LFSR implementation cross-checking the production codec.
/// </summary>
[Requirement("L2-CMD-004")]
public sealed class CltuGenerationTests
{
    [Fact]
    public void Cltu_structure_matches_the_declared_profile()
    {
        byte[] frame = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var cltu = CltuEncoder.Encode(frame, new CltuProfile());

        // start + 2 code blocks (10 data octets -> 2 blocks of 7) + tail
        Assert.Equal(2 + 2 * 8 + 8, cltu.Length);
        Assert.Equal(CltuEncoder.StartSequence, cltu.Take(2).ToArray());
        Assert.Equal(CltuEncoder.TailSequence, cltu.Skip(cltu.Length - 8).ToArray());

        // Reassembled data = frame + 0x55 fill.
        var body = cltu.Skip(2).Take(cltu.Length - 10).ToArray();
        var data = new List<byte>();
        foreach (var block in body.Chunk(8))
        {
            Assert.Equal(IndependentBchParity(block.AsSpan(0, 7)), block[7]);
            data.AddRange(block.Take(7));
        }

        Assert.Equal(frame, data.Take(frame.Length).ToArray());
        Assert.All(data.Skip(frame.Length), fill => Assert.Equal(CltuEncoder.FillOctet, fill));
    }

    [Fact]
    public void Production_bch_codec_agrees_with_the_independent_oracle()
    {
        byte[] data = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77];
        var parity = BchCodec.ComputeParity(data);

        Assert.Equal(IndependentBchParity(data), parity);

        byte[] block = [.. data, parity];
        Assert.True(BchCodec.VerifyBlock(block));
        block[3] ^= 0x10;
        Assert.False(BchCodec.VerifyBlock(block));
    }

    /// <summary>
    /// Independent BCH(63,56): LFSR with generator x^7+x^6+x^2+1 over the 56
    /// data bits, parity complemented, filler bit 0 (CCSDS 231.0-B).
    /// </summary>
    internal static byte IndependentBchParity(ReadOnlySpan<byte> dataOctets)
    {
        var register = 0;
        foreach (var octet in dataOctets)
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                var input = (octet >> bit) & 1;
                var feedback = ((register >> 6) & 1) ^ input;
                register = (register << 1) & 0x7F;
                if (feedback != 0)
                {
                    register ^= 0x45; // x^6 + x^2 + 1
                }
            }
        }

        return (byte)((~register & 0x7F) << 1);
    }
}
