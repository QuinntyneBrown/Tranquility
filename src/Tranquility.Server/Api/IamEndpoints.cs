using Tranquility.Application.Abstractions;

namespace Tranquility.Server.Api;

/// <summary>Documented IAM resource methods (L2-SEC-001), gated on ControlAccess.</summary>
public static class IamEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/iam").RequireAuthorization(SystemPrivileges.ControlAccess);

        group.MapGet("/users", (IIamAdmin iam) =>
            Results.Ok(new { users = iam.ListUsers().Select(ToWire).ToList() }));

        group.MapPost("/users", (CreateUserRequest request, IIamAdmin iam) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                throw new BadRequestServiceException("username and password are required");
            }

            return Results.Ok(ToWire(iam.CreateUser(
                request.Username, request.Password, request.Roles ?? [], request.Superuser ?? false)));
        });

        group.MapPatch("/users/{username}", (string username, UpdateUserRequest request, IIamAdmin iam) =>
            Results.Ok(ToWire(iam.UpdateUser(username, request.Roles, request.Password))));

        group.MapDelete("/users/{username}", (string username, IIamAdmin iam) =>
        {
            iam.DeleteUser(username);
            return Results.Ok();
        });

        group.MapGet("/groups", (IIamAdmin iam) =>
            Results.Ok(new { groups = iam.ListGroups().Select(g => new { name = g.Name, members = g.Members }).ToList() }));

        group.MapPost("/groups", (CreateGroupRequest request, IIamAdmin iam) =>
            Results.Ok(WireGroup(iam.CreateGroup(Require(request.Name, "name"), request.Members ?? []))));

        group.MapGet("/roles", (IIamAdmin iam) =>
            Results.Ok(new { roles = iam.ListRoles().Select(r => new { name = r.Name, privileges = r.Privileges }).ToList() }));

        group.MapPost("/roles", (CreateRoleRequest request, IIamAdmin iam) =>
            Results.Ok(WireRole(iam.CreateRole(Require(request.Name, "name"), request.Privileges ?? []))));

        group.MapGet("/service-accounts", (IIamAdmin iam) =>
            Results.Ok(new { serviceaccounts = iam.ListServiceAccounts().Select(s => new { name = s.Name, roles = s.Roles }).ToList() }));

        group.MapPost("/service-accounts", (CreateServiceAccountRequest request, IIamAdmin iam) =>
            Results.Ok(WireServiceAccount(iam.CreateServiceAccount(Require(request.Name, "name"), request.Roles ?? []))));
    }

    private static string Require(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new BadRequestServiceException($"{field} is required") : value;

    private static object ToWire(IamUser u) => new { username = u.Username, superuser = u.Superuser, roles = u.Roles };

    private static object WireGroup(IamGroup g) => new { name = g.Name, members = g.Members };

    private static object WireRole(IamRole r) => new { name = r.Name, privileges = r.Privileges };

    private static object WireServiceAccount(IamServiceAccount s) => new { name = s.Name, roles = s.Roles };

    public sealed record CreateUserRequest(string? Username, string? Password, string[]? Roles, bool? Superuser);

    public sealed record UpdateUserRequest(string[]? Roles, string? Password);

    public sealed record CreateGroupRequest(string? Name, string[]? Members);

    public sealed record CreateRoleRequest(string? Name, string[]? Privileges);

    public sealed record CreateServiceAccountRequest(string? Name, string[]? Roles);
}
