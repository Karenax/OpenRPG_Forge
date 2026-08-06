namespace Forge.Application.Assistant;

public readonly record struct AssistantWorkspaceProfileRevision
{
    public long Value { get; }

    public AssistantWorkspaceProfileRevision(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "An assistant profile revision cannot be negative.");
        }
        Value = value;
    }

    public AssistantWorkspaceProfileRevision Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
