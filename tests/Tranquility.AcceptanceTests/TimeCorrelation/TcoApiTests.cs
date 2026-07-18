using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.TimeCorrelation;

/// <summary>
/// L2-TIM-001: status fields. L2-TIM-002: set-config / set-coefficients
/// reflected in status. L2-TIM-003: TOF interval add/delete visible.
/// </summary>
public sealed class TcoApiTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    private const string Service = "default";

    [Fact]
    [Requirement("L2-TIM-001")]
    public async Task Status_returns_documented_coefficient_and_sample_fields()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/tco/{TestConfig.Instance}/{Service}/status");

        Assert.True(response.IsSuccessStatusCode,
            $"status returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("coefficients", out _), "status must expose coefficients");
        Assert.True(root.TryGetProperty("deviation", out _), "status must expose deviation");
        Assert.True(root.TryGetProperty("sampleCount", out _), "status must expose sample count");
    }

    [Fact]
    [Requirement("L2-TIM-002")]
    public async Task Set_coefficients_is_reflected_in_subsequent_status()
    {
        using var admin = await fixture.AdminClientAsync();

        var set = await admin.PostAsJsonAsync(
            $"/api/tco/{TestConfig.Instance}/{Service}/coefficients",
            new { gradient = 1.5, offset = 2_000_000.0, obtEpoch = 0 });
        Assert.True(set.IsSuccessStatusCode, $"set-coefficients returned {(int)set.StatusCode}");

        using var doc = JsonDocument.Parse(
            await admin.GetStringAsync($"/api/tco/{TestConfig.Instance}/{Service}/status"));
        var coefficients = doc.RootElement.GetProperty("coefficients");
        Assert.Equal(1.5, coefficients.GetProperty("gradient").GetDouble(), precision: 9);
        Assert.Equal(2_000_000.0, coefficients.GetProperty("offset").GetDouble(), precision: 3);
    }

    [Fact]
    [Requirement("L2-TIM-002")]
    public async Task Set_config_is_reflected_in_subsequent_status()
    {
        using var admin = await fixture.AdminClientAsync();

        var set = await admin.PostAsJsonAsync(
            $"/api/tco/{TestConfig.Instance}/{Service}/config",
            new { accuracy = 250.0, validity = 2500.0 });
        Assert.True(set.IsSuccessStatusCode, $"set-config returned {(int)set.StatusCode}");

        using var doc = JsonDocument.Parse(
            await admin.GetStringAsync($"/api/tco/{TestConfig.Instance}/{Service}/status"));
        var config = doc.RootElement.GetProperty("config");
        Assert.Equal(250.0, config.GetProperty("accuracy").GetDouble(), precision: 3);
        Assert.Equal(2500.0, config.GetProperty("validity").GetDouble(), precision: 3);
    }

    [Fact]
    [Requirement("L2-TIM-002")]
    public async Task Set_coefficients_requires_control_time_correlation_privilege()
    {
        using var observer = await fixture.AuthenticatedClientAsync(
            TestConfig.ObserverUser, TestConfig.ObserverPassword);
        var response = await observer.PostAsJsonAsync(
            $"/api/tco/{TestConfig.Instance}/{Service}/coefficients",
            new { gradient = 1.0, offset = 0.0, obtEpoch = 0 });
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Requirement("L2-TIM-003")]
    public async Task Tof_interval_add_and_delete_are_visible_in_status()
    {
        using var admin = await fixture.AdminClientAsync();

        var add = await admin.PostAsJsonAsync(
            $"/api/tco/{TestConfig.Instance}/{Service}/tof/intervals",
            new
            {
                ertStart = "2026-07-17T00:00:00.000Z",
                ertStop = "2026-07-17T01:00:00.000Z",
                delaySeconds = 1.25,
            });
        Assert.True(add.IsSuccessStatusCode, $"tof add returned {(int)add.StatusCode}: {await add.Content.ReadAsStringAsync()}");

        Assert.Contains(await IntervalsAsync(admin), i =>
            Math.Abs(i.GetProperty("delaySeconds").GetDouble() - 1.25) < 1e-9);

        var delete = await admin.DeleteAsync(
            $"/api/tco/{TestConfig.Instance}/{Service}/tof/intervals?ertStart={Uri.EscapeDataString("2026-07-17T00:00:00.000Z")}");
        Assert.True(delete.IsSuccessStatusCode, $"tof delete returned {(int)delete.StatusCode}");
        Assert.DoesNotContain(await IntervalsAsync(admin), i =>
            Math.Abs(i.GetProperty("delaySeconds").GetDouble() - 1.25) < 1e-9);
    }

    private async Task<List<JsonElement>> IntervalsAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(
            await client.GetStringAsync($"/api/tco/{TestConfig.Instance}/{Service}/status"));
        return doc.RootElement.GetProperty("tofIntervals").EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
