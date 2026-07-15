using System.Buffers.Binary;

namespace Tranquility.Core.Ccsds;

/// <summary>
/// TM transfer frame primary header (6 octets).
/// Implements: L2-SDL-001. Source: CCSDS 132.0-B (TM Space Data Link Protocol).
/// </summary>
public readonly record struct TmFrameHeader(
    byte Version,
    ushort SpacecraftId,
    byte VirtualChannelId,
    bool HasOperationalControlField,
    byte MasterChannelFrameCount,
    byte VirtualChannelFrameCount,
    bool HasSecondaryHeader,
    bool SynchronizationFlag,
    bool PacketOrderFlag,
    byte SegmentLengthId,
    ushort FirstHeaderPointer)
{
    /// <summary>Primary header length in octets.</summary>
    public const int Length = 6;

    /// <summary>First-header-pointer value meaning no packet starts in this frame.</summary>
    public const ushort FhpNoPacketStart = 0x7FF;

    /// <summary>First-header-pointer value meaning the frame contains only idle data.</summary>
    public const ushort FhpIdleData = 0x7FE;

    public static TmFrameHeader Parse(ReadOnlySpan<byte> buffer)
    {
        if (!TryParse(buffer, out var header))
        {
            throw new ArgumentException(
                $"Buffer of {buffer.Length} octets is too short for a {Length}-octet TM transfer frame primary header.",
                nameof(buffer));
        }

        return header;
    }

    public static bool TryParse(ReadOnlySpan<byte> buffer, out TmFrameHeader header)
    {
        if (buffer.Length < Length)
        {
            header = default;
            return false;
        }

        ushort word0 = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        byte mcfc = buffer[2];
        byte vcfc = buffer[3];
        ushort status = BinaryPrimitives.ReadUInt16BigEndian(buffer[4..]);

        header = new TmFrameHeader(
            Version: (byte)(word0 >> 14),
            SpacecraftId: (ushort)((word0 >> 4) & 0x3FF),
            VirtualChannelId: (byte)((word0 >> 1) & 0x7),
            HasOperationalControlField: (word0 & 0x1) != 0,
            MasterChannelFrameCount: mcfc,
            VirtualChannelFrameCount: vcfc,
            HasSecondaryHeader: (status >> 15) != 0,
            SynchronizationFlag: ((status >> 14) & 0x1) != 0,
            PacketOrderFlag: ((status >> 13) & 0x1) != 0,
            SegmentLengthId: (byte)((status >> 11) & 0x3),
            FirstHeaderPointer: (ushort)(status & 0x7FF));
        return true;
    }

    /// <summary>Writes the primary header into <paramref name="buffer"/> (used by tooling and tests).</summary>
    public void Write(Span<byte> buffer)
    {
        if (buffer.Length < Length)
        {
            throw new ArgumentException($"Buffer must be at least {Length} octets.", nameof(buffer));
        }

        ushort word0 = (ushort)(
            (Version << 14)
            | ((SpacecraftId & 0x3FF) << 4)
            | ((VirtualChannelId & 0x7) << 1)
            | (HasOperationalControlField ? 1 : 0));
        ushort status = (ushort)(
            ((HasSecondaryHeader ? 1 : 0) << 15)
            | ((SynchronizationFlag ? 1 : 0) << 14)
            | ((PacketOrderFlag ? 1 : 0) << 13)
            | ((SegmentLengthId & 0x3) << 11)
            | (FirstHeaderPointer & 0x7FF));

        BinaryPrimitives.WriteUInt16BigEndian(buffer, word0);
        buffer[2] = MasterChannelFrameCount;
        buffer[3] = VirtualChannelFrameCount;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[4..], status);
    }
}
