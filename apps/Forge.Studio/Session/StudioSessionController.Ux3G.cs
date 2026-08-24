using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.EngineAdapter.Rendering;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetAssetPackageManifestHandler _setManifestUx3G = new(new SystemClock());
    private AssetPackageManifestCommandHistory? _manifestHistoryUx3G;
    private AssetCatalogLoadResult? _manifestCatalogUx3G;
    private string? _manifestWorkspaceRootUx3G;
    private StudioUx3GManifestDraft? _approvedManifestUx3G;
    private AssetPackageManifestRevision? _approvedManifestRevisionUx3G;
    private string? _approvedManifestSourceUx3G;
    private string? _approvedManifestDraftUx3G;
    private bool _manifestCanonicalCommandUx3G;
    private bool _lastUx3GTemporaryReleased = true;

    internal bool CanUndoManifestUx3G => _manifestHistoryUx3G?.CanUndo == true;
    internal bool CanRedoManifestUx3G => _manifestHistoryUx3G?.CanRedo == true;
    internal bool LastUx3GTemporaryReleased => _lastUx3GTemporaryReleased;

    internal IReadOnlyList<StudioUx3GManifestChoice> ListManifestChoicesUx3G()
    {
        if (!EnsureManifestStateUx3G(out _)) return [];
        var names = (_manifestCatalogUx3G?.Catalog?.Items ?? [])
            .ToDictionary(item => item.Id, item => item);
        var ordinal = 0;
        return _manifestHistoryUx3G!.State.Manifests.Select(manifest =>
        {
            ordinal++;
            var asset = names.GetValueOrDefault(manifest.AssetId);
            var displayName = asset is null
                ? $"Asset gouverné {ordinal:000}"
                : ScrubHumanTextUx3G(asset.DisplayName);
            var source = asset is null
                ? $"{manifest.LicenseSpdx} · {manifest.Chunks.Count} fichier(s)"
                : $"{ScrubHumanTextUx3G(asset.Source)} · {manifest.LicenseSpdx} · {manifest.Chunks.Count} fichier(s)";
            var capabilities = string.Join(" · ", new[]
            {
                manifest.Collision is null ? null : "collision",
                manifest.Navigation is null ? null : "navigation",
                manifest.Animation is null ? null : "animations",
                manifest.Vfx is null ? null : "VFX",
                manifest.Lod is null ? null : "LOD",
            }.Where(item => item is not null));
            return new StudioUx3GManifestChoice(
                manifest.AssetId,
                $"{displayName} · {(string.IsNullOrWhiteSpace(capabilities) ? "manifeste de base" : capabilities)}",
                source,
                manifest,
                manifest.Collision is not null,
                manifest.Navigation is not null,
                manifest.Animation is not null,
                manifest.Vfx is not null,
                manifest.Lod is not null);
        }).ToArray();
    }

    internal StudioUx3GManifestDraft? CreateManifestDraftUx3G(AssetCatalogItemId assetId)
    {
        if (!EnsureManifestStateUx3G(out _)) return null;
        var manifest = _manifestHistoryUx3G!.State.Find(assetId);
        return manifest is null
            ? null
            : DraftFromManifestUx3G(
                manifest,
                _manifestHistoryUx3G.State.Revision,
                ManifestStateFingerprintUx3G(_manifestHistoryUx3G.State),
                approved: false);
    }

    internal StudioUx3GManifestPreview PreviewManifestUx3G(StudioUx3GManifestDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!EnsureManifestStateUx3G(out var error))
            return FailedManifestUx3G(error, "asset.manifest.workspace_unavailable", "assets/asset-manifests.json");

        var state = _manifestHistoryUx3G!.State;
        var persisted = AssetPackageManifestStore.Load(_manifestWorkspaceRootUx3G!);
        var currentFingerprint = ManifestStateFingerprintUx3G(state);
        if (!persisted.Succeeded || draft.BaseRevision != state.Revision ||
            draft.SourceFingerprint != currentFingerprint ||
            ManifestsFingerprintUx3G(persisted.Manifests) != ManifestsFingerprintUx3G(state.Manifests))
        {
            CancelManifestApprovalUx3G();
            return FailedManifestUx3G(
                "Les manifests d’assets ont changé ; rechargez la sélection avant l’aperçu sec.",
                SetAssetPackageManifestHandler.RevisionConflictCode,
                "assets/asset-manifests.json");
        }

        var manifest = BuildManifestUx3G(draft);
        var command = new SetAssetPackageManifestCommand(
            CommandId.From(Guid.NewGuid()), state.Revision, manifest, DryRun: true);
        var result = _setManifestUx3G.Handle(state, command);
        var diagnostics = AssetPackageManifestValidator.Validate(manifest)
            .Select(item => new StudioUx3GDiagnostic(item.Code, item.Path, item.Message))
            .ToList();

        var available = ReadAvailableChunkHashesUx3G(_manifestWorkspaceRootUx3G!, manifest);
        var resolutionReady = true;
        foreach (var platform in manifest.PlatformBudgets.Keys.OrderBy(item => item))
        {
            var resolution = AssetPackageManifestResolver.Resolve(manifest, platform, available);
            resolutionReady &= resolution.Succeeded;
            diagnostics.AddRange(resolution.Diagnostics.Select(item =>
                new StudioUx3GDiagnostic(item.Code, item.Path, item.Message)));
        }

        var vfxPlans = BuildVfxPlansUx3G(manifest.Vfx);
        var vfxReady = vfxPlans.All(item => item.Valid || item.Culled);
        if (!vfxReady)
        {
            foreach (var plan in vfxPlans.Where(item => !item.Valid && !item.Culled))
                diagnostics.Add(new(plan.DiagnosticCode, $"assets/manifests/{manifest.Id}/vfx", HumanVfxCodeUx3G(plan.DiagnosticCode)));
        }

        diagnostics = diagnostics.DistinctBy(item => (item.Code, item.Path)).ToList();
        var dryRun = result.Succeeded && result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var succeeded = dryRun && resolutionReady && vfxReady && diagnostics.Count == 0;
        var frozen = FreezeManifestDraftUx3G(draft);
        if (succeeded && draft.Approved)
        {
            _approvedManifestUx3G = frozen;
            _approvedManifestRevisionUx3G = draft.BaseRevision;
            _approvedManifestSourceUx3G = draft.SourceFingerprint;
            _approvedManifestDraftUx3G = ManifestDraftFingerprintUx3G(frozen);
        }
        else CancelManifestApprovalUx3G();

        var previous = state.Find(manifest.AssetId);
        var diff = BuildManifestDiffUx3G(previous, manifest);
        var chunkPlan = manifest.Chunks.Select(chunk =>
            $"{HumanChunkRoleUx3G(chunk.Role)} · {HumanPathUx3G(chunk.RelativePath)} · " +
            $"{HumanBytesUx3G(chunk.StoredBytes)} · {HumanPlatformsUx3G(chunk.Platforms)}").ToArray();
        var animationPlan = manifest.Animation?.Clips.Select(clip =>
            $"{ScrubHumanTextUx3G(clip.Name)} · {clip.DurationSeconds:0.###} s · " +
            $"{(clip.RootMotion ? "mouvement racine" : "sur place")} · {clip.Events.Count} événement(s)").ToArray() ?? [];
        var vfxPlan = manifest.Vfx is null
            ? ["Aucun profil VFX authoré ; aucune exécution implicite."]
            : vfxPlans.Select(item =>
                $"{HumanVfxCodeUx3G(item.DiagnosticCode)} · provider {ScrubHumanTextUx3G(item.Provider)} · " +
                $"{item.ParticleCount} particules · {item.DrawCalls} appels").ToArray();
        return new(
            succeeded,
            succeeded && draft.Approved,
            dryRun,
            resolutionReady,
            vfxReady,
            true,
            succeeded
                ? draft.Approved
                    ? "Aperçu sec canonique prêt ; application autorisée."
                    : "Aperçu sec canonique prêt ; approbation humaine requise."
                : "Le manifeste doit être corrigé avant application.",
            previous is null
                ? "Ajouter le manifeste de paquet sélectionné."
                : "Remplacer le manifeste de paquet sélectionné.",
            previous is null
                ? "Retirer le manifeste ajouté."
                : "Restaurer le manifeste précédent.",
            diff,
            diagnostics,
            chunkPlan,
            animationPlan,
            vfxPlan);
    }

    internal StudioUx3GActionResult ApplyApprovedManifestUx3G(StudioUx3GManifestDraft current)
    {
        if (!EnsureManifestStateUx3G(out var error)) return new(false, error);
        var state = _manifestHistoryUx3G!.State;
        var frozen = FreezeManifestDraftUx3G(current);
        if (_approvedManifestUx3G is null || _approvedManifestRevisionUx3G != state.Revision ||
            _approvedManifestSourceUx3G != ManifestStateFingerprintUx3G(state) ||
            _approvedManifestDraftUx3G != ManifestDraftFingerprintUx3G(frozen) ||
            !current.Approved || !ManifestStoreMatchesUx3G())
            return new(false, "L’approbation du manifeste est absente ou périmée ; relancez l’aperçu sec.");

        var manifest = BuildManifestUx3G(current);
        var validation = AssetPackageManifestValidator.Validate(manifest);
        if (validation.Count > 0)
            return new(false, validation[0].Message);
        var dryRun = _setManifestUx3G.Handle(state,
            new SetAssetPackageManifestCommand(CommandId.From(Guid.NewGuid()), state.Revision, manifest, DryRun: true));
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Inverse is null || dryRun.Audit.Applied)
            return new(false, dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le dry-run canonique a refusé le manifeste.");
        var result = _setManifestUx3G.Handle(state,
            new SetAssetPackageManifestCommand(CommandId.From(Guid.NewGuid()), state.Revision, manifest, DryRun: false));
        if (!result.Succeeded || result.NewRevision is null || result.Patch is null || result.Inverse is null)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Application du manifeste refusée.");
        var saved = AssetPackageManifestStore.Save(_manifestWorkspaceRootUx3G!, result.State.Manifests);
        if (!saved.Succeeded)
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Écriture du manifeste impossible.");
        _manifestHistoryUx3G.Record(result);
        AssetManifests = _manifestHistoryUx3G.State.Manifests;
        _manifestCanonicalCommandUx3G = result.Audit.CommandName == "set_asset_package_manifest";
        CancelManifestApprovalUx3G();
        return new(true, "Manifeste appliqué et enregistré atomiquement.");
    }

    internal StudioUx3GActionResult UndoManifestUx3G() => MoveManifestHistoryUx3G(undo: true);
    internal StudioUx3GActionResult RedoManifestUx3G() => MoveManifestHistoryUx3G(undo: false);

    internal StudioUx3GActionResult SaveManifestUx3G()
    {
        if (!EnsureManifestStateUx3G(out var error)) return new(false, error);
        var saved = AssetPackageManifestStore.Save(_manifestWorkspaceRootUx3G!, _manifestHistoryUx3G!.State.Manifests);
        return saved.Succeeded
            ? new(true, "Manifests d’assets enregistrés.")
            : new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Enregistrement impossible.");
    }

    internal StudioUx3GApprovalSnapshot CaptureManifestApprovalUx3G() => new(
        _approvedManifestUx3G,
        _approvedManifestRevisionUx3G,
        _approvedManifestSourceUx3G,
        _approvedManifestDraftUx3G);

    internal void RestoreManifestApprovalUx3G(StudioUx3GApprovalSnapshot snapshot)
    {
        _approvedManifestUx3G = snapshot.Draft;
        _approvedManifestRevisionUx3G = snapshot.Revision;
        _approvedManifestSourceUx3G = snapshot.SourceFingerprint;
        _approvedManifestDraftUx3G = snapshot.DraftFingerprint;
    }

    internal void CancelManifestApprovalUx3G()
    {
        _approvedManifestUx3G = null;
        _approvedManifestRevisionUx3G = null;
        _approvedManifestSourceUx3G = null;
        _approvedManifestDraftUx3G = null;
    }

    internal void CloseUx3G()
    {
        CancelManifestApprovalUx3G();
        _manifestHistoryUx3G = null;
        _manifestCatalogUx3G = null;
        _manifestWorkspaceRootUx3G = null;
        _manifestCanonicalCommandUx3G = false;
    }

    internal StudioUx3GEvidencePlan? CreateManifestEvidencePlanUx3G()
    {
        if (!EnsureManifestStateUx3G(out _) || Project is null || Session is null) return null;
        var choices = ListManifestChoicesUx3G();
        var selected = choices.FirstOrDefault(item =>
            item.Manifest.Collision is not null && item.Manifest.Animation is not null && item.Manifest.Lod is not null)
            ?? choices.FirstOrDefault();
        if (selected is null) return CreateCanonicalEvidencePlanUx3G(Project, Session.RootDirectory);
        var neighbor = _manifestHistoryUx3G!.State.Manifests.FirstOrDefault(item => item.AssetId != selected.AssetId)
            ?? CreateNeighborManifestUx3G();
        var enriched = EnrichEvidenceManifestUx3G(selected.Manifest);
        return new(Project, Session.RootDirectory, enriched, neighbor, selected.Label,
            UsesRealModule: true, UsesCanonicalFallback: false);
    }

    internal static bool MaterializeManifestEvidenceUx3G(string root, StudioUx3GEvidencePlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            var repository = AssetCatalogLoader.FindRepositoryRoot(plan.SourceWorkspaceRoot);
            if (repository is not null)
            {
                var sourceCatalogs = Path.Combine(repository, "asset_manifests", "catalogs");
                if (Directory.Exists(sourceCatalogs))
                {
                    var targetCatalogs = Path.Combine(root, "asset_manifests", "catalogs");
                    Directory.CreateDirectory(targetCatalogs);
                    foreach (var file in Directory.EnumerateFiles(sourceCatalogs, "*.json")
                                 .OrderBy(item => item, StringComparer.Ordinal))
                        File.Copy(file, Path.Combine(targetCatalogs, Path.GetFileName(file)), overwrite: true);
                }
            }
            File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# Isolated UX3-G evidence\n", new UTF8Encoding(false));
            foreach (var chunk in plan.Manifest.Chunks)
            {
                var target = ResolveWorkspacePathUx3G(root, chunk.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (chunk.RelativePath.StartsWith("assets/evidence/ux3-g/", StringComparison.Ordinal))
                {
                    File.WriteAllBytes(target, EvidencePayloadUx3G(chunk.Role));
                    continue;
                }
                var source = ResolveWorkspacePathUx3G(plan.SourceWorkspaceRoot, chunk.RelativePath);
                if (File.Exists(source)) File.Copy(source, target, overwrite: true);
                else if (chunk.Required) return false;
            }
            if (!string.IsNullOrWhiteSpace(plan.Manifest.Animation?.RetargetMap))
            {
                var retarget = ResolveWorkspacePathUx3G(root, plan.Manifest.Animation.RetargetMap);
                Directory.CreateDirectory(Path.GetDirectoryName(retarget)!);
                if (!File.Exists(retarget))
                    File.WriteAllText(retarget, "# UX3-G isolated retarget map\n", new UTF8Encoding(false));
            }
            return AssetPackageManifestStore.Save(root, [plan.Manifest, plan.Neighbor]).Succeeded;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    internal StudioUx3GSmokeResult RunUx3GContractSmoke()
    {
        var evidence = CreateManifestEvidencePlanUx3G();
        if (evidence is null) return EmptyManifestSmokeUx3G();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-g-contract-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        _lastUx3GTemporaryReleased = false;
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !MaterializeManifestEvidenceUx3G(root, evidence) ||
                !editing.Open(root).Succeeded)
                return EmptyManifestSmokeUx3G();
            var sourceProject = ProjectJsonSerializer.Serialize(editing.Project!);
            var choices = editing.ListManifestChoicesUx3G();
            var choice = choices.FirstOrDefault(item => item.AssetId == evidence.Manifest.AssetId);
            var draft = editing.CreateManifestDraftUx3G(evidence.Manifest.AssetId);
            if (choice is null || draft is null) return EmptyManifestSmokeUx3G();
            var initial = ManifestsFingerprintUx3G(editing._manifestHistoryUx3G!.State.Manifests);
            var neighborBefore = ManifestFingerprintUx3G(
                editing._manifestHistoryUx3G.State.Manifests.Single(item => item.AssetId == evidence.Neighbor.AssetId));
            var hiddenBefore = HiddenTailsFingerprintUx3G(draft.ModelMetadata, draft.Font);

            var invalidGovernance = !editing.PreviewManifestUx3G(draft with
            {
                LicenseSpdx = string.Empty,
                Approved = true,
            }).Succeeded;
            draft = ChangedEvidenceDraftUx3G(editing.CreateManifestDraftUx3G(evidence.Manifest.AssetId)!);
            var preview = editing.PreviewManifestUx3G(draft);
            var staleSource = !editing.PreviewManifestUx3G(draft with
            {
                SourceFingerprint = draft.SourceFingerprint + "stale",
            }).Succeeded;
            draft = ChangedEvidenceDraftUx3G(editing.CreateManifestDraftUx3G(evidence.Manifest.AssetId)!);
            preview = editing.PreviewManifestUx3G(draft);
            var staleApproval = preview.CanApply &&
                                !editing.ApplyApprovedManifestUx3G(draft with
                                {
                                    Vfx = draft.Vfx! with { MaximumParticles = draft.Vfx.MaximumParticles + 1 },
                                }).Succeeded;
            preview = editing.PreviewManifestUx3G(draft);
            var applied = preview.CanApply && editing.ApplyApprovedManifestUx3G(draft).Succeeded;
            var final = ManifestsFingerprintUx3G(editing._manifestHistoryUx3G.State.Manifests);
            var undo = editing.UndoManifestUx3G().Succeeded;
            var undone = ManifestsFingerprintUx3G(editing._manifestHistoryUx3G.State.Manifests) == initial;
            var redo = editing.RedoManifestUx3G().Succeeded;
            var redone = ManifestsFingerprintUx3G(editing._manifestHistoryUx3G.State.Manifests) == final;
            var saved = editing.SaveManifestUx3G();
            var canonicalStore = ReadManifestStoreUx3G(root);
            var reopened = new StudioSessionController();
            var reopenedReady = reopened.Open(root).Succeeded;
            var roundTripRoot = Path.Combine(root, "round-trip");
            var resaved = reopenedReady
                ? AssetPackageManifestStore.Save(roundTripRoot, reopened.AssetManifests)
                : null;
            var persistence = saved.Succeeded && reopenedReady && resaved?.Succeeded == true &&
                              canonicalStore is not null && ReadManifestStoreUx3G(roundTripRoot) == canonicalStore &&
                              reopened.AssetManifests.Count == editing.AssetManifests.Count &&
                              reopened.AssetManifests.All(item => AssetPackageManifestValidator.Validate(item).Count == 0) &&
                              ProjectJsonSerializer.Serialize(reopened.Project!) == sourceProject;
            reopened.Close();
            var appliedManifest = editing._manifestHistoryUx3G.State.Find(evidence.Manifest.AssetId)!;
            var hiddenPreserved = HiddenTailsFingerprintUx3G(appliedManifest.ModelMetadata, appliedManifest.Font) == hiddenBefore;
            var neighborPreserved = ManifestFingerprintUx3G(
                editing._manifestHistoryUx3G.State.Manifests.Single(item => item.AssetId == evidence.Neighbor.AssetId)) == neighborBefore;
            var projectPreserved = ProjectJsonSerializer.Serialize(editing.Project!) == sourceProject;
            var human = !choice.Label.Contains(evidence.Manifest.AssetId.ToString(), StringComparison.OrdinalIgnoreCase) &&
                        !Regex.IsMatch(choice.Label,
                            "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                            RegexOptions.CultureInvariant);
            var collisionNavigation = draft.Collision is not null && draft.Navigation is not null &&
                                      draft.Navigation.ChunkId is { } navigationChunk &&
                                      draft.Chunks.Any(item => item.Id == navigationChunk && item.Role == AssetChunkRole.Navigation);
            var animation = draft.Animation is { Clips.Count: >= 2 } &&
                            draft.Animation.Clips.Any(item => item.Events.Count > 0);
            var vfxLod = draft.Vfx is not null && draft.Lod is { Levels.Count: > 0 };
            var canonical = editing._manifestCanonicalCommandUx3G;
            var coreSucceeded = invalidGovernance && preview.DryRunSucceeded && preview.PlatformResolutionReady &&
                                preview.VfxPlanReady && staleSource && staleApproval && applied && canonical &&
                                undo && undone && redo && redone && persistence && hiddenPreserved && neighborPreserved &&
                                projectPreserved && human && collisionNavigation && animation && vfxLod &&
                                preview.RuntimeClaimsBounded;
            return new(
                coreSucceeded,
                true,
                choices.Count >= 2,
                collisionNavigation,
                animation,
                vfxLod,
                invalidGovernance,
                preview.PlatformResolutionReady,
                preview.VfxPlanReady,
                preview.DryRunSucceeded,
                staleSource,
                staleApproval,
                canonical,
                undo && redo && undone && redone,
                persistence,
                hiddenPreserved,
                neighborPreserved,
                projectPreserved,
                human,
                preview.RuntimeClaimsBounded,
                false);
        }
        finally
        {
            editing.Close();
            _lastUx3GTemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }
    }

    internal string ManifestDraftFingerprintUx3G(StudioUx3GManifestDraft draft) => HashBinaryUx3G(writer =>
    {
        writer.Write(draft.BaseRevision.Value);
        WriteStringUx3G(writer, draft.SourceFingerprint);
        WriteStringUx3G(writer, JsonSerializer.Serialize(BuildManifestUx3G(draft)));
        writer.Write(draft.Approved);
    });

    private bool EnsureManifestStateUx3G(out string error)
    {
        error = string.Empty;
        if (Session is null || Project is null)
        {
            error = "Aucun workspace Studio n’est ouvert.";
            return false;
        }
        if (_manifestWorkspaceRootUx3G == Session.RootDirectory && _manifestHistoryUx3G is not null) return true;
        var loaded = AssetPackageManifestStore.Load(Session.RootDirectory);
        if (!loaded.Succeeded)
        {
            error = loaded.Diagnostics.FirstOrDefault()?.Message ?? "Les manifests d’assets sont illisibles.";
            return false;
        }
        var repository = AssetCatalogLoader.FindRepositoryRoot(Session.RootDirectory);
        _manifestCatalogUx3G = repository is null
            ? null
            : AssetCatalogLoader.Load(repository,
                Directory.Exists(Path.Combine(repository, ".asset-vault"))
                    ? Path.Combine(repository, ".asset-vault")
                    : null);
        _manifestWorkspaceRootUx3G = Session.RootDirectory;
        _manifestHistoryUx3G = new AssetPackageManifestCommandHistory(loaded.Manifests);
        AssetManifests = _manifestHistoryUx3G.State.Manifests;
        CancelManifestApprovalUx3G();
        return true;
    }

    private StudioUx3GActionResult MoveManifestHistoryUx3G(bool undo)
    {
        if (!EnsureManifestStateUx3G(out var error)) return new(false, error);
        var moved = undo ? _manifestHistoryUx3G!.Undo() : _manifestHistoryUx3G!.Redo();
        if (!moved) return new(false, undo ? "Aucun manifeste à annuler." : "Aucun manifeste à rétablir.");
        var saved = AssetPackageManifestStore.Save(_manifestWorkspaceRootUx3G!, _manifestHistoryUx3G.State.Manifests);
        if (!saved.Succeeded)
        {
            if (undo) _manifestHistoryUx3G.Redo(); else _manifestHistoryUx3G.Undo();
            return new(false, saved.Diagnostics.FirstOrDefault()?.Message ?? "Historique du manifeste impossible.");
        }
        AssetManifests = _manifestHistoryUx3G.State.Manifests;
        CancelManifestApprovalUx3G();
        return new(true, undo ? "Modification du manifeste annulée." : "Modification du manifeste rétablie.");
    }

    private bool ManifestStoreMatchesUx3G()
    {
        var loaded = AssetPackageManifestStore.Load(_manifestWorkspaceRootUx3G!);
        return loaded.Succeeded && ManifestsFingerprintUx3G(loaded.Manifests) ==
            ManifestsFingerprintUx3G(_manifestHistoryUx3G!.State.Manifests);
    }

    private static StudioUx3GManifestDraft DraftFromManifestUx3G(
        AssetPackageManifest manifest,
        AssetPackageManifestRevision revision,
        string sourceFingerprint,
        bool approved) => new(
        revision,
        sourceFingerprint,
        manifest.SchemaVersion,
        manifest.Id,
        manifest.AssetId,
        manifest.ModificationProvenance,
        manifest.LicenseSpdx,
        manifest.SourceContentSha256,
        manifest.PlatformBudgets.ToDictionary(item => item.Key, item => item.Value),
        manifest.Chunks.ToArray(),
        manifest.Collision,
        manifest.Navigation,
        manifest.Animation,
        manifest.Vfx,
        manifest.Lod,
        manifest.ModelMetadata,
        manifest.Font,
        approved);

    private static StudioUx3GManifestDraft FreezeManifestDraftUx3G(StudioUx3GManifestDraft draft) => draft with
    {
        PlatformBudgets = draft.PlatformBudgets.ToDictionary(item => item.Key, item => item.Value),
        Chunks = draft.Chunks.ToArray(),
        Collision = draft.Collision is null ? null : draft.Collision with
        {
            MaterialTags = draft.Collision.MaterialTags.ToArray(),
            CookPlatforms = draft.Collision.CookPlatforms.ToArray(),
        },
        Navigation = draft.Navigation is null ? null : draft.Navigation with
        {
            MaterialTags = draft.Navigation.MaterialTags.ToArray(),
            CookPlatforms = draft.Navigation.CookPlatforms.ToArray(),
        },
        Animation = draft.Animation is null ? null : draft.Animation with
        {
            Clips = draft.Animation.Clips.Select(item => item with { Events = item.Events.ToArray() }).ToArray(),
        },
        Lod = draft.Lod is null ? null : draft.Lod with { Levels = draft.Lod.Levels.ToArray() },
    };

    private static AssetPackageManifest BuildManifestUx3G(StudioUx3GManifestDraft draft) => new(
        draft.SchemaVersion,
        draft.Id,
        draft.AssetId,
        draft.ModificationProvenance.Trim(),
        draft.LicenseSpdx.Trim(),
        draft.SourceContentSha256.Trim().ToLowerInvariant(),
        draft.PlatformBudgets.ToDictionary(item => item.Key, item => item.Value),
        draft.Chunks.ToArray(),
        draft.Collision,
        draft.Animation,
        draft.Vfx,
        draft.Lod,
        draft.ModelMetadata,
        draft.Navigation,
        draft.Font);

    private string ManifestStateFingerprintUx3G(AssetPackageManifestState state) =>
        $"{state.Revision.Value}:{ManifestsFingerprintUx3G(state.Manifests)}";

    private static string ManifestsFingerprintUx3G(IReadOnlyList<AssetPackageManifest> manifests) => HashBinaryUx3G(writer =>
    {
        var ordered = manifests.OrderBy(item => item.AssetId.ToString(), StringComparer.Ordinal).ToArray();
        writer.Write(ordered.Length);
        foreach (var manifest in ordered) WriteStringUx3G(writer, JsonSerializer.Serialize(manifest));
    });

    private static string ManifestFingerprintUx3G(AssetPackageManifest manifest) => HashBinaryUx3G(writer =>
        WriteStringUx3G(writer, JsonSerializer.Serialize(manifest)));

    private static string HiddenTailsFingerprintUx3G(AssetModelMetadata? model, AssetFontManifest? font) =>
        HashBinaryUx3G(writer =>
        {
            WriteStringUx3G(writer, JsonSerializer.Serialize(model));
            WriteStringUx3G(writer, JsonSerializer.Serialize(font));
        });

    private static IReadOnlyDictionary<string, string> ReadAvailableChunkHashesUx3G(
        string root,
        AssetPackageManifest manifest)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var chunk in manifest.Chunks)
        {
            var path = ResolveWorkspacePathUx3G(root, chunk.RelativePath);
            if (!File.Exists(path)) continue;
            using var stream = File.OpenRead(path);
            result[chunk.RelativePath] = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        return result;
    }

    private static IReadOnlyList<AssetVfxRenderPlan> BuildVfxPlansUx3G(AssetVfxManifest? vfx)
    {
        if (vfx is null) return [];
        var provider = new PreviewVfxProviderUx3G(vfx.Provider);
        return
        [
            AssetVfxRenderPlanBuilder.Build(new(
                vfx,
                Math.Max(0.1m, vfx.CullDistance / 2m),
                Math.Min(vfx.MaximumParticles, 512),
                Math.Min(vfx.MaximumDrawCalls, 4),
                IncludeDecal: true,
                IncludeLight: true,
                IncludeAudioCue: true), provider),
            AssetVfxRenderPlanBuilder.Build(new(
                vfx,
                vfx.CullDistance + 1m,
                0,
                0,
                IncludeDecal: false,
                IncludeLight: false,
                IncludeAudioCue: false), provider),
        ];
    }

    private static IReadOnlyList<string> BuildManifestDiffUx3G(
        AssetPackageManifest? previous,
        AssetPackageManifest candidate)
    {
        if (previous is null) return ["Ajouter un manifeste de paquet gouverné."];
        var diff = new List<string>();
        if (previous.ModificationProvenance != candidate.ModificationProvenance ||
            previous.LicenseSpdx != candidate.LicenseSpdx ||
            previous.SourceContentSha256 != candidate.SourceContentSha256)
            diff.Add("Mettre à jour la gouvernance et la provenance du paquet.");
        if (ManifestPartFingerprintUx3G(previous.PlatformBudgets, previous.Chunks) !=
            ManifestPartFingerprintUx3G(candidate.PlatformBudgets, candidate.Chunks))
            diff.Add($"Mettre à jour {candidate.Chunks.Count} fichier(s) adressé(s) par contenu et leurs budgets plateforme.");
        if (JsonSerializer.Serialize(previous.Collision) != JsonSerializer.Serialize(candidate.Collision) ||
            JsonSerializer.Serialize(previous.Navigation) != JsonSerializer.Serialize(candidate.Navigation))
            diff.Add("Mettre à jour collision et navigation sans générer de géométrie.");
        if (JsonSerializer.Serialize(previous.Animation) != JsonSerializer.Serialize(candidate.Animation))
            diff.Add($"Mettre à jour la bibliothèque de {candidate.Animation?.Clips.Count ?? 0} animation(s).");
        if (JsonSerializer.Serialize(previous.Vfx) != JsonSerializer.Serialize(candidate.Vfx) ||
            JsonSerializer.Serialize(previous.Lod) != JsonSerializer.Serialize(candidate.Lod))
            diff.Add("Mettre à jour le plan VFX et les niveaux de détail.");
        return diff.Count == 0 ? ["Aucune différence sémantique."] : diff;
    }

    private static string ManifestPartFingerprintUx3G(object first, object second) => HashBinaryUx3G(writer =>
    {
        WriteStringUx3G(writer, JsonSerializer.Serialize(first));
        WriteStringUx3G(writer, JsonSerializer.Serialize(second));
    });

    private static StudioUx3GManifestDraft ChangedEvidenceDraftUx3G(StudioUx3GManifestDraft draft)
    {
        var animation = draft.Animation!;
        var clips = animation.Clips
            .Where(item => !string.Equals(item.Name, "studio-impact", StringComparison.OrdinalIgnoreCase))
            .Append(new AssetAnimationClipManifest(
                "studio-impact",
                0.75m,
                RootMotion: false,
                AssetAnimationCompression.Balanced,
                ["impact", "audio.cue"])).ToArray();
        return draft with
        {
            ModificationProvenance = draft.ModificationProvenance + ":ux3-g",
            Collision = draft.Collision! with
            {
                Generation = AssetCollisionGeneration.Manual,
                MaterialTags = draft.Collision.MaterialTags.Append("studio-walkmesh")
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            },
            Navigation = draft.Navigation! with
            {
                Generation = AssetNavigationGeneration.Manual,
                MaterialTags = draft.Navigation.MaterialTags.Append("walkable")
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            },
            Animation = animation with { Rig = "humanoid-studio", Clips = clips },
            Vfx = draft.Vfx! with
            {
                MaximumParticles = Math.Max(1024, draft.Vfx.MaximumParticles),
                MaximumDrawCalls = Math.Max(6, draft.Vfx.MaximumDrawCalls),
                CullDistance = Math.Max(32m, draft.Vfx.CullDistance),
            },
            Lod = draft.Lod! with { CrossFadeSeconds = Math.Min(5m, draft.Lod.CrossFadeSeconds + 0.05m) },
            Approved = true,
        };
    }

    private static AssetPackageManifest EnrichEvidenceManifestUx3G(AssetPackageManifest source)
    {
        var platforms = source.PlatformBudgets.Keys.Where(item => item != AssetTargetPlatform.Any).ToArray();
        if (platforms.Length == 0) platforms = [AssetTargetPlatform.Windows];
        var chunks = source.Chunks.ToList();
        var navigation = chunks.FirstOrDefault(item => item.Role == AssetChunkRole.Navigation);
        if (navigation is null)
        {
            var payload = EvidencePayloadUx3G(AssetChunkRole.Navigation);
            navigation = new(
                StableGuidUx3G(source.AssetId, "navigation"),
                AssetChunkRole.Navigation,
                Required: true,
                "assets/evidence/ux3-g/navigation.bin",
                HashBytesUx3G(payload),
                payload.Length,
                payload.Length,
                AssetChunkCompression.None,
                platforms);
            chunks.Add(navigation);
        }
        var vfxChunk = chunks.FirstOrDefault(item => item.Role == AssetChunkRole.Vfx);
        if (vfxChunk is null)
        {
            var payload = EvidencePayloadUx3G(AssetChunkRole.Vfx);
            vfxChunk = new(
                StableGuidUx3G(source.AssetId, "vfx"),
                AssetChunkRole.Vfx,
                Required: true,
                "assets/evidence/ux3-g/vfx.bin",
                HashBytesUx3G(payload),
                payload.Length,
                payload.Length,
                AssetChunkCompression.None,
                platforms);
            chunks.Add(vfxChunk);
        }
        var extraBytes = navigation.StoredBytes + vfxChunk.StoredBytes;
        var budgets = source.PlatformBudgets.ToDictionary(item => item.Key, item => checked(item.Value + extraBytes + 1024));
        var collision = source.Collision ?? new(false, source.Chunks.FirstOrDefault()?.Id,
            AssetCollisionGeneration.Embedded, ["default"], platforms);
        var sourceAnimation = source.Animation ?? new(false, source.Chunks.FirstOrDefault()?.Id, "generic", null,
            [new("preview", 1m, false, AssetAnimationCompression.Balanced, [])]);
        var animation = sourceAnimation with
        {
            RetargetMap = string.IsNullOrWhiteSpace(sourceAnimation.RetargetMap)
                ? "assets/evidence/ux3-g/retarget.map"
                : sourceAnimation.RetargetMap,
            Clips = sourceAnimation.Clips.Any(item =>
                string.Equals(item.Name, "impact-témoin", StringComparison.OrdinalIgnoreCase))
                ? sourceAnimation.Clips
                : sourceAnimation.Clips.Append(new AssetAnimationClipManifest(
                    "impact-témoin", 0.6m, false, AssetAnimationCompression.Balanced,
                    ["impact", "audio.cue"])).ToArray(),
        };
        var lod = source.Lod ?? new(false, 0.15m,
            [new(0, source.Chunks.FirstOrDefault()?.Id, 1m, 100_000)]);
        return source with
        {
            PlatformBudgets = budgets,
            Chunks = chunks,
            Collision = collision,
            Navigation = new(true, navigation.Id, AssetNavigationGeneration.Manual, ["walkable"], platforms),
            Animation = animation,
            Vfx = new(true, vfxChunk.Id, "forge-vfx", 2048, 8, 50m),
            Lod = lod,
        };
    }

    private static StudioUx3GEvidencePlan CreateCanonicalEvidencePlanUx3G(
        Forge.Domain.Projects.ProjectDefinition project,
        string sourceRoot)
    {
        var asset = AssetCatalogItemId.From(StableGuidUx3G("ux3-g", "asset"));
        var modelPayload = EvidencePayloadUx3G(AssetChunkRole.Model);
        var model = new AssetManifestChunk(
            StableGuidUx3G(asset, "model"), AssetChunkRole.Model, true,
            "assets/evidence/ux3-g/model.bin", HashBytesUx3G(modelPayload), modelPayload.Length, modelPayload.Length,
            AssetChunkCompression.None, [AssetTargetPlatform.Windows]);
        var baseManifest = new AssetPackageManifest(
            1,
            AssetPackageManifestId.ForAsset(asset),
            asset,
            "studio:ux3-g:canonical-fallback",
            "CC0-1.0",
            HashBytesUx3G(modelPayload),
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Windows] = 4096 },
            [model],
            new(false, model.Id, AssetCollisionGeneration.Embedded, ["default"], [AssetTargetPlatform.Windows]),
            new(false, model.Id, "generic", null,
                [new("preview", 1m, false, AssetAnimationCompression.Balanced, [])]),
            null,
            new(false, 0.15m, [new(0, model.Id, 1m, 10_000)]));
        var enriched = EnrichEvidenceManifestUx3G(baseManifest);
        return new(project, sourceRoot, enriched, CreateNeighborManifestUx3G(),
            "Asset canonique temporaire", UsesRealModule: false, UsesCanonicalFallback: true);
    }

    private static AssetPackageManifest CreateNeighborManifestUx3G()
    {
        var asset = AssetCatalogItemId.From(StableGuidUx3G("ux3-g", "neighbor"));
        var payload = EvidencePayloadUx3G(AssetChunkRole.Model);
        var chunk = new AssetManifestChunk(
            StableGuidUx3G(asset, "model"), AssetChunkRole.Model, false,
            "assets/evidence/ux3-g/neighbor.bin", HashBytesUx3G(payload), payload.Length, payload.Length,
            AssetChunkCompression.None, [AssetTargetPlatform.Windows]);
        return new(
            1,
            AssetPackageManifestId.ForAsset(asset),
            asset,
            "studio:ux3-g:neighbor",
            "CC0-1.0",
            HashBytesUx3G(payload),
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Windows] = 4096 },
            [chunk],
            null,
            null,
            null,
            null);
    }

    private static StudioUx3GManifestPreview FailedManifestUx3G(string message, string code, string path) => new(
        false, false, false, false, false, true, message, "Aucun patch.", "Aucun inverse.", [],
        [new(code, path, message)], [], [], []);

    private static StudioUx3GSmokeResult EmptyManifestSmokeUx3G() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false);

    private static string HashBinaryUx3G(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) write(writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteStringUx3G(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ResolveWorkspacePathUx3G(string root, string relativePath) => Path.Combine(
        Path.GetFullPath(root), relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string? ReadManifestStoreUx3G(string root)
    {
        var path = ResolveWorkspacePathUx3G(root, AssetPackageManifestStore.RelativePath);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static byte[] EvidencePayloadUx3G(AssetChunkRole role) =>
        Encoding.UTF8.GetBytes($"openrpg-forge:ux3-g:{role.ToString().ToLowerInvariant()}:evidence-v1");

    private static string HashBytesUx3G(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static Guid StableGuidUx3G(AssetCatalogItemId assetId, string suffix) =>
        StableGuidUx3G(assetId.ToString(), suffix);

    private static Guid StableGuidUx3G(string source, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:ux3-g:{source}:{suffix}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string ScrubHumanTextUx3G(string value) => Regex.Replace(
        value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé",
        RegexOptions.CultureInvariant).Trim();

    internal static string HumanPathUx3G(string path)
    {
        var name = Path.GetFileName(path.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "fichier gouverné" : ScrubHumanTextUx3G(name);
    }

    internal static string HumanChunkRoleUx3G(AssetChunkRole role) => role switch
    {
        AssetChunkRole.Model => "Modèle",
        AssetChunkRole.Texture => "Texture",
        AssetChunkRole.Collision => "Collision",
        AssetChunkRole.Animation => "Animation",
        AssetChunkRole.Vfx => "VFX",
        AssetChunkRole.Lod => "LOD",
        AssetChunkRole.Navigation => "Navigation",
        AssetChunkRole.Font => "Police",
        _ => "Fichier",
    };

    internal static string HumanPlatformsUx3G(IReadOnlyList<AssetTargetPlatform> platforms) =>
        string.Join(", ", platforms.Select(item => item switch
        {
            AssetTargetPlatform.Any => "Toutes",
            AssetTargetPlatform.Windows => "Windows",
            AssetTargetPlatform.Linux => "Linux",
            AssetTargetPlatform.MacOS => "macOS",
            AssetTargetPlatform.Android => "Android",
            AssetTargetPlatform.IOS => "iOS",
            _ => "Plateforme",
        }));

    private static string HumanBytesUx3G(long value) => value >= 1_048_576
        ? $"{value / 1_048_576m:0.##} Mio"
        : value >= 1024 ? $"{value / 1024m:0.##} Kio" : $"{value} octets";

    private static string HumanVfxCodeUx3G(string code) => code switch
    {
        "asset.vfx.render_ready" => "Plan VFX compatible",
        "asset.vfx.culled" => "VFX masqué hors distance",
        "asset.vfx.provider_optional_absent" => "Provider optionnel absent",
        "asset.vfx.provider_required" => "Provider VFX requis",
        "asset.vfx.provider_mismatch" => "Provider VFX incompatible",
        "asset.vfx.budget_exceeded" => "Budget VFX dépassé",
        "asset.vfx.capability_missing" => "Capacité VFX absente",
        _ => ScrubHumanTextUx3G(code),
    };

    private sealed class PreviewVfxProviderUx3G(string provider) : IAssetVfxRenderProvider
    {
        public VfxProviderCapabilities Capabilities { get; } = new(provider, true, true, true, true);
    }
}
