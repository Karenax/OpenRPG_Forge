using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Entities;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Combat;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private bool _lastUx7GTemporaryReleased = true;
    private Process? _runtimePlaytestProcessUx7G;
    private string? _runtimePlaytestRootUx7G;

    internal bool LastUx7GTemporaryReleased => _lastUx7GTemporaryReleased;

    internal StudioUx7GRuntimeLaunchResult LaunchRuntimePlaytestUx7G()
    {
        var previous = PollRuntimePlaytestUx7G();
        if (previous.Running)
        {
            return new StudioUx7GRuntimeLaunchResult(
                false, true, _runtimePlaytestProcessUx7G?.Id, true, true,
                "Un playtest Runtime est déjà en cours.");
        }
        if (Project is null || History is null || Session is null)
        {
            return FailedRuntimeLaunchUx7G("Ouvrez d'abord un module avant de lancer le jeu.");
        }

        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceProjectFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var sourceRoot = Session.RootDirectory;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-runtime-playtest",
            Guid.NewGuid().ToString("N"));

        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire du module n'a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, root))
                throw new IOException("Les ressources du module n'ont pas pu être copiées pour le playtest.");

            var engine = FindOfficialEngineUx7H() ?? throw new InvalidOperationException(
                "Godot 4.7.1 .NET officiel est introuvable.");
            var runtime = FindRuntimeProjectUx7H() ?? throw new InvalidOperationException(
                "Forge.Runtime compilé est introuvable. Compilez apps/Forge.Runtime avant le playtest.");
            var isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  FileFingerprintUx7D(Session.ProjectFilePath) == sourceProjectFile;
            if (!isolated || !sourceUntouched)
                throw new InvalidOperationException("Le playtest n'a pas pu garantir une copie isolée du module.");

            var logPath = Path.Combine(root, ".forge", "runtime-interactive.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var start = new ProcessStartInfo(engine.Path)
            {
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = runtime,
                WindowStyle = ProcessWindowStyle.Normal,
            };
            foreach (var argument in new[]
                     {
                         "--rendering-method", "forward_plus", "--path", runtime,
                         "--log-file", logPath, "--", "--workspace", root,
                     })
                start.ArgumentList.Add(argument);
            var spawn = ResolveRuntimePlaytestSpawnUx7G();
            if (spawn is not null)
            {
                start.ArgumentList.Add("--spawn-id");
                start.ArgumentList.Add(spawn.Value.ToString());
            }

            _runtimePlaytestProcessUx7G = Process.Start(start) ??
                                          throw new InvalidOperationException("Forge.Runtime n'a pas pu démarrer.");
            _runtimePlaytestRootUx7G = root;
            return new StudioUx7GRuntimeLaunchResult(
                true, true, _runtimePlaytestProcessUx7G.Id, true, true,
                $"Jeu lancé dans une copie temporaire · processus {_runtimePlaytestProcessUx7G.Id} · le Studio reste ouvert.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or NotSupportedException or
                                           Win32Exception)
        {
            CleanupRuntimePlaytestUx7G(root);
            return FailedRuntimeLaunchUx7G("Playtest indisponible · " + exception.Message);
        }
    }

    internal StudioUx7GRuntimeStatus PollRuntimePlaytestUx7G()
    {
        var process = _runtimePlaytestProcessUx7G;
        if (process is null)
            return new StudioUx7GRuntimeStatus(false, false, null, true, string.Empty);
        try
        {
            if (!process.HasExited)
                return new StudioUx7GRuntimeStatus(true, false, null, false,
                    $"Jeu en cours · processus {process.Id}.");
            var exitCode = process.ExitCode;
            process.Dispose();
            _runtimePlaytestProcessUx7G = null;
            var released = CleanupRuntimePlaytestUx7G(_runtimePlaytestRootUx7G);
            _runtimePlaytestRootUx7G = null;
            return new StudioUx7GRuntimeStatus(false, true, exitCode, released,
                exitCode == 0 && released
                    ? "Jeu fermé · copie temporaire supprimée · contexte Studio conservé."
                    : $"Jeu fermé avec le code {exitCode} · " +
                      (released ? "copie temporaire supprimée." : "nettoyage temporaire à vérifier."));
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or
                                           Win32Exception)
        {
            return new StudioUx7GRuntimeStatus(false, true, null, false,
                "Le suivi du processus Runtime a échoué · " + exception.Message);
        }
    }

    internal void StopRuntimePlaytestUx7G()
    {
        var process = _runtimePlaytestProcessUx7G;
        if (process is not null)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or
                                               Win32Exception)
            {
                _ = exception;
            }
            process.Dispose();
        }
        _runtimePlaytestProcessUx7G = null;
        CleanupRuntimePlaytestUx7G(_runtimePlaytestRootUx7G);
        _runtimePlaytestRootUx7G = null;
    }

    internal StudioUx7GPlaytestSelection? CurrentPlaytestLabUx7G()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var entry = Project.Areas.SingleOrDefault(area => area.Id == Project.Module.EntryAreaId) ??
                        Project.Areas.FirstOrDefault();
            if (entry is null) return null;
            var combat = Project.Blueprints.Count(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) &&
                                                           item.Combat is { Abilities.Count: > 0 });
            return new StudioUx7GPlaytestSelection(Project.Id.Value, Project.Module.DisplayName,
                SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                entry.DisplayName, Project.Areas.Count, Project.Dialogues.Count, Project.Quests.Count, combat);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    internal StudioUx7GPlaytestRunReport RunIsolatedPlaytestUx7G(
        StudioUx7GPlaytestSelection selection,
        CancellationToken cancellationToken,
        Action<StudioUx7GPlaytestStage>? stageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedPlaytestRunUx7G("Run refusé · aucun module source n’est ouvert.");
        var current = CurrentPlaytestLabUx7G();
        if (current is null || current.ProjectId != selection.ProjectId ||
            !string.Equals(current.SourceFingerprint, selection.SourceFingerprint, StringComparison.Ordinal))
            return FailedPlaytestRunUx7G("Run refusé · le module ou ses fichiers ont changé.");

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
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-g",
            Guid.NewGuid().ToString("N"));
        var stages = new List<StudioUx7GPlaytestStage>();
        var cancelled = false;
        var isolated = false;
        var artifactsConfined = false;
        var error = string.Empty;
        _lastUx7GTemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, root))
                throw new IOException("Les fichiers du module n’ont pas pu être copiés dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            var evidence = BuildPlaytestEvidenceUx7G(created.Session.Project);

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(PreviewModeStageUx7G(evidence, Path.Combine(root, ".forge", "playtest-preview")));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(PlayFromHereStageUx7G(evidence));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(ReplayStageUx7G(evidence.Project));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(TestAgentStageUx7G(evidence.Project));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(CombatSimulationStageUx7G(evidence.Project));

            var rootPrefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToArray();
            artifactsConfined = files.Length > 0 && files.All(path =>
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
            _lastUx7GTemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }

        var final = CurrentPlaytestLabUx7G();
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
                        stages.Count == 5 && stages.All(stage => stage.Completed && stage.Passed) &&
                        sourceUntouched && _lastUx7GTemporaryReleased;
        return new StudioUx7GPlaytestRunReport(succeeded, cancelled, isolated, artifactsConfined,
            sourceUntouched, _lastUx7GTemporaryReleased, PlaytestRunFingerprintUx7G(stages), stages,
            cancelled
                ? "Run annulé proprement · aucun fichier source n’a été modifié."
                : succeeded
                    ? "Aperçu, Play From Here, replay, agent et combat simulés dans une copie."
                    : "Run incomplet · " + (string.IsNullOrEmpty(error) ?
                        "consultez les résultats localisés." : error));

        void AddStage(StudioUx7GPlaytestStage stage)
        {
            stages.Add(stage);
            stageCompleted?.Invoke(stage);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal void CloseUx7G()
    {
        StopRuntimePlaytestUx7G();
        _lastUx7GTemporaryReleased = true;
    }

    internal StudioUx7GPlaytestSmokeResult RunUx7GContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedPlaytestSmokeUx7G();
        var originalProject = Project;
        var originalHistory = History;
        var originalRevision = History.State.Revision;
        var originalJournal = History.Journal.ToArray();
        var originalDirty = IsDirty;
        var originalArea = ActiveAreaId;
        var originalSelection = SelectedEntityIds.ToArray();
        var originalSelected = SelectedEntityId;
        var originalFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var selection = CurrentPlaytestLabUx7G();
        if (selection is null) return FailedPlaytestSmokeUx7G();

        var run = RunIsolatedPlaytestUx7G(selection, CancellationToken.None);
        var second = RunIsolatedPlaytestUx7G(selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunIsolatedPlaytestUx7G(selection, cancellation.Token, stage =>
        {
            if (stage.Key == "preview-mode") cancellation.Cancel();
        });

        var routes = run.Stages.Select(stage => stage.Route).ToHashSet(StringComparer.Ordinal);
        var fiveRoutes = routes.SetEquals(["ORF-SHELL-018", "ORF-AREA-036", "ORF-GAME-029",
            "ORF-AI-025", "ORF-AI-028"]);
        var preview = run.Stages.SingleOrDefault(stage => stage.Key == "preview-mode");
        var playFromHere = run.Stages.SingleOrDefault(stage => stage.Key == "play-from-here");
        var replay = run.Stages.SingleOrDefault(stage => stage.Key == "deterministic-replay");
        var agent = run.Stages.SingleOrDefault(stage => stage.Key == "test-agent");
        var combat = run.Stages.SingleOrDefault(stage => stage.Key == "combat-simulation");
        var explicitSelection = selection.ProjectId != Guid.Empty && selection.SourceFingerprint.Length == 64 &&
                                selection.AreaCount > 0 && !string.IsNullOrWhiteSpace(selection.EntryAreaName);
        var previewReady = preview is { Completed: true, Passed: true, Cases: >= 3 } &&
                           preview.Details.Count >= 6;
        var playReady = playFromHere is { Completed: true, Passed: true, Cases: >= 5 } &&
                        playFromHere.Details.Count >= 7;
        var replayReady = replay is { Completed: true, Passed: true, Cases: >= 3 } && replay.Details.Count >= 6;
        var agentReady = agent is { Completed: true, Passed: true, Cases: >= 1 } && agent.Details.Count >= 6;
        var combatReady = combat is { Completed: true, Passed: true, Cases: >= 16 } && combat.Details.Count >= 7;
        var cancellationReady = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                cancelled.Stages[0].Key == "preview-mode" &&
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
                       cancelled.TemporaryWorkspaceReleased && _lastUx7GTemporaryReleased;
        var succeeded = run.Succeeded && second.Succeeded && fiveRoutes && explicitSelection && previewReady &&
                        playReady && replayReady && agentReady && combatReady && cancellationReady && navigation &&
                        deterministic && run.Isolated && run.ArtifactsConfined && sourceUnchanged && released;
        return new StudioUx7GPlaytestSmokeResult(succeeded, fiveRoutes, explicitSelection, previewReady,
            playReady, replayReady, agentReady, combatReady, cancellationReady, navigation, deterministic,
            run.Isolated, run.ArtifactsConfined, sourceUnchanged, released);
    }

    private static StudioUx7GPlaytestStage PreviewModeStageUx7G(
        PlaytestEvidenceUx7G evidence,
        string root)
    {
        Directory.CreateDirectory(root);
        var before = ProjectJsonSerializer.Serialize(evidence.Project);
        var diagnostics = AreaPlaytestProfileValidator.Validate(evidence.Project, evidence.Area, evidence.Profile);
        var store = new PlaytestSnapshotStore(root);
        var snapshot = store.Create(evidence.Project, evidence.Area.Id, evidence.Profile);
        var restored = ProjectJsonSerializer.Deserialize(File.ReadAllText(snapshot.ProjectFilePath));
        var manifest = PlaytestLaunchManifestStore.Load(snapshot.LaunchManifestFilePath!);
        var contained = Path.GetFullPath(snapshot.DirectoryPath).StartsWith(
            Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        var passed = diagnostics.Count == 0 && contained && manifest.Headless &&
                     manifest.AreaId == evidence.Area.Id.ToString() &&
                     manifest.SpawnEntityId == snapshot.SpawnEntityId.ToString() &&
                     restored.Areas.Any(area => area.Id == evidence.Area.Id) &&
                     ProjectJsonSerializer.Serialize(evidence.Project) == before;
        store.Delete(snapshot);
        passed = passed && !Directory.Exists(snapshot.DirectoryPath);
        var details = new[]
        {
            $"Zone · {evidence.Area.DisplayName} · départ {evidence.Profile.StartMode}",
            $"Profil · {evidence.Profile.TestProfile} · cible {evidence.Profile.TargetKind}",
            $"Manifest · headless {(manifest.Headless ? "oui" : "non")} · schéma {manifest.SchemaVersion}",
            $"Snapshot · projet canonique relu · spawn temporaire {(evidence.AddedSpawn ? "créé dans l’isolat" : "réutilisé")}",
            $"Confinement · {(contained ? "copie temporaire" : "hors racine")}",
            "Frontière · aperçu préparé sans lancer Godot Runtime, physique, audio, script ni réseau",
        };
        return StageUx7G("ORF-SHELL-018", "preview-mode", "Aperçu et mode test", passed, 3,
            "Snapshot, manifeste et retour éditeur exercés sans mutation source.", details);
    }

    private EntityId? ResolveRuntimePlaytestSpawnUx7G()
    {
        if (Project is null || ActiveArea is null) return null;
        var blueprints = Project.Blueprints.ToDictionary(item => item.Id);
        return ActiveArea.Entities.FirstOrDefault(entity =>
                   entity.Tags.Contains("player", StringComparer.OrdinalIgnoreCase) ||
                   (blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
                    blueprint.Kind == BlueprintKind.PlayerSpawn))?.Id;
    }

    private static bool CleanupRuntimePlaytestUx7G(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullRoot.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(Path.GetDirectoryName(fullRoot) ?? string.Empty)
                    .Equals("openrpg-forge-studio-runtime-playtest", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx7GRuntimeLaunchResult FailedRuntimeLaunchUx7G(string message) =>
        new(false, false, null, false, true, message);

    private static StudioUx7GPlaytestStage PlayFromHereStageUx7G(PlaytestEvidenceUx7G evidence)
    {
        var state = new ProjectState(evidence.Project, new ProjectRevision(0));
        var handler = new SetAreaPlaytestHandler(new SystemClock());
        var command = new SetAreaPlaytestCommand(
            CommandId.From(StableUx7G(evidence.Project.Id.Value, "play-from-here-command")), state.Revision,
            evidence.Area.Id, evidence.Profile, true);
        var dryRun = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var stale = handler.Handle(applied.State, command with { DryRun = false });
        var history = new ProjectCommandHistory(state);
        if (applied.Succeeded) history.Record(applied);
        var undo = history.Undo();
        var redo = history.Redo();
        var roundTrip = applied.Succeeded
            ? ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(applied.State.Project))
            : evidence.Project;
        var restored = roundTrip.Areas.Single(area => area.Id == evidence.Area.Id).Playtest;
        var passed = dryRun.Succeeded && ReferenceEquals(dryRun.State, state) && applied.Succeeded &&
                     stale.Diagnostics.Any(item => item.Code == SetAreaPlaytestHandler.RevisionConflictCode) &&
                     undo.Succeeded && undo.State.Project.Areas.Single(area => area.Id == evidence.Area.Id).Playtest ==
                     evidence.Area.Playtest && redo.Succeeded &&
                     redo.State.Project.Areas.Single(area => area.Id == evidence.Area.Id).Playtest == evidence.Profile &&
                     restored == evidence.Profile;
        var details = new[]
        {
            $"Commande · dry-run {(dryRun.Succeeded ? "PASS" : "FAILED")} · apply {(applied.Succeeded ? "PASS" : "FAILED")}",
            $"Départ · {evidence.Profile.StartMode} · personnage {evidence.Player.DisplayName}",
            $"Cible · {evidence.Profile.TargetKind} · profil {evidence.Profile.TestProfile}",
            $"Révision périmée · {(stale.Succeeded ? "acceptée à tort" : "refusée")}",
            $"Historique isolé · undo {(undo.Succeeded ? "PASS" : "FAILED")} · redo {(redo.Succeeded ? "PASS" : "FAILED")}",
            $"Round-trip · {(restored == evidence.Profile ? "exact" : "divergent")}",
            "Frontière · aucune scène, quête, dialogue ou session runtime n’est exécuté par le Studio",
        };
        return StageUx7G("ORF-AREA-036", "play-from-here", "Play From Here", passed, 5,
            "Commande, garde de révision, historique et persistance validés dans l’isolat.", details);
    }

    private static StudioUx7GPlaytestStage ReplayStageUx7G(ProjectDefinition project)
    {
        var source = project.Module.RuntimePolicies.Gameplay.Session.Replay;
        var profile = source with
        {
            Enabled = true,
            HeadlessReplay = true,
            EmitMinimalPackage = true,
            IncludePersonalData = false
        };
        var frames = Enumerable.Range(0, 8).Select(index =>
        {
            var state = ReplayStateUx7G(index, $"input-{index}", $"event-{index}", 700 + index);
            return new ReplayFrame(index, $"input-{index}", $"event-{index}", 700 + index,
                GameplaySessionServices.HashState(state));
        }).ToArray();
        var recording = GameplaySessionServices.Record(profile,
            StableUx7G(project.Id.Value, "deterministic-replay"), frames, "studio:ux7-g:replay");
        var pass = GameplaySessionServices.ReplayHeadless(profile, recording,
            frame => ReplayStateUx7G((int)frame.Tick, frame.Input, frame.Event, frame.Seed));
        var second = GameplaySessionServices.ReplayHeadless(profile, recording,
            frame => ReplayStateUx7G((int)frame.Tick, frame.Input, frame.Event, frame.Seed));
        var divergence = GameplaySessionServices.ReplayHeadless(profile, recording,
            frame => frame.Tick == 4 ? "état divergent" :
                ReplayStateUx7G((int)frame.Tick, frame.Input, frame.Event, frame.Seed));
        var passed = pass.Succeeded && second.Succeeded && pass.RecordingHash == second.RecordingHash &&
                     !divergence.Succeeded && divergence.Divergence?.Tick == 4 &&
                     divergence.Divergence.MinimalReproduction.Frames.Count == 5 &&
                     divergence.MinimalPackageManifest?.Contains("privacy=redacted", StringComparison.Ordinal) == true;
        var details = new[]
        {
            $"Replay · {pass.FrameCount} frames · hash {pass.RecordingHash[..12]}",
            $"Entrées · événements · seeds · empreintes d’état comparés deux fois",
            $"Divergence injectée · tick {divergence.Divergence?.Tick} · reproduction minimale {divergence.Divergence?.MinimalReproduction.Frames.Count} frames",
            $"Confidentialité · {(divergence.MinimalPackageManifest?.Contains("privacy=redacted", StringComparison.Ordinal) == true ? "expurgée" : "absente")}",
            "Déterminisme · même empreinte pour deux replays identiques",
            "Frontière · replay headless en mémoire, sans fenêtre, acteur, physique, playback ni transport réseau",
        };
        return StageUx7G("ORF-GAME-029", "deterministic-replay", "Replay deterministic test", passed, 3,
            "Replay conforme, divergence localisée et reproduction minimale expurgée.", details);
    }

    private static StudioUx7GPlaytestStage TestAgentStageUx7G(ProjectDefinition project)
    {
        var profile = AssistantTestAutomationProfile.CreateDefault();
        var first = AssistantScenarioTestService.Run(project, profile);
        var second = AssistantScenarioTestService.Run(project, profile);
        var actionsGoverned = first.GameplayActions.All(AssistantTestAutomationProfile.SupportedGameplayActions.Contains) &&
                              first.GameplayActions.All(action => !action.Contains("shell", StringComparison.OrdinalIgnoreCase));
        var findingsCited = first.Findings.All(finding => finding.Citations.Count > 0 &&
                                                          finding.ReproductionSteps.Count > 0 &&
                                                          !string.IsNullOrWhiteSpace(finding.DocumentPath));
        var coverage = first.Coverage;
        var passed = first.Fingerprint == second.Fingerprint && actionsGoverned && findingsCited &&
                     coverage.Dialogues == project.Dialogues.Count && coverage.Quests == project.Quests.Count;
        var details = new[]
        {
            $"Couverture · dialogues {coverage.Dialogues} · nœuds {coverage.DialogueNodesCovered}/{coverage.DialogueNodesTotal}",
            $"Couverture · quêtes {coverage.Quests} · transitions {coverage.QuestTransitionsCovered}/{coverage.QuestTransitionsTotal}",
            $"Actions autorisées · {string.Join(", ", first.GameplayActions)}",
            $"Constats · {first.Findings.Count} · bloquants {first.Findings.Count(item => item.Severity == AssistantScenarioFindingSeverity.Blocking)}",
            $"Verdict contenu · {(first.Succeeded ? "conforme" : "à corriger")} · runner déterministe {passed}",
            "Frontière · agent en processus, sans shell, écriture, transaction automatique, runtime ou réseau",
        };
        return StageUx7G("ORF-AI-025", "test-agent", "Agent de test", passed,
            Math.Max(1, coverage.Dialogues + coverage.Quests),
            "Couverture et constats déterministes avec actions gameplay strictement autorisées.", details);
    }

    private static StudioUx7GPlaytestStage CombatSimulationStageUx7G(ProjectDefinition project)
    {
        var available = project.Blueprints
            .Where(item => (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) &&
                           item.Combat is not null && item.Combat.Abilities.Any(ability =>
                               ability.Effects.Any(effect => effect.Kind == AbilityEffectKind.Damage)))
            .OrderBy(item => item.Id.Value)
            .Take(4)
            .Select(item => new CombatSimulationArchetype(item.Id, item.DisplayName, item.Combat!))
            .ToList();
        var fallback = available.Count < 2;
        if (fallback)
        {
            available =
            [
                CombatArchetypeUx7G(project.Id.Value, "guardian", "Gardienne témoin", 140, 10, 13, 4),
                CombatArchetypeUx7G(project.Id.Value, "raider", "Assaillant témoin", 105, 12, 16, 2),
            ];
        }
        var ids = available.Select(item => item.BlueprintId).ToArray();
        var profile = CombatSimulationProfile.CreateDefault() with
        {
            Enabled = true,
            RunsPerMatchup = 16,
            FirstSeed = 7301,
            ArchetypeBlueprintIds = ids,
        };
        var distances = new Dictionary<string, float>(StringComparer.Ordinal);
        for (var first = 0; first < ids.Length - 1; first++)
            for (var second = first + 1; second < ids.Length; second++)
                distances[CombatBalanceSimulationService.PairKey(ids[first], ids[second])] = 18 + first + second;
        var report = CombatBalanceSimulationService.Run(profile, available, distances);
        var repeated = CombatBalanceSimulationService.Run(profile, available, distances);
        var disabled = CombatBalanceSimulationService.Run(CombatSimulationProfile.CreateDefault(), [], null);
        var runs = report.Matchups.Sum(item => item.Runs);
        var passed = report.Succeeded && report.Fingerprint == repeated.Fingerprint &&
                     report.Matchups.Count > 0 && report.Matchups.All(item =>
                         item.FirstWins + item.SecondWins + item.Draws == item.Runs &&
                         item.AverageDurationSeconds > 0 && item.AveragePathingSeconds > 0) &&
                     disabled.Diagnostics.Any(item => item.Code == CombatBalanceSimulationService.DisabledCode);
        var firstMatchup = report.Matchups.FirstOrDefault();
        var details = new[]
        {
            $"Archétypes · {string.Join(" / ", available.Select(item => item.Name))}",
            $"Origine · {(fallback ? "fallback canonique dans l’isolat" : "blueprints réels du module")}",
            $"Campagne · {report.Matchups.Count} matchups · {runs} runs · seed {profile.FirstSeed}",
            $"Premier matchup · victoires {firstMatchup?.FirstWins}/{firstMatchup?.SecondWins} · nuls {firstMatchup?.Draws}",
            $"Durée moyenne · {firstMatchup?.AverageDurationSeconds:0.###}s · pathing {firstMatchup?.AveragePathingSeconds:0.###}s",
            $"Déterminisme · hash {report.Fingerprint[..12]} · profil désactivé refusé",
            "Frontière · calcul RPG Kit hors ligne, sans IA live, acteur, animation, moteur, socket ni autorité réseau",
        };
        return StageUx7G("ORF-AI-028", "combat-simulation", "Simulation de combat", passed,
            Math.Max(16, runs), "Matchups, ressources, pathing et phases simulés de façon déterministe.", details);
    }

    private static PlaytestEvidenceUx7G BuildPlaytestEvidenceUx7G(ProjectDefinition source)
    {
        var blueprints = source.Blueprints.ToList();
        var player = blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.PlayerSpawn);
        var addedBlueprint = player is null;
        if (player is null)
        {
            player = new BlueprintDefinition(BlueprintId.From(StableUx7G(source.Id.Value, "player-blueprint")),
                "joueur-temoin-ux7-g", "Joueur témoin UX7-G", BlueprintKind.PlayerSpawn);
            blueprints.Add(player);
        }
        var area = source.Areas.SingleOrDefault(item => item.Id == source.Module.EntryAreaId) ?? source.Areas[0];
        var spawn = area.Entities.FirstOrDefault(item => item.BlueprintId == player.Id);
        var addedSpawn = spawn is null;
        if (spawn is null)
        {
            spawn = new EntityInstance(EntityId.From(StableUx7G(area.Id.Value, "player-spawn")), player.Id,
                TransformValue.Identity, ["playtest", "ux7-g"]);
            area = area.WithEntities(area.Entities.Append(spawn).ToArray());
        }
        var project = addedBlueprint || addedSpawn
            ? new ProjectDefinition(source.Id, source.Title, source.Module, blueprints,
                source.Areas.Select(item => item.Id == area.Id ? area : item).ToArray(),
                source.Dialogues, source.Quests)
            : source;
        var defaults = AreaPlaytestProfile.CreateDefault(area.Id);
        var profile = new AreaPlaytestProfile(defaults.Id, AreaPlaytestProfile.CurrentSchemaVersion,
            "studio:ux7-g", true, false, PlayFromHereStartMode.PlayerSpawn, spawn.Id,
            TransformValue.Identity, null, player.Id, "studio-playtest", [], PlaytestTargetKind.World,
            null, headless: true);
        return new(project, area, player, profile, addedBlueprint, addedSpawn);
    }

    private static CombatSimulationArchetype CombatArchetypeUx7G(Guid projectId, string slug, string name,
        int health, int resource, int offense, int defense)
    {
        var blueprintId = BlueprintId.From(StableUx7G(projectId, $"combat:{slug}"));
        var ability = new AbilityDefinition(AbilityId.From(StableUx7G(projectId, $"combat:{slug}:strike")),
            $"frappe-{slug}", $"Frappe {name}", 2, 0, AbilityTargeting.SingleEnemy,
            [new AbilityEffect(AbilityEffectKind.Damage, 8)], new AbilityVisualCue("combat.strike"));
        return new(blueprintId, name,
            new CombatProfile(new CombatStats(health, resource, offense, defense, slug, slug), [ability]));
    }

    private static StudioUx7GPlaytestStage StageUx7G(string route, string key, string title, bool passed,
        int cases, string summary, IReadOnlyList<string> details) => new(route, key, title, true, passed, cases,
        summary, details, HashJsonUx7D(new { route, key, passed, cases, summary, details }));

    private static string ReplayStateUx7G(int tick, string input, string @event, int seed) =>
        $"tick={tick};input={input};event={@event};seed={seed};x={tick * 3};hp={100 - tick}";

    private static string PlaytestRunFingerprintUx7G(IReadOnlyList<StudioUx7GPlaytestStage> stages)
    {
        var canonical = string.Join('|', stages.Select(stage =>
            $"{stage.Route}:{stage.Key}:{stage.Passed}:{stage.Cases}:{stage.Fingerprint}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static Guid StableUx7G(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}|ux7-g|{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static StudioUx7GPlaytestRunReport FailedPlaytestRunUx7G(string message) =>
        new(false, false, false, false, false, true, string.Empty, [], message);

    private static StudioUx7GPlaytestSmokeResult FailedPlaytestSmokeUx7G() =>
        new(false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, true);

    private sealed record PlaytestEvidenceUx7G(
        ProjectDefinition Project,
        AreaDefinition Area,
        BlueprintDefinition Player,
        AreaPlaytestProfile Profile,
        bool AddedBlueprint,
        bool AddedSpawn);
}
