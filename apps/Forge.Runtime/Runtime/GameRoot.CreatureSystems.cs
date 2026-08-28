using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Areas;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Creatures;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Localization;
using Forge.Application.Modules;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.Rules;
using Forge.Application.Quality;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.RuntimeExtensions;
using Forge.Application.Scripting;
using Forge.Application.Entities;
using Forge.Application.Combat;
using Forge.Application.Factions;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Audio;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Lifecycle;
using Forge.Domain.Localization;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Quality;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Production;
using Forge.Network.Observability;
using Forge.Network.Testing;
using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Forge.Network.World;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using Forge.RpgKit.Domain.Merchants;
using RpgAttackContext = Forge.RpgKit.Domain.Scripting.AttackContext;
using RpgDamageSpec = Forge.RpgKit.Domain.Scripting.DamageSpec;
using RpgEffectDefinition = Forge.RpgKit.Domain.Scripting.EffectDefinition;
using RpgEffectSpec = Forge.RpgKit.Domain.Scripting.EffectSpec;
using RpgEffectStacking = Forge.RpgKit.Domain.Scripting.EffectStacking;
using RpgScriptCombatState = Forge.RpgKit.Domain.Scripting.ScriptCombatState;
using RpgScriptGameplayService = Forge.RpgKit.Domain.Scripting.ScriptGameplayService;
using RpgDialogueRequest = Forge.RpgKit.Domain.Scripting.DialogueRequest;
using RpgFactionState = Forge.RpgKit.Domain.Scripting.FactionState;
using RpgFactionTransaction = Forge.RpgKit.Domain.Scripting.FactionTransaction;
using RpgInventoryState = Forge.RpgKit.Domain.Scripting.InventoryState;
using RpgInventoryTransaction = Forge.RpgKit.Domain.Scripting.InventoryTransaction;
using RpgPerceptionQuery = Forge.RpgKit.Domain.Scripting.PerceptionQuery;
using RpgQuestState = Forge.RpgKit.Domain.Scripting.QuestState;
using RpgQuestTransition = Forge.RpgKit.Domain.Scripting.QuestTransition;
using RpgScriptWorldService = Forge.RpgKit.Domain.Scripting.ScriptWorldService;
using ScriptAudioCue = Forge.Scripting.AudioCue;
using ScriptAudioState = Forge.Scripting.AudioState;
using ScriptCustomEvent = Forge.Scripting.CustomEvent;
using ScriptCustomEventSchema = Forge.Scripting.CustomEventSchema;
using ScriptDebugFrame = Forge.Scripting.DebugFrame;
using ScriptHotReloadModule = Forge.Scripting.HotReloadModule;
using ScriptRuntimeTools = Forge.Scripting.ForgeScriptRuntimeTools;
using ScriptWorldClock = Forge.Scripting.WorldClock;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private bool TryAttachVisual(Node parent, EntityInstance entity, bool applyPlayerVisualTransform = false)
    {
        var authoredAssetId = GetOverride(entity, VisualAssetProperty);
        var assetId = authoredAssetId;
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (_modelLibrary is null)
        {
            throw new InvalidOperationException($"Entity '{entity.Id}' references visual asset '{assetId}' but the module model library is unavailable.");
        }
        var visual = _modelLibrary.Instantiate(assetId);
        if (visual is null)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_VISUAL_FALLBACK entity={entity.Id} asset={assetId}");
            return false;
        }
        visual.Name = "AuthoredVisual";
        if (applyPlayerVisualTransform)
        {
            visual.Basis = EngineValueConverter.ToEngine(entity.Transform).Basis;
            visual.Position = new Vector3(0, -1f, 0);
            visual.RotateY(Mathf.Pi);
        }
        parent.AddChild(visual);
        if (parent is RuntimeFlyingMount)
        {
            EnhanceFlyingMountMaterials(visual);
        }
        if (entity.Tags.Contains("wall", StringComparer.Ordinal) &&
            TryMeasureNodeBounds(visual, out var wallMinimum, out var wallMaximum))
        {
            var center = (wallMinimum + wallMaximum) * .5f;
            visual.Position -= new Vector3(center.X, 0, center.Z);
        }
        _runtimeModelInstances++;
        var animationProfile = GetOverride(entity, RuntimeAnimationProfileProperty);
        var animation = animationProfile switch
        {
            "humanoid-basic" => GodotAssetAnimationController.Configure(visual),
            "kaykit-adventurer" => GodotAssetAnimationController.Configure(
                visual,
                idle: GetOverride(entity, "runtime.animationIdle") ?? "Idle",
                run: GetOverride(entity, "runtime.animationRun") ?? "Running_A",
                jump: GetOverride(entity, "runtime.animationJump") ?? "Jump_Full_Short",
                attack: GetOverride(entity, "runtime.animationAttack") ?? "1H_Melee_Attack_Slice_Horizontal",
                hit: GetOverride(entity, "runtime.animationHit") ?? "Hit_A",
                death: GetOverride(entity, "runtime.animationDeath") ?? "Death_A"),
            "authored-clips" => GodotAssetAnimationController.Configure(
                visual,
                idle: RequiredAnimationOverride(entity, "runtime.animationIdle"),
                run: RequiredAnimationOverride(entity, "runtime.animationRun"),
                jump: RequiredAnimationOverride(entity, "runtime.animationJump"),
                attack: RequiredAnimationOverride(entity, "runtime.animationAttack"),
                hit: RequiredAnimationOverride(entity, "runtime.animationHit"),
                death: RequiredAnimationOverride(entity, "runtime.animationDeath")),
            "quaternius-dragon" => GodotAssetAnimationController.Configure(
                visual,
                idle: GetOverride(entity, "runtime.animationIdle") ?? "Dragon_Flying",
                run: GetOverride(entity, "runtime.animationRun") ?? "Dragon_Flying",
                jump: GetOverride(entity, "runtime.animationJump") ?? "Dragon_Flying",
                attack: GetOverride(entity, "runtime.animationAttack") ?? "Dragon_Attack",
                hit: GetOverride(entity, "runtime.animationHit") ?? "Dragon_Hit",
                death: GetOverride(entity, "runtime.animationDeath") ?? "Dragon_Death"),
            _ => null,
        };
        if (animation is not null)
        {
            GodotAssetAnimationController.Play(animation, animation.IdleClip, 0);
            _animatedCharacterInstances++;
            _animatedCharacterClipNames.UnionWith(animation.Clips);
            if (parent is Node3D character)
            {
                _characterAnimations[character] = animation;
            }
            if (parent is PlayerController player)
            {
                player.ConfigureAnimations(animation);
            }
            if (parent is RuntimeFlyingMount mount)
            {
                mount.ConfigureAnimations(animation);
            }
        }
        if (applyPlayerVisualTransform) AlignPlayerVisualToCapsule(visual);
        return true;
    }

    private static void AlignPlayerVisualToCapsule(Node3D visual)
    {
        if (!TryMeasureNodeBounds(visual, out var minimum, out var maximum)) return;
        const float capsuleBottom = -.9f;
        var center = (minimum + maximum) * .5f;
        visual.Position += new Vector3(-center.X, capsuleBottom - minimum.Y, -center.Z);
    }

    private static void EnhanceFlyingMountMaterials(Node3D visual)
    {
        foreach (var node in visual.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh is null) continue;
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source) continue;
                var material = (BaseMaterial3D)source.Duplicate(true);
                var palette = source.ResourceName.ToLowerInvariant() switch
                {
                    "main" => new Color("bd6570"),
                    "wings" => new Color("896176"),
                    "belly" => new Color("d6a62d"),
                    "claws" => new Color("302d39"),
                    "eyes" => new Color("d9c69a"),
                    _ => source.AlbedoColor,
                };
                material.AlbedoColor = new Color(palette.R, palette.G, palette.B, 1);
                material.Metallic = .04f;
                material.Roughness = .72f;
                mesh.SetSurfaceOverrideMaterial(surface, material);
            }
        }
    }

    private static bool HasRuntimeRole(EntityInstance entity, string role) =>
        string.Equals(GetOverride(entity, RuntimeRoleProperty), role, StringComparison.OrdinalIgnoreCase);

    private void ConfigureCreatureFoundation(Node3D creature, BlueprintDefinition blueprint)
    {
        var resolved = CreatureFoundationRuntimeResolver.Resolve(blueprint.CreatureFoundation,
            blueprint.CreatureFoundation?.RequiredAtRuntime == true);
        if (!resolved.Succeeded)
            throw new InvalidOperationException(string.Join("; ", resolved.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (resolved.Profile is null) return;
        var profile = resolved.Profile;
        creature.SetMeta("forge_creature_foundation_id", profile.Id.ToString("D"));
        creature.SetMeta("forge_creature_movement_mode", profile.Movement.Mode.ToString());
        creature.SetMeta("forge_creature_spawn_handler", profile.Spawned.Handler);
        creature.SetMeta("forge_creature_resources", string.Join(',', profile.Resources.Select(item =>
            $"{item.ResourceId}:{item.Current:0.##}/{item.Maximum:0.##}")));
        var authored = CreatureAuthoringCompositionRuntimeResolver.Resolve(profile.AuthoringComposition,
            profile.AuthoringCompositionRequiredAtRuntime);
        if (!authored.Succeeded)
            throw new InvalidOperationException(string.Join("; ", authored.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (authored.Profile is not null)
        {
            var composition = authored.Profile;
            var root = new Node3D { Name = "CreatureAuthoringComposition" };
            root.SetMeta("forge_creature_authoring_id", composition.Id.ToString("D"));
            root.SetMeta("forge_creature_encounter_role", composition.EncounterRole.ToString());
            root.SetMeta("forge_creature_threat_rating", composition.ThreatRating);
            root.SetMeta("forge_creature_species", composition.Species.SpeciesId);
            root.SetMeta("forge_creature_mesh_asset", composition.Appearance.MeshAssetId);
            creature.AddChild(root);
            root.AddChild(new Node { Name = $"Role_{composition.EncounterRole}" });
            root.AddChild(new Node { Name = $"Species_{SafeNodeName(composition.Species.SpeciesId)}" });
            root.AddChild(new Node3D { Name = $"Model_{SafeNodeName(composition.Appearance.MeshAssetId)}" });
            var graph = new AnimationPlayer { Name = "AnimationGraph" };
            graph.SetMeta("forge_retarget_profile", composition.AnimationGraph.RetargetProfileId);
            root.AddChild(graph);
            foreach (var state in composition.AnimationGraph.States)
            {
                var stateNode = new Node { Name = $"State_{SafeNodeName(state.StateId)}" };
                stateNode.SetMeta("forge_animation_category", state.Category);
                stateNode.SetMeta("forge_animation_clip", state.ClipAssetId);
                graph.AddChild(stateNode);
            }
            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureAuthoringCompositionScriptService.Read(loaded,
                    CreatureAuthoringCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureAuthoringCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var optional = CreatureAuthoringCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureAuthoringCompositionRuntimeResolver.Resolve(null, true);
                var hasVisibleModel = creature.GetChildren().OfType<MeshInstance3D>().Any() ||
                    creature.GetNodeOrNull<Node3D>("AuthoredVisual") is not null;
                AssertSmoke(root.GetChildCount() == 4 && graph.GetChildCount() == composition.AnimationGraph.States.Count &&
                            hasVisibleModel && script?.AnimationStates.Count == 5 && rpg.Succeeded &&
                            rpg.Creature?.Species?.Id == "species.emberborn" && optional.Succeeded && optional.UsedFallback &&
                            required.Diagnostics.Single().Code == CreatureAuthoringCompositionValidator.MissingRequiredCode,
                    "Creature authoring runtime adapter did not preserve model, species and animation nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R077_CREATURE_AUTHORING_PASS " +
                    "role=encounter-threat-tags species=inheritance-traits-variants-skeleton " +
                    "appearance=archetype-mesh-rig-morph-material-palette-preset-visible-model " +
                    "animation=graph-states-transitions-locomotion-combat-emote-additive-retarget " +
                    "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic " +
                    "rpgkit=validated-adapter engine=role-species-model-animation-nodes");
            }
        }
        var ruled = CreatureRulesCompositionRuntimeResolver.Resolve(profile.RulesComposition,
            profile.RulesCompositionRequiredAtRuntime);
        if (!ruled.Succeeded)
            throw new InvalidOperationException(string.Join("; ", ruled.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (ruled.Profile is not null)
        {
            var rules = ruled.Profile;
            var root = new Node3D { Name = "CreatureRulesComposition" };
            root.SetMeta("forge_creature_rules_id", rules.Id.ToString("D"));
            root.SetMeta("forge_creature_rules_asset", rules.AssetId);
            creature.AddChild(root);

            var initial = new AnimationPlayer { Name = "InitialStateAndPose" };
            initial.SetMeta("forge_initial_life_state", rules.InitialState.LifeState.ToString());
            initial.SetMeta("forge_initial_animation_loop", rules.InitialState.AnimationLoopAssetId ?? string.Empty);
            initial.SetMeta("forge_initial_pose", rules.InitialState.PoseId ?? string.Empty);
            initial.SetMeta("forge_initial_invisible", rules.InitialState.Invisible);
            root.AddChild(initial);

            var attributes = new Node { Name = "Attributes" };
            var evaluated = CreatureRulesCompositionValidator.EvaluateAttributes(rules.Attributes);
            attributes.SetMeta("forge_ruleset", rules.Attributes.RulesetId);
            attributes.SetMeta("forge_attribute_count", evaluated.Count);
            foreach (var attribute in evaluated)
            {
                var node = new Node { Name = $"Attribute_{SafeNodeName(attribute.Key)}" };
                node.SetMeta("forge_attribute_value", attribute.Value);
                attributes.AddChild(node);
            }
            root.AddChild(attributes);

            var defenses = new Node { Name = "DefensesAndSaves" };
            defenses.SetMeta("forge_avoidance", rules.Defenses.Avoidance);
            defenses.SetMeta("forge_armor", rules.Defenses.Armor);
            defenses.SetMeta("forge_resistances", string.Join(',', rules.Defenses.Resistances.Select(item =>
                $"{item.Key}:{item.Value:0.###}")));
            defenses.SetMeta("forge_saves", string.Join(',', rules.Defenses.Saves.Select(item =>
                $"{item.Key}:{item.Value:0.###}")));
            defenses.SetMeta("forge_immunities", string.Join(',', rules.Defenses.Immunities));
            root.AddChild(defenses);

            var abilities = new Node { Name = "SpecialAbilities" };
            foreach (var ability in rules.Abilities)
            {
                var node = new Node { Name = $"Ability_{SafeNodeName(ability.AbilityId)}" };
                node.SetMeta("forge_ability_level", ability.Level);
                node.SetMeta("forge_ability_charges", ability.Charges);
                node.SetMeta("forge_ability_cooldown", ability.CooldownSeconds);
                node.SetMeta("forge_ability_script", ability.ScriptId);
                abilities.AddChild(node);
            }
            root.AddChild(abilities);

            var inventory = new Node { Name = "Inventory" };
            inventory.SetMeta("forge_inventory_owner", rules.Inventory.OwnerId);
            inventory.SetMeta("forge_inventory_persistent", rules.Inventory.Persistent);
            foreach (var container in rules.Inventory.Containers)
            {
                var containerNode = new Node { Name = $"Container_{SafeNodeName(container.ContainerId)}" };
                containerNode.SetMeta("forge_inventory_slots", container.SlotCount);
                containerNode.SetMeta("forge_inventory_max_weight", container.MaximumWeight);
                containerNode.SetMeta("forge_inventory_max_volume", container.MaximumVolume);
                foreach (var entry in container.Items)
                {
                    var itemNode = new Node { Name = $"Item_{SafeNodeName(entry.Id.ToString("D"))}" };
                    itemNode.SetMeta("forge_item_blueprint", entry.ItemBlueprintId.ToString());
                    itemNode.SetMeta("forge_item_quantity", entry.Quantity);
                    containerNode.AddChild(itemNode);
                }
                inventory.AddChild(containerNode);
            }
            root.AddChild(inventory);

            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureRulesCompositionScriptService.Read(loaded,
                    CreatureRulesCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureRulesCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var optional = CreatureRulesCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureRulesCompositionRuntimeResolver.Resolve(null, true);
                AssertSmoke(root.GetChildCount() == 5 && initial.GetMeta("forge_initial_life_state").AsString() == "Seated" &&
                            evaluated.GetValueOrDefault("evasion") == 16 && abilities.GetChildCount() == 1 &&
                            inventory.GetChildCount() == 1 && script?.InventoryItemCount == 1 && rpg.Succeeded &&
                            rpg.Creature?.Abilities?.Single().AbilityId == "ability.ash-nova" &&
                            optional.Succeeded && optional.UsedFallback && required.Diagnostics.Single().Code ==
                            CreatureRulesCompositionValidator.MissingRequiredCode,
                    "Creature rules runtime adapter did not preserve initial state, rules and inventory nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R078_CREATURE_RULES_PASS " +
                    "initial=alive-unconscious-dead-sleeping-seated-frozen-loop-pose-invisible " +
                    "attributes=ruleset-base-modifiers-derived-inspectable " +
                    "defenses=avoidance-armor-resistances-saves-immunities-mitigation " +
                    "abilities=level-charges-cooldown-conditions-ai-tags-script " +
                    "inventory=containers-slots-weight-volume-ownership-equipment-persistence " +
                    "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip " +
                    "optional=ignored required=diagnostic rpgkit=validated-adapter " +
                    "engine=state-attributes-defenses-abilities-inventory-nodes");
            }
        }
        var interacted = CreatureInteractionCompositionRuntimeResolver.Resolve(profile.InteractionComposition,
            profile.InteractionCompositionRequiredAtRuntime);
        if (!interacted.Succeeded)
            throw new InvalidOperationException(string.Join("; ", interacted.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (interacted.Profile is not null)
        {
            var interaction = interacted.Profile;
            var root = new Node3D { Name = "CreatureInteractionComposition" };
            root.SetMeta("forge_creature_interaction_id", interaction.Id.ToString("D"));
            root.SetMeta("forge_creature_interaction_asset", interaction.AssetId);
            creature.AddChild(root);

            var factions = new Node { Name = "Factions" };
            factions.SetMeta("forge_primary_faction", interaction.Factions.PrimaryFactionId.ToString("D"));
            foreach (var membership in interaction.Factions.Memberships)
            {
                var member = new Node { Name = $"Membership_{SafeNodeName(membership.FactionId.ToString("D"))}" };
                member.SetMeta("forge_faction_weight", membership.Weight);
                member.SetMeta("forge_faction_rank", membership.Rank);
                member.SetMeta("forge_faction_reputation", membership.PersonalReputation);
                member.SetMeta("forge_faction_inherited", membership.Inherited);
                factions.AddChild(member);
            }
            root.AddChild(factions);

            var perception = new Node3D { Name = "Perception" };
            perception.SetMeta("forge_perception_memory_seconds", interaction.Perception.MemorySeconds);
            foreach (var sense in interaction.Perception.Senses)
            {
                var sensor = new Area3D { Name = $"Sense_{sense.Kind}" };
                sensor.SetMeta("forge_sense_range", sense.Range);
                sensor.SetMeta("forge_sense_cone_degrees", sense.ConeDegrees);
                sensor.SetMeta("forge_sense_occlusion", sense.UsesOcclusion);
                sensor.SetMeta("forge_sense_stimuli", string.Join(',', sense.StimulusTags));
                perception.AddChild(sensor);
            }
            root.AddChild(perception);

            var dialogues = new Node { Name = "DialogueBindings" };
            foreach (var binding in interaction.DialogueBindings)
            {
                var node = new Node { Name = $"Dialogue_{SafeNodeName(binding.Id.ToString("D"))}" };
                node.SetMeta("forge_dialogue_context", binding.Context);
                node.SetMeta("forge_dialogue_priority", binding.Priority);
                node.SetMeta("forge_dialogue_id", binding.DialogueId.ToString());
                node.SetMeta("forge_dialogue_interlocutor", binding.InterlocutorId ?? string.Empty);
                node.SetMeta("forge_dialogue_state", binding.RequiredState ?? string.Empty);
                node.SetMeta("forge_dialogue_fallback", binding.Fallback);
                dialogues.AddChild(node);
            }
            root.AddChild(dialogues);

            var protection = new Node { Name = "ProtectionPolicy" };
            protection.SetMeta("forge_invulnerable", interaction.Protection.Invulnerable);
            protection.SetMeta("forge_narrative_death", interaction.Protection.UsesNarrativeDeath);
            protection.SetMeta("forge_dialogue_interruptible", interaction.Protection.DialogueInterruptible);
            protection.SetMeta("forge_gm_permissions", string.Join(',', interaction.Protection.GameMasterPermissions));
            root.AddChild(protection);

            var onConversation = new Node { Name = "OnConversation" };
            onConversation.SetMeta("forge_event_name", interaction.OnConversation.EventName);
            onConversation.SetMeta("forge_event_handler", interaction.OnConversation.Handler);
            onConversation.SetMeta("forge_event_authority", interaction.OnConversation.Authority.ToString());
            root.AddChild(onConversation);

            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureInteractionCompositionScriptService.Read(loaded,
                    CreatureInteractionCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureInteractionCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var decision = CreatureInteractionCompositionValidator.RequestDialogue(interaction,
                    "greeting", "player.hero", "state.hostile");
                var protectedDecision = CreatureInteractionCompositionValidator.RequestDialogue(interaction,
                    "interrupt", "player.hero", null);
                var optional = CreatureInteractionCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureInteractionCompositionRuntimeResolver.Resolve(null, true);
                AssertSmoke(root.GetChildCount() == 5 && factions.GetChildCount() == 2 &&
                            perception.GetChildCount() == 4 && dialogues.GetChildCount() == 3 &&
                            protection.GetMeta("forge_invulnerable").AsBool() && decision.Accepted &&
                            decision.Binding?.Priority == 20 && !protectedDecision.Accepted &&
                            protectedDecision.RefusalCode == "creature.interaction.dialogue_protected" &&
                            script?.Memberships.Count == 2 && script.Senses.Count == 4 && rpg.Succeeded &&
                            rpg.Creature?.Factions?.Memberships.Count == 2 &&
                            optional.Succeeded && optional.UsedFallback && required.Diagnostics.Single().Code ==
                            CreatureInteractionCompositionValidator.MissingRequiredCode,
                    "Creature interaction runtime adapter did not preserve factions, senses, dialogue and protection nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R079_CREATURE_INTERACTION_PASS " +
                    "factions=multiple-weighted-primary-rank-reputation-inheritance " +
                    "perception=vision-hearing-smell-magic-range-cone-occlusion-stimuli-memory " +
                    "dialogue=context-priority-interlocutor-state-fallback-selection-refusal " +
                    "protection=invulnerability-narrative-death-interruption-gm-permissions " +
                    "onconversation=interaction-requested-dialogue-starting-server-binding " +
                    "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip " +
                    "optional=ignored required=diagnostic rpgkit=validated-adapter " +
                    "engine=factions-perception-dialogue-protection-event-nodes");
            }
        }
        var eventAiResolved = CreatureEventAiCompositionRuntimeResolver.Resolve(profile.EventAiComposition,
            profile.EventAiCompositionRequiredAtRuntime);
        if (!eventAiResolved.Succeeded)
            throw new InvalidOperationException(string.Join("; ", eventAiResolved.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (eventAiResolved.Profile is not null)
        {
            var eventAi = eventAiResolved.Profile;
            var root = new Node3D { Name = "CreatureEventAiComposition" };
            root.SetMeta("forge_creature_event_ai_id", eventAi.Id.ToString("D"));
            root.SetMeta("forge_creature_event_ai_asset", eventAi.AssetId);
            creature.AddChild(root);

            var onPerception = new Node { Name = "OnPerception" };
            AddEventNode(onPerception, "StimulusDetected", eventAi.Perception.Detected);
            AddEventNode(onPerception, "StimulusLost", eventAi.Perception.Lost);
            root.AddChild(onPerception);

            var onPhysicalAttacked = new Node { Name = "OnPhysicalAttacked" };
            AddEventNode(onPhysicalAttacked, "AttackDeclared", eventAi.PhysicalAttack.Declared);
            AddEventNode(onPhysicalAttacked, "AttackResolved", eventAi.PhysicalAttack.Resolved);
            root.AddChild(onPhysicalAttacked);

            var onSpellCastAt = new Node { Name = "OnSpellCastAt" };
            AddEventNode(onSpellCastAt, "AbilityTargeted", eventAi.Ability.Targeted);
            AddEventNode(onSpellCastAt, "AbilityImpacted", eventAi.Ability.Impacted);
            root.AddChild(onSpellCastAt);

            var onBlocked = new Node { Name = "OnBlocked" };
            AddEventNode(onBlocked, "MovementBlocked", eventAi.Blocked.Blocked);
            onBlocked.SetMeta("forge_recovery_order", string.Join(',', eventAi.Blocked.RecoveryOrder));
            root.AddChild(onBlocked);

            var controller = new Node { Name = "AiController" };
            controller.SetMeta("forge_ai_kind", eventAi.Controller.Kind.ToString());
            controller.SetMeta("forge_ai_definition", eventAi.Controller.DefinitionId);
            controller.SetMeta("forge_ai_allowed_actions", string.Join(',', eventAi.Controller.AllowedActions));
            foreach (var entry in eventAi.Controller.Blackboard)
            {
                var blackboardNode = new Node { Name = $"Blackboard_{SafeNodeName(entry.Key)}" };
                blackboardNode.SetMeta("forge_blackboard_type", entry.Kind.ToString());
                blackboardNode.SetMeta("forge_blackboard_value", entry.Value);
                blackboardNode.SetMeta("forge_blackboard_persistent", entry.Persistent);
                controller.AddChild(blackboardNode);
            }
            root.AddChild(controller);

            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureEventAiCompositionScriptService.Read(loaded,
                    CreatureEventAiCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureEventAiCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var perceptionEvents = Forge.RpgKit.Domain.Creatures.CreatureInteractionService.DiffPerception(
                    eventAi.Id,
                    [new Forge.RpgKit.Domain.Creatures.PerceptionResult("old",
                        Forge.RpgKit.Domain.Creatures.SenseKind.Hearing, .4, Vector3Value.Zero, 10)],
                    [new Forge.RpgKit.Domain.Creatures.PerceptionResult("new",
                        Forge.RpgKit.Domain.Creatures.SenseKind.Vision, .8, new Vector3Value(2, 0, 1), 20)],
                    Guid.NewGuid);
                var network = new Forge.RpgKit.Domain.Creatures.AttackNetworkContext("server", 42, 3, false);
                var attack = Forge.RpgKit.Domain.Creatures.CreatureInteractionService.ResolveAttack(
                    new Forge.RpgKit.Domain.Creatures.AttackDeclared(Guid.NewGuid(), eventAi.Id, "player.hero",
                        "weapon.sword", "ash-warden-boss", network), "hit", 7, Guid.NewGuid);
                var ability = Forge.RpgKit.Domain.Creatures.CreatureAiService.Impact(
                    new Forge.RpgKit.Domain.Creatures.AbilityTargeted(Guid.NewGuid(), eventAi.Id, "player.hero",
                        "ability.firebolt", new HashSet<string> { "fire" }, "entity"), "resisted", 3,
                    Guid.NewGuid);
                var blocked = Forge.RpgKit.Domain.Creatures.CreatureAiService.Blocked(eventAi.Id, "pillar.ash",
                    "route", "dynamic-obstacle", Vector3Value.Zero, Guid.NewGuid);
                var decision = Forge.RpgKit.Domain.Creatures.CreatureAiService.Decide(rpg.Controller!,
                    new Dictionary<string, double> { ["attack"] = .9, ["guard"] = .4, ["patrol"] = .2 });
                var optional = CreatureEventAiCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureEventAiCompositionRuntimeResolver.Resolve(null, true);
                AssertSmoke(root.GetChildCount() == 5 && onPerception.GetChildCount() == 2 &&
                            onPhysicalAttacked.GetChildCount() == 2 && onSpellCastAt.GetChildCount() == 2 &&
                            onBlocked.GetChildCount() == 1 && controller.GetChildCount() == 3 &&
                            perceptionEvents.Detected.Single().StimulusId == "new" &&
                            perceptionEvents.Lost.Single().StimulusId == "old" && attack.AppliedDamage == 7 &&
                            ability.Result == "resisted" &&
                            blocked.Recovery == Forge.RpgKit.Domain.Creatures.MovementRecoveryStrategy.Avoid &&
                            decision.Action == "attack" && script?.Blackboard.Count == 3 && rpg.Succeeded &&
                            optional.Succeeded && optional.UsedFallback && required.Diagnostics.Single().Code ==
                            CreatureEventAiCompositionValidator.MissingRequiredCode,
                    "Creature event/AI runtime adapter did not preserve typed events, recovery or controller nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R080_CREATURE_EVENT_AI_PASS " +
                    "onperception=stimulus-detected-lost-sense-confidence-position-memory " +
                    "onphysicalattacked=declared-resolved-attacker-weapon-target-result-network " +
                    "onspellcastat=ability-targeted-impacted-source-target-tags-result-magnitude " +
                    "onblocked=obstacle-cause-nav-status-position-wait-repath-avoid-teleport-abort " +
                    "ai=state-machine-behavior-tree-utility-script-typed-inspectable-blackboard-bounded-actions " +
                    "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip " +
                    "optional=ignored required=diagnostic rpgkit=validated-adapter " +
                    "engine=perception-attack-ability-blocked-ai-nodes");
            }
        }
        if (blueprint.Slug == "ash-warden-boss")
            GD.Print("OPENRPG_FORGE_RUNTIME_R038_CREATURE_FOUNDATION_PASS identity=localized resources=health-stamina " +
                     $"movement={profile.Movement.Mode.ToString().ToLowerInvariant()} spawn=validated-after-construction " +
                     "forgescript=safe-handle optional=empty-safe required=diagnostic");

        static void AddEventNode(Node owner, string name, EventBinding binding)
        {
            var node = new Node { Name = name };
            node.SetMeta("forge_event_name", binding.EventName);
            node.SetMeta("forge_event_handler", binding.Handler);
            node.SetMeta("forge_event_authority", binding.Authority.ToString());
            owner.AddChild(node);
        }
    }

    private void ConfigureCreatureRoleBehavior(Node3D creature, BlueprintDefinition blueprint)
    {
        var profile = blueprint.CreatureRoleBehavior;
        var snapshot = CreatureRoleBehaviorRuntimeResolver.Resolve(profile, blueprint.CreatureProgression,
            21 * 60, interrupted: false, required: profile?.RequiredAtRuntime == true);
        if (!snapshot.Succeeded)
            throw new InvalidOperationException($"Creature '{blueprint.Slug}' role/behavior is invalid: " +
                string.Join("; ", snapshot.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        if (snapshot.IgnoredOptionalProfile) return;
        _creatureRoleBehaviors[creature] = snapshot;
        _creatureAmbientOrigins[creature] = creature.Position;
        _creatureAmbientRotations[creature] = creature.Rotation;
        _creatureAmbientPhases[creature] = StableLootRoll(blueprint.Id.ToString()) * Math.Tau;
        if (_characterAnimations.TryGetValue(creature, out var animation) && snapshot.ActiveAnimationId is { } clip)
        {
            var resolvedClip = animation.Clips.FirstOrDefault(item => string.Equals(item, clip,
                StringComparison.OrdinalIgnoreCase)) ?? animation.IdleClip;
            GodotAssetAnimationController.Play(animation, resolvedClip, .1);
        }
        GD.Print($"OPENRPG_FORGE_CREATURE_AMBIENT_READY blueprint={blueprint.Slug} " +
            $"roles={snapshot.RoleTags.Count} lore={snapshot.LoreFields.Count} " +
            $"set={snapshot.ActiveIdleSetId} animation={snapshot.ActiveAnimationId} " +
            $"autolevel_changes={snapshot.AutoLevel?.Changes.Count ?? 0}");
    }

    private void AdvanceCreatureAmbientBehavior(double delta)
    {
        foreach (var creature in _creatureRoleBehaviors.Keys.ToArray())
        {
            if (!GodotObject.IsInstanceValid(creature))
            {
                RemoveCreatureRoleBehavior(creature);
                continue;
            }
            var phase = _creatureAmbientPhases[creature] + Math.Max(0, delta);
            _creatureAmbientPhases[creature] = phase;
            var origin = _creatureAmbientOrigins[creature];
            var rotation = _creatureAmbientRotations[creature];
            creature.Position = origin + Vector3.Up * (float)(Math.Sin(phase * 1.3) * .025);
            creature.Rotation = rotation + Vector3.Up * (float)(Math.Sin(phase * .45) * .06);
            _creatureAmbientTicks++;
        }
    }

    private void RemoveCreatureRoleBehavior(Node3D creature)
    {
        _creatureRoleBehaviors.Remove(creature);
        _creatureAmbientOrigins.Remove(creature);
        _creatureAmbientRotations.Remove(creature);
        _creatureAmbientPhases.Remove(creature);
    }

    private void ConfigureCreatureEvents(Node3D creature, BlueprintDefinition blueprint)
    {
        var profile = blueprint.CreatureEvents;
        if (profile is null) return;
        var diagnostics = CreatureEventValidator.Validate(profile, $"blueprints/{blueprint.Id}/creatureEvents");
        if (diagnostics.Count > 0)
            throw new InvalidOperationException($"Creature '{blueprint.Slug}' events are invalid: " +
                string.Join("; ", diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        _creatureEventProfiles[creature] = profile;
        _creatureEventStates[creature] = CreatureEventRuntimeState.Empty;
        GD.Print($"OPENRPG_FORGE_CREATURE_EVENTS_READY blueprint={blueprint.Slug} " +
            $"bindings={CreatureEventValidator.EnumerateBindings(profile).Count - (profile.DamageDeath is null ? 0 : 3)} " +
            $"heartbeat_ms={profile.Heartbeat?.IntervalMilliseconds ?? 0} " +
            $"budget_ms={profile.Heartbeat?.BudgetMilliseconds ?? 0} custom={profile.CustomEvents.Count}");
    }

    private void AdvanceCreatureEvents(double delta)
    {
        var milliseconds = Math.Max(0, (long)Math.Round(delta * 1_000, MidpointRounding.AwayFromZero));
        foreach (var creature in _creatureEventProfiles.Keys.ToArray())
        {
            if (!GodotObject.IsInstanceValid(creature))
            {
                RemoveCreatureEvents(creature);
                continue;
            }
            var profile = _creatureEventProfiles[creature];
            var insideInterest = _player is null || creature.GlobalPosition.DistanceTo(_player.GlobalPosition) <= 40;
            var result = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile, _creatureEventStates[creature],
                milliseconds, insideInterest, creature.Name, profile.RequiredAtRuntime);
            _creatureEventStates[creature] = result.State;
            foreach (var diagnostic in result.Diagnostics)
                GD.Print($"OPENRPG_FORGE_CREATURE_EVENT_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
            foreach (var dispatch in result.Dispatches)
            {
                _creatureEventHeartbeatDispatchCount++;
                DispatchCreatureEvent(dispatch);
            }
        }
    }

    private void DispatchCreatureEvent(CreatureEventDispatch dispatch)
    {
        GD.Print($"OPENRPG_FORGE_CREATURE_EVENT_DISPATCH event={dispatch.EventName} handler={dispatch.Handler} " +
            $"authority={dispatch.Authority} budget_ms={dispatch.BudgetMilliseconds} " +
            $"payload={string.Join(',', dispatch.Payload.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"))}");
    }

    private void RemoveCreatureEvents(Node3D creature)
    {
        _creatureEventProfiles.Remove(creature);
        _creatureEventStates.Remove(creature);
    }

    private void ConfigureFactions(FactionCatalog catalog)
    {
        var foundation = FactionRuntimeResolver.ResolveFoundation(catalog);
        if (foundation.Diagnostics.Count > 0)
            throw new InvalidOperationException("Faction foundation is invalid: " +
                string.Join("; ", foundation.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        if (GodotObject.IsInstanceValid(_factionFoundationRoot))
            _factionFoundationRoot!.QueueFree();
        _factionFoundationRoot = null;
        if (foundation.Catalog is not null)
        {
            _factionFoundationRoot = new Node { Name = "FactionFoundationRuntime" };
            AddChild(_factionFoundationRoot);
            foreach (var faction in catalog.Factions.OrderBy(item => item.Slug, StringComparer.Ordinal))
            {
                var node = new Node { Name = $"Faction_{faction.Slug}" };
                node.SetMeta("id", faction.Id.ToString("D"));
                node.SetMeta("schema_version", faction.SchemaVersion);
                node.SetMeta("archived", faction.Archived);
                node.SetMeta("semantic_tags", string.Join(',',
                    FactionRuntimeResolver.EffectiveDefinition(catalog, faction.Id).Tags));
                _factionFoundationRoot.AddChild(node);
            }
            foreach (var relationship in catalog.Relationships.OrderBy(item => item.SourceId)
                         .ThenBy(item => item.TargetId))
            {
                var node = new Node { Name = $"Relationship_{relationship.SourceId:N}_{relationship.TargetId:N}" };
                node.SetMeta("score", relationship.Score);
                node.SetMeta("disposition", FactionValidator.ResolveBand(
                    catalog, relationship.SourceId, relationship.TargetId));
                node.SetMeta("explanation", relationship.Explanation);
                _factionFoundationRoot.AddChild(node);
            }
            foreach (var overlay in catalog.EffectivePersonalReputation.OrderBy(item => item.Id))
            {
                var node = new Node { Name = $"PersonalReputation_{overlay.Id:N}" };
                node.SetMeta("subject_kind", overlay.SubjectKind.ToString());
                node.SetMeta("subject", overlay.SubjectId);
                node.SetMeta("delta", overlay.Delta);
                node.SetMeta("cause", overlay.Cause);
                _factionFoundationRoot.AddChild(node);
            }
        }

        if (GodotObject.IsInstanceValid(_factionReputationRoot))
            _factionReputationRoot!.QueueFree();
        _factionReputationRoot = null;
        var reputation = FactionRuntimeResolver.ResolveReputation(catalog);
        if (reputation.Diagnostics.Count > 0)
            throw new InvalidOperationException("Faction reputation is invalid: " +
                string.Join("; ", reputation.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        _factionReputationProfile = reputation.Profile;
        _factionReputationState = FactionReputationService.InitialState(catalog);
        if (reputation.Profile is not null)
        {
            _factionReputationRoot = new Node { Name = "FactionReputationRuntime" };
            AddChild(_factionReputationRoot);
            var reaction = new Node { Name = $"ReactionPolicy_{reputation.Profile.Reaction.Id:N}" };
            reaction.SetMeta("default_scope", reputation.Profile.Reaction.DefaultScope.ToString());
            reaction.SetMeta("default_scope_id", reputation.Profile.Reaction.DefaultScopeId);
            reaction.SetMeta("gm_override", reputation.Profile.Reaction.AllowGameMasterOverride);
            _factionReputationRoot.AddChild(reaction);
            foreach (var transaction in reputation.Profile.Transactions.OrderBy(item => item.OccurredAt)
                         .ThenBy(item => item.Id))
            {
                var node = new Node { Name = $"GlobalReputation_{transaction.Id:N}" };
                node.SetMeta("scope", transaction.Scope.ToString());
                node.SetMeta("scope_id", transaction.ScopeId);
                node.SetMeta("delta", transaction.Delta);
                node.SetMeta("cause", transaction.Cause);
                _factionReputationRoot.AddChild(node);
            }
            foreach (var snapshot in reputation.Profile.HeatmapHistory.OrderBy(item => item.CapturedAt)
                         .ThenBy(item => item.Id))
            {
                var node = new Node { Name = $"Heatmap_{snapshot.Id:N}" };
                node.SetMeta("label", snapshot.Label);
                node.SetMeta("relationships", snapshot.Relationships.Count);
                node.SetMeta("captured_at", snapshot.CapturedAt.ToString("O"));
                _factionReputationRoot.AddChild(node);
            }
        }

        var resolution = FactionRuntimeResolver.Resolve(catalog);
        if (resolution.Diagnostics.Count > 0)
            throw new InvalidOperationException("Advanced faction profile is invalid: " +
                string.Join("; ", resolution.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        _factionCatalog = catalog;
        _factionRuntimeProfile = resolution.Profile;
        if (resolution.Profile is null)
        {
            GD.Print($"OPENRPG_FORGE_FACTIONS_OPTIONAL_FALLBACK definitions={catalog.Factions.Count}");
            return;
        }

        var watch = catalog.Factions.FirstOrDefault(item =>
            string.Equals(item.Slug, "ash-watch", StringComparison.Ordinal));
        _factionInheritedTagCount = watch is null ? 0 :
            FactionRuntimeResolver.EffectiveDefinition(catalog, watch.Id).Tags.Count;
        _factionMembershipCount = FactionRuntimeResolver.Memberships(catalog, "hero",
            DateTimeOffset.Parse("2026-08-05T12:00:00Z")).Count;
        GD.Print($"OPENRPG_FORGE_FACTIONS_READY definitions={catalog.Factions.Count} " +
            $"inheritance={resolution.Profile.Inheritance.Count} witness={resolution.Profile.WitnessPolicies.Count} " +
            $"memberships={resolution.Profile.Memberships.Count} dynamic={resolution.Profile.DynamicFactions.Count} " +
            $"authority=server inherited_tags={_factionInheritedTagCount}");
    }

    private void ReportFactionWitness(string incidentTag)
    {
        if (_factionRuntimeProfile is null || _factionWitnessReportCount > 0) return;
        var watch = _factionCatalog.Factions.FirstOrDefault(item =>
            string.Equals(item.Slug, "ash-watch", StringComparison.Ordinal));
        if (watch is null) return;
        var at = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
        var observation = new FactionWitnessObservation(
            Guid.Parse("b2050000-0000-4000-8000-000000000020"), incidentTag, "hero", "ash-warden-boss",
            watch.Id, "crime.assault", .9, .8, 12, at, true);
        var result = FactionRuntimeResolver.PropagateWitnesses(_factionCatalog, [observation], at);
        if (result.Diagnostics.Count > 0)
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(item => item.Code)));
        foreach (var report in result.Reports)
        {
            _factionWitnessReportCount++;
            GD.Print($"OPENRPG_FORGE_FACTION_WITNESS_REPORT incident={report.IncidentId} " +
                $"witness={report.WitnessActorId} faction={report.ReportingFactionId:D} " +
                $"evidence={report.Evidence:0.00} reputation={report.ReputationDelta:0.00}");
        }
    }

    private void TransitionFactionDiplomacy()
    {
        if (_factionRuntimeProfile is null || _factionDiplomacyTransitionCount > 0) return;
        var dynamic = _factionRuntimeProfile.DynamicFactions.FirstOrDefault(item => item.Active);
        var target = _factionCatalog.Factions.FirstOrDefault(item =>
            string.Equals(item.Slug, "surface-alliance", StringComparison.Ordinal));
        if (dynamic is null || target is null) return;
        var at = DateTimeOffset.Parse("2026-08-05T12:05:00Z");
        var diplomacy = new FactionDiplomacyState(
            Guid.Parse("b2050000-0000-4000-8000-000000000021"), target.Id,
            FactionDiplomacyStatus.Truce, at, at.AddHours(12), "ash-warden-defeated");
        var result = FactionRuntimeResolver.TransitionDiplomacy(_factionRuntimeProfile, dynamic.Id, diplomacy,
            isServer: true);
        if (result.Diagnostics.Count > 0)
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(item => item.Code)));
        _factionRuntimeProfile = result.Profile;
        _factionCatalog = _factionCatalog with { Advanced = result.Profile };
        _factionDiplomacyTransitionCount += result.Events.Count;
        foreach (var item in result.Events)
            GD.Print($"OPENRPG_FORGE_FACTION_DIPLOMACY event={item.Code} faction={item.FactionId:D} " +
                $"target={item.TargetFactionId:D} revision={item.Revision}");
    }

    private static string? GetOverride(EntityInstance entity, string key) =>
        entity.Overrides.Properties.SingleOrDefault(property =>
            string.Equals(property.Key, key, StringComparison.Ordinal))?.Value;

    private static string RequiredAnimationOverride(EntityInstance entity, string key) =>
        !string.IsNullOrWhiteSpace(GetOverride(entity, key))
            ? GetOverride(entity, key)!
            : throw new InvalidDataException(
                $"Entity '{entity.Id}' uses authored-clips but does not define '{key}'.");


}
