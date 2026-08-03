using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Forge.Domain.Blueprints;

namespace Forge.Domain.Validation;

public static class ProjectValidator
{
    public const string MissingBlueprintCode = "project.reference.blueprint_missing";
    public const string MissingEntryAreaCode = "project.reference.entry_area_missing";
    public const string MissingDialogueSpeakerBlueprintCode = "project.reference.dialogue_speaker_blueprint_missing";
    public const string DuplicatePortalCode = "area.portal.id_duplicate";
    public const string MissingPortalSourceCode = "area.portal.source_missing";
    public const string MissingPortalDestinationAreaCode = "area.portal.destination_area_missing";
    public const string MissingPortalDestinationEntityCode = "area.portal.destination_entity_missing";
    public const string UnsupportedBlueprintProvenanceSchemaCode = "blueprint.provenance.schema_unsupported";
    public const string InvalidInstalledBlueprintProvenanceCode = "blueprint.provenance.installed_invalid";
    public const string LocalizationDefaultLocaleMismatchCode = "localization.default_locale.mismatch";
    public const string MissingLocalizationKeyCode = "localization.key.missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var diagnostics = new List<DomainDiagnostic>();
        var blueprintIds = project.Blueprints.Select(blueprint => blueprint.Id).ToHashSet();
        var areaIds = project.Areas.Select(area => area.Id).ToHashSet();
        var portalIds = new HashSet<PortalId>();

        foreach (var blueprint in project.Blueprints)
        {
            var path = $"blueprints/{blueprint.Id}/provenance";
            if (blueprint.Provenance.SchemaVersion != BlueprintProvenance.CurrentSchemaVersion)
            {
                diagnostics.Add(new DomainDiagnostic(
                    UnsupportedBlueprintProvenanceSchemaCode,
                    $"Blueprint '{blueprint.Id}' uses unsupported provenance schema {blueprint.Provenance.SchemaVersion}.",
                    $"{path}/schemaVersion"));
            }

            if (blueprint.PaletteOrigin == BlueprintPaletteOrigin.InstalledPack &&
                (!blueprint.Provenance.Source.StartsWith("pack://", StringComparison.Ordinal) ||
                 string.Equals(blueprint.Provenance.LicenseSpdx, "UNLICENSED", StringComparison.OrdinalIgnoreCase) ||
                 blueprint.Provenance.ContentHash.Length != 64 ||
                 blueprint.Provenance.ContentHash.Any(character => !Uri.IsHexDigit(character))))
            {
                diagnostics.Add(new DomainDiagnostic(
                    InvalidInstalledBlueprintProvenanceCode,
                    $"Installed blueprint '{blueprint.Id}' requires a pack:// source, SPDX license and SHA-256 hash.",
                    path));
            }
        }

        if (!areaIds.Contains(project.Module.EntryAreaId))
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingEntryAreaCode,
                $"Module '{project.Module.Id}' references missing entry area '{project.Module.EntryAreaId}'.",
                "module/entryAreaId"));
        }

        foreach (var area in project.Areas)
        {
            for (var index = 0; index < area.Entities.Count; index++)
            {
                var entity = area.Entities[index];
                if (!blueprintIds.Contains(entity.BlueprintId))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingBlueprintCode,
                        $"Entity '{entity.Id}' references missing blueprint '{entity.BlueprintId}'.",
                        $"areas/{area.Id}/entities/{index}/blueprintId"));
                }
            }

            for (var index = 0; index < area.Portals.Count; index++)
            {
                var portal = area.Portals[index];
                var path = $"areas/{area.Id}/portals/{index}";
                if (!portalIds.Add(portal.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        DuplicatePortalCode,
                        $"Portal '{portal.Id}' is declared more than once in the project.",
                        $"{path}/id"));
                }

                if (!area.Entities.Any(entity => entity.Id == portal.SourceEntityId))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingPortalSourceCode,
                        $"Portal '{portal.Id}' references missing source entity '{portal.SourceEntityId}'.",
                        $"{path}/sourceEntityId"));
                }

                var destinationArea = project.Areas.FirstOrDefault(candidate =>
                    candidate.Id == portal.DestinationAreaId);
                if (destinationArea is null)
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingPortalDestinationAreaCode,
                        $"Portal '{portal.Id}' references missing destination area '{portal.DestinationAreaId}'.",
                        $"{path}/destinationAreaId"));
                }
                else if (!destinationArea.Entities.Any(entity => entity.Id == portal.DestinationEntityId))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingPortalDestinationEntityCode,
                        $"Portal '{portal.Id}' references missing destination entity '{portal.DestinationEntityId}'.",
                        $"{path}/destinationEntityId"));
                }
            }
        }

        foreach (var dialogue in project.Dialogues)
        {
            diagnostics.AddRange(DialogueValidator.Validate(dialogue));
            foreach (var node in dialogue.Nodes.Where(node =>
                         node.SpeakerBlueprintId is not null && !blueprintIds.Contains(node.SpeakerBlueprintId)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingDialogueSpeakerBlueprintCode,
                    $"Dialogue node '{node.Id}' references missing speaker blueprint '{node.SpeakerBlueprintId}'.",
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}/speakerBlueprintId"));
            }
        }

        foreach (var quest in project.Quests)
        {
            diagnostics.AddRange(QuestValidator.Validate(quest));
        }

        if (project.Module.Localization.Messages.Count > 0)
        {
            if (!string.Equals(
                    project.Module.Localization.DefaultLocale,
                    project.Module.Metadata.DefaultLocale,
                    StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DomainDiagnostic(
                    LocalizationDefaultLocaleMismatchCode,
                    "The localization catalog default locale must match the module metadata default locale.",
                    "module/localization/defaultLocale"));
            }

            var catalogKeys = project.Module.Localization.Messages.Select(message => message.Key).ToHashSet(StringComparer.Ordinal);
            var requiredKeys = project.Dialogues.SelectMany(dialogue => dialogue.Nodes)
                .SelectMany(node => new[] { node.Text.Key }.Concat(node.Choices.Select(choice => choice.Text.Key)))
                .Concat(project.Quests.SelectMany(quest => quest.States).Select(state => state.Journal.LocalizationKey))
                .Distinct(StringComparer.Ordinal);
            foreach (var key in requiredKeys.Where(key => !catalogKeys.Contains(key)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingLocalizationKeyCode,
                    $"Localization key '{key}' is referenced but absent from the catalog.",
                    $"module/localization/messages/{key}"));
            }
        }

        return diagnostics;
    }
}
