namespace Forge.Application.Assistant;

public readonly record struct LoreIndexRevision
{
    public long Value { get; }

    public LoreIndexRevision(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A lore index revision cannot be negative.");
        }
        Value = value;
    }

    public LoreIndexRevision Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
