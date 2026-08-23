using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Domain.Build;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private string? _retainedUx8BRoot;
    private bool _lastUx8BTemporaryReleased = true;

    internal bool LastUx8BTemporaryReleased => _lastUx8BTemporaryReleased;

    internal StudioUx8BMaintenanceSelection? CurrentMaintenanceUx8B()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var loaded = WorkspaceBuildProfileStore.Load(Session.RootDirectory);
            var profile = loaded.Profile;
            var collaboration = profile.Collaboration;
            var ready = loaded.Succeeded &&
                        WorkspaceBuildProfileValidator.Validate(profile).Count == 0 &&
                        WorkspaceBuildCollaborationProfileValidator.Validate(collaboration).Count == 0;
            return new StudioUx8BMaintenanceSelection(
                Project.Id.Value,
                Project.Module.DisplayName,
                SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                ready,
                profile.CachePath,
                collaboration.Cache.MaximumEntries,
                collaboration.References.MaximumNodes,
                collaboration.SemanticDiff.MaximumEntries,
                "cache → normalize → migrate → references → diff · copie temporaire uniquement");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or InvalidDataException or
                                           JsonException or NotSupportedException)
        {
            _ = exception;
            return null;
        }
    }

    internal StudioUx8BMaintenanceReport RunMaintenanceUx8B(
        StudioUx8BMaintenanceSelection selection,
        CancellationToken cancellationToken,
        bool retainWorkspace,
        bool exerciseProof,
        Action? firstStageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedMaintenanceUx8B("Maintenance refusée · aucun module source n’est ouvert.");
        var current = CurrentMaintenanceUx8B();
        if (current is null || current.ProjectId != selection.ProjectId ||
            !string.Equals(current.SourceFingerprint, selection.SourceFingerprint, StringComparison.Ordinal))
            return FailedMaintenanceUx8B("Maintenance refusée · le module source a changé.");
        if (!selection.ProfileReady)
            return FailedMaintenanceUx8B("Maintenance refusée · le profil collaboratif est invalide.");

        ReleaseMaintenanceArtifactsUx8B();
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
        var sourceRoot = Session.RootDirectory;
        var sourceProjectFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var sourceWorkspace = SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(sourceProject), sourceRoot);
        var runRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux8-b",
            Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(runRoot, "workspace");
        var stages = new List<StudioUx8BMaintenanceStage>();
        var isolated = false;
        var cancelled = false;
        var retained = false;
        var message = string.Empty;
        _lastUx8BTemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(workspaceRoot, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire du module n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, workspaceRoot))
                throw new IOException("Les ressources du module n’ont pas pu être copiées dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(workspaceRoot), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);

            var sourceProfile = WorkspaceBuildProfileStore.Load(sourceRoot);
            if (!sourceProfile.Succeeded || !WorkspaceBuildProfileStore.Save(workspaceRoot, sourceProfile.Profile).Succeeded)
                throw new InvalidOperationException("Le profil de build n’a pas pu être projeté dans l’isolat.");
            var profile = sourceProfile.Profile;
            PrepareProofWorkspaceUx8B(workspaceRoot, exerciseProof);

            cancellationToken.ThrowIfCancellationRequested();
            var projectBeforeCache = File.ReadAllBytes(Path.Combine(workspaceRoot,
                ProjectSession.DefaultProjectFileName));
            var cachePreview = WorkspaceBuildCollaborationService.RebuildCacheIndex(
                workspaceRoot, profile, dryRun: true);
            var cacheIndexed = WorkspaceBuildCollaborationService.RebuildCacheIndex(
                workspaceRoot, profile, dryRun: false);
            var cacheInvalidated = WorkspaceBuildCollaborationService.InvalidateCache(
                workspaceRoot, profile, dryRun: false);
            var cacheEmpty = !Directory.Exists(cacheIndexed.CacheDirectory) ||
                             !Directory.EnumerateFiles(cacheIndexed.CacheDirectory, "*",
                                 SearchOption.AllDirectories).Any();
            var cachePassed = cachePreview.Succeeded && cacheIndexed.Succeeded && cacheInvalidated.Succeeded &&
                              cacheEmpty && projectBeforeCache.SequenceEqual(File.ReadAllBytes(Path.Combine(
                                  workspaceRoot, ProjectSession.DefaultProjectFileName)));
            stages.Add(new StudioUx8BMaintenanceStage(
                "ORF-BUILD-006", "disposable-cache", "Cache jetable", cachePassed,
                cachePassed
                    ? "Le cache a été indexé par SHA-256 puis invalidé sans toucher aux documents canoniques."
                    : "Le cache isolé n’a pas pu être reconstruit et invalidé proprement.",
                [
                    $"Inventaire · {cacheIndexed.Entries.Count} entrée(s) · limite {profile.Collaboration.Cache.MaximumEntries:N0}",
                    $"Index · {profile.Collaboration.Cache.IndexFileName} · cache {profile.CachePath}",
                    "Après invalidation · aucun fichier restant · reconstruction possible depuis la source",
                ], BuildDiagnosticsLogUx8B(cachePreview.Diagnostics, cacheIndexed.Diagnostics,
                    cacheInvalidated.Diagnostics)));
            firstStageCompleted?.Invoke();

            cancellationToken.ThrowIfCancellationRequested();
            var normalizePreview = WorkspaceBuildCollaborationService.NormalizeDocuments(
                workspaceRoot, profile, dryRun: true);
            var normalized = WorkspaceBuildCollaborationService.NormalizeDocuments(
                workspaceRoot, profile, dryRun: false);
            var normalizeSecond = WorkspaceBuildCollaborationService.NormalizeDocuments(
                workspaceRoot, profile, dryRun: true);
            var normalizationPassed = normalizePreview.Succeeded && normalized.Succeeded &&
                                      normalizeSecond.Succeeded && normalizeSecond.Changes.Count == 0 &&
                                      (!exerciseProof || normalizePreview.Changes.Any(change =>
                                          change.RelativePath == "data/ux8-b-normalization.json"));
            stages.Add(new StudioUx8BMaintenanceStage(
                "ORF-BUILD-008", "document-normalization", "Normalisation des documents",
                normalizationPassed,
                normalizationPassed
                    ? "La prévisualisation a été appliquée atomiquement dans la copie; le second passage est vide."
                    : "La normalisation canonique ou son idempotence a échoué.",
                [
                    $"Prévisualisation · {normalizePreview.Changes.Count} document(s) à normaliser",
                    "Format · JSON canonique · UTF-8 sans BOM · LF · ordre et nombres stables",
                    $"Second passage · {normalizeSecond.Changes.Count} changement · source ouverte inchangée",
                ], BuildDiagnosticsLogUx8B(normalizePreview.Diagnostics, normalized.Diagnostics,
                    normalizeSecond.Diagnostics)));

            cancellationToken.ThrowIfCancellationRequested();
            var migrationPreview = WorkspaceBuildCollaborationService.MigrateWorkspace(
                workspaceRoot, profile, dryRun: true);
            var migrated = WorkspaceBuildCollaborationService.MigrateWorkspace(
                workspaceRoot, profile, dryRun: false);
            var migrationSecond = WorkspaceBuildCollaborationService.MigrateWorkspace(
                workspaceRoot, profile, dryRun: true);
            var migrationPassed = migrationPreview.Succeeded && migrated.Succeeded && migrated.Idempotent &&
                                  migrationSecond.Succeeded && migrationSecond.ChangedDocuments.Count == 0 &&
                                  (!exerciseProof || migrated.Applied && migrated.BackupDirectory is not null);
            stages.Add(new StudioUx8BMaintenanceStage(
                "ORF-BUILD-009", "workspace-migration", "Migrations", migrationPassed,
                migrationPassed
                    ? "Le plan versionné a été appliqué avec sauvegarde dans l’isolat puis relu sans nouveau delta."
                    : "La migration, sa sauvegarde ou sa preuve d’idempotence a échoué.",
                [
                    $"Version · {migrationPreview.FromVersion} → {migrationPreview.ToVersion}",
                    $"Plan · {migrationPreview.ChangedDocuments.Count} document(s) · application {(migrated.Applied ? "effectuée" : "inutile")}",
                    $"Sauvegarde · {(migrated.BackupDirectory ?? "aucune nécessaire")} · downgrade non simulé",
                ], BuildDiagnosticsLogUx8B(migrationPreview.Diagnostics, migrated.Diagnostics,
                    migrationSecond.Diagnostics)));

            cancellationToken.ThrowIfCancellationRequested();
            var opened = ProjectSession.Open(workspaceRoot);
            if (!opened.Succeeded || opened.Session is null)
                throw new InvalidOperationException("La copie maintenue n’est pas relisible.");
            var referenceIndex = WorkspaceBuildCollaborationService.BuildReferenceIndex(
                workspaceRoot, opened.Session.Project, opened.Session.ProjectFilePath, profile.Collaboration);
            var forward = referenceIndex.Nodes.Sum(node => node.Dependencies.Count);
            var reverse = referenceIndex.Nodes.Sum(node => node.Dependents.Count);
            var referencesPassed = referenceIndex.Diagnostics.Count == 0 && referenceIndex.Nodes.Count > 0 &&
                                   forward > 0 && reverse > 0 && forward == reverse;
            stages.Add(new StudioUx8BMaintenanceStage(
                "ORF-BUILD-010", "reverse-references", "Références inverses", referencesPassed,
                referencesPassed
                    ? "Le graphe a été reconstruit depuis la source avec dépendances avant et dépendants inverses."
                    : "Le graphe de références n’a pas produit les deux directions attendues.",
                [
                    $"Graphe · {referenceIndex.Nodes.Count} nœud(s) · limite {profile.Collaboration.References.MaximumNodes:N0}",
                    $"Arêtes · {forward} avant · {reverse} inverses · ordre stable",
                    $"Identité · {ShortHashUx8B(referenceIndex.GraphId)} · aucun index source requis",
                ], BuildDiagnosticsLogUx8B(referenceIndex.Diagnostics)));

            cancellationToken.ThrowIfCancellationRequested();
            var graphBefore = WorkspaceBuildGraphBuilder.Build(
                workspaceRoot, opened.Session.Project, opened.Session.ProjectFilePath).Graph;
            var currentBefore = WorkspaceBuildCollaborationService.CreateSemanticSnapshot(
                opened.Session.Project, graphBefore);
            var stored = WorkspaceBuildCollaborationService.LoadSemanticSnapshot(sourceRoot);
            var baseline = stored.Count > 0 ? stored : currentBefore;
            WorkspaceBuildCollaborationService.SaveSemanticSnapshot(workspaceRoot, baseline);
            if (exerciseProof) MutateProofScriptUx8B(workspaceRoot);
            var reopened = ProjectSession.Open(workspaceRoot);
            if (!reopened.Succeeded || reopened.Session is null)
                throw new InvalidOperationException("La copie du diff sémantique n’est pas relisible.");
            var graphAfter = WorkspaceBuildGraphBuilder.Build(
                workspaceRoot, reopened.Session.Project, reopened.Session.ProjectFilePath).Graph;
            var currentAfter = WorkspaceBuildCollaborationService.CreateSemanticSnapshot(
                reopened.Session.Project, graphAfter);
            var semanticDiff = WorkspaceBuildCollaborationService.Diff(
                baseline, currentAfter, profile.Collaboration);
            var snapshotRoundTrip = WorkspaceBuildCollaborationService.LoadSemanticSnapshot(workspaceRoot);
            var semanticPassed = semanticDiff.Succeeded && snapshotRoundTrip.Count == baseline.Count &&
                                 (!exerciseProof || semanticDiff.Changes.Any(change =>
                                     change.Domain == "script" && change.Kind is
                                         WorkspaceSemanticChangeKind.Added or WorkspaceSemanticChangeKind.Modified));
            var byDomain = semanticDiff.Changes.GroupBy(change => change.Domain)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}").ToArray();
            stages.Add(new StudioUx8BMaintenanceStage(
                "ORF-BUILD-011", "semantic-diff", "Diff sémantique", semanticPassed,
                semanticPassed
                    ? "Le snapshot jetable et le diff ordonné décrivent les changements métier sans écrire la source."
                    : "Le snapshot ou le diff sémantique structuré a échoué.",
                [
                    $"Snapshot · {baseline.Count} ressource(s) · limite {profile.Collaboration.SemanticDiff.MaximumEntries:N0}",
                    $"Changements · {semanticDiff.Changes.Count} · {(byDomain.Length == 0 ? "aucun" : string.Join(", ", byDomain))}",
                    "Domaines · zones · graphes · entités · packages · scripts · texte et JSON",
                ], semanticDiff.Text + "\n" + BuildDiagnosticsLogUx8B(semanticDiff.Diagnostics)));

            var allPassed = stages.Count == 5 && stages.All(stage => stage.Passed);
            if (retainWorkspace && allPassed)
            {
                _retainedUx8BRoot = runRoot;
                retained = true;
            }
            message = allPassed
                ? "Maintenance terminée dans la copie · cinq résultats conformes et source ouverte inchangée."
                : "Maintenance terminée avec un résultat à examiner.";
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            message = "Maintenance annulée entre deux opérations · copie temporaire en cours de libération.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or InvalidDataException or
                                           JsonException or NotSupportedException)
        {
            message = "Maintenance interrompue · " + exception.Message;
        }
        finally
        {
            if (!retained)
                _lastUx8BTemporaryReleased = DeleteTemporaryBlueprintUx3A(runRoot);
        }

        var sourceUntouched = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              History?.CanUndo == sourceCanUndo && History?.CanRedo == sourceCanRedo &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) && SelectedEntityId == sourceSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == sourceProjectFile &&
                              SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project!), sourceRoot) == sourceWorkspace;
        var succeeded = !cancelled && stages.Count == 5 && stages.All(stage => stage.Passed) &&
                        isolated && sourceUntouched;
        return new StudioUx8BMaintenanceReport(
            succeeded, cancelled, isolated, sourceUntouched,
            retained ? false : _lastUx8BTemporaryReleased,
            retained, retained ? workspaceRoot : string.Empty, stages, message);
    }

    internal void ReleaseMaintenanceArtifactsUx8B()
    {
        var root = _retainedUx8BRoot;
        _retainedUx8BRoot = null;
        _lastUx8BTemporaryReleased = string.IsNullOrWhiteSpace(root) ||
                                     DeleteTemporaryBlueprintUx3A(root);
    }

    internal void CloseUx8B() => ReleaseMaintenanceArtifactsUx8B();

    internal StudioUx8BMaintenanceSmokeResult RunUx8BContractSmoke()
    {
        var selection = CurrentMaintenanceUx8B();
        if (selection is null) return FailedMaintenanceSmokeUx8B("Sélection UX8-B indisponible.");
        var report = RunMaintenanceUx8B(selection, CancellationToken.None,
            retainWorkspace: false, exerciseProof: true);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunMaintenanceUx8B(selection, cancellation.Token,
            retainWorkspace: false, exerciseProof: true, cancellation.Cancel);
        var routes = report.Stages.Select(stage => stage.Route).ToArray();
        var expected = new[]
        {
            "ORF-BUILD-006", "ORF-BUILD-008", "ORF-BUILD-009", "ORF-BUILD-010", "ORF-BUILD-011",
        };
        var cache = PassedStageUx8B(report, "ORF-BUILD-006");
        var normalization = PassedStageUx8B(report, "ORF-BUILD-008");
        var migration = PassedStageUx8B(report, "ORF-BUILD-009");
        var references = PassedStageUx8B(report, "ORF-BUILD-010");
        var diff = PassedStageUx8B(report, "ORF-BUILD-011");
        var cancellationReady = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                cancelled.TemporaryWorkspaceReleased && cancelled.SourceUntouched;
        var released = report.TemporaryWorkspaceReleased && cancelled.TemporaryWorkspaceReleased &&
                       LastUx8BTemporaryReleased;
        var succeeded = report.Succeeded && routes.SequenceEqual(expected) && cache && normalization &&
                        migration && references && diff && cancellationReady && report.Isolated &&
                        report.SourceUntouched && released;
        return new StudioUx8BMaintenanceSmokeResult(
            succeeded, routes.SequenceEqual(expected), selection.ProfileReady, cache, normalization, migration,
            references, diff, cancellationReady, report.Isolated, report.SourceUntouched, released,
            report.Message);
    }

    private static void PrepareProofWorkspaceUx8B(string workspaceRoot, bool exerciseProof)
    {
        if (!exerciseProof) return;
        var cache = Path.Combine(workspaceRoot, ".forge");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "ux8-b-cache.bin"), "reconstructible", new UTF8Encoding(false));
        var data = Path.Combine(workspaceRoot, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "ux8-b-normalization.json"),
            "{\r\n  \"z\": 1.00,\r\n  \"a\": { \"b\": true, \"a\": 2 }\r\n}\r\n",
            new UTF8Encoding(true));
        var projectPath = Path.Combine(workspaceRoot, ProjectSession.DefaultProjectFileName);
        var project = JsonNode.Parse(File.ReadAllText(projectPath))?.AsObject() ??
                      throw new JsonException("Le projet de preuve est illisible.");
        project["formatVersion"] = "1.26.0";
        File.WriteAllText(projectPath, project.ToJsonString(), new UTF8Encoding(false));
    }

    private static void MutateProofScriptUx8B(string workspaceRoot)
    {
        var script = Directory.EnumerateFiles(workspaceRoot, "*.forge", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (script is null)
        {
            var scripts = Path.Combine(workspaceRoot, "scripts");
            Directory.CreateDirectory(scripts);
            script = Path.Combine(scripts, "ux8-b-proof.forge");
            File.WriteAllText(script, "void main() {}\n", new UTF8Encoding(false));
            return;
        }
        File.AppendAllText(script, "\n// ux8-b isolated semantic diff\n", new UTF8Encoding(false));
    }

    private static bool PassedStageUx8B(StudioUx8BMaintenanceReport report, string route) =>
        report.Stages.SingleOrDefault(stage => stage.Route == route)?.Passed == true;

    private static string BuildDiagnosticsLogUx8B(
        params IReadOnlyList<BuildCollaborationDiagnostic>[] groups)
    {
        var diagnostics = groups.SelectMany(group => group).ToArray();
        return diagnostics.Length == 0
            ? "Aucun diagnostic canonique."
            : string.Join('\n', diagnostics.Select(item => $"{item.Code} · {item.Path} · {item.Message}"));
    }

    private static string ShortHashUx8B(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "indisponible";
        if (value.Length <= 12) return value;
        return value[..12] + "…";
    }

    private static StudioUx8BMaintenanceReport FailedMaintenanceUx8B(string message) =>
        new(false, false, false, false, true, false, string.Empty, [], message);

    private static StudioUx8BMaintenanceSmokeResult FailedMaintenanceSmokeUx8B(string message) =>
        new(false, false, false, false, false, false, false, false, false, false, false, true, message);
}
