using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Ccsds;
using Xunit;

namespace Tranquility.AcceptanceTests.SpacePackets;

/// <summary>
/// L2-SPP-004: GIVEN a packet with invalid structural fields WHEN parse is
/// attempted THEN a processing error is emitted and contains a
/// machine-readable reason.
/// </summary>
[Requirement("L2-SPP-004")]
public sealed class ParseFailureTests
{
    [Fact]
    public void Truncated_header_yields_a_machine_readable_error()
    {
        var error = SpacePacketValidator.Validate([0x00, 0x64, 0xC0]);

        Assert.NotNull(error);
        Assert.Equal(PacketErrorCode.TruncatedHeader, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public void Body_shorter_than_declared_length_yields_a_machine_readable_error()
    {
        // Declares 8 octets of data but carries only 2.
        var error = SpacePacketValidator.Validate([0x00, 0x64, 0xC0, 0x05, 0x00, 0x07, 0x01, 0x02]);

        Assert.NotNull(error);
        Assert.Equal(PacketErrorCode.TruncatedBody, error.Code);
    }

    [Fact]
    public void Unsupported_version_number_yields_a_machine_readable_error()
    {
        // Version bits = 0b001 (invalid for CCSDS 133.0-B issue in force).
        var error = SpacePacketValidator.Validate([0x20, 0x64, 0xC0, 0x05, 0x00, 0x00, 0xAA]);

        Assert.NotNull(error);
        Assert.Equal(PacketErrorCode.UnsupportedVersion, error.Code);
    }

    [Fact]
    public void Valid_packet_passes_validation()
    {
        Assert.Null(SpacePacketValidator.Validate(HeaderDecodeTests.GoldenPacket));
    }
}
