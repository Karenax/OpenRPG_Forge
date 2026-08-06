namespace Forge.Domain.Authoring;

public sealed record EditorKeymapProfileId
{
    public Guid Value { get; }

    private EditorKeymapProfileId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Editor keymap profile ID cannot be empty.", nameof(value));
        Value = value;
    }

    public static EditorKeymapProfileId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
