using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Ccsds;
using Xunit;

namespace Tranquility.AcceptanceTests.Commanding;

/// <summary>
/// L2-SDL-004: GIVEN TC frames compliant with the mission profile WHEN
/// processed for uplink THEN frames pass protocol validation.
/// </summary>
[Requirement("L2-SDL-004")]
public sealed class TcFrameValidationTests
{
    [Fact]
    public void Built_frame_passes_uplink_validation()
    {
        byte[] data = [0x10, 0x64, 0xC0, 0x00, 0x00, 0x00, 0x02];
        var frame = TcTransferFrame.Build(spacecraftId: 42, virtualChannelId: 0,
            frameSequenceNumber: 5, data, bypass: false);

        Assert.Equal(TcTransferFrame.HeaderLength + data.Length + 2, frame.Length);
        Assert.Null(TcTransferFrame.Validate(frame));
    }

    [Fact]
    public void Corrupted_fecf_fails_validation_with_a_machine_readable_reason()
    {
        var frame = TcTransferFrame.Build(42, 0, 1, [0xAB, 0xCD], bypass: true);
        frame[^1] ^= 0xFF;

        var error = TcTransferFrame.Validate(frame);
        Assert.NotNull(error);
        Assert.Equal(FrameErrorCode.FecfMismatch, error.Code);
    }

    [Fact]
    public void Truncated_frame_fails_validation()
    {
        var error = TcTransferFrame.Validate([0x00, 0x01]);
        Assert.NotNull(error);
        Assert.Equal(FrameErrorCode.Truncated, error.Code);
    }

    [Fact]
    public void Declared_length_mismatch_fails_validation()
    {
        var frame = TcTransferFrame.Build(42, 0, 1, [0xAB, 0xCD], bypass: false);
        var truncated = frame.Take(frame.Length - 1).ToArray();

        var error = TcTransferFrame.Validate(truncated);
        Assert.NotNull(error);
        Assert.Equal(FrameErrorCode.LengthMismatch, error.Code);
    }
}
