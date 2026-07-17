using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Ccsds;
using Xunit;

namespace Tranquility.AcceptanceTests.SpaceDataLink;

/// <summary>
/// L2-SDL-001/002/003: GIVEN TM/AOS/USLP frames compliant with the mission
/// profile WHEN ingested THEN frames are accepted for downstream packet
/// extraction.
/// </summary>
public sealed class FrameIngestTests
{
    private static readonly byte[] Payload = [0xDE, 0xAD, 0xBE, 0xEF];

    [Fact]
    [Requirement("L2-SDL-001")]
    public void Tm_frame_is_accepted_and_its_packet_extracted()
    {
        var packet = FrameBuilder.Packet(apid: 100, Payload);
        var profile = new FrameProfile(FrameFamily.Tm, FrameLength: 64, HasFecf: true, ExpectedSpacecraftId: 42);
        var frame = FrameBuilder.Tm(64, scid: 42, vcid: 1, fhp: 0, Pad(packet, 64 - 6 - 2));

        Assert.True(FrameDecoder.TryDecode(frame, profile, out var decoded, out var error),
            $"Compliant TM frame rejected: {error?.Message}");
        Assert.Equal(42, decoded!.SpacecraftId);
        Assert.Equal(1, decoded.VirtualChannelId);

        var extractor = new VirtualChannelPacketExtractor();
        var packets = extractor.Feed(decoded.DataField, decoded.FirstHeaderPointer);
        Assert.Equal(packet, Assert.Single(NonIdle(packets)));
    }

    [Fact]
    [Requirement("L2-SDL-001")]
    public void Tm_packet_spanning_two_frames_is_reassembled()
    {
        var packet = FrameBuilder.Packet(apid: 100, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        var profile = new FrameProfile(FrameFamily.Tm, FrameLength: 18, HasFecf: true);
        var dataPerFrame = 18 - 6 - 2; // 10 octets of data field per frame

        var frame1 = FrameBuilder.Tm(18, 42, 1, fhp: 0, packet.AsSpan(0, dataPerFrame).ToArray());
        var frame2 = FrameBuilder.Tm(18, 42, 1, fhp: TmFrameHeader.FhpNoPacketStart,
            Pad(packet.AsSpan(dataPerFrame).ToArray(), dataPerFrame));

        var extractor = new VirtualChannelPacketExtractor();
        Assert.True(FrameDecoder.TryDecode(frame1, profile, out var d1, out _));
        Assert.Empty(extractor.Feed(d1!.DataField, d1.FirstHeaderPointer));
        Assert.True(FrameDecoder.TryDecode(frame2, profile, out var d2, out _));
        var packets = extractor.Feed(d2!.DataField, d2.FirstHeaderPointer);

        Assert.Equal(packet, Assert.Single(NonIdle(packets)));
    }

    [Fact]
    [Requirement("L2-SDL-002")]
    public void Aos_frame_is_accepted_and_its_packet_extracted()
    {
        var packet = FrameBuilder.Packet(apid: 200, Payload);
        var profile = new FrameProfile(FrameFamily.Aos, FrameLength: 64, HasFecf: true, ExpectedSpacecraftId: 77);
        var frame = FrameBuilder.Aos(64, scid: 77, vcid: 3, fhp: 0, Pad(packet, 64 - 8 - 2));

        Assert.True(FrameDecoder.TryDecode(frame, profile, out var decoded, out var error),
            $"Compliant AOS frame rejected: {error?.Message}");
        Assert.Equal(77, decoded!.SpacecraftId);
        Assert.Equal(3, decoded.VirtualChannelId);
        Assert.Equal(1, decoded.FrameCount);

        var packets = new VirtualChannelPacketExtractor().Feed(decoded.DataField, decoded.FirstHeaderPointer);
        Assert.Equal(packet, Assert.Single(NonIdle(packets)));
    }

    [Fact]
    [Requirement("L2-SDL-003")]
    public void Uslp_frame_is_accepted_and_its_packet_extracted()
    {
        var packet = FrameBuilder.Packet(apid: 300, Payload);
        var profile = new FrameProfile(FrameFamily.Uslp, FrameLength: 64, HasFecf: true, ExpectedSpacecraftId: 1234);
        var frame = FrameBuilder.Uslp(64, scid: 1234, vcid: 5, fhp: 0, Pad(packet, 64 - 9 - 2));

        Assert.True(FrameDecoder.TryDecode(frame, profile, out var decoded, out var error),
            $"Compliant USLP frame rejected: {error?.Message}");
        Assert.Equal(1234, decoded!.SpacecraftId);
        Assert.Equal(5, decoded.VirtualChannelId);

        var packets = new VirtualChannelPacketExtractor().Feed(decoded.DataField, decoded.FirstHeaderPointer);
        Assert.Equal(packet, Assert.Single(NonIdle(packets)));
    }

    /// <summary>The mission packets: extraction minus the idle padding packets.</summary>
    private static List<byte[]> NonIdle(IReadOnlyList<byte[]> packets) =>
        packets.Where(p => !SpacePacketHeader.Parse(p).IsIdle).ToList();

    /// <summary>Pads with 0xCA idle filler behind a trailing idle-packet-free zone.</summary>
    private static byte[] Pad(byte[] data, int size)
    {
        var padded = new byte[size];
        data.CopyTo(padded, 0);
        // Fill the remainder with an idle packet so the extractor terminates cleanly.
        var remainder = size - data.Length;
        if (remainder >= SpacePacketHeader.Length + 1)
        {
            FrameBuilder.Packet(0x7FF, new byte[remainder - SpacePacketHeader.Length]).CopyTo(padded, data.Length);
        }

        return padded;
    }
}
