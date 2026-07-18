namespace Tranquility.Application.Abstractions;

/// <summary>An authenticated principal resolved by the identity store.</summary>
public sealed record AuthenticatedUser(
    string Username,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Privileges,
    bool Superuser);

/// <summary>
/// Identity port (L1-SEC-001). M1 seeds principals from configuration; M9
/// swaps in the SQLite IAM store behind this same contract so authentication
/// behaviour and policies never change.
/// </summary>
public interface IIdentityStore
{
    Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
}

/// <summary>One security-relevant event record (L2-SEC-004).</summary>
public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string Actor,
    string Action,
    string Resource,
    string Outcome,
    string? Detail);

/// <summary>
/// Append-only audit port. Authentication events, authorization denials, and
/// command uplink actions are recorded through this interface.
/// </summary>
public interface IAuditLog
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>Result of walking the audit hash chain.</summary>
public sealed record AuditChainStatus(bool Valid, long Count, long? FirstBrokenSeq);

/// <summary>Read + integrity-verification side of the audit store (L2-SEC-004).</summary>
public interface IAuditQuery
{
    IReadOnlyList<AuditEntry> Query(string? service);

    AuditChainStatus Verify();
}

// ---- IAM administration (L2-SEC-001) ----

public sealed record IamUser(string Username, bool Superuser, IReadOnlyList<string> Roles);

public sealed record IamGroup(string Name, IReadOnlyList<string> Members);

public sealed record IamRole(string Name, IReadOnlyList<string> Privileges);

public sealed record IamServiceAccount(string Name, IReadOnlyList<string> Roles);

/// <summary>CRUD over IAM resources (L2-SEC-001). Backed by the SQLite identity store.</summary>
public interface IIamAdmin
{
    IReadOnlyList<IamUser> ListUsers();

    IamUser CreateUser(string username, string password, IReadOnlyList<string> roles, bool superuser);

    IamUser UpdateUser(string username, IReadOnlyList<string>? roles, string? password);

    void DeleteUser(string username);

    IReadOnlyList<IamGroup> ListGroups();

    IamGroup CreateGroup(string name, IReadOnlyList<string> members);

    IReadOnlyList<IamRole> ListRoles();

    IamRole CreateRole(string name, IReadOnlyList<string> privileges);

    IReadOnlyList<IamServiceAccount> ListServiceAccounts();

    IamServiceAccount CreateServiceAccount(string name, IReadOnlyList<string> roles);
}

/// <summary>System privilege names used by authorization policies (L2-SEC-003).</summary>
public static class SystemPrivileges
{
    public const string ControlAccess = "ControlAccess";
    public const string ControlProcessor = "ControlProcessor";
    public const string ControlLinks = "ControlLinks";
    public const string CommandIssue = "CommandIssue";
    public const string CommandQueueControl = "CommandQueueControl";
    public const string CommandOverride = "CommandOverride";
    public const string ControlArchiving = "ControlArchiving";
    public const string ControlTimeCorrelation = "ControlTimeCorrelation";
    public const string ControlFileTransfers = "ControlFileTransfers";
    public const string ControlCop1 = "ControlCop1";
    public const string ReadAudit = "ReadAudit";

    public static readonly IReadOnlyList<string> All =
    [
        ControlAccess, ControlProcessor, ControlLinks, CommandIssue, CommandQueueControl,
        CommandOverride, ControlArchiving, ControlTimeCorrelation, ControlFileTransfers,
        ControlCop1, ReadAudit,
    ];
}
