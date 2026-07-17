using System.Collections.Concurrent;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Processing;

/// <summary>Latest processed value per parameter qualified name.</summary>
public sealed class ParameterCache
{
    private readonly ConcurrentDictionary<string, ParameterValue> _latest = new(StringComparer.Ordinal);

    public void Update(IReadOnlyList<ParameterValue> values)
    {
        foreach (var value in values)
        {
            _latest[value.Parameter.QualifiedName] = value;
        }
    }

    public ParameterValue? GetLatest(string qualifiedName) => _latest.GetValueOrDefault(qualifiedName);
}
