using System.Collections.Concurrent;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Processing;

/// <summary>
/// Thread-safe latest-value cache for decommutated parameters.
/// Implements: L2-PAR-001 (current value availability).
/// </summary>
public sealed class ParameterCache
{
    private readonly ConcurrentDictionary<string, ParameterValue> _values = new(StringComparer.Ordinal);

    public void Update(IReadOnlyList<ParameterValue> values)
    {
        foreach (var value in values)
        {
            _values[value.Parameter.QualifiedName] = value;
        }
    }

    public bool TryGet(string qualifiedName, out ParameterValue value) =>
        _values.TryGetValue(qualifiedName, out value!);

    public IReadOnlyCollection<ParameterValue> Snapshot() => _values.Values.ToArray();

    public int Count => _values.Count;
}
