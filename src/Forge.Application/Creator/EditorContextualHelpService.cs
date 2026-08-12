using Forge.Domain.Authoring;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public sealed record EditorHelpQueryResult(
    IReadOnlyList<EditorHelpTopic> Topics,
    string Answer,
    IReadOnlyList<string> Citations,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class EditorContextualHelpService
{
    public const string DisabledCode = "editor.help.disabled";
    public const string MissingCitationCode = "editor.help.citation_missing";

    public static EditorHelpQueryResult Query(EditorContextualHelpProfile profile, string query,
        bool assisted = false)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Enabled)
            return new([], string.Empty, [], [new(DisabledCode, "Contextual help is disabled by the editor profile.",
                "editorWorkspace/help/enabled")]);
        var topics = EditorContextualHelpCatalog.Search(query);
        var citations = topics.SelectMany(topic => topic.Citations).Distinct(StringComparer.Ordinal).ToArray();
        if (assisted && (!profile.SourcedAssistantEnabled || citations.Length == 0))
            return new(topics, string.Empty, citations,
                [new(MissingCitationCode, "Assisted help requires at least one local citation.",
                    "editorWorkspace/help/citations")]);
        var answer = topics.Count == 0
            ? "Aucun sujet local ne correspond à la recherche."
            : string.Join("\n", topics.Take(3).Select(topic =>
                $"{topic.Title} — {topic.Description} Dépendances: {string.Join(", ", topic.Dependencies)}."));
        return new(topics, answer, citations, []);
    }
}
