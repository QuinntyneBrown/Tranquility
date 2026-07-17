namespace Tranquility.Core.Ccsds;

/// <summary>Machine-readable packet parse failure reasons (L2-SPP-004).</summary>
public enum PacketErrorCode
{
    /// <summary>Buffer shorter than the 6-octet primary header.</summary>
    TruncatedHeader,

    /// <summary>Buffer shorter than the length declared in the primary header.</summary>
    TruncatedBody,

    /// <summary>Primary header version number is not 0 (CCSDS 133.0-B).</summary>
    UnsupportedVersion,
}

/// <summary>A structured processing error emitted for an unparseable packet.</summary>
public sealed record PacketError(PacketErrorCode Code, string Message);

/// <summary>
/// Structural validation of CCSDS space packets ahead of decommutation.
/// Implements: L2-SPP-004. Source: CCSDS 133.0-B.
/// </summary>
public static class SpacePacketValidator
{
    /// <summary>Returns null when the buffer holds a structurally valid packet.</summary>
    public static PacketError? Validate(ReadOnlySpan<byte> buffer)
    {
        if (!SpacePacketHeader.TryParse(buffer, out var header))
        {
            return new PacketError(PacketErrorCode.TruncatedHeader,
                $"Buffer of {buffer.Length} octets is shorter than the {SpacePacketHeader.Length}-octet primary header");
        }

        if (header.Version != 0)
        {
            return new PacketError(PacketErrorCode.UnsupportedVersion,
                $"Packet version number {header.Version} is not 0 (CCSDS 133.0-B)");
        }

        if (buffer.Length < header.TotalPacketLength)
        {
            return new PacketError(PacketErrorCode.TruncatedBody,
                $"Buffer of {buffer.Length} octets is shorter than the declared total packet length of {header.TotalPacketLength} octets");
        }

        return null;
    }
}
