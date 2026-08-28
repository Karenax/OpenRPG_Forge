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
    private void HandleAreaTabRequested(AreaId areaId)
    {
        ActivateArea(areaId);
    }

    private void HandleAreaTabCloseRequested(AreaId areaId)
    {
        var profile = LoadWorkspaceProfile();
        if (profile.OpenAreaTabs.Count <= 1)
        {
            return;
        }

        SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.CloseArea(profile, areaId));
    }

    private void HandleWorkspaceRequest(string workspace)
    {
        _creatorShell?.SetWorkspace(workspace);
        if (string.Equals(workspace, "Dialogues", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_dialogueEditor, OpenDialogueEditor);
            return;
        }

        if (string.Equals(workspace, "Quêtes", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_questJournalWindow, () => _questJournalWindow?.Open());
            return;
        }

        if (string.Equals(workspace, "Blueprints", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_blueprintCatalog, OpenBlueprintCatalog);
            return;
        }

        if (string.Equals(workspace, "Scripts", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_forgeScriptEditor, () => _forgeScriptEditor?.Open());
            return;
        }

        if (string.Equals(workspace, "Assets", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_assetBrowser, () => _assetBrowser?.Open());
            return;
        }

        if (string.Equals(workspace, "Données", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_contentPackageWindow, () => _contentPackageWindow?.Open());
            return;
        }

        if (string.Equals(workspace, "Validation", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_distributionProfiles, () => _distributionProfiles?.Open());
            return;
        }

        if (string.Equals(workspace, "Assistant", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_assistantWorkspace, () => _assistantWorkspace?.Open());
            return;
        }

        if (string.Equals(workspace, "Réseau", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_networkBetaWindow, () => _networkBetaWindow?.Open());
            return;
        }

        if (string.Equals(workspace, "Live Director", StringComparison.Ordinal))
        {
            OpenWorkspaceTool(_liveDirectorWindow, () => _liveDirectorWindow?.Open());
            return;
        }

        if (string.Equals(workspace, "Monde", StringComparison.Ordinal))
        {
            HideActiveWorkspaceTool();
            _statusLabel!.Text = "✓ Workspace Monde actif";
            _detailsLabel!.Text = "Explorateur, sélection, aperçu et diagnostics partagent le projet courant.";
            return;
        }

        if (string.Equals(workspace, "Playtests", StringComparison.Ordinal))
        {
            HideActiveWorkspaceTool();
            _statusLabel!.Text = "✓ Espace Playtests actif";
            _detailsLabel!.Text = "Choisissez un point de départ puis lancez le module depuis la barre inférieure.";
            return;
        }

        _statusLabel!.Text = $"Espace {workspace} actif";
        _detailsLabel!.Text = "Le contexte du projet et la sélection sont conservés entre les espaces.";
    }

    private void OpenWorkspaceTool(Window? window, Action open)
    {
        _toolWindowManager?.Open(window, open);
    }

    private void HideActiveWorkspaceTool()
    {
        _toolWindowManager?.HideActive();
    }

    private void HandleExplorerSelection(CreatorExplorerNode node)
    {
        var selection = node.ToSelection();
        if (selection is null)
        {
            return;
        }

        PublishSearchSelection(selection);
    }

    private void PublishSearchSelection(CreatorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (_history is not null && selection.Kind == CreatorSelectionKind.Area)
        {
            ActivateArea(AreaId.Parse(selection.Id));
        }
        else if (_history is not null && selection.Kind == CreatorSelectionKind.Entity)
        {
            var owner = _history.State.Project.Areas.FirstOrDefault(area =>
                area.Entities.Any(entity => entity.Id.ToString() == selection.Id));
            if (owner is not null)
            {
                ActivateArea(owner.Id);
            }
        }

        if (!_selectionContext.Replace(selection))
        {
            return;
        }

        if (selection.Kind == CreatorSelectionKind.Entity && _entityPicker is not null)
        {
            var entityIndex = _entityIds.FindIndex(id => id.ToString() == selection.Id);
            if (entityIndex >= 0)
            {
                _entityPicker.Selected = entityIndex;
                RefreshOverrideSummary();
            }
        }
    }

    private IReadOnlyList<CreatorDiagnostic> GetSearchDiagnostics() => _history is null
        ? []
        : CreatorDiagnostics.FromProject(
            _history.State.Project,
            $"revision:{_history.State.Revision}");

    private void HandleDiagnosticSelection(CreatorDiagnostic diagnostic)
    {
        var selection = diagnostic.Target ?? new CreatorSelection(
            CreatorSelectionKind.Diagnostic,
            diagnostic.Id,
            diagnostic.Code,
            diagnostic.DocumentPath);
        if (_selectionContext.Replace(selection))
        {
            _statusLabel!.Text = $"Diagnostic ciblé · {diagnostic.Code}";
            _detailsLabel!.Text =
                $"{diagnostic.Message}\n{diagnostic.DocumentPath}\n" +
                $"Propriété · {diagnostic.Property}\nSource · {diagnostic.Source}\nUUID · {diagnostic.Id}";
        }
    }

    private void HandleDiagnosticQuickFix(
        CreatorDiagnostic diagnostic,
        DiagnosticQuickFixDefinition quickFix,
        bool dryRun)
    {
        if (_history is null)
        {
            return;
        }

        var command = new ApplyDiagnosticQuickFixCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            diagnostic.DiagnosticId,
            quickFix.Id,
            dryRun);
        var result = _diagnosticQuickFixHandler.Handle(_history.State, command);
        if (dryRun)
        {
            _statusLabel!.Text = result.Succeeded
                ? $"Aperçu du correctif · {quickFix.Label}"
                : "✗ Prévisualisation refusée";
            _detailsLabel!.Text = result.Succeeded
                ? result.Patch?.Summary ?? quickFix.Description
                : string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} — {item.Message}"));
            return;
        }

        ApplyCommand(
            result,
            $"Correctif appliqué · {quickFix.Label}",
            quickFix.EntityId);
    }

    private void HandleDiagnosticExport(IReadOnlyList<CreatorDiagnostic> diagnostics)
    {
        if (_projectSession is null || _creatorShell is null) return;
        try
        {
            var exported = CreatorDiagnosticBundleExporter.Export(
                _projectSession.RootDirectory,
                diagnostics,
                _playtestLog?.Text);
            var message =
                $"✓ Bundle exporté · {exported.DiagnosticCount} diagnostic(s) · {exported.RelativePath}";
            _creatorShell.SetDiagnosticExportStatus(message, failed: false);
            if (_statusLabel is not null) _statusLabel.Text = "✓ Diagnostics exportés";
            if (_detailsLabel is not null)
                _detailsLabel.Text = $"{exported.RelativePath}\nSHA-256 · {exported.Sha256}";
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_DIAGNOSTICS_EXPORTED count={exported.DiagnosticCount} " +
                $"runtime={exported.IncludesRuntimeLog.ToString().ToLowerInvariant()} " +
                $"reused={exported.Reused.ToString().ToLowerInvariant()} sha256={exported.Sha256}");
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            _creatorShell.SetDiagnosticExportStatus($"✕ Export impossible · {exception.Message}", failed: true);
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_DIAGNOSTICS_EXPORT_FAILED {exception.Message}");
        }
    }

    private void HandleSelectionLockRequest(bool locked) => _selectionContext.SetLocked(locked);

    private void HandleSharedSelectionChanged()
    {
        _creatorShell?.SetSelection(_selectionContext.Current, _selectionContext.IsLocked);
        if (_history is not null && _selectionContext.Current.Count > 1)
        {
            _creatorShell?.SetSelectionInspection(CreatorSelectionInspector.Inspect(
                _history.State.Project, _selectionContext.Current, "creator-shell-inspector"));
        }
        var entitySelection = _selectionContext.Current.Count == 1 &&
            _selectionContext.Current[0].Kind == CreatorSelectionKind.Entity
                ? _selectionContext.Current[0]
                : null;
        if (entitySelection is null || _entityPicker is null)
        {
            return;
        }

        var entityIndex = _entityIds.FindIndex(id => id.ToString() == entitySelection.Id);
        if (entityIndex >= 0 && _entityPicker.Selected != entityIndex)
        {
            _entityPicker.Selected = entityIndex;
            RefreshOverrideSummary();
        }
    }

    private void HandleActiveAreaChanged()
    {
        if (_history is null)
        {
            return;
        }

        try
        {
            RebuildPreview();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_AREA_PREVIEW_FAILED {exception.Message}");
        }
        RefreshEditorControls();
        _projectPreview?.FrameActiveArea();
        _areaWorkspace?.Refresh();
    }

    private void ShowProjectPicker()
    {
        if (_projectDialog is null)
        {
            return;
        }

        _projectDialog.CurrentDir = _projectSession?.RootDirectory ?? GetDefaultWorkspaceDirectory();
        _projectDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_projectDialog);
    }

    private void ShowExportDialog()
    {
        if (_exportDialog is null || _projectSession is null || _history is null)
        {
            return;
        }

        _exportDialog.CurrentDir = Path.GetDirectoryName(_projectSession.RootDirectory)
            ?? _projectSession.RootDirectory;
        _exportDialog.CurrentFile = $"{_history.State.Project.Module.Slug}-0.1.0.orpmod";
        _exportDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_exportDialog);
    }

    private void ExportModule(string destinationPath)
    {
        if (_projectSession is null || _history is null || _statusLabel is null ||
            _detailsLabel is null || _exportButton is null)
        {
            return;
        }

        if (!string.Equals(Path.GetExtension(destinationPath), ".orpmod", StringComparison.OrdinalIgnoreCase))
        {
            destinationPath += ".orpmod";
        }

        _exportButton.Disabled = true;
        try
        {
            var saved = _projectSession.Save(_history.State.Project);
            if (!saved.Succeeded)
            {
                _statusLabel.Text = "✕ Export interrompu";
                _detailsLabel.Text = FormatDiagnostics(saved.Diagnostics);
                return;
            }

            var exported = ModulePackager.Export(
                _projectSession.RootDirectory,
                destinationPath,
                target: _distributionTarget);
            _statusLabel.Text = "✓ Module exporté et validé";
            _detailsLabel.Text =
                $"{exported.Manifest.Slug} · cible {exported.Manifest.Target} · " +
                $"{exported.FileCount} fichiers · {exported.ArchiveBytes / 1024d:0.0} Kio · " +
                $"{exported.BuildPlan.RebuiltResources.Count} recalculé(s) · " +
                $"{exported.BuildPlan.ReusedResources.Count} réutilisé(s)";
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_MODULE_EXPORTED slug={exported.Manifest.Slug} " +
                $"files={exported.FileCount} bytes={exported.ArchiveBytes} " +
                $"rebuilt={exported.BuildPlan.RebuiltResources.Count} " +
                $"reused={exported.BuildPlan.ReusedResources.Count} " +
                $"dependencies={exported.Manifest.Dependencies.Count} " +
                $"target={exported.Manifest.Target.ToString().ToLowerInvariant()} notices=generated");
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or FileNotFoundException or
            InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _statusLabel.Text = "✕ Échec de l’export";
            _detailsLabel.Text = exception.Message;
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_MODULE_EXPORT_FAILED {exception.Message}");
        }
        finally
        {
            _exportButton.Disabled = false;
        }
    }

    private void ShowGameDistributionDialog()
    {
        if (_gameDistributionDialog is null || _projectSession is null || _history is null)
        {
            return;
        }

        if (!_history.State.Project.Module.Distribution.Launcher.Enabled)
        {
            ShowEditStatus("Activez et enregistrez le profil de lanceur avant de créer la distribution.");
            return;
        }

        _gameDistributionDialog.CurrentDir = Path.GetDirectoryName(_projectSession.RootDirectory)
            ?? _projectSession.RootDirectory;
        _gameDistributionDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_gameDistributionDialog);
    }

    private void ExportGameDistribution(string parentDirectory)
    {
        if (_projectSession is null || _history is null || _statusLabel is null || _detailsLabel is null)
        {
            return;
        }

        var launcher = _history.State.Project.Module.Distribution.Launcher;
        if (!launcher.Enabled)
        {
            _statusLabel.Text = "✕ Lanceur désactivé";
            _detailsLabel.Text = "Activez et enregistrez le profil de lanceur dans Validation & distribution.";
            return;
        }

        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "game-launcher-export",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = _projectSession.Save(_history.State.Project);
            if (!saved.Succeeded)
            {
                _statusLabel.Text = "✕ Création du lanceur interrompue";
                _detailsLabel.Text = FormatDiagnostics(saved.Diagnostics);
                return;
            }

            Directory.CreateDirectory(temporaryRoot);
            var packagePath = Path.Combine(temporaryRoot, $"{_history.State.Project.Module.Slug}.orpmod");
            var exported = ModulePackager.Export(
                _projectSession.RootDirectory,
                packagePath,
                target: ModuleTargetKind.Client);
            var template = FindGameLauncherTemplateDirectory();
            var destination = Path.Combine(
                Path.GetFullPath(parentDirectory),
                $"{launcher.ExecutableName}-windows-x64");
            var bundle = GameLauncherBundleBuilder.Build(packagePath, template, destination, launcher);
            _statusLabel.Text = "✓ Jeu et lanceur créés";
            _detailsLabel.Text =
                $"{bundle.Manifest.DisplayName} · {bundle.FileCount} fichiers · " +
                $"{bundle.TotalBytes / 1024d / 1024d:0.0} Mio · {bundle.OutputDirectory}";
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_GAME_LAUNCHER_EXPORTED slug={exported.Manifest.Slug} " +
                $"target=client files={bundle.FileCount} bytes={bundle.TotalBytes} " +
                $"multiplayer={launcher.Multiplayer.ToString().ToLowerInvariant()}");
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or
                                           FileNotFoundException or InvalidDataException or InvalidOperationException or
                                           IOException or UnauthorizedAccessException)
        {
            _statusLabel.Text = "✕ Échec de la création du lanceur";
            _detailsLabel.Text = exception.Message;
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_GAME_LAUNCHER_EXPORT_FAILED {exception.Message}");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static string FindGameLauncherTemplateDirectory()
    {
        var configured = System.Environment.GetEnvironmentVariable("OPENRPG_FORGE_GAME_LAUNCHER_TEMPLATE");
        var toolsetRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        var candidates = new[]
        {
            configured,
            Path.GetFullPath(Path.Combine(toolsetRoot, "..", "..", "templates", "game-windows-x64")),
            Path.GetFullPath(Path.Combine(toolsetRoot, "..", "..", "artifacts", "templates", "game-windows-x64")),
        };
        foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
        {
            var fullPath = Path.GetFullPath(candidate!);
            if (File.Exists(Path.Combine(fullPath, GameLauncherBundleBuilder.TemplateExecutableName)) &&
                File.Exists(Path.Combine(fullPath,
                    GameLauncherBundleBuilder.RuntimeExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar))))
            {
                return fullPath;
            }
        }

        throw new DirectoryNotFoundException(
            "Le modèle de lanceur Windows est absent. Construisez-le avec " +
            "scripts\\Build-GameLauncherTemplate.ps1 -Force, puis recommencez.");
    }

    private void OpenDialogueEditor() => OpenTool(_dialogueEditor, () => _dialogueEditor?.Open());

    private void OpenBlueprintCatalog() => OpenTool(_blueprintCatalog, () => _blueprintCatalog?.Open());

    private void OpenAreaWorkspace() => OpenTool(_areaWorkspace, () => _areaWorkspace?.Open());

    private void OpenCreatorWorkspace() => OpenTool(_workspaceWindow, () => _workspaceWindow?.Open());

    private void OpenRecoveryWindow() => OpenTool(_recoveryWindow, () => _recoveryWindow?.Open());

    private void OpenQualityLab() => OpenTool(_qualityLabWindow, () => _qualityLabWindow?.Open());

    private void OpenTool(Window? window, Action open) => _toolWindowManager?.Open(window, open);

    private IReadOnlyList<RecoverySnapshotInfo> ListRecoverySnapshots()
    {
        if (_projectSession is null) return [];
        try
        {
            var quarantine = _history?.State.Project.Module.RuntimePolicies.Extensions.OperationalReadiness
                .Recovery.QuarantineCorruptFiles == true;
            var inspection = _recoveryStore.Inspect(_projectSession.RootDirectory, quarantine);
            foreach (var item in inspection.Where(item => item.Status == RecoverySnapshotStatus.Quarantined))
                GD.Print($"OPENRPG_FORGE_RECOVERY_QUARANTINED code={item.Code} file={Path.GetFileName(item.QuarantinePath)}");
            return _recoveryStore.List(_projectSession.RootDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
        {
            GD.PrintErr($"OPENRPG_FORGE_RECOVERY_LIST_FAILED {exception.Message}"); return [];
        }
    }

    private void CaptureRecoverySnapshot()
    {
        if (_projectSession is null || _history is null || _history.State.Revision.Value == _lastSavedRevision ||
            _history.State.Revision.Value == _lastRecoveryRevision) return;
        try
        {
            var snapshot = _recoveryStore.Capture(_projectSession.RootDirectory, _history.State.Project,
                _history.State.Revision.Value, DateTimeOffset.UtcNow);
            _lastRecoveryRevision = _history.State.Revision.Value;
            GD.Print($"OPENRPG_FORGE_AUTOSAVE_CAPTURED revision={snapshot.Revision} hash={snapshot.ProjectSha256[..12]}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { GD.PrintErr($"OPENRPG_FORGE_AUTOSAVE_FAILED {exception.Message}"); }
    }

    private CrashRecoveryPolicy LoadRecoveryPolicy() =>
        _history?.State.Project.Module.RuntimePolicies.Extensions.OperationalReadiness.Recovery ??
        OperationalReadinessProfile.CreateDefault(
            Guid.Parse("e0600000-0000-4000-8000-000000000001")).Recovery;

    private bool ConfigureRecoveryPolicy(CrashRecoveryPolicy policy)
    {
        if (_history is null) return false;
        var extensions = _history.State.Project.Module.RuntimePolicies.Extensions;
        var readiness = extensions.OperationalReadiness;
        var updatedReadiness = new OperationalReadinessProfile(readiness.Id, readiness.SchemaVersion,
            readiness.TaskManifests, readiness.CrashArtifacts, readiness.ProjectHealth, policy,
            readiness.ContentSynchronization, "creator-r060-ui");
        var updated = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
            extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments, extensions.SnapshotPolicy,
            "creator-r060-ui", extensions.ModernTooling, updatedReadiness, extensions.PostLaunch);
        if (!ApplyRuntimeExtensions(updated)) return false;
        _recoveryStore = new RecoverySnapshotStore(policy.RetainedSnapshots);
        return true;
    }

    private RecoveryRestorePreview? PreviewRecoverySnapshot(RecoverySnapshotInfo snapshot,
        RecoveryRestoreSelection selection)
    {
        if (_history is null) return null;
        try
        {
            var restored = _recoveryStore.Restore(snapshot);
            var command = new RestoreRecoverySnapshotCommand(CommandId.From(Guid.NewGuid()),
                _history.State.Revision, restored, selection, true);
            if (!_restoreRecoverySnapshotHandler.Handle(_history.State, command).Succeeded) return null;
            return _recoveryStore.PreviewRestore(snapshot, _history.State.Project, selection);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or
                                          System.Text.Json.JsonException or NotSupportedException or
                                          InvalidOperationException or ArgumentException)
        {
            GD.PrintErr($"OPENRPG_FORGE_RECOVERY_PREVIEW_FAILED {exception.Message}");
            return null;
        }
    }

    private RecoveryComparison? RestoreRecoverySnapshot(RecoverySnapshotInfo snapshot,
        RecoveryRestoreSelection selection)
    {
        if (_history is null) return null;
        var preview = PreviewRecoverySnapshot(snapshot, selection);
        if (preview is null) return null;
        try
        {
            var restored = _recoveryStore.Restore(snapshot);
            var dryRun = new RestoreRecoverySnapshotCommand(CommandId.From(Guid.NewGuid()),
                _history.State.Revision, restored, selection, true);
            if (!_restoreRecoverySnapshotHandler.Handle(_history.State, dryRun).Succeeded) return null;
            var apply = dryRun with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _restoreRecoverySnapshotHandler.Handle(_history.State, apply);
            if (!ApplyCommand(result, "Instantané restauré en mémoire par transaction.", null)) return null;
            _activeAreaContext.EnsureValid(result.State.Project);
            RebuildPreview();
            RefreshEditorControls();
            _statusLabel!.Text = "✓ Instantané restauré en mémoire · undo/redo disponible";
            _detailsLabel!.Text =
                $"Avant/après comparé · {preview.Comparison.CurrentSha256[..8]}→{preview.Comparison.SnapshotSha256[..8]} · Enregistrer pour confirmer";
            return preview.Comparison;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or
                                          System.Text.Json.JsonException or NotSupportedException or
                                          InvalidOperationException or ArgumentException)
        {
            GD.PrintErr($"OPENRPG_FORGE_RECOVERY_RESTORE_FAILED {exception.Message}");
            return null;
        }
    }

    private void ClearRecoverySnapshots()
    {
        if (_projectSession is null) return;
        try { _recoveryStore.Clear(_projectSession.RootDirectory); _lastRecoveryRevision = -1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { GD.PrintErr($"OPENRPG_FORGE_RECOVERY_CLEAR_FAILED {exception.Message}"); }
    }

    private void EndRecoverySession()
    {
        if (_projectSession is null || _recoverySessionId == Guid.Empty) return;
        try
        {
            _ = _recoverySessionStore.End(_projectSession.RootDirectory, _recoverySessionId,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          System.Text.Json.JsonException)
        {
            GD.PrintErr($"OPENRPG_FORGE_RECOVERY_SESSION_CLOSE_FAILED {exception.Message}");
        }
        _recoverySessionId = Guid.Empty;
        _recoverySessionInspection = null;
    }

    private int ApplyWorkspaceFilter(EditorDisplayFilter filter)
    {
        if (_projectPreview is null) return 0;
        _projectPreview.SetDisplayFilter(filter);
        return _projectPreview.ApplyDisplayFilter();
    }

    private EditorWorkspaceProfile LoadWorkspaceProfile()
    {
        var areaId = _activeAreaContext.Current;
        if (_projectSession is null) return EditorWorkspaceProfile.CreateDefault(EditorRole.Beginner, areaId);
        try
        {
            var loaded = EditorWorkspaceProfileStore.TryLoad(_projectSession.RootDirectory) ??
                         EditorWorkspaceProfile.CreateDefault(EditorRole.Beginner, areaId);
            var availableAreas = _history?.State.Project.Areas.Select(area => area.Id).ToArray() ?? [];
            return EditorWorkspaceProfilePlanner.ReconcileAreas(loaded, availableAreas, areaId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidDataException or ArgumentException or
                                           System.Text.Json.JsonException)
        {
            GD.PrintErr($"OPENRPG_FORGE_WORKSPACE_PROFILE_LOAD_FAILED {exception.Message}");
            return EditorWorkspaceProfile.CreateDefault(EditorRole.Beginner, areaId);
        }
    }

    private void SaveWorkspaceProfile(EditorWorkspaceProfile profile)
    {
        if (_projectSession is null) return;
        EditorWorkspaceProfileStore.Save(_projectSession.RootDirectory, profile);
        _projectPreview?.SetDisplayFilter(profile.DisplayFilter);
        _projectPreview?.SetEditorMode(profile.Shell.ViewportMode);
        _creatorShell?.ApplyWorkspaceProfile(profile);
        ApplyEditorExperience(profile);
        RefreshCreatorShell();
        GD.Print("OPENRPG_FORGE_WORKSPACE_PROFILE_SAVED");
    }

    private void RecordRecentBlueprint(BlueprintId blueprintId)
    {
        if (_projectSession is null) return;
        SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.RecordRecent(LoadWorkspaceProfile(), blueprintId));
    }

    private bool TogglePersonalFavorite(BlueprintId blueprintId)
    {
        if (_projectSession is null) return false;
        var updated = EditorWorkspaceProfilePlanner.TogglePersonalFavorite(LoadWorkspaceProfile(), blueprintId);
        SaveWorkspaceProfile(updated);
        return updated.PersonalFavoriteBlueprintIds.Contains(blueprintId);
    }

    private void ApplyEditorExperience(EditorWorkspaceProfile profile)
    {
        if (_mainPanel is null || _history is null)
        {
            return;
        }

        var theme = new Theme
        {
            DefaultBaseScale = (float)profile.UiScale,
            DefaultFontSize = Math.Max(11, (int)Math.Round(14 * profile.TextScale)),
        };
        var font = new SystemFont
        {
            FontNames = new[] { profile.PrimaryFontFamily }.Concat(profile.FallbackFontFamilies).ToArray(),
        };
        theme.DefaultFont = font;
        _mainPanel.Theme = theme;
        _projectPreview?.SetDisplayFilter(profile.DisplayFilter);
        _creatorShell?.SetAreaTabs(_history.State.Project, profile);
        if (profile.ActiveAreaId is not null)
        {
            ActivateArea(profile.ActiveAreaId);
        }

        GD.Print($"OPENRPG_FORGE_EDITOR_EXPERIENCE_APPLIED ui={profile.UiScale:0.##} text={profile.TextScale:0.##} font={profile.PrimaryFontFamily} tabs={profile.OpenAreaTabs.Count}");
    }

    private bool PlaceRepeatedSeries(int count, float spacing, PlacementDistribution distribution, int seed)
    {
        if (_history is null || _blueprintPalette is null || _positionX is null || _positionZ is null ||
            _blueprintPalette.Selected < 0 || _blueprintPalette.Selected >= _paletteBlueprintIds.Count) return false;
        var area = GetActiveArea();
        var positions = RepeatedPlacementPlanner.Plan(new(
            new Vector3Value(SnapToGrid(_positionX.Value), 0, SnapToGrid(_positionZ.Value)),
            count, spacing, distribution, Seed: seed));
        var blueprintId = _paletteBlueprintIds[_blueprintPalette.Selected];
        var sources = positions.Select(position => new EntityInstance(
            EntityId.From(Guid.NewGuid()), blueprintId,
            new TransformValue(position, QuaternionValue.Identity, Vector3Value.One))).ToArray();
        var payload = new EntityClipboardPayload(area.Id, sources);
        var mapping = sources.ToDictionary(entity => entity.Id, _ => EntityId.From(Guid.NewGuid()));
        var command = new PasteEntitiesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            area.Id, payload, mapping, Vector3Value.Zero, DryRun: false);
        return ApplyCommand(_pasteEntitiesHandler.Handle(_history.State, command),
            $"{count} entités placées en série ({distribution}).", mapping[sources[0].Id]);
    }

    private bool PlaceRepeatedSeriesAdvanced(EditorRepeatedPlacementProfile profile, int count)
    {
        if (_history is null || _blueprintPalette is null || _positionX is null || _positionZ is null ||
            _blueprintPalette.Selected < 0 || _blueprintPalette.Selected >= _paletteBlueprintIds.Count) return false;
        RepeatedPlacementPlan plan;
        try
        {
            plan = RepeatedPlacementPlanner.Plan(profile,
                new Vector3Value(SnapToGrid(_positionX.Value), 0, SnapToGrid(_positionZ.Value)), count,
                position => new PlacementSurfaceSample(new Vector3Value(position.X, 0, position.Z),
                    new Vector3Value(0, 1, 0)));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_R059_PLACEMENT_FAILED {exception.Message}");
            return false;
        }
        var area = GetActiveArea();
        var blueprintId = _paletteBlueprintIds[_blueprintPalette.Selected];
        var sources = plan.Poses.Select(pose => new EntityInstance(EntityId.From(Guid.NewGuid()), blueprintId,
            new TransformValue(pose.Position, QuaternionValue.Identity, Vector3Value.One))).ToArray();
        var payload = new EntityClipboardPayload(area.Id, sources, provenance: profile.Provenance);
        var mapping = sources.ToDictionary(entity => entity.Id, _ => EntityId.From(Guid.NewGuid()));
        var command = new PasteEntitiesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            area.Id, payload, mapping, Vector3Value.Zero, DryRun: false);
        return ApplyCommand(_pasteEntitiesHandler.Handle(_history.State, command),
            $"{count} entités placées ({profile.Distribution}, {profile.Alignment}).", mapping[sources[0].Id]);
    }

    private bool CopySelectionToComparisonArea(AreaId sourceAreaId, AreaId targetAreaId)
    {
        if (_history is null || _activeAreaContext.Current != sourceAreaId) return false;
        var selected = GetSelectedEntityIdsForActiveArea();
        if (selected.Count == 0) return false;
        var request = new CrossAreaCopyRequest(Guid.NewGuid(), CrossAreaCopyRequest.CurrentSchemaVersion,
            "creator-r059-ui", sourceAreaId, targetAreaId, selected, new Vector3Value(1, 0, 1));
        var result = new CrossAreaCopyService(new SystemClock()).Copy(_history.State, request);
        return ApplyCommand(result, $"{selected.Count} entité(s) copiée(s) vers la zone de comparaison.", null);
    }


}
#endif
