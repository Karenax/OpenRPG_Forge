using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private bool _lastUx7FTemporaryReleased = true;

    internal bool LastUx7FTemporaryReleased => _lastUx7FTemporaryReleased;

    internal StudioUx7FHistorySelection? CurrentHistoryLabUx7F()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var recovery = Project.Module.RuntimePolicies.Extensions.OperationalReadiness.Recovery;
            var gameplay = Project.Module.RuntimePolicies.Gameplay;
            return new StudioUx7FHistorySelection(Project.Id.Value, Project.Module.DisplayName,
                SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                recovery.AutosaveIntervalSeconds, recovery.RetainedSnapshots,
                gameplay.Foundation.Save.RollbackSlots, gameplay.Session.Persistence.Scope.ToString(),
                gameplay.Session.Checkpoints.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    internal StudioUx7FHistoryRunReport RunIsolatedHistoryUx7F(
        StudioUx7FHistorySelection selection,
        CancellationToken cancellationToken,
        Action<StudioUx7FHistoryStage>? stageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedHistoryRunUx7F("Run refusé · aucun module source n’est ouvert.");
        var current = CurrentHistoryLabUx7F();
        if (current is null || current.ProjectId != selection.ProjectId ||
            !string.Equals(current.SourceFingerprint, selection.SourceFingerprint, StringComparison.Ordinal))
            return FailedHistoryRunUx7F("Run refusé · le module ou ses fichiers ont changé.");

        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceProjectFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var sourceRoot = Session.RootDirectory;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-f",
            Guid.NewGuid().ToString("N"));
        var stages = new List<StudioUx7FHistoryStage>();
        var cancelled = false;
        var isolated = false;
        var artifactsConfined = false;
        var error = string.Empty;
        _lastUx7FTemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, root))
                throw new IOException("Les fichiers du module n’ont pas pu être copiés dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            var project = created.Session.Project;

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(AutosaveRecoveryStageUx7F(project, Path.Combine(root, "autosave-recovery")));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(RuntimeSaveStageUx7F(project, Path.Combine(root, "runtime-saves")));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(SaveScopesStageUx7F(project.Module.RuntimePolicies.Gameplay.Session));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(CheckpointStageUx7F(project.Module.RuntimePolicies.Gameplay.Session));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(CrashRecoveryStageUx7F(project, Path.Combine(root, "crash-recovery")));

            var rootPrefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToArray();
            artifactsConfined = files.Length >= 8 && files.All(path =>
                Path.GetFullPath(path).StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase));
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or NotSupportedException or
                                           System.Text.Json.JsonException or InvalidDataException)
        {
            error = exception.Message;
        }
        finally
        {
            _lastUx7FTemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }

        var final = CurrentHistoryLabUx7F();
        var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                              ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              History?.CanUndo == sourceCanUndo && History?.CanRedo == sourceCanRedo &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == sourceProjectFile &&
                              final?.SourceFingerprint == selection.SourceFingerprint;
        var succeeded = !cancelled && string.IsNullOrEmpty(error) && isolated && artifactsConfined &&
                        stages.Count == 5 && stages.All(stage => stage.Completed) && sourceUntouched &&
                        _lastUx7FTemporaryReleased;
        return new StudioUx7FHistoryRunReport(succeeded, cancelled, isolated, artifactsConfined,
            sourceUntouched, _lastUx7FTemporaryReleased, HistoryRunFingerprintUx7F(stages), stages,
            cancelled
                ? "Run annulé proprement · aucun fichier source n’a été modifié."
                : succeeded
                    ? "Autosaves, archives, portées, checkpoints et récupération inspectés dans une copie."
                    : "Run incomplet · " + (string.IsNullOrEmpty(error) ?
                        "consultez les résultats localisés." : error));

        void AddStage(StudioUx7FHistoryStage stage)
        {
            stages.Add(stage);
            stageCompleted?.Invoke(stage);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal void CloseUx7F() => _lastUx7FTemporaryReleased = true;

    internal StudioUx7FHistorySmokeResult RunUx7FContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedHistorySmokeUx7F();
        var originalProject = Project;
        var originalHistory = History;
        var originalRevision = History.State.Revision;
        var originalJournal = History.Journal.ToArray();
        var originalDirty = IsDirty;
        var originalArea = ActiveAreaId;
        var originalSelection = SelectedEntityIds.ToArray();
        var originalSelected = SelectedEntityId;
        var originalFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var selection = CurrentHistoryLabUx7F();
        if (selection is null) return FailedHistorySmokeUx7F();

        var run = RunIsolatedHistoryUx7F(selection, CancellationToken.None);
        var second = RunIsolatedHistoryUx7F(selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunIsolatedHistoryUx7F(selection, cancellation.Token, stage =>
        {
            if (stage.Key == "autosave-recovery") cancellation.Cancel();
        });

        var routes = run.Stages.Select(stage => stage.Route).ToHashSet(StringComparer.Ordinal);
        var fiveRoutes = routes.SetEquals(["ORF-SHELL-024", "ORF-GAME-020", "ORF-GAME-021",
            "ORF-GAME-025", "ORF-QA-028"]);
        var autosave = run.Stages.SingleOrDefault(stage => stage.Key == "autosave-recovery");
        var save = run.Stages.SingleOrDefault(stage => stage.Key == "save-load");
        var scopes = run.Stages.SingleOrDefault(stage => stage.Key == "save-scopes");
        var checkpoint = run.Stages.SingleOrDefault(stage => stage.Key == "checkpoint-respawn");
        var crash = run.Stages.SingleOrDefault(stage => stage.Key == "crash-recovery");
        var explicitSelection = selection.ProjectId != Guid.Empty && selection.SourceFingerprint.Length == 64 &&
                                selection.AutosaveIntervalSeconds > 0 &&
                                selection.RetainedRecoverySnapshots > 0 && selection.SaveRollbackSlots > 0 &&
                                !string.IsNullOrWhiteSpace(selection.PersistenceScope) &&
                                selection.CheckpointCount > 0;
        var autosaveReady = autosave is { Completed: true, Passed: true, Cases: >= 2 } &&
                            autosave.Details.Count >= 7;
        var saveReady = save is { Completed: true, Passed: true, Cases: >= 2 } && save.Details.Count >= 7;
        var scopesReady = scopes is { Completed: true, Passed: true, Cases: 3 } && scopes.Details.Count >= 6;
        var checkpointReady = checkpoint is { Completed: true, Passed: true, Cases: >= 4 } &&
                              checkpoint.Details.Count >= 7;
        var crashReady = crash is { Completed: true, Passed: true, Cases: >= 2 } && crash.Details.Count >= 6;
        var cancellationReady = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                cancelled.Stages[0].Key == "autosave-recovery" &&
                                cancelled.TemporaryWorkspaceReleased;
        var navigation = run.Stages.Count == 5 && run.Stages.All(stage =>
            !string.IsNullOrWhiteSpace(stage.Title) && !string.IsNullOrWhiteSpace(stage.Summary) &&
            stage.Details.Count > 0);
        var deterministic = run.Succeeded && second.Succeeded &&
                            string.Equals(run.Fingerprint, second.Fingerprint, StringComparison.Ordinal);
        var sourceUnchanged = run.SourceUntouched && second.SourceUntouched && cancelled.SourceUntouched &&
                              ReferenceEquals(originalProject, Project) &&
                              ReferenceEquals(originalHistory, History) &&
                              History?.State.Revision == originalRevision &&
                              (History?.Journal ?? []).SequenceEqual(originalJournal) &&
                              IsDirty == originalDirty && ActiveAreaId == originalArea &&
                              SelectedEntityIds.SequenceEqual(originalSelection) &&
                              SelectedEntityId == originalSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == originalFile;
        var released = run.TemporaryWorkspaceReleased && second.TemporaryWorkspaceReleased &&
                       cancelled.TemporaryWorkspaceReleased && _lastUx7FTemporaryReleased;
        var succeeded = run.Succeeded && second.Succeeded && fiveRoutes && explicitSelection && autosaveReady &&
                        saveReady && scopesReady && checkpointReady && crashReady && cancellationReady &&
                        navigation && deterministic && run.Isolated && run.ArtifactsConfined && sourceUnchanged &&
                        released;
        return new StudioUx7FHistorySmokeResult(succeeded, fiveRoutes, explicitSelection, autosaveReady,
            saveReady, scopesReady, checkpointReady, crashReady, cancellationReady, navigation, deterministic,
            run.Isolated, run.ArtifactsConfined, sourceUnchanged, released);
    }

    private static StudioUx7FHistoryStage AutosaveRecoveryStageUx7F(ProjectDefinition project, string root)
    {
        Directory.CreateDirectory(root);
        var policy = project.Module.RuntimePolicies.Extensions.OperationalReadiness.Recovery;
        var store = new RecoverySnapshotStore(policy.RetainedSnapshots);
        var sessions = new RecoverySessionStore();
        var firstSession = sessions.Begin(root, project.Id, DateTimeOffset.UnixEpoch, 1001, "studio:ux7-f");
        var interrupted = sessions.Begin(root, project.Id, DateTimeOffset.UnixEpoch.AddMinutes(1), 1002,
            "studio:ux7-f");
        var snapshot = store.Capture(root, project, 4, DateTimeOffset.UnixEpoch.AddMinutes(2));
        var current = new ProjectDefinition(project.Id, project.Title + " · brouillon non sauvegardé",
            project.Module, project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        var selection = new RecoveryRestoreSelection(
            Guid.Parse("f7000000-0000-4000-8000-000000000001"), 1,
            RecoveryRestoreSection.ProjectAndModule, "studio:ux7-f");
        var preview = store.PreviewRestore(snapshot, current, selection);
        var state = new ProjectState(current, new ProjectRevision(4));
        var handler = new RestoreRecoverySnapshotHandler(new SystemClock());
        var command = new RestoreRecoverySnapshotCommand(
            CommandId.From(Guid.Parse("f7000000-0000-4000-8000-000000000002")), state.Revision,
            Snapshot: project, selection, DryRun: true);
        var dryRun = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var history = new ProjectCommandHistory(state);
        if (applied.Succeeded) history.Record(applied);
        var undo = history.Undo();
        var redo = history.Redo();
        var clean = sessions.End(root, interrupted.Current.Id, DateTimeOffset.UnixEpoch.AddMinutes(3));
        var candidate = store.FindRecoveryCandidate(root, DateTimeOffset.UnixEpoch);
        var passed = !firstSession.PreviousSessionWasUnclean && interrupted.PreviousSessionWasUnclean &&
                      preview.Comparison.Changed && dryRun.Succeeded && ReferenceEquals(dryRun.State, state) &&
                     applied.Succeeded && applied.State.Project.Title == project.Title && undo.Succeeded &&
                     undo.State.Project.Title == current.Title && redo.Succeeded &&
                      redo.State.Project.Title == project.Title && clean && candidate?.Id == snapshot.Id &&
                      sessions.Read(root)?.CleanShutdown == true;
        if (!passed)
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_F_AUTOSAVE_FAILED " +
                              $"first_clean={(!firstSession.PreviousSessionWasUnclean).ToString().ToLowerInvariant()} " +
                              $"interrupted={interrupted.PreviousSessionWasUnclean.ToString().ToLowerInvariant()} " +
                              $"comparison={preview.Comparison.Changed.ToString().ToLowerInvariant()} " +
                              $"dry_run={dryRun.Succeeded.ToString().ToLowerInvariant()} " +
                              $"dry_untouched={ReferenceEquals(dryRun.State, state).ToString().ToLowerInvariant()} " +
                              $"apply={applied.Succeeded.ToString().ToLowerInvariant()} " +
                              $"restored_title={(applied.Succeeded && applied.State.Project.Title == project.Title).ToString().ToLowerInvariant()} " +
                              $"undo={undo.Succeeded.ToString().ToLowerInvariant()} " +
                              $"undo_title={(undo.Succeeded && undo.State.Project.Title == current.Title).ToString().ToLowerInvariant()} " +
                              $"redo={redo.Succeeded.ToString().ToLowerInvariant()} " +
                              $"redo_title={(redo.Succeeded && redo.State.Project.Title == project.Title).ToString().ToLowerInvariant()} " +
                              $"clean={clean.ToString().ToLowerInvariant()} " +
                              $"candidate={(candidate?.Id == snapshot.Id).ToString().ToLowerInvariant()} " +
                              $"marker={(sessions.Read(root)?.CleanShutdown == true).ToString().ToLowerInvariant()} " +
                              $"diagnostics={string.Join(',', applied.Diagnostics.Select(item => item.Code))}");
        var details = new[]
        {
            $"Politique · autosave {policy.AutosaveIntervalSeconds}s · rétention {policy.RetainedSnapshots} · comparaison obligatoire {(policy.CompareBeforeRestore ? "oui" : "non")}",
            $"Snapshot · révision {snapshot.Revision} · SHA-256 {snapshot.ProjectSha256[..12]} · candidat de récupération {(candidate is null ? "absent" : "présent")}",
            $"Session précédente · {(interrupted.PreviousSessionWasUnclean ? "interrompue détectée" : "non détectée")} · fermeture propre {(clean ? "enregistrée" : "échouée")}",
            $"Impact · titre courant « {current.Title} » → snapshot « {preview.Project.Title} »",
            $"Sélection · {string.Join(", ", preview.Comparison.SelectedSections ?? [])}",
            $"Commande · dry-run {(dryRun.Succeeded ? "PASS" : "FAILED")} · apply {(applied.Succeeded ? "PASS" : "FAILED")}",
            $"Historique isolé · undo {(undo.Succeeded ? "PASS" : "FAILED")} · redo {(redo.Succeeded ? "PASS" : "FAILED")}",
            "Frontière · restauration appliquée uniquement à un ProjectState en mémoire dans la copie temporaire",
        };
        return new StudioUx7FHistoryStage("ORF-SHELL-024", "autosave-recovery",
            "Sauvegarde automatique et récupération", true, passed, 2,
            $"Snapshot versionné, session interrompue, comparaison, dry-run, restauration et historique " +
            $"{(passed ? "validés" : "incomplets")}.", details, HashJsonUx7D(new
            {
                passed,
                snapshot.Revision,
                snapshot.ProjectSha256,
                preview.Comparison,
                dryRun = dryRun.Succeeded,
                apply = applied.Succeeded,
                undo = undo.Succeeded,
                redo = redo.Succeeded,
                clean,
            }));
    }

    private static StudioUx7FHistoryStage RuntimeSaveStageUx7F(ProjectDefinition project, string root)
    {
        Directory.CreateDirectory(root);
        var policy = project.Module.RuntimePolicies.Gameplay.Foundation.Save;
        var moduleId = project.Module.Id.Value.ToString("D");
        var contentHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));
        RuntimeSaveDocument Save(string questState) => new(
            RuntimeSaveDocument.CurrentFormatVersion, moduleId, contentHash,
            project.Module.EntryAreaId.Value.ToString("D"), DateTimeOffset.UnixEpoch,
            new(1, 2, 3), new("f7000000-0000-4000-8000-000000000010", 42, 7), [],
            [new("quest", questState)], null, null, [], new(false, false, false, false, false), null,
            null, []);
        var first = RuntimeSaveArchiveStore.Save(root, "studio-history", 1, Save("active"), policy, [],
            DateTimeOffset.UnixEpoch.AddMinutes(1));
        var second = RuntimeSaveArchiveStore.Save(root, "studio-history", 2, Save("complete"), policy, [],
            DateTimeOffset.UnixEpoch.AddMinutes(2));
        var latest = RuntimeSaveArchiveStore.Load(root, "studio-history", 0, moduleId, contentHash, []);
        var rollback = RuntimeSaveArchiveStore.Load(root, "studio-history", 1, moduleId, contentHash, []);
        var rejected = RuntimeSaveArchiveStore.Load(root, "studio-history", 0, moduleId, new string('f', 64), []);
        var beforeAutosave = RuntimeSaveArchiveStore.ShouldAutosave(policy, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(Math.Max(0, policy.AutosaveIntervalSeconds - 1)));
        var atAutosave = RuntimeSaveArchiveStore.ShouldAutosave(policy, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(policy.AutosaveIntervalSeconds));
        var passed = first.Succeeded && second.Succeeded && latest.Succeeded && rollback.Succeeded &&
                     latest.Save?.Variables.Single().Value == "complete" &&
                     rollback.Save?.Variables.Single().Value == "active" && !rejected.Succeeded &&
                     rejected.Diagnostics.Single().Code == RuntimeSaveStore.ContentMismatchCode &&
                     !beforeAutosave && atAutosave && second.Manifest?.PayloadSha256.Length == 64;
        var details = new[]
        {
            $"Format · {RuntimeSaveDocument.CurrentFormatVersion} · slot studio-history · chunks {second.Manifest?.ChunkSha256.Count ?? 0}",
            $"Archive · révisions 1 et 2 · rollback slots {policy.RollbackSlots}",
            $"Chargement courant · {latest.Save?.Variables.Single().Value ?? "refusé"}",
            $"Chargement rollback · {rollback.Save?.Variables.Single().Value ?? "refusé"}",
            $"SHA-256 contenu · {contentHash[..12]} · payload {second.Manifest?.PayloadSha256[..12] ?? "absent"}",
            $"Autosave · avant seuil {(beforeAutosave ? "déclenché" : "non")} · au seuil {(atAutosave ? "déclenché" : "non")}",
            $"Contenu incompatible · {rejected.Diagnostics.SingleOrDefault()?.Code ?? "NON REJETÉ"}",
            "Frontière · archive, chunks et manifestes écrits uniquement sous la racine temporaire",
        };
        return new StudioUx7FHistoryStage("ORF-GAME-020", "save-load", "Sauvegarde et chargement",
            true, passed, 2, $"Archive versionnée · latest/rollback · checksum et contenu " +
            $"{(passed ? "validés" : "incomplets")}.", details, HashJsonUx7D(new
            {
                passed,
                latest = latest.Save?.Variables.Single().Value,
                rollback = rollback.Save?.Variables.Single().Value,
                rejected = rejected.Diagnostics.Select(item => item.Code),
                first = first.Manifest is null ? null : new { first.Manifest.Revision, first.Manifest.PayloadSha256 },
                second = second.Manifest is null ? null : new { second.Manifest.Revision, second.Manifest.PayloadSha256 },
                beforeAutosave,
                atAutosave,
            }));
    }

    private static StudioUx7FHistoryStage SaveScopesStageUx7F(GameplaySessionProfile session)
    {
        var envelopes = new List<SaveEnvelope>();
        foreach (var scope in Enum.GetValues<PersistenceScope>())
        {
            var authority = scope switch
            {
                PersistenceScope.Player => SaveAuthority.LocalPlayer,
                PersistenceScope.Game => SaveAuthority.Host,
                _ => SaveAuthority.Server,
            };
            var profile = session.Persistence with
            {
                Scope = scope,
                Authority = authority,
                CloudAdapterEnabled = false,
                ContainsPersonalData = false,
            };
            envelopes.Add(GameplaySessionServices.WriteSave(profile,
                new($"actor:{scope}", authority, true, true), $"owner:{scope}", 2,
                DateTimeOffset.UnixEpoch, new Dictionary<string, string> { ["state"] = "ready" }));
        }
        var wrongAuthorityRejected = ThrowsCodeUx7F(() => GameplaySessionServices.WriteSave(
            session.Persistence with { Authority = SaveAuthority.Server, ContainsPersonalData = false },
            new("player", SaveAuthority.LocalPlayer, true, true), "owner", 1, DateTimeOffset.UnixEpoch,
            new Dictionary<string, string>()), "GAME_SAVE_AUTHORITY_REQUIRED");
        var privacyRejected = ThrowsCodeUx7F(() => GameplaySessionServices.WriteSave(
            session.Persistence with { ContainsPersonalData = true, CloudAdapterEnabled = false },
            new("host", session.Persistence.Authority, true, false), "owner", 1, DateTimeOffset.UnixEpoch,
            new Dictionary<string, string>()), "GAME_SAVE_PRIVACY_CONSENT_REQUIRED");
        var current = envelopes[1];
        var incoming = current with { Revision = 3, SavedAt = current.SavedAt.AddMinutes(1) };
        var resolved = GameplaySessionServices.ResolveSave(current, incoming);
        var passed = envelopes.Count == 3 && envelopes.Select(item => item.Profile.Scope).Distinct().Count() == 3 &&
                     envelopes.All(item => item.Profile.EncryptAtRest && item.Values["state"] == "ready") &&
                     wrongAuthorityRejected && privacyRejected && resolved.Revision == 3;
        var details = envelopes.Select(item =>
                $"Portée {item.Profile.Scope} · autorité {item.Profile.Authority} · propriétaire {item.OwnerId} · chiffrement {(item.Profile.EncryptAtRest ? "requis" : "non")}")
            .Concat(new[]
            {
                $"Conflit · révision retenue {resolved.Revision} · politique {resolved.Profile.ConflictPolicy}",
                $"Autorité incorrecte · {(wrongAuthorityRejected ? "rejetée" : "NON REJETÉE")}",
                $"Consentement vie privée absent · {(privacyRejected ? "rejeté" : "NON REJETÉ")}",
                "Frontière · enveloppes en mémoire, aucun cloud ni serveur distant contacté",
            }).ToArray();
        return new StudioUx7FHistoryStage("ORF-GAME-021", "save-scopes",
            "Sauvegarde par joueur, partie et serveur", true, passed, envelopes.Count,
            $"Trois portées et autorités · conflits et confidentialité {(passed ? "validés" : "incomplets")}.",
            details, HashJsonUx7D(new
            {
                passed,
                envelopes = envelopes.Select(item => new { item.Profile.Scope, item.Profile.Authority, item.OwnerId }),
                resolved.Revision,
                wrongAuthorityRejected,
                privacyRejected,
            }));
    }

    private static StudioUx7FHistoryStage CheckpointStageUx7F(GameplaySessionProfile session)
    {
        var source = session.Checkpoints.FirstOrDefault() ?? GameplaySessionProfile.CreateDefault(
            Forge.Domain.Modules.ModuleId.From(session.Id)).Checkpoints[0];
        var checkpoint = source with { Enabled = true, CurrencyCost = Math.Max(10, source.CurrencyCost) };
        var request = new RespawnRequest("hero", checkpoint.WaypointId, checkpoint.CurrencyCost + 20, true,
            ["hero", "companion"], true);
        var ready = GameplaySessionServices.Respawn(checkpoint, request, true);
        var denied = GameplaySessionServices.Respawn(checkpoint, request, false);
        var unsafeSpawn = GameplaySessionServices.Respawn(checkpoint,
            request with { SafeSpawnAvailable = false }, true);
        var insufficient = GameplaySessionServices.Respawn(checkpoint,
            request with { Currency = checkpoint.CurrencyCost - 1 }, true);
        var mismatch = GameplaySessionServices.Respawn(checkpoint,
            request with { CheckpointWaypointId = "waypoint:other" }, true);
        var passed = ready.Succeeded && ready.Code == "GAME_RESPAWN_READY" &&
                     ready.RemainingCurrency == request.Currency - checkpoint.CurrencyCost &&
                     ready.Events.SequenceEqual([checkpoint.BeforeRespawnEvent, checkpoint.AfterRespawnEvent]) &&
                     denied.Code == "GAME_RESPAWN_AUTHORITY_REQUIRED" &&
                     unsafeSpawn.Code == "GAME_RESPAWN_SAFE_SPAWN_REQUIRED" &&
                     insufficient.Code == "GAME_RESPAWN_COST_UNAVAILABLE" &&
                     mismatch.Code == "GAME_RESPAWN_CHECKPOINT_MISMATCH";
        var details = new[]
        {
            $"Checkpoint · {checkpoint.WaypointId} · rayon sûr {checkpoint.SafeRadius:0.###}",
            $"Coût · {checkpoint.CurrencyCost} · restant {ready.RemainingCurrency}",
            $"Groupe · politique {checkpoint.PartyPolicy} · acteurs {string.Join(", ", ready.RespawnActorIds)}",
            $"Cadavre · {checkpoint.CorpsePolicy} · délai {ready.DelaySeconds:0.###}s",
            $"Hooks · {string.Join(" → ", ready.Events)}",
            $"Refus · autorité {denied.Code} · spawn {unsafeSpawn.Code} · coût {insufficient.Code}",
            $"Refus · waypoint divergent {mismatch.Code}",
            "Frontière · résolution pure, aucun déplacement d’acteur ni chargement moteur exécuté",
        };
        return new StudioUx7FHistoryStage("ORF-GAME-025", "checkpoint-respawn",
            "Checkpoints et respawn", true, passed, 5,
            $"Respawn prêt et quatre refus stables · {(passed ? "conforme" : "incomplet")}.", details,
            HashJsonUx7D(new
            {
                passed,
                ReadyCode = ready.Code,
                DeniedCode = denied.Code,
                UnsafeSpawnCode = unsafeSpawn.Code,
                InsufficientCode = insufficient.Code,
                MismatchCode = mismatch.Code,
                ready.RemainingCurrency,
                ready.RespawnActorIds,
            }));
    }

    private static StudioUx7FHistoryStage CrashRecoveryStageUx7F(ProjectDefinition project, string root)
    {
        Directory.CreateDirectory(root);
        var store = new RecoverySnapshotStore(2);
        var valid = store.Capture(root, project, 8, DateTimeOffset.UnixEpoch.AddMinutes(8));
        var corrupt = store.Capture(root, project, 9, DateTimeOffset.UnixEpoch.AddMinutes(9));
        File.AppendAllText(corrupt.SnapshotPath, "tampered", Encoding.UTF8);
        var inspections = store.Inspect(root, quarantineCorruptFiles: true);
        var quarantined = inspections.SingleOrDefault(item => item.Status == RecoverySnapshotStatus.Quarantined);
        var validInspection = inspections.SingleOrDefault(item => item.Status == RecoverySnapshotStatus.Valid);
        var remaining = store.List(root);
        var quarantineConfined = quarantined?.QuarantinePath is not null &&
                                 Path.GetFullPath(quarantined.QuarantinePath).StartsWith(
                                     Path.GetFullPath(root) + Path.DirectorySeparatorChar,
                                     StringComparison.OrdinalIgnoreCase) &&
                                 File.Exists(quarantined.QuarantinePath);
        var passed = validInspection?.Snapshot?.Id == valid.Id && quarantined?.Snapshot?.Id == corrupt.Id &&
                     quarantined.Code == "recovery.snapshot.corrupt" && quarantineConfined &&
                     !File.Exists(corrupt.SnapshotPath) && remaining.Count == 1 && remaining[0].Id == valid.Id;
        var details = new[]
        {
            $"Snapshots inspectés · {inspections.Count} · valides {inspections.Count(item => item.Status == RecoverySnapshotStatus.Valid)}",
            $"Corruption injectée · snapshot révision {corrupt.Revision} · hash journal {corrupt.ProjectSha256[..12]}",
            $"Diagnostic · {quarantined?.Code ?? "ABSENT"} · statut {quarantined?.Status.ToString() ?? "absent"}",
            $"Quarantaine · {(quarantineConfined ? "confinée" : "NON CONFINÉE")}",
            $"Journal réparé · {remaining.Count} snapshot valide retenu",
            "Frontière · corruption créée et déplacée uniquement dans la copie temporaire ; la source reste intacte",
        };
        return new StudioUx7FHistoryStage("ORF-QA-028", "crash-recovery", "Crash recovery", true,
            passed, inspections.Count, $"Snapshot altéré, détection SHA-256, quarantaine et journal réparé · " +
            $"{(passed ? "conformes" : "incomplets")}.", details, HashJsonUx7D(new
            {
                passed,
                valid = validInspection?.Status,
                corrupt = quarantined?.Status,
                quarantined?.Code,
                quarantineConfined,
                remaining = remaining.Select(item => new { item.Revision, item.ProjectSha256 }),
            }));
    }

    private static bool ThrowsCodeUx7F(Action action, string code)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException exception)
        {
            return string.Equals(exception.Message, code, StringComparison.Ordinal);
        }
    }

    private static string HistoryRunFingerprintUx7F(IReadOnlyList<StudioUx7FHistoryStage> stages) =>
        HashJsonUx7D(stages.Select(stage => new
        {
            stage.Route,
            stage.Key,
            stage.Completed,
            stage.Passed,
            stage.Cases,
            stage.Fingerprint,
        }).ToArray());

    private static StudioUx7FHistoryRunReport FailedHistoryRunUx7F(string message) =>
        new(false, false, false, false, false, true, string.Empty, [], message);

    private static StudioUx7FHistorySmokeResult FailedHistorySmokeUx7F() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false);
}
