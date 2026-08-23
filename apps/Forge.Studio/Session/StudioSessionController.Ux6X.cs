using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Authoring;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6XSelectionCode = "studio.runtime.ui.selection_required";
    private const string Ux6XSourceCode = "studio.runtime.ui.source_changed";
    private const string Ux6XProfileCode = "studio.runtime.ui.profile_invalid";
    private const string Ux6XProvenance = "studio:ux6-x:runtime-ui";
    private const string Ux6XBoundary =
        "Aperçu copié uniquement · aucun HUD, police, viewport, manette, GPU ou nœud runtime n’est modifié.";

    private readonly SetGameplayControlProfileHandler _setGameplayUx6X = new(new SystemClock());
    private StudioUx6XDraft? _approvedRuntimeUiUx6X;
    private ProjectRevision? _approvedRuntimeUiRevisionUx6X;
    private string? _approvedRuntimeUiFingerprintUx6X;

    internal IReadOnlyList<StudioUx6XFontGovernance> FontGovernanceUx6X
    {
        get
        {
            var governed = AssetManifests.Where(item => item.Font is not null)
                .SelectMany(item => new[]
                {
                    new StudioUx6XFontGovernance(item.Font!.PrimaryFamily, true,
                        item.Font.ShapingScripts, item.Font.LocalizationTestLocales, item.Font.DpiBuckets,
                        item.Font.MinimumTextScale, item.Font.MaximumTextScale),
                }.Concat(item.Font.Fallbacks.Select(fallback => new StudioUx6XFontGovernance(
                    fallback.Family, true, item.Font.ShapingScripts, item.Font.LocalizationTestLocales,
                    item.Font.DpiBuckets, item.Font.MinimumTextScale, item.Font.MaximumTextScale))))
                .GroupBy(item => item.Family, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Family, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (_workspaceProfileHistory is null) return governed;
            foreach (var family in new[] { WorkspaceProfile.PrimaryFontFamily }
                         .Concat(WorkspaceProfile.FallbackFontFamilies))
            {
                if (governed.All(item => !string.Equals(item.Family, family, StringComparison.OrdinalIgnoreCase)))
                    governed.Add(new(family, false, [], [], [],
                        (decimal)EditorWorkspaceProfile.MinimumTextScale,
                        (decimal)EditorWorkspaceProfile.MaximumTextScale));
            }
            return governed.OrderBy(item => item.Family, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }

    internal StudioUx6XDraft? CreateRuntimeUiDraftUx6X(bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null || _workspaceProfileHistory is null)
            return null;
        var sourceGameplay = project.Module.RuntimePolicies.Gameplay;
        var shell = sourceGameplay.Session.Shell;
        var workspace = WorkspaceProfile;
        return new(history.State.Revision, ProjectFingerprintUx6X(project), WorkspaceFingerprintUx6X(workspace),
            sourceGameplay, workspace,
            new(shell.Enabled, shell.RequiredAtRuntime, shell.KitId, shell.SkinId, shell.LayoutDocument,
                shell.Scale, shell.MapVisible, shell.JournalVisible, shell.HudVisible, shell.InputAgnostic,
                shell.Scalable, shell.InputActions.ToArray()),
            new(workspace.UiScale, workspace.TextScale, workspace.PrimaryFontFamily,
                workspace.FallbackFontFamilies.ToArray()));
    }

    internal StudioUx6XPreview PreviewRuntimeUiUx6X(StudioUx6XDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelRuntimeUiApprovalUx6X();
        if (!TryGetCurrentUx6X(draft, out var history, out var project, out var failure)) return failure!;
        if (!TryBuildCandidatesUx6X(draft, out var gameplay, out var workspace, out var diagnostic))
            return FailedPreviewUx6X("La configuration HUD ou typographique est invalide.", diagnostic!);

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history!.State.Revision, gameplay!, DryRun: true);
        var projectDryRun = _setGameplayUx6X.Handle(history.State, command);
        if (!projectDryRun.Succeeded) return FailedCommandUx6X(projectDryRun);
        var workspaceDryRun = PreviewWorkspaceProfile(workspace!);
        if (!workspaceDryRun.Succeeded)
            return FailedPreviewUx6X(workspaceDryRun.Message,
                new(Ux6XProfileCode, "editorWorkspace/typography", workspaceDryRun.Message));

        var diff = BuildDiffUx6X(draft.SourceGameplay.Session.Shell, gameplay!.Session.Shell,
            draft.SourceWorkspace, workspace!);
        var results = BuildRuntimePreviewUx6X(gameplay.Session.Shell, workspace!);
        if (diff.Count > 0)
        {
            _approvedRuntimeUiUx6X = FreezeDraftUx6X(draft);
            _approvedRuntimeUiRevisionUx6X = history.State.Revision;
            _approvedRuntimeUiFingerprintUx6X = CandidateFingerprintUx6X(draft);
        }
        return new(true, diff.Count > 0, true, true,
            diff.Count > 0
                ? "HUD runtime et typographie prêts à appliquer dans deux historiques explicites."
                : "Aperçu sec valide · la configuration est déjà identique.",
            diff, [], results, true, true, true);
    }

    internal StudioUx6XActionResult ApplyApprovedRuntimeUiUx6X(StudioUx6XDraft currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var approved = _approvedRuntimeUiUx6X;
        var history = History;
        var project = Project;
        if (approved is null || history is null || project is null || _workspaceProfileHistory is null ||
            _approvedRuntimeUiRevisionUx6X != history.State.Revision ||
            approved.BaseRevision != history.State.Revision || currentDraft.BaseRevision != history.State.Revision ||
            !string.Equals(approved.SourceProjectFingerprint, ProjectFingerprintUx6X(project), StringComparison.Ordinal) ||
            !string.Equals(approved.SourceWorkspaceFingerprint, WorkspaceFingerprintUx6X(WorkspaceProfile),
                StringComparison.Ordinal) ||
            !string.Equals(_approvedRuntimeUiFingerprintUx6X, CandidateFingerprintUx6X(currentDraft),
                StringComparison.Ordinal) ||
            !TryBuildCandidatesUx6X(approved, out var gameplay, out var workspace, out _))
        {
            CancelRuntimeUiApprovalUx6X();
            return new(false, "Le projet, le profil utilisateur ou le brouillon a changé · relancez l’aperçu sec.");
        }

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, gameplay!, DryRun: true);
        var projectDryRun = _setGameplayUx6X.Handle(history.State, command);
        var workspaceDryRun = PreviewWorkspaceProfile(workspace!);
        if (!projectDryRun.Succeeded || !workspaceDryRun.Succeeded)
        {
            CancelRuntimeUiApprovalUx6X();
            return !projectDryRun.Succeeded
                ? new(false, HumanCommandFailureUx6X(projectDryRun))
                : new(false, workspaceDryRun.Message);
        }

        var applied = _setGameplayUx6X.Handle(history.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            CancelRuntimeUiApprovalUx6X();
            return new(false, HumanCommandFailureUx6X(applied));
        }
        history.Record(applied);
        var workspaceApplied = ApplyWorkspaceProfile(workspace!);
        CancelRuntimeUiApprovalUx6X();
        if (!workspaceApplied.Succeeded)
        {
            _ = Undo();
            return new(false, "Le profil utilisateur a été refusé ; la mutation du projet a été annulée.");
        }
        InvalidateContentIndexes();
        return new(true, "HUD runtime appliqué, puis typographie appliquée dans un second historique annulable.");
    }

    internal StudioUx6XActionResult UndoRuntimeUiUx6X()
    {
        CancelRuntimeUiApprovalUx6X();
        var workspace = UndoWorkspaceProfile(false);
        if (!workspace.Succeeded) return new(false, workspace.Message);
        var project = Undo();
        if (!project.Succeeded)
        {
            _ = RedoWorkspaceProfile(false);
            return new(false, project.Message);
        }
        InvalidateContentIndexes();
        return new(true, "HUD et typographie annulés dans leurs deux historiques.");
    }

    internal StudioUx6XActionResult RedoRuntimeUiUx6X()
    {
        CancelRuntimeUiApprovalUx6X();
        var project = Redo();
        if (!project.Succeeded) return new(false, project.Message);
        var workspace = RedoWorkspaceProfile(false);
        if (!workspace.Succeeded)
        {
            _ = Undo();
            return new(false, workspace.Message);
        }
        InvalidateContentIndexes();
        return new(true, "HUD et typographie rétablis dans leurs deux historiques.");
    }

    internal StudioUx6XActionResult SaveRuntimeUiUx6X()
    {
        var project = Save();
        if (!project.Succeeded) return new(false, project.Message);
        var workspace = SaveWorkspaceProfile();
        return new(workspace.Succeeded, workspace.Succeeded
            ? "Projet et profil typographique enregistrés."
            : workspace.Message);
    }

    internal StudioUx6XApprovalSnapshot CaptureRuntimeUiApprovalUx6X() =>
        new(_approvedRuntimeUiUx6X, _approvedRuntimeUiRevisionUx6X, _approvedRuntimeUiFingerprintUx6X);

    internal void RestoreRuntimeUiApprovalUx6X(StudioUx6XApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedRuntimeUiUx6X = snapshot.Draft;
        _approvedRuntimeUiRevisionUx6X = snapshot.Revision;
        _approvedRuntimeUiFingerprintUx6X = snapshot.CandidateFingerprint;
    }

    internal void CancelRuntimeUiApprovalUx6X()
    {
        _approvedRuntimeUiUx6X = null;
        _approvedRuntimeUiRevisionUx6X = null;
        _approvedRuntimeUiFingerprintUx6X = null;
    }

    internal void CloseUx6X() => CancelRuntimeUiApprovalUx6X();

    internal string RuntimeUiDraftFingerprintUx6X(StudioUx6XDraft draft) => CandidateFingerprintUx6X(draft);

    internal StudioUx6XSmokeResult RunUx6XContractSmoke()
    {
        var source = Project;
        var root = string.Empty;
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var cleanup = true;
        try
        {
            var realReadable = source is not null && _workspaceProfileHistory is not null &&
                               GameplaySessionProfileValidator.Validate(
                                   source.Module.RuntimePolicies.Gameplay.Session).Count == 0;
            var explicitSelection = CreateRuntimeUiDraftUx6X(false) is null;
            var initial = CreateRuntimeUiDraftUx6X(true);
            if (initial is null) return FailedSmokeUx6X(explicitSelection, realReadable, cleanup);

            var invalidShell = PreviewRuntimeUiUx6X(initial with
            {
                RuntimeShell = initial.RuntimeShell with { Scale = 8 },
            });
            var invalidTypography = PreviewRuntimeUiUx6X(initial with
            {
                Typography = initial.Typography with { UiScale = 4 },
            });
            var invalidConfiguration = !invalidShell.Succeeded && !invalidTypography.Succeeded;
            var changed = ChangedRuntimeUiDraftUx6X(initial);
            var beforeProject = Project;
            var beforeWorkspace = WorkspaceProfile;
            var beforeRevision = History!.State.Revision;
            var preview = PreviewRuntimeUiUx6X(changed);
            var projectDryRun = preview.Succeeded && preview.ProjectDryRun && ReferenceEquals(beforeProject, Project) &&
                                History.State.Revision == beforeRevision;
            var workspaceDryRun = preview.Succeeded && preview.WorkspaceDryRun &&
                                  ReferenceEquals(beforeWorkspace, WorkspaceProfile);
            var runtimeResolved = preview.RuntimeShellResolved;
            var typographyResolved = preview.TypographyResolved;
            var human = !ContainsUuidUx6X(string.Join(' ', preview.Diff.Concat(preview.Results))) &&
                        preview.Results.Contains(Ux6XBoundary);
            var bounded = preview.RuntimeClaimsBounded;
            var staleSource = !PreviewRuntimeUiUx6X(changed with
            {
                BaseRevision = changed.BaseRevision.Next(),
            }).Succeeded;
            _ = PreviewRuntimeUiUx6X(changed);
            var staleApproval = !ApplyApprovedRuntimeUiUx6X(changed with
            {
                Typography = changed.Typography with { TextScale = changed.Typography.TextScale + .05 },
            }).Succeeded;

            root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-x-{Guid.NewGuid():N}");
            if (!ProjectSession.Create(root, source!).Succeeded)
                return FailedSmokeUx6X(explicitSelection, realReadable, cleanup);
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded)
                return FailedSmokeUx6X(explicitSelection, realReadable, cleanup);
            var sourceProject = editing.Project!;
            var sourceGameplay = sourceProject.Module.RuntimePolicies.Gameplay;
            var sourceWorkspace = editing.WorkspaceProfile;
            var draft = ChangedRuntimeUiDraftUx6X(editing.CreateRuntimeUiDraftUx6X(true)!);
            var freshPreview = editing.PreviewRuntimeUiUx6X(draft);
            var apply = freshPreview.Succeeded && editing.ApplyApprovedRuntimeUiUx6X(draft).Succeeded;
            var finalProject = editing.Project!;
            var finalGameplay = finalProject.Module.RuntimePolicies.Gameplay;
            var finalWorkspace = editing.WorkspaceProfile;
            var canonicalProject = apply && !ReferenceEquals(sourceGameplay.Session.Shell,
                finalGameplay.Session.Shell) && finalGameplay.Session.Shell.SkinId == draft.RuntimeShell.SkinId;
            var canonicalWorkspace = apply && finalWorkspace.PrimaryFontFamily ==
                draft.Typography.PrimaryFontFamily && finalWorkspace.TextScale == draft.Typography.TextScale;
            var gameplayNeighbors = apply && ReferenceEquals(sourceGameplay.Swimming, finalGameplay.Swimming) &&
                                    ReferenceEquals(sourceGameplay.Climbing, finalGameplay.Climbing) &&
                                    ReferenceEquals(sourceGameplay.RadialMenu, finalGameplay.RadialMenu) &&
                                    ReferenceEquals(sourceGameplay.TimeControl, finalGameplay.TimeControl) &&
                                    ReferenceEquals(sourceGameplay.CombatModes, finalGameplay.CombatModes) &&
                                    ReferenceEquals(sourceGameplay.Foundation, finalGameplay.Foundation) &&
                                    ReferenceEquals(sourceGameplay.Simulation, finalGameplay.Simulation) &&
                                    ReferenceEquals(sourceGameplay.Session.Persistence,
                                        finalGameplay.Session.Persistence) &&
                                    ReferenceEquals(sourceGameplay.Session.Checkpoints,
                                        finalGameplay.Session.Checkpoints) &&
                                    ReferenceEquals(sourceGameplay.Session.ControlLocks,
                                        finalGameplay.Session.ControlLocks) &&
                                    ReferenceEquals(sourceGameplay.Session.Replay, finalGameplay.Session.Replay);
            var workspaceNeighbors = apply && WorkspaceNeighborsUx6X(sourceWorkspace, finalWorkspace);
            var projectNeighbors = apply && sourceProject.Areas.SequenceEqual(finalProject.Areas) &&
                                   sourceProject.Blueprints.SequenceEqual(finalProject.Blueprints) &&
                                   sourceProject.Dialogues.SequenceEqual(finalProject.Dialogues) &&
                                   sourceProject.Quests.SequenceEqual(finalProject.Quests) &&
                                   ReferenceEquals(sourceProject.Module.WorldFeatures,
                                       finalProject.Module.WorldFeatures);
            var appliedProjectJson = apply ? ProjectJsonSerializer.Serialize(finalProject) : string.Empty;
            var appliedWorkspaceJson = apply
                ? EditorWorkspaceProfileJsonSerializer.Serialize(finalWorkspace)
                : string.Empty;
            var undo = apply && editing.UndoRuntimeUiUx6X().Succeeded &&
                       ReferenceEquals(editing.Project!.Module.RuntimePolicies.Gameplay.Session.Shell,
                           sourceGameplay.Session.Shell) &&
                       EditorWorkspaceProfileJsonSerializer.Serialize(editing.WorkspaceProfile) ==
                       EditorWorkspaceProfileJsonSerializer.Serialize(sourceWorkspace);
            var redo = undo && editing.RedoRuntimeUiUx6X().Succeeded &&
                       ProjectJsonSerializer.Serialize(editing.Project!) == appliedProjectJson &&
                       EditorWorkspaceProfileJsonSerializer.Serialize(editing.WorkspaceProfile) == appliedWorkspaceJson;
            var saved = redo && editing.SaveRuntimeUiUx6X().Succeeded;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded && reopened.Project is not null &&
                              ProjectJsonSerializer.Serialize(reopened.Project) == appliedProjectJson &&
                              EditorWorkspaceProfileJsonSerializer.Serialize(reopened.WorkspaceProfile) ==
                              appliedWorkspaceJson;
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6X(root);
            var succeeded = explicitSelection && realReadable && invalidConfiguration && projectDryRun &&
                            workspaceDryRun && runtimeResolved && typographyResolved && staleSource &&
                            staleApproval && canonicalProject && canonicalWorkspace && undo && redo && persistence &&
                            gameplayNeighbors && workspaceNeighbors && projectNeighbors && human && bounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, invalidConfiguration, projectDryRun,
                workspaceDryRun, runtimeResolved, typographyResolved, staleSource, staleApproval,
                canonicalProject, canonicalWorkspace, undo && redo, persistence, gameplayNeighbors,
                workspaceNeighbors, projectNeighbors, human, bounded, cleanup);
        }
        catch
        {
            return FailedSmokeUx6X(false, false, cleanup);
        }
        finally
        {
            editing?.Close();
            reopened?.Close();
            if (!string.IsNullOrWhiteSpace(root)) cleanup = DeleteTemporaryUx6X(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_X_CLEANUP_FAILED");
        }
    }

    internal static StudioUx6XDraft ChangedRuntimeUiDraftUx6X(StudioUx6XDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var shellScale = Math.Abs(draft.RuntimeShell.Scale - 1.15) < .001 ? 1.25 : 1.15;
        var uiScale = Math.Abs(draft.Typography.UiScale - 1.10) < .001 ? 1.20 : 1.10;
        var textScale = Math.Abs(draft.Typography.TextScale - 1.25) < .001 ? 1.35 : 1.25;
        var primary = string.Equals(draft.Typography.PrimaryFontFamily, "Atkinson Hyperlegible",
            StringComparison.OrdinalIgnoreCase) ? "Noto Sans" : "Atkinson Hyperlegible";
        var fallbacks = new[] { "Noto Sans", "Segoe UI", "Arial" }
            .Where(item => !string.Equals(item, primary, StringComparison.OrdinalIgnoreCase)).ToArray();
        return draft with
        {
            RuntimeShell = draft.RuntimeShell with
            {
                Enabled = true,
                KitId = "core-rpg",
                SkinId = "forge-runtime-accessible",
                LayoutDocument = "ui/runtime-shell.json",
                Scale = shellScale,
                MapVisible = true,
                JournalVisible = true,
                HudVisible = true,
                InputAgnostic = true,
                Scalable = true,
                InputActions = draft.RuntimeShell.InputActions.Append("character")
                    .Append("inventory").Distinct(StringComparer.Ordinal).ToArray(),
            },
            Typography = draft.Typography with
            {
                UiScale = uiScale,
                TextScale = textScale,
                PrimaryFontFamily = primary,
                FallbackFontFamilies = fallbacks,
            },
        };
    }

    private bool TryGetCurrentUx6X(StudioUx6XDraft draft, out ProjectCommandHistory? history,
        out ProjectDefinition? project, out StudioUx6XPreview? failure)
    {
        history = History;
        project = Project;
        failure = null;
        if (history is null || project is null || _workspaceProfileHistory is null)
        {
            failure = FailedPreviewUx6X("Ouvrez un module et choisissez explicitement sa configuration UI.",
                new(Ux6XSelectionCode, "module/runtimePolicies/gameplay/session/shell",
                    "Sélection explicite requise."));
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceProjectFingerprint, ProjectFingerprintUx6X(project), StringComparison.Ordinal) ||
            !string.Equals(draft.SourceWorkspaceFingerprint, WorkspaceFingerprintUx6X(WorkspaceProfile),
                StringComparison.Ordinal))
        {
            failure = FailedPreviewUx6X("Le projet ou le profil utilisateur a changé · rechargez les valeurs.",
                new(Ux6XSourceCode, "module/runtimePolicies/gameplay/session/shell",
                    "La révision ou l’une des sources ne correspond plus."));
            return false;
        }
        return true;
    }

    private static bool TryBuildCandidatesUx6X(StudioUx6XDraft draft,
        out ModuleGameplayControlProfile? gameplay, out EditorWorkspaceProfile? workspace,
        out StudioUx6XDiagnostic? diagnostic)
    {
        gameplay = null;
        workspace = null;
        diagnostic = null;
        try
        {
            var shellDraft = draft.RuntimeShell;
            var typography = draft.Typography;
            if (!double.IsFinite(shellDraft.Scale) || shellDraft.Scale is < .5 or > 3 ||
                !double.IsFinite(typography.UiScale) ||
                typography.UiScale is < EditorWorkspaceProfile.MinimumUiScale or > EditorWorkspaceProfile.MaximumUiScale ||
                !double.IsFinite(typography.TextScale) ||
                typography.TextScale is < EditorWorkspaceProfile.MinimumTextScale or > EditorWorkspaceProfile.MaximumTextScale ||
                string.IsNullOrWhiteSpace(shellDraft.KitId) || string.IsNullOrWhiteSpace(shellDraft.SkinId) ||
                string.IsNullOrWhiteSpace(shellDraft.LayoutDocument) ||
                shellDraft.InputActions.Count == 0 || shellDraft.InputActions.Any(string.IsNullOrWhiteSpace) ||
                shellDraft.InputActions.Distinct(StringComparer.Ordinal).Count() != shellDraft.InputActions.Count ||
                string.IsNullOrWhiteSpace(typography.PrimaryFontFamily) ||
                typography.FallbackFontFamilies.Count > 4 ||
                typography.FallbackFontFamilies.Any(string.IsNullOrWhiteSpace) ||
                typography.FallbackFontFamilies.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                typography.FallbackFontFamilies.Count || typography.FallbackFontFamilies.Any(item =>
                    string.Equals(item, typography.PrimaryFontFamily, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("HUD, échelles, police et listes doivent respecter leurs bornes et unicités.");

            var source = draft.SourceGameplay;
            var sourceSession = source.Session;
            var sourceShell = sourceSession.Shell;
            var shell = new RuntimeShellProfile(sourceShell.Id, sourceShell.SchemaVersion, Ux6XProvenance,
                shellDraft.SkinId.Trim(), shellDraft.Scale, shellDraft.MapVisible, shellDraft.JournalVisible,
                shellDraft.HudVisible, shellDraft.InputActions.Select(item => item.Trim()).ToArray(),
                shellDraft.Enabled, shellDraft.RequiredAtRuntime, shellDraft.KitId.Trim(),
                shellDraft.InputAgnostic, shellDraft.Scalable, shellDraft.LayoutDocument.Trim());
            var session = sourceSession with { Shell = shell, Provenance = Ux6XProvenance };
            gameplay = new ModuleGameplayControlProfile(source.Id, source.SchemaVersion, source.Swimming,
                source.Climbing, source.RadialMenu, source.TimeControl, source.CombatModes, Ux6XProvenance,
                source.Foundation, source.Simulation, session);
            var validation = GameplaySessionProfileValidator.Validate(session).FirstOrDefault();
            if (validation is not null)
            {
                diagnostic = new(validation.Code, validation.Path, validation.Message);
                gameplay = null;
                return false;
            }

            var workspaceSource = draft.SourceWorkspace;
            workspace = new EditorWorkspaceProfile(workspaceSource.Id, workspaceSource.Name,
                workspaceSource.Role, workspaceSource.DisplayFilter, workspaceSource.RepeatPlacement,
                workspaceSource.OpenAreaTabs, workspaceSource.ActiveAreaId, workspaceSource.ComparisonAreaId,
                workspaceSource.ContextualHelpEnabled, Ux6XProvenance, workspaceSource.SchemaVersion,
                typography.UiScale, typography.TextScale, typography.PrimaryFontFamily.Trim(),
                typography.FallbackFontFamilies.Select(item => item.Trim()).ToArray(),
                workspaceSource.PersonalFavoriteBlueprintIds, workspaceSource.RecentBlueprintIds,
                workspaceSource.Shell, workspaceSource.RepeatedPlacementProfile, workspaceSource.MultiArea,
                workspaceSource.Help, workspaceSource.RolePolicy, workspaceSource.NamedPaletteCollections);
            return true;
        }
        catch (ArgumentException exception)
        {
            diagnostic = new(Ux6XProfileCode, "module/runtimePolicies/gameplay/session/shell",
                exception.Message);
            gameplay = null;
            workspace = null;
            return false;
        }
    }

    private IReadOnlyList<string> BuildRuntimePreviewUx6X(RuntimeShellProfile shell,
        EditorWorkspaceProfile workspace)
    {
        var resolved = GameplaySessionServices.ResolveShell(shell);
        var governance = FontGovernanceUx6X.FirstOrDefault(item =>
            string.Equals(item.Family, workspace.PrimaryFontFamily, StringComparison.OrdinalIgnoreCase));
        var fontSize = Math.Max(1, (int)Math.Round(Forge.Studio.Presentation.StudioStyle.BaseFontSize *
                                                       workspace.TextScale));
        return
        [
            $"Shell : kit {HumanUx6X(resolved.KitId)} · skin {HumanUx6X(resolved.SkinId)} · échelle {resolved.Scale:0.##}×.",
            $"Panneaux : {(resolved.VisiblePanels.Count == 0 ? "aucun" : string.Join(", ", resolved.VisiblePanels.Select(HumanPanelUx6X)))}.",
            $"Entrées : {resolved.InputActions.Count} action(s) · {(resolved.InputAgnostic ? "clavier/manette indépendants" : "profil d’entrée spécifique")}.",
            $"Typographie : {HumanUx6X(workspace.PrimaryFontFamily)} · UI {workspace.UiScale:0.##}× · texte {workspace.TextScale:0.##}× · aperçu {fontSize} px.",
            governance is { Governed: true }
                ? $"Police gouvernée : scripts {JoinHumanUx6X(governance.ShapingScripts)} · locales {JoinHumanUx6X(governance.Locales)} · DPI {JoinHumanUx6X(governance.DpiBuckets.Select(item => item.ToString()).ToArray())}."
                : "Police système héritée : aucun manifeste de police gouverné ne lui est associé dans ce workspace.",
            $"Fallbacks : {(workspace.FallbackFontFamilies.Count == 0 ? "aucun" : string.Join(" → ", workspace.FallbackFontFamilies.Select(HumanUx6X)))}.",
            Ux6XBoundary,
        ];
    }

    private static IReadOnlyList<string> BuildDiffUx6X(RuntimeShellProfile sourceShell,
        RuntimeShellProfile candidateShell, EditorWorkspaceProfile sourceWorkspace,
        EditorWorkspaceProfile candidateWorkspace)
    {
        var diff = new List<string>();
        if (sourceShell.Enabled != candidateShell.Enabled ||
            sourceShell.RequiredAtRuntime != candidateShell.RequiredAtRuntime)
            diff.Add($"Shell runtime : {(candidateShell.Enabled ? "activé" : "désactivé")} · {(candidateShell.RequiredAtRuntime ? "requis" : "optionnel")}.");
        if (sourceShell.KitId != candidateShell.KitId || sourceShell.SkinId != candidateShell.SkinId ||
            sourceShell.LayoutDocument != candidateShell.LayoutDocument)
            diff.Add($"Présentation : kit {HumanUx6X(candidateShell.KitId)} · skin {HumanUx6X(candidateShell.SkinId)} · layout {HumanUx6X(candidateShell.LayoutDocument)}.");
        if (sourceShell.Scale != candidateShell.Scale || sourceShell.MapVisible != candidateShell.MapVisible ||
            sourceShell.JournalVisible != candidateShell.JournalVisible || sourceShell.HudVisible != candidateShell.HudVisible)
            diff.Add($"HUD : échelle {sourceShell.Scale:0.##}× → {candidateShell.Scale:0.##}× · carte {(candidateShell.MapVisible ? "visible" : "masquée")} · journal {(candidateShell.JournalVisible ? "visible" : "masqué")} · HUD {(candidateShell.HudVisible ? "visible" : "masqué")}.");
        if (!sourceShell.InputActions.SequenceEqual(candidateShell.InputActions) ||
            sourceShell.InputAgnostic != candidateShell.InputAgnostic ||
            sourceShell.Scalable != candidateShell.Scalable)
            diff.Add($"Entrées et reflow : {candidateShell.InputActions.Count} action(s) · {(candidateShell.InputAgnostic ? "agnostique" : "spécifique")} · {(candidateShell.Scalable ? "adaptatif" : "fixe")}.");
        if (sourceWorkspace.UiScale != candidateWorkspace.UiScale ||
            sourceWorkspace.TextScale != candidateWorkspace.TextScale)
            diff.Add($"Échelles Studio : UI {sourceWorkspace.UiScale:0.##}× → {candidateWorkspace.UiScale:0.##}× · texte {sourceWorkspace.TextScale:0.##}× → {candidateWorkspace.TextScale:0.##}×.");
        if (sourceWorkspace.PrimaryFontFamily != candidateWorkspace.PrimaryFontFamily ||
            !sourceWorkspace.FallbackFontFamilies.SequenceEqual(candidateWorkspace.FallbackFontFamilies))
            diff.Add($"Chaîne typographique : {HumanUx6X(candidateWorkspace.PrimaryFontFamily)} · {candidateWorkspace.FallbackFontFamilies.Count} fallback(s).");
        return diff;
    }

    private static bool WorkspaceNeighborsUx6X(EditorWorkspaceProfile source, EditorWorkspaceProfile candidate) =>
        source.Id == candidate.Id && source.Name == candidate.Name && source.Role == candidate.Role &&
        ReferenceEquals(source.DisplayFilter, candidate.DisplayFilter) &&
        source.RepeatPlacement == candidate.RepeatPlacement &&
        source.OpenAreaTabs.SequenceEqual(candidate.OpenAreaTabs) &&
        source.ActiveAreaId == candidate.ActiveAreaId && source.ComparisonAreaId == candidate.ComparisonAreaId &&
        source.ContextualHelpEnabled == candidate.ContextualHelpEnabled &&
        source.PersonalFavoriteBlueprintIds.SequenceEqual(candidate.PersonalFavoriteBlueprintIds) &&
        source.RecentBlueprintIds.SequenceEqual(candidate.RecentBlueprintIds) &&
        ReferenceEquals(source.Shell, candidate.Shell) &&
        ReferenceEquals(source.RepeatedPlacementProfile, candidate.RepeatedPlacementProfile) &&
        ReferenceEquals(source.MultiArea, candidate.MultiArea) && ReferenceEquals(source.Help, candidate.Help) &&
        ReferenceEquals(source.RolePolicy, candidate.RolePolicy) &&
        source.NamedPaletteCollections.SequenceEqual(candidate.NamedPaletteCollections);

    private static StudioUx6XDraft FreezeDraftUx6X(StudioUx6XDraft draft) => draft with
    {
        RuntimeShell = draft.RuntimeShell with { InputActions = draft.RuntimeShell.InputActions.ToArray() },
        Typography = draft.Typography with
        {
            FallbackFontFamilies = draft.Typography.FallbackFontFamilies.ToArray(),
        },
    };

    private static string ProjectFingerprintUx6X(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string WorkspaceFingerprintUx6X(EditorWorkspaceProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            EditorWorkspaceProfileJsonSerializer.Serialize(profile))));

    private static string CandidateFingerprintUx6X(StudioUx6XDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteUx6X(writer, draft.SourceProjectFingerprint);
        WriteUx6X(writer, draft.SourceWorkspaceFingerprint);
        writer.Write(draft.BaseRevision.Value);
        writer.Write(draft.RuntimeShell.Enabled);
        writer.Write(draft.RuntimeShell.RequiredAtRuntime);
        WriteUx6X(writer, draft.RuntimeShell.KitId);
        WriteUx6X(writer, draft.RuntimeShell.SkinId);
        WriteUx6X(writer, draft.RuntimeShell.LayoutDocument);
        writer.Write(draft.RuntimeShell.Scale);
        writer.Write(draft.RuntimeShell.MapVisible);
        writer.Write(draft.RuntimeShell.JournalVisible);
        writer.Write(draft.RuntimeShell.HudVisible);
        writer.Write(draft.RuntimeShell.InputAgnostic);
        writer.Write(draft.RuntimeShell.Scalable);
        WriteListUx6X(writer, draft.RuntimeShell.InputActions);
        writer.Write(draft.Typography.UiScale);
        writer.Write(draft.Typography.TextScale);
        WriteUx6X(writer, draft.Typography.PrimaryFontFamily);
        WriteListUx6X(writer, draft.Typography.FallbackFontFamilies);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteListUx6X(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) WriteUx6X(writer, value);
    }

    private static void WriteUx6X(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static StudioUx6XPreview FailedPreviewUx6X(string message, StudioUx6XDiagnostic diagnostic) =>
        new(false, false, false, false, message, [], [diagnostic], [], false, false, true);

    private static StudioUx6XPreview FailedCommandUx6X(CommandResult result)
    {
        var diagnostic = result.Diagnostics.FirstOrDefault();
        return FailedPreviewUx6X(HumanCommandFailureUx6X(result), new(
            diagnostic?.Code ?? Ux6XProfileCode,
            "module/runtimePolicies/gameplay/session/shell",
            diagnostic?.Message ?? "La commande HUD a été refusée."));
    }

    private static string HumanCommandFailureUx6X(CommandResult result) =>
        result.Diagnostics.FirstOrDefault() is { } diagnostic
            ? $"{diagnostic.Code} · {diagnostic.Message}"
            : "La commande HUD a été refusée.";

    private static string HumanPanelUx6X(string value) => value switch
    {
        "map" => "carte",
        "journal" => "journal",
        "hud" => "HUD",
        _ => HumanUx6X(value),
    };

    private static string JoinHumanUx6X(IReadOnlyList<string> values) =>
        values.Count == 0 ? "non déclaré" : string.Join(", ", values.Select(HumanUx6X));

    private static string HumanUx6X(string value) => Regex.Replace(value ?? string.Empty,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        "identifiant protégé").Replace('_', ' ').Trim();

    private static bool ContainsUuidUx6X(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    internal static bool DeleteTemporaryUx6X(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux6-x-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StudioUx6XSmokeResult FailedSmokeUx6X(bool explicitSelection, bool realReadable,
        bool cleanup) => new(false, explicitSelection, realReadable, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, true, cleanup);
}
