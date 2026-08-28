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
    private bool RunAssistantFoundationSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistantProfileHistory is null || _assistantWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_FAILED missing_workspace");
            return false;
        }

        var originalProfile = _assistantProfileHistory.State.Profile;
        var profilePath = Path.Combine(_projectSession.RootDirectory, "assistant", "profile.json");
        var originalBytes = File.Exists(profilePath) ? File.ReadAllBytes(profilePath) : null;
        try
        {
            if (!_assistantWorkspace.ConfigureForSmokeTest("1.1.0") ||
                !_assistantWorkspace.PreviewForSmokeTest() ||
                string.IsNullOrWhiteSpace(_assistantWorkspace.LastPromptHash) ||
                _assistantWorkspace.LastPromptHash.Length != 64 ||
                !_assistantWorkspace.LastPreviewWasOffline ||
                _assistantWorkspace.LastProviderCost != 0 ||
                !_assistantWorkspace.ApplyForSmokeTest() ||
                _assistantProfileHistory.State.Profile.Prompts[0].Version != "1.1.0")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_FAILED preview_apply");
                return false;
            }
            var restored = AssistantWorkspaceProfileStore.Load(_projectSession.RootDirectory);
            if (!restored.Succeeded || restored.Profile.Prompts[0].Version != "1.1.0" ||
                AgentSemanticWorkflowValidator.Validate(restored.Profile.AgentWorkflow).Count != 0 ||
                restored.Profile.AgentWorkflow.ContextResources.Count != 10 ||
                restored.Profile.AgentWorkflow.Tools.Count != 5 ||
                restored.Profile.AgentWorkflow.EffectiveGovernance.Authorization.MaximumCommands != 12 ||
                restored.Profile.AgentWorkflow.EffectiveGovernance.Authorization.MaximumDurationSeconds != 45 ||
                restored.Profile.AgentWorkflow.EffectiveGovernance.Authorization.AllowBoundedAutonomy ||
                !restored.Profile.AgentWorkflow.EffectiveGovernance.BudgetPrivacy.ProhibitSecretLogging ||
                restored.Profile.AgentWorkflow.EffectiveSafety.StructuredOutput.MaximumResponseCharacters != 131072 ||
                restored.Profile.AgentWorkflow.EffectiveSafety.PromptInjection.MaximumUntrustedCharacters != 32768 ||
                AgentResponseSafetyProfileValidator.Validate(restored.Profile.AgentWorkflow.EffectiveSafety).Count != 0 ||
                !_assistantWorkspace.LastStructuredResultSucceeded ||
                !_assistantWorkspace.LastOfflineReady ||
                _assistantWorkspace.LastInjectionSignalCount != 0 ||
                CreatorReferenceIndex.Build(_history.State.Project).References.Count == 0 ||
                !UndoAssistantWorkspaceProfile() ||
                _assistantProfileHistory.State.Profile.Prompts[0].Version != originalProfile.Prompts[0].Version ||
                !RedoAssistantWorkspaceProfile() ||
                _assistantProfileHistory.State.Profile.Prompts[0].Version != "1.1.0" ||
                !UndoAssistantWorkspaceProfile())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_FAILED persistence_history");
                return false;
            }
            var blueprintIds = _history.State.Project.Blueprints.Take(2).Select(item => item.Id).ToArray();
            var simulation = CombatSimulationProfile.CreateDefault() with
            {
                Enabled = true,
                RunsPerMatchup = 16,
                ArchetypeBlueprintIds = blueprintIds,
                Provenance = "toolset:r143",
            };
            var simulationProfile = new AssistantWorkspaceProfile(
                originalProfile.Id, originalProfile.SchemaVersion, originalProfile.ActiveProviderId,
                originalProfile.Providers, originalProfile.Prompts, originalProfile.MaximumPermission,
                originalProfile.NetworkAllowed, originalProfile.ShareProjectContent, originalProfile.Provenance,
                originalProfile.AgentWorkflow, originalProfile.TestAutomation, simulation);
            var simulationPlan = Guid.Parse("d1430000-0000-4000-8000-000000000001");
            var abilityA = new AbilityDefinition(AbilityId.From(Guid.Parse("d1430000-0000-4000-8000-000000000002")),
                "guardian-strike", "Frappe gardienne", 1, 0, AbilityTargeting.SingleEnemy,
                [new(AbilityEffectKind.Damage, 8)], new("combat.guardian-strike"));
            var abilityB = new AbilityDefinition(AbilityId.From(Guid.Parse("d1430000-0000-4000-8000-000000000003")),
                "raider-strike", "Frappe pillarde", 1, 0, AbilityTargeting.SingleEnemy,
                [new(AbilityEffectKind.Damage, 10)], new("combat.raider-strike"));
            var archetypes = new[]
            {
                new CombatSimulationArchetype(blueprintIds[0], "Gardien",
                    new CombatProfile(new(140, 30, 12, 4, "guardian", "first"), [abilityA])),
                new CombatSimulationArchetype(blueprintIds[1], "Pillard",
                    new CombatProfile(new(100, 30, 16, 2, "raider", "second"), [abilityB])),
            };
            if (!ApplyAssistantWorkspaceProfile(simulationProfile, simulationPlan, true) ||
                !ApplyAssistantWorkspaceProfile(simulationProfile, simulationPlan, false))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R143_COMBAT_SIMULATION_FAILED command");
                return false;
            }
            var simulationReport = CombatBalanceSimulationService.Run(simulation, archetypes,
                new Dictionary<string, float>
                {
                    [CombatBalanceSimulationService.PairKey(blueprintIds[0], blueprintIds[1])] = 20,
                });
            var simulationRestored = AssistantWorkspaceProfileStore.Load(_projectSession.RootDirectory);
            if (!simulationReport.Succeeded || simulationReport.Matchups.Single().Runs != 16 ||
                simulationReport.Matchups.Single().AveragePathingSeconds != 4 ||
                simulationReport.Fingerprint.Length != 64 ||
                !simulationRestored.Profile.CombatSimulation.Enabled ||
                !UndoAssistantWorkspaceProfile() || _assistantProfileHistory.State.Profile.CombatSimulation.Enabled ||
                !RedoAssistantWorkspaceProfile() || !_assistantProfileHistory.State.Profile.CombatSimulation.Enabled ||
                !UndoAssistantWorkspaceProfile())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R143_COMBAT_SIMULATION_FAILED report_persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_PASS ui=context-selection-transaction " +
                "providers=offline-openai-compatible-ollama prompts=versioned-tested " +
                "plan=dry-run-bound permission=configure secrets=environment-only " +
                "offline=deterministic cost=visible history=restored persistence=roundtrip");
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_R020_AGENT_WORKFLOW_PASS context=10-structured " +
                "graph=reverse-impact-typed tools=semantic-domain " +
                "plan=goal-commands-assumptions-objects-risks-tests " +
                "dry_run=revision-batch-diff-no-write permission=bounded " +
                "history=restored persistence=roundtrip");
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_R021_AGENT_GOVERNANCE_PASS " +
                "transaction=begin-preview-commit-rollback-atomic diff=objects-references-states-assets-generated " +
                "permission=read-propose-confirmed-autonomous-bounded audit=client-model-user-plan-commands-hashes-tests-provenance-rollback " +
                "privacy=classification-opt-in-redaction-no-secret budget=provider-token-cost " +
                "history=restored persistence=roundtrip");
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_R022_AGENT_SAFETY_PASS " +
                "output=json-schema-tool-calls-bounded-repair ambiguity=rejected " +
                "injection=data-provenance-allowlist-no-escalation-confirmation " +
                "offline=manual-local-optional-metadata-optional-build-runtime-independent " +
                "network=disabled python=allowlisted-no-exec history=restored persistence=roundtrip");
            GD.Print("OPENRPG_FORGE_TOOLSET_R143_COMBAT_SIMULATION_PASS " +
                     "profile=uuid-schema-provenance-seeds-budgets-archetypes phases=thresholds " +
                     "comparison=win-rate-duration-resource-pathing-phase-triggers deterministic=fingerprint " +
                     "ui=visible-configure-run-empty-error command=plan-dry-run-apply permission=bounded " +
                     "history=undo-redo persistence=roundtrip mcp=plan-transaction-permission-read-only");
            return true;
        }
        finally
        {
            _assistantProfileHistory = new AssistantWorkspaceProfileCommandHistory(originalProfile);
            if (originalBytes is null)
            {
                if (File.Exists(profilePath)) File.Delete(profilePath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                File.WriteAllBytes(profilePath, originalBytes);
            }
        }
    }

    private bool RunLoreIndexSmokeTest()
    {
        if (_projectSession is null || _loreIndexHistory is null || _loreIndexWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_FAILED missing_lore_workspace");
            return false;
        }

        var originalIndex = _loreIndexHistory.State.Index;
        var indexPath = Path.Combine(_projectSession.RootDirectory, "assistant", "lore-index.json");
        var originalBytes = File.Exists(indexPath) ? File.ReadAllBytes(indexPath) : null;
        try
        {
            if (!_loreIndexWindow.ConfigureForSmokeTest() ||
                !_loreIndexWindow.PreviewForSmokeTest() ||
                _loreIndexWindow.LastContextHash.Length != 64 ||
                _loreIndexWindow.LastContextSourceCount != 1 ||
                !_loreIndexWindow.ApplyForSmokeTest() ||
                _loreIndexHistory.State.Index.Entries.Count != originalIndex.Entries.Count + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_FAILED lore_preview_apply");
                return false;
            }
            var restored = LoreIndexStore.Load(_projectSession.RootDirectory);
            if (!restored.Succeeded || restored.Index.Entries.Count != originalIndex.Entries.Count + 1 ||
                !UndoLoreIndex() ||
                _loreIndexHistory.State.Index.Entries.Count != originalIndex.Entries.Count ||
                !RedoLoreIndex() ||
                _loreIndexHistory.State.Index.Entries.Count != originalIndex.Entries.Count + 1 ||
                !UndoLoreIndex())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_FAILED lore_persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_PASS " +
                "transports=openai-compatible-ollama network=explicit-loopback timeout=bounded cancellation=propagated " +
                "lore=world-faction-character-timeline-terminology sources=resolved retrieval=hashed-bounded " +
                "plan=dry-run-transaction permission=configure history=restored persistence=roundtrip");
            return true;
        }
        finally
        {
            _loreIndexHistory = new LoreIndexCommandHistory(originalIndex);
            if (originalBytes is null)
            {
                if (File.Exists(indexPath)) File.Delete(indexPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
                File.WriteAllBytes(indexPath, originalBytes);
            }
        }
    }

    private bool RunAssistedAreaPlanSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedAreaPlanHistory is null ||
            _assistedAreaPlanWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_FAILED missing_workspace");
            return false;
        }
        var originalCatalog = _assistedAreaPlanHistory.State.Catalog;
        var originalEntityCount = _history.State.Project.Areas.Sum(item => item.Entities.Count);
        var path = Path.Combine(_projectSession.RootDirectory, AssistedAreaPlanStore.RelativePath);
        var originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            if (!_assistedAreaPlanWindow.ConfigureForSmokeTest() ||
                !_assistedAreaPlanWindow.PreviewForSmokeTest() ||
                _assistedAreaPlanWindow.LastGeometryApplied ||
                _assistedAreaPlanWindow.LastEstimatedCostUsd != 0 ||
                !_assistedAreaPlanWindow.ApplyForSmokeTest() ||
                _assistedAreaPlanHistory.State.Catalog.Plans.Count != originalCatalog.Plans.Count + 1 ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_FAILED preview_apply_geometry_guard");
                return false;
            }
            var restored = AssistedAreaPlanStore.Load(_projectSession.RootDirectory);
            if (!restored.Succeeded || restored.Catalog.Plans.Count != originalCatalog.Plans.Count + 1 ||
                !UndoAssistedAreaPlans() || _assistedAreaPlanHistory.State.Catalog.Plans.Count != originalCatalog.Plans.Count ||
                !RedoAssistedAreaPlans() || _assistedAreaPlanHistory.State.Catalog.Plans.Count != originalCatalog.Plans.Count + 1 ||
                !UndoAssistedAreaPlans())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_PASS archetypes=crypt-village topology=connected-deterministic " +
                "kits=resolved constraints=bounded objectives=explicit lore=sourced provider=fake-offline cost=visible " +
                "geometry=not-applied plan=dry-run-transaction permission=configure-approve history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _assistedAreaPlanHistory = new AssistedAreaPlanCommandHistory(originalCatalog);
            if (originalBytes is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, originalBytes);
            }
        }
    }

    private bool RunAssistedAreaBuildSmokeTest()
    {
        if (_projectSession is null || _history is null || _projectPreview is null ||
            _assistedAreaPlanHistory is null || _assistedAreaPlanWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalCatalog = _assistedAreaPlanHistory.State.Catalog;
        var originalEntityCount = originalState.Project.Areas.Sum(item => item.Entities.Count);
        var catalogPath = Path.Combine(_projectSession.RootDirectory, AssistedAreaPlanStore.RelativePath);
        var originalCatalogBytes = File.Exists(catalogPath) ? File.ReadAllBytes(catalogPath) : null;
        var areaDocumentsDirectory = Path.Combine(_projectSession.RootDirectory, "areas");
        var originalAreaDocuments = Directory.Exists(areaDocumentsDirectory)
            ? Directory.EnumerateFiles(areaDocumentsDirectory, "*.json").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!_assistedAreaPlanWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedAreaPlanWindow.PreviewForSmokeTest() ||
                !_assistedAreaPlanWindow.ApplyForSmokeTest() ||
                !_assistedAreaPlanWindow.SelectApprovedPlanForSmokeTest() ||
                !_assistedAreaPlanWindow.PreviewBuildForSmokeTest() ||
                _assistedAreaPlanWindow.LastBuildEntityCount <= 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED plan_preview");
                return false;
            }
            var placementCount = _assistedAreaPlanWindow.LastBuildEntityCount;
            if (!_assistedAreaPlanWindow.ApplyBuildForSmokeTest() ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount + placementCount ||
                _projectPreview.EntityCount != GetActiveArea().Entities.Count)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED atomic_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null ||
                reopened.Session.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount + placementCount)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED persistence");
                return false;
            }
            var undone = _history.Undo();
            if (!undone.Succeeded ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount ||
                !_history.Redo().Succeeded ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount + placementCount ||
                !_history.Undo().Succeeded ||
                !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED history");
                return false;
            }
            RebuildPreview();
            RefreshEditorControls();
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_PASS placements=batch-manual-commands seed=preserved " +
                "geometry=validated navigation=no-regression lighting=validated approval=required " +
                "plan=dry-run-transaction permission=apply-approved atomic=true history=restored " +
                "persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            var restored = _projectSession.Save(originalState.Project);
            if (restored.Succeeded && Directory.Exists(areaDocumentsDirectory))
            {
                var referencedAreaDocuments = originalState.Project.Areas
                    .Select(area =>
                    {
                        var json = AreaJsonSerializer.Serialize(area);
                        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
                            .ToLowerInvariant()[..16];
                        return Path.GetFullPath(Path.Combine(
                            areaDocumentsDirectory,
                            $"{area.Id}-{hash}.json"));
                    })
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var generatedPath in Directory.EnumerateFiles(areaDocumentsDirectory, "*.json")
                             .Where(item => !originalAreaDocuments.Contains(item) &&
                                            !referencedAreaDocuments.Contains(Path.GetFullPath(item))))
                    File.Delete(generatedPath);
            }
            _assistedAreaPlanHistory = new AssistedAreaPlanCommandHistory(originalCatalog);
            if (originalCatalogBytes is null)
            {
                if (File.Exists(catalogPath)) File.Delete(catalogPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
                File.WriteAllBytes(catalogPath, originalCatalogBytes);
            }
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedDialogueSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedDialogueWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalDialogueCount = originalState.Project.Dialogues.Count;
        try
        {
            if (!_assistedDialogueWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedDialogueWindow.PreviewForSmokeTest() ||
                _assistedDialogueWindow.LastNodeCount != 4 ||
                _assistedDialogueWindow.LastLintCount != 0 ||
                _assistedDialogueWindow.LastCitationCount < 1 ||
                _assistedDialogueWindow.LastEstimatedCostUsd != 0 ||
                !_assistedDialogueWindow.ApplyForSmokeTest() ||
                _history.State.Project.Dialogues.Count != originalDialogueCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_FAILED preview_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null || reopened.Session.Project.Dialogues.Count != originalDialogueCount + 1 ||
                !_history.Undo().Succeeded || _history.State.Project.Dialogues.Count != originalDialogueCount ||
                !_history.Redo().Succeeded || _history.State.Project.Dialogues.Count != originalDialogueCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_PASS graph=canonical-minimap nodes=4 " +
                "conditions=typed actions=typed style=cited localization=placeholders lint=clean " +
                "simulation=entry-rendered provider=fake-offline cost=visible plan=dry-run-transaction " +
                "permission=apply-approved history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedQuestSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedQuestWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12F_QUEST_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalQuestCount = originalState.Project.Quests.Count;
        try
        {
            if (!_assistedQuestWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedQuestWindow.PreviewForSmokeTest() ||
                _assistedQuestWindow.LastStateCount != 4 ||
                _assistedQuestWindow.LastObjectiveCount < 1 ||
                _assistedQuestWindow.LastScenarioCount != 5 ||
                _assistedQuestWindow.LastLintCount != 0 ||
                _assistedQuestWindow.LastEstimatedCostUsd != 0 ||
                !_assistedQuestWindow.ApplyForSmokeTest() ||
                _history.State.Project.Quests.Count != originalQuestCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12F_QUEST_FAILED preview_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null || reopened.Session.Project.Quests.Count != originalQuestCount + 1 ||
                !_history.Undo().Succeeded || _history.State.Project.Quests.Count != originalQuestCount ||
                !_history.Redo().Succeeded || _history.State.Project.Quests.Count != originalQuestCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12F_QUEST_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12F_QUEST_PASS participants=resolved states=4 objectives=bounded " +
                "transitions=3 rewards=progression failure=explicit tests=5-scenarios style=cited lint=clean " +
                "provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved " +
                "history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedForgeScriptSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedForgeScriptWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_FAILED missing_workspace");
            return false;
        }
        var relativePath = _assistedForgeScriptWindow.SmokeRelativePath;
        var path = AssistedForgeScriptCreationService.ResolvePath(_projectSession.RootDirectory, relativePath);
        var original = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            if (!_assistedForgeScriptWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedForgeScriptWindow.PreviewForSmokeTest() ||
                !_assistedForgeScriptWindow.LastCompiled ||
                _assistedForgeScriptWindow.LastTestCount != 3 ||
                _assistedForgeScriptWindow.LastErrorCount != 0 ||
                _assistedForgeScriptWindow.LastApiVersion != "1.0.0" ||
                _assistedForgeScriptWindow.LastEstimatedCostUsd != 0 ||
                !_assistedForgeScriptWindow.ApplyForSmokeTest() ||
                !File.Exists(path) ||
                !new NativeForgeScriptRuntime().Compile(File.ReadAllText(path)).Success)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_FAILED preview_compile_test_apply");
                return false;
            }
            if (!UndoAssistedForgeScript() || File.Exists(path) != (original is not null) ||
                !RedoAssistedForgeScript() || !File.Exists(path) ||
                !UndoAssistedForgeScript() || File.Exists(path) != (original is not null))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_FAILED history_roundtrip");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_PASS api=1.0.0 vm=2.38.0 source=generated-editable " +
                "binding=typed compile=native diagnostics=structured tests=compile-api-sandbox path=confined " +
                "provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved " +
                "history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            ApplyAssistedForgeScriptHandler.Restore(
                _projectSession.RootDirectory,
                new AssistedForgeScriptFilePatch(relativePath, original, original ?? string.Empty),
                useAfter: false);
            _assistedForgeScriptHistory.Reset();
            _buildPipeline?.Refresh();
        }
    }

    private bool RunAssistedBossSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedBossWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12H_BOSS_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalBlueprintCount = originalState.Project.Blueprints.Count;
        var originalDialogueCount = originalState.Project.Dialogues.Count;
        var originalDocumentCount = originalState.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count;
        try
        {
            if (!_assistedBossWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedBossWindow.PreviewForSmokeTest() ||
                _assistedBossWindow.LastAbilityCount < 2 ||
                _assistedBossWindow.LastPhaseCount is < 2 or > 3 ||
                _assistedBossWindow.LastBalanceRuns != 32 ||
                !_assistedBossWindow.LastBalancePassed ||
                _assistedBossWindow.LastDiagnosticCount != 0 ||
                _assistedBossWindow.LastEstimatedCostUsd != 0 ||
                !_assistedBossWindow.ApplyForSmokeTest() ||
                _history.State.Project.Blueprints.Count != originalBlueprintCount + 1 ||
                _history.State.Project.Dialogues.Count != originalDialogueCount + 1 ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12H_BOSS_FAILED preview_balance_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null ||
                reopened.Session.Project.Blueprints.Count != originalBlueprintCount + 1 ||
                reopened.Session.Project.Dialogues.Count != originalDialogueCount + 1 ||
                reopened.Session.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded ||
                _history.State.Project.Blueprints.Count != originalBlueprintCount ||
                _history.State.Project.Dialogues.Count != originalDialogueCount ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount ||
                !_history.Redo().Succeeded ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12H_BOSS_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12H_BOSS_PASS creature=canonical encounter=bounded abilities=typed " +
                "phases=scripted dialogue=typed rewards=loot balance=32-runs dependencies=resolved " +
                "provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved " +
                "batch=atomic history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedAbilitySmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedAbilityWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12I_ABILITY_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalAbilityCount = originalState.Project.Blueprints
            .Where(item => item.Combat is not null)
            .Sum(item => item.Combat!.Abilities.Count);
        var originalDocumentCount = originalState.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count;
        try
        {
            if (!_assistedAbilityWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedAbilityWindow.PreviewForSmokeTest() ||
                _assistedAbilityWindow.LastTestCount != 5 ||
                _assistedAbilityWindow.LastPassedTestCount != 5 ||
                !_assistedAbilityWindow.LastBalancePassed ||
                _assistedAbilityWindow.LastDiagnosticCount != 0 ||
                _assistedAbilityWindow.LastEstimatedCostUsd != 0 ||
                !_assistedAbilityWindow.ApplyForSmokeTest() ||
                _history.State.Project.Blueprints.Where(item => item.Combat is not null)
                    .Sum(item => item.Combat!.Abilities.Count) != originalAbilityCount + 1 ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12I_ABILITY_FAILED preview_tests_balance_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null ||
                reopened.Session.Project.Blueprints.Where(item => item.Combat is not null)
                    .Sum(item => item.Combat!.Abilities.Count) != originalAbilityCount + 1 ||
                reopened.Session.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded ||
                _history.State.Project.Blueprints.Where(item => item.Combat is not null)
                    .Sum(item => item.Combat!.Abilities.Count) != originalAbilityCount ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount ||
                !_history.Redo().Succeeded ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12I_ABILITY_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12I_ABILITY_PASS targeting=typed cost=bounded effects=canonical " +
                "vfx=resolved audio=resolved ai=tagged script=resolved item=optional tests=5-combat " +
                "balance=visible provider=fake-offline costusd=visible plan=dry-run-transaction " +
                "permission=apply-approved batch=atomic history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedBlenderCreationSmokeTest()
    {
        if (_projectSession is null || _assetExternalEditHistory is null ||
            _assistedBlenderCreationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED missing_workspace");
            return false;
        }
        var originalState = _assetExternalEditHistory.State;
        string? recipePath = null;
        string? requestPath = null;
        string? externalPath = null;
        string? beforeRecipe = null;
        string? beforeRequest = null;
        string? beforeExternal = null;
        static string? ReadOptional(string? path) => path is not null && File.Exists(path)
            ? File.ReadAllText(path, Encoding.UTF8)
            : null;
        static void RestoreOptional(string? path, string? content)
        {
            if (path is null) return;
            if (content is null)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
        try
        {
            if (!_assistedBlenderCreationWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedBlenderCreationWindow.PreviewForSmokeTest() ||
                _assistedBlenderCreationWindow.LastTestCount != 5 ||
                _assistedBlenderCreationWindow.LastPassedTestCount != 5 ||
                _assistedBlenderCreationWindow.LastDiagnosticCount != 0 ||
                _assistedBlenderCreationWindow.LastEstimatedCostUsd != 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED preview_tests_cost");
                return false;
            }
            recipePath = AssistedBlenderCreationService.ResolvePath(
                _projectSession.RootDirectory, _assistedBlenderCreationWindow.LastRecipeRelativePath);
            requestPath = Path.Combine(_projectSession.RootDirectory,
                _assistedBlenderCreationWindow.LastWorkerRequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            externalPath = Path.Combine(_projectSession.RootDirectory,
                AssetExternalEditStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            beforeRecipe = ReadOptional(recipePath);
            beforeRequest = ReadOptional(requestPath);
            beforeExternal = ReadOptional(externalPath);
            if (!_assistedBlenderCreationWindow.ApplyForSmokeTest() ||
                !File.Exists(recipePath) || !File.Exists(requestPath) ||
                !File.ReadAllText(requestPath, Encoding.UTF8).Contains("\"create_primitives\"", StringComparison.Ordinal) ||
                AssetExternalEditStore.Load(_projectSession.RootDirectory).Configurations
                    .Count(item => item.Operations.Contains(AssetExternalOperation.CreatePrimitives)) != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED apply_persistence");
                return false;
            }
            var appliedRecipe = File.ReadAllText(recipePath, Encoding.UTF8);
            var appliedRequest = File.ReadAllText(requestPath, Encoding.UTF8);
            if (!UndoAssistedBlenderCreation() || ReadOptional(recipePath) != beforeRecipe ||
                ReadOptional(requestPath) != beforeRequest ||
                !RedoAssistedBlenderCreation() || ReadOptional(recipePath) != appliedRecipe ||
                ReadOptional(requestPath) != appliedRequest ||
                !UndoAssistedBlenderCreation() || ReadOptional(recipePath) != beforeRecipe ||
                ReadOptional(requestPath) != beforeRequest)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED history_roundtrip");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12J_BLENDER_PASS recipe=canonical primitives=typed transforms=bounded " +
                "material=pbr uv=typed lod=generated collision=generated validation=five-tests glb=export-request " +
                "provider=fake-offline cost=visible python=forbidden plan=dry-run-transaction " +
                "permission=apply-approved history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            RestoreOptional(recipePath, beforeRecipe);
            RestoreOptional(requestPath, beforeRequest);
            RestoreOptional(externalPath, beforeExternal);
            foreach (var directory in new[]
            {
                recipePath is null ? null : Path.GetDirectoryName(recipePath),
                requestPath is null ? null : Path.GetDirectoryName(requestPath),
                recipePath is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(recipePath)),
                externalPath is null ? null : Path.GetDirectoryName(externalPath),
            }.Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            _assetExternalEditHistory = new AssetExternalEditCommandHistory(originalState);
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(originalState);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(originalState);
            _assetBrowser?.Refresh();
        }
    }

    private bool RunAssistedModel3DGenerationSmokeTest()
    {
        if (_projectSession is null || _assetExternalEditHistory is null ||
            _assistedModel3DGenerationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED missing_workspace");
            return false;
        }
        var originalState = _assetExternalEditHistory.State;
        string? sourcePath = null;
        string? receiptPath = null;
        string? requestPath = null;
        string? externalPath = null;
        string? beforeSource = null;
        string? beforeReceipt = null;
        string? beforeRequest = null;
        string? beforeExternal = null;
        static string? ReadOptional(string? path) => path is not null && File.Exists(path)
            ? File.ReadAllText(path, Encoding.UTF8)
            : null;
        static void RestoreOptional(string? path, string? content)
        {
            if (path is null) return;
            if (content is null) { if (File.Exists(path)) File.Delete(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
        try
        {
            if (!_assistedModel3DGenerationWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedModel3DGenerationWindow.PreviewForSmokeTest() ||
                _assistedModel3DGenerationWindow.LastTestCount != 5 ||
                _assistedModel3DGenerationWindow.LastPassedTestCount != 5 ||
                _assistedModel3DGenerationWindow.LastDiagnosticCount != 0 ||
                _assistedModel3DGenerationWindow.LastEstimatedCostUsd != 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED preview_tests_cost");
                return false;
            }
            sourcePath = AssistedModel3DGenerationService.ResolvePath(_projectSession.RootDirectory,
                _assistedModel3DGenerationWindow.LastSourceRelativePath, "assets/staging/ai-3d/");
            receiptPath = AssistedModel3DGenerationService.ResolvePath(_projectSession.RootDirectory,
                _assistedModel3DGenerationWindow.LastReceiptRelativePath, "assets/staging/ai-3d/");
            requestPath = Path.Combine(_projectSession.RootDirectory,
                _assistedModel3DGenerationWindow.LastWorkerRequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            externalPath = Path.Combine(_projectSession.RootDirectory,
                AssetExternalEditStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            beforeSource = ReadOptional(sourcePath);
            beforeReceipt = ReadOptional(receiptPath);
            beforeRequest = ReadOptional(requestPath);
            beforeExternal = ReadOptional(externalPath);
            if (!_assistedModel3DGenerationWindow.ApplyForSmokeTest() || !File.Exists(sourcePath) ||
                !File.Exists(receiptPath) || !File.Exists(requestPath) ||
                !File.ReadAllText(sourcePath, Encoding.UTF8).Contains("f 4 5 8", StringComparison.Ordinal) ||
                !File.ReadAllText(receiptPath, Encoding.UTF8).Contains("\"humanAccepted\": true", StringComparison.Ordinal) ||
                !GeneratedModelRequestUsesSafeTypedOperations(requestPath))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED apply_persistence");
                return false;
            }
            var appliedSource = ReadOptional(sourcePath);
            var appliedReceipt = ReadOptional(receiptPath);
            var appliedRequest = ReadOptional(requestPath);
            if (!UndoAssistedModel3DGeneration() || ReadOptional(sourcePath) != beforeSource ||
                ReadOptional(receiptPath) != beforeReceipt || ReadOptional(requestPath) != beforeRequest ||
                !RedoAssistedModel3DGeneration() || ReadOptional(sourcePath) != appliedSource ||
                ReadOptional(receiptPath) != appliedReceipt || ReadOptional(requestPath) != appliedRequest ||
                !UndoAssistedModel3DGeneration() || ReadOptional(sourcePath) != beforeSource ||
                ReadOptional(receiptPath) != beforeReceipt || ReadOptional(requestPath) != beforeRequest)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED history_roundtrip");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_PASS provider=interchangeable offline=deterministic " +
                "trellis=loopback triposr=loopback input=text-image license=cc0 staging=hashed topology=bounded " +
                "provenance=receipt acceptance=human blender=typed tests=five plan=dry-run-transaction " +
                "permission=apply-approved history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            RestoreOptional(sourcePath, beforeSource);
            RestoreOptional(receiptPath, beforeReceipt);
            RestoreOptional(requestPath, beforeRequest);
            RestoreOptional(externalPath, beforeExternal);
            foreach (var directory in new[]
            {
                sourcePath is null ? null : Path.GetDirectoryName(sourcePath),
                requestPath is null ? null : Path.GetDirectoryName(requestPath),
                sourcePath is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(sourcePath)),
                sourcePath is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(sourcePath))),
                externalPath is null ? null : Path.GetDirectoryName(externalPath),
            }.Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            _assetExternalEditHistory = new AssetExternalEditCommandHistory(originalState);
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(originalState);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(originalState);
            _assetBrowser?.Refresh();
        }
    }

    private static bool GeneratedModelRequestUsesSafeTypedOperations(string path)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var root = document.RootElement;
            var operations = root.GetProperty("operations").EnumerateArray()
                .Select(item => item.GetString()).ToArray();
            var policy = root.GetProperty("securityPolicy");
            return !operations.Contains("create_primitives", StringComparer.Ordinal) &&
                   policy.GetProperty("denyFreeFormPython").GetBoolean() &&
                   policy.GetProperty("denyShell").GetBoolean() &&
                   policy.GetProperty("denyEval").GetBoolean() &&
                   !policy.GetProperty("networkAllowed").GetBoolean();
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private bool RunExternalWorkerSecuritySmokeTest()
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null ||
            _externalWorkerSecurityWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_FAILED missing_workspace");
            return false;
        }
        var originalPolicy = _externalWorkerSecurityHistory.State.Policy;
        var path = Path.Combine(_projectSession.RootDirectory,
            ExternalWorkerSecurityPolicyStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            if (!_externalWorkerSecurityWindow.ConfigureForSmokeTest() ||
                !_externalWorkerSecurityWindow.PreviewForSmokeTest() ||
                _externalWorkerSecurityWindow.LastTransactionId == Guid.Empty ||
                _externalWorkerSecurityWindow.LastPlanId == Guid.Empty ||
                _externalWorkerSecurityWindow.LastEstimatedCostUsd != 0 ||
                !_externalWorkerSecurityWindow.LastSemanticDiff.Contains("execution arbitraire=interdite", StringComparison.Ordinal) ||
                !_externalWorkerSecurityWindow.ApplyForSmokeTest() ||
                _externalWorkerSecurityHistory.State.Policy.MaximumJobSeconds != 180)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_FAILED preview_apply");
                return false;
            }

            var loaded = ExternalWorkerSecurityPolicyStore.Load(_projectSession.RootDirectory);
            if (!loaded.Succeeded || loaded.Policy.MaximumJobSeconds != 180 ||
                !loaded.Policy.DenyFreeFormPython || !loaded.Policy.DenyShell || !loaded.Policy.DenyEval ||
                loaded.Policy.NetworkAllowed || !loaded.Policy.RequireContainedPaths ||
                !File.ReadAllText(path, Encoding.UTF8).Contains("\"denyFreeFormPython\": true", StringComparison.Ordinal) ||
                !UndoExternalWorkerSecurityPolicy() ||
                _externalWorkerSecurityHistory.State.Policy.MaximumJobSeconds != originalPolicy.MaximumJobSeconds ||
                !RedoExternalWorkerSecurityPolicy() ||
                _externalWorkerSecurityHistory.State.Policy.MaximumJobSeconds != 180 ||
                !UndoExternalWorkerSecurityPolicy())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_FAILED persistence_history");
                return false;
            }

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_PASS policy=canonical uuid=stable schema=v1 " +
                "python=forbidden exec=forbidden eval=forbidden shell=forbidden paths=confined network=denied " +
                "operations=typed timeout=bounded output=bounded ui=visible-empty-error cost=zero " +
                "transaction=identified plan=dry-run-bound permission=configure audit=generated " +
                "history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _externalWorkerSecurityHistory = new ExternalWorkerSecurityPolicyCommandHistory(originalPolicy);
            if (originalBytes is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, originalBytes);
            }
        }
    }

    private bool RunEditorKeymapSmokeTest()
    {
        if (_projectSession is null || _editorKeymapHistory is null || _editorKeymapWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_FAILED missing_workspace");
            return false;
        }
        var originalProfile = _editorKeymapHistory.State.Profile;
        var path = Path.Combine(_projectSession.RootDirectory,
            EditorKeymapProfileStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            if (!_editorKeymapWindow.ConfigureForSmokeTest() ||
                !_editorKeymapWindow.PreviewForSmokeTest() ||
                _editorKeymapWindow.LastCommandId == Guid.Empty ||
                !_editorKeymapWindow.LastPatchSummary.Contains("searchable command bindings", StringComparison.Ordinal) ||
                !_editorKeymapWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_FAILED preview_apply");
                return false;
            }

            var current = _editorKeymapHistory.State.Profile;
            var loaded = EditorKeymapProfileStore.Load(_projectSession.RootDirectory);
            var search = current.Resolve("K", ctrl: true, alt: false, shift: true, meta: false);
            if (!loaded.Succeeded || !HasSameEditorKeymap(loaded.Profile, current) ||
                current.Id != EditorKeymapProfile.DefaultId ||
                current.SchemaVersion != EditorKeymapProfile.CurrentSchemaVersion ||
                current.Preset != EditorKeymapPreset.Custom ||
                current.Bindings.Count != EditorKeymapCatalog.Commands.Count ||
                search?.CommandId != EditorKeymapCatalog.OpenSearch ||
                EditorKeymapProfileValidator.Validate(current).Count != 0 ||
                !DispatchEditorShortcut(EditorKeymapCatalog.OpenWorld) ||
                !UndoEditorKeymap() || !HasSameEditorKeymap(_editorKeymapHistory.State.Profile, originalProfile) ||
                !RedoEditorKeymap() || !HasSameEditorKeymap(_editorKeymapHistory.State.Profile, current) ||
                !UndoEditorKeymap())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_FAILED persistence_history_input");
                return false;
            }

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_PASS profiles=standard-aurora-blender " +
                "commands=searchable bindings=14 conflicts=blocked input=executed ui=visible-empty-error " +
                "uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed " +
                "audit=generated history=restored persistence=roundtrip runtime=optional");
            return true;
        }
        finally
        {
            _editorKeymapHistory = new EditorKeymapCommandHistory(originalProfile);
            if (originalBytes is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, originalBytes);
            }
        }
    }


}
#endif
