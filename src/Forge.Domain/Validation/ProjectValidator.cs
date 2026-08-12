using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Factions;
using Forge.Domain.Items;
using Forge.Domain.WorldFeatures;
using Forge.Domain.Audio;
using Forge.Domain.Navigation;
using Forge.Domain.Modules;
using Forge.Domain.Content;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;

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
    public const string InvalidBlueprintAuthoringCode = "blueprint.authoring.invalid";
    public const string BlueprintVersionMismatchCode = "blueprint.instance.version_mismatch";
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
    public const string DuplicateCreatureFoundationCode = "creature.foundation.uuid_duplicate";
    public const string DuplicateCreatureAuthoringCode = "creature.authoring.uuid_duplicate";
    public const string DuplicateCreatureAnimationCode = "creature.authoring.animation_uuid_duplicate";
    public const string DuplicateCreatureRulesCode = "creature.rules.uuid_duplicate";
    public const string MissingCreatureInventoryItemCode = "creature.rules.inventory_item_missing";
    public const string DuplicateCreatureInteractionCode = "creature.interaction.uuid_duplicate";
    public const string MissingCreatureInteractionFactionCode = "creature.interaction.faction_missing";
    public const string MissingCreatureInteractionDialogueCode = "creature.interaction.dialogue_missing";
    public const string DuplicateCreatureEventAiCode = "creature.event_ai.uuid_duplicate";
    public const string DuplicateQuestOperationsCode = "quest.operations.uuid_duplicate";
    public const string DuplicateItemAdvancedCode = "item.advanced.uuid_duplicate";
    public const string DuplicateItemFoundationCode = "item.foundation.uuid_duplicate";
    public const string MissingItemRecipeByproductCode = "item.recipe.byproduct_missing";
    public const string MissingQuestJournalReferenceCode = "quest.journal.reference_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var diagnostics = new List<DomainDiagnostic>();
        var blueprintsById = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var blueprintIds = blueprintsById.Keys.ToHashSet();
        var areaIds = project.Areas.Select(area => area.Id).ToHashSet();
        var portalIds = new HashSet<PortalId>();
        var presentationIds = new HashSet<Guid>();
        var progressionIds = new HashSet<Guid>();
        var lootIds = new HashSet<Guid>();
        var roleBehaviorIds = new HashSet<Guid>();
        var creatureEventIds = new HashSet<Guid>();
        var creatureFoundationIds = new HashSet<Guid>();
        var creatureAuthoringIds = new HashSet<Guid>();
        var creatureAnimationIds = new HashSet<Guid>();
        var creatureRulesIds = new HashSet<Guid>();
        var creatureInteractionIds = new HashSet<Guid>();
        var creatureEventAiIds = new HashSet<Guid>();
        var factionIds = project.Module.Factions.Factions.Select(item => item.Id).ToHashSet();
        var dialogueIds = project.Dialogues.Select(item => item.Id).ToHashSet();
        var questOperationsIds = new HashSet<Guid>();
        var itemAdvancedIds = new HashSet<Guid>();
        var itemFoundationIds = new HashSet<Guid>();
        var projectItemSlugs = project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        var cameraAnchorIds = project.Areas.SelectMany(area => area.RuntimePolicies.Cinematics.CameraAnchors)
            .Select(anchor => anchor.Id).ToHashSet();

        diagnostics.AddRange(RuntimePolicyValidator.Validate(project.Module));
        diagnostics.AddRange(ProjectWorldProfileValidator.Validate(
            project.Module.RuntimePolicies.World,
            project.Module.Id.Value,
            project.Areas.Select(area => area.Id.ToString()).ToHashSet(StringComparer.Ordinal)));
        if (project.Module.Metadata.Foundation is { } foundation)
            diagnostics.AddRange(ProjectFoundationValidator.Validate(foundation, project));
        diagnostics.AddRange(ProjectVariableValidator.Validate(project.Module.Variables));
        diagnostics.AddRange(ProjectDocumentationValidator.Validate(project.Module.Metadata));
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

        diagnostics.AddRange(ContentPackageCatalogValidator.Validate(project.Module.Content.Packages, project.Module.Id.Value));
        diagnostics.AddRange(LocalizationCatalogValidator.Validate(project.Module.Localization, project.Module.Id.Value));

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
                 string.IsNullOrWhiteSpace(blueprint.Provenance.PermissionEvidence) ||
                 blueprint.Provenance.ContentHash.Length != 64 ||
                 blueprint.Provenance.ContentHash.Any(character => !Uri.IsHexDigit(character))))
            {
                diagnostics.Add(new DomainDiagnostic(
                    InvalidInstalledBlueprintProvenanceCode,
                    $"Installed blueprint '{blueprint.Id}' requires a pack:// source, SPDX license, permission evidence and SHA-256 hash.",
                    path));
            }

            var authoringPath = $"blueprints/{blueprint.Id}/authoring";
            var expectedManifestPrefix = blueprint.PaletteOrigin == BlueprintPaletteOrigin.InstalledPack
                ? "pack://"
                : "content/blueprints/";
            if (blueprint.Authoring.SchemaVersion != BlueprintAuthoringMetadata.CurrentSchemaVersion ||
                !blueprint.Authoring.ManifestPath.StartsWith(expectedManifestPrefix, StringComparison.Ordinal))
            {
                diagnostics.Add(new DomainDiagnostic(
                    InvalidBlueprintAuthoringCode,
                    $"Blueprint '{blueprint.Id}' has an invalid authoring schema or manifest location.",
                    authoringPath));
            }

            diagnostics.AddRange(BlueprintPrefabValidator.Validate(project, blueprint));

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

            if (blueprint.CreatureFoundation is not null)
            {
                var creatureFoundationPath = $"blueprints/{blueprint.Id}/creatureFoundation";
                diagnostics.AddRange(CreatureFoundationValidator.Validate(
                        blueprint.CreatureFoundation,
                        creatureFoundationPath)
                    .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!creatureFoundationIds.Add(blueprint.CreatureFoundation.Id))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        DuplicateCreatureFoundationCode,
                        $"Creature foundation '{blueprint.CreatureFoundation.Id}' is assigned more than once.",
                        $"{creatureFoundationPath}/id"));
                }
                if (blueprint.CreatureFoundation.AuthoringComposition is { } authoring)
                {
                    if (!creatureAuthoringIds.Add(authoring.Id))
                        diagnostics.Add(new DomainDiagnostic(DuplicateCreatureAuthoringCode,
                            $"Creature-authoring profile '{authoring.Id}' is assigned more than once.",
                            $"{creatureFoundationPath}/authoringComposition/id"));
                    if (!creatureAnimationIds.Add(authoring.AnimationGraph.Id))
                        diagnostics.Add(new DomainDiagnostic(DuplicateCreatureAnimationCode,
                            $"Creature animation graph '{authoring.AnimationGraph.Id}' is assigned more than once.",
                            $"{creatureFoundationPath}/authoringComposition/animationGraph/id"));
                    foreach (var id in authoring.AnimationGraph.States.Select(item => item.Id)
                                 .Concat(authoring.AnimationGraph.Transitions.Select(item => item.Id)))
                        if (!creatureAnimationIds.Add(id))
                            diagnostics.Add(new DomainDiagnostic(DuplicateCreatureAnimationCode,
                                $"Creature animation node or edge '{id}' is assigned more than once.",
                                $"{creatureFoundationPath}/authoringComposition/animationGraph/{id}"));
                }
                if (blueprint.CreatureFoundation.RulesComposition is { } rules)
                {
                    foreach (var id in CreatureRulesCompositionValidator.EnumerateComponentIds(rules))
                        if (!creatureRulesIds.Add(id))
                            diagnostics.Add(new DomainDiagnostic(DuplicateCreatureRulesCode,
                                $"Creature-rules component '{id}' is assigned more than once.",
                                $"{creatureFoundationPath}/rulesComposition/{id}"));
                    foreach (var item in rules.Inventory.Containers.SelectMany(container => container.Items))
                        if (!blueprintsById.TryGetValue(item.ItemBlueprintId, out var itemBlueprint) ||
                            itemBlueprint.Kind != BlueprintKind.Item)
                            diagnostics.Add(new DomainDiagnostic(MissingCreatureInventoryItemCode,
                                $"Creature inventory item blueprint '{item.ItemBlueprintId}' does not resolve to an item.",
                                $"{creatureFoundationPath}/rulesComposition/inventory/{item.Id}/itemBlueprintId"));
                }
                if (blueprint.CreatureFoundation.InteractionComposition is { } interaction)
                {
                    foreach (var id in CreatureInteractionCompositionValidator.EnumerateComponentIds(interaction))
                        if (!creatureInteractionIds.Add(id))
                            diagnostics.Add(new DomainDiagnostic(DuplicateCreatureInteractionCode,
                                $"Creature-interaction component '{id}' is assigned more than once.",
                                $"{creatureFoundationPath}/interactionComposition/{id}"));
                    foreach (var membership in interaction.Factions.Memberships)
                        if (!factionIds.Contains(membership.FactionId))
                            diagnostics.Add(new DomainDiagnostic(MissingCreatureInteractionFactionCode,
                                $"Creature faction '{membership.FactionId}' does not resolve.",
                                $"{creatureFoundationPath}/interactionComposition/factions/{membership.Id}/factionId"));
                    foreach (var binding in interaction.DialogueBindings)
                        if (!dialogueIds.Contains(binding.DialogueId))
                            diagnostics.Add(new DomainDiagnostic(MissingCreatureInteractionDialogueCode,
                                $"Creature dialogue '{binding.DialogueId}' does not resolve.",
                                $"{creatureFoundationPath}/interactionComposition/dialogueBindings/{binding.Id}/dialogueId"));
                }
                if (blueprint.CreatureFoundation.EventAiComposition is { } eventAi)
                {
                    foreach (var id in CreatureEventAiCompositionValidator.EnumerateComponentIds(eventAi))
                        if (!creatureEventAiIds.Add(id))
                            diagnostics.Add(new DomainDiagnostic(DuplicateCreatureEventAiCode,
                                $"Creature event/AI component '{id}' is assigned more than once.",
                                $"{creatureFoundationPath}/eventAiComposition/{id}"));
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

            if (blueprint.ItemFoundation is not null)
            {
                var itemFoundationPath = $"blueprints/{blueprint.Id}/itemFoundation";
                diagnostics.AddRange(ItemFoundationValidator.Validate(blueprint.ItemFoundation, itemFoundationPath)
                    .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
                if (!string.Equals(blueprint.ItemFoundation.Archetype, blueprint.Slug, StringComparison.Ordinal))
                    diagnostics.Add(new DomainDiagnostic("item.foundation.slug_mismatch",
                        "Item foundation archetype must match the owning blueprint slug.",
                        $"{itemFoundationPath}/archetype"));
                if (!itemFoundationIds.Add(blueprint.ItemFoundation.Id))
                    diagnostics.Add(new DomainDiagnostic(DuplicateItemFoundationCode,
                        $"Item foundation '{blueprint.ItemFoundation.Id}' is assigned more than once.",
                        $"{itemFoundationPath}/id"));
            }
        }

        if (!areaIds.Contains(project.Module.EntryAreaId) &&
            project.Module.Metadata.Foundation?.RequiresInitialArea != false)
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingEntryAreaCode,
                $"Module '{project.Module.Id}' references missing entry area '{project.Module.EntryAreaId}'.",
                "module/entryAreaId"));
        }

        foreach (var area in project.Areas)
        {
            diagnostics.AddRange(AreaWorldValidator.Validate(area, area.World));
            diagnostics.AddRange(AreaEnvironmentAuthoringValidator.Validate(area, area.Environment));
            diagnostics.AddRange(AreaExperienceAuthoringValidator.Validate(area));
            diagnostics.AddRange(AreaLayerAuthoringValidator.Validate(area));
            if (area.Authoring.RequireLayerRuntimeMetadata && area.Authoring.Layers.Count == 0)
                diagnostics.Add(new DomainDiagnostic(AreaLayerRuntimeResolver.RequiredMissingCode,
                    "Required area layer metadata is missing.",
                    $"areas/{area.Id}/authoring/layers"));
            if (area.Authoring.Experience.RequireRuntimeMetadata &&
                string.Equals(area.Authoring.Experience.Provenance, "migration-default", StringComparison.Ordinal))
                diagnostics.Add(new DomainDiagnostic(AreaExperienceRuntimeResolver.RequiredMissingCode,
                    "Required area experience metadata is missing.",
                    $"areas/{area.Id}/authoring/experience"));
            if (area.Environment.RequireAuthoringRuntimeMetadata &&
                string.Equals(area.Environment.Authoring.Provenance, "migration-default", StringComparison.Ordinal))
                diagnostics.Add(new DomainDiagnostic(AreaEnvironmentRuntimeResolver.RequiredMissingCode,
                    "Required area environment authoring metadata is missing.",
                    $"areas/{area.Id}/environment/authoring"));
            diagnostics.AddRange(AnchorCatalogValidator.Validate(project, area));
            diagnostics.AddRange(WaypointAuthoringValidator.Validate(area));
            diagnostics.AddRange(RuntimePolicyValidator.Validate(area));
            diagnostics.AddRange(AudioEmitterCatalogValidator.Validate(
                area.Environment.Audio.Emitters,
                area.Environment.Audio.Orchestration.MixBuses.Select(item => item.Id).ToHashSet(),
                path: $"areas/{area.Id}/environment/audio/emitters"));
            diagnostics.AddRange(AudioAuthoringProfileValidator.Validate(
                area.Environment.Audio.Authoring,
                area.Environment.Audio.Playlist,
                area.Environment.Audio.Emitters,
                path: $"areas/{area.Id}/environment/audio/authoring"));
            diagnostics.AddRange(AudioCaptionValidator.Validate(area.Environment.Audio,
                path: $"areas/{area.Id}/environment/audio/orchestration/captions"));
            if (area.Environment.Audio.Authoring.RequiredAtRuntime &&
                string.Equals(area.Environment.Audio.Authoring.Provenance, "migration-default", StringComparison.Ordinal))
                diagnostics.Add(new DomainDiagnostic(AudioAuthoringRuntimeResolver.RequiredMissingCode,
                    "Required audio authoring metadata is missing.",
                    $"areas/{area.Id}/environment/audio/authoring"));

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
                else
                {
                    var blueprint = blueprintsById[entity.BlueprintId];
                    if (!BlueprintVersionConstraint.Matches(
                            entity.BlueprintVersionRequirement,
                            blueprint.Authoring.Version))
                    {
                        diagnostics.Add(new DomainDiagnostic(
                            BlueprintVersionMismatchCode,
                            $"Entity '{entity.Id}' requires blueprint version '{entity.BlueprintVersionRequirement}' " +
                            $"but '{blueprint.Slug}' is version '{blueprint.Authoring.Version}'.",
                            $"areas/{area.Id}/entities/{index}/blueprintVersionRequirement"));
                    }
                }
            }

            for (var index = 0; index < area.Portals.Count; index++)
            {
                var portal = area.Portals[index];
                var path = $"areas/{area.Id}/portals/{index}";
                diagnostics.AddRange(PortalLinkValidator.Validate(area, portal));
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

            var nodeSpeakerIds = dialogue.Nodes.Where(item => item.SpeakerBlueprintId is not null)
                .Select(item => item.SpeakerBlueprintId!).ToHashSet();
            foreach (var participant in dialogue.Participants.Where(item =>
                         item.BlueprintId is not null && !blueprintIds.Contains(item.BlueprintId) &&
                         !nodeSpeakerIds.Contains(item.BlueprintId)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingDialogueSpeakerBlueprintCode,
                    $"Dialogue participant '{participant.Id}' references missing blueprint '{participant.BlueprintId}'.",
                    $"dialogues/{dialogue.Id}/participants/{participant.Id}/blueprintId"));
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
            diagnostics.AddRange(QuestAuthoringProfileValidator.ValidateReferences(project, quest));
            diagnostics.AddRange(QuestExperienceProfileValidator.ValidateReferences(project, quest));
            diagnostics.AddRange(QuestQualityProfileValidator.ValidateReferences(project, quest));
            foreach (var state in quest.States)
            {
                foreach (var link in state.Journal.ContextLinks.Where(link =>
                             link.Kind != QuestJournalLinkKind.Custom && !QuestJournalReferenceExists(project, link)))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        MissingQuestJournalReferenceCode,
                        $"Quest journal link '{link.ReferenceId}' does not resolve as {link.Kind}.",
                        $"quests/{quest.Id}/states/{state.Id}/journal/contextLinks/{link.Id}/referenceId"));
                }
            }
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

    private static bool QuestJournalReferenceExists(ProjectDefinition project, QuestJournalLink link)
    {
        var reference = link.ReferenceId;
        var unqualified = reference.Contains(':', StringComparison.Ordinal)
            ? reference[(reference.IndexOf(':', StringComparison.Ordinal) + 1)..]
            : reference;
        static bool Matches(string expected, string reference, string unqualified) =>
            string.Equals(expected, reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(expected, unqualified, StringComparison.OrdinalIgnoreCase);
        return link.Kind switch
        {
            QuestJournalLinkKind.Location => project.Areas.Any(item =>
                Matches(item.Id.ToString(), reference, unqualified) || Matches(item.Slug, reference, unqualified)),
            QuestJournalLinkKind.Character => project.Blueprints.Where(item => item.Kind == BlueprintKind.Creature)
                .Any(item => Matches(item.Id.ToString(), reference, unqualified) ||
                             Matches(item.Slug, reference, unqualified)),
            QuestJournalLinkKind.Entity => project.Areas.SelectMany(item => item.Entities)
                .Any(item => Matches(item.Id.ToString(), reference, unqualified)),
            QuestJournalLinkKind.Quest => project.Quests.Any(item =>
                Matches(item.Id.ToString(), reference, unqualified) || Matches(item.Slug, reference, unqualified)),
            QuestJournalLinkKind.Dialogue => project.Dialogues.Any(item =>
                Matches(item.Id.ToString(), reference, unqualified) || Matches(item.Slug, reference, unqualified)),
            QuestJournalLinkKind.Custom => true,
            _ => false,
        };
    }
}
