using Forge.Domain.Blueprints;

namespace Forge.Domain.Authoring;

public sealed record EditorDisplayFilter
{
    public IReadOnlyList<BlueprintKind> VisibleKinds { get; }

    public IReadOnlyList<string> RequiredTags { get; }

    public string Author { get; }

    public string State { get; }

    public bool IsolateSelection { get; }

    public bool GhostHidden { get; }

    public EditorDisplayFilter(
        IReadOnlyList<BlueprintKind>? visibleKinds = null,
        IReadOnlyList<string>? requiredTags = null,
        string author = "",
        string state = "",
        bool isolateSelection = false,
        bool ghostHidden = false)
    {
        VisibleKinds = (visibleKinds ?? Enum.GetValues<BlueprintKind>()).Distinct().Order().ToArray();
        RequiredTags = (requiredTags ?? []).Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Author = author.Trim();
        State = state.Trim();
        IsolateSelection = isolateSelection;
        GhostHidden = ghostHidden;
    }

    public static EditorDisplayFilter ShowAll { get; } = new();
}
