using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Content;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetAssetProcessingProfileHandler _setAssetProcessingUx3F = new(new SystemClock());
    private readonly SetAssetExternalEditHandler _setAssetExternalUx3F = new(new SystemClock());
    private AssetProcessingProfileCommandHistory? _processingHistoryUx3F;
    private AssetExternalEditCommandHistory? _externalHistoryUx3F;
    private AssetCatalogLoadResult? _catalogLoadUx3F;
    private UnifiedContentBrowser? _contentBrowserUx3F;
    private string? _assetWorkspaceRootUx3F;
    private StudioUx3FProcessingDraft? _approvedProcessingUx3F;
    private AssetProcessingProfileRevision? _approvedProcessingRevisionUx3F;
    private string? _approvedProcessingSourceUx3F;
    private string? _approvedProcessingDraftUx3F;
    private StudioUx3FExternalDraft? _approvedExternalUx3F;
    private AssetExternalEditRevision? _approvedExternalRevisionUx3F;
    private string? _approvedExternalSourceUx3F;
    private string? _approvedExternalDraftUx3F;
    private readonly Stack<RequestSnapshotUx3F> _externalRequestUndoUx3F = [];
    private readonly Stack<RequestSnapshotUx3F> _externalRequestRedoUx3F = [];
    private bool _processingCanonicalCommandUx3F;
    private bool _externalCanonicalCommandUx3F;

    internal bool CanUndoProcessingUx3F => _processingHistoryUx3F?.CanUndo == true;
    internal bool CanRedoProcessingUx3F => _processingHistoryUx3F?.CanRedo == true;
    internal bool CanUndoExternalUx3F => _externalHistoryUx3F?.CanUndo == true && _externalRequestUndoUx3F.Count > 0;
    internal bool CanRedoExternalUx3F => _externalHistoryUx3F?.CanRedo == true && _externalRequestRedoUx3F.Count > 0;

    internal StudioUx3FBrowserResult BrowseAssetsUx3F(
        string? text = null,
        ContentResourceKind? kind = null,
        int maximumResults = 800)
    {
        if (!EnsureAssetAuthoringStateUx3F(out var error))
            return new(false, error, 0, 0, []);
        try
        {
            var resources = _contentBrowserUx3F!.Search(new UnifiedContentBrowserQuery(
                Text: text,
                Kind: kind,
                MaximumResults: maximumResults));
            var assets = _catalogLoadUx3F!.Catalog!.Items.ToDictionary(item => item.Id);
            var choices = resources.Select(resource =>
            {
                var asset = resource.AssetCatalogId is null ? null : assets.GetValueOrDefault(resource.AssetCatalogId);
                var local = asset is not null && AssetSourceExistsUx3F(asset);
                var hasManifest = asset is not null && AssetManifests.Any(item => item.AssetId == asset.Id);
                var hasProfile = asset is not null && _processingHistoryUx3F!.State.Profiles.Any(item => item.AssetId == asset.Id);
                var hasExternal = asset is not null && _externalHistoryUx3F!.State.Find(asset.Id) is not null;
                return new StudioUx3FAssetChoice(
                    resource,
                    asset,
                    $"{ScrubHumanTextUx3F(resource.DisplayName)} · {HumanKindUx3F(resource.Kind)} · " +
                    ScrubHumanTextUx3F(resource.PackageNamespace),
                    HumanKindUx3F(resource.Kind),
                    HumanStatusUx3F(resource.Status, local),
                    local,
                    hasManifest,
                    hasProfile,
                    hasExternal);
            }).ToArray();
            return new(
                true,
                $"{choices.Length} résultat(s) sur {_contentBrowserUx3F.Items.Count} ressources unifiées.",
                _catalogLoadUx3F.Catalog.Items.Count,
                _contentBrowserUx3F.Items.Count,
                choices);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return new(false, exception.Message, _catalogLoadUx3F!.Catalog!.Items.Count,
                _contentBrowserUx3F!.Items.Count, []);
        }
    }

    internal StudioUx3FProcessingDraft? CreateProcessingDraftUx3F(
        AssetCatalogItemId assetId,
        AssetProcessingKind kind)
    {
        if (!EnsureAssetAuthoringStateUx3F(out _)) return null;
        var asset = FindAssetUx3F(assetId);
        if (asset is null) return null;
        try
        {
            var profile = _processingHistoryUx3F!.State.Find(assetId, kind) ??
                          AssetImportPlanner.CreateDefaultProfile(asset, kind);
            return DraftFromProfileUx3F(profile, _processingHistoryUx3F.State.Revision,
                ProcessingStateFingerprintUx3F(_processingHistoryUx3F.State), approved: false);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal StudioUx3FExternalDraft? CreateExternalDraftUx3F(AssetCatalogItemId assetId)
    {
        if (!EnsureAssetAuthoringStateUx3F(out _)) return null;
        var asset = FindAssetUx3F(assetId);
        if (asset is null || string.IsNullOrWhiteSpace(asset.RelativeModelPath)) return null;
        var existing = _externalHistoryUx3F!.State.Find(assetId);
        try
        {
            var configuration = existing ?? CreateExternalConfigurationUx3F(asset);
            return DraftFromExternalUx3F(configuration, _externalHistoryUx3F.State.Revision,
                ExternalStateFingerprintUx3F(_externalHistoryUx3F.State), approved: false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    internal StudioUx3FProcessingPreview PreviewProcessingUx3F(StudioUx3FProcessingDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!EnsureAssetAuthoringStateUx3F(out var error))
            return FailedProcessingUx3F(error, "asset.profile.workspace_unavailable", "assets/import-profiles");
        var currentFingerprint = ProcessingStateFingerprintUx3F(_processingHistoryUx3F!.State);
        var persisted = AssetProcessingProfileStore.Load(_assetWorkspaceRootUx3F!);
        if (!persisted.Succeeded || draft.BaseRevision != _processingHistoryUx3F.State.Revision ||
            draft.SourceFingerprint != currentFingerprint ||
            ProfilesFingerprintUx3F(persisted.Profiles) != ProfilesFingerprintUx3F(_processingHistoryUx3F.State.Profiles))
        {
            CancelProcessingApprovalUx3F();
            return FailedProcessingUx3F(
                "Les profils d’import ont changé ; rechargez l’asset avant de prévisualiser.",
                SetAssetProcessingProfileHandler.RevisionConflictCode,
                "assets/import-profiles");
        }
        AssetProcessingProfile profile;
        try
        {
            profile = BuildProfileUx3F(draft);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            CancelProcessingApprovalUx3F();
            return FailedProcessingUx3F(exception.Message, "asset.profile.draft_invalid", "assets/profile");
        }
        var command = new SetAssetProcessingProfileCommand(
            CommandId.From(Guid.NewGuid()),
            _processingHistoryUx3F.State.Revision,
            profile,
            DryRun: true);
        var result = _setAssetProcessingUx3F.Handle(_processingHistoryUx3F.State, command);
        var asset = FindAssetUx3F(profile.AssetId);
        var plan = asset is null ? null : AssetImportPlanner.Plan(asset, profile);
        var diagnostics = AssetProcessingProfileValidator.Validate(profile)
            .Select(item => new StudioUx3FDiagnostic(item.Code, item.Path, item.Message))
            .Concat(plan?.Diagnostics.Select(item => new StudioUx3FDiagnostic(item.Code, item.Path, item.Message)) ?? [])
            .DistinctBy(item => (item.Code, item.Path))
            .ToArray();
        var dryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var importPlan = plan is { Succeeded: true, Plan: not null };
        var succeeded = dryRun && importPlan && diagnostics.Length == 0;
        var frozen = FreezeProcessingDraftUx3F(draft);
        if (succeeded && draft.Approved)
        {
            _approvedProcessingUx3F = frozen;
            _approvedProcessingRevisionUx3F = draft.BaseRevision;
            _approvedProcessingSourceUx3F = draft.SourceFingerprint;
            _approvedProcessingDraftUx3F = ProcessingDraftFingerprintUx3F(frozen);
        }
        else CancelProcessingApprovalUx3F();
        var previous = _processingHistoryUx3F.State.Find(profile.AssetId, profile.Kind);
        var diff = new List<string>
        {
            previous is null ? "Ajouter un profil d’import gouverné." : "Remplacer le profil d’import sélectionné.",
            $"Format {profile.SourceFormat.ToUpperInvariant()} → {profile.CanonicalFormat.ToUpperInvariant()}.",
            $"Budget de sortie : {HumanBytesUx3F(profile.MaximumOutputBytes)}.",
            profile.Kind == AssetProcessingKind.Texture
                ? $"Texture · {profile.Texture!.MaximumDimension}px · {profile.Texture.Channels.Count} canal(aux)."
                : $"Modèle · {profile.Model!.LodRatios.Count} LOD · collisions {(profile.Model.GenerateCollisions ? "générées" : "conservées")}."
        };
        var tasks = plan?.Plan?.Tasks.Select(item =>
            $"{HumanTaskUx3F(item.Name)} → {HumanPathUx3F(item.OutputRelativePath)}").ToArray() ?? [];
        return new(
            succeeded,
            succeeded && draft.Approved,
            dryRun,
            importPlan,
            true,
            succeeded
                ? (draft.Approved ? "Aperçu sec prêt ; application autorisée." : "Aperçu sec prêt ; approbation requise.")
                : "Le profil ou son plan d’import doit être corrigé.",
            result.Patch?.Summary ?? "Aucun patch canonique.",
            result.Inverse?.Summary ?? "Aucun inverse canonique.",
            diff,
            diagnostics,
            tasks);
    }

    internal StudioUx3FExternalPreview PreviewExternalUx3F(StudioUx3FExternalDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!EnsureAssetAuthoringStateUx3F(out var error))
            return FailedExternalUx3F(error, "asset.external_edit.workspace_unavailable", "assets/external-edits");
        var currentFingerprint = ExternalStateFingerprintUx3F(_externalHistoryUx3F!.State);
        var persisted = AssetExternalEditStore.Load(_assetWorkspaceRootUx3F!);
        if (!persisted.Succeeded || draft.BaseRevision != _externalHistoryUx3F.State.Revision ||
            draft.SourceFingerprint != currentFingerprint ||
            ExternalConfigurationsFingerprintUx3F(persisted.Configurations) !=
            ExternalConfigurationsFingerprintUx3F(_externalHistoryUx3F.State.Configurations))
        {
            CancelExternalApprovalUx3F();
            return FailedExternalUx3F(
                "Les configurations d’édition externe ont changé ; rechargez l’asset.",
                SetAssetExternalEditHandler.RevisionConflictCode,
                "assets/external-edits");
        }
        var configuration = BuildExternalUx3F(draft);
        var command = new SetAssetExternalEditCommand(
            CommandId.From(Guid.NewGuid()),
            _externalHistoryUx3F.State.Revision,
            configuration,
            DryRun: true);
        var result = _setAssetExternalUx3F.Handle(_externalHistoryUx3F.State, command);
        var diagnostics = AssetExternalEditValidator.Validate(configuration)
            .Select(item => new StudioUx3FDiagnostic(item.Code, item.Path, item.Message)).ToList();
        var workerReady = ValidateWorkerRequestUx3F(configuration, diagnostics);
        var watchReady = ValidateWatchSourceUx3F(configuration, diagnostics);
        var dryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var succeeded = dryRun && workerReady && watchReady && diagnostics.Count == 0;
        var frozen = FreezeExternalDraftUx3F(draft);
        if (succeeded && draft.Approved)
        {
            _approvedExternalUx3F = frozen;
            _approvedExternalRevisionUx3F = draft.BaseRevision;
            _approvedExternalSourceUx3F = draft.SourceFingerprint;
            _approvedExternalDraftUx3F = ExternalDraftFingerprintUx3F(frozen);
        }
        else CancelExternalApprovalUx3F();
        var previous = _externalHistoryUx3F.State.Find(configuration.AssetId);
        var diff = new[]
        {
            previous is null ? "Ajouter une configuration Blender typée." : "Remplacer la configuration Blender sélectionnée.",
            $"{configuration.Operations.Count} opération(s) autorisée(s), aucun script libre.",
            $"{configuration.TargetPlatforms.Count} plateforme(s) · surveillance {(configuration.WatchForChanges ? "active" : "désactivée") }.",
            $"Sortie confinée : {HumanPathUx3F(configuration.OutputRelativePath)}."
        };
        return new(
            succeeded,
            succeeded && draft.Approved,
            dryRun,
            workerReady,
            watchReady,
            true,
            succeeded
                ? (draft.Approved ? "Aperçu sec Blender prêt ; application autorisée." : "Aperçu sec Blender prêt ; approbation requise.")
                : "La configuration externe doit être corrigée.",
            result.Patch?.Summary ?? "Aucun patch canonique.",
            result.Inverse?.Summary ?? "Aucun inverse canonique.",
            diff,
            diagnostics.DistinctBy(item => (item.Code, item.Path)).ToArray(),
            configuration.Operations.Select(HumanExternalOperationUx3F).ToArray());
    }

    internal StudioUx3FActionResult ApplyApprovedProcessingUx3F(StudioUx3FProcessingDraft current)
    {
        if (!EnsureAssetAuthoringStateUx3F(out var error)) return new(false, error);
        var frozen = FreezeProcessingDraftUx3F(current);
        if (_approvedProcessingUx3F is null || _approvedProcessingRevisionUx3F != _processingHistoryUx3F!.State.Revision ||
            _approvedProcessingSourceUx3F != ProcessingStateFingerprintUx3F(_processingHistoryUx3F.State) ||
            _approvedProcessingDraftUx3F != ProcessingDraftFingerprintUx3F(frozen) ||
            !current.Approved || !ProcessingStoreMatchesUx3F())
            return new(false, "L’approbation du profil est absente ou périmée ; relancez l’aperçu sec.");
        var profile = BuildProfileUx3F(current);
        var result = _setAssetProcessingUx3F.Handle(_processingHistoryUx3F.State,
            new SetAssetProcessingProfileCommand(CommandId.From(Guid.NewGuid()),
                _processingHistoryUx3F.State.Revision, profile, DryRun: false));
        if (!result.Succeeded || result.NewRevision is null || result.Patch is null || result.Inverse is null)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application du profil refusée.");
        var saved = AssetProcessingProfileStore.Save(_assetWorkspaceRootUx3F!, result.State.Profiles);
        if (!saved.Succeeded)
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Écriture du profil impossible.");
        _processingHistoryUx3F.Record(result);
        _processingCanonicalCommandUx3F = result.Audit.CommandName == "set_asset_processing_profile";
        CancelProcessingApprovalUx3F();
        return new(true, "Profil d’import appliqué et enregistré atomiquement.");
    }

    internal StudioUx3FActionResult ApplyApprovedExternalUx3F(StudioUx3FExternalDraft current)
    {
        if (!EnsureAssetAuthoringStateUx3F(out var error)) return new(false, error);
        var frozen = FreezeExternalDraftUx3F(current);
        if (_approvedExternalUx3F is null || _approvedExternalRevisionUx3F != _externalHistoryUx3F!.State.Revision ||
            _approvedExternalSourceUx3F != ExternalStateFingerprintUx3F(_externalHistoryUx3F.State) ||
            _approvedExternalDraftUx3F != ExternalDraftFingerprintUx3F(frozen) ||
            !current.Approved || !ExternalStoreMatchesUx3F())
            return new(false, "L’approbation Blender est absente ou périmée ; relancez l’aperçu sec.");
        var configuration = BuildExternalUx3F(current);
        var result = _setAssetExternalUx3F.Handle(_externalHistoryUx3F.State,
            new SetAssetExternalEditCommand(CommandId.From(Guid.NewGuid()),
                _externalHistoryUx3F.State.Revision, configuration, DryRun: false));
        if (!result.Succeeded || result.NewRevision is null || result.Patch is null || result.Inverse is null)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Configuration Blender refusée.");
        var previous = _externalHistoryUx3F.State.Configurations;
        var requestRelative = WorkerRequestRelativePathUx3F(configuration.AssetId);
        var requestBefore = ReadOptionalUx3F(_assetWorkspaceRootUx3F!, requestRelative);
        var saved = AssetExternalEditStore.Save(_assetWorkspaceRootUx3F!, result.State.Configurations);
        if (!saved.Succeeded)
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Écriture de la configuration impossible.");
        var worker = BlenderWorkerRequestStore.Save(_assetWorkspaceRootUx3F!, configuration);
        if (!worker.Succeeded || worker.RequestRelativePath is null)
        {
            AssetExternalEditStore.Save(_assetWorkspaceRootUx3F!, previous);
            return new(false, worker.Diagnostics.FirstOrDefault()?.Message ?? "Requête Blender invalide.");
        }
        var requestAfter = ReadOptionalUx3F(_assetWorkspaceRootUx3F!, worker.RequestRelativePath);
        _externalHistoryUx3F.Record(result);
        _externalRequestUndoUx3F.Push(new(worker.RequestRelativePath, requestBefore, requestAfter));
        _externalRequestRedoUx3F.Clear();
        _externalCanonicalCommandUx3F = result.Audit.CommandName == "set_asset_external_edit";
        CancelExternalApprovalUx3F();
        return new(true, "Configuration et requête Blender typée enregistrées ; aucun processus n’a été lancé.");
    }

    internal StudioUx3FActionResult UndoProcessingUx3F() => MoveProcessingHistoryUx3F(undo: true);
    internal StudioUx3FActionResult RedoProcessingUx3F() => MoveProcessingHistoryUx3F(undo: false);
    internal StudioUx3FActionResult UndoExternalUx3F() => MoveExternalHistoryUx3F(undo: true);
    internal StudioUx3FActionResult RedoExternalUx3F() => MoveExternalHistoryUx3F(undo: false);

    internal StudioUx3FActionResult SaveAssetAuthoringUx3F()
    {
        if (!EnsureAssetAuthoringStateUx3F(out var error)) return new(false, error);
        var profiles = AssetProcessingProfileStore.Save(_assetWorkspaceRootUx3F!, _processingHistoryUx3F!.State.Profiles);
        var external = AssetExternalEditStore.Save(_assetWorkspaceRootUx3F!, _externalHistoryUx3F!.State.Configurations);
        return profiles.Succeeded && external.Succeeded
            ? new(true, "Profils d’import et éditions externes enregistrés.")
            : new(false, profiles.Diagnostics.Concat(external.Diagnostics).FirstOrDefault()?.Message ?? "Enregistrement impossible.");
    }

    internal StudioUx3FApprovalSnapshot CaptureAssetAuthoringApprovalUx3F() => new(
        _approvedProcessingUx3F, _approvedProcessingRevisionUx3F, _approvedProcessingSourceUx3F,
        _approvedProcessingDraftUx3F, _approvedExternalUx3F, _approvedExternalRevisionUx3F,
        _approvedExternalSourceUx3F, _approvedExternalDraftUx3F);

    internal void RestoreAssetAuthoringApprovalUx3F(StudioUx3FApprovalSnapshot snapshot)
    {
        _approvedProcessingUx3F = snapshot.ProcessingDraft;
        _approvedProcessingRevisionUx3F = snapshot.ProcessingRevision;
        _approvedProcessingSourceUx3F = snapshot.ProcessingSourceFingerprint;
        _approvedProcessingDraftUx3F = snapshot.ProcessingDraftFingerprint;
        _approvedExternalUx3F = snapshot.ExternalDraft;
        _approvedExternalRevisionUx3F = snapshot.ExternalRevision;
        _approvedExternalSourceUx3F = snapshot.ExternalSourceFingerprint;
        _approvedExternalDraftUx3F = snapshot.ExternalDraftFingerprint;
    }

    internal void CancelAssetAuthoringApprovalsUx3F()
    {
        CancelProcessingApprovalUx3F();
        CancelExternalApprovalUx3F();
    }

    internal void CloseUx3F()
    {
        CancelAssetAuthoringApprovalsUx3F();
        _processingHistoryUx3F = null;
        _externalHistoryUx3F = null;
        _catalogLoadUx3F = null;
        _contentBrowserUx3F = null;
        _assetWorkspaceRootUx3F = null;
        _externalRequestUndoUx3F.Clear();
        _externalRequestRedoUx3F.Clear();
        _processingCanonicalCommandUx3F = false;
        _externalCanonicalCommandUx3F = false;
    }

    internal StudioUx3FEvidencePlan? CreateAssetAuthoringEvidencePlanUx3F()
    {
        if (Project is null || Session is null) return null;
        if (!EnsureAssetAuthoringStateUx3F(out _))
        {
            return AssetCatalogLoader.FindRepositoryRoot(Session.RootDirectory) is null
                ? CreateCanonicalEvidencePlanUx3F(Project, Session.RootDirectory)
                : null;
        }
        foreach (var asset in _catalogLoadUx3F!.Catalog!.Items
                     .Where(item => item.CanPlanImport && !string.IsNullOrWhiteSpace(item.RelativeModelPath))
                     .OrderByDescending(item => _processingHistoryUx3F!.State.Find(item.Id, AssetProcessingKind.Model) is not null)
                     .ThenBy(item => item.DisplayName, StringComparer.Ordinal))
        {
            var source = ResolveVaultPathUx3F(asset.RelativeModelPath!);
            if (!File.Exists(source)) continue;
            try
            {
                var existingProfile = _processingHistoryUx3F!.State.Find(asset.Id, AssetProcessingKind.Model);
                var profile = existingProfile ?? AssetImportPlanner.CreateDefaultProfile(asset, AssetProcessingKind.Model);
                var existingManifest = AssetManifests.FirstOrDefault(item => item.AssetId == asset.Id);
                var plan = AssetImportPlanner.Plan(asset, profile);
                if (!plan.Succeeded || plan.Plan is null) continue;
                var firstTask = plan.Plan.Tasks.First();
                var manifest = existingManifest ?? AssetPackageManifestFactory.Create(asset, profile,
                    new AssetManifestArtifactInput(firstTask.OutputRelativePath, asset.ContentSha256,
                        Math.Max(1, asset.TotalBytes), profile.SourceFormat == profile.CanonicalFormat));
                var real = existingProfile is not null && existingManifest is not null;
                return new(Project, _catalogLoadUx3F.RepositoryRoot, _catalogLoadUx3F.VaultRoot,
                    asset, manifest, profile, real, !real);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                // Try the next local governed model.
            }
        }
        return CreateCanonicalEvidencePlanUx3F(Project, Session.RootDirectory);
    }

    internal static bool MaterializeAssetAuthoringEvidenceUx3F(string root, StudioUx3FEvidencePlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            var profile = plan.Profile;
            var manifest = plan.Manifest;
            if (plan.UsesCanonicalFallback)
            {
                MaterializeCanonicalCatalogUx3F(root, plan.Asset);
                var catalog = AssetCatalogLoader.Load(root, Path.Combine(root, ".asset-vault"));
                var materialized = catalog.Catalog?.Items.FirstOrDefault(item => item.Id == plan.Asset.Id);
                if (materialized is null) return false;
                profile = AssetImportPlanner.CreateDefaultProfile(materialized, AssetProcessingKind.Model);
                var import = AssetImportPlanner.Plan(materialized, profile);
                if (!import.Succeeded || import.Plan is null) return false;
                var firstTask = import.Plan.Tasks.First();
                manifest = AssetPackageManifestFactory.Create(materialized, profile,
                    new AssetManifestArtifactInput(firstTask.OutputRelativePath, materialized.ContentSha256,
                        Math.Max(1, materialized.TotalBytes), profile.SourceFormat == profile.CanonicalFormat));
            }
            else
            {
                var catalogSource = Path.Combine(plan.RepositoryRoot, "asset_manifests", "catalogs");
                var catalogTarget = Path.Combine(root, "asset_manifests", "catalogs");
                Directory.CreateDirectory(catalogTarget);
                foreach (var file in Directory.EnumerateFiles(catalogSource, "*.json")
                             .OrderBy(item => item, StringComparer.Ordinal))
                    File.Copy(file, Path.Combine(catalogTarget, Path.GetFileName(file)), overwrite: true);
                CopyVaultFileUx3F(plan.VaultRoot, root, plan.Asset.RelativeModelPath);
                CopyVaultFileUx3F(plan.VaultRoot, root, plan.Asset.RelativePreviewPath);
            }
            File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# Isolated UX3-F evidence\n", new UTF8Encoding(false));
            var neighborProfile = CreateNeighborProfileUx3F();
            var neighborExternal = CreateNeighborExternalUx3F();
            return AssetPackageManifestStore.Save(root, [manifest]).Succeeded &&
                   AssetProcessingProfileStore.Save(root, [profile, neighborProfile]).Succeeded &&
                   AssetExternalEditStore.Save(root, [neighborExternal]).Succeeded;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static StudioUx3FEvidencePlan? CreateCanonicalEvidencePlanUx3F(
        Forge.Domain.Projects.ProjectDefinition project,
        string sourceRoot)
    {
        const string externalId = "studio:ux3-f-model";
        const string modelPath = "normalized/studio/ux3-f/model.glb";
        var payload = CanonicalModelPayloadUx3F();
        var fileSha256 = HashBytesUx3F(payload);
        var catalogSha256 = CatalogFileFingerprintUx3F(modelPath, fileSha256);
        try
        {
            var asset = new AssetCatalogItem(
                AssetCatalogItemId.ForExternalId(externalId),
                externalId,
                "Modèle canonique Studio",
                "studio",
                "ux3-f-model",
                "asset://studio/ux3-f/model",
                "CC0-1.0",
                "asset://studio/licenses/cc0-1.0",
                true,
                catalogSha256,
                payload.Length,
                ["3d-model"],
                ["studio", "evidence", "model"],
                "downloaded-and-extracted",
                AssetAvailability.ExtractedAvailable,
                modelPath,
                null,
                null,
                $"asset-catalog:{externalId}");
            var profile = AssetImportPlanner.CreateDefaultProfile(asset, AssetProcessingKind.Model);
            var import = AssetImportPlanner.Plan(asset, profile);
            if (!import.Succeeded || import.Plan is null) return null;
            var firstTask = import.Plan.Tasks.First();
            var manifest = AssetPackageManifestFactory.Create(asset, profile,
                new AssetManifestArtifactInput(firstTask.OutputRelativePath, asset.ContentSha256,
                    Math.Max(1, asset.TotalBytes), profile.SourceFormat == profile.CanonicalFormat));
            return new(project, sourceRoot, sourceRoot, asset, manifest, profile,
                UsesRealModule: false, UsesCanonicalFallback: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void MaterializeCanonicalCatalogUx3F(string root, AssetCatalogItem model)
    {
        const string catalogDate = "2026-08-23";
        const string textureExternalId = "studio:ux3-f-texture";
        const string texturePath = "previews/studio/ux3-f/texture.png";
        var modelPayload = CanonicalModelPayloadUx3F();
        var modelFileSha256 = HashBytesUx3F(modelPayload);
        var texturePayload = CanonicalTexturePayloadUx3F();
        var textureFileSha256 = HashBytesUx3F(texturePayload);
        var catalogRoot = Path.Combine(root, "asset_manifests", "catalogs");
        var vaultRoot = Path.Combine(root, ".asset-vault");
        Directory.CreateDirectory(catalogRoot);
        WriteCanonicalVaultFileUx3F(vaultRoot, model.RelativeModelPath!, modelPayload);

        var unitCatalog = new
        {
            schema_version = AssetCatalog.CurrentSchemaVersion,
            catalog_date = catalogDate,
            license_policy = "cc0-only",
            assets = new object[]
            {
                new
                {
                    asset_id = model.ExternalId,
                    catalog_date = catalogDate,
                    categories = model.Categories,
                    files = new[]
                    {
                        new { path = model.RelativeModelPath, sha256 = modelFileSha256, size = modelPayload.Length,
                            url = "asset://studio/ux3-f/model.glb" },
                    },
                    license = model.LicenseSpdx,
                    license_url = model.LicenseUrl,
                    license_verified = true,
                    name = model.DisplayName,
                    schema_version = AssetCatalogItem.CurrentSchemaVersion,
                    source = model.Source,
                    source_asset_id = model.SourceAssetId,
                    source_page = model.SourcePage,
                    status = model.CatalogStatus,
                    tags = model.Tags,
                    total_bytes = model.TotalBytes,
                    attribution_required = false,
                },
                new
                {
                    asset_id = textureExternalId,
                    catalog_date = catalogDate,
                    categories = new[] { "texture" },
                    files = new[]
                    {
                        new { path = texturePath, sha256 = textureFileSha256, size = texturePayload.Length,
                            url = "asset://studio/ux3-f/texture.png" },
                    },
                    license = "CC0-1.0",
                    license_url = "asset://studio/licenses/cc0-1.0",
                    license_verified = true,
                    name = "Texture canonique Studio",
                    schema_version = AssetCatalogItem.CurrentSchemaVersion,
                    source = "studio",
                    source_asset_id = "ux3-f-texture",
                    source_page = "asset://studio/ux3-f/texture",
                    status = "catalogued",
                    tags = new[] { "studio", "evidence", "texture" },
                    total_bytes = texturePayload.Length,
                    attribution_required = false,
                },
            },
        };
        var packCatalog = new
        {
            schema_version = AssetCatalog.CurrentSchemaVersion,
            catalog_date = catalogDate,
            license_policy = "cc0-only",
            packs = Array.Empty<object>(),
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(catalogRoot, "cc0_3d_all.json"),
            JsonSerializer.Serialize(unitCatalog, options), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(catalogRoot, "kenney_3d_packs.json"),
            JsonSerializer.Serialize(packCatalog, options), new UTF8Encoding(false));
    }

    private static void WriteCanonicalVaultFileUx3F(string vaultRoot, string relativePath, byte[] payload)
    {
        var path = Path.Combine(vaultRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, payload);
    }

    private static byte[] CanonicalModelPayloadUx3F() =>
        Encoding.UTF8.GetBytes("openrpg-forge:ux3-f:canonical-model-evidence-v1");

    private static byte[] CanonicalTexturePayloadUx3F() =>
        Encoding.UTF8.GetBytes("openrpg-forge:ux3-f:canonical-texture-evidence-v1");

    private static string HashBytesUx3F(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    private static string CatalogFileFingerprintUx3F(string relativePath, string fileSha256) =>
        HashBytesUx3F(Encoding.UTF8.GetBytes($"{relativePath}:{fileSha256}"));

    internal StudioUx3FSmokeResult RunUx3FContractSmoke()
    {
        var evidence = CreateAssetAuthoringEvidencePlanUx3F();
        if (evidence is null) return EmptyAssetAuthoringSmokeUx3F();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-f-contract-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var released = false;
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !MaterializeAssetAuthoringEvidenceUx3F(root, evidence) ||
                !editing.Open(root).Succeeded)
                return EmptyAssetAuthoringSmokeUx3F();
            var sourceProject = ProjectJsonSerializer.Serialize(editing.Project!);
            var browser = editing.BrowseAssetsUx3F(maximumResults: 2_000);
            var choice = browser.Choices.FirstOrDefault(item => item.Asset?.Id == evidence.Asset.Id);
            var textureAndModel = browser.Choices.Any(item => item.Resource.Kind == ContentResourceKind.Texture) &&
                                  browser.Choices.Any(item => item.Resource.Kind == ContentResourceKind.Model);
            if (choice?.Asset is null) return EmptyAssetAuthoringSmokeUx3F();
            var initialProfiles = editing._processingHistoryUx3F!.State.Profiles;
            var initialExternal = editing._externalHistoryUx3F!.State.Configurations;
            var neighborProfile = initialProfiles.Single(item => item.AssetId != evidence.Asset.Id);
            var neighborExternal = initialExternal.Single();
            var profileDraft = editing.CreateProcessingDraftUx3F(evidence.Asset.Id, AssetProcessingKind.Model);
            var externalDraft = editing.CreateExternalDraftUx3F(evidence.Asset.Id);
            if (profileDraft is null || externalDraft is null) return EmptyAssetAuthoringSmokeUx3F();
            profileDraft = profileDraft with
            {
                MaximumOutputBytes = checked(profileDraft.MaximumOutputBytes + 4096),
                ModelGenerateCollisions = !profileDraft.ModelGenerateCollisions,
                Approved = true,
            };
            externalDraft = externalDraft with
            {
                ModificationProvenance = externalDraft.ModificationProvenance + ":ux3-f",
                WatchForChanges = true,
                Approved = true,
            };
            var processingPreview = editing.PreviewProcessingUx3F(profileDraft);
            var externalPreview = editing.PreviewExternalUx3F(externalDraft);
            var separateApprovals = editing.CaptureAssetAuthoringApprovalUx3F() is
            { ProcessingDraft: not null, ExternalDraft: not null };
            var processingApplied = processingPreview.CanApply && editing.ApplyApprovedProcessingUx3F(profileDraft).Succeeded;
            var externalStillApproved = editing.CaptureAssetAuthoringApprovalUx3F().ExternalDraft is not null;
            var staleProcessing = !editing.PreviewProcessingUx3F(profileDraft).Succeeded;
            var externalApplied = externalStillApproved && externalPreview.CanApply &&
                                  editing.ApplyApprovedExternalUx3F(externalDraft).Succeeded;
            var staleExternal = !editing.PreviewExternalUx3F(externalDraft).Succeeded;
            var finalProfiles = ProfilesFingerprintUx3F(editing._processingHistoryUx3F.State.Profiles);
            var finalExternal = ExternalConfigurationsFingerprintUx3F(editing._externalHistoryUx3F.State.Configurations);
            var watchPlan = ExerciseWatchPlanUx3F(editing, BuildExternalUx3F(externalDraft));
            var undoExternal = editing.UndoExternalUx3F().Succeeded;
            var undoProcessing = editing.UndoProcessingUx3F().Succeeded;
            var undone = ProfilesFingerprintUx3F(editing._processingHistoryUx3F.State.Profiles) ==
                         ProfilesFingerprintUx3F(initialProfiles) &&
                         ExternalConfigurationsFingerprintUx3F(editing._externalHistoryUx3F.State.Configurations) ==
                         ExternalConfigurationsFingerprintUx3F(initialExternal);
            var redoProcessing = editing.RedoProcessingUx3F().Succeeded;
            var redoExternal = editing.RedoExternalUx3F().Succeeded;
            var redone = ProfilesFingerprintUx3F(editing._processingHistoryUx3F.State.Profiles) == finalProfiles &&
                         ExternalConfigurationsFingerprintUx3F(editing._externalHistoryUx3F.State.Configurations) == finalExternal;
            var reopened = new StudioSessionController();
            var persistence = reopened.Open(root).Succeeded && reopened.EnsureAssetAuthoringStateUx3F(out _) &&
                              ProfilesFingerprintUx3F(reopened._processingHistoryUx3F!.State.Profiles) == finalProfiles &&
                              ExternalConfigurationsFingerprintUx3F(reopened._externalHistoryUx3F!.State.Configurations) == finalExternal &&
                              ProjectJsonSerializer.Serialize(reopened.Project!) == sourceProject;
            reopened.Close();
            var profileNeighbor = editing._processingHistoryUx3F.State.Profiles.Any(item =>
                item.AssetId == neighborProfile.AssetId && ProfileFingerprintUx3F(item) == ProfileFingerprintUx3F(neighborProfile));
            var externalNeighbor = editing._externalHistoryUx3F.State.Configurations.Any(item =>
                item.AssetId == neighborExternal.AssetId && ExternalFingerprintUx3F(item) == ExternalFingerprintUx3F(neighborExternal));
            var canonical = editing._processingCanonicalCommandUx3F && editing._externalCanonicalCommandUx3F;
            var projectPreserved = ProjectJsonSerializer.Serialize(editing.Project!) == sourceProject;
            var human = !choice.Label.Contains(evidence.Asset.Id.ToString(), StringComparison.OrdinalIgnoreCase);
            var coreSucceeded = browser.Succeeded && browser.CatalogCount >= 1 &&
                                browser.UnifiedCount >= browser.CatalogCount && textureAndModel &&
                                processingPreview.DryRunSucceeded && processingPreview.ImportPlanReady &&
                                externalPreview.DryRunSucceeded && externalPreview.WorkerRequestValidated &&
                                watchPlan && staleProcessing && staleExternal && separateApprovals && canonical &&
                                processingApplied && externalApplied && undoExternal && undoProcessing && undone &&
                                redoProcessing && redoExternal && redone && persistence && profileNeighbor &&
                                externalNeighbor && projectPreserved && human &&
                                processingPreview.RuntimeClaimsBounded && externalPreview.RuntimeClaimsBounded;
            var result = new StudioUx3FSmokeResult(
                coreSucceeded,
                true,
                browser.Succeeded && browser.CatalogCount >= 1 && browser.UnifiedCount >= browser.CatalogCount,
                textureAndModel,
                evidence.UsesRealModule,
                evidence.UsesCanonicalFallback,
                processingPreview.DryRunSucceeded,
                processingPreview.ImportPlanReady,
                externalPreview.DryRunSucceeded,
                externalPreview.WorkerRequestValidated,
                watchPlan,
                staleProcessing,
                staleExternal,
                separateApprovals,
                canonical,
                undoExternal && undoProcessing && redoProcessing && redoExternal && undone && redone,
                persistence,
                profileNeighbor,
                externalNeighbor,
                projectPreserved,
                human,
                processingPreview.RuntimeClaimsBounded && externalPreview.RuntimeClaimsBounded,
                false);
            return result;
        }
        finally
        {
            editing.Close();
            released = DeleteTemporaryBlueprintUx3A(root);
            _lastUx3FTemporaryReleased = released;
        }
    }

    private bool _lastUx3FTemporaryReleased = true;
    internal bool LastUx3FTemporaryReleased => _lastUx3FTemporaryReleased;

    internal string ProcessingDraftFingerprintUx3F(StudioUx3FProcessingDraft draft) => HashBinaryUx3F(writer =>
    {
        writer.Write(draft.BaseRevision.Value);
        WriteStringUx3F(writer, draft.SourceFingerprint);
        WriteStringUx3F(writer, draft.AssetId.ToString());
        writer.Write((int)draft.Kind);
        WriteStringUx3F(writer, draft.SourceFormat);
        WriteStringUx3F(writer, draft.CanonicalFormat);
        WriteStringUx3F(writer, draft.LicenseSpdx);
        WriteStringUx3F(writer, draft.ContentSha256);
        WriteStringUx3F(writer, draft.Provenance);
        writer.Write(draft.MaximumOutputBytes);
        writer.Write((int)draft.TextureColorSpace);
        writer.Write((int)draft.TextureCompression);
        writer.Write(draft.TextureMaximumDimension);
        writer.Write(draft.TextureTexelDensity);
        WriteDictionaryUx3F(writer, draft.TextureChannels);
        writer.Write(draft.ModelImportRig);
        writer.Write(draft.ModelImportAnimations);
        writer.Write(draft.ModelGenerateCollisions);
        WriteStringsUx3F(writer, draft.ModelSockets);
        writer.Write(draft.ModelLodRatios.Count);
        foreach (var value in draft.ModelLodRatios) writer.Write(value);
        writer.Write(draft.Approved);
    });

    internal string ExternalDraftFingerprintUx3F(StudioUx3FExternalDraft draft) => HashBinaryUx3F(writer =>
    {
        writer.Write(draft.BaseRevision.Value);
        WriteStringUx3F(writer, draft.SourceFingerprint);
        WriteExternalUx3F(writer, BuildExternalUx3F(draft));
        writer.Write(draft.Approved);
    });

    private bool EnsureAssetAuthoringStateUx3F(out string error)
    {
        error = string.Empty;
        if (Session is null || Project is null)
        {
            error = "Aucun workspace Studio n’est ouvert.";
            return false;
        }
        if (_assetWorkspaceRootUx3F == Session.RootDirectory && _processingHistoryUx3F is not null &&
            _externalHistoryUx3F is not null && _catalogLoadUx3F?.Catalog is not null && _contentBrowserUx3F is not null)
            return true;
        var profiles = AssetProcessingProfileStore.Load(Session.RootDirectory);
        var external = AssetExternalEditStore.Load(Session.RootDirectory);
        var repository = AssetCatalogLoader.FindRepositoryRoot(Session.RootDirectory);
        if (!profiles.Succeeded || !external.Succeeded || repository is null)
        {
            error = profiles.Diagnostics.Concat(external.Diagnostics).FirstOrDefault()?.Message ??
                    "Le dépôt des assets est introuvable depuis ce workspace.";
            return false;
        }
        var containedVault = Path.Combine(repository, ".asset-vault");
        var catalog = AssetCatalogLoader.Load(repository, Directory.Exists(containedVault) ? containedVault : null);
        if (catalog.Catalog is null)
        {
            error = catalog.Diagnostics.FirstOrDefault()?.Message ?? "Le catalogue d’assets est indisponible.";
            return false;
        }
        _assetWorkspaceRootUx3F = Session.RootDirectory;
        _processingHistoryUx3F = new AssetProcessingProfileCommandHistory(profiles.Profiles);
        _externalHistoryUx3F = new AssetExternalEditCommandHistory(external.Configurations);
        _catalogLoadUx3F = catalog;
        _contentBrowserUx3F = new UnifiedContentBrowser(Project, catalog.Catalog);
        _externalRequestUndoUx3F.Clear();
        _externalRequestRedoUx3F.Clear();
        CancelAssetAuthoringApprovalsUx3F();
        return true;
    }

    private AssetCatalogItem? FindAssetUx3F(AssetCatalogItemId id) =>
        _catalogLoadUx3F?.Catalog?.Items.FirstOrDefault(item => item.Id == id);

    private AssetExternalEditConfiguration CreateExternalConfigurationUx3F(AssetCatalogItem asset)
    {
        var manifest = AssetManifests.FirstOrDefault(item => item.AssetId == asset.Id)
            ?? throw new ArgumentException("Cet asset ne possède pas de manifeste de paquet gouverné.", nameof(asset));
        var source = ResolveVaultPathUx3F(asset.RelativeModelPath!);
        using var stream = File.OpenRead(source);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return AssetExternalEditFactory.CreateBlenderRoundTrip(asset, manifest, hash, watchForChanges: false);
    }

    private string ResolveVaultPathUx3F(string relativePath) => Path.Combine(
        _catalogLoadUx3F!.VaultRoot,
        relativePath.Replace('/', Path.DirectorySeparatorChar));

    private bool AssetSourceExistsUx3F(AssetCatalogItem asset) =>
        !string.IsNullOrWhiteSpace(asset.RelativeModelPath) && File.Exists(ResolveVaultPathUx3F(asset.RelativeModelPath));

    private StudioUx3FActionResult MoveProcessingHistoryUx3F(bool undo)
    {
        if (!EnsureAssetAuthoringStateUx3F(out var error)) return new(false, error);
        var moved = undo ? _processingHistoryUx3F!.Undo() : _processingHistoryUx3F!.Redo();
        if (!moved) return new(false, undo ? "Aucun profil à annuler." : "Aucun profil à rétablir.");
        var saved = AssetProcessingProfileStore.Save(_assetWorkspaceRootUx3F!, _processingHistoryUx3F.State.Profiles);
        if (!saved.Succeeded)
        {
            if (undo) _processingHistoryUx3F.Redo(); else _processingHistoryUx3F.Undo();
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Historique du profil impossible.");
        }
        CancelProcessingApprovalUx3F();
        return new(true, undo ? "Profil d’import annulé." : "Profil d’import rétabli.");
    }

    private StudioUx3FActionResult MoveExternalHistoryUx3F(bool undo)
    {
        if (!EnsureAssetAuthoringStateUx3F(out var error)) return new(false, error);
        var sourceStack = undo ? _externalRequestUndoUx3F : _externalRequestRedoUx3F;
        var targetStack = undo ? _externalRequestRedoUx3F : _externalRequestUndoUx3F;
        if (sourceStack.Count == 0) return new(false, undo ? "Aucune édition externe à annuler." : "Aucune édition externe à rétablir.");
        var moved = undo ? _externalHistoryUx3F!.Undo() : _externalHistoryUx3F!.Redo();
        if (!moved) return new(false, "L’historique externe n’est plus aligné avec ses fichiers dérivés.");
        var snapshot = sourceStack.Pop();
        var saved = AssetExternalEditStore.Save(_assetWorkspaceRootUx3F!, _externalHistoryUx3F.State.Configurations);
        try
        {
            if (!saved.Succeeded) throw new IOException(saved.Diagnostics.FirstOrDefault()?.Message);
            RestoreOptionalUx3F(_assetWorkspaceRootUx3F!, snapshot.RelativePath, undo ? snapshot.Before : snapshot.After);
            targetStack.Push(snapshot);
            CancelExternalApprovalUx3F();
            return new(true, undo ? "Édition externe et requête dérivée annulées." : "Édition externe et requête dérivée rétablies.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (undo) _externalHistoryUx3F.Redo(); else _externalHistoryUx3F.Undo();
            sourceStack.Push(snapshot);
            return new(false, exception.Message);
        }
    }

    private bool ProcessingStoreMatchesUx3F()
    {
        var loaded = AssetProcessingProfileStore.Load(_assetWorkspaceRootUx3F!);
        return loaded.Succeeded && ProfilesFingerprintUx3F(loaded.Profiles) ==
            ProfilesFingerprintUx3F(_processingHistoryUx3F!.State.Profiles);
    }

    private bool ExternalStoreMatchesUx3F()
    {
        var loaded = AssetExternalEditStore.Load(_assetWorkspaceRootUx3F!);
        return loaded.Succeeded && ExternalConfigurationsFingerprintUx3F(loaded.Configurations) ==
            ExternalConfigurationsFingerprintUx3F(_externalHistoryUx3F!.State.Configurations);
    }

    private bool ValidateWorkerRequestUx3F(
        AssetExternalEditConfiguration configuration,
        ICollection<StudioUx3FDiagnostic> diagnostics)
    {
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-ux3-f-worker-{Guid.NewGuid():N}");
        try
        {
            var result = BlenderWorkerRequestStore.Save(root, configuration);
            foreach (var item in result.Diagnostics)
                diagnostics.Add(new(item.Code, item.Path, item.Message));
            return result.Succeeded && result.RequestRelativePath is not null &&
                   File.Exists(Path.Combine(root, result.RequestRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
        finally
        {
            DeleteTemporaryBlueprintUx3A(root);
        }
    }

    private bool ValidateWatchSourceUx3F(
        AssetExternalEditConfiguration configuration,
        ICollection<StudioUx3FDiagnostic> diagnostics)
    {
        var opened = AssetExternalEditWatchService.Open(_catalogLoadUx3F!.VaultRoot, configuration);
        foreach (var item in opened.Diagnostics) diagnostics.Add(new(item.Code, item.Path, item.Message));
        opened.Service?.Dispose();
        return opened.Succeeded;
    }

    private static bool ExerciseWatchPlanUx3F(
        StudioSessionController editing,
        AssetExternalEditConfiguration configuration)
    {
        var opened = AssetExternalEditWatchService.Open(editing._catalogLoadUx3F!.VaultRoot, configuration);
        if (!opened.Succeeded || opened.Service is null) return false;
        using var watcher = opened.Service;
        var path = Path.Combine(editing._catalogLoadUx3F.VaultRoot,
            configuration.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            stream.WriteByte(0);
        var plan = watcher.Poll(dryRun: true);
        return plan is { DryRun: true } && plan.MetadataChanges.Any(item => item.Field == "contentSha256") &&
               plan.Operations.SequenceEqual(configuration.Operations);
    }

    private static StudioUx3FProcessingDraft DraftFromProfileUx3F(
        AssetProcessingProfile profile,
        AssetProcessingProfileRevision revision,
        string sourceFingerprint,
        bool approved) => new(
        revision,
        sourceFingerprint,
        profile.AssetId,
        profile.Kind,
        profile.SourceFormat,
        profile.CanonicalFormat,
        profile.LicenseSpdx,
        profile.ContentSha256,
        profile.Provenance,
        profile.MaximumOutputBytes,
        profile.Texture?.ColorSpace ?? TextureColorSpace.Srgb,
        profile.Texture?.Compression ?? TextureCompressionProfile.Universal,
        profile.Texture?.MaximumDimension ?? 4096,
        profile.Texture?.TexelDensity ?? 10.24m,
        profile.Texture?.Channels ?? new Dictionary<PbrTextureChannel, string>(),
        profile.Model?.ImportRig ?? false,
        profile.Model?.ImportAnimations ?? true,
        profile.Model?.GenerateCollisions ?? true,
        profile.Model?.Sockets ?? [],
        profile.Model?.LodRatios ?? [1m, 0.5m, 0.25m],
        approved);

    private static StudioUx3FExternalDraft DraftFromExternalUx3F(
        AssetExternalEditConfiguration configuration,
        AssetExternalEditRevision revision,
        string sourceFingerprint,
        bool approved) => new(
        revision,
        sourceFingerprint,
        configuration.Id,
        configuration.AssetId,
        configuration.ManifestId,
        configuration.ModificationProvenance,
        configuration.LicenseSpdx,
        configuration.SourceContentSha256,
        configuration.SourceRelativePath,
        configuration.OutputRelativePath,
        configuration.Tool,
        configuration.Operations,
        configuration.WatchForChanges,
        configuration.TargetPlatforms,
        approved);

    private static AssetProcessingProfile BuildProfileUx3F(StudioUx3FProcessingDraft draft) => new(
        draft.AssetId,
        AssetProcessingProfile.CurrentSchemaVersion,
        draft.Kind,
        draft.SourceFormat,
        draft.CanonicalFormat,
        draft.LicenseSpdx,
        draft.ContentSha256,
        draft.Provenance,
        draft.MaximumOutputBytes,
        texture: draft.Kind == AssetProcessingKind.Texture
            ? new TextureProcessingOptions(
                draft.TextureColorSpace,
                draft.TextureCompression,
                draft.TextureMaximumDimension,
                draft.TextureTexelDensity,
                draft.TextureChannels)
            : null,
        model: draft.Kind == AssetProcessingKind.Model
            ? new ModelProcessingOptions(
                draft.ModelImportRig,
                draft.ModelImportAnimations,
                draft.ModelGenerateCollisions,
                draft.ModelSockets,
                draft.ModelLodRatios)
            : null);

    private static AssetExternalEditConfiguration BuildExternalUx3F(StudioUx3FExternalDraft draft) => new(
        AssetExternalEditConfiguration.CurrentSchemaVersion,
        draft.Id,
        draft.AssetId,
        draft.ManifestId,
        draft.ModificationProvenance,
        draft.LicenseSpdx,
        draft.SourceContentSha256,
        draft.SourceRelativePath,
        draft.OutputRelativePath,
        draft.Tool,
        draft.Operations,
        draft.WatchForChanges,
        draft.TargetPlatforms);

    private static StudioUx3FProcessingDraft FreezeProcessingDraftUx3F(StudioUx3FProcessingDraft draft) => draft with
    {
        TextureChannels = draft.TextureChannels.OrderBy(item => item.Key).ToDictionary(item => item.Key, item => item.Value),
        ModelSockets = draft.ModelSockets.ToArray(),
        ModelLodRatios = draft.ModelLodRatios.ToArray(),
    };

    private static StudioUx3FExternalDraft FreezeExternalDraftUx3F(StudioUx3FExternalDraft draft) => draft with
    {
        Operations = draft.Operations.Distinct().Order().ToArray(),
        TargetPlatforms = draft.TargetPlatforms.Distinct().Order().ToArray(),
    };

    private string ProcessingStateFingerprintUx3F(AssetProcessingProfileState state) =>
        $"{state.Revision.Value}:{ProfilesFingerprintUx3F(state.Profiles)}";

    private string ExternalStateFingerprintUx3F(AssetExternalEditState state) =>
        $"{state.Revision.Value}:{ExternalConfigurationsFingerprintUx3F(state.Configurations)}";

    private static string ProfilesFingerprintUx3F(IReadOnlyList<AssetProcessingProfile> profiles) =>
        HashBinaryUx3F(writer =>
        {
            var ordered = profiles.OrderBy(item => item.AssetId.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.Kind).ToArray();
            writer.Write(ordered.Length);
            foreach (var item in ordered) WriteProfileUx3F(writer, item);
        });

    private static string ProfileFingerprintUx3F(AssetProcessingProfile profile) =>
        HashBinaryUx3F(writer => WriteProfileUx3F(writer, profile));

    private static string ExternalConfigurationsFingerprintUx3F(
        IReadOnlyList<AssetExternalEditConfiguration> configurations) =>
        HashBinaryUx3F(writer =>
        {
            var ordered = configurations.OrderBy(item => item.AssetId.ToString(), StringComparer.Ordinal).ToArray();
            writer.Write(ordered.Length);
            foreach (var item in ordered) WriteExternalUx3F(writer, item);
        });

    private static string ExternalFingerprintUx3F(AssetExternalEditConfiguration configuration) =>
        HashBinaryUx3F(writer => WriteExternalUx3F(writer, configuration));

    private static void WriteProfileUx3F(BinaryWriter writer, AssetProcessingProfile profile)
    {
        WriteStringUx3F(writer, profile.AssetId.ToString());
        writer.Write(profile.SchemaVersion);
        writer.Write((int)profile.Kind);
        WriteStringUx3F(writer, profile.SourceFormat);
        WriteStringUx3F(writer, profile.CanonicalFormat);
        WriteStringUx3F(writer, profile.LicenseSpdx);
        WriteStringUx3F(writer, profile.ContentSha256);
        WriteStringUx3F(writer, profile.Provenance);
        writer.Write(profile.MaximumOutputBytes);
        writer.Write(profile.Texture is not null);
        if (profile.Texture is not null)
        {
            writer.Write((int)profile.Texture.ColorSpace);
            writer.Write((int)profile.Texture.Compression);
            writer.Write(profile.Texture.MaximumDimension);
            writer.Write(profile.Texture.TexelDensity);
            WriteDictionaryUx3F(writer, profile.Texture.Channels);
        }
        writer.Write(profile.Model is not null);
        if (profile.Model is not null)
        {
            writer.Write(profile.Model.ImportRig);
            writer.Write(profile.Model.ImportAnimations);
            writer.Write(profile.Model.GenerateCollisions);
            WriteStringsUx3F(writer, profile.Model.Sockets);
            writer.Write(profile.Model.LodRatios.Count);
            foreach (var value in profile.Model.LodRatios) writer.Write(value);
        }
    }

    private static void WriteExternalUx3F(BinaryWriter writer, AssetExternalEditConfiguration configuration)
    {
        writer.Write(configuration.SchemaVersion);
        WriteStringUx3F(writer, configuration.Id.ToString());
        WriteStringUx3F(writer, configuration.AssetId.ToString());
        WriteStringUx3F(writer, configuration.ManifestId.ToString());
        WriteStringUx3F(writer, configuration.ModificationProvenance);
        WriteStringUx3F(writer, configuration.LicenseSpdx);
        WriteStringUx3F(writer, configuration.SourceContentSha256);
        WriteStringUx3F(writer, configuration.SourceRelativePath);
        WriteStringUx3F(writer, configuration.OutputRelativePath);
        writer.Write((int)configuration.Tool);
        writer.Write(configuration.Operations.Count);
        foreach (var item in configuration.Operations) writer.Write((int)item);
        writer.Write(configuration.WatchForChanges);
        writer.Write(configuration.TargetPlatforms.Count);
        foreach (var item in configuration.TargetPlatforms) writer.Write((int)item);
    }

    private static void WriteDictionaryUx3F(BinaryWriter writer, IReadOnlyDictionary<PbrTextureChannel, string> values)
    {
        writer.Write(values.Count);
        foreach (var item in values.OrderBy(item => item.Key))
        {
            writer.Write((int)item.Key);
            WriteStringUx3F(writer, item.Value);
        }
    }

    private static void WriteStringsUx3F(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) WriteStringUx3F(writer, value);
    }

    private static void WriteStringUx3F(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string HashBinaryUx3F(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) write(writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private void CancelProcessingApprovalUx3F()
    {
        _approvedProcessingUx3F = null;
        _approvedProcessingRevisionUx3F = null;
        _approvedProcessingSourceUx3F = null;
        _approvedProcessingDraftUx3F = null;
    }

    private void CancelExternalApprovalUx3F()
    {
        _approvedExternalUx3F = null;
        _approvedExternalRevisionUx3F = null;
        _approvedExternalSourceUx3F = null;
        _approvedExternalDraftUx3F = null;
    }

    private static StudioUx3FProcessingPreview FailedProcessingUx3F(string message, string code, string path) =>
        new(false, false, false, false, true, message, "Aucun patch.", "Aucun inverse.", [],
            [new(code, path, message)], []);

    private static StudioUx3FExternalPreview FailedExternalUx3F(string message, string code, string path) =>
        new(false, false, false, false, false, true, message, "Aucun patch.", "Aucun inverse.", [],
            [new(code, path, message)], []);

    private static StudioUx3FSmokeResult EmptyAssetAuthoringSmokeUx3F() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private static AssetProcessingProfile CreateNeighborProfileUx3F() => new(
        AssetCatalogItemId.ForExternalId("ux3-f-neighbor-profile"),
        AssetProcessingProfile.CurrentSchemaVersion,
        AssetProcessingKind.Model,
        "glb",
        "glb",
        "CC0-1.0",
        new string('a', 64),
        "studio:evidence-neighbor",
        4_194_304,
        model: new ModelProcessingOptions(false, false, false, [], [1m]));

    private static AssetExternalEditConfiguration CreateNeighborExternalUx3F()
    {
        var asset = AssetCatalogItemId.ForExternalId("ux3-f-neighbor-external");
        return new(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(asset),
            asset,
            AssetPackageManifestId.ForAsset(asset),
            "studio:evidence-neighbor",
            "CC0-1.0",
            new string('b', 64),
            "assets/evidence/neighbor.glb",
            "assets/imported/neighbor/model.glb",
            AssetExternalTool.Blender,
            [AssetExternalOperation.ValidateModel, AssetExternalOperation.ExportGlb],
            false,
            [AssetTargetPlatform.Windows]);
    }

    private static void CopyVaultFileUx3F(string sourceRoot, string evidenceRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var source = Path.Combine(sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source)) return;
        var target = Path.Combine(evidenceRoot, ".asset-vault", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
    }

    private static string WorkerRequestRelativePathUx3F(AssetCatalogItemId assetId) =>
        $"assets/roundtrip/{assetId}.worker-request.json";

    private static string? ReadOptionalUx3F(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
    }

    private static void RestoreOptionalUx3F(string root, string relativePath, string? content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (content is null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static string HumanKindUx3F(ContentResourceKind kind) => kind switch
    {
        ContentResourceKind.Texture => "Texture",
        ContentResourceKind.Model => "Modèle 3D",
        ContentResourceKind.Localization => "Texte localisé",
        ContentResourceKind.Script => "Script",
        _ => "Ressource",
    };

    private static string HumanStatusUx3F(string status, bool local) => local
        ? "Source locale gouvernée"
        : status switch
        {
            "installed" => "Installé",
            "required" => "Requis par le module",
            "optional" => "Optionnel",
            "translated" => "Traduit",
            _ => "Catalogue seulement",
        };

    private static string HumanExternalOperationUx3F(AssetExternalOperation operation) => operation switch
    {
        AssetExternalOperation.ValidateModel => "Valider le modèle",
        AssetExternalOperation.NormalizeModel => "Normaliser le modèle",
        AssetExternalOperation.GenerateCollision => "Générer les collisions",
        AssetExternalOperation.GenerateLods => "Générer les LOD",
        AssetExternalOperation.ExportGlb => "Exporter en GLB",
        AssetExternalOperation.GenerateThumbnail => "Générer la vignette",
        AssetExternalOperation.AuditQuality => "Auditer la qualité",
        _ => operation.ToString(),
    };

    private static string HumanTaskUx3F(string task) => task switch
    {
        "texture.process" => "Convertir la texture",
        "model.normalize" => "Normaliser le modèle",
        "model.animations" => "Indexer les animations",
        "model.collisions" => "Générer les collisions",
        "model.sockets" => "Exporter les points d’attache",
        _ when task.StartsWith("model.lod.", StringComparison.Ordinal) => "Générer un niveau de détail",
        _ => task,
    };

    internal static string HumanPathUx3F(string path)
    {
        var name = Path.GetFileName(path.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "fichier gouverné" : ScrubHumanTextUx3F(name);
    }

    private static string ScrubHumanTextUx3F(string value) => Regex.Replace(
        value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé",
        RegexOptions.CultureInvariant);

    private static string HumanBytesUx3F(long value) => value >= 1_048_576
        ? $"{value / 1_048_576d:0.##} Mio"
        : $"{value / 1024d:0.##} Kio";

    private sealed record RequestSnapshotUx3F(string RelativePath, string? Before, string? After);
}
