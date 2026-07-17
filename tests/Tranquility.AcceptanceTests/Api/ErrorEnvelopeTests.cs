using System.Net;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Api;

/// <summary>
/// L2-API-004: GIVEN an invalid API request WHEN Tranquility returns an error
/// THEN the body contains `exception.type` and `exception.msg`.
/// </summary>
[Requirement("L2-API-004")]
public sealed class ErrorEnvelopeTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Unknown_instance_returns_404_with_exception_envelope()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/instances/no-such-instance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var (type, msg) = await JsonApiAssert.IsErrorEnvelopeAsync(response);
        Assert.Equal("NotFoundException", type);
        Assert.Contains("no-such-instance", msg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_route_returns_404_with_exception_envelope()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/definitely/not/a/route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }

    [Fact]
    public async Task Unauthenticated_mutation_returns_401_with_exception_envelope()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsync($"/api/instances/{TestConfig.Instance}:stop", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var (type, _) = await JsonApiAssert.IsErrorEnvelopeAsync(response);
        Assert.Equal("UnauthorizedException", type);
    }
}
