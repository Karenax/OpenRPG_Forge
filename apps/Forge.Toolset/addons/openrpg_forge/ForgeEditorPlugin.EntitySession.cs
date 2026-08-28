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
    private void ShowExampleEntityPropertiesDialog()
    {
        if (_history is null || _entityPicker is null)
        {
            return;
        }

        var entities = GetActiveArea().Entities;
        var exampleIndex = entities.ToList().FindIndex(entity => entity.Overrides.Properties.Count > 0);
        _entityPicker.Selected = exampleIndex >= 0 ? exampleIndex : 0;
        EntitySelectionChanged(_entityPicker.Selected);
        ShowEntityPropertiesDialog();
    }

    private void ShowEntityPropertiesDialog()
    {
        var selected = GetSelectedEntity();
        if (_history is null || selected is null || _entityPropertiesDialog is null)
        {
            return;
        }

        var blueprint = _history.State.Project.Blueprints.Single(candidate => candidate.Id == selected.BlueprintId);
        _draftOverrides.Clear();
        _draftOverrides.AddRange(selected.Overrides.Properties);
        _selectedDraftOverrideIndex = -1;
        _entityPropertiesIdentity!.Text =
            $"{blueprint.DisplayName} · instance {selected.Id.ToString()[..8]} · schéma v{selected.Overrides.SchemaVersion}";
        _entityPropertyKey!.Clear();
        _entityPropertyKind!.Selected = 0;
        _entityPropertyValue!.Clear();
        _entityPropertyRemoveButton!.Disabled = true;
        SetOverrideValidation("Sélectionnez une propriété ou créez une nouvelle valeur locale.", false);
        RefreshDraftOverrides();
        _entityPropertiesDialog.Exclusive = true;
        _entityPropertiesDialog.PopupCentered(new Vector2I(720, 560));
        ApplyForgeWindowIcon(_entityPropertiesDialog);
        _entityPropertyKey.GrabFocus();
    }

    private void HideEntityPropertiesDialog()
    {
        _entityPropertiesDialog?.Hide();
        if (_entityPropertiesDialog is not null)
        {
            _entityPropertiesDialog.Exclusive = false;
        }
    }

    private void EntitySelectionChanged(long _)
    {
        RefreshOverrideSummary();
        RefreshScaleControls();
        PublishSelectedEntity();
    }

    private void SynchronizeUniformScale(double value)
    {
        if (_uniformScale?.ButtonPressed == true && _scaleY is not null && _scaleZ is not null)
        {
            _scaleY.SetValueNoSignal(value);
            _scaleZ.SetValueNoSignal(value);
        }
    }

    private void ToggleUniformScale(bool enabled)
    {
        if (enabled && _scaleX is not null)
        {
            SynchronizeUniformScale(_scaleX.Value);
        }
    }

    private void ScaleSelectedDown() => MultiplySelectedScale(0.9f);

    private void ScaleSelectedUp() => MultiplySelectedScale(1.1f);

    private void MultiplySelectedScale(float factor)
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (selected.Count > 0)
        {
            ApplyDirectManipulation(selected, EditorTransformTool.Scale,
                new Vector3Value(factor, factor, factor), EditorTransformAlignment.None,
                selected.Count == 1 ? "Échelle de l’entité appliquée." : $"Échelle appliquée à {selected.Count} entités.");
        }
    }

    private void ApplySelectedScale()
    {
        var selected = GetSelectedEntity();
        if (selected is null || _scaleX is null || _scaleY is null || _scaleZ is null)
        {
            return;
        }

        ApplyScaleCommand(
            [selected.Id],
            new Vector3Value((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value),
            multiply: false);
    }

    private bool ApplyScaleCommand(IReadOnlyList<EntityId> entityIds, Vector3Value scale, bool multiply)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new ScaleEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            GetActiveArea().Id,
            entityIds,
            scale,
            multiply,
            DryRun: false);
        var preview = _scaleEntitiesHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return ApplyCommand(preview, string.Empty, entityIds.Count == 1 ? entityIds[0] : null);
        }

        var applied = ApplyCommand(
            _scaleEntitiesHandler.Handle(_history.State, command),
            entityIds.Count == 1 ? "Échelle de l’entité appliquée." : $"Échelle appliquée à {entityIds.Count} entités.",
            entityIds.Count == 1 ? entityIds[0] : null);
        if (applied)
        {
            RefreshScaleControls();
            RebuildNavigationAfterScale();
        }

        return applied;
    }

    private void RebuildNavigationAfterScale()
    {
        if (_history is null || _projectPreview is null) return;
        var area = GetActiveArea();
        var agent = area.Navigation.AgentProfiles.FirstOrDefault();
        if (agent is null) return;
        var surface = NavigationSurfaceBuilder.Build(_history.State.Project, area, agent);
        _projectPreview.ShowNavigationOverlay(surface);
        if (_detailsLabel is not null)
        {
            _detailsLabel.Text += $" · navigation reconstruite {surface.WalkableCells.Count} cellules";
        }
    }

    private void RefreshScaleControls()
    {
        if (_scaleX is null || _scaleY is null || _scaleZ is null)
        {
            return;
        }

        var selected = GetSelectedEntity();
        if (selected is null)
        {
            return;
        }

        _scaleX.SetValueNoSignal(selected.Transform.Scale.X);
        _scaleY.SetValueNoSignal(selected.Transform.Scale.Y);
        _scaleZ.SetValueNoSignal(selected.Transform.Scale.Z);
    }

    private void SelectDraftOverride(long index)
    {
        if (index < 0 || index >= _draftOverrides.Count)
        {
            return;
        }

        _selectedDraftOverrideIndex = (int)index;
        var property = _draftOverrides[_selectedDraftOverrideIndex];
        _entityPropertyKey!.Text = property.Key;
        _entityPropertyKind!.Selected = (int)property.Kind;
        _entityPropertyValue!.Text = property.Value;
        _entityPropertyRemoveButton!.Disabled = false;
        SetOverrideValidation("La propriété sélectionnée peut être modifiée ou retirée.", false);
    }

    private void UpsertDraftOverride()
    {
        if (_entityPropertyKey is null || _entityPropertyKind is null || _entityPropertyValue is null)
        {
            return;
        }

        try
        {
            var property = new EntityPropertyOverride(
                _entityPropertyKey.Text,
                (EntityPropertyValueKind)_entityPropertyKind.Selected,
                _entityPropertyValue.Text);
            var index = _draftOverrides.FindIndex(candidate =>
                string.Equals(candidate.Key, property.Key, StringComparison.Ordinal));
            if (index >= 0)
            {
                _draftOverrides[index] = property;
            }
            else
            {
                _draftOverrides.Add(property);
                _draftOverrides.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
                index = _draftOverrides.FindIndex(candidate => candidate.Key == property.Key);
            }

            RefreshDraftOverrides(index);
            SetOverrideValidation($"✓ Propriété « {property.Key} » prête à être appliquée.", true);
        }
        catch (ArgumentException exception)
        {
            SetOverrideValidation($"Valeur invalide — {exception.Message}", false, isError: true);
        }
    }

    private void RemoveDraftOverride()
    {
        if (_selectedDraftOverrideIndex < 0 || _selectedDraftOverrideIndex >= _draftOverrides.Count)
        {
            return;
        }

        var removedKey = _draftOverrides[_selectedDraftOverrideIndex].Key;
        _draftOverrides.RemoveAt(_selectedDraftOverrideIndex);
        _selectedDraftOverrideIndex = -1;
        _entityPropertyKey!.Clear();
        _entityPropertyValue!.Clear();
        _entityPropertyRemoveButton!.Disabled = true;
        RefreshDraftOverrides();
        SetOverrideValidation($"La propriété « {removedKey} » sera retirée à l’application.", false);
    }

    private void ApplyDraftOverrides()
    {
        var overrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            _draftOverrides);
        if (ApplyEntityOverrides(overrides))
        {
            HideEntityPropertiesDialog();
        }
    }

    private bool ApplyEntityOverrides(EntityPropertyOverrides overrides)
    {
        var selected = GetSelectedEntity();
        if (_history is null || selected is null)
        {
            return false;
        }

        var area = GetActiveArea();
        var command = new SetEntityOverridesCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            selected.Id,
            overrides,
            DryRun: false);
        return ApplyCommand(
            _setOverridesHandler.Handle(_history.State, command),
            $"{overrides.Properties.Count} propriété(s) locale(s) appliquée(s).",
            selected.Id);
    }

    private void RefreshDraftOverrides(int selectedIndex = -1)
    {
        if (_entityPropertiesList is null)
        {
            return;
        }

        _entityPropertiesList.Clear();
        foreach (var property in _draftOverrides)
        {
            var displayedValue = property.Value.Length > 38
                ? $"{property.Value[..35]}…"
                : property.Value;
            _entityPropertiesList.AddItem(
                $"{property.Key}   ·   {FormatPropertyKind(property.Kind)}   ·   {displayedValue}");
        }

        if (selectedIndex >= 0 && selectedIndex < _draftOverrides.Count)
        {
            _selectedDraftOverrideIndex = selectedIndex;
            _entityPropertiesList.Select(selectedIndex);
            _entityPropertyRemoveButton!.Disabled = false;
        }
    }

    private void SetOverrideValidation(string message, bool isSuccess, bool isError = false)
    {
        if (_entityPropertyValidation is null)
        {
            return;
        }

        _entityPropertyValidation.Text = message;
        _entityPropertyValidation.AddThemeColorOverride(
            "font_color",
            isError ? new Color("ff8c8c") : isSuccess ? new Color("83d6ae") : new Color("ab9acb"));
    }

    private static string FormatPropertyKind(EntityPropertyValueKind kind) => kind switch
    {
        EntityPropertyValueKind.String => "Texte",
        EntityPropertyValueKind.Boolean => "Booléen",
        EntityPropertyValueKind.Integer => "Entier",
        EntityPropertyValueKind.Number => "Nombre",
        _ => kind.ToString(),
    };

    private static string FormatPaletteOrigin(BlueprintPaletteOrigin origin) => origin switch
    {
        BlueprintPaletteOrigin.InstalledPack => "Standard",
        BlueprintPaletteOrigin.ProjectContent => "Projet",
        BlueprintPaletteOrigin.Generated => "Généré",
        BlueprintPaletteOrigin.Temporary => "Temporaire",
        _ => origin.ToString(),
    };

    private void OpenSelectedProject(string workspaceDirectory) =>
        OpenProjectDirectory(workspaceDirectory);

    private void ReloadProject() =>
        OpenProjectDirectory(_projectSession?.RootDirectory ?? GetDefaultWorkspaceDirectory());

    private void OpenProjectDirectory(string workspaceDirectory)
    {
        if (_statusLabel is null || _detailsLabel is null ||
            _projectPathLabel is null || _saveButton is null || _exportButton is null)
        {
            return;
        }

        EndRecoverySession();
        _semanticClipboard.Clear();
        if (_clipboardSummary is not null)
        {
            _clipboardSummary.Text = "Aucun contenu copié";
        }
        var result = ProjectSession.Open(workspaceDirectory);
        if (!result.Succeeded || result.Session is null)
        {
            _projectSession = null;
            _history = null;
            _assetProcessingProfileHistory = null;
            _assetGovernanceHistory = null;
            _qualityFoundationHistory = null;
            _assetPackageManifestHistory = null;
            _assetExternalEditHistory = null;
            _externalWorkerSecurityHistory = null;
            _editorKeymapHistory = null;
            _assistantProfileHistory = null;
            _loreIndexHistory = null;
            _activeAreaContext.Clear();
            _selectionContext.SetLocked(false);
            _selectionContext.Clear();
            _projectPreview?.Clear();
            _statusLabel.Text = "✗ Impossible d’ouvrir ce dossier Forge";
            _detailsLabel.Text = FormatDiagnostics(result.Diagnostics);
            _projectPathLabel.Text = workspaceDirectory;
            _saveButton.Disabled = true;
            _exportButton.Disabled = true;
            RefreshEditorControls();
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(MapSessionDiagnostic).ToArray());
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_PROJECT_INVALID {result.Diagnostics.FirstOrDefault()?.Code}");
            return;
        }

        _projectSession = result.Session;
        _recoveryStore = new RecoverySnapshotStore(_projectSession.Project.Module.RuntimePolicies.Extensions
            .OperationalReadiness.Recovery.RetainedSnapshots);
        _recoverySessionInspection = _recoverySessionStore.Begin(_projectSession.RootDirectory,
            _projectSession.Project.Id, DateTimeOffset.UtcNow, System.Environment.ProcessId,
            "creator-r060-session");
        _recoverySessionId = _recoverySessionInspection.Current.Id;
        _selectionContext.SetLocked(false);
        _selectionContext.Clear();
        _history = new ProjectCommandHistory(new ProjectState(
            _projectSession.Project,
            new ProjectRevision(0)));
        var assetProfiles = AssetProcessingProfileStore.Load(_projectSession.RootDirectory);
        _assetProcessingProfileHistory = new AssetProcessingProfileCommandHistory(assetProfiles.Profiles);
        if (!assetProfiles.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_PROFILES_INVALID {assetProfiles.Diagnostics[0].Code}");
        }
        var assetGovernance = AssetGovernanceStore.Load(_projectSession.RootDirectory);
        _assetGovernanceHistory = new AssetGovernanceCommandHistory(assetGovernance.Records);
        if (!assetGovernance.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_GOVERNANCE_INVALID {assetGovernance.Diagnostics[0].Code}");
        }
        var qualityFoundation = QualityFoundationProfileStore.Load(
            _projectSession.RootDirectory, _projectSession.Project.Id.Value);
        _qualityFoundationHistory = new QualityFoundationCommandHistory(qualityFoundation.Profile);
        if (!qualityFoundation.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_QUALITY_FOUNDATION_INVALID {qualityFoundation.Diagnostics[0].Code}");
        }
        var assetManifests = AssetPackageManifestStore.Load(_projectSession.RootDirectory);
        _assetPackageManifestHistory = new AssetPackageManifestCommandHistory(assetManifests.Manifests);
        if (!assetManifests.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFESTS_INVALID {assetManifests.Diagnostics[0].Code}");
        }
        var externalEdits = AssetExternalEditStore.Load(_projectSession.RootDirectory);
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(externalEdits.Configurations);
        _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
        _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
        if (!externalEdits.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDITS_INVALID {externalEdits.Diagnostics[0].Code}");
        }
        var workerSecurity = ExternalWorkerSecurityPolicyStore.Load(_projectSession.RootDirectory);
        _externalWorkerSecurityHistory = new ExternalWorkerSecurityPolicyCommandHistory(workerSecurity.Policy);
        if (!workerSecurity.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_WORKER_SECURITY_INVALID {workerSecurity.Diagnostics[0].Code}");
        }
        var editorKeymap = EditorKeymapProfileStore.Load(_projectSession.RootDirectory);
        _editorKeymapHistory = new EditorKeymapCommandHistory(editorKeymap.Profile);
        if (!editorKeymap.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_KEYMAP_INVALID {editorKeymap.Diagnostics[0].Code}");
        }
        var assistantProfile = AssistantWorkspaceProfileStore.Load(_projectSession.RootDirectory);
        _assistantProfileHistory = new AssistantWorkspaceProfileCommandHistory(assistantProfile.Profile);
        if (!assistantProfile.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTANT_PROFILE_INVALID {assistantProfile.Diagnostics[0].Code}");
        }
        var loreIndex = LoreIndexStore.Load(_projectSession.RootDirectory);
        _loreIndexHistory = new LoreIndexCommandHistory(loreIndex.Index);
        if (!loreIndex.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_LORE_INDEX_INVALID {loreIndex.Diagnostics[0].Code}");
        }
        var assistedAreaPlans = AssistedAreaPlanStore.Load(_projectSession.RootDirectory);
        _assistedAreaPlanHistory = new AssistedAreaPlanCommandHistory(assistedAreaPlans.Catalog);
        if (!assistedAreaPlans.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_AREA_PLANS_INVALID {assistedAreaPlans.Diagnostics[0].Code}");
        }
        _lastSavedRevision = 0;
        _lastRecoveryRevision = 0;
        var cameraProfile = EditorCameraProfileStore.LoadOrCreate(
            _projectSession.RootDirectory,
            _projectSession.Project.Id);
        _projectPreview?.SetCameraProfile(cameraProfile.Profile, PersistEditorCameraProfile);
        _activeAreaContext.Activate(_projectSession.Project, PreferredCreatorArea(_projectSession.Project));
        ApplyEditorExperience(LoadWorkspaceProfile());
        RebuildPreview();
        RefreshEditorControls();
        _projectPreview?.FrameActiveArea();
        var project = _history.State.Project;
        _rpgKitAuthoringWindow?.Refresh();
        var entityCount = project.Areas.Sum(area => area.Entities.Count);
        var activeArea = GetActiveArea();
        _statusLabel.Text = $"✓ {activeArea.DisplayName} prête à éditer";
        _detailsLabel.Text =
            "Choisissez un objet ou une brosse dans la vue 3D, puis cliquez directement dans la scène.";
        _projectPathLabel.Text = _projectSession.RootDirectory;
        _saveButton.Disabled = false;
        _exportButton.Disabled = false;
        var recoveryPolicy = project.Module.RuntimePolicies.Extensions.OperationalReadiness.Recovery;
        if (recoveryPolicy.OfferRecoveryOnOpen)
        {
            var canonicalProjectPath = Path.Combine(
                _projectSession.RootDirectory,
                ProjectSession.DefaultProjectFileName);
            var canonicalSavedAt = File.Exists(canonicalProjectPath)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(canonicalProjectPath))
                : DateTimeOffset.MinValue;
            var recoveryCandidate = _recoveryStore.FindRecoveryCandidate(
                _projectSession.RootDirectory,
                canonicalSavedAt);
            if (recoveryCandidate is not null || _recoverySessionInspection?.PreviousSessionWasUnclean == true)
            {
                _detailsLabel.Text += recoveryCandidate is null
                    ? " · interruption détectée, aucun snapshot récupérable"
                    : $" · récupération disponible (révision {recoveryCandidate.Revision})";
                _recoveryWindow?.Open();
                GD.Print(recoveryCandidate is null
                    ? "OPENRPG_FORGE_RECOVERY_OFFERED reason=unclean-session candidate=none"
                    : $"OPENRPG_FORGE_RECOVERY_OFFERED reason={(_recoverySessionInspection?.PreviousSessionWasUnclean == true ? "unclean-session" : "newer-snapshot")} id={recoveryCandidate.Id} revision={recoveryCandidate.Revision}");
            }
        }
        GD.Print($"OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILT entities={_projectPreview?.EntityCount ?? 0}");
        GD.Print($"OPENRPG_FORGE_TOOLSET_PROJECT_VALID entities={entityCount}");
    }


}
#endif
