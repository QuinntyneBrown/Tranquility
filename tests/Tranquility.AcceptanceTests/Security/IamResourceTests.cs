using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Security;

/// <summary>
/// L2-SEC-001: GIVEN an authenticated administrator WHEN IAM list/create/
/// update methods are called THEN responses follow documented IAM resource
/// structures — for users, groups, roles, and service accounts. Also proves a
/// created user can authenticate and that role assignment takes effect on the
/// existing authorization policies.
/// </summary>
[Requirement("L2-SEC-001")]
public sealed class IamResourceTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Users_support_list_create_update_and_delete()
    {
        using var admin = await fixture.AdminClientAsync();

        // list includes the seeded principals
        using (var list = JsonDocument.Parse(await admin.GetStringAsync("/api/iam/users")))
        {
            var names = list.RootElement.GetProperty("users").EnumerateArray()
                .Select(u => u.GetProperty("username").GetString()).ToList();
            Assert.Contains(TestConfig.AdminUser, names);
            Assert.Contains(TestConfig.OperatorUser, names);
        }

        // create
        var create = await admin.PostAsJsonAsync("/api/iam/users",
            new { username = "flight", password = "flight-pass-123", roles = new[] { "Operator" } });
        Assert.True(create.IsSuccessStatusCode, $"create returned {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        using (var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync()))
        {
            Assert.Equal("flight", doc.RootElement.GetProperty("username").GetString());
            Assert.Contains("Operator", doc.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }

        // the created user can authenticate and use its Operator privileges
        using var flight = await fixture.AuthenticatedClientAsync("flight", "flight-pass-123");
        var issue = await flight.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode",
            new { args = new { mode = "SAFE" } });
        Assert.True(issue.IsSuccessStatusCode, "Operator-roled user must be able to issue commands");

        // update: strip roles -> command issue is now forbidden
        var update = await admin.PatchAsJsonAsync("/api/iam/users/flight", new { roles = Array.Empty<string>() });
        Assert.True(update.IsSuccessStatusCode, $"update returned {(int)update.StatusCode}");
        using var flightNoRole = await fixture.AuthenticatedClientAsync("flight", "flight-pass-123");
        var denied = await flightNoRole.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode",
            new { args = new { mode = "SAFE" } });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // delete
        var delete = await admin.DeleteAsync("/api/iam/users/flight");
        Assert.True(delete.IsSuccessStatusCode);
        using var afterDelete = JsonDocument.Parse(await admin.GetStringAsync("/api/iam/users"));
        Assert.DoesNotContain(afterDelete.RootElement.GetProperty("users").EnumerateArray(),
            u => u.GetProperty("username").GetString() == "flight");
    }

    [Theory]
    [InlineData("groups", "members")]
    [InlineData("roles", "privileges")]
    [InlineData("service-accounts", "roles")]
    public async Task Iam_collections_support_documented_create_and_list(string collection, string arrayField)
    {
        using var admin = await fixture.AdminClientAsync();
        var name = $"test-{collection}";

        object body = collection switch
        {
            "groups" => new { name, members = new[] { TestConfig.OperatorUser } },
            "roles" => new { name, privileges = new[] { "ControlLinks" } },
            _ => new { name, roles = new[] { "Observer" } },
        };
        var create = await admin.PostAsJsonAsync($"/api/iam/{collection}", body);
        Assert.True(create.IsSuccessStatusCode,
            $"create {collection} returned {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        using (var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync()))
        {
            Assert.Equal(name, doc.RootElement.GetProperty("name").GetString());
            Assert.True(doc.RootElement.TryGetProperty(arrayField, out var arr) && arr.ValueKind == JsonValueKind.Array);
        }

        using var list = JsonDocument.Parse(await admin.GetStringAsync($"/api/iam/{collection}"));
        Assert.Contains(list.RootElement.GetProperty(collection.Replace("-", "")).EnumerateArray(),
            e => e.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Iam_management_requires_the_control_access_privilege()
    {
        using var operatorClient = await fixture.AuthenticatedClientAsync(
            TestConfig.OperatorUser, TestConfig.OperatorPassword);
        var response = await operatorClient.PostAsJsonAsync("/api/iam/users",
            new { username = "nope", password = "x", roles = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
