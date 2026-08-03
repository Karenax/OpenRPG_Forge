namespace Forge.Domain.Authoring;

public sealed record EditorWorkspaceProfileId
{
    public Guid Value { get; }

    private EditorWorkspaceProfileId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Editor profile ID cannot be empty.", nameof(value));
        Value = value;
    }

    public static EditorWorkspaceProfileId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
