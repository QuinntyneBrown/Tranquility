using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Tranquility.Application;
using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure.Sqlite;

/// <summary>
/// Append-only hash-chained audit log (L2-SEC-004): each entry stores
/// SHA-256(prev_hash || canonical_json(entry)); BEFORE UPDATE/DELETE triggers
/// reject mutation as defence-in-depth. Serialized through one connection so
/// the chain stays consistent under concurrency.
/// </summary>
public sealed class SqliteAuditLog : IAuditLog, IAuditQuery, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Lock _gate = new();
    private string _lastHash = new('0', 64);

    public SqliteAuditLog(TranquilityOptions options)
    {
        var directory = options.DataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(directory);
        _connection = new SqliteConnection($"Data Source={Path.Combine(directory, "system.db")}");
        _connection.Open();
        Initialize();
        LoadLastHash();
    }

    private void Initialize()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS audit_log(
                seq INTEGER PRIMARY KEY AUTOINCREMENT,
                ts TEXT NOT NULL, actor TEXT NOT NULL, action TEXT NOT NULL,
                resource TEXT NOT NULL, outcome TEXT NOT NULL, detail TEXT,
                prev_hash TEXT NOT NULL, hash TEXT NOT NULL);
            CREATE TRIGGER IF NOT EXISTS audit_no_update BEFORE UPDATE ON audit_log
                BEGIN SELECT RAISE(ABORT, 'audit_log is append-only'); END;
            CREATE TRIGGER IF NOT EXISTS audit_no_delete BEFORE DELETE ON audit_log
                BEGIN SELECT RAISE(ABORT, 'audit_log is append-only'); END;
            """;
        command.ExecuteNonQuery();
    }

    private void LoadLastHash()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT hash FROM audit_log ORDER BY seq DESC LIMIT 1";
        if (command.ExecuteScalar() is string hash)
        {
            _lastHash = hash;
        }
    }

    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var canonical = Canonical(entry);
            var hash = Hash(_lastHash + canonical);

            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO audit_log(ts, actor, action, resource, outcome, detail, prev_hash, hash)
                VALUES ($ts, $actor, $action, $resource, $outcome, $detail, $prev, $hash)
                """;
            Bind(command, "$ts", entry.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
            Bind(command, "$actor", entry.Actor);
            Bind(command, "$action", entry.Action);
            Bind(command, "$resource", entry.Resource);
            Bind(command, "$outcome", entry.Outcome);
            Bind(command, "$detail", entry.Detail);
            Bind(command, "$prev", _lastHash);
            Bind(command, "$hash", hash);
            command.ExecuteNonQuery();
            _lastHash = hash;
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<AuditEntry> Query(string? service)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT ts, actor, action, resource, outcome, detail FROM audit_log ORDER BY seq";
            using var reader = command.ExecuteReader();
            var entries = new List<AuditEntry>();
            while (reader.Read())
            {
                entries.Add(new AuditEntry(
                    DateTimeOffset.Parse(reader.GetString(0)),
                    reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5)));
            }

            return entries;
        }
    }

    /// <summary>Walks the chain; reports the first sequence whose hash breaks.</summary>
    public AuditChainStatus Verify()
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                "SELECT seq, ts, actor, action, resource, outcome, detail, prev_hash, hash FROM audit_log ORDER BY seq";
            using var reader = command.ExecuteReader();
            var prev = new string('0', 64);
            long count = 0;
            while (reader.Read())
            {
                count++;
                var entry = new AuditEntry(
                    DateTimeOffset.Parse(reader.GetString(1)),
                    reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6));
                var storedPrev = reader.GetString(7);
                var storedHash = reader.GetString(8);
                var expected = Hash(prev + Canonical(entry));
                if (storedPrev != prev || storedHash != expected)
                {
                    return new AuditChainStatus(false, count, reader.GetInt64(0));
                }

                prev = storedHash;
            }

            return new AuditChainStatus(true, count, null);
        }
    }

    private static void Bind(SqliteCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = (object?)value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string Canonical(AuditEntry entry) => JsonSerializer.Serialize(new
    {
        ts = entry.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
        actor = entry.Actor,
        action = entry.Action,
        resource = entry.Resource,
        outcome = entry.Outcome,
        detail = entry.Detail,
    });

    private static string Hash(string input) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    public void Dispose() => _connection.Dispose();
}
