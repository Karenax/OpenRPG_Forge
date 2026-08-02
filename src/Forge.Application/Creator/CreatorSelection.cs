namespace Forge.Application.Creator;

public sealed record CreatorSelection
{
    public CreatorSelectionKind Kind { get; }

    public string Id { get; }

    public string DisplayName { get; }

    public string DocumentPath { get; }

    public CreatorSelection(
        CreatorSelectionKind kind,
        string id,
        string displayName,
        string documentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        Kind = kind;
        Id = id;
        DisplayName = displayName.Trim();
        DocumentPath = documentPath.Trim();
    }
}
