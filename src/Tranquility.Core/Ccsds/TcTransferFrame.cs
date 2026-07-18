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
        throw new NotImplementedException();
    }

    /// <summary>Returns null when the frame passes uplink protocol validation.</summary>
    public static TcFrameError? Validate(ReadOnlySpan<byte> frame)
    {
        throw new NotImplementedException();
    }
}
