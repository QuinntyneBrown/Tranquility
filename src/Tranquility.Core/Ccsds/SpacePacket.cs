using System.Buffers.Binary;

namespace Tranquility.Core.Ccsds;

/// <summary>
/// CCSDS space packet type (primary header Type bit).
/// Source: CCSDS 133.0-B (Space Packet Protocol).
/// </summary>
public enum PacketType : byte
{
    Telemetry = 0,
    Telecommand = 1,
}

/// <summary>
/// CCSDS space packet sequence flags.
/// Source: CCSDS 133.0-B (Space Packet Protocol).
/// </summary>
public enum SequenceFlags : byte
{
    Continuation = 0b00,
    FirstSegment = 0b01,
    LastSegment = 0b10,
    Unsegmented = 0b11,
}

/// <summary>
/// CCSDS space packet primary header (6 octets).
/// Implements: L2-SPP-001, L2-SPP-002. Source: CCSDS 133.0-B.
/// Pure, allocation-free parsing over byte spans (L2-QLT-002).
/// </summary>
public readonly record struct SpacePacketHeader(
    byte Version,
    PacketType Type,
    bool HasSecondaryHeader,
    ushort Apid,
    SequenceFlags SequenceFlags,
    ushort SequenceCount,
    ushort PacketDataLength)
{
    /// <summary>Primary header length in octets.</summary>
    public const int Length = 6;

    /// <summary>APID reserved for idle packets.</summary>
    public const ushort IdleApid = 0x7FF;

    /// <summary>Total packet length in octets (header + data field).</summary>
    public int TotalPacketLength => Length + PacketDataLength + 1;

    public bool IsIdle => Apid == IdleApid;

    public static SpacePacketHeader Parse(ReadOnlySpan<byte> buffer)
    {
        if (!TryParse(buffer, out var header))
        {
            throw new ArgumentException(
                $"Buffer of {buffer.Length} octets is too short for a {Length}-octet space packet primary header.",
                nameof(buffer));
        }

        return header;
    }

    public static bool TryParse(ReadOnlySpan<byte> buffer, out SpacePacketHeader header)
    {
        if (buffer.Length < Length)
        {
            header = default;
            return false;
        }

        ushort word0 = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        ushort word1 = BinaryPrimitives.ReadUInt16BigEndian(buffer[2..]);
        ushort word2 = BinaryPrimitives.ReadUInt16BigEndian(buffer[4..]);

        header = new SpacePacketHeader(
            Version: (byte)(word0 >> 13),
            Type: (PacketType)((word0 >> 12) & 0x1),
            HasSecondaryHeader: ((word0 >> 11) & 0x1) != 0,
            Apid: (ushort)(word0 & 0x7FF),
            SequenceFlags: (SequenceFlags)((word1 >> 14) & 0x3),
            SequenceCount: (ushort)(word1 & 0x3FFF),
            PacketDataLength: word2);
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
            (Version << 13)
            | ((byte)Type << 12)
            | ((HasSecondaryHeader ? 1 : 0) << 11)
            | (Apid & 0x7FF));
        ushort word1 = (ushort)(((byte)SequenceFlags << 14) | (SequenceCount & 0x3FFF));

        BinaryPrimitives.WriteUInt16BigEndian(buffer, word0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[2..], word1);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[4..], PacketDataLength);
    }
}

/// <summary>
/// A complete CCSDS space packet view over a byte buffer.
/// Source: CCSDS 133.0-B.
/// </summary>
public readonly ref struct SpacePacket
{
    public SpacePacketHeader Header { get; }

    /// <summary>Packet data field (everything after the primary header).</summary>
    public ReadOnlySpan<byte> DataField { get; }

    private SpacePacket(SpacePacketHeader header, ReadOnlySpan<byte> dataField)
    {
        Header = header;
        DataField = dataField;
    }

    public static SpacePacket Parse(ReadOnlySpan<byte> buffer)
    {
        var header = SpacePacketHeader.Parse(buffer);
        if (buffer.Length < header.TotalPacketLength)
        {
            throw new ArgumentException(
                $"Buffer of {buffer.Length} octets is shorter than the declared packet length of {header.TotalPacketLength} octets.",
                nameof(buffer));
        }

        return new SpacePacket(header, buffer.Slice(SpacePacketHeader.Length, header.PacketDataLength + 1));
    }
}
