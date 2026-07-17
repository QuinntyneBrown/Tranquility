using System.Collections.Concurrent;
using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure.Security;

/// <summary>
/// Append-only in-memory audit sink (M1..M5). M6 replaces this with the
/// SQLite hash-chained store required by L2-SEC-004; the port stays the same.
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public IReadOnlyCollection<AuditEntry> Entries => _entries.ToArray();

    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }
}
