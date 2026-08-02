namespace Forge.Domain.Lifecycle;

public sealed record RuntimeLifecycleEventId
{
    public Guid Value { get; }

    private RuntimeLifecycleEventId(Guid value)
    {
        Value = value;
    }

    public static RuntimeLifecycleEventId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A runtime lifecycle event ID cannot be empty.", nameof(value));
        }

        return new RuntimeLifecycleEventId(value);
    }

    public override string ToString() => Value.ToString("D");
}
