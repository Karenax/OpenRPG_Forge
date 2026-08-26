namespace Forge.Application.Gameplay;

public sealed record RuntimeActorResourceSnapshot
{
    public string Id { get; }

    public double Current { get; }

    public double Maximum { get; }

    public RuntimeActorResourceSnapshot(string id, double current, double maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!double.IsFinite(current) || !double.IsFinite(maximum) || maximum <= 0 || current < 0 || current > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(current),
                "A live resource requires finite values with 0 <= current <= maximum and maximum > 0.");
        }

        Id = id.Trim();
        Current = current;
        Maximum = maximum;
    }
}
