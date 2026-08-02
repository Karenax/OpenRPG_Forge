using Forge.Application.Time;

namespace Forge.Application.Tests.Entities;

internal sealed class FixedClock : IClock
{
    internal FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}
