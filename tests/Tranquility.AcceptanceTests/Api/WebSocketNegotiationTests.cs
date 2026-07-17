using System.Net.WebSockets;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Api;

/// <summary>
/// L2-API-003: GIVEN a WebSocket upgrade request WHEN the client requests
/// Sec-WebSocket-Protocol json (or protobuf) THEN the session is accepted
/// with that encoding. Exercised over real Kestrel TLS (wss), which also
/// demonstrates the WebSocket half of L2-SEC-005.
/// </summary>
[Collection(RealServerCollection.Name)]
[Requirement("L2-API-003")]
public sealed class WebSocketNegotiationTests(SecureKestrelFixture fixture)
{
    private Uri WsUri => new UriBuilder(fixture.BaseAddress) { Scheme = "wss", Path = "/api/websocket" }.Uri;

    [Fact]
    [Requirement("L2-SEC-005")]
    public async Task Json_subprotocol_is_accepted_over_wss()
    {
        using var ws = Create("json");
        await ws.ConnectAsync(WsUri, CancellationToken.None);

        Assert.Equal(WebSocketState.Open, ws.State);
        Assert.Equal("json", ws.SubProtocol);
    }

    [Fact]
    public async Task Protobuf_subprotocol_is_accepted()
    {
        using var ws = Create("protobuf");
        await ws.ConnectAsync(WsUri, CancellationToken.None);

        Assert.Equal(WebSocketState.Open, ws.State);
        Assert.Equal("protobuf", ws.SubProtocol);
    }

    [Fact]
    public async Task Unsupported_subprotocol_offer_is_rejected()
    {
        using var ws = Create("xml");
        await Assert.ThrowsAsync<WebSocketException>(
            () => ws.ConnectAsync(WsUri, CancellationToken.None));
    }

    private ClientWebSocket Create(string subprotocol)
    {
        var ws = new ClientWebSocket();
        ws.Options.AddSubProtocol(subprotocol);
        ws.Options.RemoteCertificateValidationCallback = (_, cert, _, _) =>
            cert is not null &&
            cert.GetCertHashString() == fixture.Certificate.GetCertHashString();
        return ws;
    }
}
