using Tranquility.Core.Alarms;

namespace Tranquility.Application.Processing;

/// <summary>Documented wire names for monitoring/severity states.</summary>
public static class MonitoringNames
{
    public static string Wire(MonitoringResult monitoring) => monitoring switch
    {
        MonitoringResult.Disabled => "",
        MonitoringResult.InLimits => "IN_LIMITS",
        MonitoringResult.Watch => "WATCH",
        MonitoringResult.Warning => "WARNING",
        MonitoringResult.Distress => "DISTRESS",
        MonitoringResult.Critical => "CRITICAL",
        _ => "SEVERE",
    };
}
