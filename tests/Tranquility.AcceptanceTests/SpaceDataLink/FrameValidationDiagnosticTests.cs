using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Ccsds;
using Xunit;

namespace Tranquility.AcceptanceTests.SpaceDataLink;

/// <summary>
/// L2-SDL-005: GIVEN a malformed transfer frame WHEN validation executes THEN
/// an error diagnostic is recorded with failure reason and frame context.
/// </summary>
[Requirement("L2-SDL-005")]
public sealed class FrameValidationDiagnosticTests
{
    private static readonly byte[] Payload = new byte[54];

    [Fact]
    public void Bad_tm_version_yields_diagnostic_with_family_and_channel_context()
    {
        var profile = new FrameProfile(FrameFamily.Tm, 64, HasFecf: true);
        var frame = FrameBuilder.Tm(64, scid: 42, vcid: 1, fhp: 0, new byte[56]);
        frame[0] |= 0xC0; // version bits -> 3
        FixCrc(frame);

        Assert.False(FrameDecoder.TryDecode(frame, profile, out _, out var error));
        Assert.Equal(FrameErrorCode.BadVersion, error!.Code);
        Assert.Equal(FrameFamily.Tm, error.Family);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public void Fecf_mismatch_yields_diagnostic_with_parsed_header_context()
    {
        var profile = new FrameProfile(FrameFamily.Tm, 64, HasFecf: true);
        var frame = FrameBuilder.Tm(64, scid: 42, vcid: 5, fhp: 0, new byte[56]);
        frame[^1] ^= 0xFF; // corrupt the FECF

        Assert.False(FrameDecoder.TryDecode(frame, profile, out _, out var error));
        Assert.Equal(FrameErrorCode.FecfMismatch, error!.Code);
        Assert.Equal(42, error.SpacecraftId);
        Assert.Equal(5, error.VirtualChannelId);
    }

    [Fact]
    public void Wrong_frame_length_yields_length_mismatch_diagnostic()
    {
        var profile = new FrameProfile(FrameFamily.Tm, 64, HasFecf: true);
        var frame = FrameBuilder.Tm(60, scid: 42, vcid: 1, fhp: 0, new byte[52]);

        Assert.False(FrameDecoder.TryDecode(frame, profile, out _, out var error));
        Assert.Equal(FrameErrorCode.LengthMismatch, error!.Code);
    }

    [Fact]
    public void Truncated_aos_frame_yields_truncation_diagnostic()
    {
        var profile = new FrameProfile(FrameFamily.Aos, 64, HasFecf: true);

        Assert.False(FrameDecoder.TryDecode(new byte[4], profile, out _, out var error));
        Assert.Equal(FrameErrorCode.Truncated, error!.Code);
        Assert.Equal(FrameFamily.Aos, error.Family);
    }

    [Fact]
    public void Unexpected_spacecraft_id_yields_diagnostic_naming_the_scid()
    {
        var profile = new FrameProfile(FrameFamily.Tm, 64, HasFecf: true, ExpectedSpacecraftId: 42);
        var frame = FrameBuilder.Tm(64, scid: 99, vcid: 1, fhp: 0, new byte[56]);

        Assert.False(FrameDecoder.TryDecode(frame, profile, out _, out var error));
        Assert.Equal(FrameErrorCode.UnexpectedSpacecraftId, error!.Code);
        Assert.Equal(99, error.SpacecraftId);
        Assert.Contains("99", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Uslp_bad_version_nibble_yields_diagnostic()
    {
        var profile = new FrameProfile(FrameFamily.Uslp, 64, HasFecf: true);
        var frame = FrameBuilder.Uslp(64, scid: 1, vcid: 0, fhp: 0, new byte[53]);
        frame[0] = 0x00; // version nibble no longer 0b1100
        FixCrc(frame);

        Assert.False(FrameDecoder.TryDecode(frame, profile, out _, out var error));
        Assert.Equal(FrameErrorCode.BadVersion, error!.Code);
        Assert.Equal(FrameFamily.Uslp, error.Family);
    }

    private static void FixCrc(byte[] frame)
    {
        var crc = FrameBuilder.IndependentCrc16(frame.AsSpan(0, frame.Length - 2));
        frame[^2] = (byte)(crc >> 8);
        frame[^1] = (byte)crc;
    }
}
