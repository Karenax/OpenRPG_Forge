namespace Forge.Application.Creator;

public readonly record struct EditorKeymapRevision
{
    public long Value { get; }

    public EditorKeymapRevision(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public EditorKeymapRevision Next() => new(checked(Value + 1));
}
