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
        frame = null;
        error = null;

        int minimumHeader = profile.Family switch
        {
            FrameFamily.Tm => TmFrameHeader.Length,
            FrameFamily.Aos => 8,  // 6-octet TFPH + 2-octet M_PDU header
            FrameFamily.Uslp => 9, // 7-octet primary header + 2-octet FHP
            _ => throw new ArgumentException("TC frames are validated on the uplink path.", nameof(profile)),
        };

        if (buffer.Length < minimumHeader)
        {
            error = new FrameValidationError(FrameErrorCode.Truncated,
                $"Frame of {buffer.Length} octets is shorter than the {minimumHeader}-octet {profile.Family} header",
                profile.Family, null, null);
            return false;
        }

        if (buffer.Length != profile.FrameLength)
        {
            error = new FrameValidationError(FrameErrorCode.LengthMismatch,
                $"Frame of {buffer.Length} octets does not match the profile frame length of {profile.FrameLength}",
                profile.Family, null, null);
            return false;
        }

        return profile.Family switch
        {
            FrameFamily.Tm => DecodeTm(buffer, profile, out frame, out error),
            FrameFamily.Aos => DecodeAos(buffer, profile, out frame, out error),
            _ => DecodeUslp(buffer, profile, out frame, out error),
        };
    }

    private static bool DecodeTm(ReadOnlySpan<byte> buffer, FrameProfile profile,
        out DecodedFrame? frame, out FrameValidationError? error)
    {
        frame = null;
        var header = TmFrameHeader.Parse(buffer);

        if (header.Version != 0)
        {
            error = Fail(FrameErrorCode.BadVersion,
                $"TM frame version number {header.Version} is not 0", profile,
                header.SpacecraftId, header.VirtualChannelId);
            return false;
        }

        if (!CheckCommon(buffer, profile, header.SpacecraftId, header.VirtualChannelId, out error))
        {
            return false;
        }

        var dataEnd = buffer.Length - (profile.HasFecf ? 2 : 0);
        frame = new DecodedFrame(FrameFamily.Tm, header.SpacecraftId, header.VirtualChannelId,
            header.VirtualChannelFrameCount, header.FirstHeaderPointer,
            buffer[TmFrameHeader.Length..dataEnd].ToArray());
        return true;
    }

    private static bool DecodeAos(ReadOnlySpan<byte> buffer, FrameProfile profile,
        out DecodedFrame? frame, out FrameValidationError? error)
    {
        frame = null;
        int word0 = (buffer[0] << 8) | buffer[1];
        int version = word0 >> 14;
        int scid = (word0 >> 6) & 0xFF;
        int vcid = word0 & 0x3F;

        if (version != 0b01)
        {
            error = Fail(FrameErrorCode.BadVersion,
                $"AOS frame version number {version} is not 1", profile, scid, vcid);
            return false;
        }

        if (!CheckCommon(buffer, profile, scid, vcid, out error))
        {
            return false;
        }

        long frameCount = (buffer[2] << 16) | (buffer[3] << 8) | buffer[4];
        ushort fhp = (ushort)(((buffer[6] & 0x07) << 8) | buffer[7]);
        var dataEnd = buffer.Length - (profile.HasFecf ? 2 : 0);
        frame = new DecodedFrame(FrameFamily.Aos, scid, vcid, frameCount, fhp,
            buffer[8..dataEnd].ToArray());
        return true;
    }

    private static bool DecodeUslp(ReadOnlySpan<byte> buffer, FrameProfile profile,
        out DecodedFrame? frame, out FrameValidationError? error)
    {
        frame = null;
        uint word0 = (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
        int version = (int)(word0 >> 28);
        int scid = (int)((word0 >> 12) & 0xFFFF);
        int vcid = (int)((word0 >> 5) & 0x3F);

        if (version != 0b1100)
        {
            error = Fail(FrameErrorCode.BadVersion,
                $"USLP frame version number {version} is not 12", profile, scid, vcid);
            return false;
        }

        int declaredLength = ((buffer[4] << 8) | buffer[5]) + 1; // field carries total minus one
        if (declaredLength != profile.FrameLength)
        {
            error = Fail(FrameErrorCode.LengthMismatch,
                $"USLP declared frame length {declaredLength} does not match the profile length {profile.FrameLength}",
                profile, scid, vcid);
            return false;
        }

        if (!CheckCommon(buffer, profile, scid, vcid, out error))
        {
            return false;
        }

        // Normalize 16-bit FHP sentinels onto the shared M_PDU semantics
        // (baseline profile keeps frames <= 1024 octets, so no collision).
        int fhp16 = (buffer[7] << 8) | buffer[8];
        ushort fhp = fhp16 switch
        {
            0xFFFF => TmFrameHeader.FhpNoPacketStart,
            0xFFFE => TmFrameHeader.FhpIdleData,
            _ => (ushort)fhp16,
        };

        var dataEnd = buffer.Length - (profile.HasFecf ? 2 : 0);
        frame = new DecodedFrame(FrameFamily.Uslp, scid, vcid, 0, fhp, buffer[9..dataEnd].ToArray());
        return true;
    }

    private static bool CheckCommon(ReadOnlySpan<byte> buffer, FrameProfile profile,
        int scid, int vcid, out FrameValidationError? error)
    {
        if (profile.HasFecf)
        {
            var expected = Crc16Ccitt.Compute(buffer[..^2]);
            var actual = (ushort)((buffer[^2] << 8) | buffer[^1]);
            if (expected != actual)
            {
                error = Fail(FrameErrorCode.FecfMismatch,
                    $"FECF mismatch: computed 0x{expected:X4}, frame carries 0x{actual:X4}",
                    profile, scid, vcid);
                return false;
            }
        }

        if (profile.ExpectedSpacecraftId is { } expectedScid && scid != expectedScid)
        {
            error = Fail(FrameErrorCode.UnexpectedSpacecraftId,
                $"Frame spacecraft ID {scid} does not match the configured spacecraft ID {expectedScid}",
                profile, scid, vcid);
            return false;
        }

        error = null;
        return true;
    }

    private static FrameValidationError Fail(FrameErrorCode code, string message,
        FrameProfile profile, int? scid, int? vcid) =>
        new(code, message, profile.Family, scid, vcid);
}

/// <summary>
/// CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF) — the CCSDS frame error
/// control field algorithm shared by TM/AOS/USLP FECF and TC frames.
/// </summary>
public static class Crc16Ccitt
{
    public static ushort Compute(ReadOnlySpan<byte> data)
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
}
