using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Factions;
using Forge.Domain.Items;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Validation;

public static class ProjectValidator
{
    public const string MissingBlueprintCode = "project.reference.blueprint_missing";
    public const string MissingEntryAreaCode = "project.reference.entry_area_missing";
    public const string MissingDialogueSpeakerBlueprintCode = "project.reference.dialogue_speaker_blueprint_missing";
    public const string MissingDialoguePresentationBlueprintCode = "project.reference.dialogue_presentation_blueprint_missing";
    public const string MissingDialogueCameraAnchorCode = "project.reference.dialogue_camera_anchor_missing";
    public const string DuplicatePortalCode = "area.portal.id_duplicate";
    public const string MissingPortalSourceCode = "area.portal.source_missing";
    public const string MissingPortalDestinationAreaCode = "area.portal.destination_area_missing";
    public const string MissingPortalDestinationEntityCode = "area.portal.destination_entity_missing";
    public const string UnsupportedBlueprintProvenanceSchemaCode = "blueprint.provenance.schema_unsupported";
    public const string InvalidInstalledBlueprintProvenanceCode = "blueprint.provenance.installed_invalid";
    public const string LocalizationDefaultLocaleMismatchCode = "localization.default_locale.mismatch";
    public const string MissingLocalizationKeyCode = "localization.key.missing";
    public const string ContentWorkspaceIdentityCode = "content.workspace.id_mismatch";
    public const string ArtisticStyleIdentityCode = "art.style.id_mismatch";
    public const string CrossAreaPatrolReferenceCode = "runtime.experience.patrol_reference";
    public const string DuplicateCreaturePresentationCode = "creature.presentation.uuid_duplicate";
    public const string DuplicateCreatureProgressionCode = "creature.progression.uuid_duplicate";
    public const string DuplicateCreatureLootCode = "creature.loot.uuid_duplicate";
    public const string DuplicateCreatureRoleBehaviorCode = "creature.role_behavior.uuid_duplicate";
    public const string DuplicateCreatureEventsCode = "creature.events.uuid_duplicate";
    public const string DuplicateQuestOperationsCode = "quest.operations.uuid_duplicate";
    public const string DuplicateItemAdvancedCode = "item.advanced.uuid_duplicate";
    public const string MissingItemRecipeByproductCode = "item.recipe.byproduct_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var diagnostics = new List<DomainDiagnostic>();
        var blueprintIds = project.Blueprints.Select(blueprint => blueprint.Id).ToHashSet();
        var areaIds = project.Areas.Select(area => area.Id).ToHashSet();
        var portalIds = new HashSet<PortalId>();
        var presentationIds = new HashSet<Guid>();
        var progressionIds = new HashSet<Guid>();
        var lootIds = new HashSet<Guid>();
        var roleBehaviorIds = new HashSet<Guid>();
        var creatureEventIds = new HashSet<Guid>();
        var questOperationsIds = new HashSet<Guid>();
        var itemAdvancedIds = new HashSet<Guid>();
        var projectItemSlugs = project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        var cameraAnchorIds = project.Areas.SelectMany(area => area.RuntimePolicies.Cinematics.CameraAnchors)
            .Select(anchor => anchor.Id).ToHashSet();

        diagnostics.AddRange(RuntimePolicyValidator.Validate(project.Module));
        diagnostics.AddRange(FactionValidator.Validate(project.Module.Factions)
            .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
        diagnostics.AddRange(WorldFeatureCatalogValidator.Validate(project));

        if (project.Module.Content.Id.Value != project.Module.Id.Value)
        {
            diagnostics.Add(new DomainDiagnostic(
                ContentWorkspaceIdentityCode,
                "The content workspace configuration ID must match the module ID.",
                "module/content/id"));
        }

        if (project.Module.ArtisticStyle.Id.Value != project.Module.Id.Value)
        {
            diagnostics.Add(new DomainDiagnostic(
                ArtisticStyleIdentityCode,
                "The artistic style profile ID must match the module ID.",
                "module/artisticStyle/id"));
        }

        diagnostics.AddRange(ArtisticCompatibilityValidator.ValidateReferences(project));

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

            if (blueprint.CreaturePresentation is not null)
            {
                var presentationPath = $"blueprints/{blueprint.Id}/creaturePresentation";
                diagnostics.AddRange(CreaturePresentationValidator.Validate(
                    blueprint.CreaturePresentation,
                    presentationPath).Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!presentationIds.Add(blueprint.CreaturePresentation.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        DuplicateCreaturePresentationCode,
                        $"Creature presentation '{blueprint.CreaturePresentation.Id}' is assigned more than once.",
                        $"{presentationPath}/id"));
                }
            }

            if (blueprint.CreatureProgression is not null)
            {
                var progressionPath = $"blueprints/{blueprint.Id}/creatureProgression";
                var availableAbilities = (blueprint.Combat?.Abilities ?? [])
                    .Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
                diagnostics.AddRange(CreatureProgressionValidator.Validate(
                    blueprint.CreatureProgression,
                    progressionPath,
                    availableAbilities).Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!progressionIds.Add(blueprint.CreatureProgression.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        DuplicateCreatureProgressionCode,
                        $"Creature progression '{blueprint.CreatureProgression.Id}' is assigned more than once.",
                        $"{progressionPath}/id"));
                }
            }

            if (blueprint.CreatureLoot is not null)
            {
                var lootPath = $"blueprints/{blueprint.Id}/creatureLoot";
                var itemSlugs = (blueprint.Combat?.Loot ?? []).Select(item => item.ItemSlug)
                    .ToHashSet(StringComparer.Ordinal);
                var skillIds = (blueprint.CreatureProgression?.Skills ?? []).Select(item => item.SkillId)
                    .ToHashSet(StringComparer.Ordinal);
                diagnostics.AddRange(CreatureLootValidator.Validate(blueprint.CreatureLoot, lootPath, itemSlugs, skillIds)
                    .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!lootIds.Add(blueprint.CreatureLoot.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(DuplicateCreatureLootCode,
                        $"Creature loot profile '{blueprint.CreatureLoot.Id}' is assigned more than once.",
                        $"{lootPath}/id"));
                }
            }

            if (blueprint.CreatureRoleBehavior is not null)
            {
                var roleBehaviorPath = $"blueprints/{blueprint.Id}/creatureRoleBehavior";
                diagnostics.AddRange(CreatureRoleBehaviorValidator.Validate(blueprint.CreatureRoleBehavior,
                        blueprint.CreatureProgression, roleBehaviorPath)
                    .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!roleBehaviorIds.Add(blueprint.CreatureRoleBehavior.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(DuplicateCreatureRoleBehaviorCode,
                        $"Creature role and behavior profile '{blueprint.CreatureRoleBehavior.Id}' is assigned more than once.",
                        $"{roleBehaviorPath}/id"));
                }
            }

            if (blueprint.CreatureEvents is not null)
            {
                var creatureEventsPath = $"blueprints/{blueprint.Id}/creatureEvents";
                diagnostics.AddRange(CreatureEventValidator.Validate(blueprint.CreatureEvents, creatureEventsPath)
                    .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!creatureEventIds.Add(blueprint.CreatureEvents.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(DuplicateCreatureEventsCode,
                        $"Creature-event profile '{blueprint.CreatureEvents.Id}' is assigned more than once.",
                        $"{creatureEventsPath}/id"));
                }
            }

            if (blueprint.ItemAdvanced is not null)
            {
                var itemPath = $"blueprints/{blueprint.Id}/itemAdvanced";
                diagnostics.AddRange(ItemAdvancedValidator.Validate(blueprint.ItemAdvanced, itemPath)
                    .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!itemAdvancedIds.Add(blueprint.ItemAdvanced.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(DuplicateItemAdvancedCode,
                        $"Advanced item profile '{blueprint.ItemAdvanced.Id}' is assigned more than once.",
                        $"{itemPath}/id"));
                }

                foreach (var byproduct in blueprint.ItemAdvanced.Recipes.SelectMany(item => item.ByproductSlugs)
                             .Where(item => !projectItemSlugs.Contains(item)))
                {
                    diagnostics.Add(new DomainDiagnostic(MissingItemRecipeByproductCode,
                        $"Recipe byproduct '{byproduct}' does not reference an item blueprint.",
                        $"{itemPath}/recipes/byproducts/{byproduct}"));
                }
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
            diagnostics.AddRange(AreaWorldValidator.Validate(area, area.World));
            diagnostics.AddRange(RuntimePolicyValidator.Validate(area));

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

        foreach (var ownerArea in project.Areas)
        {
            foreach (var route in ownerArea.RuntimePolicies.Experience.PatrolRoutes)
            {
                foreach (var segment in route.Segments)
                {
                    var path = $"areas/{ownerArea.Id}/runtimePolicies/experience/patrolRoutes/{route.Id}/segments/{segment.Id}";
                    var sourceArea = project.Areas.FirstOrDefault(candidate => candidate.Id == segment.FromAreaId);
                    var destinationArea = project.Areas.FirstOrDefault(candidate => candidate.Id == segment.ToAreaId);
                    var portal = sourceArea?.Portals.FirstOrDefault(candidate => candidate.Id == segment.PortalId);
                    if (sourceArea is null || destinationArea is null || portal is null ||
                        portal.DestinationAreaId != segment.ToAreaId)
                    {
                        diagnostics.Add(new DomainDiagnostic(
                            CrossAreaPatrolReferenceCode,
                            "Cross-area patrol segments require existing source and destination areas plus a matching source portal.",
                            path));
                    }
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

            if (dialogue.Presentation is not { } presentation)
                continue;
            var presentationBlueprints = presentation.AnimationCues
                .SelectMany(item => new[] { item.ParticipantBlueprintId, item.LookAtBlueprintId })
                .Concat(presentation.VoiceCues.Select(item => item.ParticipantBlueprintId))
                .Concat(presentation.CameraShots.Select(item => item.FocusBlueprintId))
                .Concat(presentation.InterjectionRules.Select(item => item.CompanionBlueprintId))
                .Where(item => item is not null).Cast<BlueprintId>().Distinct();
            foreach (var blueprintId in presentationBlueprints.Where(item => !blueprintIds.Contains(item)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingDialoguePresentationBlueprintCode,
                    $"Dialogue presentation references missing blueprint '{blueprintId}'.",
                    $"dialogues/{dialogue.Id}/presentation/blueprints/{blueprintId}"));
            }

            foreach (var shot in presentation.CameraShots.Where(item =>
                         item.Mode == DialogueCameraShotMode.Anchor &&
                         item.CameraAnchorId is { } anchorId && !cameraAnchorIds.Contains(anchorId)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingDialogueCameraAnchorCode,
                    $"Dialogue camera shot '{shot.Id}' references missing anchor '{shot.CameraAnchorId}'.",
                    $"dialogues/{dialogue.Id}/presentation/cameraShots/{shot.Id}/cameraAnchorId"));
            }
        }

        foreach (var quest in project.Quests)
        {
            diagnostics.AddRange(QuestValidator.Validate(quest));
            if (quest.Operations is { } operations && !questOperationsIds.Add(operations.Id))
            {
                diagnostics.Add(new DomainDiagnostic(
                    DuplicateQuestOperationsCode,
                    $"Quest operations profile '{operations.Id}' is assigned more than once.",
                    $"quests/{quest.Id}/operations/id"));
            }
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

        diagnostics.AddRange(CinematicValidator.Validate(project));

        return diagnostics;
    }
}
