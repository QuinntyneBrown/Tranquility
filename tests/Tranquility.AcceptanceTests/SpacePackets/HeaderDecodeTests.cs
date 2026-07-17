using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Ccsds;
using Xunit;

namespace Tranquility.AcceptanceTests.SpacePackets;

/// <summary>
/// L2-SPP-001: GIVEN a valid space packet WHEN parsed THEN header fields used
/// by routing are extracted with expected values. Golden vectors hand-computed
/// from CCSDS 133.0-B primary header layout.
/// </summary>
[Requirement("L2-SPP-001")]
public sealed class HeaderDecodeTests
{
    /// <summary>APID 100, unsegmented, seq 5, data length 8 (field = 7).</summary>
    public static readonly byte[] GoldenPacket =
    [
        0x00, 0x64, 0xC0, 0x05, 0x00, 0x07,
        0x01, 0x02,             // Counter = 258
        0x40, 0x02,             // Temperature raw = 1024, Mode = 2
        0x3F, 0xC0, 0x00, 0x00, // BusVoltage = 1.5f
    ];

    [Fact]
    public void Golden_packet_header_fields_decode_to_expected_values()
    {
        var header = SpacePacketHeader.Parse(GoldenPacket);

        Assert.Equal(0, header.Version);
        Assert.Equal(PacketType.Telemetry, header.Type);
        Assert.False(header.HasSecondaryHeader);
        Assert.Equal(100, header.Apid);
        Assert.Equal(SequenceFlags.Unsegmented, header.SequenceFlags);
        Assert.Equal(5, header.SequenceCount);
        Assert.Equal(7, header.PacketDataLength);
        Assert.Equal(14, header.TotalPacketLength);
        Assert.False(header.IsIdle);
    }

    [Theory]
    // word0: version(3) type(1) secHdr(1) apid(11); word1: flags(2) count(14); word2: length
    [InlineData(new byte[] { 0x1F, 0xFF, 0x40, 0x01, 0x00, 0x00, 0xAA }, 0, 1, true, 0x7FF, SequenceFlags.FirstSegment, 1, 0)]
    [InlineData(new byte[] { 0x08, 0x2A, 0x80, 0x00, 0x00, 0x01, 0x11, 0x22 }, 0, 0, true, 42, SequenceFlags.LastSegment, 0, 1)]
    public void Hand_computed_vectors_decode_field_by_field(
        byte[] packet, int version, int type, bool secHdr, int apid, SequenceFlags flags, int seq, int length)
    {
        var header = SpacePacketHeader.Parse(packet);

        Assert.Equal(version, header.Version);
        Assert.Equal((PacketType)type, header.Type);
        Assert.Equal(secHdr, header.HasSecondaryHeader);
        Assert.Equal(apid, header.Apid);
        Assert.Equal(flags, header.SequenceFlags);
        Assert.Equal(seq, header.SequenceCount);
        Assert.Equal(length, header.PacketDataLength);
    }

    [Fact]
    public void Idle_apid_is_recognized_for_routing()
    {
        var header = SpacePacketHeader.Parse([0x07, 0xFF, 0xC0, 0x00, 0x00, 0x00, 0x00]);
        Assert.True(header.IsIdle);
    }

    [Fact]
    public void Header_write_read_round_trips()
    {
        var original = new SpacePacketHeader(0, PacketType.Telemetry, true, 513,
            SequenceFlags.Unsegmented, 12345, 99);
        Span<byte> buffer = stackalloc byte[SpacePacketHeader.Length];
        original.Write(buffer);
        Assert.Equal(original, SpacePacketHeader.Parse(buffer));
    }
}
