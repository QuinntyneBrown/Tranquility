using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure;

/// <summary>Wall-clock implementation of the processing clock port.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
