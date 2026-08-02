namespace Forge.Application.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
