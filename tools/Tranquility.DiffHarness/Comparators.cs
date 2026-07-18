using System.Text.Json;

namespace Tranquility.DiffHarness;

/// <summary>The observable surfaces compared for equivalence (L2-DIF-002).</summary>
public enum CompareSurface
{
    ParameterValues,
    EngineeringConversions,
    Timestamps,
    AlarmStates,
    CommandHistory,
    ApiResponses,
}

/// <summary>Triage classification for a divergence (L2-DIF-003).</summary>
public enum TriageClass
{
    TranquilityDefect,
    ReferenceOutsideStandard,
    StandardAmbiguity,
    Unclassified,
}

/// <summary>One comparison outcome per surface (equivalence or divergence).</summary>
public sealed record SurfaceResult(CompareSurface Surface, bool Equivalent, string Detail, TriageClass? Triage = null);

/// <summary>
/// Compares one observable surface's SUT and reference JSON payloads. Float
/// engineering values use a relative tolerance; timestamps are microsecond
/// exact after parse; ids/generated times are masked per surface.
/// </summary>
public static class SurfaceComparator
{
    public static SurfaceResult Compare(CompareSurface surface, string sutJson, string referenceJson, double relTolerance = 1e-9)
    {
        using var sut = JsonDocument.Parse(sutJson);
        using var reference = JsonDocument.Parse(referenceJson);
        var differences = new List<string>();
        CompareElements(sut.RootElement, reference.RootElement, "$", relTolerance, IgnoreMask(surface), differences);
        return differences.Count == 0
            ? new SurfaceResult(surface, true, "equivalent")
            : new SurfaceResult(surface, false, string.Join("; ", differences.Take(10)));
    }

    private static IReadOnlySet<string> IgnoreMask(CompareSurface surface) => surface switch
    {
        // Server-generated identifiers/times are masked (reviewed allowlist).
        CompareSurface.CommandHistory => new HashSet<string>(StringComparer.Ordinal) { "id", "generationTime" },
        CompareSurface.ApiResponses => new HashSet<string>(StringComparer.Ordinal) { "missionTime" },
        _ => new HashSet<string>(StringComparer.Ordinal),
    };

    private static void CompareElements(JsonElement a, JsonElement b, string path, double relTolerance,
        IReadOnlySet<string> mask, List<string> differences)
    {
        if (a.ValueKind != b.ValueKind)
        {
            differences.Add($"{path}: kind {a.ValueKind} vs {b.ValueKind}");
            return;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in a.EnumerateObject())
                {
                    if (mask.Contains(property.Name))
                    {
                        continue;
                    }

                    if (!b.TryGetProperty(property.Name, out var other))
                    {
                        differences.Add($"{path}.{property.Name}: missing on reference");
                        continue;
                    }

                    CompareElements(property.Value, other, $"{path}.{property.Name}", relTolerance, mask, differences);
                }

                break;
            case JsonValueKind.Array:
                var aItems = a.EnumerateArray().ToList();
                var bItems = b.EnumerateArray().ToList();
                if (aItems.Count != bItems.Count)
                {
                    differences.Add($"{path}: array length {aItems.Count} vs {bItems.Count}");
                    break;
                }

                for (var i = 0; i < aItems.Count; i++)
                {
                    CompareElements(aItems[i], bItems[i], $"{path}[{i}]", relTolerance, mask, differences);
                }

                break;
            case JsonValueKind.Number:
                var an = a.GetDouble();
                var bn = b.GetDouble();
                var scale = Math.Max(Math.Abs(an), Math.Abs(bn));
                if (Math.Abs(an - bn) > relTolerance * Math.Max(1.0, scale))
                {
                    differences.Add($"{path}: {an} vs {bn}");
                }

                break;
            case JsonValueKind.String:
                if (a.GetString() != b.GetString())
                {
                    differences.Add($"{path}: '{a.GetString()}' vs '{b.GetString()}'");
                }

                break;
            default:
                if (a.GetRawText() != b.GetRawText())
                {
                    differences.Add($"{path}: {a.GetRawText()} vs {b.GetRawText()}");
                }

                break;
        }
    }
}

/// <summary>
/// Maps divergences onto triage classes from a review overlay; unmatched
/// divergences are Unclassified and fail the gate (L2-DIF-003).
/// </summary>
public sealed class TriageClassifier(IReadOnlyDictionary<string, TriageClass> overlay)
{
    public TriageClass Classify(SurfaceResult divergence)
    {
        foreach (var (pattern, triage) in overlay)
        {
            if (divergence.Detail.Contains(pattern, StringComparison.Ordinal))
            {
                return triage;
            }
        }

        return TriageClass.Unclassified;
    }
}
