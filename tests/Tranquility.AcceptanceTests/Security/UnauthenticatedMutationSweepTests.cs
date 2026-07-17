using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Security;

/// <summary>
/// L2-SEC-002: GIVEN an unauthenticated caller WHEN a state-changing API
/// operation is requested THEN the operation is rejected.
///
/// The sweep enumerates every mapped non-GET route from the server's own
/// EndpointDataSource, so endpoints added by later milestones are covered
/// automatically — this requirement can never silently regress.
/// </summary>
[Requirement("L2-SEC-002")]
public sealed class UnauthenticatedMutationSweepTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    /// <summary>Routes that are anonymous by documented design.</summary>
    private static readonly string[] AnonymousByDesign = ["/auth/token"];

    [Fact]
    public async Task Every_mutation_endpoint_rejects_unauthenticated_callers_with_401()
    {
        var routes = MutationRoutes();
        Assert.True(routes.Count > 0,
            "Expected at least one state-changing endpoint to be mapped (the sweep must have teeth).");

        using var client = fixture.CreateClient();
        foreach (var (method, path) in routes)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            var response = await client.SendAsync(request);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{method} {path} returned {(int)response.StatusCode} for an unauthenticated caller; expected 401.");
            var (type, _) = await JsonApiAssert.IsErrorEnvelopeAsync(response);
            Assert.Equal("UnauthorizedException", type);
        }
    }

    private List<(string Method, string Path)> MutationRoutes()
    {
        var dataSource = fixture.Services.GetRequiredService<EndpointDataSource>();
        var routes = new List<(string, string)>();
        foreach (var endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            var template = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
            if (AnonymousByDesign.Contains(template, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var method in methods.Where(m =>
                         !string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(m, "HEAD", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(m, "OPTIONS", StringComparison.OrdinalIgnoreCase)))
            {
                routes.Add((method, SubstituteRouteValues(template)));
            }
        }

        return routes;
    }

    private static string SubstituteRouteValues(string template)
    {
        var path = template;
        while (path.Contains('{', StringComparison.Ordinal))
        {
            var start = path.IndexOf('{');
            var end = path.IndexOf('}', start);
            path = path[..start] + "sweep" + path[(end + 1)..];
        }

        return path;
    }
}
