using Tranquility.Core.Ccsds;

namespace Tranquility.AcceptanceTests.SpaceDataLink;

/// <summary>
/// Test-side golden frame construction. The CRC here is an independent
/// implementation (bitwise CRC-16/CCITT-FALSE) so it cross-checks the
/// production <c>Crc16Ccitt</c> rather than echoing it.
/// </summary>
public static class FrameBuilder
{
    public static ushort IndependentCrc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    /// <summary>TM frame: 6-octet primary header + data field (+ FECF).</summary>
    public static byte[] Tm(int frameLength, int scid, int vcid, ushort fhp, byte[] payload, bool fecf = true)
    {
        var frame = new byte[frameLength];
        new TmFrameHeader(0, (ushort)scid, (byte)vcid, false, 1, 1, false, false, false, 0, fhp)
            .Write(frame);
        payload.CopyTo(frame, TmFrameHeader.Length);
        if (fecf)
        {
            WriteCrc(frame);
        }

        return frame;
    }

    /// <summary>AOS frame: 6-octet TFPH + 2-octet M_PDU header + data (+ FECF).</summary>
    public static byte[] Aos(int frameLength, int scid, int vcid, ushort fhp, byte[] payload, bool fecf = true)
    {
        var frame = new byte[frameLength];
        var word0 = (0b01 << 14) | ((scid & 0xFF) << 6) | (vcid & 0x3F);
        frame[0] = (byte)(word0 >> 8);
        frame[1] = (byte)word0;
        frame[2] = 0; frame[3] = 0; frame[4] = 1;   // VC frame count = 1
        frame[5] = 0;                                // signaling field
        frame[6] = (byte)((fhp >> 8) & 0x07);        // M_PDU: 5 spare + FHP(11)
        frame[7] = (byte)fhp;
        payload.CopyTo(frame, 8);
        if (fecf)
        {
            WriteCrc(frame);
        }

        return frame;
    }

    /// <summary>USLP frame: 7-octet primary header + 2-octet FHP + data (+ FECF).</summary>
    public static byte[] Uslp(int frameLength, int scid, int vcid, ushort fhp, byte[] payload, bool fecf = true)
    {
        var frame = new byte[frameLength];
        long word0 = (0xCL << 28) | ((long)(scid & 0xFFFF) << 12) | (0L << 11) | ((long)(vcid & 0x3F) << 5) | (0L << 1) | 0L;
        frame[0] = (byte)(word0 >> 24);
        frame[1] = (byte)(word0 >> 16);
        frame[2] = (byte)(word0 >> 8);
        frame[3] = (byte)word0;
        var lengthField = frameLength - 1; // USLP frame length = total octets minus one
        frame[4] = (byte)(lengthField >> 8);
        frame[5] = (byte)lengthField;
        frame[6] = 0;                      // no VC frame count, no OCF
        frame[7] = (byte)(fhp >> 8);
        frame[8] = (byte)fhp;
        payload.CopyTo(frame, 9);
        if (fecf)
        {
            WriteCrc(frame);
        }

        return frame;
    }

    /// <summary>A minimal unsegmented space packet with the given APID and payload.</summary>
    public static byte[] Packet(int apid, byte[] payload)
    {
        var packet = new byte[SpacePacketHeader.Length + payload.Length];
        new SpacePacketHeader(0, PacketType.Telemetry, false, (ushort)apid,
            SequenceFlags.Unsegmented, 1, (ushort)(payload.Length - 1)).Write(packet);
        payload.CopyTo(packet, SpacePacketHeader.Length);
        return packet;
    }

    private static void WriteCrc(byte[] frame)
    {
        var crc = IndependentCrc16(frame.AsSpan(0, frame.Length - 2));
        frame[^2] = (byte)(crc >> 8);
        frame[^1] = (byte)crc;
    }
}
