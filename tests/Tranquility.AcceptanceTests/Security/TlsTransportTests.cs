using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Security;

/// <summary>
/// L2-SEC-005: GIVEN a production deployment WHEN clients connect to API
/// endpoints THEN transport is established over TLS. Exercised against real
/// Kestrel with a fixture-pinned certificate; the WebSocket (wss) half of
/// this requirement is exercised once /api/websocket exists (M4).
/// </summary>
[Collection(RealServerCollection.Name)]
[Requirement("L2-SEC-005")]
public sealed class TlsTransportTests(SecureKestrelFixture fixture)
{
    [Fact]
    public async Task Http_api_is_served_over_tls_with_the_configured_certificate()
    {
        Assert.Equal(Uri.UriSchemeHttps, fixture.BaseAddress.Scheme);

        // The client validation callback only accepts the fixture certificate,
        // so a successful call proves the TLS handshake used it.
        using var client = fixture.CreateClient();
        var body = await client.GetStringAsync("/api/instances");

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("instances", out _),
            "TLS-served API must return the documented instances payload.");
    }
}
