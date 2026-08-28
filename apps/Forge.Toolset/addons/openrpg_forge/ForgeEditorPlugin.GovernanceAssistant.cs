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
    private void PersistEditorCameraProfile(EditorCameraProfile profile)
    {
        if (_projectSession is null)
        {
            return;
        }

        var result = EditorCameraProfileStore.Save(_projectSession.RootDirectory, profile);
        if (!result.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_CAMERA_PROFILE_SAVE_FAILED {result.Diagnostics[0].Code}");
        }
    }

    private bool ApplyAssetGovernance(AssetGovernanceRecord record, bool dryRun)
    {
        if (_projectSession is null || _assetGovernanceHistory is null) return false;
        var command = new SetAssetGovernanceCommand(CommandId.From(Guid.NewGuid()),
            _assetGovernanceHistory.State.Revision, record, dryRun);
        var result = _assetGovernanceHandler.Handle(_assetGovernanceHistory.State, command);
        if (!result.Succeeded) return false;
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = AssetGovernanceStore.Save(_projectSession.RootDirectory, result.State.Records);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_GOVERNANCE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _assetGovernanceHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSET_GOVERNANCE_SAVED asset={record.AssetId} " +
                 $"quarantine={record.Quarantine.Status} origin={record.IpBoundary.Origin}");
        return true;
    }

    private bool ApplyQualityFoundation(QualityFoundationProfile profile, bool dryRun)
    {
        if (_projectSession is null || _qualityFoundationHistory is null) return false;
        var command = new SetQualityFoundationCommand(CommandId.From(Guid.NewGuid()),
            _qualityFoundationHistory.State.Revision, profile, dryRun);
        var result = _qualityFoundationHandler.Handle(_qualityFoundationHistory.State, command);
        if (!result.Succeeded) return false;
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = QualityFoundationProfileStore.Save(_projectSession.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_QUALITY_FOUNDATION_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _qualityFoundationHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_QUALITY_FOUNDATION_SAVED revision={result.NewRevision?.Value} " +
                 $"severity={profile.Logs.MinimumSeverity} privacy={profile.Logs.PlayerPrivacy}");
        return true;
    }

    private bool UndoQualityFoundation()
    {
        if (_projectSession is null || _qualityFoundationHistory is null || !_qualityFoundationHistory.Undo()) return false;
        var saved = QualityFoundationProfileStore.Save(_projectSession.RootDirectory, _qualityFoundationHistory.State.Profile);
        if (saved.Succeeded) return true;
        _qualityFoundationHistory.Redo();
        return false;
    }

    private bool RedoQualityFoundation()
    {
        if (_projectSession is null || _qualityFoundationHistory is null || !_qualityFoundationHistory.Redo()) return false;
        var saved = QualityFoundationProfileStore.Save(_projectSession.RootDirectory, _qualityFoundationHistory.State.Profile);
        if (saved.Succeeded) return true;
        _qualityFoundationHistory.Undo();
        return false;
    }

    private bool UndoAssetGovernance()
    {
        if (_projectSession is null || _assetGovernanceHistory is null || !_assetGovernanceHistory.Undo()) return false;
        var saved = AssetGovernanceStore.Save(_projectSession.RootDirectory, _assetGovernanceHistory.State.Records);
        if (saved.Succeeded) return true;
        _assetGovernanceHistory.Redo();
        return false;
    }

    private bool RedoAssetGovernance()
    {
        if (_projectSession is null || _assetGovernanceHistory is null || !_assetGovernanceHistory.Redo()) return false;
        var saved = AssetGovernanceStore.Save(_projectSession.RootDirectory, _assetGovernanceHistory.State.Records);
        if (saved.Succeeded) return true;
        _assetGovernanceHistory.Undo();
        return false;
    }

    private bool ApplyAssetProcessingProfile(AssetProcessingProfile profile, bool dryRun)
    {
        if (_projectSession is null || _assetProcessingProfileHistory is null)
        {
            return false;
        }

        var command = new SetAssetProcessingProfileCommand(
            CommandId.From(Guid.NewGuid()),
            _assetProcessingProfileHistory.State.Revision,
            profile,
            dryRun);
        var result = _assetProcessingProfileHandler.Handle(_assetProcessingProfileHistory.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                result.Patch?.Path ?? $"assets/{profile.AssetId}/profile",
                command.CommandId.ToString())).ToArray());
            return false;
        }

        if (dryRun)
        {
            return result.Patch is not null && !result.Audit.Applied;
        }

        var saved = AssetProcessingProfileStore.Save(_projectSession.RootDirectory, result.State.Profiles);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_PROFILE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }

        _assetProcessingProfileHistory.Record(result);
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_ASSET_PROFILE_SAVED asset={profile.AssetId} kind={profile.Kind} " +
            $"revision={_assetProcessingProfileHistory.State.Revision}");
        return true;
    }

    private bool UndoAssetProcessingProfile()
    {
        if (_projectSession is null || _assetProcessingProfileHistory is null ||
            !_assetProcessingProfileHistory.Undo())
        {
            return false;
        }

        var saved = AssetProcessingProfileStore.Save(
            _projectSession.RootDirectory,
            _assetProcessingProfileHistory.State.Profiles);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetProcessingProfileHistory.Redo();
        return false;
    }

    private bool RedoAssetProcessingProfile()
    {
        if (_projectSession is null || _assetProcessingProfileHistory is null ||
            !_assetProcessingProfileHistory.Redo())
        {
            return false;
        }

        var saved = AssetProcessingProfileStore.Save(
            _projectSession.RootDirectory,
            _assetProcessingProfileHistory.State.Profiles);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetProcessingProfileHistory.Undo();
        return false;
    }

    private bool ApplyAssistantWorkspaceProfile(
        AssistantWorkspaceProfile profile,
        Guid planId,
        bool dryRun)
    {
        if (_projectSession is null || _assistantProfileHistory is null) return false;
        var command = new SetAssistantWorkspaceProfileCommand(
            CommandId.From(Guid.NewGuid()),
            planId,
            _assistantProfileHistory.State.Revision,
            profile,
            AssistantPermissionLevel.Configure,
            dryRun);
        var result = _assistantProfileHandler.Handle(_assistantProfileHistory.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/profile",
                profile.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = AssistantWorkspaceProfileStore.Save(_projectSession.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTANT_PROFILE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _assistantProfileHistory.Record(result);
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_ASSISTANT_PROFILE_SAVED provider={profile.ActiveProvider.Key} " +
            $"prompts={profile.Prompts.Count} revision={_assistantProfileHistory.State.Revision} plan={planId:D}");
        return true;
    }

    private bool UndoAssistantWorkspaceProfile()
    {
        if (_projectSession is null || _assistantProfileHistory is null || !_assistantProfileHistory.Undo())
            return false;
        var saved = AssistantWorkspaceProfileStore.Save(
            _projectSession.RootDirectory,
            _assistantProfileHistory.State.Profile);
        if (saved.Succeeded) return true;
        _assistantProfileHistory.Redo();
        return false;
    }

    private bool RedoAssistantWorkspaceProfile()
    {
        if (_projectSession is null || _assistantProfileHistory is null || !_assistantProfileHistory.Redo())
            return false;
        var saved = AssistantWorkspaceProfileStore.Save(
            _projectSession.RootDirectory,
            _assistantProfileHistory.State.Profile);
        if (saved.Succeeded) return true;
        _assistantProfileHistory.Undo();
        return false;
    }

    private bool ApplyExternalWorkerSecurityPolicy(
        ExternalWorkerSecurityPolicy policy,
        Guid transactionId,
        Guid planId,
        bool dryRun)
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null) return false;
        var command = new SetExternalWorkerSecurityPolicyCommand(
            CommandId.From(transactionId),
            planId,
            _externalWorkerSecurityHistory.State.Revision,
            policy,
            AssistantPermissionLevel.Configure,
            dryRun);
        var result = _externalWorkerSecurityHandler.Handle(_externalWorkerSecurityHistory.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assets/worker-security",
                policy.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var saved = ExternalWorkerSecurityPolicyStore.Save(_projectSession.RootDirectory, result.State.Policy);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_WORKER_SECURITY_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _externalWorkerSecurityHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_WORKER_SECURITY_SAVED policy={policy.Id:D} " +
                 $"revision={_externalWorkerSecurityHistory.State.Revision.Value} transaction={transactionId:D} plan={planId:D}");
        return true;
    }

    private bool UndoExternalWorkerSecurityPolicy()
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null ||
            !_externalWorkerSecurityHistory.Undo()) return false;
        var saved = ExternalWorkerSecurityPolicyStore.Save(
            _projectSession.RootDirectory, _externalWorkerSecurityHistory.State.Policy);
        if (saved.Succeeded) return true;
        _externalWorkerSecurityHistory.Redo();
        return false;
    }

    private bool RedoExternalWorkerSecurityPolicy()
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null ||
            !_externalWorkerSecurityHistory.Redo()) return false;
        var saved = ExternalWorkerSecurityPolicyStore.Save(
            _projectSession.RootDirectory, _externalWorkerSecurityHistory.State.Policy);
        if (saved.Succeeded) return true;
        _externalWorkerSecurityHistory.Undo();
        return false;
    }

    private EditorKeymapCommandResult ApplyEditorKeymap(EditorKeymapProfile profile, bool dryRun)
    {
        if (_projectSession is null || _editorKeymapHistory is null)
        {
            var state = new EditorKeymapState(profile);
            var command = new SetEditorKeymapCommand(
                CommandId.From(Guid.NewGuid()), state.Revision, profile, dryRun);
            return new EditorKeymapCommandResult(false, state, state.Revision, null,
                [new CommandDiagnostic("authoring.keymap.workspace_missing", "Open a Forge workspace before changing shortcuts.")],
                null, null, new AuditEntry(command.CommandId, "set_editor_keymap", DateTimeOffset.UtcNow, false));
        }

        var request = new SetEditorKeymapCommand(
            CommandId.From(Guid.NewGuid()), _editorKeymapHistory.State.Revision, profile, dryRun);
        var result = _editorKeymapHandler.Handle(_editorKeymapHistory.State, request);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                ".forge/editor-keymap",
                profile.Id.ToString())).ToArray());
            return result;
        }
        if (dryRun) return result;

        var saved = EditorKeymapProfileStore.Save(_projectSession.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_KEYMAP_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return result with
            {
                Succeeded = false,
                State = _editorKeymapHistory.State,
                NewRevision = null,
                Diagnostics = saved.Diagnostics.Select(diagnostic =>
                    new CommandDiagnostic(diagnostic.Code, diagnostic.Message)).ToArray(),
                Patch = null,
                Inverse = null,
                Audit = result.Audit with { Applied = false },
            };
        }

        _editorKeymapHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_KEYMAP_SAVED preset={profile.Preset} " +
                 $"revision={_editorKeymapHistory.State.Revision.Value} command={request.CommandId}");
        return result;
    }

    private bool UndoEditorKeymap()
    {
        if (_projectSession is null || _editorKeymapHistory is null || !_editorKeymapHistory.Undo())
            return false;
        var saved = EditorKeymapProfileStore.Save(_projectSession.RootDirectory, _editorKeymapHistory.State.Profile);
        if (saved.Succeeded) return true;
        _editorKeymapHistory.Redo();
        return false;
    }

    private bool RedoEditorKeymap()
    {
        if (_projectSession is null || _editorKeymapHistory is null || !_editorKeymapHistory.Redo())
            return false;
        var saved = EditorKeymapProfileStore.Save(_projectSession.RootDirectory, _editorKeymapHistory.State.Profile);
        if (saved.Succeeded) return true;
        _editorKeymapHistory.Undo();
        return false;
    }

    private bool ApplyLoreIndex(
        LoreIndexDefinition index,
        Guid transactionId,
        Guid planId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _loreIndexHistory is null) return false;
        var command = new SetLoreIndexCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            planId,
            _loreIndexHistory.State.Revision,
            index,
            AssistantPermissionLevel.Configure,
            dryRun);
        var result = _loreIndexHandler.Handle(_loreIndexHistory.State, command, _history.State.Project);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/lore-index",
                index.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = LoreIndexStore.Save(_projectSession.RootDirectory, result.State.Index);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_LORE_INDEX_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _loreIndexHistory.Record(result);
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_LORE_INDEX_SAVED entries={index.Entries.Count} " +
            $"revision={_loreIndexHistory.State.Revision} transaction={transactionId:D} plan={planId:D}");
        return true;
    }

    private bool UndoLoreIndex()
    {
        if (_projectSession is null || _loreIndexHistory is null || !_loreIndexHistory.Undo()) return false;
        var saved = LoreIndexStore.Save(_projectSession.RootDirectory, _loreIndexHistory.State.Index);
        if (saved.Succeeded) return true;
        _loreIndexHistory.Redo();
        return false;
    }

    private bool RedoLoreIndex()
    {
        if (_projectSession is null || _loreIndexHistory is null || !_loreIndexHistory.Redo()) return false;
        var saved = LoreIndexStore.Save(_projectSession.RootDirectory, _loreIndexHistory.State.Index);
        if (saved.Succeeded) return true;
        _loreIndexHistory.Undo();
        return false;
    }

    private bool ApplyAssistedAreaPlans(
        AssistedAreaPlanCatalog catalog,
        Guid transactionId,
        Guid planId,
        AssistantPermissionLevel permission,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _assistedAreaPlanHistory is null) return false;
        var command = new SetAssistedAreaPlansCommand(
            CommandId.From(Guid.NewGuid()), transactionId, planId, _assistedAreaPlanHistory.State.Revision,
            catalog, permission, dryRun);
        var result = _assistedAreaPlanHandler.Handle(_assistedAreaPlanHistory.State, command, _history.State.Project);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/area-plans",
                catalog.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = AssistedAreaPlanStore.Save(_projectSession.RootDirectory, result.State.Catalog);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_AREA_PLANS_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _assistedAreaPlanHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_AREA_PLANS_SAVED plans={catalog.Plans.Count} " +
                 $"revision={_assistedAreaPlanHistory.State.Revision} geometry=false transaction={transactionId:D} plan={planId:D}");
        return true;
    }

    private bool UndoAssistedAreaPlans()
    {
        if (_projectSession is null || _assistedAreaPlanHistory is null || !_assistedAreaPlanHistory.Undo()) return false;
        var saved = AssistedAreaPlanStore.Save(_projectSession.RootDirectory, _assistedAreaPlanHistory.State.Catalog);
        if (saved.Succeeded) return true;
        _assistedAreaPlanHistory.Redo();
        return false;
    }

    private bool BuildAssistedAreaPlan(
        Guid assistedAreaPlanId,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _assistedAreaPlanHistory is null) return false;
        var command = new BuildAssistedAreaPlanCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            assistedAreaPlanId,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedAreaBuildHandler.Handle(
            _history.State,
            command,
            _assistedAreaPlanHistory.State.Catalog);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/area-build",
                assistedAreaPlanId.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_AREA_BUILD_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_AREA_BUILD_APPLIED area_plan={assistedAreaPlanId:D} " +
                 $"placements={result.EntityIds.Count} revision={_history.State.Revision} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedDialogue(
        AssistedDialogueProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedDialogueCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedDialogueHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/dialogue",
                proposal.Dialogue.Id.ToString())).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_DIALOGUE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_DIALOGUE_APPLIED dialogue={proposal.Dialogue.Id} " +
                 $"nodes={proposal.Dialogue.Nodes.Count} revision={_history.State.Revision} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedQuest(
        AssistedQuestProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedQuestCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedQuestHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/quest",
                proposal.Quest.Id.ToString())).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_QUEST_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_QUEST_APPLIED quest={proposal.Quest.Id} " +
                 $"objectives={proposal.AdvancedQuest.Objectives.Count} revision={_history.State.Revision} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedForgeScript(
        AssistedForgeScriptProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedForgeScriptCommand(
            Guid.NewGuid(),
            transactionId,
            executionPlanId,
            _history.State.Revision.Value,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedForgeScriptHandler.Handle(
            _projectSession.RootDirectory,
            _history.State.Revision.Value,
            command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(code => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                code,
                "La transaction ForgeScript assistée a été refusée.",
                proposal.RelativePath,
                proposal.ScriptId.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Applied;
        _assistedForgeScriptHistory.Record(result);
        _buildPipeline?.Refresh();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_SCRIPT_APPLIED path={proposal.RelativePath} " +
                 $"api={proposal.ForgeScriptApiVersion} tests={proposal.Tests.Count} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedBoss(
        AssistedBossProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedBossCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedBossHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                $"bosses/{proposal.Boss.Id:D}",
                proposal.Boss.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_BOSS_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_BOSS_APPLIED boss={proposal.Boss.Id:D} " +
                 $"abilities={proposal.Boss.Blueprint.Combat!.Abilities.Count} phases={proposal.Boss.Phases.Count} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedAbility(
        AssistedAbilityProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedAbilityCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedAbilityHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                $"abilities/{proposal.Orchestration.Id:D}",
                proposal.Orchestration.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_ABILITY_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_ABILITY_APPLIED ability={proposal.Orchestration.Id:D} " +
                 $"owner={proposal.Orchestration.OwnerBlueprintId} tests={proposal.Tests.Count} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedBlenderCreation(
        AssistedBlenderCreationProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _assetExternalEditHistory is null) return false;
        var state = _assetExternalEditHistory.State;
        if (_assistedBlenderCreationHistory is null ||
            _assistedBlenderCreationHistory.State.Revision != state.Revision ||
            !_assistedBlenderCreationHistory.State.Configurations.SequenceEqual(state.Configurations))
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(state);
        var command = new ApplyAssistedBlenderCreationCommand(
            CommandId.From(Guid.NewGuid()), transactionId, executionPlanId, state.Revision, proposal,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);
        var result = _assistedBlenderCreationHandler.Handle(_projectSession.RootDirectory, state, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(code => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error, code, code,
                $"assets/assisted-3d/{proposal.Definition.Id:D}", proposal.Definition.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return !result.Applied && result.Patches.Count == 3;
        _assistedBlenderCreationHistory.Record(result);
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(result.State);
        _assetBrowser?.Refresh();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_BLENDER_APPLIED asset={proposal.Configuration.AssetId} " +
                 $"primitives={proposal.Definition.Primitives.Count} transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool UndoAssistedBlenderCreation()
    {
        if (_projectSession is null || _assistedBlenderCreationHistory is null ||
            !_assistedBlenderCreationHistory.Undo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedBlenderCreationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool RedoAssistedBlenderCreation()
    {
        if (_projectSession is null || _assistedBlenderCreationHistory is null ||
            !_assistedBlenderCreationHistory.Redo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedBlenderCreationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool ApplyAssistedModel3DGeneration(
        AssistedModel3DGenerationProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _assetExternalEditHistory is null) return false;
        var state = _assetExternalEditHistory.State;
        if (_assistedModel3DGenerationHistory is null ||
            _assistedModel3DGenerationHistory.State.Revision != state.Revision ||
            !_assistedModel3DGenerationHistory.State.Configurations.SequenceEqual(state.Configurations))
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(state);
        var command = new ApplyAssistedModel3DGenerationCommand(
            CommandId.From(Guid.NewGuid()), transactionId, executionPlanId, state.Revision, proposal,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);
        var result = _assistedModel3DGenerationHandler.Handle(_projectSession.RootDirectory, state, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(code => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error, code, code,
                $"assets/assisted-provider-3d/{proposal.Receipt.Id:D}", proposal.Receipt.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return !result.Applied && result.Patches.Count == 4;
        _assistedModel3DGenerationHistory.Record(result);
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(result.State);
        _assetBrowser?.Refresh();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_MODEL3D_APPLIED asset={proposal.Configuration.AssetId} " +
                 $"provider={proposal.Receipt.Provider.Key} triangles={proposal.Receipt.SourceTriangleCount} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool UndoAssistedModel3DGeneration()
    {
        if (_projectSession is null || _assistedModel3DGenerationHistory is null ||
            !_assistedModel3DGenerationHistory.Undo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedModel3DGenerationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool RedoAssistedModel3DGeneration()
    {
        if (_projectSession is null || _assistedModel3DGenerationHistory is null ||
            !_assistedModel3DGenerationHistory.Redo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedModel3DGenerationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool UndoAssistedForgeScript()
    {
        if (_projectSession is null || !_assistedForgeScriptHistory.Undo(_projectSession.RootDirectory)) return false;
        _buildPipeline?.Refresh();
        return true;
    }

    private bool RedoAssistedForgeScript()
    {
        if (_projectSession is null || !_assistedForgeScriptHistory.Redo(_projectSession.RootDirectory)) return false;
        _buildPipeline?.Refresh();
        return true;
    }

    private bool RedoAssistedAreaPlans()
    {
        if (_projectSession is null || _assistedAreaPlanHistory is null || !_assistedAreaPlanHistory.Redo()) return false;
        var saved = AssistedAreaPlanStore.Save(_projectSession.RootDirectory, _assistedAreaPlanHistory.State.Catalog);
        if (saved.Succeeded) return true;
        _assistedAreaPlanHistory.Undo();
        return false;
    }

    private AssetPackageManifest? PrepareAssetPackageManifest(
        AssetCatalogItem item,
        AssetProcessingProfile profile,
        bool includeVfx,
        bool dryRun)
    {
        if (_projectSession is null || _assetPackageManifestHistory is null)
        {
            return null;
        }

        try
        {
            var planned = AssetImportPlanner.Plan(item, profile);
            if (!planned.Succeeded || planned.Plan is null)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_PLAN_FAILED asset={item.ExternalId}");
                return null;
            }

            AssetManifestArtifactInput artifact;
            if (dryRun)
            {
                var outputPath = planned.Plan.Tasks.First().OutputRelativePath;
                artifact = new AssetManifestArtifactInput(
                    outputPath,
                    item.ContentSha256,
                    Math.Max(1, item.TotalBytes),
                    string.Equals(profile.SourceFormat, profile.CanonicalFormat, StringComparison.Ordinal));
            }
            else
            {
                var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(GetRepositoryRoot());
                if (repositoryRoot is null)
                {
                    return null;
                }

                var catalog = AssetCatalogLoader.Load(repositoryRoot);
                if (catalog.Catalog is null || catalog.Diagnostics.Count > 0)
                {
                    return null;
                }

                var execution = AssetImportExecutor.Execute(
                    planned.Plan,
                    catalog.VaultRoot,
                    _projectSession.RootDirectory);
                if (!execution.Succeeded)
                {
                    GD.PrintErr(
                        $"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_IMPORT_FAILED {execution.Diagnostics[0].Code}");
                    return null;
                }

                var prepared = execution.Execution!;
                artifact = new AssetManifestArtifactInput(
                    prepared.OutputRelativePath,
                    prepared.OutputContentSha256,
                    prepared.OutputBytes,
                    prepared.Status == AssetImportExecutionStatus.CanonicalReady);
            }

            var manifest = AssetPackageManifestFactory.Create(
                item,
                profile,
                artifact,
                AssetPackageManifestAuthoringOptions.Default with { IncludeVfx = includeVfx });
            var command = new SetAssetPackageManifestCommand(
                CommandId.From(Guid.NewGuid()),
                _assetPackageManifestHistory.State.Revision,
                manifest,
                dryRun);
            var result = _assetPackageManifestHandler.Handle(_assetPackageManifestHistory.State, command);
            if (!result.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_INVALID {result.Diagnostics[0].Code}");
                return null;
            }

            if (dryRun)
            {
                return result.Patch is not null && !result.Audit.Applied ? manifest : null;
            }

            var saved = AssetPackageManifestStore.Save(
                _projectSession.RootDirectory,
                result.State.Manifests);
            if (!saved.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_SAVE_FAILED {saved.Diagnostics[0].Code}");
                return null;
            }

            _assetPackageManifestHistory.Record(result);
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_SAVED asset={item.ExternalId} id={manifest.Id} " +
                $"chunks={manifest.Chunks.Count} revision={_assetPackageManifestHistory.State.Revision}");
            return manifest;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_FAILED {exception.Message}");
            return null;
        }
    }

    private BlueprintDefinition? InstallAssetForPlacement(
        AssetCatalogItem item,
        AssetProcessingProfile profile,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _assetPackageManifestHistory is null)
        {
            return null;
        }

        try
        {
            var manifest = _assetPackageManifestHistory.State.Find(item.Id) ??
                PrepareAssetPackageManifest(item, profile, includeVfx: false, dryRun);
            if (manifest is null)
            {
                return null;
            }

            var command = new InstallAssetBlueprintCommand(
                CommandId.From(Guid.NewGuid()),
                _history.State.Revision,
                item,
                manifest,
                dryRun);
            var result = _installAssetBlueprintHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_TOOLSET_ASSET_INSTALL_FAILED asset={item.ExternalId} " +
                    $"code={result.Diagnostics[0].Code}");
                return null;
            }

            var blueprint = AssetBlueprintFactory.Create(item, manifest);
            if (dryRun)
            {
                return result.Patch is not null && !result.Audit.Applied ? blueprint : null;
            }

            if (!GovernedAssetModelVerifier.Verify(_projectSession.RootDirectory, manifest).Succeeded ||
                !ApplyCommand(
                    result,
                    $"Asset « {item.DisplayName} » installé dans la palette.",
                    selectedEntityId: null))
            {
                return null;
            }

            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_ASSET_INSTALL_PASS asset={item.ExternalId} blueprint={blueprint.Id} " +
                "placement=palette-drag visual=governed-glb");
            return _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_INSTALL_FAILED asset={item.ExternalId} {exception.Message}");
            return null;
        }
    }

    private bool UndoAssetPackageManifest()
    {
        if (_projectSession is null || _assetPackageManifestHistory is null ||
            !_assetPackageManifestHistory.Undo())
        {
            return false;
        }

        var saved = AssetPackageManifestStore.Save(
            _projectSession.RootDirectory,
            _assetPackageManifestHistory.State.Manifests);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetPackageManifestHistory.Redo();
        return false;
    }

    private bool RedoAssetPackageManifest()
    {
        if (_projectSession is null || _assetPackageManifestHistory is null ||
            !_assetPackageManifestHistory.Redo())
        {
            return false;
        }

        var saved = AssetPackageManifestStore.Save(
            _projectSession.RootDirectory,
            _assetPackageManifestHistory.State.Manifests);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetPackageManifestHistory.Undo();
        return false;
    }

    private AssetExternalEditConfiguration? ConfigureAssetExternalEdit(
        AssetCatalogItem item,
        bool watchForChanges,
        bool dryRun)
    {
        if (_projectSession is null || _assetExternalEditHistory is null)
        {
            return null;
        }

        try
        {
            var manifest = _assetPackageManifestHistory?.State.Find(item.Id);
            if (manifest is null && dryRun)
            {
                var profile = _assetProcessingProfileHistory?.State.Find(item.Id, AssetProcessingKind.Model) ??
                    AssetImportPlanner.CreateDefaultProfile(item, AssetProcessingKind.Model);
                var plan = AssetImportPlanner.Plan(item, profile);
                if (!plan.Succeeded || plan.Plan is null)
                {
                    return null;
                }

                manifest = AssetPackageManifestFactory.Create(
                    item,
                    profile,
                    new AssetManifestArtifactInput(
                        plan.Plan.Tasks.First().OutputRelativePath,
                        item.ContentSha256,
                        Math.Max(1, item.TotalBytes),
                        string.Equals(profile.SourceFormat, profile.CanonicalFormat, StringComparison.Ordinal)));
            }

            if (manifest is null)
            {
                return null;
            }

            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(GetRepositoryRoot());
            if (repositoryRoot is null)
            {
                return null;
            }

            var catalog = AssetCatalogLoader.Load(repositoryRoot);
            if (catalog.Catalog is null || catalog.Diagnostics.Count > 0 ||
                string.IsNullOrWhiteSpace(item.RelativeModelPath) ||
                !AssetCatalogValidator.IsSafeRelativePath(item.RelativeModelPath))
            {
                return null;
            }

            var sourcePath = Path.Combine(
                catalog.VaultRoot,
                item.RelativeModelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                return null;
            }

            using var stream = File.OpenRead(sourcePath);
            var sourceHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            var configuration = AssetExternalEditFactory.CreateBlenderRoundTrip(
                item,
                manifest,
                sourceHash,
                watchForChanges);
            var command = new SetAssetExternalEditCommand(
                CommandId.From(Guid.NewGuid()),
                _assetExternalEditHistory.State.Revision,
                configuration,
                dryRun);
            var result = _assetExternalEditHandler.Handle(_assetExternalEditHistory.State, command);
            if (!result.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_INVALID {result.Diagnostics[0].Code}");
                return null;
            }

            if (dryRun)
            {
                return result.Patch is not null && !result.Audit.Applied ? configuration : null;
            }

            var previous = _assetExternalEditHistory.State.Configurations;
            var saved = AssetExternalEditStore.Save(_projectSession.RootDirectory, result.State.Configurations);
            if (!saved.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_SAVE_FAILED {saved.Diagnostics[0].Code}");
                return null;
            }

            var request = BlenderWorkerRequestStore.Save(
                _projectSession.RootDirectory,
                configuration,
                BlenderWorkerOptions.Default with
                {
                    GenerateCollision = manifest.Collision is not null,
                    LodRatios = manifest.Lod?.Levels
                        .Where(level => level.Level > 0)
                        .Select(level => level.ScreenRatio)
                        .ToArray() ?? [],
                });
            if (!request.Succeeded)
            {
                AssetExternalEditStore.Save(_projectSession.RootDirectory, previous);
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_REQUEST_FAILED {request.Diagnostics[0].Code}");
                return null;
            }

            _assetExternalEditHistory.Record(result);
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_SAVED asset={item.ExternalId} id={configuration.Id} " +
                $"request={request.RequestRelativePath} revision={_assetExternalEditHistory.State.Revision.Value}");
            return configuration;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_FAILED {exception.Message}");
            return null;
        }
    }

    private bool UndoAssetExternalEdit()
    {
        if (_projectSession is null || _assetExternalEditHistory is null || !_assetExternalEditHistory.Undo())
        {
            return false;
        }

        var saved = AssetExternalEditStore.Save(
            _projectSession.RootDirectory,
            _assetExternalEditHistory.State.Configurations);
        if (saved.Succeeded)
        {
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
            return true;
        }

        _assetExternalEditHistory.Redo();
        return false;
    }

    private bool RedoAssetExternalEdit()
    {
        if (_projectSession is null || _assetExternalEditHistory is null || !_assetExternalEditHistory.Redo())
        {
            return false;
        }

        var saved = AssetExternalEditStore.Save(
            _projectSession.RootDirectory,
            _assetExternalEditHistory.State.Configurations);
        if (saved.Succeeded)
        {
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
            return true;
        }

        _assetExternalEditHistory.Undo();
        return false;
    }


}
#endif
