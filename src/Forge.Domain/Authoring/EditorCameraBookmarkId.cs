namespace Forge.Domain.Authoring;

public sealed record EditorCameraBookmarkId
{
    public Guid Value { get; }

    private EditorCameraBookmarkId(Guid value)
    {
        Value = value;
    }

    public static EditorCameraBookmarkId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An editor camera bookmark ID cannot be empty.", nameof(value));
        }

        return new EditorCameraBookmarkId(value);
    }

    public static EditorCameraBookmarkId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid editor camera bookmark ID.");
    }

    public override string ToString() => Value.ToString("D");
}
