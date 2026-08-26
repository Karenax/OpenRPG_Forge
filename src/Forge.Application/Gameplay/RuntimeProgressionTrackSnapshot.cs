namespace Forge.Application.Gameplay;

public sealed record RuntimeProgressionTrackSnapshot
{
    public string Id { get; }

    public string DisplayName { get; }

    public int Level { get; }

    public int MaximumLevel { get; }

    public double Experience { get; }

    public RuntimeProgressionTrackSnapshot(
        string id,
        string displayName,
        int level,
        int maximumLevel,
        double experience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (maximumLevel <= 0 || level <= 0 || level > maximumLevel ||
            !double.IsFinite(experience) || experience < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level),
                "A live progression track requires 0 < level <= maximumLevel and non-negative finite experience.");
        }

        Id = id.Trim();
        DisplayName = displayName.Trim();
        Level = level;
        MaximumLevel = maximumLevel;
        Experience = experience;
    }
}
