using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Application.Build;
using Forge.Application.Commands;
using Forge.Domain.Assets;
using Forge.Domain.Build;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private StudioUx8ICompatibilityDraft? _approvedCompatibilityUx8I;
    private WorkspaceBuildProfileRevision? _approvedCompatibilityRevisionUx8I;
    private string? _approvedCompatibilitySourceUx8I;
    private string? _approvedCompatibilityDraftUx8I;
    private bool _compatibilityCanonicalCommandUx8I;
    private bool _lastUx8ITemporaryReleased = true;

    internal bool CanUndoCompatibilityUx8I => _buildProfileHistory?.CanUndo == true;
    internal bool CanRedoCompatibilityUx8I => _buildProfileHistory?.CanRedo == true;
    internal bool LastUx8ITemporaryReleased => _lastUx8ITemporaryReleased;

    internal StudioUx8ICompatibilityDraft? CurrentCompatibilityDraftUx8I()
    {
        if (Project is null || Session is null || _buildProfileHistory is null) return null;
        var compatibility = _buildProfileHistory.State.Profile.Compatibility;
        return new(
            Project.Id.Value,
            Project.Module.DisplayName,
            _buildProfileHistory.State.Revision,
            BuildProfileFingerprintUx8C(_buildProfileHistory.State.Profile),
            compatibility.Id,
            compatibility.SchemaVersion,
            compatibility.Provenance,
            compatibility.MinimumRuntimeVersion,
            compatibility.ProjectFormatVersion,
            compatibility.AutomaticMigrations,
            compatibility.RequiredAtRuntime,
            Approved: false);
    }

    internal IReadOnlyList<StudioUx8IPlatformChoice> ListPlatformChoicesUx8I()
    {
        if (Session is null) return [];
        var loaded = AssetPackageManifestStore.Load(Session.RootDirectory);
        if (!loaded.Succeeded) return [];
        var ordinal = 0;
        return loaded.Manifests.OrderBy(item => item.AssetId.ToString(), StringComparer.Ordinal).Select(manifest =>
        {
            ordinal++;
            var roles = manifest.Chunks.Select(item => item.Role).Distinct().Order().Select(HumanRoleUx8I);
            var declared = manifest.PlatformBudgets.Keys.Order().Select(HumanPlatformUx8I);
            return new StudioUx8IPlatformChoice(
                manifest.AssetId,
                $"Ressource gouvernée {ordinal:000} · {manifest.LicenseSpdx}",
                $"{manifest.Chunks.Count} fichier(s) · {string.Join(", ", roles)} · budgets {string.Join(", ", declared)}",
                manifest);
        }).ToArray();
    }

    internal StudioUx8IPlatformReport AnalyzePlatformUx8I(
        AssetCatalogItemId assetId,
        AssetTargetPlatform platform)
    {
        if (Session is null)
            return FailedPlatformUx8I(platform, "Aucun workspace n’est ouvert.");
        var choice = ListPlatformChoicesUx8I().FirstOrDefault(item => item.AssetId == assetId);
        if (choice is null)
            return FailedPlatformUx8I(platform, "Sélectionnez explicitement un manifeste d’asset.");
        if (platform == AssetTargetPlatform.Any)
            return FailedPlatformUx8I(platform, "Choisissez une plateforme concrète.");

        var manifest = choice.Manifest;
        var hashes = ReadAvailableChunkHashesUx3G(Session.RootDirectory, manifest);
        var resolution = AssetPackageManifestResolver.Resolve(manifest, platform, hashes);
        var parity = AssetPlatformParityValidator.Validate(manifest, [platform]);
        var applicable = resolution.Chunks;
        var requiredRoles = manifest.Chunks.Where(item => item.Required).Select(item => item.Role)
            .Distinct().Order().ToArray();
        var coveredRoles = applicable.Where(item => item.Required).Select(item => item.Role).Distinct().ToHashSet();
        var rolesCovered = requiredRoles.All(coveredRoles.Contains) && parity.Count == 0;
        var contentVerified = applicable.All(item =>
            !item.Required || hashes.TryGetValue(item.RelativePath, out var hash) &&
            string.Equals(hash, item.ContentSha256, StringComparison.Ordinal));
        var budgetDeclared = manifest.PlatformBudgets.TryGetValue(platform, out var budget) ||
                             manifest.PlatformBudgets.TryGetValue(AssetTargetPlatform.Any, out budget);
        var stored = applicable.Sum(item => item.StoredBytes);
        var metadataCompatible = manifest.ModelMetadata is null ||
            manifest.ModelMetadata.Compatibility.Platforms.Contains(platform) ||
            manifest.ModelMetadata.Compatibility.Platforms.Contains(AssetTargetPlatform.Any);
        var diagnostics = resolution.Diagnostics.Concat(parity)
            .Select(item => $"{item.Code} · {HumanPathUx8I(item.Path)} · {item.Message}")
            .ToList();
        if (!budgetDeclared)
            diagnostics.Add($"asset.platform.budget_missing · Aucun budget déclaré pour {HumanPlatformUx8I(platform)}.");
        if (!metadataCompatible)
            diagnostics.Add($"asset.platform.metadata_missing · Les métadonnées du modèle n’annoncent pas {HumanPlatformUx8I(platform)}.");
        var plan = applicable.Select(item =>
            $"{HumanRoleUx8I(item.Role)} · {HumanPathUx8I(item.RelativePath)} · {HumanBytesUx8I(item.StoredBytes)} · " +
            (item.Platforms.Contains(platform) ? "variante exacte" : "repli commun")).ToList();
        plan.Insert(0, $"{HumanPlatformUx8I(platform)} · {applicable.Count} fichier(s) · " +
                       $"{HumanBytesUx8I(stored)} / {(budgetDeclared ? HumanBytesUx8I(budget) : "budget absent")}");
        if (manifest.ModelMetadata is not null)
            plan.Add($"Shader {ScrubHumanUx8I(manifest.ModelMetadata.Compatibility.ShaderFamily)} · " +
                     $"échelle {manifest.ModelMetadata.Compatibility.MetersPerUnit:0.###} m/unité · " +
                     $"densité {manifest.ModelMetadata.Compatibility.TargetTexelDensity} px/m");
        var succeeded = resolution.Succeeded && rolesCovered && contentVerified && budgetDeclared &&
                        stored <= budget && metadataCompatible;
        return new(platform, succeeded, budgetDeclared, rolesCovered, contentVerified, applicable.Count,
            stored, budgetDeclared ? budget : null, plan, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    internal StudioUx8ICompatibilityPreview PreviewCompatibilityUx8I(StudioUx8ICompatibilityDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCompatibilityApprovalUx8I();
        if (!ValidateCompatibilitySourceUx8I(draft, out var sourceError))
            return FailedCompatibilityUx8I(sourceError);

        WorkspaceBuildProfile candidate;
        try
        {
            candidate = BuildCompatibilityCandidateUx8I(draft);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedCompatibilityUx8I(exception.Message);
        }

        var command = new SetWorkspaceBuildProfileCommand(
            CommandId.From(Guid.NewGuid()), draft.BaseRevision, candidate, DryRun: true);
        var result = _setWorkspaceBuildProfile.Handle(_buildProfileHistory!.State, command);
        var diagnostics = result.Diagnostics.Select(item => HumanCompatibilityDiagnosticUx8I(item.Code)).ToList();
        var migration = WorkspaceBuildCollaborationService.MigrateWorkspace(
            Session!.RootDirectory, candidate, dryRun: true, targetVersion: candidate.Compatibility.ProjectFormatVersion);
        diagnostics.AddRange(migration.Diagnostics.Select(item =>
            $"{item.Code} · {HumanPathUx8I(item.Path)} · {item.Message}"));

        var manifest = ModuleManifest.Create(
            Project!,
            Project!.Module.Metadata.Version,
            candidate.Compatibility.MinimumRuntimeVersion,
            "1.0.0",
            new string('a', 64),
            target: ModuleTargetKind.Editor,
            runtimeCompatibilityRequired: candidate.Compatibility.RequiredAtRuntime);
        var runtimeDiagnostic = ModuleRuntimeCompatibility.Evaluate(
            manifest, ModuleRuntimeCompatibility.CurrentRuntimeVersion);
        var runtimeReady = manifest.RuntimeMinimumVersion == candidate.Compatibility.MinimumRuntimeVersion &&
                           manifest.RuntimeCompatibilityRequired == candidate.Compatibility.RequiredAtRuntime;
        if (runtimeDiagnostic is not null)
            diagnostics.Add($"{runtimeDiagnostic.Code} · runtime local {ModuleRuntimeCompatibility.CurrentRuntimeVersion} · " +
                            $"le paquet exigerait {manifest.RuntimeMinimumVersion}");

        var dryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var migrationReady = migration.Succeeded && migration.DryRun && migration.Idempotent;
        var succeeded = dryRun && migrationReady;
        var frozen = FreezeCompatibilityDraftUx8I(draft);
        if (succeeded && draft.Approved)
        {
            _approvedCompatibilityUx8I = frozen;
            _approvedCompatibilityRevisionUx8I = draft.BaseRevision;
            _approvedCompatibilitySourceUx8I = draft.SourceFingerprint;
            _approvedCompatibilityDraftUx8I = CompatibilityDraftFingerprintUx8I(frozen);
        }

        var before = _buildProfileHistory.State.Profile.Compatibility;
        var diff = new List<string>();
        if (before.MinimumRuntimeVersion != candidate.Compatibility.MinimumRuntimeVersion)
            diff.Add($"Runtime minimal : {before.MinimumRuntimeVersion} → {candidate.Compatibility.MinimumRuntimeVersion}");
        if (before.ProjectFormatVersion != candidate.Compatibility.ProjectFormatVersion)
            diff.Add($"Format projet : {before.ProjectFormatVersion} → {candidate.Compatibility.ProjectFormatVersion}");
        if (before.AutomaticMigrations != candidate.Compatibility.AutomaticMigrations)
            diff.Add($"Migrations automatiques : {(candidate.Compatibility.AutomaticMigrations ? "requises" : "désactivées")}");
        if (before.RequiredAtRuntime != candidate.Compatibility.RequiredAtRuntime)
            diff.Add($"Compatibilité runtime : {(candidate.Compatibility.RequiredAtRuntime ? "bloquante" : "informative")}");
        if (diff.Count == 0) diff.Add("Aucune différence authorée ; le contrat et la migration restent vérifiés à sec.");
        var migrationPlan = new List<string>
        {
            $"Format détecté {migration.FromVersion} → cible {migration.ToVersion}",
            migration.ChangedDocuments.Count == 0
                ? "Workspace déjà canonique · aucune réécriture nécessaire."
                : $"{migration.ChangedDocuments.Count} document(s) seraient migrés avec sauvegarde préalable.",
            "Dry-run uniquement · aucun fichier source modifié.",
        };
        return new(
            succeeded,
            succeeded && draft.Approved,
            dryRun,
            runtimeReady,
            migrationReady,
            succeeded
                ? draft.Approved
                    ? "Compatibilité validée à sec · application autorisée."
                    : "Compatibilité validée à sec · approbation humaine requise."
                : diagnostics.FirstOrDefault() ?? "Le profil de compatibilité doit être corrigé.",
            result.Patch?.Summary ?? "Remplacer le profil de compatibilité du workspace.",
            diff,
            diagnostics.Distinct(StringComparer.Ordinal).ToArray(),
            migrationPlan);
    }

    internal StudioUx8IActionResult ApplyApprovedCompatibilityUx8I(StudioUx8ICompatibilityDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (_buildProfileHistory is null || _approvedCompatibilityUx8I is null ||
            _approvedCompatibilityRevisionUx8I != _buildProfileHistory.State.Revision ||
            _approvedCompatibilitySourceUx8I != BuildProfileFingerprintUx8C(_buildProfileHistory.State.Profile) ||
            _approvedCompatibilityDraftUx8I != CompatibilityDraftFingerprintUx8I(_approvedCompatibilityUx8I) ||
            _approvedCompatibilityDraftUx8I != CompatibilityDraftFingerprintUx8I(draft) ||
            !draft.Approved || !BuildProfileStoreMatchesUx8I())
        {
            CancelCompatibilityApprovalUx8I();
            return new(false, "Application refusée · le profil ou l’approbation a changé depuis l’aperçu sec.");
        }

        var candidate = BuildCompatibilityCandidateUx8I(_approvedCompatibilityUx8I);
        var command = new SetWorkspaceBuildProfileCommand(
            CommandId.From(Guid.NewGuid()), _buildProfileHistory.State.Revision, candidate, DryRun: true);
        var dryRun = _setWorkspaceBuildProfile.Handle(_buildProfileHistory.State, command);
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Inverse is null || dryRun.Audit.Applied)
        {
            CancelCompatibilityApprovalUx8I();
            return new(false, HumanCompatibilityDiagnosticUx8I(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var migration = WorkspaceBuildCollaborationService.MigrateWorkspace(
            Session!.RootDirectory, candidate, dryRun: true, targetVersion: candidate.Compatibility.ProjectFormatVersion);
        if (!migration.Succeeded || !migration.Idempotent)
        {
            CancelCompatibilityApprovalUx8I();
            return new(false, migration.Diagnostics.FirstOrDefault()?.Message ?? "La migration automatique n’est pas disponible.");
        }

        var previousFileMissing = _buildProfileFileMissing;
        var applied = _setWorkspaceBuildProfile.Handle(_buildProfileHistory.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            CancelCompatibilityApprovalUx8I();
            return new(false, HumanCompatibilityDiagnosticUx8I(applied.Diagnostics.FirstOrDefault()?.Code));
        }
        _buildProfileHistory.Record(applied);
        _buildProfileUndoFileStates.Push(previousFileMissing);
        _buildProfileRedoFileStates.Clear();
        if (!TryPersistBuildProfile(applied.State.Profile, fileMissing: false, out var error))
        {
            _buildProfileHistory.Undo();
            _buildProfileUndoFileStates.Pop();
            CancelCompatibilityApprovalUx8I();
            return new(false, error);
        }
        _persistedBuildProfile = applied.State.Profile;
        _buildProfileFileMissing = false;
        _compatibilityCanonicalCommandUx8I = applied.Audit.CommandName == "set_workspace_build_profile";
        CancelCompatibilityApprovalUx8I();
        return new(true, "Compatibilité enregistrée atomiquement · annulation et rétablissement disponibles.");
    }

    internal StudioUx8IActionResult UndoCompatibilityUx8I()
    {
        CancelCompatibilityApprovalUx8I();
        var result = UndoScriptCacheProfile();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8IActionResult RedoCompatibilityUx8I()
    {
        CancelCompatibilityApprovalUx8I();
        var result = RedoScriptCacheProfile();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8IActionResult SaveCompatibilityUx8I()
    {
        CancelCompatibilityApprovalUx8I();
        if (_buildProfileHistory is null) return new(false, "Aucun profil de build n’est ouvert.");
        return TryPersistBuildProfile(_buildProfileHistory.State.Profile, fileMissing: false, out var error)
            ? new(true, "Profil de compatibilité enregistré.")
            : new(false, error);
    }

    internal StudioUx8IApprovalSnapshot CaptureCompatibilityApprovalUx8I() => new(
        _approvedCompatibilityUx8I,
        _approvedCompatibilityRevisionUx8I,
        _approvedCompatibilitySourceUx8I,
        _approvedCompatibilityDraftUx8I);

    internal void RestoreCompatibilityApprovalUx8I(StudioUx8IApprovalSnapshot snapshot)
    {
        _approvedCompatibilityUx8I = snapshot.Draft;
        _approvedCompatibilityRevisionUx8I = snapshot.Revision;
        _approvedCompatibilitySourceUx8I = snapshot.SourceFingerprint;
        _approvedCompatibilityDraftUx8I = snapshot.DraftFingerprint;
    }

    internal void CancelCompatibilityApprovalUx8I()
    {
        _approvedCompatibilityUx8I = null;
        _approvedCompatibilityRevisionUx8I = null;
        _approvedCompatibilitySourceUx8I = null;
        _approvedCompatibilityDraftUx8I = null;
    }

    internal void CloseUx8I()
    {
        CancelCompatibilityApprovalUx8I();
        _compatibilityCanonicalCommandUx8I = false;
    }

    internal StudioUx8ISmokeResult RunUx8IContractSmoke()
    {
        if (Project is null || Session is null) return FailedSmokeUx8I();
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = IsDirty;
        var sourceApproval = CaptureCompatibilityApprovalUx8I();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-i-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        _lastUx8ITemporaryReleased = false;
        var temporaryReleased = false;
        var result = FailedSmokeUx8I();
        try
        {
            if (!ProjectSession.Create(root, Project).Succeeded)
                throw new InvalidOperationException("La session de preuve UX8-I n’a pas pu être créée.");
            var manifest = CreateEvidenceManifestUx8I(root);
            if (
                !WorkspaceBuildProfileStore.Save(root, WorkspaceBuildProfile.CreateDefault()).Succeeded ||
                !AssetPackageManifestStore.Save(root, [manifest]).Succeeded ||
                !editing.Open(root).Succeeded)
                throw new InvalidOperationException("Le workspace de preuve UX8-I n’a pas pu être ouvert.");

            var original = editing.BuildProfile;
            var neighborsBefore = CompatibilityNeighborsFingerprintUx8I(original);
            var draft = editing.CurrentCompatibilityDraftUx8I();
            if (draft is null)
                throw new InvalidOperationException("Le brouillon de compatibilité UX8-I est indisponible.");
            draft = draft with
            {
                MinimumRuntimeVersion = ModuleRuntimeCompatibility.CurrentRuntimeVersion,
                ProjectFormatVersion = ProjectSession.CurrentProjectFormatVersion,
                AutomaticMigrations = true,
                RequiredAtRuntime = true,
                Approved = true,
            };
            var invalid = !editing.PreviewCompatibilityUx8I(draft with { AutomaticMigrations = false }).Succeeded;
            var preview = editing.PreviewCompatibilityUx8I(draft);
            var migration = preview.MigrationDryRunReady;
            var runtimeRequired = VerifyRuntimeGateUx8I(editing.Project!);
            var platform = editing.AnalyzePlatformUx8I(manifest.AssetId, AssetTargetPlatform.Windows);
            var staleSource = !editing.PreviewCompatibilityUx8I(draft with { SourceFingerprint = "stale" }).Succeeded;
            preview = editing.PreviewCompatibilityUx8I(draft);
            var staleApproval = !editing.ApplyApprovedCompatibilityUx8I(
                draft with { MinimumRuntimeVersion = "0.6.33" }).Succeeded;
            preview = editing.PreviewCompatibilityUx8I(draft);
            var applied = preview.CanApply && editing.ApplyApprovedCompatibilityUx8I(draft).Succeeded;
            var command = applied && editing._compatibilityCanonicalCommandUx8I;
            var undo = applied && editing.UndoCompatibilityUx8I().Succeeded;
            var redo = undo && editing.RedoCompatibilityUx8I().Succeeded;
            var saved = redo && editing.SaveCompatibilityUx8I().Succeeded;
            var neighbors = saved && CompatibilityNeighborsFingerprintUx8I(editing.BuildProfile) == neighborsBefore;
            editing.Close();
            var opened = reopened.Open(root);
            var persistence = saved && opened.Succeeded &&
                              CompatibilityFingerprintUx8I(reopened.BuildProfile.Compatibility) ==
                              CompatibilityFingerprintUx8I(draft);
            reopened.Close();
            var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && CaptureCompatibilityApprovalUx8I() == sourceApproval;
            var succeeded = invalid && preview.CanonicalDryRun && runtimeRequired && migration && platform.Succeeded &&
                            platform.RequiredRolesCovered && platform.ContentVerified && staleSource && staleApproval &&
                            command && undo && redo && persistence && neighbors && sourceUnchanged;
            result = new(succeeded, true, invalid, runtimeRequired, migration,
                platform.RequiredRolesCovered, platform.ContentVerified, preview.CanonicalDryRun, command,
                staleSource, staleApproval, undo && redo, persistence, neighbors, sourceUnchanged, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidOperationException or ArgumentException)
        {
            result = FailedSmokeUx8I();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                temporaryReleased = !Directory.Exists(root);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                temporaryReleased = false;
            }
            _lastUx8ITemporaryReleased = temporaryReleased;
        }
        return result with
        {
            Succeeded = result.Succeeded && temporaryReleased,
            TemporaryWorkspaceReleased = temporaryReleased,
        };
    }

    private bool ValidateCompatibilitySourceUx8I(StudioUx8ICompatibilityDraft draft, out string error)
    {
        if (Project is null || Session is null || _buildProfileHistory is null ||
            draft.ProjectId != Project.Id.Value || draft.BaseRevision != _buildProfileHistory.State.Revision ||
            !string.Equals(draft.SourceFingerprint,
                BuildProfileFingerprintUx8C(_buildProfileHistory.State.Profile), StringComparison.Ordinal))
        {
            error = "Le profil de build a changé ; rechargez le formulaire.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private WorkspaceBuildProfile BuildCompatibilityCandidateUx8I(StudioUx8ICompatibilityDraft draft)
    {
        var source = _buildProfileHistory?.State.Profile ??
                     throw new InvalidOperationException("Aucun profil de build n’est ouvert.");
        var current = source.Compatibility;
        var compatibility = new ProjectCompatibilityProfile(
            current.Id,
            current.SchemaVersion,
            current.Provenance,
            draft.MinimumRuntimeVersion.Trim(),
            draft.ProjectFormatVersion.Trim(),
            draft.AutomaticMigrations,
            draft.RequiredAtRuntime);
        return new WorkspaceBuildProfile(
            source.Id, source.SchemaVersion, source.Provenance,
            source.RequireGitRepository, source.TextDocuments, source.ExplicitGeneratedFiles,
            source.ReproducibleAssets, source.Layout, source.CliOperations, source.StableOrdering,
            source.NormalizedTimestamps, source.LockedDependencies, source.ContentHashes,
            source.VerifyReproducibility, source.IncrementalBuild, source.CachePath,
            source.RebuildAffectedOnly, source.Governance, source.Collaboration, compatibility);
    }

    private bool BuildProfileStoreMatchesUx8I()
    {
        if (Session is null || _buildProfileHistory is null) return false;
        var loaded = WorkspaceBuildProfileStore.Load(Session.RootDirectory);
        return loaded.Succeeded &&
               BuildProfileFingerprintUx8C(loaded.Profile) ==
               BuildProfileFingerprintUx8C(_buildProfileHistory.State.Profile);
    }

    internal static string CompatibilityDraftFingerprintUx8I(StudioUx8ICompatibilityDraft draft) =>
        HashBinaryUx8I(writer =>
        {
            writer.Write(draft.ProjectId.ToByteArray());
            writer.Write(draft.BaseRevision.Value);
            WriteStringUx8I(writer, draft.SourceFingerprint);
            writer.Write(draft.CompatibilityId.ToByteArray());
            writer.Write(draft.SchemaVersion);
            WriteStringUx8I(writer, draft.Provenance);
            WriteStringUx8I(writer, draft.MinimumRuntimeVersion);
            WriteStringUx8I(writer, draft.ProjectFormatVersion);
            writer.Write(draft.AutomaticMigrations);
            writer.Write(draft.RequiredAtRuntime);
            writer.Write(draft.Approved);
        });

    private static StudioUx8ICompatibilityDraft FreezeCompatibilityDraftUx8I(
        StudioUx8ICompatibilityDraft draft) => draft with
        {
            MinimumRuntimeVersion = draft.MinimumRuntimeVersion.Trim(),
            ProjectFormatVersion = draft.ProjectFormatVersion.Trim(),
        };

    private static string CompatibilityNeighborsFingerprintUx8I(WorkspaceBuildProfile profile) =>
        HashUx8I(JsonSerializer.Serialize(new
        {
            profile.Id,
            profile.SchemaVersion,
            profile.Provenance,
            profile.RequireGitRepository,
            profile.TextDocuments,
            profile.ExplicitGeneratedFiles,
            profile.ReproducibleAssets,
            profile.Layout,
            profile.CliOperations,
            profile.StableOrdering,
            profile.NormalizedTimestamps,
            profile.LockedDependencies,
            profile.ContentHashes,
            profile.VerifyReproducibility,
            profile.IncrementalBuild,
            profile.CachePath,
            profile.RebuildAffectedOnly,
            profile.Governance,
            profile.Collaboration,
        }));

    private static string CompatibilityFingerprintUx8I(ProjectCompatibilityProfile profile) =>
        HashUx8I(JsonSerializer.Serialize(profile));

    private static string CompatibilityFingerprintUx8I(StudioUx8ICompatibilityDraft draft) =>
        CompatibilityFingerprintUx8I(new ProjectCompatibilityProfile(
            draft.CompatibilityId, draft.SchemaVersion, draft.Provenance,
            draft.MinimumRuntimeVersion, draft.ProjectFormatVersion,
            draft.AutomaticMigrations, draft.RequiredAtRuntime));

    private static bool VerifyRuntimeGateUx8I(Forge.Domain.Projects.ProjectDefinition project)
    {
        var required = ModuleManifest.Create(project, project.Module.Metadata.Version, "999.0.0", "1.0.0",
            new string('b', 64), target: ModuleTargetKind.Editor, runtimeCompatibilityRequired: true);
        var optional = required with { RuntimeCompatibilityRequired = false };
        return ModuleRuntimeCompatibility.Evaluate(required, ModuleRuntimeCompatibility.CurrentRuntimeVersion)?.Code ==
               ModuleRuntimeCompatibility.IncompatibleRuntimeCode &&
               ModuleRuntimeCompatibility.Evaluate(optional, ModuleRuntimeCompatibility.CurrentRuntimeVersion) is null;
    }

    private static AssetPackageManifest CreateEvidenceManifestUx8I(string root)
    {
        var assetId = AssetCatalogItemId.From(Guid.Parse("891f0000-0000-4000-8000-000000000025"));
        var model = Encoding.UTF8.GetBytes("ux8-i-model-platform-evidence");
        var texture = Encoding.UTF8.GetBytes("ux8-i-texture-platform-evidence");
        var modelPath = "assets/evidence/ux8-i/model.bin";
        var texturePath = "assets/evidence/ux8-i/texture.bin";
        WriteEvidenceChunkUx8I(root, modelPath, model);
        WriteEvidenceChunkUx8I(root, texturePath, texture);
        return new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(assetId),
            assetId,
            "studio:ux8-i-platform-evidence",
            "CC0-1.0",
            new string('c', 64),
            new Dictionary<AssetTargetPlatform, long>
            {
                [AssetTargetPlatform.Windows] = 4096,
                [AssetTargetPlatform.Linux] = 4096,
            },
            [
                new(Guid.Parse("891f0000-0000-4000-8000-000000000026"), AssetChunkRole.Model, true,
                    modelPath, HashBytesUx8I(model), model.Length, model.Length, AssetChunkCompression.None,
                    [AssetTargetPlatform.Any]),
                new(Guid.Parse("891f0000-0000-4000-8000-000000000027"), AssetChunkRole.Texture, true,
                    texturePath, HashBytesUx8I(texture), texture.Length, texture.Length, AssetChunkCompression.None,
                    [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux]),
            ],
            null, null, null, null, null);
    }

    private static void WriteEvidenceChunkUx8I(string root, string relativePath, byte[] content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static StudioUx8ICompatibilityPreview FailedCompatibilityUx8I(string message) => new(
        false, false, false, false, false, message, string.Empty, [], [message], []);

    private static StudioUx8IPlatformReport FailedPlatformUx8I(AssetTargetPlatform platform, string message) => new(
        platform, false, false, false, false, 0, 0, null, [], [message]);

    private static StudioUx8ISmokeResult FailedSmokeUx8I() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false);

    private static string HumanCompatibilityDiagnosticUx8I(string? code) => code switch
    {
        ProjectCompatibilityProfileValidator.SchemaUnsupportedCode => "Le schéma de compatibilité n’est pas pris en charge.",
        ProjectCompatibilityProfileValidator.IdentityInvalidCode => "L’identité interne du profil est invalide.",
        ProjectCompatibilityProfileValidator.VersionInvalidCode => "Les versions doivent respecter SemVer (ex. 1.30.0).",
        ProjectCompatibilityProfileValidator.AutomaticMigrationRequiredCode => "Les migrations automatiques versionnées sont obligatoires.",
        SetWorkspaceBuildProfileHandler.RevisionConflictCode => "Le profil a changé ; rechargez le formulaire.",
        _ => string.IsNullOrWhiteSpace(code) ? "Commande refusée." : code,
    };

    private static string HumanPlatformUx8I(AssetTargetPlatform platform) => platform switch
    {
        AssetTargetPlatform.Windows => "Windows",
        AssetTargetPlatform.Linux => "Linux",
        AssetTargetPlatform.MacOS => "macOS",
        AssetTargetPlatform.Android => "Android",
        AssetTargetPlatform.IOS => "iOS",
        _ => "Toutes plateformes",
    };

    private static string HumanRoleUx8I(AssetChunkRole role) => role switch
    {
        AssetChunkRole.Model => "modèle",
        AssetChunkRole.Texture => "textures",
        AssetChunkRole.Collision => "collision",
        AssetChunkRole.Animation => "animations",
        AssetChunkRole.Vfx => "VFX",
        AssetChunkRole.Lod => "LOD",
        AssetChunkRole.Navigation => "navigation",
        AssetChunkRole.Font => "police",
        _ => role.ToString(),
    };

    private static string HumanPathUx8I(string value) => ScrubHumanUx8I(value.Replace('\\', '/'));

    private static string ScrubHumanUx8I(string value) => Regex.Replace(
        value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé",
        RegexOptions.CultureInvariant);

    private static string HumanBytesUx8I(long bytes) => bytes switch
    {
        >= 1024L * 1024L * 1024L => $"{bytes / (1024d * 1024d * 1024d):0.##} Gio",
        >= 1024L * 1024L => $"{bytes / (1024d * 1024d):0.##} Mio",
        >= 1024L => $"{bytes / 1024d:0.##} Kio",
        _ => $"{bytes} octet(s)",
    };

    private static string HashBytesUx8I(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string HashUx8I(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string HashBinaryUx8I(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) write(writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteStringUx8I(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
