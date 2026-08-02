using System.Globalization;
using System.Text;
using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public sealed class CreatorSearchIndex
{
    private readonly CreatorSearchDocument[] _documents;

    private CreatorSearchIndex(IReadOnlyList<CreatorSearchDocument> documents)
    {
        _documents = documents.ToArray();
    }

    public int Count => _documents.Length;

    public static CreatorSearchIndex Build(
        ProjectDefinition project,
        IReadOnlyList<CreatorDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var documents = new List<CreatorSearchDocument>();
        documents.Add(Document(
            CreatorContentKind.Project,
            project.Id.ToString(),
            project.Title,
            "project.json",
            project.Title,
            ["project"],
            CreatorSelectionKind.Project));
        documents.Add(new CreatorSearchDocument(
            CreatorContentKind.Module,
            project.Module.Id.ToString(),
            project.Module.DisplayName,
            "module",
            $"{project.Module.Slug} {project.Module.Description} {project.Module.EntryAreaId} " +
            $"{project.Module.Metadata.Author} {project.Module.Metadata.Version} " +
            $"{project.Module.Metadata.License} {project.Module.Metadata.Classification}",
            ["module", .. project.Module.Metadata.Tags]));
        documents.Add(new CreatorSearchDocument(
            CreatorContentKind.ProjectMetadata,
            project.Module.Metadata.Id.ToString(),
            $"Publication · {project.Module.Metadata.GetDefaultLocalization().Name}",
            "module/metadata",
            $"{project.Module.Metadata.Author} {project.Module.Metadata.Version} " +
            $"{project.Module.Metadata.License} {project.Module.Metadata.Classification} " +
            $"{project.Module.Metadata.ImagePath} " +
            string.Join(' ', project.Module.Metadata.Localizations.Select(localization =>
                $"{localization.Locale} {localization.Name} {localization.Summary} " +
                $"{localization.Description} {localization.Credits}")),
            ["metadata", "publication", .. project.Module.Metadata.Tags],
            selection: new CreatorSelection(
                CreatorSelectionKind.Project,
                project.Id.ToString(),
                project.Title,
                "project.json")));
        foreach (var variable in project.Module.Variables)
        {
            documents.Add(new CreatorSearchDocument(
                CreatorContentKind.ProjectVariable,
                variable.Id.ToString(),
                variable.Key,
                $"module/variables/{variable.Id}",
                $"{variable.Kind} {variable.Scope} {variable.DefaultValue} " +
                $"{variable.Description} {variable.Provenance}",
                ["variable", variable.Kind.ToString(), variable.Scope.ToString()],
                selection: new CreatorSelection(
                    CreatorSelectionKind.Project,
                    project.Id.ToString(),
                    project.Title,
                    "project.json")));
        }

        var blueprintNames = project.Blueprints.ToDictionary(blueprint => blueprint.Id, blueprint => blueprint.DisplayName);
        foreach (var blueprint in project.Blueprints)
        {
            documents.Add(new CreatorSearchDocument(
                CreatorContentKind.Blueprint,
                blueprint.Id.ToString(),
                blueprint.DisplayName,
                $"blueprints/{blueprint.Id}",
                $"{blueprint.Slug} {blueprint.Kind} {blueprint.Id} {blueprint.PaletteOrigin} " +
                $"{blueprint.Provenance.Source} {blueprint.Provenance.Author} " +
                $"{blueprint.Provenance.LicenseSpdx} {blueprint.Provenance.ContentHash} " +
                string.Join(' ', blueprint.Provenance.Modifications),
                [
                    "blueprint",
                    blueprint.Kind.ToString(),
                    blueprint.PaletteOrigin.ToString(),
                    .. (blueprint.IsFavorite ? new[] { "favorite" } : Array.Empty<string>()),
                ],
                $"{blueprint.Provenance.Source} Â· {blueprint.Provenance.Author} Â· {blueprint.Provenance.LicenseSpdx}",
                new CreatorSelection(
                    CreatorSelectionKind.Blueprint,
                    blueprint.Id.ToString(),
                    blueprint.DisplayName,
                    $"blueprints/{blueprint.Id}")));
        }

        foreach (var area in project.Areas)
        {
            documents.Add(Document(
                CreatorContentKind.Area,
                area.Id.ToString(),
                area.DisplayName,
                $"areas/{area.Id}",
                $"{area.Slug} {area.Kind} {area.Dimensions.Width} {area.Dimensions.Depth} {area.Id}",
                ["area", area.Kind.ToString()],
                CreatorSelectionKind.Area));
            foreach (var entity in area.Entities)
            {
                var blueprintName = blueprintNames.GetValueOrDefault(entity.BlueprintId, entity.BlueprintId.ToString());
                var propertyText = string.Join(' ', entity.Overrides.Properties.Select(property =>
                    $"{property.Key} {property.Kind} {property.Value}"));
                documents.Add(Document(
                    CreatorContentKind.Entity,
                    entity.Id.ToString(),
                    $"{blueprintName} · {entity.Id.ToString()[..8]}",
                    $"areas/{area.Id}/entities/{entity.Id}",
                    $"{entity.Id} {entity.BlueprintId} {blueprintName} {string.Join(' ', entity.Tags)} {propertyText}",
                    ["entity", .. entity.Tags],
                    CreatorSelectionKind.Entity));
            }

            foreach (var portal in area.Portals)
            {
                documents.Add(new CreatorSearchDocument(
                    CreatorContentKind.Portal,
                    portal.Id.ToString(),
                    portal.DisplayName,
                    $"areas/{area.Id}/portals/{portal.Id}",
                    $"{portal.Id} {portal.SourceEntityId} {portal.DestinationAreaId} " +
                    $"{portal.DestinationEntityId} {portal.Bidirectional} {portal.PreloadDestination}",
                    ["portal", "transition"],
                    selection: new CreatorSelection(
                        CreatorSelectionKind.Area,
                        area.Id.ToString(),
                        area.DisplayName,
                        $"areas/{area.Id}")));
            }
        }

        foreach (var dialogue in project.Dialogues)
        {
            documents.Add(Document(
                CreatorContentKind.Dialogue,
                dialogue.Id.ToString(),
                dialogue.DisplayName,
                $"dialogues/{dialogue.Id}",
                $"{dialogue.Slug} {dialogue.Id}",
                ["dialogue"],
                CreatorSelectionKind.Dialogue));
            foreach (var node in dialogue.Nodes)
            {
                var nodeText = string.Join(' ',
                    node.Text.Key,
                    node.Text.Fallback,
                    node.SpeakerBlueprintId?.ToString() ?? string.Empty,
                    string.Join(' ', node.OnEnterActions.Select(action => $"{action.Kind} {action.Key} {action.Value}")));
                documents.Add(new CreatorSearchDocument(
                    CreatorContentKind.DialogueNode,
                    node.Id.ToString(),
                    node.Text.Fallback,
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}",
                    nodeText,
                    ["dialogue", "node"],
                    selection: DialogueSelection(dialogue)));
                foreach (var choice in node.Choices)
                {
                    var choiceText = string.Join(' ',
                        choice.Text.Key,
                        choice.Text.Fallback,
                        choice.TargetNodeId?.ToString() ?? string.Empty,
                        string.Join(' ', choice.Conditions.Select(condition =>
                            $"{condition.Kind} {condition.Key} {condition.ExpectedValue}")),
                        string.Join(' ', choice.Actions.Select(action => $"{action.Kind} {action.Key} {action.Value}")));
                    documents.Add(new CreatorSearchDocument(
                        CreatorContentKind.DialogueChoice,
                        choice.Id.ToString(),
                        choice.Text.Fallback,
                        $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}",
                        choiceText,
                        ["dialogue", "choice"],
                        selection: DialogueSelection(dialogue)));
                }
            }
        }

        foreach (var quest in project.Quests)
        {
            documents.Add(Document(
                CreatorContentKind.Quest,
                quest.Id.ToString(),
                quest.DisplayName,
                $"quests/{quest.Id}",
                $"{quest.Slug} {quest.Id}",
                ["quest"],
                CreatorSelectionKind.Quest));
            foreach (var state in quest.States)
            {
                documents.Add(new CreatorSearchDocument(
                    CreatorContentKind.QuestState,
                    state.Id.ToString(),
                    state.DisplayName,
                    $"quests/{quest.Id}/states/{state.Id}",
                    $"{state.Journal.LocalizationKey} {state.Journal.Fallback} {state.IsTerminal}",
                    ["quest", "state"],
                    selection: QuestSelection(quest)));
            }

            foreach (var transition in quest.Transitions)
            {
                var transitionText = string.Join(' ',
                    transition.Id,
                    transition.FromStateId,
                    transition.ToStateId,
                    string.Join(' ', transition.Conditions.Select(condition =>
                        $"{condition.Kind} {condition.Key} {condition.ExpectedValue}")),
                    string.Join(' ', transition.Actions.Select(action => $"{action.Kind} {action.Key} {action.Value}")));
                documents.Add(new CreatorSearchDocument(
                    CreatorContentKind.QuestTransition,
                    transition.Id.ToString(),
                    $"Transition {transition.FromStateId.ToString()[..8]} → {transition.ToStateId.ToString()[..8]}",
                    $"quests/{quest.Id}/transitions/{transition.Id}",
                    transitionText,
                    ["quest", "transition"],
                    selection: QuestSelection(quest)));
            }
        }

        foreach (var diagnostic in diagnostics ?? [])
        {
            documents.Add(new CreatorSearchDocument(
                CreatorContentKind.Diagnostic,
                diagnostic.Id,
                diagnostic.Code,
                diagnostic.DocumentPath,
                $"{diagnostic.Code} {diagnostic.Message} {diagnostic.CorrelationId}",
                ["diagnostic", diagnostic.Severity.ToString()],
                provenance: "validation-derived",
                selection: diagnostic.Target));
        }

        return new CreatorSearchIndex(documents
            .OrderBy(document => document.DocumentPath, StringComparer.Ordinal)
            .ThenBy(document => document.Kind)
            .ThenBy(document => document.Id, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<CreatorSearchResult> Search(CreatorSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Search result limit must be between 1 and 1000.");
        }

        var normalizedText = Normalize(query.Text ?? string.Empty);
        var tokens = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalizedTag = Normalize(query.Tag ?? string.Empty);
        return _documents
            .Where(document => query.Kind is null || document.Kind == query.Kind)
            .Where(document => string.IsNullOrEmpty(normalizedTag) ||
                document.Tags.Any(tag => Normalize(tag) == normalizedTag))
            .Select(document => new CreatorSearchResult(document, Score(document, normalizedText, tokens)))
            .Where(result => tokens.Length == 0 || result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Document.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Document.DocumentPath, StringComparer.Ordinal)
            .Take(query.Limit)
            .ToArray();
    }

    private static int Score(CreatorSearchDocument document, string text, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return 1;
        }

        var display = Normalize(document.DisplayName);
        var path = Normalize(document.DocumentPath);
        var search = Normalize(document.SearchText);
        var tags = Normalize(string.Join(' ', document.Tags));
        var haystack = $"{display} {path} {search} {tags}";
        if (tokens.Any(token => !haystack.Contains(token, StringComparison.Ordinal)))
        {
            return 0;
        }

        var score = display == text ? 120 : display.StartsWith(text, StringComparison.Ordinal) ? 80 : 0;
        if (display.Contains(text, StringComparison.Ordinal)) score += 50;
        score += tokens.Sum(token => display.Contains(token, StringComparison.Ordinal) ? 20 : 0);
        score += tokens.Sum(token => tags.Contains(token, StringComparison.Ordinal) ? 12 : 0);
        score += tokens.Sum(token => path.Contains(token, StringComparison.Ordinal) ? 6 : 0);
        score += tokens.Sum(token => search.Contains(token, StringComparison.Ordinal) ? 4 : 0);
        return score;
    }

    private static CreatorSearchDocument Document(
        CreatorContentKind kind,
        string id,
        string displayName,
        string path,
        string searchText,
        IReadOnlyList<string> tags,
        CreatorSelectionKind selectionKind) =>
        new(
            kind,
            id,
            displayName,
            path,
            searchText,
            tags,
            selection: new CreatorSelection(selectionKind, id, displayName, path));

    private static CreatorSelection DialogueSelection(Forge.Domain.Dialogues.DialogueDefinition dialogue) =>
        new(CreatorSelectionKind.Dialogue, dialogue.Id.ToString(), dialogue.DisplayName, $"dialogues/{dialogue.Id}");

    private static CreatorSelection QuestSelection(Forge.Domain.Quests.QuestDefinition quest) =>
        new(CreatorSelectionKind.Quest, quest.Id.ToString(), quest.DisplayName, $"quests/{quest.Id}");

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWhitespace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalized = char.ToLowerInvariant(character);
            if (char.IsWhiteSpace(normalized))
            {
                if (!previousWhitespace) builder.Append(' ');
                previousWhitespace = true;
            }
            else
            {
                builder.Append(normalized);
                previousWhitespace = false;
            }
        }

        return builder.ToString().Trim();
    }
}
