namespace Tranquility.Core.Ccsds;

/// <summary>Machine-readable TC frame validation failures (L2-SDL-004).</summary>
public sealed record TcFrameError(FrameErrorCode Code, string Message);

/// <summary>
/// TC transfer frame build + validation per CCSDS 232.0-B. Baseline profile
/// (resolves TBD-005): BD/AD frames, no segment header, one space packet per
/// frame, CRC-16 FECF, maximum 1024 octets.
/// Implements: L2-SDL-004.
/// </summary>
public static class TcTransferFrame
{
    public const int HeaderLength = 5;

    public const int MaxFrameLength = 1024;

    /// <summary>
    /// Builds a TC transfer frame: primary header, data field, FECF.
    /// <paramref name="bypass"/> selects BD (true) vs AD (false) service.
    /// </summary>
    public static byte[] Build(int spacecraftId, int virtualChannelId, byte frameSequenceNumber,
        ReadOnlySpan<byte> data, bool bypass)
    {
        int frameLength = HeaderLength + data.Length + 2;
        if (frameLength > MaxFrameLength)
        {
            throw new ArgumentException($"TC frame length {frameLength} exceeds the {MaxFrameLength}-octet maximum.");
        }

        var frame = new byte[frameLength];

        // Word 0: version(2)=00, bypass(1), ctrl-cmd(1)=0, spare(2)=00, SCID(10).
        int word0 = ((bypass ? 1 : 0) << 13) | (spacecraftId & 0x3FF);
        frame[0] = (byte)(word0 >> 8);
        frame[1] = (byte)word0;

        // Word 1: VCID(6), frame length(10) = total octets minus one.
        int word1 = ((virtualChannelId & 0x3F) << 10) | ((frameLength - 1) & 0x3FF);
        frame[2] = (byte)(word1 >> 8);
        frame[3] = (byte)word1;

        frame[4] = frameSequenceNumber;
        data.CopyTo(frame.AsSpan(HeaderLength));

        var crc = Crc16Ccitt.Compute(frame.AsSpan(0, frameLength - 2));
        frame[^2] = (byte)(crc >> 8);
        frame[^1] = (byte)crc;
        return frame;
    }

    /// <summary>Returns null when the frame passes uplink protocol validation.</summary>
    public static TcFrameError? Validate(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < HeaderLength + 2)
        {
            return new TcFrameError(FrameErrorCode.Truncated,
                $"TC frame of {frame.Length} octets is shorter than the minimum {HeaderLength + 2} octets");
        }

        int declaredLength = (((frame[2] & 0x03) << 8) | frame[3]) + 1;
        if (declaredLength != frame.Length)
        {
            return new TcFrameError(FrameErrorCode.LengthMismatch,
                $"TC frame declared length {declaredLength} does not match the {frame.Length}-octet buffer");
        }

        var expected = Crc16Ccitt.Compute(frame[..^2]);
        var actual = (ushort)((frame[^2] << 8) | frame[^1]);
        if (expected != actual)
        {
            return new TcFrameError(FrameErrorCode.FecfMismatch,
                $"TC FECF mismatch: computed 0x{expected:X4}, frame carries 0x{actual:X4}");
        }

        return null;
    }

    /// <summary>Extracts the spacecraft id from a TC frame header.</summary>
    public static int SpacecraftId(ReadOnlySpan<byte> frame) => ((frame[0] & 0x03) << 8) | frame[1];
}
