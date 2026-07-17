using System.Globalization;
using Tranquility.Core.Alarms;
using Tranquility.Core.Mdb;

namespace Tranquility.Core.Decommutation;

/// <summary>
/// Deterministic decommutation engine: matches a packet against the XTCE container
/// hierarchy and extracts, calibrates, and monitors parameter values.
///
/// Pure functions over byte spans; no I/O dependencies (L2-QLT-002, L2-SPP-003).
/// Implements: L2-SPP-003, L2-MDB-002, L2-PAR-001, L2-PAR-002.
/// Source: OMG XTCE 1.3 (container inheritance, restriction criteria, calibrators).
/// </summary>
public sealed class DecommutationEngine
{
    private readonly MissionDatabase _mdb;

    public DecommutationEngine(MissionDatabase mdb)
    {
        _mdb = mdb;
    }

    /// <summary>
    /// Decommutates one packet starting from <paramref name="rootContainer"/>,
    /// descending into the most-derived container whose restriction criteria match.
    /// </summary>
    public DecommutationResult Decommutate(
        ReadOnlySpan<byte> packet,
        SequenceContainer rootContainer,
        DateTimeOffset generationTime,
        DateTimeOffset acquisitionTime)
    {
        var values = new List<ParameterValue>();
        var byName = new Dictionary<string, ParameterValue>(StringComparer.Ordinal);

        int bitOffset = ExtractEntries(packet, rootContainer, 0, values, byName, generationTime, acquisitionTime);
        var container = rootContainer;

        // Descend into derived containers whose restriction criteria hold.
        while (true)
        {
            SequenceContainer? next = null;
            foreach (var candidate in _mdb.GetDerivedContainers(container))
            {
                if (CriteriaMatch(candidate.RestrictionCriteria, byName))
                {
                    next = candidate;
                    break;
                }
            }

            if (next is null)
            {
                break;
            }

            bitOffset = ExtractEntries(packet, next, bitOffset, values, byName, generationTime, acquisitionTime);
            container = next;
        }

        return new DecommutationResult(container, values);
    }

    private static int ExtractEntries(
        ReadOnlySpan<byte> packet,
        SequenceContainer container,
        int bitOffset,
        List<ParameterValue> values,
        Dictionary<string, ParameterValue> byName,
        DateTimeOffset generationTime,
        DateTimeOffset acquisitionTime)
    {
        foreach (var entry in container.Entries)
        {
            if (entry.AbsoluteBitOffset is { } absolute)
            {
                bitOffset = absolute;
            }

            var value = ExtractValue(packet, bitOffset, entry.Parameter, generationTime, acquisitionTime);
            bitOffset += entry.Parameter.Type.Encoding.SizeInBits;
            values.Add(value);
            byName[entry.Parameter.QualifiedName] = value;
        }

        return bitOffset;
    }

    private static ParameterValue ExtractValue(
        ReadOnlySpan<byte> packet,
        int bitOffset,
        Parameter parameter,
        DateTimeOffset generationTime,
        DateTimeOffset acquisitionTime)
    {
        var type = parameter.Type;
        int size = type.Encoding.SizeInBits;

        object raw;
        object eng;
        StaticAlarmRanges? alarm = null;

        switch (type)
        {
            case IntegerParameterType intType:
            {
                if (intType.Signed || (intType.Encoding is IntegerDataEncoding { Encoding: IntegerEncodingType.TwosComplement }))
                {
                    long value = BitReader.ReadSigned(packet, bitOffset, size);
                    raw = value;
                    eng = intType.Calibrator is { } cal ? cal.Apply(value) : value;
                }
                else
                {
                    ulong value = BitReader.ReadUnsigned(packet, bitOffset, size);
                    raw = value;
                    eng = intType.Calibrator is { } cal ? cal.Apply(value) : value;
                }

                alarm = intType.DefaultAlarm;
                break;
            }

            case FloatParameterType floatType:
            {
                if (type.Encoding is FloatDataEncoding)
                {
                    double value = BitReader.ReadFloat(packet, bitOffset, size);
                    raw = value;
                    eng = floatType.Calibrator is { } cal ? cal.Apply(value) : value;
                }
                else
                {
                    // Integer-encoded float (raw counts calibrated to engineering units).
                    ulong value = BitReader.ReadUnsigned(packet, bitOffset, size);
                    raw = value;
                    eng = floatType.Calibrator is { } cal ? cal.Apply(value) : (double)value;
                }

                alarm = floatType.DefaultAlarm;
                break;
            }

            case EnumeratedParameterType enumType:
            {
                long value = enumType.Encoding is IntegerDataEncoding { Encoding: IntegerEncodingType.TwosComplement }
                    ? BitReader.ReadSigned(packet, bitOffset, size)
                    : (long)BitReader.ReadUnsigned(packet, bitOffset, size);
                raw = value;
                eng = enumType.GetLabel(value);
                break;
            }

            default:
                throw new NotSupportedException($"Parameter type {type.GetType().Name} is not supported.");
        }

        var monitoring = MonitoringResult.Disabled;
        if (alarm is not null)
        {
            double? numeric = eng switch
            {
                double d => d,
                long l => l,
                ulong u => u,
                _ => null,
            };
            if (numeric is { } n)
            {
                monitoring = alarm.Evaluate(n);
            }
        }

        return new ParameterValue(parameter, raw, eng, generationTime, acquisitionTime, monitoring);
    }

    private static bool CriteriaMatch(
        IReadOnlyList<RestrictionCriterion> criteria,
        Dictionary<string, ParameterValue> byName)
    {
        if (criteria.Count == 0)
        {
            return false;
        }

        foreach (var criterion in criteria)
        {
            if (!byName.TryGetValue(criterion.Parameter.QualifiedName, out var value))
            {
                return false;
            }

            object comparand = criterion.UseCalibratedValue ? value.EngValue : value.RawValue;
            if (!Compare(comparand, criterion.Operator, criterion.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Compare(object actual, ComparisonOperator op, string expected)
    {
        if (actual is string label)
        {
            int cmp = string.CompareOrdinal(label, expected);
            return Satisfies(cmp, op);
        }

        double actualNumeric = actual switch
        {
            double d => d,
            long l => l,
            ulong u => u,
            _ => throw new NotSupportedException($"Cannot compare value of type {actual.GetType().Name}."),
        };

        if (!double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out double expectedNumeric))
        {
            return false;
        }

        int numericCmp = actualNumeric.CompareTo(expectedNumeric);
        return Satisfies(numericCmp, op);
    }

    private static bool Satisfies(int cmp, ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => cmp == 0,
        ComparisonOperator.NotEqual => cmp != 0,
        ComparisonOperator.LessThan => cmp < 0,
        ComparisonOperator.LessThanOrEqual => cmp <= 0,
        ComparisonOperator.GreaterThan => cmp > 0,
        ComparisonOperator.GreaterThanOrEqual => cmp >= 0,
        _ => false,
    };
}

/// <summary>The outcome of decommutating one packet.</summary>
public sealed record DecommutationResult(
    SequenceContainer MatchedContainer,
    IReadOnlyList<ParameterValue> Values);
