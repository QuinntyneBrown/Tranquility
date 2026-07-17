using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Tranquility.Server;
using Xunit;

namespace Tranquility.AcceptanceTests.Fixtures;

/// <summary>
/// Real-transport boot mode: production pipeline on a real Kestrel socket at
/// <c>https://127.0.0.1:&lt;ephemeral&gt;</c> with a fixture-generated
/// self-signed certificate — no dev-certs dependency, works on Linux CI.
/// Used for TLS, WebSocket-negotiation, and UDP acceptance tests.
/// </summary>
public sealed class SecureKestrelFixture : IAsyncLifetime
{
    private WebApplication? _app;

    public X509Certificate2 Certificate { get; } = CreateEphemeralCertificate();

    public Uri BaseAddress { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _app = TranquilityApp.Build([], builder =>
        {
            builder.Configuration.AddInMemoryCollection(TestConfig.Settings());
            builder.WebHost.ConfigureKestrel(kestrel =>
                kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(Certificate)));
        });
        await _app.StartAsync();

        var address = _app.Urls.First();
        BaseAddress = new Uri(address.Replace("[::]", "127.0.0.1", StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// HttpClient pinned to the fixture certificate: a successful request
    /// proves the TLS handshake completed against our ephemeral cert.
    /// </summary>
    public HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler();
        handler.SslOptions.RemoteCertificateValidationCallback = (_, cert, _, _) =>
            cert is not null && cert.GetCertHashString() == Certificate.GetCertHashString();
        return new HttpClient(handler) { BaseAddress = BaseAddress };
    }

    private static X509Certificate2 CreateEphemeralCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var ephemeral = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

        // Re-import so the private key is usable by SChannel/OpenSSL alike.
        return X509CertificateLoader.LoadPkcs12(
            ephemeral.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }
}

[CollectionDefinition(Name)]
public sealed class RealServerCollection : ICollectionFixture<SecureKestrelFixture>
{
    // Kestrel-backed tests share one serialized collection to avoid port and
    // socket contention.
    public const string Name = "RealServer";
}
