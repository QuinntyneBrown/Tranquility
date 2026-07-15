using Tranquility.Core.Ccsds;

namespace Tranquility.Core.Tests.Ccsds;

/// <summary>Verifies L2-SPP-001/002 against hand-computed CCSDS 133.0-B vectors.</summary>
public class SpacePacketHeaderTests
{
    // 0x08 0x64: version 0, type TM, sec hdr present, APID 100
    // 0xC0 0x01: unsegmented, sequence count 1
    // 0x00 0x05: packet data length field 5 => 6-octet data field, 12-octet packet
    private static readonly byte[] GoldenHeader = [0x08, 0x64, 0xC0, 0x01, 0x00, 0x05];

    [Fact]
    public void Parse_GoldenTelemetryHeader_DecodesAllFields()
    {
        var h = SpacePacketHeader.Parse(GoldenHeader);

        Assert.Equal(0, h.Version);
        Assert.Equal(PacketType.Telemetry, h.Type);
        Assert.True(h.HasSecondaryHeader);
        Assert.Equal(100, h.Apid);
        Assert.Equal(SequenceFlags.Unsegmented, h.SequenceFlags);
        Assert.Equal(1, h.SequenceCount);
        Assert.Equal(5, h.PacketDataLength);
        Assert.Equal(12, h.TotalPacketLength);
        Assert.False(h.IsIdle);
    }

    [Fact]
    public void Parse_TelecommandHeader_DecodesTypeAndApid()
    {
        // 0x10 0x2A: version 0, type TC, no sec hdr, APID 0x02A
        // 0x40 0x00: first segment, count 0
        var h = SpacePacketHeader.Parse([0x10, 0x2A, 0x40, 0x00, 0x00, 0x00]);

        Assert.Equal(PacketType.Telecommand, h.Type);
        Assert.False(h.HasSecondaryHeader);
        Assert.Equal(0x2A, h.Apid);
        Assert.Equal(SequenceFlags.FirstSegment, h.SequenceFlags);
    }

    [Fact]
    public void Parse_IdleApid_IsFlaggedIdle()
    {
        var h = SpacePacketHeader.Parse([0x07, 0xFF, 0xC0, 0x00, 0x00, 0x00]);
        Assert.True(h.IsIdle);
    }

    [Fact]
    public void Write_ThenParse_RoundTrips()
    {
        var original = new SpacePacketHeader(0, PacketType.Telecommand, true, 0x7FE, SequenceFlags.LastSegment, 0x3FFF, 0xABCD);
        Span<byte> buffer = stackalloc byte[SpacePacketHeader.Length];
        original.Write(buffer);

        Assert.Equal(original, SpacePacketHeader.Parse(buffer));
    }

    [Fact]
    public void TryParse_ShortBuffer_ReturnsFalse()
    {
        Assert.False(SpacePacketHeader.TryParse([0x08, 0x64], out _));
    }

    [Fact]
    public void SpacePacket_Parse_SlicesDataField()
    {
        byte[] packet = [.. GoldenHeader, 1, 2, 3, 4, 5, 6];
        var p = SpacePacket.Parse(packet);

        Assert.Equal(6, p.DataField.Length);
        Assert.Equal(1, p.DataField[0]);
        Assert.Equal(6, p.DataField[5]);
    }

    [Fact]
    public void SpacePacket_Parse_TruncatedBody_Throws()
    {
        byte[] packet = [.. GoldenHeader, 1, 2];
        Assert.Throws<ArgumentException>(() => SpacePacket.Parse(packet));
    }
}
