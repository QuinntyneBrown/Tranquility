using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Tranquility.AcceptanceTests.Fixtures;

/// <summary>
/// Default boot mode: the full production pipeline hosted in-process via
/// <see cref="WebApplicationFactory{TEntryPoint}"/> with the shared seeded
/// test configuration.
/// </summary>
public sealed class InProcApiFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // UseSetting (not ConfigureAppConfiguration) so values are visible to
        // configuration reads during app composition under minimal hosting.
        foreach (var (key, value) in TestConfig.Settings())
        {
            builder.UseSetting(key, value);
        }
    }

    /// <summary>Obtains a bearer token via the documented token endpoint.</summary>
    public async Task<string> GetTokenAsync(string username, string password)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/token", new { username, password });
        Assert.True(response.IsSuccessStatusCode,
            $"Token issue for '{username}' failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>An HttpClient authenticated as the given seeded principal.</summary>
    public async Task<HttpClient> AuthenticatedClientAsync(string username, string password)
    {
        var token = await GetTokenAsync(username, password);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public Task<HttpClient> AdminClientAsync() =>
        AuthenticatedClientAsync(TestConfig.AdminUser, TestConfig.AdminPassword);
}
