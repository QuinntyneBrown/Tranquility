using Tranquility.Core.Ccsds;

namespace Tranquility.Core.Tests.Ccsds;

/// <summary>Verifies L2-SDL-001/002 against hand-computed CCSDS 132.0-B vectors.</summary>
public class TmFrameTests
{
    [Fact]
    public void Parse_GoldenFrameHeader_DecodesAllFields()
    {
        // word0 = 0x0AB7: version 0, SCID 0x0AB (171), VCID 3, OCF present
        // MCFC 0x12, VCFC 0x34
        // status = 0x1800: seg length id '11', FHP 0
        var h = TmFrameHeader.Parse([0x0A, 0xB7, 0x12, 0x34, 0x18, 0x00]);

        Assert.Equal(0, h.Version);
        Assert.Equal(171, h.SpacecraftId);
        Assert.Equal(3, h.VirtualChannelId);
        Assert.True(h.HasOperationalControlField);
        Assert.Equal(0x12, h.MasterChannelFrameCount);
        Assert.Equal(0x34, h.VirtualChannelFrameCount);
        Assert.False(h.HasSecondaryHeader);
        Assert.False(h.SynchronizationFlag);
        Assert.False(h.PacketOrderFlag);
        Assert.Equal(3, h.SegmentLengthId);
        Assert.Equal(0, h.FirstHeaderPointer);
    }

    [Fact]
    public void Write_ThenParse_RoundTrips()
    {
        var original = new TmFrameHeader(0, 0x3FF, 7, true, 255, 254, true, true, true, 3, 0x7FF);
        Span<byte> buffer = stackalloc byte[TmFrameHeader.Length];
        original.Write(buffer);

        Assert.Equal(original, TmFrameHeader.Parse(buffer));
    }

    [Fact]
    public void Extractor_WholePacketInOneFrame_IsEmitted()
    {
        byte[] packet = MakePacket(apid: 100, dataLength: 6, fill: 0xAA);
        var extractor = new VirtualChannelPacketExtractor();

        var emitted = extractor.Feed(packet, firstHeaderPointer: 0);

        Assert.Single(emitted);
        Assert.Equal(packet, emitted[0]);
    }

    [Fact]
    public void Extractor_PacketSpanningTwoFrames_IsReassembled()
    {
        byte[] packet = MakePacket(apid: 200, dataLength: 10, fill: 0x5A);
        var extractor = new VirtualChannelPacketExtractor();

        var first = extractor.Feed(packet.AsSpan(0, 9), firstHeaderPointer: 0);
        Assert.Empty(first);

        var second = extractor.Feed(packet.AsSpan(9), firstHeaderPointer: TmFrameHeader.FhpNoPacketStart);
        Assert.Single(second);
        Assert.Equal(packet, second[0]);
    }

    [Fact]
    public void Extractor_ContinuationThenNewPacketInSameFrame_EmitsBoth()
    {
        byte[] packetA = MakePacket(apid: 1, dataLength: 8, fill: 0x11);
        byte[] packetB = MakePacket(apid: 2, dataLength: 4, fill: 0x22);

        var extractor = new VirtualChannelPacketExtractor();
        extractor.Feed(packetA.AsSpan(0, 10), firstHeaderPointer: 0);

        byte[] secondFrame = [.. packetA.AsSpan(10).ToArray(), .. packetB];
        var emitted = extractor.Feed(secondFrame, firstHeaderPointer: (ushort)(packetA.Length - 10));

        Assert.Equal(2, emitted.Count);
        Assert.Equal(packetA, emitted[0]);
        Assert.Equal(packetB, emitted[1]);
    }

    [Fact]
    public void Extractor_IdleDataFrame_EmitsNothingAndKeepsPending()
    {
        byte[] packet = MakePacket(apid: 5, dataLength: 10, fill: 0x33);
        var extractor = new VirtualChannelPacketExtractor();
        extractor.Feed(packet.AsSpan(0, 8), firstHeaderPointer: 0);

        Assert.Empty(extractor.Feed([0x55, 0x55], TmFrameHeader.FhpIdleData));

        var emitted = extractor.Feed(packet.AsSpan(8), TmFrameHeader.FhpNoPacketStart);
        Assert.Single(emitted);
        Assert.Equal(packet, emitted[0]);
    }

    [Fact]
    public void Extractor_Reset_DiscardsPendingAndCounts()
    {
        byte[] packet = MakePacket(apid: 5, dataLength: 10, fill: 0x33);
        var extractor = new VirtualChannelPacketExtractor();
        extractor.Feed(packet.AsSpan(0, 8), firstHeaderPointer: 0);

        extractor.Reset();

        Assert.Equal(1, extractor.DiscardedCount);
        Assert.Empty(extractor.Feed(packet.AsSpan(8), TmFrameHeader.FhpNoPacketStart));
    }

    private static byte[] MakePacket(ushort apid, ushort dataLength, byte fill)
    {
        var header = new SpacePacketHeader(
            0, PacketType.Telemetry, false, apid, SequenceFlags.Unsegmented, 0, (ushort)(dataLength - 1));
        byte[] packet = new byte[header.TotalPacketLength];
        header.Write(packet);
        packet.AsSpan(SpacePacketHeader.Length).Fill(fill);
        return packet;
    }
}
