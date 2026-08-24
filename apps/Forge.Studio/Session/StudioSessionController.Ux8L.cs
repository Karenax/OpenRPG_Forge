using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Network.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const long Ux8LInteractiveLimit = 32L * 1024 * 1024;
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx8L = new(new SystemClock());
    private StudioUx8LSyncDraft? _approvedSyncUx8L;
    private string? _approvedSyncFingerprintUx8L;
    private bool _lastUx8LTemporaryReleased = true;

    internal bool LastUx8LTemporaryReleased => _lastUx8LTemporaryReleased;

    internal StudioUx8LSyncDraft? CreateSyncDraftUx8L(bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var extensions = project.Module.RuntimePolicies.Extensions;
        var policy = extensions.OperationalReadiness.ContentSynchronization;
        var repository = extensions.PostLaunch.CreatorExperience.RepositoryMetadata;
        return new(
            history.State.Revision,
            ProjectFingerprintUx8L(project),
            policy.Resumable,
            policy.DeduplicateByHash,
            policy.VerifyContent,
            policy.RequireTrustedSigner,
            policy.StorageDirectory,
            policy.MaximumCacheBytes,
            policy.AllowedLicenses.ToArray(),
            policy.TrustedSignerFingerprints.ToArray(),
            repository.Channel,
            repository.Compatibility,
            repository.RequireSignature,
            repository.RequireChangeNotes,
            "content",
            "*.*",
            Consent: false);
    }

    internal StudioUx8LSyncPreview PreviewSyncUx8L(StudioUx8LSyncDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelSyncApprovalUx8L();
        var project = Project;
        var history = History;
        var root = Session?.RootDirectory;
        if (project is null || history is null || string.IsNullOrWhiteSpace(root))
            return FailedPreviewUx8L("Ouvrez un module avant de configurer la synchronisation.");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceProjectFingerprint, ProjectFingerprintUx8L(project), StringComparison.Ordinal))
            return FailedPreviewUx8L("Le projet a changé ; rechargez le formulaire avant l’aperçu sec.");

        if (!TryPrepareSyncUx8L(project, root, draft, out var normalized, out var candidate,
                out var manifest, out var report, out var resources, out var error))
            return FailedPreviewUx8L(error);

        var dryRun = _setRuntimeExtensionsUx8L.Handle(history.State, new SetRuntimeExtensionsCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate!, DryRun: true));
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Inverse is null || dryRun.Audit.Applied)
            return FailedPreviewUx8L(HumanDiagnosticUx8L(dryRun.Diagnostics.FirstOrDefault()?.Code));

        var diff = BuildDiffUx8L(project.Module.RuntimePolicies.Extensions, candidate!);
        var canApply = normalized!.Consent && diff.Count > 0;
        if (canApply)
        {
            _approvedSyncUx8L = FreezeDraftUx8L(normalized);
            _approvedSyncFingerprintUx8L = DraftFingerprintUx8L(_approvedSyncUx8L);
        }

        return new(
            true,
            canApply,
            true,
            manifest is not null,
            report is { Valid: true },
            true,
            canApply
                ? "Aperçu sec validé · consentement enregistré, aucune écriture effectuée."
                : diff.Count == 0
                    ? "Aperçu sec validé · le profil est déjà identique."
                    : "Aperçu sec validé · cochez le consentement pour autoriser l’application.",
            manifest?.Fingerprint ?? string.Empty,
            manifest?.Resources.Sum(item => item.Size) ?? 0,
            report?.ReusedBytes ?? 0,
            diff,
            resources,
            report?.Diagnostics.Select(item => HumanDiagnosticUx8L(item.Code)).Distinct(StringComparer.Ordinal).ToArray() ?? []);
    }

    internal StudioUx8LActionResult ApplyApprovedSyncUx8L(StudioUx8LSyncDraft current)
    {
        var approved = _approvedSyncUx8L;
        var history = History;
        var project = Project;
        var root = Session?.RootDirectory;
        if (approved is null || _approvedSyncFingerprintUx8L is null || history is null || project is null ||
            string.IsNullOrWhiteSpace(root) || !current.Consent ||
            !string.Equals(DraftFingerprintUx8L(FreezeDraftUx8L(current)), _approvedSyncFingerprintUx8L,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx8L(approved), _approvedSyncFingerprintUx8L, StringComparison.Ordinal) ||
            approved.BaseRevision != history.State.Revision ||
            !string.Equals(approved.SourceProjectFingerprint, ProjectFingerprintUx8L(project), StringComparison.Ordinal))
        {
            CancelSyncApprovalUx8L();
            return new(false, "La source, le brouillon ou le consentement a changé ; relancez l’aperçu sec.");
        }

        if (!TryPrepareSyncUx8L(project, root, approved, out _, out var candidate, out _, out _, out _, out var error))
        {
            CancelSyncApprovalUx8L();
            return new(false, error);
        }

        var command = new SetRuntimeExtensionsCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx8L.Handle(history.State, command);
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Inverse is null || dryRun.Audit.Applied)
        {
            CancelSyncApprovalUx8L();
            return new(false, HumanDiagnosticUx8L(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setRuntimeExtensionsUx8L.Handle(history.State, command with { DryRun = false });
        CancelSyncApprovalUx8L();
        if (!applied.Succeeded)
            return new(false, HumanDiagnosticUx8L(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Synchronisation et métadonnées NWSync appliquées par la commande canonique.");
    }

    internal StudioUx8LActionResult UndoSyncUx8L()
    {
        CancelSyncApprovalUx8L();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8LActionResult RedoSyncUx8L()
    {
        CancelSyncApprovalUx8L();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8LActionResult SaveSyncUx8L()
    {
        CancelSyncApprovalUx8L();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8LApprovalSnapshot CaptureSyncApprovalUx8L() =>
        new(_approvedSyncUx8L, _approvedSyncFingerprintUx8L);

    internal void RestoreSyncApprovalUx8L(StudioUx8LApprovalSnapshot snapshot)
    {
        _approvedSyncUx8L = snapshot.Draft;
        _approvedSyncFingerprintUx8L = snapshot.DraftFingerprint;
    }

    internal void CancelSyncApprovalUx8L()
    {
        _approvedSyncUx8L = null;
        _approvedSyncFingerprintUx8L = null;
    }

    internal void CloseUx8L() => CancelSyncApprovalUx8L();

    internal StudioUx8LActionResult VerifySyncInSandboxUx8L(StudioUx8LSyncDraft draft)
    {
        var project = Project;
        var root = Session?.RootDirectory;
        if (project is null || string.IsNullOrWhiteSpace(root))
            return new(false, "Aucun module n’est ouvert.");
        if (!TryPrepareSyncUx8L(project, root, draft, out var normalized, out var candidate,
                out var manifest, out _, out _, out var error))
            return new(false, error);
        var preparedDraft = normalized!;
        var preparedCandidate = candidate!;
        var preparedManifest = manifest!;
        var sandbox = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-l-sync-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(sandbox);
            var repository = ResolveRepositoryUx8L(root, preparedDraft.RepositoryRelativePath);
            var transport = new LocalRepositoryTransportUx8L(repository);
            var service = new ContentRepositorySyncService(
                preparedCandidate.OperationalReadiness.ContentSynchronization, sandbox, chunkBytes: 16 * 1024);
            var applied = service.Synchronize(preparedManifest, transport);
            var reused = service.Synchronize(preparedManifest, transport);
            var succeeded = applied.Valid && applied.Applied && applied.Items.All(item => item.CacheHit) &&
                            reused.Valid && reused.Applied && reused.DownloadedBytes == 0 &&
                            reused.Items.All(item => item.CacheHit) &&
                            reused.ReusedBytes == preparedManifest.Resources.Sum(item => item.Size);
            return new(succeeded, succeeded
                ? $"Synchronisation jetable vérifiée · {preparedManifest.Resources.Count} ressource(s), cache réutilisé."
                : "La synchronisation jetable n’a pas produit un cache vérifié.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return new(false, HumanDiagnosticUx8L(exception.Message.Split(':')[0]));
        }
        finally
        {
            DeleteTemporaryUx8L(sandbox, "openrpg-forge-studio-ux8-l-sync-");
        }
    }

    internal StudioUx8LSmokeResult RunUx8LContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx8L();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureSyncApprovalUx8L();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-l-{Guid.NewGuid():N}");
        var released = false;
        var twoRoutes = false;
        var explicitSelection = false;
        var consentRequired = false;
        var scope = false;
        var manifestValid = false;
        var trustBlocked = false;
        var tamperBlocked = false;
        var resumeReuse = false;
        var canonicalDryRun = false;
        var canonicalCommand = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var neighbors = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, sourceProject);
            if (!created.Succeeded) return FailedSmokeUx8L();
            var repository = Path.Combine(tempRoot, "repository");
            Directory.CreateDirectory(repository);
            File.WriteAllBytes(Path.Combine(repository, "core.bin"), Encoding.UTF8.GetBytes("ux8-l-resumable-content"));
            File.WriteAllBytes(Path.Combine(repository, "localization.bin"), Encoding.UTF8.GetBytes("ux8-l-localization"));
            var editing = new StudioSessionController();
            if (!editing.Open(tempRoot).Succeeded) return FailedSmokeUx8L();
            var initial = editing.Project!;
            var initialNeighbors = NeighborFingerprintUx8L(initial);
            var source = editing.CreateSyncDraftUx8L(explicitlySelected: true);
            explicitSelection = source is not null;
            if (source is null) return FailedSmokeUx8L();
            var signer = new string('a', 64);
            var fixture = source with
            {
                Resumable = true,
                DeduplicateByHash = true,
                VerifyContent = true,
                RequireTrustedSigner = true,
                StorageDirectory = ".forge/content-cache-ux8-l",
                MaximumCacheBytes = 64L * 1024 * 1024,
                AllowedLicenses = ["CC0-1.0"],
                TrustedSignerFingerprints = [signer],
                Channel = "preview",
                Compatibility = "1.x",
                RequireSignature = true,
                RequireChangeNotes = true,
                RepositoryRelativePath = "repository",
                ResourceGlob = "*.bin",
                Consent = true,
            };
            twoRoutes = fixture.Channel == "preview" && fixture.Resumable && fixture.RequireSignature;
            var withoutConsent = editing.PreviewSyncUx8L(fixture with { Consent = false });
            consentRequired = withoutConsent.Succeeded && !withoutConsent.CanApply &&
                              editing.CaptureSyncApprovalUx8L().Draft is null;
            staleSource = !editing.PreviewSyncUx8L(fixture with { SourceProjectFingerprint = "stale" }).Succeeded;
            var outside = editing.PreviewSyncUx8L(fixture with { RepositoryRelativePath = "../outside" });
            scope = !outside.Succeeded;

            var content = Encoding.UTF8.GetBytes("blocked");
            var hash = Convert.ToHexStringLower(SHA256.HashData(content));
            var blockedPolicy = new ContentSynchronizationPolicy(Guid.NewGuid(), 1, true, true, true, true,
                "cache", 1_048_576, ["CC0-1.0"], [signer], "smoke");
            var blockedManifest = ContentRepositoryManifestService.Create(Guid.NewGuid(), "smoke", "1.0.0",
                new string('b', 64), [new ContentRepositoryResource("blocked.bin", content.Length, hash, "MIT", "smoke")]);
            var blockedReport = new ContentRepositorySyncService(blockedPolicy, tempRoot).Plan(blockedManifest);
            trustBlocked = !blockedReport.Valid &&
                           blockedReport.Diagnostics.Any(item => item.Code == "NET_CONTENT_SIGNER_UNTRUSTED") &&
                           blockedReport.Diagnostics.Any(item => item.Code == "NET_CONTENT_LICENSE_BLOCKED");
            var tampered = blockedManifest with { Fingerprint = new string('c', 64) };
            var tamperPolicy = new ContentSynchronizationPolicy(Guid.NewGuid(), 1, true, true, true, false,
                "tamper-cache", 1_048_576, ["MIT"], [], "smoke");
            tamperBlocked = new ContentRepositorySyncService(tamperPolicy, tempRoot).Plan(tampered)
                .Diagnostics.Any(item => item.Code == "NET_CONTENT_MANIFEST_TAMPERED");

            var preview = editing.PreviewSyncUx8L(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun &&
                              preview.ManifestValid && preview.SyncPlanValid;
            manifestValid = preview.ManifestValid && preview.Resources.Count == 2 && preview.TotalBytes > 0;
            resumeReuse = editing.VerifySyncInSandboxUx8L(fixture).Succeeded;
            var approval = editing.CaptureSyncApprovalUx8L();
            staleApproval = !editing.ApplyApprovedSyncUx8L(fixture with { Channel = "changed" }).Succeeded;
            editing.RestoreSyncApprovalUx8L(approval);
            var rePreview = editing.PreviewSyncUx8L(fixture);
            var applied = rePreview.CanApply && editing.ApplyApprovedSyncUx8L(fixture).Succeeded;
            canonicalCommand = applied && editing.History!.Journal.LastOrDefault()?.CommandName == "set_runtime_extensions";
            var appliedFingerprint = ProjectFingerprintUx8L(editing.Project!);
            var undone = applied && editing.UndoSyncUx8L().Succeeded &&
                         ProjectFingerprintUx8L(editing.Project!) == ProjectFingerprintUx8L(initial);
            var redone = undone && editing.RedoSyncUx8L().Succeeded &&
                         ProjectFingerprintUx8L(editing.Project!) == appliedFingerprint;
            undoRedo = applied && undone && redone;
            neighbors = redone && initialNeighbors == NeighborFingerprintUx8L(editing.Project!);
            var saved = redone && editing.SaveSyncUx8L().Succeeded;
            var reopened = saved ? ProjectSession.Open(tempRoot) : null;
            persistence = reopened is { Succeeded: true, Session: not null } &&
                          ProjectJsonSerializer.Serialize(reopened.Session.Project) ==
                          ProjectJsonSerializer.Serialize(editing.Project!);
            editing.Close();
        }
        finally
        {
            released = DeleteTemporaryUx8L(tempRoot, "openrpg-forge-studio-ux8-l-");
            RestoreSyncApprovalUx8L(sourceApproval);
            _lastUx8LTemporaryReleased = released;
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              ReferenceEquals(sourceSession, Session) && History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected && CaptureSyncApprovalUx8L() == sourceApproval;
        var succeeded = twoRoutes && explicitSelection && consentRequired && scope && manifestValid && trustBlocked &&
                        tamperBlocked && resumeReuse && canonicalDryRun && canonicalCommand && staleSource &&
                        staleApproval && undoRedo && persistence && neighbors && sourceUnchanged && released;
        return new(succeeded, twoRoutes, explicitSelection, consentRequired, scope, manifestValid, trustBlocked,
            tamperBlocked, resumeReuse, canonicalDryRun, canonicalCommand, staleSource, staleApproval, undoRedo,
            persistence, neighbors, sourceUnchanged, released);
    }

    private static bool TryPrepareSyncUx8L(
        ProjectDefinition project,
        string workspaceRoot,
        StudioUx8LSyncDraft draft,
        out StudioUx8LSyncDraft? normalized,
        out ModuleRuntimeExtensionProfile? candidate,
        out ContentRepositoryManifest? manifest,
        out ContentSyncReport? report,
        out IReadOnlyList<string> resources,
        out string error)
    {
        normalized = null;
        candidate = null;
        manifest = null;
        report = null;
        resources = [];
        error = string.Empty;
        try
        {
            normalized = NormalizeDraftUx8L(draft);
            candidate = BuildCandidateUx8L(project.Module.RuntimePolicies.Extensions, normalized);
            var repositoryRoot = ResolveRepositoryUx8L(workspaceRoot, normalized.RepositoryRelativePath);
            var files = EnumerateRepositoryFilesUx8L(repositoryRoot, normalized.ResourceGlob);
            if (files.Count == 0) throw new ArgumentException("Le dépôt local ne contient aucune ressource correspondant au filtre.");
            var total = files.Sum(item => new FileInfo(item).Length);
            if (files.Count > 128 || total > Ux8LInteractiveLimit)
                throw new ArgumentException("L’aperçu interactif accepte au maximum 128 fichiers et 32 Mio.");
            var policy = candidate.OperationalReadiness.ContentSynchronization;
            var metadata = candidate.PostLaunch.CreatorExperience.RepositoryMetadata;
            var signer = policy.TrustedSignerFingerprints.FirstOrDefault();
            if ((policy.RequireTrustedSigner || metadata.RequireSignature) && signer is null)
                throw new ArgumentException("Une empreinte de signataire SHA-256 est requise par la politique NWSync.");
            var license = policy.AllowedLicenses.First();
            var entries = files.Select(path =>
            {
                var relative = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                using var stream = File.OpenRead(path);
                var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
                return new ContentRepositoryResource(relative, new FileInfo(path).Length, hash, license,
                    "studio:ux8-l:local-repository");
            }).ToArray();
            manifest = ContentRepositoryManifestService.Create(
                StableGuidUx8L($"{project.Module.Id.Value:D}|{project.Module.Metadata.Version}|{string.Join('|', entries.Select(item => item.Sha256))}"),
                project.Module.Slug,
                project.Module.Metadata.Version,
                signer,
                entries);
            var planRoot = Path.GetFullPath(Path.Combine(workspaceRoot, ".forge", "studio-sync-plan"));
            report = new ContentRepositorySyncService(policy, planRoot).Plan(manifest);
            if (!report.Valid)
                throw new InvalidDataException(HumanDiagnosticUx8L(report.Diagnostics.FirstOrDefault()?.Code));
            resources = report.Items.Select(item =>
                $"{HumanFileUx8L(item.Path)} · {HumanBytesUx8L(item.Size)} · " +
                $"{(item.CacheHit ? "cache réutilisé" : $"{item.ChunkCount} bloc(s) à recevoir")}").ToArray();
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error = exception.Message;
            normalized = null;
            candidate = null;
            manifest = null;
            report = null;
            resources = [];
            return false;
        }
    }

    private static StudioUx8LSyncDraft NormalizeDraftUx8L(StudioUx8LSyncDraft draft)
    {
        var licenses = draft.AllowedLicenses.Select(item => item.Trim().ToUpperInvariant())
            .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var signers = draft.TrustedSignerFingerprints.Select(item => item.Trim().ToLowerInvariant())
            .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var repository = NormalizeRelativeUx8L(draft.RepositoryRelativePath, "Le dossier de dépôt local");
        var storage = NormalizeRelativeUx8L(draft.StorageDirectory, "Le dossier de cache");
        var glob = draft.ResourceGlob.Trim();
        if (glob.Length == 0 || glob.Length > 80 || glob.Contains('/') || glob.Contains('\\') || glob.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Le filtre de ressources doit être un glob de nom de fichier simple.");
        return draft with
        {
            StorageDirectory = storage,
            AllowedLicenses = licenses,
            TrustedSignerFingerprints = signers,
            Channel = draft.Channel.Trim().ToLowerInvariant(),
            Compatibility = draft.Compatibility.Trim(),
            RepositoryRelativePath = repository,
            ResourceGlob = glob,
        };
    }

    private static ModuleRuntimeExtensionProfile BuildCandidateUx8L(
        ModuleRuntimeExtensionProfile source,
        StudioUx8LSyncDraft draft)
    {
        var readiness = source.OperationalReadiness;
        var policy = new ContentSynchronizationPolicy(
            readiness.ContentSynchronization.Id,
            readiness.ContentSynchronization.SchemaVersion,
            draft.Resumable,
            draft.DeduplicateByHash,
            draft.VerifyContent,
            draft.RequireTrustedSigner,
            draft.StorageDirectory,
            draft.MaximumCacheBytes,
            draft.AllowedLicenses,
            draft.TrustedSignerFingerprints,
            "studio:ux8-l:content-sync");
        var updatedReadiness = new OperationalReadinessProfile(
            readiness.Id, readiness.SchemaVersion, readiness.TaskManifests, readiness.CrashArtifacts,
            readiness.ProjectHealth, readiness.Recovery, policy, "studio:ux8-l:content-sync");
        var post = source.PostLaunch;
        var creator = post.CreatorExperience;
        var repository = new CreatorExperienceProductionProfile.RepositoryMetadataSettings(
            creator.RepositoryMetadata.Id,
            draft.Channel,
            draft.Compatibility,
            draft.RequireSignature,
            draft.RequireChangeNotes);
        var updatedCreator = new CreatorExperienceProductionProfile(
            creator.Id, creator.SchemaVersion, creator.Water, creator.DeviceQueries, creator.ResourceCommands,
            creator.DynamicAreas, creator.PartyControl, creator.ProgressionTracks, repository,
            creator.Observability, creator.Graphics, "studio:ux8-l:nwsync-metadata");
        var updatedPost = new PostLaunchCapabilityProfile(
            post.Id, post.SchemaVersion, post.Gameplay, post.LiveDirector, post.Network, updatedCreator,
            post.Production, "studio:ux8-l:nwsync-metadata");
        return new ModuleRuntimeExtensionProfile(
            source.Id, source.SchemaVersion, source.RuleSchemas, source.UiDocuments, source.JsonDocuments,
            source.SnapshotPolicy, "studio:ux8-l:repository-sync", source.ModernTooling, updatedReadiness, updatedPost);
    }

    private static IReadOnlyList<string> BuildDiffUx8L(
        ModuleRuntimeExtensionProfile source,
        ModuleRuntimeExtensionProfile candidate)
    {
        var before = source.OperationalReadiness.ContentSynchronization;
        var after = candidate.OperationalReadiness.ContentSynchronization;
        var repositoryBefore = source.PostLaunch.CreatorExperience.RepositoryMetadata;
        var repositoryAfter = candidate.PostLaunch.CreatorExperience.RepositoryMetadata;
        var entries = new List<string>();
        Add("Reprise", before.Resumable, after.Resumable);
        Add("Déduplication SHA-256", before.DeduplicateByHash, after.DeduplicateByHash);
        Add("Vérification du contenu", before.VerifyContent, after.VerifyContent);
        Add("Signataire de confiance", before.RequireTrustedSigner, after.RequireTrustedSigner);
        Add("Cache", before.StorageDirectory, after.StorageDirectory);
        Add("Quota", HumanBytesUx8L(before.MaximumCacheBytes), HumanBytesUx8L(after.MaximumCacheBytes));
        Add("Licences", string.Join(", ", before.AllowedLicenses), string.Join(", ", after.AllowedLicenses));
        Add("Signataires", $"{before.TrustedSignerFingerprints.Count}", $"{after.TrustedSignerFingerprints.Count}");
        Add("Canal", repositoryBefore.Channel, repositoryAfter.Channel);
        Add("Compatibilité", repositoryBefore.Compatibility, repositoryAfter.Compatibility);
        Add("Signature NWSync", repositoryBefore.RequireSignature, repositoryAfter.RequireSignature);
        Add("Notes de changement", repositoryBefore.RequireChangeNotes, repositoryAfter.RequireChangeNotes);
        return entries;

        void Add(string label, object prior, object next)
        {
            if (!Equals(prior, next)) entries.Add($"{label} · {HumanValueUx8L(prior)} → {HumanValueUx8L(next)}");
        }
    }

    private static string NeighborFingerprintUx8L(ProjectDefinition project)
    {
        var extensions = project.Module.RuntimePolicies.Extensions;
        var readiness = extensions.OperationalReadiness;
        var post = extensions.PostLaunch;
        var creator = post.CreatorExperience;
        return HashTextUx8L(JsonSerializer.Serialize(new
        {
            project.Id,
            project.Title,
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests,
            Module = new
            {
                project.Module.Id,
                project.Module.Slug,
                project.Module.DisplayName,
                project.Module.Description,
                project.Module.EntryAreaId,
                project.Module.Metadata,
                project.Module.Variables,
                project.Module.Distribution,
                project.Module.Localization,
                project.Module.Content,
                project.Module.ArtisticStyle,
                project.Module.Factions,
                project.Module.WorldFeatures,
            },
            Runtime = new
            {
                project.Module.RuntimePolicies.Id,
                project.Module.RuntimePolicies.SchemaVersion,
                project.Module.RuntimePolicies.Heartbeats,
                project.Module.RuntimePolicies.Rest,
                project.Module.RuntimePolicies.SequenceAbort,
                project.Module.RuntimePolicies.Cinematics,
                project.Module.RuntimePolicies.Gameplay,
                project.Module.RuntimePolicies.Rules,
                project.Module.RuntimePolicies.GameplayEvents,
                project.Module.RuntimePolicies.World,
                project.Module.RuntimePolicies.LifecycleEvents,
            },
            Extensions = new
            {
                extensions.Id,
                extensions.SchemaVersion,
                extensions.RuleSchemas,
                extensions.UiDocuments,
                extensions.JsonDocuments,
                extensions.SnapshotPolicy,
                extensions.ModernTooling,
                Readiness = new
                {
                    readiness.Id,
                    readiness.SchemaVersion,
                    readiness.TaskManifests,
                    readiness.CrashArtifacts,
                    readiness.ProjectHealth,
                    readiness.Recovery,
                },
                Post = new
                {
                    post.Id,
                    post.SchemaVersion,
                    post.Gameplay,
                    post.LiveDirector,
                    post.Network,
                    Creator = new
                    {
                        creator.Id,
                        creator.SchemaVersion,
                        creator.Water,
                        creator.DeviceQueries,
                        creator.ResourceCommands,
                        creator.DynamicAreas,
                        creator.PartyControl,
                        creator.ProgressionTracks,
                        creator.Observability,
                        creator.Graphics,
                    },
                    post.Production,
                },
            },
        }));
    }

    private static StudioUx8LSyncDraft FreezeDraftUx8L(StudioUx8LSyncDraft draft) => draft with
    {
        AllowedLicenses = draft.AllowedLicenses.ToArray(),
        TrustedSignerFingerprints = draft.TrustedSignerFingerprints.ToArray(),
    };

    private static string ProjectFingerprintUx8L(ProjectDefinition project) =>
        HashTextUx8L(ProjectJsonSerializer.Serialize(project));

    private static string DraftFingerprintUx8L(StudioUx8LSyncDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        WriteText(draft.SourceProjectFingerprint);
        writer.Write(draft.Resumable);
        writer.Write(draft.DeduplicateByHash);
        writer.Write(draft.VerifyContent);
        writer.Write(draft.RequireTrustedSigner);
        WriteText(draft.StorageDirectory);
        writer.Write(draft.MaximumCacheBytes);
        WriteMany(draft.AllowedLicenses);
        WriteMany(draft.TrustedSignerFingerprints);
        WriteText(draft.Channel);
        WriteText(draft.Compatibility);
        writer.Write(draft.RequireSignature);
        writer.Write(draft.RequireChangeNotes);
        WriteText(draft.RepositoryRelativePath);
        WriteText(draft.ResourceGlob);
        writer.Write(draft.Consent);
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));

        void WriteText(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        void WriteMany(IReadOnlyList<string> values)
        {
            writer.Write(values.Count);
            foreach (var value in values) WriteText(value);
        }
    }

    private static string HashTextUx8L(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string NormalizeRelativeUx8L(string value, string label)
    {
        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.Length > 240 || Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new ArgumentException($"{label} doit rester relatif, non vide et confiné.");
        return normalized;
    }

    private static string ResolveRepositoryUx8L(string workspaceRoot, string relative)
    {
        var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalized = NormalizeRelativeUx8L(relative, "Le dépôt local");
        var resolved = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Le dépôt local sort du workspace.");
        if (!Directory.Exists(resolved)) throw new ArgumentException("Le dépôt local sélectionné n’existe pas.");
        var current = new DirectoryInfo(resolved);
        while (current is not null && current.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Les répertoires liés ne sont pas autorisés dans le dépôt local.");
            if (string.Equals(current.FullName, root, StringComparison.OrdinalIgnoreCase)) break;
            current = current.Parent;
        }
        return resolved;
    }

    private static IReadOnlyList<string> EnumerateRepositoryFilesUx8L(string root, string glob)
    {
        return Directory.EnumerateFiles(root, glob, SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment =>
                string.Equals(segment, ".forge", StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Les fichiers liés ne sont pas autorisés dans le manifeste.");
                return path;
            }).ToArray();
    }

    private static Guid StableGuidUx8L(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string HumanValueUx8L(object value) => value switch
    {
        true => "activé",
        false => "désactivé",
        _ => HumanTextUx8L(value.ToString()),
    };

    internal static string HumanBytesUx8L(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / (1024d * 1024 * 1024):0.##} Gio"
        : bytes >= 1024L * 1024 ? $"{bytes / (1024d * 1024):0.##} Mio" : $"{bytes} octet(s)";

    internal static string HumanTextUx8L(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "non défini" : value.Trim();
        return System.Text.RegularExpressions.Regex.Replace(text,
            @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b",
            "identifiant protégé");
    }

    private static string HumanFileUx8L(string value) => HumanTextUx8L(value.Replace('\\', '/'));

    private static string HumanDiagnosticUx8L(string? code) => code switch
    {
        null or "" => "Aucun diagnostic bloquant.",
        "NET_CONTENT_SIGNER_UNTRUSTED" => "Le manifeste n’est pas signé par une autorité approuvée.",
        "NET_CONTENT_LICENSE_BLOCKED" => "Une ressource utilise une licence non autorisée.",
        "NET_CONTENT_MANIFEST_TAMPERED" => "L’empreinte du manifeste ne correspond pas à son contenu.",
        "NET_CONTENT_CACHE_QUOTA_EXCEEDED" => "Le quota du cache de contenu serait dépassé.",
        "NET_CONTENT_HASH_MISMATCH" => "Le contenu reçu ne correspond pas à son SHA-256.",
        "command.revision_conflict" => "La révision du projet a changé.",
        _ => HumanTextUx8L(code).Replace('_', ' ').Replace('.', ' '),
    };

    private static StudioUx8LSyncPreview FailedPreviewUx8L(string message) =>
        new(false, false, false, false, false, false, HumanTextUx8L(message), string.Empty, 0, 0, [], [],
            [HumanTextUx8L(message)]);

    private static StudioUx8LSmokeResult FailedSmokeUx8L() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false);

    private static bool DeleteTemporaryUx8L(string directory, string requiredPrefix)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith(requiredPrefix, StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private sealed class LocalRepositoryTransportUx8L(string repositoryRoot) : IContentRepositoryTransport
    {
        private readonly string _root = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public Stream OpenRead(string resourcePath, long offset)
        {
            var normalized = NormalizeRelativeUx8L(resourcePath, "La ressource");
            var path = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("NET_CONTENT_RESOURCE_OUTSIDE_REPOSITORY");
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (offset < 0 || offset > stream.Length)
            {
                stream.Dispose();
                throw new IOException("NET_CONTENT_RESUME_OFFSET_INVALID");
            }
            stream.Position = offset;
            return stream;
        }
    }
}
