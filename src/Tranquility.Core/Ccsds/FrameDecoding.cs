namespace Tranquility.Core.Ccsds;

/// <summary>Transfer frame families in baseline scope (L1-SDL-001).</summary>
public enum FrameFamily
{
    Tm,
    Aos,
    Uslp,
    Tc,
}

/// <summary>
/// The mission-selected profile for one downlink frame stream. Resolves the
/// baseline halves of TBD-002..004: fixed frame length, optional CRC-16 FECF,
/// no secondary headers / insert zones, M_PDU packet extraction.
/// (USLP baseline constraint: frame length &lt;= 1024 octets so 16-bit FHP
/// sentinels normalize onto the shared M_PDU semantics.)
/// </summary>
public sealed record FrameProfile(
    FrameFamily Family,
    int FrameLength,
    bool HasFecf,
    int? ExpectedSpacecraftId = null);

/// <summary>Machine-readable frame validation failure reasons (L2-SDL-005).</summary>
public enum FrameErrorCode
{
    Truncated,
    LengthMismatch,
    BadVersion,
    FecfMismatch,
    UnexpectedSpacecraftId,
    MalformedDataField,
}

/// <summary>
/// An explicit frame validation diagnostic: failure reason plus frame context
/// (family, SCID/VCID when parseable) so no frame is ever dropped silently.
/// Implements: L2-SDL-005.
/// </summary>
public sealed record FrameValidationError(
    FrameErrorCode Code,
    string Message,
    FrameFamily Family,
    int? SpacecraftId,
    int? VirtualChannelId);

/// <summary>A validated transfer frame ready for packet extraction.</summary>
public sealed record DecodedFrame(
    FrameFamily Family,
    int SpacecraftId,
    int VirtualChannelId,
    long FrameCount,
    ushort FirstHeaderPointer,
    byte[] DataField);

/// <summary>
/// Profile-driven decoders for TM (CCSDS 132.0-B), AOS (CCSDS 732.0-B), and
/// USLP (CCSDS 732.1-B) transfer frames.
/// Implements: L2-SDL-001, L2-SDL-002, L2-SDL-003, L2-SDL-005.
/// </summary>
public static class FrameDecoder
{
    /// <summary>
    /// Validates and decodes one transfer frame per the profile. Returns true
    /// with <paramref name="frame"/> set, or false with
    /// <paramref name="error"/> carrying the diagnostic.
    /// </summary>
    public static bool TryDecode(
        ReadOnlySpan<byte> buffer,
        FrameProfile profile,
        out DecodedFrame? frame,
        out FrameValidationError? error)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF) — the CCSDS frame error
/// control field algorithm shared by TM/AOS/USLP FECF and TC frames.
/// </summary>
public static class Crc16Ccitt
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }
}
