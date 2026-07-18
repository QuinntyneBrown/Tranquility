using Microsoft.Data.Sqlite;
using Tranquility.Application;
using Tranquility.Application.Abstractions;
using Tranquility.Infrastructure.Security;

namespace Tranquility.Infrastructure.Sqlite;

/// <summary>
/// SQLite-backed identity + IAM store (L2-SEC-001). Implements the same
/// <see cref="IIdentityStore"/> contract as the M1 config store — seeded from
/// configuration at first boot with the identical role model — so the M1
/// unauthenticated-mutation sweep and M6 authorization tests keep passing while
/// IAM CRUD becomes live.
/// </summary>
public sealed class SqliteIdentityStore : IIdentityStore, IIamAdmin, IDisposable
{
    private static readonly Dictionary<string, string[]> BuiltInRoles = new(StringComparer.Ordinal)
    {
        ["Administrator"] = [.. SystemPrivileges.All],
        ["Operator"] =
        [
            SystemPrivileges.ControlProcessor, SystemPrivileges.ControlLinks,
            SystemPrivileges.CommandIssue, SystemPrivileges.CommandQueueControl,
            SystemPrivileges.ControlFileTransfers,
        ],
        ["Observer"] = [],
    };

    private readonly SqliteConnection _connection;
    private readonly Lock _gate = new();

    public SqliteIdentityStore(TranquilityOptions options)
    {
        var directory = options.DataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(directory);
        _connection = new SqliteConnection($"Data Source={Path.Combine(directory, "system.db")}");
        _connection.Open();
        Initialize();
        Seed(options);
    }

    private void Initialize()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS iam_user(username TEXT PRIMARY KEY, hash TEXT NOT NULL, superuser INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS iam_user_role(username TEXT NOT NULL, role TEXT NOT NULL, PRIMARY KEY(username, role));
            CREATE TABLE IF NOT EXISTS iam_role(name TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS iam_role_privilege(role TEXT NOT NULL, privilege TEXT NOT NULL, PRIMARY KEY(role, privilege));
            CREATE TABLE IF NOT EXISTS iam_group(name TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS iam_group_member(name TEXT NOT NULL, member TEXT NOT NULL, PRIMARY KEY(name, member));
            CREATE TABLE IF NOT EXISTS iam_service_account(name TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS iam_service_account_role(name TEXT NOT NULL, role TEXT NOT NULL, PRIMARY KEY(name, role));
            """;
        command.ExecuteNonQuery();

        // Register built-in roles idempotently.
        foreach (var (role, privileges) in BuiltInRoles)
        {
            Execute("INSERT OR IGNORE INTO iam_role(name) VALUES ($n)", ("$n", role));
            foreach (var privilege in privileges)
            {
                Execute("INSERT OR IGNORE INTO iam_role_privilege(role, privilege) VALUES ($r, $p)",
                    ("$r", role), ("$p", privilege));
            }
        }
    }

    private void Seed(TranquilityOptions options)
    {
        foreach (var user in options.Security.Users)
        {
            if (ScalarExists("SELECT 1 FROM iam_user WHERE username = $u", ("$u", user.Username)))
            {
                continue;
            }

            Execute("INSERT INTO iam_user(username, hash, superuser) VALUES ($u, $h, $s)",
                ("$u", user.Username), ("$h", user.PasswordHash), ("$s", user.Superuser ? 1 : 0));
            foreach (var role in user.Roles)
            {
                Execute("INSERT OR IGNORE INTO iam_user_role(username, role) VALUES ($u, $r)",
                    ("$u", user.Username), ("$r", role));
            }
        }
    }

    public Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var hash = QueryScalar("SELECT hash FROM iam_user WHERE username = $u", ("$u", username));
            if (hash is null || !PasswordHasher.Verify(password, hash))
            {
                return Task.FromResult<AuthenticatedUser?>(null);
            }

            bool superuser = QueryScalar("SELECT superuser FROM iam_user WHERE username = $u", ("$u", username)) == "1";
            var roles = QueryList("SELECT role FROM iam_user_role WHERE username = $u", ("$u", username));
            var privileges = ResolvePrivileges(roles);
            return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser(username, roles, privileges, superuser));
        }
    }

    public IReadOnlyList<IamUser> ListUsers()
    {
        lock (_gate)
        {
            return QueryList("SELECT username FROM iam_user ORDER BY username")
                .Select(u => new IamUser(u, QueryScalar("SELECT superuser FROM iam_user WHERE username = $u", ("$u", u)) == "1",
                    QueryList("SELECT role FROM iam_user_role WHERE username = $u", ("$u", u))))
                .ToList();
        }
    }

    public IamUser CreateUser(string username, string password, IReadOnlyList<string> roles, bool superuser)
    {
        lock (_gate)
        {
            if (ScalarExists("SELECT 1 FROM iam_user WHERE username = $u", ("$u", username)))
            {
                throw new ConflictServiceException($"User '{username}' already exists");
            }

            Execute("INSERT INTO iam_user(username, hash, superuser) VALUES ($u, $h, $s)",
                ("$u", username), ("$h", PasswordHasher.Hash(password, iterations: 1_000)), ("$s", superuser ? 1 : 0));
            foreach (var role in roles)
            {
                Execute("INSERT OR IGNORE INTO iam_user_role(username, role) VALUES ($u, $r)", ("$u", username), ("$r", role));
            }

            return new IamUser(username, superuser, roles);
        }
    }

    public IamUser UpdateUser(string username, IReadOnlyList<string>? roles, string? password)
    {
        lock (_gate)
        {
            if (!ScalarExists("SELECT 1 FROM iam_user WHERE username = $u", ("$u", username)))
            {
                throw new NotFoundServiceException($"User '{username}' not found");
            }

            if (roles is not null)
            {
                Execute("DELETE FROM iam_user_role WHERE username = $u", ("$u", username));
                foreach (var role in roles)
                {
                    Execute("INSERT OR IGNORE INTO iam_user_role(username, role) VALUES ($u, $r)", ("$u", username), ("$r", role));
                }
            }

            if (password is not null)
            {
                Execute("UPDATE iam_user SET hash = $h WHERE username = $u",
                    ("$h", PasswordHasher.Hash(password, iterations: 1_000)), ("$u", username));
            }

            bool superuser = QueryScalar("SELECT superuser FROM iam_user WHERE username = $u", ("$u", username)) == "1";
            return new IamUser(username, superuser,
                QueryList("SELECT role FROM iam_user_role WHERE username = $u", ("$u", username)));
        }
    }

    public void DeleteUser(string username)
    {
        lock (_gate)
        {
            if (!ScalarExists("SELECT 1 FROM iam_user WHERE username = $u", ("$u", username)))
            {
                throw new NotFoundServiceException($"User '{username}' not found");
            }

            Execute("DELETE FROM iam_user_role WHERE username = $u", ("$u", username));
            Execute("DELETE FROM iam_user WHERE username = $u", ("$u", username));
        }
    }

    public IReadOnlyList<IamGroup> ListGroups()
    {
        lock (_gate)
        {
            return QueryList("SELECT name FROM iam_group ORDER BY name")
                .Select(g => new IamGroup(g, QueryList("SELECT member FROM iam_group_member WHERE name = $n", ("$n", g))))
                .ToList();
        }
    }

    public IamGroup CreateGroup(string name, IReadOnlyList<string> members)
    {
        lock (_gate)
        {
            Execute("INSERT OR REPLACE INTO iam_group(name) VALUES ($n)", ("$n", name));
            Execute("DELETE FROM iam_group_member WHERE name = $n", ("$n", name));
            foreach (var member in members)
            {
                Execute("INSERT OR IGNORE INTO iam_group_member(name, member) VALUES ($n, $m)", ("$n", name), ("$m", member));
            }

            return new IamGroup(name, members);
        }
    }

    public IReadOnlyList<IamRole> ListRoles()
    {
        lock (_gate)
        {
            return QueryList("SELECT name FROM iam_role ORDER BY name")
                .Select(r => new IamRole(r, ResolvePrivileges([r])))
                .ToList();
        }
    }

    public IamRole CreateRole(string name, IReadOnlyList<string> privileges)
    {
        lock (_gate)
        {
            Execute("INSERT OR REPLACE INTO iam_role(name) VALUES ($n)", ("$n", name));
            Execute("DELETE FROM iam_role_privilege WHERE role = $n", ("$n", name));
            foreach (var privilege in privileges)
            {
                Execute("INSERT OR IGNORE INTO iam_role_privilege(role, privilege) VALUES ($r, $p)", ("$r", name), ("$p", privilege));
            }

            return new IamRole(name, privileges);
        }
    }

    public IReadOnlyList<IamServiceAccount> ListServiceAccounts()
    {
        lock (_gate)
        {
            return QueryList("SELECT name FROM iam_service_account ORDER BY name")
                .Select(s => new IamServiceAccount(s, QueryList("SELECT role FROM iam_service_account_role WHERE name = $n", ("$n", s))))
                .ToList();
        }
    }

    public IamServiceAccount CreateServiceAccount(string name, IReadOnlyList<string> roles)
    {
        lock (_gate)
        {
            Execute("INSERT OR REPLACE INTO iam_service_account(name) VALUES ($n)", ("$n", name));
            Execute("DELETE FROM iam_service_account_role WHERE name = $n", ("$n", name));
            foreach (var role in roles)
            {
                Execute("INSERT OR IGNORE INTO iam_service_account_role(name, role) VALUES ($n, $r)", ("$n", name), ("$r", role));
            }

            return new IamServiceAccount(name, roles);
        }
    }

    private List<string> ResolvePrivileges(IReadOnlyList<string> roles) =>
        roles.SelectMany(r => QueryList("SELECT privilege FROM iam_role_privilege WHERE role = $r", ("$r", r)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private void Execute(string sql, params (string, object)[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private string? QueryScalar(string sql, params (string, object)[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteScalar()?.ToString();
    }

    private bool ScalarExists(string sql, params (string, object)[] parameters) => QueryScalar(sql, parameters) is not null;

    private List<string> QueryList(string sql, params (string, object)[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    public void Dispose() => _connection.Dispose();
}
