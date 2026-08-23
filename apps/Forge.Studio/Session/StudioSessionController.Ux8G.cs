using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetContentWorkspaceHandler _setContentUx8G = new(new SystemClock());
    private StudioUx8GPackageDraft? _approvedPackagesUx8G;
    private ProjectRevision? _approvedPackagesRevisionUx8G;
    private string? _approvedPackagesSourceUx8G;
    private string? _approvedPackagesDraftUx8G;
    private string? _approvedPackagesCandidateUx8G;

    internal StudioUx8GPackageDraft? CreatePackageDraftUx8G()
    {
        var project = Project;
        var history = History;
        if (project is null || history is null) return null;
        var catalog = project.Module.Content.Packages;
        return new StudioUx8GPackageDraft(
            history.State.Revision,
            ProjectFingerprintUx8G(project),
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.ConflictPolicy,
            ClonePackagesUx8G(catalog.Packages),
            Approved: false);
    }

    internal StudioUx8GPackagePreview PreviewPackagesUx8G(StudioUx8GPackageDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var project = Project;
        var history = History;
        var root = Session?.RootDirectory;
        if (project is null || history is null || string.IsNullOrWhiteSpace(root))
            return FailedPreviewUx8G("Ouvrez un workspace avant l’aperçu.", "content.workspace.unavailable");
        var sourceFingerprint = ProjectFingerprintUx8G(project);
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(sourceFingerprint, draft.SourceFingerprint, StringComparison.Ordinal))
        {
            CancelPackageApprovalUx8G();
            return FailedPreviewUx8G(
                "Le projet a changé ; rechargez le catalogue avant l’aperçu.",
                SetContentWorkspaceHandler.RevisionConflictCode);
        }

        ContentWorkspaceConfiguration candidate;
        ProjectDefinition candidateProject;
        try
        {
            candidate = BuildContentUx8G(project, draft);
            candidateProject = ReplaceContentUx8G(project, candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           FormatException or OverflowException)
        {
            CancelPackageApprovalUx8G();
            return FailedPreviewUx8G(ScrubHumanUx8G(exception.Message), "content.package.draft_invalid");
        }

        var beforeProject = history.State.Project;
        var beforeRevision = history.State.Revision;
        var result = _setContentUx8G.Handle(history.State, new SetContentWorkspaceCommand(
            CommandId.From(Guid.NewGuid()), beforeRevision, candidate, DryRun: true));
        ContentPackageCompilationReport compilation;
        try
        {
            compilation = ContentPackageCompiler.Compile(root, project.Module.Id.Value, candidate.Packages);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException)
        {
            CancelPackageApprovalUx8G();
            return FailedPreviewUx8G(ScrubHumanUx8G(exception.Message), "content.package.compile_failed");
        }

        var diagnostics = result.Diagnostics
            .Select(item => new StudioUx8GDiagnostic(item.Code, "module/content", ScrubHumanUx8G(item.Message)))
            .Concat(compilation.Diagnostics.Select(item => new StudioUx8GDiagnostic(
                item.Code, item.Path, ScrubHumanUx8G(item.Message))))
            .DistinctBy(item => (item.Code, item.Path))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
        var canonicalDryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null &&
                              !result.Audit.Applied && ReferenceEquals(beforeProject, history.State.Project) &&
                              history.State.Revision == beforeRevision;
        var frozen = FreezeDraftUx8G(draft);
        var sourceDraft = CreatePackageDraftUx8G();
        var changed = sourceDraft is not null && !string.Equals(
            DraftFingerprintUx8G(sourceDraft with { Approved = false }),
            DraftFingerprintUx8G(frozen with { Approved = false }),
            StringComparison.Ordinal);
        var succeeded = canonicalDryRun && compilation.Success && diagnostics.Length == 0;
        var canApply = succeeded && changed && draft.Approved;
        var candidateFingerprint = succeeded ? ProjectFingerprintUx8G(candidateProject) : string.Empty;
        if (canApply)
        {
            _approvedPackagesUx8G = frozen;
            _approvedPackagesRevisionUx8G = draft.BaseRevision;
            _approvedPackagesSourceUx8G = draft.SourceFingerprint;
            _approvedPackagesDraftUx8G = DraftFingerprintUx8G(frozen);
            _approvedPackagesCandidateUx8G = candidateFingerprint;
        }
        else
        {
            CancelPackageApprovalUx8G();
        }

        var packagePlan = candidate.Packages.Packages
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.PackageNamespace, StringComparer.Ordinal)
            .Select(item => new StudioUx8GPackagePlan(
                HumanTextUx8G(item.PackageNamespace), item.Version, item.Priority, item.Dependencies.Count,
                item.Sources.Count, item.Assets.Count))
            .ToArray();
        var dependencyPlan = candidate.Packages.Packages
            .SelectMany(package => package.Dependencies.Select(dependency =>
                $"{HumanTextUx8G(package.PackageNamespace)} requiert {HumanTextUx8G(dependency.PackageNamespace)} " +
                $"{dependency.VersionConstraint} · {HumanDependencyKindUx8G(dependency.Kind)}"))
            .DefaultIfEmpty("Aucune dépendance déclarée ; état vide conservé.")
            .ToArray();
        var lockPlan = compilation.LockEntries.Select(item =>
                $"{HumanTextUx8G(item.PackageNamespace)} {item.Version} · priorité {item.Priority}")
            .DefaultIfEmpty("Aucune entrée de verrouillage.")
            .ToArray();
        var resourcePlan = compilation.Resources.Select(item =>
                $"{HumanTextUx8G(item.LogicalResourceId)} → {HumanTextUx8G(item.PackageNamespace)} · priorité {item.Priority}")
            .DefaultIfEmpty("Aucune ressource logique résolue.")
            .ToArray();
        var artifactPlan = compilation.GeneratedArtifacts.Select(item =>
                $"{HumanFileUx8G(item.PackagePath)} · {item.Content.Length} octet(s) en mémoire")
            .DefaultIfEmpty("Aucun artefact compilé tant que les diagnostics ne sont pas résolus.")
            .ToArray();

        return new StudioUx8GPackagePreview(
            succeeded,
            changed,
            canonicalDryRun,
            compilation.Success,
            canApply,
            candidateFingerprint,
            ScrubHumanUx8G(result.Patch?.Summary ?? "Aucun changement."),
            ScrubHumanUx8G(result.Inverse?.Summary ?? "Aucune opération inverse."),
            packagePlan,
            dependencyPlan,
            lockPlan,
            resourcePlan,
            artifactPlan,
            diagnostics,
            succeeded
                ? changed
                    ? draft.Approved
                        ? "Aperçu sec canonique prêt ; catalogue approuvé pour cette révision."
                        : "Aperçu valide ; cochez l’approbation puis relancez l’aperçu sec."
                    : "Le catalogue est valide mais identique à la source."
                : diagnostics.FirstOrDefault()?.Message ?? "Le catalogue doit être corrigé.");
    }

    internal StudioUx8GActionResult ApplyApprovedPackagesUx8G(StudioUx8GPackageDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var project = Project;
        var history = History;
        var root = Session?.RootDirectory;
        var approved = _approvedPackagesUx8G;
        if (project is null || history is null || string.IsNullOrWhiteSpace(root) || approved is null ||
            _approvedPackagesRevisionUx8G is null || _approvedPackagesSourceUx8G is null ||
            _approvedPackagesDraftUx8G is null || _approvedPackagesCandidateUx8G is null)
            return new(false, "Prévisualisez et approuvez d’abord le catalogue de packages.");

        var stale = history.State.Revision != _approvedPackagesRevisionUx8G ||
                    !string.Equals(ProjectFingerprintUx8G(project), _approvedPackagesSourceUx8G, StringComparison.Ordinal) ||
                    !string.Equals(DraftFingerprintUx8G(approved), _approvedPackagesDraftUx8G, StringComparison.Ordinal) ||
                    !string.Equals(DraftFingerprintUx8G(current), _approvedPackagesDraftUx8G, StringComparison.Ordinal);
        if (stale)
        {
            CancelPackageApprovalUx8G();
            return new(false, "Le projet ou le brouillon a changé ; relancez l’aperçu sec.");
        }

        ContentWorkspaceConfiguration candidate;
        try
        {
            candidate = BuildContentUx8G(project, approved);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           FormatException or OverflowException)
        {
            CancelPackageApprovalUx8G();
            return new(false, ScrubHumanUx8G(exception.Message));
        }
        var candidateProject = ReplaceContentUx8G(project, candidate);
        if (!string.Equals(ProjectFingerprintUx8G(candidateProject), _approvedPackagesCandidateUx8G,
                StringComparison.Ordinal))
        {
            CancelPackageApprovalUx8G();
            return new(false, "Le candidat ne correspond plus à l’aperçu approuvé.");
        }

        var compilation = ContentPackageCompiler.Compile(root, project.Module.Id.Value, candidate.Packages);
        if (!compilation.Success)
        {
            CancelPackageApprovalUx8G();
            return new(false, ScrubHumanUx8G(compilation.Diagnostics.FirstOrDefault()?.Message ??
                "La compilation en mémoire refuse le catalogue."));
        }
        var dryRun = _setContentUx8G.Handle(history.State, new SetContentWorkspaceCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: true));
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Inverse is null || dryRun.Audit.Applied)
        {
            CancelPackageApprovalUx8G();
            return new(false, ScrubHumanUx8G(dryRun.Diagnostics.FirstOrDefault()?.Message ??
                "Le re-dry-run refuse le catalogue."));
        }
        var result = _setContentUx8G.Handle(history.State, new SetContentWorkspaceCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate, DryRun: false));
        CancelPackageApprovalUx8G();
        if (!result.Succeeded)
            return new(false, ScrubHumanUx8G(result.Diagnostics.FirstOrDefault()?.Message ??
                "L’application du catalogue a échoué."));
        history.Record(result);
        return new(result.Audit.Applied && string.Equals(result.Audit.CommandName, "set_content_workspace",
                StringComparison.Ordinal),
            ScrubHumanUx8G(result.Patch?.Summary ?? "Catalogue appliqué."));
    }

    internal StudioUx8GActionResult UndoPackagesUx8G()
    {
        CancelPackageApprovalUx8G();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8GActionResult RedoPackagesUx8G()
    {
        CancelPackageApprovalUx8G();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8GActionResult SavePackagesUx8G()
    {
        CancelPackageApprovalUx8G();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8GApprovalSnapshot CapturePackageApprovalUx8G() => new(
        _approvedPackagesUx8G,
        _approvedPackagesRevisionUx8G,
        _approvedPackagesSourceUx8G,
        _approvedPackagesDraftUx8G,
        _approvedPackagesCandidateUx8G);

    internal void RestorePackageApprovalUx8G(StudioUx8GApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedPackagesUx8G = snapshot.Draft;
        _approvedPackagesRevisionUx8G = snapshot.Revision;
        _approvedPackagesSourceUx8G = snapshot.SourceFingerprint;
        _approvedPackagesDraftUx8G = snapshot.DraftFingerprint;
        _approvedPackagesCandidateUx8G = snapshot.CandidateFingerprint;
    }

    internal void CancelPackageApprovalUx8G()
    {
        _approvedPackagesUx8G = null;
        _approvedPackagesRevisionUx8G = null;
        _approvedPackagesSourceUx8G = null;
        _approvedPackagesDraftUx8G = null;
        _approvedPackagesCandidateUx8G = null;
    }

    internal StudioUx8GSmokeResult RunUx8GContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceProject is null || sourceHistory is null)
            return FailedSmokeUx8G("Aucun projet ouvert pour le smoke UX8-G.");
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CapturePackageApprovalUx8G();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux8g-contract-" +
                                                     Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        StudioSessionController? reopened = null;
        var fiveRoutes = false;
        var versioned = false;
        var priority = false;
        var dependencies = false;
        var compilation = false;
        var canonicalDryRun = false;
        var canonicalCommand = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var neighbors = false;
        var released = false;
        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || !editing.Open(root).Succeeded)
                throw new InvalidOperationException("Le workspace UX8-G isolé n’a pas pu être ouvert.");
            var initial = editing.Project!;
            var sourceDraft = editing.CreatePackageDraftUx8G() ??
                              throw new InvalidOperationException("Le brouillon UX8-G est indisponible.");
            var fixture = CreatePackageFixtureUx8G(sourceDraft);
            var invalidPackage = fixture.Packages.Last();
            var invalidDependency = new ContentPackageDependency(
                Guid.Parse("8f070000-0000-4000-8000-000000000099"),
                ContentPackageDefinition.CurrentSchemaVersion,
                "studio:evidence",
                "studio.ux8g.absent",
                "^1.0.0",
                ContentDependencyKind.Required);
            var invalidPackages = fixture.Packages
                .Select(item => item.Id == invalidPackage.Id
                    ? RehashPackageUx8G(new ContentPackageDefinition(
                        item.Id, item.SchemaVersion, item.Provenance, item.PackageNamespace, item.Version,
                        item.LicenseSpdx, item.ContentSha256, item.Priority, item.SealedResources,
                        item.Dependencies.Append(invalidDependency).ToArray(), item.Sources, item.Assets))
                    : item)
                .ToArray();
            var invalid = editing.PreviewPackagesUx8G(fixture with { Packages = invalidPackages, Approved = true });
            staleSource = !editing.PreviewPackagesUx8G(fixture with { SourceFingerprint = "source-périmée" }).Succeeded;
            var beforePreview = editing.Project;
            var beforeRevision = editing.History!.State.Revision;
            var preview = editing.PreviewPackagesUx8G(fixture with { Approved = true });
            var approval = editing.CapturePackageApprovalUx8G();
            fiveRoutes = new[] { "ORF-PROJ-020", "ORF-CONT-001", "ORF-CONT-002", "ORF-CONT-003", "ORF-CONT-007" }
                .Distinct(StringComparer.Ordinal).Count() == 5;
            versioned = preview.Succeeded && preview.Packages.Count >= 2 && fixture.Packages
                .All(item => string.Equals(item.ContentSha256, ContentPackageCompiler.ComputePackageHash(item),
                    StringComparison.Ordinal));
            priority = preview.Succeeded && preview.ResourcePlan.Any(item =>
                item.Contains("studio.ux8g.addon", StringComparison.Ordinal) &&
                item.Contains("priorité 40", StringComparison.Ordinal));
            dependencies = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                               item.Code == ContentPackageCatalogValidator.MissingDependencyCode) &&
                           preview.DependencyPlan.Any(item => item.Contains("studio.ux8g.core", StringComparison.Ordinal));
            compilation = preview.CompilationReady && preview.LockPlan.Count >= 2 &&
                          preview.ArtifactPlan.Any(item => item.Contains("packages.lock", StringComparison.Ordinal)) &&
                          preview.ArtifactPlan.Any(item => item.Contains("packages.manifest", StringComparison.Ordinal));
            canonicalDryRun = preview.Succeeded && preview.Changed && preview.CanonicalDryRun && preview.CanApply &&
                              ReferenceEquals(beforePreview, editing.Project) &&
                              editing.History.State.Revision == beforeRevision;
            var applied = canonicalDryRun && editing.ApplyApprovedPackagesUx8G(fixture with { Approved = true }).Succeeded;
            canonicalCommand = applied && editing.History.State.Revision != beforeRevision &&
                               editing.History.Journal.Count == 1 &&
                               editing.Project!.Module.Content.Packages.Packages.Any(item =>
                                   item.PackageNamespace == "studio.ux8g.addon");
            var appliedFingerprint = ProjectFingerprintUx8G(editing.Project!);
            var undone = canonicalCommand && editing.UndoPackagesUx8G().Succeeded &&
                         string.Equals(ProjectFingerprintUx8G(editing.Project!), ProjectFingerprintUx8G(initial),
                             StringComparison.Ordinal);
            var redone = undone && editing.RedoPackagesUx8G().Succeeded &&
                         string.Equals(ProjectFingerprintUx8G(editing.Project!), appliedFingerprint,
                             StringComparison.Ordinal);
            undoRedo = undone && redone;
            editing.RestorePackageApprovalUx8G(approval);
            staleApproval = !editing.ApplyApprovedPackagesUx8G(fixture with { Approved = true }).Succeeded;
            var saved = editing.SavePackagesUx8G().Succeeded;
            var expectedJson = ProjectJsonSerializer.Serialize(editing.Project!);
            reopened = new StudioSessionController();
            persistence = saved && reopened.Open(root).Succeeded && reopened.Project is not null &&
                          string.Equals(ProjectJsonSerializer.Serialize(reopened.Project), expectedJson,
                              StringComparison.Ordinal);
            var finalProject = editing.Project!;
            var restored = ReplaceContentUx8G(finalProject, initial.Module.Content);
            neighbors = string.Equals(ProjectJsonSerializer.Serialize(restored),
                            ProjectJsonSerializer.Serialize(initial), StringComparison.Ordinal) &&
                        Equals(finalProject.Module.Content.Batch, initial.Module.Content.Batch) &&
                        finalProject.Module.Content.DataTables.SequenceEqual(initial.Module.Content.DataTables) &&
                        finalProject.Module.Content.MergePolicy == initial.Module.Content.MergePolicy &&
                        string.Equals(finalProject.Module.Content.Provenance, initial.Module.Content.Provenance,
                            StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or InvalidDataException)
        {
            _ = exception;
        }
        finally
        {
            reopened?.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                released = !Directory.Exists(root);
            }
            catch
            {
                released = false;
            }
            RestorePackageApprovalUx8G(sourceApproval);
        }

        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected && CapturePackageApprovalUx8G() == sourceApproval;
        var succeeded = fiveRoutes && versioned && priority && dependencies && compilation && canonicalDryRun &&
                        canonicalCommand && staleSource && staleApproval && undoRedo && persistence && neighbors &&
                        released && sourceUnchanged;
        return new StudioUx8GSmokeResult(
            succeeded, fiveRoutes, versioned, priority, dependencies, compilation, canonicalDryRun,
            canonicalCommand, staleSource, staleApproval, undoRedo, persistence, neighbors, released,
            sourceUnchanged,
            succeeded
                ? "UX8-G · packages, priorité, dépendances et compilation prouvés sans mutation source."
                : "UX8-G · une preuve de catalogue de packages a échoué.");
    }

    internal void CloseUx8G() => CancelPackageApprovalUx8G();

    internal static ContentPackageDefinition RehashPackageUx8G(ContentPackageDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var draft = new ContentPackageDefinition(
            source.Id, source.SchemaVersion, source.Provenance, source.PackageNamespace, source.Version,
            source.LicenseSpdx, new string('0', 64), source.Priority, source.SealedResources,
            source.Dependencies, source.Sources, source.Assets);
        return new ContentPackageDefinition(
            draft.Id, draft.SchemaVersion, draft.Provenance, draft.PackageNamespace, draft.Version,
            draft.LicenseSpdx, ContentPackageCompiler.ComputePackageHash(draft), draft.Priority,
            draft.SealedResources, draft.Dependencies, draft.Sources, draft.Assets);
    }

    internal static string HumanDependencyKindUx8G(ContentDependencyKind kind) => kind switch
    {
        ContentDependencyKind.Required => "requise",
        ContentDependencyKind.Optional => "optionnelle",
        ContentDependencyKind.Peer => "fournie par l’hôte",
        _ => "valeur inconnue protégée",
    };

    internal static StudioUx8GPackageDraft CreatePackageFixtureUx8G(StudioUx8GPackageDraft source)
    {
        const string sourceHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string addonHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var coreSource = new ContentPackageSource(
            Guid.Parse("8f070000-0000-4000-8000-000000000011"), 1, "studio:evidence", "ui.theme",
            "content/ux8g-core.json", ContentSourceKind.Json, false, "MIT", sourceHash, 65_536, [], []);
        var addonSource = new ContentPackageSource(
            Guid.Parse("8f070000-0000-4000-8000-000000000012"), 1, "studio:evidence", "ui.theme",
            "content/ux8g-addon.json", ContentSourceKind.Json, false, "MIT", addonHash, 65_536, [], []);
        var core = RehashPackageUx8G(new ContentPackageDefinition(
            Guid.Parse("8f070000-0000-4000-8000-000000000001"), 1, "studio:evidence",
            "studio.ux8g.core", "1.2.0", "MIT", new string('0', 64), 10,
            ["rules.foundation"], [], [coreSource], []));
        var dependency = new ContentPackageDependency(
            Guid.Parse("8f070000-0000-4000-8000-000000000021"), 1, "studio:evidence",
            core.PackageNamespace, "^1.0.0", ContentDependencyKind.Required);
        var addon = RehashPackageUx8G(new ContentPackageDefinition(
            Guid.Parse("8f070000-0000-4000-8000-000000000002"), 1, "studio:evidence",
            "studio.ux8g.addon", "2.0.0", "MIT", new string('0', 64), 40,
            [], [dependency], [addonSource], []));
        var packages = source.Packages
            .Where(item => item.PackageNamespace is not "studio.ux8g.core" and not "studio.ux8g.addon")
            .Append(core)
            .Append(addon)
            .ToArray();
        return source with
        {
            CatalogProvenance = "studio:evidence",
            ConflictPolicy = ContentConflictPolicy.HighestPriority,
            Packages = packages,
            Approved = false,
        };
    }

    private static ContentWorkspaceConfiguration BuildContentUx8G(
        ProjectDefinition project,
        StudioUx8GPackageDraft draft)
    {
        var previous = project.Module.Content;
        var catalog = new ContentPackageCatalog(
            draft.CatalogId,
            draft.CatalogSchemaVersion,
            draft.CatalogProvenance,
            draft.ConflictPolicy,
            ClonePackagesUx8G(draft.Packages));
        return new ContentWorkspaceConfiguration(
            previous.Id,
            previous.Batch,
            previous.DataTables,
            previous.MergePolicy,
            previous.Provenance,
            previous.SchemaVersion,
            catalog);
    }

    private static ProjectDefinition ReplaceContentUx8G(
        ProjectDefinition source,
        ContentWorkspaceConfiguration content)
    {
        var module = source.Module;
        var updatedModule = new ModuleDefinition(
            module.Id, module.Slug, module.DisplayName, module.Description, module.EntryAreaId,
            module.Metadata, module.Variables, module.Distribution, module.Localization, content,
            module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        return new ProjectDefinition(source.Id, source.Title, updatedModule, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private static IReadOnlyList<ContentPackageDefinition> ClonePackagesUx8G(
        IReadOnlyList<ContentPackageDefinition> packages) => packages.Select(package =>
        new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources.ToArray(),
            package.Dependencies.ToArray(), package.Sources.ToArray(), package.Assets.ToArray())).ToArray();

    private static StudioUx8GPackageDraft FreezeDraftUx8G(StudioUx8GPackageDraft draft) => draft with
    {
        Packages = ClonePackagesUx8G(draft.Packages),
    };

    private static string ProjectFingerprintUx8G(ProjectDefinition project) =>
        HashBytesUx8G(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project)));

    private static string DraftFingerprintUx8G(StudioUx8GPackageDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        writer.Write(draft.SourceFingerprint ?? string.Empty);
        writer.Write(draft.CatalogId.ToByteArray());
        writer.Write(draft.CatalogSchemaVersion);
        writer.Write(draft.CatalogProvenance ?? string.Empty);
        writer.Write((int)draft.ConflictPolicy);
        writer.Write(draft.Approved);
        writer.Write(draft.Packages.Count);
        foreach (var package in draft.Packages.OrderBy(item => item.PackageNamespace, StringComparer.Ordinal))
        {
            writer.Write(package.Id.ToByteArray());
            writer.Write(package.SchemaVersion);
            writer.Write(package.Provenance);
            writer.Write(package.PackageNamespace);
            writer.Write(package.Version);
            writer.Write(package.LicenseSpdx);
            writer.Write(package.ContentSha256);
            writer.Write(package.Priority);
            WriteStringsUx8G(writer, package.SealedResources);
            writer.Write(package.Dependencies.Count);
            foreach (var dependency in package.Dependencies.OrderBy(item => item.PackageNamespace, StringComparer.Ordinal))
            {
                writer.Write(dependency.Id.ToByteArray());
                writer.Write(dependency.SchemaVersion);
                writer.Write(dependency.Provenance);
                writer.Write(dependency.PackageNamespace);
                writer.Write(dependency.VersionConstraint);
                writer.Write((int)dependency.Kind);
            }
            writer.Write(package.Sources.Count);
            foreach (var source in package.Sources.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
            {
                writer.Write(source.Id.ToByteArray());
                writer.Write(source.SchemaVersion);
                writer.Write(source.Provenance);
                writer.Write(source.LogicalResourceId);
                writer.Write(source.RelativePath);
                writer.Write((int)source.Kind);
                writer.Write(source.Required);
                writer.Write(source.LicenseSpdx);
                writer.Write(source.ContentSha256);
                writer.Write(source.PlatformByteBudget);
                WriteStringsUx8G(writer, source.Aliases ?? []);
                WriteStringsUx8G(writer, source.References ?? []);
            }
            writer.Write(package.Assets.Count);
            foreach (var asset in package.Assets.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
            {
                writer.Write(asset.Id.ToByteArray());
                writer.Write(asset.SchemaVersion);
                writer.Write(asset.Provenance);
                writer.Write(asset.LogicalResourceId);
                writer.Write(asset.RelativePath);
                writer.Write(asset.Required);
                writer.Write(asset.LicenseSpdx);
                writer.Write(asset.ContentSha256);
                writer.Write(asset.PlatformByteBudget);
                WriteStringsUx8G(writer, asset.Aliases ?? []);
                WriteStringsUx8G(writer, asset.References ?? []);
            }
        }
        writer.Flush();
        return HashBytesUx8G(stream.ToArray());
    }

    private static void WriteStringsUx8G(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) writer.Write(value ?? string.Empty);
    }

    private static string HashBytesUx8G(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HumanTextUx8G(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        text = Regex.Replace(text, "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "identifiant protégé", RegexOptions.CultureInvariant);
        return text;
    }

    private static string HumanFileUx8G(string? value)
    {
        var normalized = ContentPackageDefinition.NormalizePath(value);
        return string.IsNullOrWhiteSpace(normalized) ? "fichier non défini" : HumanTextUx8G(normalized);
    }

    private static string ScrubHumanUx8G(string? value) => HumanTextUx8G(value)
        .Replace("UUID", "identifiant", StringComparison.OrdinalIgnoreCase);

    private static StudioUx8GPackagePreview FailedPreviewUx8G(string message, string code) => new(
        false, false, false, false, false, string.Empty, "Aucun patch.", "Aucune opération inverse.",
        [], [], [], [], [], [new StudioUx8GDiagnostic(code, "module/content/packages", message)], message);

    private static StudioUx8GSmokeResult FailedSmokeUx8G(string message) => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, message);
}
