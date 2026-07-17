using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.DataLinks;

/// <summary>
/// L2-LNK-004: GIVEN active link traffic WHEN link status is queried THEN
/// inbound and outbound counters are present and nondecreasing.
/// </summary>
[Requirement("L2-LNK-004")]
public sealed class LinkCounterTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Counters_are_present_and_nondecreasing_under_real_udp_traffic()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);

        var first = await LinkApi.GetLinkAsync(admin, "tm-in");
        var firstIn = first.GetProperty("dataInCount").GetInt64();
        var firstOut = first.GetProperty("dataOutCount").GetInt64();

        await LinkControlTests.SendDatagramsAsync(port, 10);
        await Eventually.Async(async () => await LinkApi.DataInCountAsync(admin) >= firstIn + 10,
            "10 datagrams are reflected in dataInCount");

        var second = await LinkApi.GetLinkAsync(admin, "tm-in");
        Assert.True(second.GetProperty("dataInCount").GetInt64() >= firstIn, "dataInCount nondecreasing");
        Assert.True(second.GetProperty("dataOutCount").GetInt64() >= firstOut, "dataOutCount nondecreasing");
    }
}
