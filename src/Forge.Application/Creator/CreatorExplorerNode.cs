namespace Forge.Application.Creator;

public sealed record CreatorExplorerNode
{
    public string Key { get; }

    public CreatorExplorerNodeKind Kind { get; }

    public string DisplayName { get; }

    public string DocumentPath { get; }

    public string? TargetId { get; }

    public IReadOnlyList<CreatorExplorerNode> Children { get; }

    public CreatorExplorerNode(
        string key,
        CreatorExplorerNodeKind kind,
        string displayName,
        string documentPath,
        string? targetId = null,
        IReadOnlyList<CreatorExplorerNode>? children = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        Key = key.Trim();
        Kind = kind;
        DisplayName = displayName.Trim();
        DocumentPath = documentPath.Trim();
        TargetId = string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();
        Children = (children ?? []).ToArray();
    }

    public CreatorSelection? ToSelection()
    {
        if (Kind == CreatorExplorerNodeKind.Folder || TargetId is null)
        {
            return null;
        }

        var selectionKind = Kind switch
        {
            CreatorExplorerNodeKind.Project => CreatorSelectionKind.Project,
            CreatorExplorerNodeKind.Area => CreatorSelectionKind.Area,
            CreatorExplorerNodeKind.Entity => CreatorSelectionKind.Entity,
            CreatorExplorerNodeKind.Blueprint => CreatorSelectionKind.Blueprint,
            CreatorExplorerNodeKind.Dialogue => CreatorSelectionKind.Dialogue,
            CreatorExplorerNodeKind.Quest => CreatorSelectionKind.Quest,
            _ => throw new InvalidOperationException($"Unsupported explorer node kind '{Kind}'."),
        };
        return new CreatorSelection(selectionKind, TargetId, DisplayName, DocumentPath);
    }
}
