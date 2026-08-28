#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Creator;
using Forge.Application.Creatures;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Modules;
using Forge.Application.Rules;
using Forge.Application.Localization;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Quality;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Combat;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Factions;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Merchants;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
    private bool RunCreatureFoundationSmokeTest()
    {
        if (_history is null || _creatureFoundationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED missing_workspace");
            return false;
        }
        var originalProjectJson = ProjectJsonSerializer.Serialize(_history.State.Project);
        var applied = false;
        try
        {
            _creatureFoundationWindow.Open();
            var visible = _creatureFoundationWindow.Visible;
            _creatureFoundationWindow.Hide();
            if (!visible || !_creatureFoundationWindow.ConfigureForSmokeTest() ||
                !_creatureFoundationWindow.PreviewForSmokeTest() || !_creatureFoundationWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED preview_apply_ui " +
                    _creatureFoundationWindow.LastDiagnostics);
                return false;
            }
            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Slug == "ash-warden-boss");
            var profile = blueprint.CreatureFoundation;
            var script = CreatureFoundationScriptService.Read(_history.State.Project, blueprint.Id);
            if (profile is null || profile.Resources.Count != 2 ||
                profile.Movement.Mode != CreatureFoundationMovementMode.Grounded ||
                profile.Spawned.EventName != "creature.spawned" || script?.SpawnHandler != "OnSpawned" ||
                _creatureFoundationWindow.SummarySectionCount != 4)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED canonical_summary");
                return false;
            }
            var spawned = CreatureFoundationValidator.Spawn(profile, blueprint.Id.ToString(),
                new(Guid.NewGuid(), _history.State.Project.Areas[0].Id.ToString(), TransformValue.Identity, 4201,
                    EventAuthorityScope.Server, "toolset.smoke"), Guid.NewGuid);
            var aggregate = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            if (spawned.ResourceState["health"] != 84 ||
                aggregate.Blueprints.Single(item => item.Id == blueprint.Id).CreatureFoundation?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED spawn_persistence");
                return false;
            }
            var invalid = new CreatureFoundationProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
                profile.RequiredAtRuntime, profile.Identity, profile.DefaultTransform,
                [new(Guid.Empty, "health", 10, 11, -1, true)], profile.Movement, profile.References,
                profile.Spawned, profile.Overrides);
            var rejected = _creatureFoundationHandler.Handle(_history.State, new SetCreatureFoundationCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id, invalid, true));
            if (rejected.Succeeded || rejected.Diagnostics.All(item =>
                    item.Code is not "creature.foundation.resource_invalid" and not "creature.foundation.resources_duplicate"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED invalid_prebuild_validation");
                return false;
            }
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            applied = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                ProjectJsonSerializer.Serialize(_history.State.Project) != originalProjectJson ||
                redo.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureFoundation is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED history");
                return false;
            }
            GD.Print("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_PASS ui=sections-preview-empty-error " +
                "identity=localized resources=generic movement=six-modes spawn=validated-after-construction " +
                "command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle " +
                "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED " + exception);
            return false;
        }
        finally
        {
            _creatureFoundationWindow.Hide();
            if (applied) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunCreatureAuthoringCompositionSmokeTest()
    {
        if (_history is null || _creatureAuthoringCompositionWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED missing_workspace");
            return false;
        }
        var originalProjectJson = ProjectJsonSerializer.Serialize(_history.State.Project);
        var applied = false;
        try
        {
            _creatureAuthoringCompositionWindow.Open();
            var visible = _creatureAuthoringCompositionWindow.Visible;
            _creatureAuthoringCompositionWindow.Hide();
            if (!visible || !_creatureAuthoringCompositionWindow.ConfigureForSmokeTest() ||
                !_creatureAuthoringCompositionWindow.PreviewForSmokeTest() ||
                !_creatureAuthoringCompositionWindow.ApplyForSmokeTest() ||
                !_creatureAuthoringCompositionWindow.SimulateForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED preview_apply_ui " +
                    _creatureAuthoringCompositionWindow.LastDiagnostics);
                return false;
            }
            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Slug == "ash-warden-boss");
            var foundation = blueprint.CreatureFoundation;
            var profile = foundation?.AuthoringComposition;
            var handle = CreatureAuthoringCompositionScriptService.CreateHandle(blueprint.Id);
            var script = CreatureAuthoringCompositionScriptService.Read(_history.State.Project, handle);
            var rpg = CreatureAuthoringCompositionFoundationService.Resolve(_history.State.Project, blueprint.Id);
            if (foundation is null || profile is null || !foundation.AuthoringCompositionRequiredAtRuntime ||
                profile.EncounterRole != CreatureAuthoringEncounterRole.Boss || profile.ThreatRating != 12 ||
                profile.Species.SpeciesId != "species.emberborn" ||
                profile.Appearance.MeshAssetId != "mesh.ash-warden" || profile.AnimationGraph.States.Count != 5 ||
                profile.AnimationGraph.States.Select(item => item.StateId).SequenceEqual(
                    profile.AnimationGraph.States.Select(item => item.StateId).Order(StringComparer.Ordinal)) == false ||
                script?.SpeciesId != "species.emberborn" || !rpg.Succeeded ||
                rpg.Creature?.Animations?.RetargetProfileId != "retarget.humanoid" ||
                _creatureAuthoringCompositionWindow.SummarySectionCount != 4 ||
                string.IsNullOrWhiteSpace(_creatureAuthoringCompositionWindow.LastSimulatedState))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED canonical_adapter_summary");
                return false;
            }
            var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var restoredProfile = restored.Blueprints.Single(item => item.Id == blueprint.Id)
                .CreatureFoundation?.AuthoringComposition;
            if (restoredProfile?.Id != profile.Id || restoredProfile.AnimationGraph.Id != profile.AnimationGraph.Id ||
                restoredProfile.Appearance.MeshAssetId != profile.Appearance.MeshAssetId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED persistence_roundtrip");
                return false;
            }
            var invalidProfile = new CreatureAuthoringCompositionProfile(profile.Id, profile.SchemaVersion,
                profile.Provenance, profile.AssetId, profile.PreviewTransform, profile.ArchetypeId,
                profile.RulesetId, profile.RigId, profile.FactionId, profile.EncounterRole,
                profile.ThreatRating, profile.VisualKitId, profile.CombatTags, profile.Species,
                profile.Appearance, profile.AnimationGraph with
                {
                    States = [profile.AnimationGraph.States[0]],
                    Transitions = [],
                }, profile.Overrides);
            var invalidFoundation = new CreatureFoundationProfile(foundation.Id, foundation.SchemaVersion,
                foundation.Provenance, foundation.RequiredAtRuntime, foundation.Identity,
                foundation.DefaultTransform, foundation.Resources, foundation.Movement, foundation.References,
                foundation.Spawned, foundation.Overrides, invalidProfile, true);
            var rejected = _creatureFoundationHandler.Handle(_history.State, new SetCreatureFoundationCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id, invalidFoundation, true));
            if (rejected.Succeeded || rejected.Diagnostics.All(item =>
                    item.Code != "creature.authoring.animation_graph_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED invalid_prebuild_validation");
                return false;
            }
            if (!CreatureAuthoringCompositionRuntimeResolver.Resolve(null, false).Succeeded ||
                CreatureAuthoringCompositionRuntimeResolver.Resolve(null, true).Diagnostics.Single().Code !=
                CreatureAuthoringCompositionValidator.MissingRequiredCode)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED optional_required_runtime");
                return false;
            }
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            applied = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                ProjectJsonSerializer.Serialize(_history.State.Project) != originalProjectJson ||
                redo.State.Project.Blueprints.Single(item => item.Id == blueprint.Id)
                    .CreatureFoundation?.AuthoringComposition?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED history");
                return false;
            }
            GD.Print("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_PASS " +
                "ui=wizard-sections-empty-error-preview-graph-simulation role=encounter-threat-tags " +
                "species=inheritance-traits-variants-skeleton appearance=archetype-mesh-rig-morph-material-palette-preset " +
                "animation=graph-uuid-ordered-locomotion-combat-emote-additive-retarget command=dry-run-apply " +
                "history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run " +
                "runtime=optional-safe-required-diagnostic-rpgkit-adapter");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_FAILED " + exception);
            return false;
        }
        finally
        {
            _creatureAuthoringCompositionWindow.Hide();
            if (applied) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunCreatureRulesCompositionSmokeTest()
    {
        if (_history is null || _creatureRulesCompositionWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED missing_workspace");
            return false;
        }
        var originalProjectJson = ProjectJsonSerializer.Serialize(_history.State.Project);
        var applied = false;
        try
        {
            _creatureRulesCompositionWindow.Open();
            var visible = _creatureRulesCompositionWindow.Visible;
            _creatureRulesCompositionWindow.Hide();
            if (!visible || !_creatureRulesCompositionWindow.ConfigureForSmokeTest() ||
                !_creatureRulesCompositionWindow.PreviewForSmokeTest() ||
                !_creatureRulesCompositionWindow.ApplyForSmokeTest() ||
                !_creatureRulesCompositionWindow.SimulateForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED preview_apply_ui " +
                    _creatureRulesCompositionWindow.LastDiagnostics);
                return false;
            }
            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Slug == "ash-warden-boss");
            var foundation = blueprint.CreatureFoundation;
            var profile = foundation?.RulesComposition;
            var script = CreatureRulesCompositionScriptService.Read(_history.State.Project,
                CreatureRulesCompositionScriptService.CreateHandle(blueprint.Id));
            var rpg = CreatureRulesCompositionFoundationService.Resolve(_history.State.Project, blueprint.Id);
            if (foundation is null || profile is null || !foundation.RulesCompositionRequiredAtRuntime ||
                profile.InitialState.LifeState != CreatureRulesInitialLifeState.Seated ||
                profile.InitialState.PoseId != "pose.warden-throne" ||
                CreatureRulesCompositionValidator.EvaluateAttributes(profile.Attributes).GetValueOrDefault("evasion") != 16 ||
                CreatureRulesCompositionValidator.Mitigate(profile.Defenses, "glass", 10) != 5 ||
                profile.Abilities.Single().AbilityId != "ability.ash-nova" ||
                profile.Inventory.Containers.Single().Items.Single().ItemBlueprintId.ToString() !=
                "10000000-0000-4000-8000-000000000008" ||
                script?.LifeState != "Seated" || script.InventoryItemCount != 1 || !rpg.Succeeded ||
                rpg.Creature?.Attributes is null || rpg.Creature.Inventory?.Persistent != true ||
                _creatureRulesCompositionWindow.SummarySectionCount != 5 ||
                Math.Abs(_creatureRulesCompositionWindow.LastMitigatedDamage - 5) > .001)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED canonical_adapter_summary");
                return false;
            }
            var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var restoredProfile = restored.Blueprints.Single(item => item.Id == blueprint.Id)
                .CreatureFoundation?.RulesComposition;
            if (restoredProfile?.Id != profile.Id || restoredProfile.InitialState.Id != profile.InitialState.Id ||
                restoredProfile.Inventory.Containers.Single().Items.Single().Id !=
                profile.Inventory.Containers.Single().Items.Single().Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED persistence_roundtrip");
                return false;
            }
            var invalidProfile = new CreatureRulesCompositionProfile(profile.Id, profile.SchemaVersion,
                profile.Provenance, profile.AssetId, profile.PreviewTransform, profile.InitialState,
                profile.Attributes, profile.Defenses with
                {
                    Resistances = new Dictionary<string, double> { ["glass"] = 2 },
                }, profile.Abilities, profile.Inventory, profile.Overrides);
            var invalidFoundation = new CreatureFoundationProfile(foundation.Id, foundation.SchemaVersion,
                foundation.Provenance, foundation.RequiredAtRuntime, foundation.Identity,
                foundation.DefaultTransform, foundation.Resources, foundation.Movement, foundation.References,
                foundation.Spawned, foundation.Overrides, foundation.AuthoringComposition,
                foundation.AuthoringCompositionRequiredAtRuntime, invalidProfile, true,
                foundation.InteractionComposition, foundation.InteractionCompositionRequiredAtRuntime);
            var rejected = _creatureFoundationHandler.Handle(_history.State, new SetCreatureFoundationCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id, invalidFoundation, true));
            if (rejected.Succeeded || rejected.Diagnostics.All(item => item.Code != "creature.rules.defenses_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED invalid_prebuild_validation");
                return false;
            }
            if (!CreatureRulesCompositionRuntimeResolver.Resolve(null, false).Succeeded ||
                CreatureRulesCompositionRuntimeResolver.Resolve(null, true).Diagnostics.Single().Code !=
                CreatureRulesCompositionValidator.MissingRequiredCode)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED optional_required_runtime");
                return false;
            }
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            applied = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                ProjectJsonSerializer.Serialize(_history.State.Project) != originalProjectJson ||
                redo.State.Project.Blueprints.Single(item => item.Id == blueprint.Id)
                    .CreatureFoundation?.RulesComposition?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED history");
                return false;
            }
            GD.Print("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_PASS " +
                "ui=sections-empty-error-preview-damage-simulation " +
                "initial=alive-unconscious-dead-sleeping-seated-frozen-loop-pose-invisible " +
                "attributes=ruleset-base-modifiers-derived-inspectable " +
                "defenses=avoidance-armor-resistances-saves-immunities-mitigation " +
                "abilities=level-charges-cooldown-conditions-ai-tags-script " +
                "inventory=containers-slots-weight-volume-ownership-equipment-persistence " +
                "command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle " +
                "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_FAILED " + exception);
            return false;
        }
        finally
        {
            _creatureRulesCompositionWindow.Hide();
            if (applied) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunCreatureInteractionCompositionSmokeTest()
    {
        if (_history is null || _creatureInteractionCompositionWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED missing_workspace");
            return false;
        }
        var originalProjectJson = ProjectJsonSerializer.Serialize(_history.State.Project);
        var applied = false;
        try
        {
            _creatureInteractionCompositionWindow.Open();
            var visible = _creatureInteractionCompositionWindow.Visible;
            _creatureInteractionCompositionWindow.Hide();
            if (!visible || !_creatureInteractionCompositionWindow.ConfigureForSmokeTest() ||
                !_creatureInteractionCompositionWindow.PreviewForSmokeTest() ||
                !_creatureInteractionCompositionWindow.ApplyForSmokeTest() ||
                !_creatureInteractionCompositionWindow.SimulateForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED preview_apply_ui " +
                    _creatureInteractionCompositionWindow.LastDiagnostics);
                return false;
            }
            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Slug == "ash-warden-boss");
            var foundation = blueprint.CreatureFoundation;
            var profile = foundation?.InteractionComposition;
            var script = CreatureInteractionCompositionScriptService.Read(_history.State.Project,
                CreatureInteractionCompositionScriptService.CreateHandle(blueprint.Id));
            var rpg = CreatureInteractionCompositionFoundationService.Resolve(_history.State.Project, blueprint.Id);
            var dialogue = profile is null ? null : CreatureInteractionCompositionValidator.RequestDialogue(
                profile, "greeting", "player.hero", "state.hostile");
            if (foundation is null || profile is null || !foundation.InteractionCompositionRequiredAtRuntime ||
                profile.Factions.PrimaryFactionId != Guid.Parse("b2050000-0000-4000-8000-000000000002") ||
                profile.Factions.Memberships.Count != 2 || profile.Perception.Senses.Count != 4 ||
                profile.DialogueBindings.Count != 3 || !profile.Protection.Invulnerable ||
                !profile.Protection.UsesNarrativeDeath || profile.Protection.DialogueInterruptible ||
                profile.OnConversation.Handler != "OnConversation" || dialogue?.Accepted != true ||
                dialogue.Binding?.Priority != 20 || script?.PerceptionMemorySeconds != 12 ||
                script.OnConversationHandler != "OnConversation" || !rpg.Succeeded ||
                rpg.Creature?.Factions?.Memberships.Count != 2 || rpg.Creature.Perception?.Senses.Count != 4 ||
                _creatureInteractionCompositionWindow.SummarySectionCount != 5 ||
                _creatureInteractionCompositionWindow.LastSelectedDialogueId !=
                "40000000-0000-4000-8000-000000000001")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED canonical_adapter_summary");
                return false;
            }
            var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var restoredProfile = restored.Blueprints.Single(item => item.Id == blueprint.Id)
                .CreatureFoundation?.InteractionComposition;
            if (restoredProfile?.Id != profile.Id || restoredProfile.Factions.Memberships.Count != 2 ||
                restoredProfile.DialogueBindings.Single(item => item.Priority == 20).Id !=
                profile.DialogueBindings.Single(item => item.Priority == 20).Id ||
                restoredProfile.OnConversation.Id != profile.OnConversation.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED persistence_roundtrip");
                return false;
            }
            var invalidProfile = new CreatureInteractionCompositionProfile(profile.Id, profile.SchemaVersion,
                profile.Provenance, profile.AssetId, profile.PreviewTransform, profile.Factions,
                new(profile.Perception.Id,
                    profile.Perception.Senses.Select((item, index) => index == 0 ? item with { Range = 0 } : item)
                        .ToArray(), profile.Perception.MemorySeconds),
                profile.DialogueBindings, profile.Protection, profile.OnConversation, profile.Overrides);
            var invalidFoundation = new CreatureFoundationProfile(foundation.Id, foundation.SchemaVersion,
                foundation.Provenance, foundation.RequiredAtRuntime, foundation.Identity,
                foundation.DefaultTransform, foundation.Resources, foundation.Movement, foundation.References,
                foundation.Spawned, foundation.Overrides, foundation.AuthoringComposition,
                foundation.AuthoringCompositionRequiredAtRuntime, foundation.RulesComposition,
                foundation.RulesCompositionRequiredAtRuntime, invalidProfile, true);
            var rejected = _creatureFoundationHandler.Handle(_history.State, new SetCreatureFoundationCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id, invalidFoundation, true));
            if (rejected.Succeeded || rejected.Diagnostics.All(item =>
                    item.Code != "creature.interaction.perception_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED invalid_prebuild_validation");
                return false;
            }
            if (!CreatureInteractionCompositionRuntimeResolver.Resolve(null, false).Succeeded ||
                CreatureInteractionCompositionRuntimeResolver.Resolve(null, true).Diagnostics.Single().Code !=
                CreatureInteractionCompositionValidator.MissingRequiredCode)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED optional_required_runtime");
                return false;
            }
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            applied = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                ProjectJsonSerializer.Serialize(_history.State.Project) != originalProjectJson ||
                redo.State.Project.Blueprints.Single(item => item.Id == blueprint.Id)
                    .CreatureFoundation?.InteractionComposition?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED history");
                return false;
            }
            GD.Print("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_PASS " +
                "ui=sections-empty-error-preview-dialogue-simulation " +
                "factions=multiple-weighted-primary-rank-reputation-inheritance " +
                "perception=vision-hearing-smell-magic-range-cone-occlusion-stimuli-memory " +
                "dialogue=context-priority-interlocutor-state-fallback-selection-refusal " +
                "protection=invulnerability-narrative-death-interruption-gm-permissions " +
                "onconversation=interaction-requested-dialogue-starting-server-binding " +
                "command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle " +
                "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_FAILED " + exception);
            return false;
        }
        finally
        {
            _creatureInteractionCompositionWindow.Hide();
            if (applied) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunCreatureEventAiCompositionSmokeTest()
    {
        if (_history is null || _creatureEventAiCompositionWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED missing_workspace");
            return false;
        }
        var originalProjectJson = ProjectJsonSerializer.Serialize(_history.State.Project);
        var applied = false;
        try
        {
            _creatureEventAiCompositionWindow.Open();
            var visible = _creatureEventAiCompositionWindow.Visible;
            _creatureEventAiCompositionWindow.Hide();
            if (!visible || !_creatureEventAiCompositionWindow.ConfigureForSmokeTest() ||
                !_creatureEventAiCompositionWindow.PreviewForSmokeTest() ||
                !_creatureEventAiCompositionWindow.ApplyForSmokeTest() ||
                !_creatureEventAiCompositionWindow.SimulateForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED preview_apply_ui " +
                    _creatureEventAiCompositionWindow.LastDiagnostics);
                return false;
            }
            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Slug == "ash-warden-boss");
            var foundation = blueprint.CreatureFoundation;
            var profile = foundation?.EventAiComposition;
            var script = CreatureEventAiCompositionScriptService.Read(_history.State.Project,
                CreatureEventAiCompositionScriptService.CreateHandle(blueprint.Id));
            var rpg = CreatureEventAiCompositionFoundationService.Resolve(_history.State.Project, blueprint.Id);
            if (foundation is null || profile is null || !foundation.EventAiCompositionRequiredAtRuntime ||
                profile.Controller.Kind != CreatureEventAiControllerKind.Utility ||
                profile.Controller.Blackboard.Count != 3 || profile.Controller.AllowedActions.Count != 3 ||
                profile.Blocked.RecoveryOrder.Count != 4 ||
                CreatureEventAiCompositionFoundationService.EnumerateBindings(rpg).Length != 7 ||
                script?.OnPerceptionDetectedHandler != "OnPerceptionDetected" ||
                script.OnPerceptionLostHandler != "OnPerceptionLost" ||
                script.BeforePhysicalAttackedHandler != "BeforePhysicalAttacked" ||
                script.OnPhysicalAttackedHandler != "OnPhysicalAttacked" ||
                script.BeforeAbilityTargetedHandler != "BeforeAbilityTargeted" ||
                script.OnSpellCastAtHandler != "OnSpellCastAt" || script.OnBlockedHandler != "OnBlocked" ||
                _creatureEventAiCompositionWindow.SummarySectionCount != 5 ||
                _creatureEventAiCompositionWindow.LastDecisionAction != "attack")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED canonical_adapter_summary");
                return false;
            }

            var creatureId = profile.Id;
            var perception = Forge.RpgKit.Domain.Creatures.CreatureInteractionService.DiffPerception(creatureId,
                [new Forge.RpgKit.Domain.Creatures.PerceptionResult("old", Forge.RpgKit.Domain.Creatures.SenseKind.Hearing,
                    .4, Vector3Value.Zero, 10)],
                [new Forge.RpgKit.Domain.Creatures.PerceptionResult("new", Forge.RpgKit.Domain.Creatures.SenseKind.Vision,
                    .8, new Vector3Value(2, 0, 1), 20)], Guid.NewGuid);
            var network = new Forge.RpgKit.Domain.Creatures.AttackNetworkContext("server", 42, 3, false);
            var attack = Forge.RpgKit.Domain.Creatures.CreatureInteractionService.ResolveAttack(
                new Forge.RpgKit.Domain.Creatures.AttackDeclared(Guid.NewGuid(), creatureId, "player.hero",
                    "weapon.sword", "ash-warden-boss", network), "hit", 7, Guid.NewGuid);
            var ability = Forge.RpgKit.Domain.Creatures.CreatureAiService.Impact(
                new Forge.RpgKit.Domain.Creatures.AbilityTargeted(Guid.NewGuid(), creatureId, "player.hero",
                    "ability.firebolt", new HashSet<string> { "fire" }, "entity"), "resisted", 3,
                Guid.NewGuid);
            var blocked = Forge.RpgKit.Domain.Creatures.CreatureAiService.Blocked(creatureId, "pillar.ash",
                "route", "dynamic-obstacle", Vector3Value.Zero, Guid.NewGuid);
            var mappedDecision = Forge.RpgKit.Domain.Creatures.CreatureAiService.Decide(rpg.Controller!,
                new Dictionary<string, double> { ["attack"] = .9, ["guard"] = .4, ["patrol"] = .2 });
            if (perception.Detected.Single().StimulusId != "new" || perception.Lost.Single().StimulusId != "old" ||
                attack.AppliedDamage != 7 || ability.Result != "resisted" ||
                blocked.Recovery != Forge.RpgKit.Domain.Creatures.MovementRecoveryStrategy.Avoid ||
                mappedDecision.Action != "attack")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED typed_event_simulation");
                return false;
            }

            var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var restoredProfile = restored.Blueprints.Single(item => item.Id == blueprint.Id)
                .CreatureFoundation?.EventAiComposition;
            if (restoredProfile?.Id != profile.Id || restoredProfile.Controller.Id != profile.Controller.Id ||
                restoredProfile.Controller.Blackboard.Count != 3 ||
                restoredProfile.Ability.Impacted.Id != profile.Ability.Impacted.Id ||
                restoredProfile.Blocked.RecoveryOrder.Count != 4)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED persistence_roundtrip");
                return false;
            }
            var invalidProfile = new CreatureEventAiCompositionProfile(profile.Id, profile.SchemaVersion,
                profile.Provenance, profile.AssetId, profile.PreviewTransform, profile.Perception,
                profile.PhysicalAttack, profile.Ability, profile.Blocked,
                profile.Controller with
                {
                    Blackboard = profile.Controller.Blackboard.Select((item, index) => index == 0
                        ? item with { Kind = CreatureEventAiBlackboardValueKind.Number, Value = "NaN" }
                        : item).ToArray(),
                }, profile.Overrides);
            var invalidFoundation = new CreatureFoundationProfile(foundation.Id, foundation.SchemaVersion,
                foundation.Provenance, foundation.RequiredAtRuntime, foundation.Identity,
                foundation.DefaultTransform, foundation.Resources, foundation.Movement, foundation.References,
                foundation.Spawned, foundation.Overrides, foundation.AuthoringComposition,
                foundation.AuthoringCompositionRequiredAtRuntime, foundation.RulesComposition,
                foundation.RulesCompositionRequiredAtRuntime, foundation.InteractionComposition,
                foundation.InteractionCompositionRequiredAtRuntime, invalidProfile, true);
            var rejected = _creatureFoundationHandler.Handle(_history.State, new SetCreatureFoundationCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id, invalidFoundation, true));
            if (rejected.Succeeded || rejected.Diagnostics.All(item =>
                    item.Code != "creature.event_ai.controller_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED invalid_prebuild_validation");
                return false;
            }
            var optional = CreatureEventAiCompositionRuntimeResolver.Resolve(null, false);
            var required = CreatureEventAiCompositionRuntimeResolver.Resolve(null, true);
            if (!optional.Succeeded || !optional.UsedFallback || required.Diagnostics.Single().Code !=
                CreatureEventAiCompositionValidator.MissingRequiredCode)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED optional_required_runtime");
                return false;
            }
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            applied = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                ProjectJsonSerializer.Serialize(_history.State.Project) != originalProjectJson ||
                redo.State.Project.Blueprints.Single(item => item.Id == blueprint.Id)
                    .CreatureFoundation?.EventAiComposition?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED history");
                return false;
            }
            GD.Print("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_PASS " +
                "ui=sections-empty-error-preview-decision-simulation " +
                "onperception=stimulus-detected-lost-sense-confidence-position-memory " +
                "onphysicalattacked=declared-resolved-attacker-weapon-target-result-network " +
                "onspellcastat=ability-targeted-impacted-source-target-tags-result-magnitude " +
                "onblocked=obstacle-cause-nav-status-position-wait-repath-avoid-teleport-abort " +
                "ai=state-machine-behavior-tree-utility-script-typed-inspectable-blackboard-bounded-actions " +
                "command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle " +
                "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_FAILED " + exception);
            return false;
        }
        finally
        {
            _creatureEventAiCompositionWindow.Hide();
            if (applied) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunCreaturePresentationSmokeTest()
    {
        if (_history is null || _projectSession is null || _creaturePresentationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED missing_workspace");
            return false;
        }

        var portraitPath = Path.Combine(_projectSession.RootDirectory, "artifacts", "verification", "g13b-creature-portrait.png");
        var relativePortraitPath = Path.GetRelativePath(_projectSession.RootDirectory, portraitPath).Replace('\\', '/');
        var applied = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(portraitPath)!);
            var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            image.Fill(new Color("7a5cff"));
            if (image.SavePng(portraitPath) != Error.Ok)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED portrait_fixture");
                return false;
            }

            _creaturePresentationWindow.Open();
            var visible = _creaturePresentationWindow.Visible;
            _creaturePresentationWindow.Hide();
            if (!visible || !_creaturePresentationWindow.ConfigureForSmokeTest(relativePortraitPath))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED configure_ui");
                return false;
            }

            var blueprintId = _creaturePresentationWindow.SelectedBlueprintId;
            var originalPresentation = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId).CreaturePresentation;
            if (!_creaturePresentationWindow.PreviewForSmokeTest() ||
                _creaturePresentationWindow.LastCommandId == Guid.Empty ||
                !_creaturePresentationWindow.LastPatchSummary.Contains("presentation", StringComparison.OrdinalIgnoreCase) ||
                !_creaturePresentationWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED preview_apply_ui");
                return false;
            }

            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreaturePresentation;
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            var runtime = CreaturePresentationRuntimeResolver.Resolve(profile, required: true);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                profile.GenderId != "nonbinary" || profile.BodyTypeId != "humanoid-medium" ||
                profile.Pronouns.Subject != "iel" || profile.Portraits.Count != 2 ||
                profile.Portraits[0].ContentSha256.Length != 64 || profile.BodySlots.Count != 2 ||
                profile.MaterialColors.Single().HexColor != "#7A5CFF" || profile.Attachments.Count != 2 ||
                profile.Voice?.AudioBankIds.Count != 2 || profile.Voice.EmotionIds.Count != 2 ||
                profile.Voice.VariantIds.Count != 2 || profile.Voice.LipSyncProfileId != "lipsync.fr-v1" ||
                !runtime.Succeeded || runtime.UseModelFallback ||
                CreaturePresentationValidator.Validate(profile).Count != 0 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreaturePresentation?.Id != profile.Id ||
                !_creaturePresentationWindow.PortraitPreviewReady || _creaturePresentationWindow.PreviewMeshCount < 5)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED canonical_roundtrip_runtime_preview");
                return false;
            }

            Undo();
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreaturePresentation?.Id !=
                originalPresentation?.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED undo");
                return false;
            }
            applied = false;
            Redo();
            applied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreaturePresentation?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED redo");
                return false;
            }
            Undo();
            applied = false;

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_PASS identity=gender-body-pronouns " +
                "portrait=hashed-multires-ready fallback=3d body=modular-rig-layer-clipping " +
                "materials=colored attachments=wings-tail voice=bank-language-rights " +
                "ui=visible-empty-error preview=3d-and-portrait uuid=stable schema=v1 provenance=tracked " +
                "transaction=identified dry-run=previewed audit=generated history=restored " +
                "persistence=project-roundtrip package=canonical runtime=optional-safe");
            return true;
        }
        finally
        {
            _creaturePresentationWindow.Hide();
            if (applied)
            {
                _history.Undo();
                RebuildPreview();
                RefreshEditorControls();
            }
            if (File.Exists(portraitPath)) File.Delete(portraitPath);
        }
    }

    private bool RunCreatureProgressionSmokeTest()
    {
        if (_history is null || _creatureProgressionWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED missing_workspace");
            return false;
        }

        var applied = false;
        var roleBehaviorDetached = false;
        try
        {
            _creatureProgressionWindow.Open();
            var visible = _creatureProgressionWindow.Visible;
            _creatureProgressionWindow.Hide();
            if (!visible || !_creatureProgressionWindow.ConfigureForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED configure_ui");
                return false;
            }

            var blueprintId = _creatureProgressionWindow.SelectedBlueprintId;
            if (blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED selection");
                return false;
            }
            var originalBlueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var originalProgression = originalBlueprint.CreatureProgression;
            var originalRoleBehaviorId = originalBlueprint.CreatureRoleBehavior?.Id;
            if (originalBlueprint.CreatureRoleBehavior is not null)
            {
                var detach = new SetCreatureRoleBehaviorCommand(CommandId.From(Guid.NewGuid()),
                    _history.State.Revision, blueprintId, null, false);
                if (!ApplyCommand(_creatureRoleBehaviorHandler.Handle(_history.State, detach),
                        "Isolation du profil G13-E pour le smoke de progression G13-C.", null))
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED behavior_detach");
                    return false;
                }
                roleBehaviorDetached = true;
            }
            if (!_creatureProgressionWindow.PreviewForSmokeTest() ||
                _creatureProgressionWindow.LastCommandId == Guid.Empty ||
                !_creatureProgressionWindow.LastPatchSummary.Contains("progression", StringComparison.OrdinalIgnoreCase) ||
                !_creatureProgressionWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED preview_apply_ui");
                return false;
            }

            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureProgression;
            var availableAbilities = (blueprint.Combat?.Abilities ?? []).Select(item => item.Slug)
                .ToHashSet(StringComparer.Ordinal);
            var runtime = CreatureProgressionRuntimeResolver.Resolve(profile, availableAbilities, required: true);
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                profile.RulesetId != "forge.generic" || !profile.AllowMultipleTracks || !profile.RequiredAtRuntime ||
                profile.Tracks.Count != 2 || profile.Tracks.Sum(item => item.Level) != 5 ||
                profile.DispositionAxes.Count != 2 || profile.Skills.Count != 2 ||
                profile.Skills.Single(item => item.SkillId == "lore").Expertise is not true ||
                profile.Talents.Count != 1 || profile.Talents[0].Prerequisites.Count != 2 ||
                profile.Loadout.Mode != AbilityLoadoutMode.Prepared || profile.Loadout.PreparedAbilities.Count != 1 ||
                profile.Overrides.GetValueOrDefault("xp-rate") != "1.0" ||
                !runtime.Succeeded || runtime.TotalLevel != 5 || runtime.PreparedAbilityIds.Count != 1 ||
                CreatureProgressionValidator.Validate(profile, availableAbilityIds: availableAbilities).Count != 0 ||
                _creatureProgressionWindow.SummarySectionCount != 5 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id != profile.Id)
            {
                GD.PrintErr(
                    "OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} tracks={profile?.Tracks.Count ?? -1} level={profile?.Tracks.Sum(item => item.Level) ?? -1} " +
                    $"axes={profile?.DispositionAxes.Count ?? -1} skills={profile?.Skills.Count ?? -1} " +
                    $"talents={profile?.Talents.Count ?? -1} prepared={profile?.Loadout.PreparedAbilities.Count ?? -1} " +
                    $"runtime={runtime.Succeeded}/{runtime.TotalLevel} validation={CreatureProgressionValidator.Validate(profile!, availableAbilityIds: availableAbilities).Count} " +
                    $"summary={_creatureProgressionWindow.SummarySectionCount} roundtrip={ProjectJsonSerializer.Serialize(restored) == serialized} " +
                    $"restored={restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id == profile?.Id}");
                return false;
            }

            Undo();
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id !=
                originalProgression?.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED undo");
                return false;
            }
            applied = false;
            Redo();
            applied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED redo");
                return false;
            }
            Undo();
            applied = false;
            if (roleBehaviorDetached)
            {
                Undo();
                roleBehaviorDetached = false;
                if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id)
                        .CreatureRoleBehavior?.Id != originalRoleBehaviorId)
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED behavior_restore");
                    return false;
                }
            }

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_PASS tracks=multiclass-levels disposition=project-axes " +
                "skills=rank-training-expertise-checks talents=prerequisites-ranks-passive-hooks " +
                "loadout=learned-prepared-deck-cooldown-charges-resource-modifiers ruleset=data-driven " +
                "ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked transaction=identified " +
                "dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=optional-safe");
            return true;
        }
        finally
        {
            _creatureProgressionWindow.Hide();
            if (applied)
            {
                _history.Undo();
            }
            if (roleBehaviorDetached) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunCreatureLootSmokeTest()
    {
        if (_history is null || _creatureLootWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED missing_workspace");
            return false;
        }

        var progressionApplied = false;
        var lootApplied = false;
        try
        {
            _creatureLootWindow.Open();
            var visible = _creatureLootWindow.Visible;
            _creatureLootWindow.Hide();
            var blueprintId = _creatureLootWindow.SelectedBlueprintId;
            if (!visible || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED selection_ui");
                return false;
            }

            var original = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var originalProgressionId = original.CreatureProgression?.Id;
            var originalLootId = original.CreatureLoot?.Id;
            if (original.CreatureProgression is null || original.CreatureProgression.Skills.Count < 2)
            {
                var sourceProgression = original.CreatureProgression ?? CreatureProgressionProfile.CreateDefault(
                    Guid.Parse("a1420000-0000-4000-8000-000000000001"), "smoke:g13-d");
                var fixtureSkills = sourceProgression.Skills
                    .Where(item => item.SkillId is not ("sleight" or "awareness"))
                    .Concat([
                        new("sleight", "Larcin", "agility", 2, 10, 2, true, false, 12),
                        new("awareness", "Vigilance", "insight", 2, 10, 2, true, false, 10),
                    ]).ToArray();
                var progression = new CreatureProgressionProfile(
                    sourceProgression.Id, 1, sourceProgression.Provenance, sourceProgression.RulesetId,
                    sourceProgression.AllowMultipleTracks, sourceProgression.RequiredAtRuntime,
                    sourceProgression.Tracks, sourceProgression.DispositionAxes, fixtureSkills,
                    sourceProgression.Talents, sourceProgression.Loadout, sourceProgression.Overrides);
                var command = new SetCreatureProgressionCommand(CommandId.From(Guid.NewGuid()),
                    _history.State.Revision, blueprintId, progression, false);
                if (!ApplyCommand(_creatureProgressionHandler.Handle(_history.State, command),
                        "Préparation du test de butin.", null))
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED progression_fixture");
                    return false;
                }
                progressionApplied = true;
            }

            _creatureLootWindow.Refresh();
            var configured = _creatureLootWindow.ConfigureForSmokeTest();
            var previewed = configured && _creatureLootWindow.PreviewForSmokeTest();
            var applied = previewed && _creatureLootWindow.LastCommandId != Guid.Empty &&
                          _creatureLootWindow.LastPatchSummary.Contains("loot", StringComparison.OrdinalIgnoreCase) &&
                          _creatureLootWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_creatureLootWindow.LastPatchSummary} diagnostics={_creatureLootWindow.LastDiagnostics}");
                return false;
            }

            lootApplied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureLoot;
            var itemIds = (blueprint.Combat?.Loot ?? []).Select(item => item.ItemSlug).ToHashSet(StringComparer.Ordinal);
            var skillIds = (blueprint.CreatureProgression?.Skills ?? []).Select(item => item.SkillId).ToHashSet(StringComparer.Ordinal);
            var death = CreatureLootRuntimeResolver.ResolveDeath(profile, blueprint.Combat?.Loot ?? [], 0.2,
                "player", "player-party", DateTimeOffset.UnixEpoch, required: true);
            var theft = CreatureLootRuntimeResolver.ResolvePickpocket(profile!,
                Guid.Parse("a1420000-0000-4000-8000-000000000010"), 0.2,
                "player", "player-party", 20, 0, true, DateTimeOffset.UnixEpoch, null);
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 || !profile.RequiredAtRuntime ||
                profile.Entries.Count == 0 || profile.Entries.Any(item => !item.DropOnDeath || !item.Pickpocketable) ||
                !profile.Theft.Enabled || !profile.Theft.RequireServerConsent || profile.Theft.CooldownSeconds != 30 ||
                profile.Corpse.Mode != CreatureCorpseMode.RagdollContainer || !profile.Corpse.AllowResurrection ||
                profile.Corpse.CleanupMode != CorpseCleanupMode.Timed ||
                CreatureLootValidator.Validate(profile, availableItemSlugs: itemIds, availableSkillIds: skillIds).Count != 0 ||
                !death.Succeeded || !death.Corpse.Created || death.Corpse.Contents.Count == 0 ||
                !theft.Succeeded || theft.Grant is null || _creatureLootWindow.SummarySectionCount != 3 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureLoot?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} entries={profile?.Entries.Count ?? -1} theft={profile?.Theft.Enabled} " +
                    $"corpse={profile?.Corpse.Mode} death={death.Succeeded}/{death.Corpse.Contents.Count} " +
                    $"pickpocket={theft.Succeeded} summary={_creatureLootWindow.SummarySectionCount}");
                return false;
            }

            Undo(); lootApplied = false;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureLoot?.Id != originalLootId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED undo");
                return false;
            }
            Redo(); lootApplied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureLoot?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED redo");
                return false;
            }
            Undo(); lootApplied = false;
            if (progressionApplied)
            {
                Undo(); progressionApplied = false;
                if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id != originalProgressionId)
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED progression_restore");
                    return false;
                }
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_PASS entries=item-slot-creature probability=bounded " +
                "ownership=resolved instancing=shared-player-party theft=skill-perception-server-consent-crime-cooldown-audit " +
                "corpse=container-ragdoll-lifetime-resurrection-ownership-cleanup ui=visible-empty-error-summary " +
                "uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated " +
                "history=restored persistence=project-roundtrip runtime=legacy-safe");
            return true;
        }
        finally
        {
            _creatureLootWindow.Hide();
            if (lootApplied) _history.Undo();
            if (progressionApplied) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunCreatureRoleBehaviorSmokeTest()
    {
        if (_history is null || _creatureRoleBehaviorWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED missing_workspace");
            return false;
        }

        var progressionApplied = false;
        var behaviorApplied = false;
        try
        {
            _creatureRoleBehaviorWindow.Open();
            var visible = _creatureRoleBehaviorWindow.Visible;
            _creatureRoleBehaviorWindow.Hide();
            var blueprintId = _creatureRoleBehaviorWindow.SelectedBlueprintId;
            if (!visible || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED selection_ui");
                return false;
            }

            var original = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var originalBehaviorId = original.CreatureRoleBehavior?.Id;
            var originalProgressionId = original.CreatureProgression?.Id;
            if (original.CreatureProgression is null || original.CreatureProgression.Tracks.Count == 0)
            {
                var progression = CreatureProgressionProfile.CreateDefault(
                    Guid.Parse("a1460000-0000-4000-8000-000000000001"), "smoke:g13-e");
                var command = new SetCreatureProgressionCommand(CommandId.From(Guid.NewGuid()),
                    _history.State.Revision, blueprintId, progression, false);
                if (!ApplyCommand(_creatureProgressionHandler.Handle(_history.State, command),
                        "Préparation du test de rôle et comportement.", null))
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED progression_fixture");
                    return false;
                }
                progressionApplied = true;
            }

            var beforeTemplate = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId)
                .CreatureProgression!;
            var beforeLevel = beforeTemplate.Tracks.Sum(item => item.Level);
            _creatureRoleBehaviorWindow.Refresh();
            var configured = _creatureRoleBehaviorWindow.ConfigureForSmokeTest();
            var previewed = configured && _creatureRoleBehaviorWindow.PreviewForSmokeTest();
            var applied = previewed && _creatureRoleBehaviorWindow.LastCommandId != Guid.Empty &&
                          _creatureRoleBehaviorWindow.LastPatchSummary.Contains("role and behavior",
                              StringComparison.OrdinalIgnoreCase) &&
                          _creatureRoleBehaviorWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_creatureRoleBehaviorWindow.LastPatchSummary} diagnostics={_creatureRoleBehaviorWindow.LastDiagnostics}");
                return false;
            }

            behaviorApplied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureRoleBehavior;
            var runtime = CreatureRoleBehaviorRuntimeResolver.Resolve(profile, blueprint.CreatureProgression,
                21 * 60, false, required: true);
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                !profile.RequiredAtRuntime || profile.Role.LoreFields.GetValueOrDefault("deity") != "deity.embers" ||
                profile.Role.RoleTags.Count < 2 || profile.Ambient.IdleSets.Count != 2 ||
                profile.Ambient.Schedules.Count != 1 || profile.Ambient.Interactions.Count != 1 ||
                profile.AutoLevel.Seed != 42 || profile.AutoLevel.ManualLock || !profile.AutoLevel.ApplyOnSave ||
                CreatureRoleBehaviorValidator.Validate(profile, blueprint.CreatureProgression).Count != 0 ||
                !runtime.Succeeded || runtime.ActiveAnimationId is null || runtime.AutoLevel is null ||
                blueprint.CreatureProgression!.Tracks.Sum(item => item.Level) <= beforeLevel ||
                _creatureRoleBehaviorWindow.SummarySectionCount != 3 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureRoleBehavior?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} lore={profile?.Role.LoreFields.Count ?? -1} " +
                    $"idle={profile?.Ambient.IdleSets.Count ?? -1} runtime={runtime.Succeeded}/{runtime.ActiveAnimationId} " +
                    $"level={blueprint.CreatureProgression?.Tracks.Sum(item => item.Level)} summary={_creatureRoleBehaviorWindow.SummarySectionCount}");
                return false;
            }

            Undo(); behaviorApplied = false;
            var undone = _history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id);
            if (undone.CreatureRoleBehavior?.Id != originalBehaviorId ||
                undone.CreatureProgression?.Tracks.Sum(item => item.Level) != beforeLevel)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED undo");
                return false;
            }
            Redo(); behaviorApplied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureRoleBehavior?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED redo");
                return false;
            }
            Undo(); behaviorApplied = false;
            if (progressionApplied)
            {
                Undo(); progressionApplied = false;
                if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id !=
                    originalProgressionId)
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED progression_restore");
                    return false;
                }
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_PASS role=ruleset-lore-tags-no-implicit-mechanics " +
                "ambient=idle-schedule-interaction-waypoint-conversation-interruption-resume " +
                "autolevel=deterministic-seed-preview-diff-apply-manual-lock ui=visible-empty-error-summary " +
                "uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated " +
                "history=restored persistence=project-roundtrip runtime=optional-safe");
            return true;
        }
        finally
        {
            _creatureRoleBehaviorWindow.Hide();
            if (behaviorApplied) _history.Undo();
            if (progressionApplied) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunCreatureEventSmokeTest()
    {
        if (_history is null || _creatureEventWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED missing_workspace");
            return false;
        }

        var appliedToHistory = false;
        try
        {
            _creatureEventWindow.Open();
            var visible = _creatureEventWindow.Visible;
            _creatureEventWindow.Hide();
            var blueprintId = _creatureEventWindow.SelectedBlueprintId;
            if (!visible || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED selection_ui");
                return false;
            }

            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = _creatureEventWindow.ConfigureForSmokeTest();
            var previewed = configured && _creatureEventWindow.PreviewForSmokeTest();
            var applied = previewed && _creatureEventWindow.LastCommandId != Guid.Empty &&
                          _creatureEventWindow.LastPatchSummary.Contains("creature-event",
                              StringComparison.OrdinalIgnoreCase) &&
                          _creatureEventWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_creatureEventWindow.LastPatchSummary} diagnostics={_creatureEventWindow.LastDiagnostics}");
                return false;
            }

            appliedToHistory = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureEvents;
            var heartbeat = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile,
                CreatureEventRuntimeState.Empty, 1_500, true, blueprint.Slug, required: true);
            var suspended = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile,
                CreatureEventRuntimeState.Empty, 1_500, false, blueprint.Slug, required: true);
            var custom = profile is null || profile.CustomEvents.Count == 0 ? null :
                CreatureEventRuntimeResolver.Custom(profile, profile.CustomEvents[0].SchemaId,
                    new Dictionary<string, string> { ["count"] = "3", ["source"] = blueprint.Slug });
            var damageTransaction = Guid.Parse("a1590000-0000-4000-8000-000000000001");
            var beforeDamage = CreatureEventRuntimeResolver.BeforeDamage(profile, damageTransaction,
                blueprint.Slug, "hero", 12, "radiant", "smoke");
            var damageApplied = CreatureEventRuntimeResolver.DamageApplied(profile, damageTransaction,
                blueprint.Slug, "hero", 12, 10, 2, "radiant", "smoke");
            var died = CreatureEventRuntimeResolver.EntityDied(profile, blueprint.Slug, "hero",
                ["hero", "elyra"], "smoke", "crypt:4,0,9", ["warden-ember"], "drop");
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                !profile.RequiredAtRuntime || profile.CombatBoundary?.Mode != CreatureCombatBoundaryMode.CombatTickCompleted ||
                profile.Inventory is null || profile.Rest is null || profile.Heartbeat is not { Enabled: true } ||
                profile.Heartbeat.IntervalMilliseconds != 1_500 || profile.Heartbeat.BudgetMilliseconds != 8 ||
                !profile.Heartbeat.SuspendOutsideInterest || profile.CustomEvents.Count != 1 ||
                CreatureEventValidator.Validate(profile).Count != 0 || heartbeat.Dispatches.Count != 1 ||
                heartbeat.Dispatches[0].BudgetMilliseconds != 8 || suspended.Dispatches.Count != 0 ||
                custom?.EventName != "custom.crypt.braziers_lit" || profile.DamageDeath is null ||
                beforeDamage?.EventName != "creature.before_damage" || beforeDamage.BudgetMilliseconds != 4 ||
                damageApplied?.EventName != "creature.damage_applied" || died?.EventName != "entity.died" ||
                _creatureEventWindow.SummarySectionCount != 4 ||
                ProjectJsonSerializer.Serialize(restored) != appliedJson ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureEvents?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} heartbeat={heartbeat.Dispatches.Count} suspended={suspended.Dispatches.Count} " +
                    $"custom={custom?.EventName} summary={_creatureEventWindow.SummarySectionCount}");
                return false;
            }

            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED undo");
                return false;
            }
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED redo");
                return false;
            }
            Undo(); appliedToHistory = false;

            GD.Print("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_PASS combat=tick-or-turn " +
                "inventory=accessed-transferred-cause-actor-result rest=duration-resources-interruptions " +
                "heartbeat=cadence-suspension-budget-backlog custom=namespaced-typed ui=visible-empty-error-summary " +
                "uuid=stable schema=v1 provenance=tracked forgescript=api-1.0-server transaction=identified " +
                "dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=optional-safe");
            GD.Print("OPENRPG_FORGE_TOOLSET_R039_CREATURE_DAMAGE_DEATH_PASS " +
                "damage=before-restricted-after-transaction death=killer-contributors-cause-location-loot-policy " +
                "validation=rig-rules-equipment-capabilities-scripts-faction-resources-budgets " +
                "ui=sections-summary transaction=dry-run-apply history=undo-redo persistence=roundtrip " +
                "forgescript=typed-handle mcp=filtered-dry-run runtime=ordered-authoritative");
            return true;
        }
        finally
        {
            _creatureEventWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }


}
#endif
