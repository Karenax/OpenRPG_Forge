namespace Forge.Domain.Authoring;

public sealed record EditorCameraProfileId
{
    public Guid Value { get; }

    private EditorCameraProfileId(Guid value)
    {
        Value = value;
    }

    public static EditorCameraProfileId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An editor camera profile ID cannot be empty.", nameof(value));
        }

        return new EditorCameraProfileId(value);
    }

    public static EditorCameraProfileId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid editor camera profile ID.");
    }

    public override string ToString() => Value.ToString("D");
}
