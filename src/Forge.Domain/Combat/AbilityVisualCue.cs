namespace Forge.Domain.Combat;

public sealed record AbilityVisualCue
{
    public string CueKey { get; }

    public AbilityVisualCue(string cueKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cueKey);
        CueKey = cueKey.Trim();
    }
}
