using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public sealed class LoreProjectSourceCatalog
{
    private readonly IReadOnlyDictionary<string, LoreProjectSource> _sources;

    private LoreProjectSourceCatalog(IReadOnlyDictionary<string, LoreProjectSource> sources)
    {
        _sources = sources;
    }

    public int Count => _sources.Count;
    public IReadOnlyCollection<string> SourceIds => _sources.Keys.ToArray();

    public bool TryGet(string sourceId, out LoreProjectSource source) =>
        _sources.TryGetValue((sourceId ?? string.Empty).Trim().ToLowerInvariant(), out source!);

    public static LoreProjectSourceCatalog Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sources = new Dictionary<string, LoreProjectSource>(StringComparer.Ordinal);
        Add(
            $"project:{project.Id}",
            project.Title,
            $"{project.Title} {project.Module.DisplayName} {project.Module.Description}",
            "project.json");
        Add(
            $"module:{project.Module.Id}",
            project.Module.DisplayName,
            $"{project.Module.Slug} {project.Module.Description}",
            "module");
        foreach (var blueprint in project.Blueprints)
        {
            Add(
                $"blueprint:{blueprint.Id}",
                blueprint.DisplayName,
                $"{blueprint.Slug} {blueprint.Kind} {blueprint.Provenance.Source} " +
                $"{blueprint.Provenance.Author} {string.Join(' ', blueprint.Provenance.Modifications)}",
                $"blueprints/{blueprint.Id}");
        }
        foreach (var dialogue in project.Dialogues)
        {
            Add(
                $"dialogue:{dialogue.Id}",
                dialogue.DisplayName,
                $"{dialogue.Slug} {string.Join(' ', dialogue.Nodes.Select(node => node.Text.Fallback))}",
                $"dialogues/{dialogue.Id}");
            foreach (var node in dialogue.Nodes)
            {
                Add(
                    $"dialogue-node:{node.Id}",
                    node.Text.Fallback,
                    $"{node.Text.Key} {node.Text.Fallback} " +
                    string.Join(' ', node.Choices.Select(choice => $"{choice.Text.Key} {choice.Text.Fallback}")),
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}");
            }
        }
        foreach (var quest in project.Quests)
        {
            Add(
                $"quest:{quest.Id}",
                quest.DisplayName,
                $"{quest.Slug} {string.Join(' ', quest.States.Select(state => state.Journal.Fallback))}",
                $"quests/{quest.Id}");
            foreach (var state in quest.States)
            {
                Add(
                    $"quest-state:{state.Id}",
                    state.DisplayName,
                    $"{state.Journal.LocalizationKey} {state.Journal.Fallback} {state.IsTerminal}",
                    $"quests/{quest.Id}/states/{state.Id}");
            }
        }
        return new LoreProjectSourceCatalog(sources);

        void Add(string id, string displayName, string content, string documentPath)
        {
            var normalized = id.ToLowerInvariant();
            sources.Add(normalized, new LoreProjectSource(normalized, displayName, content, documentPath));
        }
    }
}
