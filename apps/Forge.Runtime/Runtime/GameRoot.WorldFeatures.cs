using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private void ConfigureWorldFeatures(ProjectDefinition project)
    {
        var catalog = project.Module.WorldFeatures;
        var configured = new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        var diagnostics = WorldFeatureCatalogValidator.Validate(configured);
        if (diagnostics.Count > 0)
        {
            throw new InvalidOperationException("WORLD_FEATURE_CATALOG_INVALID: " + string.Join("; ",
                diagnostics.Select(item => $"{item.Code} at {item.Path}: {item.Message}")));
        }

        _worldFeatureCatalog = catalog;
        _doorAccessEventRoot?.QueueFree();
        _doorAccessEventRoot = new Node { Name = "DoorAccessEventAdapters" };
        AddChild(_doorAccessEventRoot);
        foreach (var doorProfile in catalog.Doors.Where(item => item.AccessEvents is not null)
                     .OrderBy(item => item.Id))
        {
            var access = doorProfile.AccessEvents!;
            var doorNode = new Node { Name = $"Door_{doorProfile.Id:N}" };
            doorNode.SetMeta("profile_id", access.Id.ToString("D"));
            doorNode.SetMeta("blueprint_id", doorProfile.BlueprintId.ToString());
            doorNode.SetMeta("asset_id", access.AssetId);
            _doorAccessEventRoot.AddChild(doorNode);
            foreach (var part in new[]
                     {
                         ("LockAndKey", access.Lock.Id),
                         ("DoorOpenRejected", access.Rejection.Id),
                         ("DoorTrap", access.Trap.Id),
                         ("PortalUseLifecycle", access.PortalUse.Id),
                         ("DoorStateLifecycle", access.StateEvents.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                doorNode.AddChild(partNode);
            }
            if (access.LockStateEvents is { } lockEvents)
            {
                var lockEventNode = new Node { Name = "DoorLockLifecycle" };
                lockEventNode.SetMeta("profile_id", lockEvents.Id.ToString("D"));
                lockEventNode.SetMeta("locked_binding", lockEvents.LockedBinding);
                lockEventNode.SetMeta("unlocked_binding", lockEvents.UnlockedBinding);
                doorNode.AddChild(lockEventNode);
            }
        }
        _encounterCompositionRoot?.QueueFree();
        _encounterCompositionRoot = new Node { Name = "EncounterCompositionAdapters" };
        AddChild(_encounterCompositionRoot);
        foreach (var encounterProfile in catalog.Encounters.Where(item => item.Composition is not null)
                     .OrderBy(item => item.Id))
        {
            var composition = encounterProfile.Composition!;
            var encounterNode = new Node { Name = $"Encounter_{encounterProfile.Id:N}" };
            encounterNode.SetMeta("profile_id", composition.Id.ToString("D"));
            encounterNode.SetMeta("area_id", encounterProfile.AreaId.ToString());
            encounterNode.SetMeta("slug", composition.Slug);
            _encounterCompositionRoot.AddChild(encounterNode);
            foreach (var part in new[]
                     {
                         ("EncounterWizard", composition.Id),
                         ("DifficultyAndBudget", composition.Difficulty.Id),
                         ("GroupCountRange", composition.Groups[0].Id),
                         ("PartyAssessmentSelection", composition.Selection.Id),
                         ("ActivationPolicy", composition.Activation.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                encounterNode.AddChild(partNode);
            }
        }
        _encounterScenarioRoot?.QueueFree();
        _encounterScenarioRoot = new Node { Name = "EncounterScenarioAdapters" };
        AddChild(_encounterScenarioRoot);
        foreach (var encounterProfile in catalog.Encounters.Where(item => item.Scenario is not null)
                     .OrderBy(item => item.Id))
        {
            var scenario = encounterProfile.Scenario!;
            var encounterNode = new Node { Name = $"Encounter_{encounterProfile.Id:N}" };
            encounterNode.SetMeta("profile_id", scenario.Id.ToString("D"));
            encounterNode.SetMeta("distribution", scenario.Distribution.ToString());
            _encounterScenarioRoot.AddChild(encounterNode);

            var anchorsNode = new Node { Name = "ManualSpawnAnchors" };
            anchorsNode.SetMeta("count", scenario.SpawnAnchors.Count);
            anchorsNode.SetMeta("fallback", scenario.SpawnAnchors.Single(item => item.IsFallback).AnchorId);
            encounterNode.AddChild(anchorsNode);
            foreach (var anchor in scenario.SpawnAnchors.OrderBy(item => item.AnchorId, StringComparer.Ordinal))
            {
                var anchorNode = new Node { Name = $"Anchor_{anchor.Id:N}" };
                anchorNode.SetMeta("anchor_id", anchor.AnchorId);
                anchorNode.SetMeta("capacity", anchor.Capacity);
                anchorNode.SetMeta("navigable", anchor.Navigable);
                anchorsNode.AddChild(anchorNode);
            }

            var activatorNode = new Node { Name = "ActivatorRestriction" };
            activatorNode.SetMeta("profile_id", scenario.Activator.Id.ToString("D"));
            activatorNode.SetMeta("authority", scenario.Activator.RequiredAuthority.ToString());
            encounterNode.AddChild(activatorNode);

            var lifecycleNode = new Node { Name = "EncounterLifecycle" };
            lifecycleNode.SetMeta("profile_id", scenario.Lifecycle.Id.ToString("D"));
            lifecycleNode.SetMeta("entered", scenario.Lifecycle.Entered.Binding);
            lifecycleNode.SetMeta("exited", scenario.Lifecycle.Exited.Binding);
            lifecycleNode.SetMeta("exhausted", scenario.Lifecycle.Exhausted.Binding);
            encounterNode.AddChild(lifecycleNode);

            var waveGraphNode = new Node { Name = "WaveGraph" };
            waveGraphNode.SetMeta("phases", scenario.Phases.Count);
            waveGraphNode.SetMeta("waves", scenario.Phases.Sum(item => item.Waves.Count));
            encounterNode.AddChild(waveGraphNode);
            foreach (var phase in scenario.Phases.OrderBy(item => item.Order))
            {
                var phaseNode = new Node { Name = $"Phase_{phase.Order}_{phase.Id:N}" };
                phaseNode.SetMeta("display_name", phase.DisplayName);
                waveGraphNode.AddChild(phaseNode);
                foreach (var wave in phase.Waves.OrderBy(item => item.Order))
                {
                    var waveNode = new Node { Name = $"Wave_{wave.Order}_{wave.Id:N}" };
                    waveNode.SetMeta("completion", wave.Completion.ToString());
                    waveNode.SetMeta("voice", wave.VoiceCueId ?? string.Empty);
                    waveNode.SetMeta("next", wave.NextWaveId?.ToString("D") ?? string.Empty);
                    phaseNode.AddChild(waveNode);
                }
            }
        }
        _encounterStatisticsRoot?.QueueFree();
        _encounterStatisticsRoot = new Node { Name = "EncounterStatisticsAdapters" };
        AddChild(_encounterStatisticsRoot);
        foreach (var encounterProfile in catalog.Encounters.Where(item => item.Statistics is not null)
                     .OrderBy(item => item.Id))
        {
            var statistics = encounterProfile.Statistics!;
            var statisticsNode = new Node { Name = $"Encounter_{encounterProfile.Id:N}" };
            statisticsNode.SetMeta("profile_id", statistics.Id.ToString("D"));
            statisticsNode.SetMeta("runs", statistics.Runs);
            statisticsNode.SetMeta("base_seed", statistics.BaseSeed);
            statisticsNode.SetMeta("duration_estimator", statistics.DurationEstimatorId);
            statisticsNode.SetMeta("reward_estimator", statistics.RewardEstimatorId);
            statisticsNode.SetMeta("percentiles", string.Join(',', statistics.Percentiles));
            _encounterStatisticsRoot.AddChild(statisticsNode);
        }
        _companionFoundationRoot?.QueueFree();
        _companionFoundationRoot = new Node { Name = "CompanionFoundationAdapters" };
        AddChild(_companionFoundationRoot);
        foreach (var companionProfile in catalog.Companions.Where(item => item.Foundation is not null)
                     .OrderBy(item => item.Id))
        {
            var companionNode = new Node { Name = $"Companion_{companionProfile.Id:N}" };
            companionNode.SetMeta("profile_id", companionProfile.Id.ToString("D"));
            companionNode.SetMeta("blueprint_id", companionProfile.BlueprintId.ToString());
            _companionFoundationRoot.AddChild(companionNode);
            var foundation = companionProfile.Foundation!;
            foreach (var part in new[]
                     {
                         ("RecruitableCreature", foundation.Recruitable.Id),
                         ("PreRecruitmentDialogue", foundation.PreRecruitment.Id),
                         ("OwnerDialogue", foundation.OwnerDialogue.Id),
                         ("RecruitDismissTransaction", foundation.Membership.Id),
                         ("PartyPolicy", foundation.Party.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                companionNode.AddChild(partNode);
            }
        }
        _companionControlRoot?.QueueFree();
        _companionControlRoot = new Node { Name = "CompanionControlAdapters" };
        AddChild(_companionControlRoot);
        foreach (var companionProfile in catalog.Companions.Where(item => item.Control is not null)
                     .OrderBy(item => item.Id))
        {
            var companionNode = new Node { Name = $"CompanionControl_{companionProfile.Id:N}" };
            companionNode.SetMeta("profile_id", companionProfile.Id.ToString("D"));
            _companionControlRoot.AddChild(companionNode);
            var control = companionProfile.Control!;
            foreach (var part in new[]
                     {
                         ("Ownership", control.Ownership.Id),
                         ("MovementCommands", control.Movement.Id),
                         ("TacticalCommands", control.Tactical.Id),
                         ("CompanionAi", control.Ai.Id),
                         ("EquipmentControl", control.Equipment.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                companionNode.AddChild(partNode);
            }
        }
        _companionExperienceRoot?.QueueFree();
        _companionExperienceRoot = new Node { Name = "CompanionExperienceAdapters" };
        AddChild(_companionExperienceRoot);
        foreach (var companionProfile in catalog.Companions.Where(item => item.Experience is not null)
                     .OrderBy(item => item.Id))
        {
            var companionNode = new Node { Name = $"CompanionExperience_{companionProfile.Id:N}" };
            companionNode.SetMeta("profile_id", companionProfile.Id.ToString("D"));
            _companionExperienceRoot.AddChild(companionNode);
            var experience = companionProfile.Experience!;
            foreach (var part in new[]
                     {
                         ("DeathLifecycle", experience.Death.Id),
                         ("CohesionRecovery", experience.Cohesion.Id),
                         ("PartyHud", experience.Hud.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                companionNode.AddChild(partNode);
            }
        }
        if (catalog.Companions.FirstOrDefault() is { } companion)
        {
            var destination = companion.Validation?.CrossZoneNavigation.AllowedAreaIds.FirstOrDefault();
            var resolved = CompanionValidationRuntimeResolver.Resolve(companion, destination);
            var optional = CompanionValidationRuntimeResolver.Resolve(
                companion with { Validation = null, ValidationRequiredAtRuntime = false });
            var required = CompanionValidationRuntimeResolver.Resolve(
                companion with { Validation = null, ValidationRequiredAtRuntime = true });
            AssertSmoke(resolved.Available && resolved.DestinationAllowed && optional.OptionalFallback &&
                        required.Diagnostics.Any(item => item.Code == "world.companion.validation_required"),
                "Companion validation runtime policy did not resolve its authored and fallback paths.");
            GD.Print("OPENRPG_FORGE_RUNTIME_R037_COMPANION_PASS policy=canonical recruitment=dialogue-script commands=4 " +
                     "navigation=bounded optional=empty-safe required=diagnostic");
        }
    }

    private void ConfigurePostLaunch(ProjectDefinition project)
    {
        _postLaunchProfile = project.Module.RuntimePolicies.Extensions.PostLaunch;
    }


}
