using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure.Security;

/// <summary>
/// Bounded in-memory audit trail. A durable sink replaces this in the security
/// implementation phase.
/// Implements: L2-SEC-004 (audit logging skeleton).
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly object _gate = new();
    private readonly Queue<AuditEvent> _events = new();
    private readonly int _capacity;

    public InMemoryAuditLog(int capacity = 10_000)
    {
        _capacity = capacity;
    }

    public void Record(AuditEvent auditEvent)
    {
        lock (_gate)
        {
            _events.Enqueue(auditEvent);
            while (_events.Count > _capacity)
            {
                _events.Dequeue();
            }
        }
    }

    public IReadOnlyList<AuditEvent> Recent(int count)
    {
        lock (_gate)
        {
            return _events.Reverse().Take(count).ToArray();
        }
    }
}
