using Tranquility.Core.Decommutation;

namespace Tranquility.Core.Tests.Decommutation;

/// <summary>Verifies the bit-extraction primitive backing L2-SPP-003.</summary>
public class BitReaderTests
{
    private static readonly byte[] Buffer = [0xAC, 0x35]; // 1010_1100 0011_0101

    [Theory]
    [InlineData(0, 3, 0b101ul)]
    [InlineData(3, 5, 0b01100ul)]
    [InlineData(4, 8, 0xC3ul)]
    [InlineData(0, 16, 0xAC35ul)]
    [InlineData(15, 1, 0b1ul)]
    public void ReadUnsigned_GoldenVectors(int offset, int count, ulong expected)
    {
        Assert.Equal(expected, BitReader.ReadUnsigned(Buffer, offset, count));
    }

    [Fact]
    public void ReadUnsigned_64Bits_ReadsFullWord()
    {
        byte[] buffer = [0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88];
        Assert.Equal(0xFFEEDDCCBBAA9988ul, BitReader.ReadUnsigned(buffer, 0, 64));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF }, 0, 8, -1L)]
    [InlineData(new byte[] { 0x80 }, 0, 4, -8L)]
    [InlineData(new byte[] { 0x7F }, 0, 8, 127L)]
    [InlineData(new byte[] { 0xFF, 0xFE }, 0, 16, -2L)]
    public void ReadSigned_TwosComplement(byte[] buffer, int offset, int count, long expected)
    {
        Assert.Equal(expected, BitReader.ReadSigned(buffer, offset, count));
    }

    [Fact]
    public void ReadFloat_Binary32_Decodes()
    {
        // IEEE 754: 1.5f = 0x3FC00000
        Assert.Equal(1.5, BitReader.ReadFloat([0x3F, 0xC0, 0x00, 0x00], 0, 32));
    }

    [Fact]
    public void ReadFloat_Binary64_Decodes()
    {
        // IEEE 754: -2.0 = 0xC000000000000000
        Assert.Equal(-2.0, BitReader.ReadFloat([0xC0, 0, 0, 0, 0, 0, 0, 0], 0, 64));
    }

    [Fact]
    public void ReadUnsigned_PastEndOfBuffer_Throws()
    {
        Assert.Throws<ArgumentException>(() => BitReader.ReadUnsigned(Buffer, 10, 8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void ReadUnsigned_InvalidBitCount_Throws(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BitReader.ReadUnsigned(Buffer, 0, count));
    }
}
