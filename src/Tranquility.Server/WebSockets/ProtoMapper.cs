using Google.Protobuf.WellKnownTypes;
using Tranquility.Application.Processing;
using Tranquility.Application.Queries;
using Tranquility.Core.Alarms;
using Tranquility.Wire.Proto;
using Value = Tranquility.Wire.Proto.Value;

namespace Tranquility.Server.WebSockets;

/// <summary>Maps processing-layer events onto the clean-room wire schema.</summary>
public static class ProtoMapper
{
    public static ParameterData ToProto(ParameterBatch batch, IReadOnlySet<string>? filter = null)
    {
        var data = new ParameterData();
        foreach (var value in batch.Values)
        {
            if (filter is null || filter.Contains(value.Parameter.QualifiedName))
            {
                data.Values.Add(ToProto(value));
            }
        }

        return data;
    }

    public static Wire.Proto.ParameterValue ToProto(Core.Decommutation.ParameterValue value) => new()
    {
        Id = new NamedObjectId { Name = value.Parameter.QualifiedName },
        RawValue = ToValue(value.RawValue),
        EngValue = ToValue(value.EngValue),
        AcquisitionTime = Timestamp.FromDateTimeOffset(value.AcquisitionTime),
        GenerationTime = Timestamp.FromDateTimeOffset(value.GenerationTime),
        MonitoringResult = MonitoringName(value.Monitoring),
    };

    public static LinkEvent ToProto(LinkStateEvent e) => new()
    {
        Instance = e.Instance,
        Name = e.Link.Name,
        Status = e.Link.Status.ToString().ToUpperInvariant(),
        Disabled = e.Link.Disabled,
        DataInCount = e.Link.DataInCount,
        DataOutCount = e.Link.DataOutCount,
        DetailedStatus = e.Link.DetailedStatus,
    };

    public static ProcessorInfo ToProto(ProcessorStateEvent e) => new()
    {
        Instance = e.Instance,
        Name = e.Processor.Name,
        Type = e.Processor.Type,
        State = e.Processor.State,
    };

    public static TransferInfo ToProto(TransferEvent e) => new()
    {
        Instance = e.Instance,
        Id = e.Transfer.Id,
        State = e.Transfer.State.ToString().ToUpperInvariant(),
        Bucket = e.Transfer.Bucket,
        ObjectName = e.Transfer.ObjectName,
        RemotePath = e.Transfer.RemotePath,
        Direction = e.Transfer.Direction,
        TotalSize = e.Transfer.TotalSize,
        SizeTransferred = Interlocked.Read(ref e.Transfer.SizeTransferred),
        Reliable = e.Transfer.Reliable,
        TransferType = "CFDP",
        FailureReason = e.Transfer.FailureReason ?? "",
    };

    public static AlarmData ToProto(AlarmEvent e) => new()
    {
        NotificationType = e.Transition.Kind switch
        {
            AlarmTransitionKind.Raised => "RAISED",
            AlarmTransitionKind.SeverityIncreased => "SEVERITY_INCREASED",
            _ => "CLEARED",
        },
        Id = new NamedObjectId { Name = e.Transition.Alarm.Parameter.QualifiedName },
        Severity = MonitoringName(e.Transition.Alarm.Severity),
        TriggerValue = ToProto(e.Transition.Alarm.TriggerValue),
        MostRecentValue = ToProto(e.Transition.Alarm.MostRecentValue),
        ViolationCount = e.Transition.Alarm.ViolationCount,
    };

    public static string MonitoringName(MonitoringResult monitoring) =>
        MonitoringNames.Wire(monitoring);

    /// <summary>Archived value → wire shape (same as live values, L2-ARC-001).</summary>
    public static Wire.Proto.ParameterValue ToProto(Application.Abstractions.ArchivedParameterValue value) => new()
    {
        Id = new NamedObjectId { Name = value.QualifiedName },
        RawValue = ToValue(value.RawValue),
        EngValue = ToValue(value.EngValue),
        AcquisitionTime = Timestamp.FromDateTimeOffset(
            Application.Abstractions.MicroTime.ToDateTimeOffset(value.AcqTimeUs)),
        GenerationTime = Timestamp.FromDateTimeOffset(
            Application.Abstractions.MicroTime.ToDateTimeOffset(value.GenTimeUs)),
        MonitoringResult = value.Monitoring,
    };

    private static Value ToValue(object raw) => raw switch
    {
        double d => new Value { DoubleValue = d },
        float f => new Value { FloatValue = f },
        long l => new Value { Sint64Value = l },
        ulong u => new Value { Uint64Value = u },
        bool b => new Value { BoolValue = b },
        string s => new Value { StringValue = s },
        _ => new Value { StringValue = raw.ToString() ?? string.Empty },
    };
}
