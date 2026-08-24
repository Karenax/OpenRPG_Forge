using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Content;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux8NPath = "module/content/packages/lifecycle";
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx8N = new(new SystemClock());
    private StudioUx8NCuratedDraft? _approvedCuratedUx8N;
    private ProjectRevision? _approvedCuratedRevisionUx8N;
    private string? _approvedCuratedFingerprintUx8N;
    private StudioUx8NMigrationDraft? _approvedMigrationUx8N;
    private string? _approvedMigrationFingerprintUx8N;
    private StudioUx8NUninstallDraft? _approvedUninstallUx8N;
    private string? _approvedUninstallFingerprintUx8N;
    private StudioUx8NMigrationAction? _lastMigrationUx8N;
    private StudioUx8NUninstallAction? _lastUninstallUx8N;
    private string? _vaultOverrideUx8N;

    internal StudioUx8NCuratedDraft? CreateCuratedPackDraftUx8N(bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var settings = project.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay.CuratedPacks;
        return new(history.State.Revision, ProjectFingerprintUx8N(project), settings.Channel,
            settings.Compatibility, settings.AllowedLicenses.ToArray(), settings.RequireTrustedSignature,
            settings.AllowOfflineFallback, false);
    }

    internal IReadOnlyList<StudioUx8NResourceChoice> ResourceChoicesUx8N()
    {
        var project = Project;
        if (project is null) return [];
        var result = ContentResourceRegistry.Build(project.Module.Content.Packages);
        return result.Resources.Select(resource => new StudioUx8NResourceChoice(
                project.Module.Content.Packages.Packages.Single(package =>
                    package.PackageNamespace == resource.PackageNamespace).Id,
                resource.Id,
                project.Module.Content.Packages.Packages.Any(package => package.Assets.Any(asset =>
                    asset.Id == resource.Id)),
                HumanUx8N(resource.PackageNamespace),
                HumanUx8N(resource.LogicalPath),
                resource.Address))
            .OrderBy(item => item.PackageLabel, StringComparer.Ordinal)
            .ThenBy(item => item.ResourceLabel, StringComparer.Ordinal)
            .ToArray();
    }

    internal StudioUx8NResourceDraft? CreateResourceDraftUx8N(StudioUx8NResourceChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var catalog = CreatePackageDraftUx8G();
        if (catalog is null) return null;
        var package = catalog.Packages.SingleOrDefault(item => item.Id == choice.PackageId);
        if (package is null) return null;
        if (choice.IsAsset)
        {
            var asset = package.Assets.SingleOrDefault(item => item.Id == choice.ResourceId);
            return asset is null ? null : new(catalog, package.Id, asset.Id, true,
                asset.Aliases?.ToArray() ?? [], asset.References?.ToArray() ?? [], false);
        }
        var source = package.Sources.SingleOrDefault(item => item.Id == choice.ResourceId);
        return source is null ? null : new(catalog, package.Id, source.Id, false,
            source.Aliases?.ToArray() ?? [], source.References?.ToArray() ?? [], false);
    }

    internal StudioUx8NMigrationDraft? CreateMigrationDraftUx8N(Guid packageId)
    {
        var catalog = CreatePackageDraftUx8G();
        var package = catalog?.Packages.SingleOrDefault(item => item.Id == packageId);
        return catalog is null || package is null
            ? null
            : new(catalog, packageId, NextPatchVersionUx8N(package.Version), false);
    }

    internal StudioUx8NUninstallDraft? CreateUninstallDraftUx8N(Guid packageId)
    {
        var catalog = CreatePackageDraftUx8G();
        return catalog is null || catalog.Packages.All(item => item.Id != packageId)
            ? null
            : new(catalog, packageId, null, false, false);
    }

    internal StudioUx8NPreview PreviewCuratedPacksUx8N(StudioUx8NCuratedDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCuratedApprovalUx8N();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedUx8N("Ouvrez un module avant de configurer les packs curatés.",
                "studio.packages.workspace_missing", "module/runtimePolicies/extensions/postLaunch/gameplay/curatedPacks");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, ProjectFingerprintUx8N(project), StringComparison.Ordinal))
            return FailedUx8N("Le projet a changé ; rechargez la politique curatée.",
                SetContentWorkspaceHandler.RevisionConflictCode,
                "module/runtimePolicies/extensions/postLaunch/gameplay/curatedPacks");

        if (!TryBuildCuratedCandidateUx8N(project, draft, out var candidate, out var failure)) return failure!;
        var dryRun = _setRuntimeExtensionsUx8N.Handle(history.State, new SetRuntimeExtensionsCommand(
            CommandId.From(Guid.NewGuid()), history.State.Revision, candidate!, true));
        if (!dryRun.Succeeded) return FailedCommandUx8N(dryRun,
            "module/runtimePolicies/extensions/postLaunch/gameplay/curatedPacks");
        var source = project.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay.CuratedPacks;
        var target = candidate!.PostLaunch.Gameplay.CuratedPacks;
        var changed = !SameCuratedUx8N(source, target);
        var allowed = target.AllowedLicenses.First();
        var accepted = PostLaunchCapabilityResolver.ResolveNetworkContent(
            candidate.PostLaunch.Gameplay, candidate.PostLaunch.Network, true, allowed);
        var rejected = PostLaunchCapabilityResolver.ResolveNetworkContent(
            candidate.PostLaunch.Gameplay, candidate.PostLaunch.Network, false, "UNLICENSED");
        var canonical = dryRun.Patch is not null && dryRun.Inverse is not null && !dryRun.Audit.Applied;
        if (changed && canonical && draft.Approved)
        {
            _approvedCuratedUx8N = FreezeCuratedUx8N(draft);
            _approvedCuratedRevisionUx8N = history.State.Revision;
            _approvedCuratedFingerprintUx8N = CandidateFingerprintUx8N(project, candidate);
        }
        return new(true, changed, canonical, changed && canonical && draft.Approved,
            changed
                ? draft.Approved ? "Politique curatée approuvée pour cette révision." : "Cochez l’approbation puis relancez l’aperçu."
                : "La politique curatée est déjà identique.",
            HumanUx8N(dryRun.Patch?.Summary),
            [
                $"Canal · {HumanUx8N(target.Channel)} · compatibilité {HumanUx8N(target.Compatibility)}",
                $"Licences autorisées · {string.Join(", ", target.AllowedLicenses)}",
                $"Signature de confiance · {(target.RequireTrustedSignature ? "requise" : "optionnelle")}",
                $"Repli hors ligne · {(target.AllowOfflineFallback ? "autorisé" : "refusé")}",
                $"Décision autorisée · {HumanUx8N(accepted.Diagnostic)}",
                $"Décision hostile · {HumanUx8N(rejected.Diagnostic)}",
                "Aucun pack distant n’est téléchargé, signé, publié ou installé par cet aperçu.",
            ], []);
    }

    internal StudioUx8NActionResult ApplyCuratedPacksUx8N(StudioUx8NCuratedDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var project = Project;
        var history = History;
        var approved = _approvedCuratedUx8N;
        if (project is null || history is null || approved is null || _approvedCuratedRevisionUx8N is null ||
            _approvedCuratedFingerprintUx8N is null || history.State.Revision != _approvedCuratedRevisionUx8N ||
            current.BaseRevision != history.State.Revision ||
            !string.Equals(current.SourceFingerprint, ProjectFingerprintUx8N(project), StringComparison.Ordinal) ||
            !TryBuildCuratedCandidateUx8N(project, current, out var candidate, out _) ||
            !string.Equals(_approvedCuratedFingerprintUx8N, CandidateFingerprintUx8N(project, candidate!),
                StringComparison.Ordinal))
        {
            CancelCuratedApprovalUx8N();
            return new(false, "Le projet ou la politique a changé ; relancez l’aperçu sec.");
        }
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, true);
        var dryRun = _setRuntimeExtensionsUx8N.Handle(history.State, command);
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Inverse is null || dryRun.Audit.Applied)
        {
            CancelCuratedApprovalUx8N();
            return new(false, HumanUx8N(dryRun.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = _setRuntimeExtensionsUx8N.Handle(history.State, command with { DryRun = false });
        CancelCuratedApprovalUx8N();
        if (!applied.Succeeded) return new(false, HumanUx8N(applied.Diagnostics.FirstOrDefault()?.Message));
        history.Record(applied);
        return new(true, "Politique des packs curatés appliquée par la commande canonique.");
    }

    internal StudioUx8NPreview PreviewResourceNamespaceUx8N(StudioUx8NResourceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!TryBuildResourceCatalogUx8N(draft, out var packageDraft, out var resourceId, out var failure))
            return failure!;
        var preview = PreviewPackagesUx8G(packageDraft!);
        if (!preview.Succeeded) return FromPackagePreviewUx8N(preview);
        var registry = ContentResourceRegistry.Build(new ContentPackageCatalog(
            packageDraft!.CatalogId, packageDraft.CatalogSchemaVersion, packageDraft.CatalogProvenance,
            packageDraft.ConflictPolicy, packageDraft.Packages));
        var resource = registry.Resources.Single(item => item.Id == resourceId);
        var lines = new List<string>
        {
            $"Adresse canonique · {resource.Address}",
            $"Identité interne stable · {(resource.Id == draft.ResourceId ? "préservée" : "invalide")}",
            $"Alias · {(resource.Aliases.Count == 0 ? "aucun" : string.Join(", ", resource.Aliases))}",
            $"Références · {(resource.References.Count == 0 ? "aucune" : string.Join(", ", resource.References))}",
            $"Licence · {resource.LicenseSpdx} · provenance {HumanUx8N(resource.Provenance)}",
            $"Hash et budget · {resource.ContentSha256[..8]}… · {resource.PlatformByteBudget} octets",
        };
        return new(true, preview.Changed, preview.CanonicalDryRun,
            preview.CanApply, preview.Message, preview.Patch, lines, []);
    }

    internal StudioUx8NActionResult ApplyResourceNamespaceUx8N(StudioUx8NResourceDraft current)
    {
        if (!TryBuildResourceCatalogUx8N(current, out var draft, out _, out var failure))
            return new(false, failure?.Message ?? "Le brouillon de ressource est invalide.");
        var result = ApplyApprovedPackagesUx8G(draft!);
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8NPreview PreviewMigrationUx8N(StudioUx8NMigrationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelMigrationApprovalUx8N();
        if (!TryBuildMigrationUx8N(draft, dryRun: true, out var packageDraft, out var installation,
                out var step, out var execution, out var failure)) return failure!;
        var preview = PreviewPackagesUx8G(packageDraft!);
        if (!preview.Succeeded || execution is null || !execution.Succeeded || !execution.DryRun)
            return preview.Succeeded ? FailedUx8N("Le plan hôte de migration a été refusé.",
                execution?.Diagnostics.FirstOrDefault()?.Code ?? AssetPackageMigrationStore.PlanInvalidCode,
                Ux8NPath) : FromPackagePreviewUx8N(preview);
        var fingerprint = MigrationFingerprintUx8N(draft, packageDraft!, step!);
        if (preview.CanApply && draft.Approved)
        {
            _approvedMigrationUx8N = FreezeMigrationUx8N(draft);
            _approvedMigrationFingerprintUx8N = fingerprint;
        }
        return new(true, preview.Changed, preview.CanonicalDryRun, preview.CanApply && draft.Approved,
            preview.Changed
                ? draft.Approved ? "Migration de compatibilité approuvée avec backup et changelog." : "Cochez l’approbation puis relancez l’aperçu."
                : "La version cible est déjà appliquée.",
            preview.Patch,
            [
                $"Version · {installation!.Version} → {step!.ToVersion}",
                $"Étape hôte compilée · {HumanUx8N(step.Changelog)}",
                $"Backup gouverné · {execution.Installation.InstalledFiles.Count} entrée(s)",
                "Transformation · compatibilité métadonnées uniquement; aucun fichier de contenu n’est réécrit.",
                "Dry-run · aucun backup, changelog ou fichier créé.",
            ], []);
    }

    internal StudioUx8NActionResult ApplyMigrationUx8N(StudioUx8NMigrationDraft current)
    {
        var approved = _approvedMigrationUx8N;
        StudioUx8NPreview? failure = null;
        if (approved is null || _approvedMigrationFingerprintUx8N is null ||
            !TryBuildMigrationUx8N(current, dryRun: true, out var packageDraft, out var installation,
                out var step, out _, out failure) ||
            !string.Equals(_approvedMigrationFingerprintUx8N,
                MigrationFingerprintUx8N(current, packageDraft!, step!), StringComparison.Ordinal))
        {
            CancelMigrationApprovalUx8N();
            return new(false, failure?.Message ?? "La migration a changé ; relancez l’aperçu sec.");
        }
        var root = Session!.RootDirectory;
        var plan = AssetPackageMigrationPlanner.Plan(installation!, step!.ToVersion, [step], false);
        var execution = AssetPackageMigrationStore.Execute(root, installation!, plan,
            new StudioMetadataMigrationHostStepUx8N(step.Id));
        if (!execution.Succeeded)
        {
            CancelMigrationApprovalUx8N();
            return new(false, HumanUx8N(execution.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = ApplyApprovedPackagesUx8G(packageDraft!);
        if (!applied.Succeeded)
        {
            if (execution.BackupRelativePath is not null)
                AssetPackageMigrationStore.RestoreBackup(root, execution.BackupRelativePath, installation!.InstalledFiles);
            CancelMigrationApprovalUx8N();
            return new(false, applied.Message);
        }
        _lastMigrationUx8N = new(current, installation!, step, execution, Undone: false);
        CancelMigrationApprovalUx8N(cancelPackage: false);
        return new(true, "Migration de compatibilité appliquée; backup et changelog sont persistés.");
    }

    internal StudioUx8NPreview PreviewUninstallUx8N(StudioUx8NUninstallDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUninstallApprovalUx8N();
        if (!TryBuildUninstallUx8N(draft, out var packageDraft, out var impact,
                out var package, out var replacement, out var vaultPlan, out var failure)) return failure!;
        var preview = PreviewPackagesUx8G(packageDraft!);
        if (!preview.Succeeded) return FromPackagePreviewUx8N(preview);
        var resolvedImpact = impact!;
        var resolvedPackage = package!;
        var vaultReady = !draft.RemoveFromAssetVault || vaultPlan is { CanApply: true };
        if (!vaultReady)
            return FailedUx8N("Le Vault refuse le retrait physique.",
                vaultPlan?.Diagnostics.FirstOrDefault()?.Code ?? AssetVaultPackageStore.PackageMissingCode,
                $"assets/packages/{package!.PackageNamespace}");
        var fingerprint = UninstallFingerprintUx8N(draft, packageDraft!);
        if (preview.CanApply && resolvedImpact.CanUninstall && draft.Approved)
        {
            _approvedUninstallUx8N = FreezeUninstallUx8N(draft);
            _approvedUninstallFingerprintUx8N = fingerprint;
        }
        return new(true, preview.Changed, preview.CanonicalDryRun,
            preview.CanApply && resolvedImpact.CanUninstall && draft.Approved,
            draft.Approved ? "Désinstallation sûre approuvée pour cette révision." : "Cochez l’approbation puis relancez l’aperçu.",
            preview.Patch,
            [
                $"Package retiré · {HumanUx8N(resolvedPackage.PackageNamespace)} {resolvedPackage.Version}",
                replacement is null
                    ? "Remplacement · aucun; aucune dépendance ne doit subsister."
                    : $"Remplacement · {HumanUx8N(replacement.PackageNamespace)} {replacement.Version}",
                $"Dépendants bloquants · {resolvedImpact.BlockingDependents.Count}",
                $"Fichiers de backup logique · {resolvedImpact.BackupFiles.Count}",
                draft.RemoveFromAssetVault
                    ? $"Asset Vault · plan sec prêt; {vaultPlan!.ResultingState.Packages.Count} package(s) resteraient."
                    : "Asset Vault · laissé intact explicitement.",
                "Les fichiers du workspace ne sont jamais supprimés par la commande de catalogue.",
            ], []);
    }

    internal StudioUx8NActionResult ApplyUninstallUx8N(StudioUx8NUninstallDraft current)
    {
        var approved = _approvedUninstallUx8N;
        StudioUx8NPreview? failure = null;
        if (approved is null || _approvedUninstallFingerprintUx8N is null ||
            !TryBuildUninstallUx8N(current, out var packageDraft, out _, out var package,
                out _, out var vaultPlan, out failure) ||
            !string.Equals(_approvedUninstallFingerprintUx8N,
                UninstallFingerprintUx8N(current, packageDraft!), StringComparison.Ordinal))
        {
            CancelUninstallApprovalUx8N();
            return new(false, failure?.Message ?? "Le plan de désinstallation a changé ; relancez l’aperçu sec.");
        }
        AssetVaultPackageMutationResult? vaultResult = null;
        AssetVaultPackageStore? vaultStore = null;
        if (approved.RemoveFromAssetVault)
        {
            if (!TryGetVaultStoreUx8N(out vaultStore, out var vaultError))
                return new(false, vaultError);
            vaultResult = vaultStore!.Apply(vaultStore.PlanUninstall(package!.PackageNamespace, false));
            if (!vaultResult.Succeeded)
                return new(false, HumanUx8N(vaultResult.Diagnostics.FirstOrDefault()?.Message));
        }
        var applied = ApplyApprovedPackagesUx8G(packageDraft!);
        if (!applied.Succeeded)
        {
            if (vaultResult is not null) _ = vaultStore!.RestoreUninstall(vaultResult);
            CancelUninstallApprovalUx8N();
            return new(false, applied.Message);
        }
        _lastUninstallUx8N = new(current, package!.PackageNamespace, vaultResult, Undone: false);
        CancelUninstallApprovalUx8N(cancelPackage: false);
        return new(true, vaultResult is null
            ? "Package retiré du catalogue; fichiers et Vault laissés intacts."
            : "Package retiré du catalogue et du Vault avec backup et reçu gouvernés.");
    }

    internal StudioUx8NActionResult UndoUx8N(string tab)
    {
        if (tab == "migration" && _lastMigrationUx8N is { Undone: false } migration)
        {
            var undo = UndoPackagesUx8G();
            if (!undo.Succeeded) return new(false, undo.Message);
            if (migration.Execution.BackupRelativePath is not null)
                AssetPackageMigrationStore.RestoreBackup(Session!.RootDirectory,
                    migration.Execution.BackupRelativePath, migration.Installation.InstalledFiles);
            _lastMigrationUx8N = migration with { Undone = true };
            return new(true, "Migration annulée; catalogue et backup restaurés.");
        }
        if (tab == "uninstall" && _lastUninstallUx8N is { Undone: false } uninstall)
        {
            if (uninstall.VaultResult is not null)
            {
                if (!TryGetVaultStoreUx8N(out var store, out var error)) return new(false, error);
                var restored = store!.RestoreUninstall(uninstall.VaultResult);
                if (!restored.Succeeded) return new(false, HumanUx8N(restored.Diagnostics.FirstOrDefault()?.Message));
            }
            var undo = UndoPackagesUx8G();
            if (!undo.Succeeded)
            {
                if (uninstall.VaultResult is not null && TryGetVaultStoreUx8N(out var store, out _))
                    _ = store!.Apply(store.PlanUninstall(uninstall.PackageNamespace, false));
                return new(false, undo.Message);
            }
            _lastUninstallUx8N = uninstall with { Undone = true };
            return new(true, "Désinstallation annulée; catalogue et Vault restaurés.");
        }
        if (tab == "curated")
        {
            var result = Undo();
            return new(result.Succeeded, result.Message);
        }
        var packageResult = UndoPackagesUx8G();
        return new(packageResult.Succeeded, packageResult.Message);
    }

    internal StudioUx8NActionResult RedoUx8N(string tab)
    {
        if (tab == "migration" && _lastMigrationUx8N is { Undone: true } migration)
        {
            var plan = AssetPackageMigrationPlanner.Plan(migration.Installation, migration.Step.ToVersion,
                [migration.Step], false);
            var execution = AssetPackageMigrationStore.Execute(Session!.RootDirectory, migration.Installation,
                plan, new StudioMetadataMigrationHostStepUx8N(migration.Step.Id));
            if (!execution.Succeeded) return new(false, HumanUx8N(execution.Diagnostics.FirstOrDefault()?.Message));
            var redo = RedoPackagesUx8G();
            if (!redo.Succeeded)
            {
                if (execution.BackupRelativePath is not null)
                    AssetPackageMigrationStore.RestoreBackup(Session.RootDirectory,
                        execution.BackupRelativePath, migration.Installation.InstalledFiles);
                return new(false, redo.Message);
            }
            _lastMigrationUx8N = migration with { Execution = execution, Undone = false };
            return new(true, "Migration rétablie avec son étape hôte approuvée.");
        }
        if (tab == "uninstall" && _lastUninstallUx8N is { Undone: true } uninstall)
        {
            AssetVaultPackageMutationResult? vaultResult = null;
            if (uninstall.VaultResult is not null)
            {
                if (!TryGetVaultStoreUx8N(out var store, out var error)) return new(false, error);
                vaultResult = store!.Apply(store.PlanUninstall(uninstall.PackageNamespace, false));
                if (!vaultResult.Succeeded) return new(false, HumanUx8N(vaultResult.Diagnostics.FirstOrDefault()?.Message));
            }
            var redo = RedoPackagesUx8G();
            if (!redo.Succeeded)
            {
                if (vaultResult is not null && TryGetVaultStoreUx8N(out var store, out _))
                    _ = store!.RestoreUninstall(vaultResult);
                return new(false, redo.Message);
            }
            _lastUninstallUx8N = uninstall with { VaultResult = vaultResult, Undone = false };
            return new(true, "Désinstallation rétablie avec collecte des seuls blobs orphelins.");
        }
        if (tab == "curated")
        {
            var result = Redo();
            return new(result.Succeeded, result.Message);
        }
        var packageResult = RedoPackagesUx8G();
        return new(packageResult.Succeeded, packageResult.Message);
    }

    internal StudioUx8NActionResult SaveUx8N()
    {
        CancelAllUx8NApprovals();
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx8NApprovalSnapshot CapturePackageLifecycleApprovalUx8N() => new(
        _approvedCuratedUx8N, _approvedCuratedRevisionUx8N, _approvedCuratedFingerprintUx8N,
        _approvedMigrationUx8N, _approvedMigrationFingerprintUx8N,
        _approvedUninstallUx8N, _approvedUninstallFingerprintUx8N,
        CapturePackageApprovalUx8G());

    internal void RestorePackageLifecycleApprovalUx8N(StudioUx8NApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCuratedUx8N = snapshot.CuratedDraft;
        _approvedCuratedRevisionUx8N = snapshot.CuratedRevision;
        _approvedCuratedFingerprintUx8N = snapshot.CuratedFingerprint;
        _approvedMigrationUx8N = snapshot.MigrationDraft;
        _approvedMigrationFingerprintUx8N = snapshot.MigrationFingerprint;
        _approvedUninstallUx8N = snapshot.UninstallDraft;
        _approvedUninstallFingerprintUx8N = snapshot.UninstallFingerprint;
        RestorePackageApprovalUx8G(snapshot.PackageApproval);
    }

    internal void SetAssetVaultOverrideUx8N(string? root) =>
        _vaultOverrideUx8N = string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);

    internal bool PrepareUx8NEvidenceFixture(string workspaceRoot, string vaultRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        SetAssetVaultOverrideUx8N(vaultRoot);
        var source = CreatePackageDraftUx8G();
        if (source is null) return false;
        var fixture = CreateUx8NPackageFixture(source) with { Approved = true };
        var preview = PreviewPackagesUx8G(fixture);
        if (!preview.CanApply || !ApplyApprovedPackagesUx8G(fixture).Succeeded || !Save().Succeeded) return false;
        var core = Project!.Module.Content.Packages.Packages.Single(item =>
            item.PackageNamespace == "studio.ux8g.core");
        PrepareMigrationMarkerUx8N(workspaceRoot, core.Id);
        PrepareVaultFixtureUx8N(vaultRoot, core.PackageNamespace);
        return true;
    }

    internal StudioUx8NSmokeResult RunUx8NContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceProject is null || sourceHistory is null) return FailedSmokeUx8N("Aucun projet source.");
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CapturePackageLifecycleApprovalUx8N();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-n-{Guid.NewGuid():N}");
        var vault = Path.Combine(root, "vault");
        var editing = new StudioSessionController();
        StudioSessionController? reopened = null;
        var fourRoutes = false;
        var curated = false;
        var namespaces = false;
        var migration = false;
        var uninstall = false;
        var dryRuns = false;
        var commands = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var neighbors = false;
        var released = false;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded || !editing.Open(root).Succeeded)
                throw new InvalidOperationException("Le workspace de preuve UX8-N est indisponible.");
            editing.SetAssetVaultOverrideUx8N(vault);
            var baseDraft = editing.CreatePackageDraftUx8G() ??
                            throw new InvalidOperationException("Le catalogue de preuve est indisponible.");
            var fixture = CreateUx8NPackageFixture(baseDraft);
            var fixturePreview = editing.PreviewPackagesUx8G(fixture with { Approved = true });
            if (!fixturePreview.CanApply || !editing.ApplyApprovedPackagesUx8G(fixture with { Approved = true }).Succeeded)
                throw new InvalidOperationException("Le catalogue de preuve UX8-N a été refusé.");
            if (!editing.Save().Succeeded) throw new InvalidOperationException("Le catalogue de preuve n’a pas été sauvegardé.");
            PrepareMigrationMarkerUx8N(root, fixture.Packages.First(item =>
                item.PackageNamespace == "studio.ux8g.core").Id);
            PrepareVaultFixtureUx8N(vault, "studio.ux8g.core");
            var baseline = editing.Project!;

            var curatedBase = editing.CreateCuratedPackDraftUx8N(true)!;
            var invalidCurated = editing.PreviewCuratedPacksUx8N(curatedBase with
            {
                AllowedLicenses = [],
                Approved = true,
            });
            staleSource = !editing.PreviewCuratedPacksUx8N(curatedBase with
            {
                SourceFingerprint = "source-périmée",
                Approved = true,
            }).Succeeded;
            var curatedDraft = curatedBase with
            {
                Channel = curatedBase.Channel == "stable" ? "preview" : "stable",
                Compatibility = "1.x",
                AllowedLicenses = ["CC0-1.0", "MIT"],
                RequireTrustedSignature = true,
                AllowOfflineFallback = true,
                Approved = true,
            };
            var curatedPreview = editing.PreviewCuratedPacksUx8N(curatedDraft);
            var curatedTamper = editing.ApplyCuratedPacksUx8N(curatedDraft with
            {
                Compatibility = "2.x",
            });
            staleApproval = curatedPreview.CanApply && !curatedTamper.Succeeded;
            curatedPreview = editing.PreviewCuratedPacksUx8N(curatedDraft);
            var curatedApply = editing.ApplyCuratedPacksUx8N(curatedDraft);
            var curatedUndo = editing.UndoUx8N("curated");
            var curatedRedo = editing.RedoUx8N("curated");
            curated = !invalidCurated.Succeeded && curatedPreview.CanApply && curatedApply.Succeeded &&
                      editing.Project!.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay.CuratedPacks
                          .AllowedLicenses.SequenceEqual(["CC0-1.0", "MIT"]);

            var choices = editing.ResourceChoicesUx8N();
            var resourceChoice = choices.Single(item => item.PackageLabel.Contains("studio.ux8g.core",
                StringComparison.Ordinal));
            var resourceBase = editing.CreateResourceDraftUx8N(resourceChoice)!;
            var collision = editing.PreviewResourceNamespaceUx8N(resourceBase with
            {
                Aliases = ["studio.ux8g.addon:text/ui.theme"],
                Approved = true,
            });
            var resourceDraft = resourceBase with
            {
                Aliases = ["studio.ux8g.core:text/ui/legacy-theme"],
                References = ["studio.ux8g.addon:text/ui.theme"],
                Approved = true,
            };
            var resourcePreview = editing.PreviewResourceNamespaceUx8N(resourceDraft);
            var resourceApply = editing.ApplyResourceNamespaceUx8N(resourceDraft);
            var registry = ContentResourceRegistry.Build(editing.Project!.Module.Content.Packages);
            namespaces = !collision.Succeeded && resourcePreview.CanApply && resourceApply.Succeeded &&
                         registry.Valid && registry.Resources.Any(item =>
                             item.Aliases.Contains("studio.ux8g.core:text/ui/legacy-theme", StringComparer.Ordinal) &&
                             item.References.Contains("studio.ux8g.addon:text/ui.theme", StringComparer.Ordinal));
            if (!namespaces)
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8_N_NAMESPACE_FAILED " +
                                  $"collision_succeeded={collision.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"preview_succeeded={resourcePreview.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"preview_apply={resourcePreview.CanApply.ToString().ToLowerInvariant()} " +
                                  $"apply={resourceApply.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"registry={registry.Valid.ToString().ToLowerInvariant()}");

            var core = editing.Project.Module.Content.Packages.Packages.Single(item =>
                item.PackageNamespace == "studio.ux8g.core");
            var migrationDraft = editing.CreateMigrationDraftUx8N(core.Id)! with
            {
                TargetVersion = "1.3.0",
                Approved = true,
            };
            var migrationPreview = editing.PreviewMigrationUx8N(migrationDraft);
            var migrationApply = editing.ApplyMigrationUx8N(migrationDraft);
            var migrationAction = editing._lastMigrationUx8N;
            var migrationUndo = editing.UndoUx8N("migration");
            var migrationRedo = editing.RedoUx8N("migration");
            migration = migrationPreview.CanApply && migrationApply.Succeeded && migrationAction is not null &&
                        migrationAction.Execution.BackupRelativePath is not null &&
                        migrationAction.Execution.ChangelogRelativePath is not null &&
                        Directory.Exists(Path.Combine(root, migrationAction.Execution.BackupRelativePath)) &&
                        File.Exists(Path.Combine(root, migrationAction.Execution.ChangelogRelativePath)) &&
                        editing.Project!.Module.Content.Packages.Packages.Single(item =>
                            item.PackageNamespace == "studio.ux8g.core").Version == "1.3.0";
            if (!migration)
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8_N_MIGRATION_FAILED " +
                                  $"preview={migrationPreview.CanApply.ToString().ToLowerInvariant()} " +
                                  $"preview_succeeded={migrationPreview.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"preview_code={migrationPreview.Diagnostics.FirstOrDefault()?.Code ?? "none"} " +
                                  $"apply={migrationApply.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"apply_message={HumanUx8N(migrationApply.Message).Replace(' ', '_')} " +
                                  $"action={(migrationAction is not null).ToString().ToLowerInvariant()} " +
                                  $"undo={migrationUndo.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"redo={migrationRedo.Succeeded.ToString().ToLowerInvariant()}");

            core = editing.Project.Module.Content.Packages.Packages.Single(item =>
                item.PackageNamespace == "studio.ux8g.core");
            var replacement = editing.Project.Module.Content.Packages.Packages.Single(item =>
                item.PackageNamespace == "studio.ux8n.replacement");
            var uninstallBase = editing.CreateUninstallDraftUx8N(core.Id)!;
            var blocked = editing.PreviewUninstallUx8N(uninstallBase with { Approved = true });
            var uninstallDraft = uninstallBase with
            {
                ReplacementPackageId = replacement.Id,
                RemoveFromAssetVault = true,
                Approved = true,
            };
            var uninstallPreview = editing.PreviewUninstallUx8N(uninstallDraft);
            var uninstallApply = editing.ApplyUninstallUx8N(uninstallDraft);
            var vaultStore = new AssetVaultPackageStore(vault);
            var removed = uninstallApply.Succeeded &&
                          editing.Project!.Module.Content.Packages.Packages.All(item => item.Id != core.Id) &&
                          vaultStore.LoadState().Packages.All(item => item.PackageId != "studio.ux8g.core");
            var uninstallUndo = editing.UndoUx8N("uninstall");
            var restored = uninstallUndo.Succeeded &&
                           editing.Project!.Module.Content.Packages.Packages.Any(item => item.Id == core.Id) &&
                           vaultStore.LoadState().Packages.Any(item => item.PackageId == "studio.ux8g.core");
            var uninstallRedo = editing.RedoUx8N("uninstall");
            var reremoved = uninstallRedo.Succeeded &&
                            editing.Project!.Module.Content.Packages.Packages.All(item => item.Id != core.Id) &&
                            vaultStore.LoadState().Packages.All(item => item.PackageId != "studio.ux8g.core");
            uninstall = !blocked.Succeeded && uninstallPreview.CanApply && removed && restored && reremoved;
            if (!uninstall)
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8_N_UNINSTALL_FAILED " +
                                  $"blocked_succeeded={blocked.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"preview={uninstallPreview.CanApply.ToString().ToLowerInvariant()} " +
                                  $"preview_succeeded={uninstallPreview.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"preview_code={uninstallPreview.Diagnostics.FirstOrDefault()?.Code ?? "none"} " +
                                  $"apply={uninstallApply.Succeeded.ToString().ToLowerInvariant()} " +
                                  $"apply_message={HumanUx8N(uninstallApply.Message).Replace(' ', '_')} " +
                                  $"removed={removed.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()} " +
                                  $"reremoved={reremoved.ToString().ToLowerInvariant()}");

            dryRuns = curatedPreview.CanonicalDryRun && resourcePreview.CanonicalDryRun &&
                      migrationPreview.CanonicalDryRun && uninstallPreview.CanonicalDryRun;
            commands = curatedApply.Succeeded && resourceApply.Succeeded && migrationApply.Succeeded &&
                       uninstallApply.Succeeded && editing.History!.Journal.Count(entry =>
                           entry.CommandName == "set_runtime_extensions") >= 1 &&
                       editing.History.Journal.Count(entry => entry.CommandName == "set_content_workspace") >= 4;
            undoRedo = curatedUndo.Succeeded && curatedRedo.Succeeded && migrationUndo.Succeeded &&
                       migrationRedo.Succeeded && uninstallUndo.Succeeded && uninstallRedo.Succeeded;
            fourRoutes = new[] { "ORF-CONT-010", "ORF-CONT-019", "ORF-CONT-026", "ORF-CONT-027" }
                .Distinct(StringComparer.Ordinal).Count() == 4;

            var finalProject = editing.Project!;
            var finalExtensions = finalProject.Module.RuntimePolicies.Extensions;
            var baselineExtensions = baseline.Module.RuntimePolicies.Extensions;
            neighbors = ReferenceEquals(baselineExtensions.PostLaunch.LiveDirector,
                            finalExtensions.PostLaunch.LiveDirector) &&
                        ReferenceEquals(baselineExtensions.PostLaunch.Network,
                            finalExtensions.PostLaunch.Network) &&
                        ReferenceEquals(baselineExtensions.PostLaunch.CreatorExperience,
                            finalExtensions.PostLaunch.CreatorExperience) &&
                        ReferenceEquals(baselineExtensions.PostLaunch.Production,
                            finalExtensions.PostLaunch.Production) &&
                        ReferenceEquals(baselineExtensions.ModernTooling, finalExtensions.ModernTooling) &&
                        ReferenceEquals(baseline.Module.Content.Batch, finalProject.Module.Content.Batch) &&
                        baseline.Module.Content.DataTables.SequenceEqual(finalProject.Module.Content.DataTables) &&
                        baseline.Areas.SequenceEqual(finalProject.Areas) &&
                        baseline.Blueprints.SequenceEqual(finalProject.Blueprints) &&
                        baseline.Dialogues.SequenceEqual(finalProject.Dialogues) &&
                        baseline.Quests.SequenceEqual(finalProject.Quests);
            if (!neighbors)
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8_N_NEIGHBORS_FAILED " +
                                  $"live={ReferenceEquals(baselineExtensions.PostLaunch.LiveDirector, finalExtensions.PostLaunch.LiveDirector).ToString().ToLowerInvariant()} " +
                                  $"network={ReferenceEquals(baselineExtensions.PostLaunch.Network, finalExtensions.PostLaunch.Network).ToString().ToLowerInvariant()} " +
                                  $"creator={ReferenceEquals(baselineExtensions.PostLaunch.CreatorExperience, finalExtensions.PostLaunch.CreatorExperience).ToString().ToLowerInvariant()} " +
                                  $"production={ReferenceEquals(baselineExtensions.PostLaunch.Production, finalExtensions.PostLaunch.Production).ToString().ToLowerInvariant()} " +
                                  $"modern={ReferenceEquals(baselineExtensions.ModernTooling, finalExtensions.ModernTooling).ToString().ToLowerInvariant()} " +
                                  $"batch={ReferenceEquals(baseline.Module.Content.Batch, finalProject.Module.Content.Batch).ToString().ToLowerInvariant()} " +
                                  $"tables={ReferenceEquals(baseline.Module.Content.DataTables, finalProject.Module.Content.DataTables).ToString().ToLowerInvariant()} " +
                                  $"areas={ReferenceEquals(baseline.Areas, finalProject.Areas).ToString().ToLowerInvariant()} " +
                                  $"blueprints={ReferenceEquals(baseline.Blueprints, finalProject.Blueprints).ToString().ToLowerInvariant()} " +
                                  $"dialogues={ReferenceEquals(baseline.Dialogues, finalProject.Dialogues).ToString().ToLowerInvariant()} " +
                                  $"quests={ReferenceEquals(baseline.Quests, finalProject.Quests).ToString().ToLowerInvariant()}");
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var saved = editing.SaveUx8N();
            reopened = new StudioSessionController();
            persistence = saved.Succeeded && reopened.Open(root).Succeeded && reopened.Project is not null &&
                          string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project),
                              StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX8_N_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
        }
        finally
        {
            reopened?.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                released = !Directory.Exists(root);
            }
            catch
            {
                released = false;
            }
            RestorePackageLifecycleApprovalUx8N(sourceApproval);
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              CapturePackageLifecycleApprovalUx8N() == sourceApproval;
        var succeeded = fourRoutes && curated && namespaces && migration && uninstall && dryRuns && commands &&
                        staleSource && staleApproval && undoRedo && persistence && neighbors && released &&
                        sourceUnchanged;
        return new(succeeded, fourRoutes, curated, namespaces, migration, uninstall, dryRuns, commands,
            staleSource, staleApproval, undoRedo, persistence, neighbors, released, sourceUnchanged,
            succeeded
                ? "UX8-N · gouvernance, namespaces et cycle de vie des packages prouvés sans mutation source."
                : "UX8-N · une preuve de package reste en échec.");
    }

    internal static StudioUx8GPackageDraft CreateUx8NPackageFixture(StudioUx8GPackageDraft source)
    {
        var fixture = CreatePackageFixtureUx8G(source);
        var replacement = RehashPackageUx8G(new ContentPackageDefinition(
            Guid.Parse("8f0e0000-0000-4000-8000-000000000003"), 1, "studio:evidence",
            "studio.ux8n.replacement", "1.5.0", "MIT", new string('0', 64), 5,
            [], [], [], []));
        return fixture with { Packages = fixture.Packages.Append(replacement).ToArray() };
    }

    private static void PrepareMigrationMarkerUx8N(string root, Guid packageId)
    {
        var path = Path.Combine(root, ".forge", "package-migrations", "inputs", $"{packageId:D}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "ux8-n compatibility input\n", new UTF8Encoding(false));
    }

    private static void PrepareVaultFixtureUx8N(string vault, string packageId)
    {
        var model = Path.Combine(vault, "normalized", "studio", "core", "model.glb");
        Directory.CreateDirectory(Path.GetDirectoryName(model)!);
        File.WriteAllText(model, "ux8-n-model", new UTF8Encoding(false));
        var item = new AssetCatalogItem(
            AssetCatalogItemId.ForExternalId("studio:ux8n-core"), "studio:ux8n-core", "Pack UX8-N",
            "studio", "ux8n-core", "https://example.invalid/ux8n-core", "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/", true, new string('a', 64), 12,
            ["3d-model"], ["evidence"], "downloaded-and-extracted", AssetAvailability.ExtractedAvailable,
            "normalized/studio/core/model.glb", null, null, "studio:ux8-n", author: "OpenRPG Forge");
        var catalog = new AssetCatalog("2026-08-22", "cc0-only", vault, [item], "studio:ux8-n");
        var store = new AssetVaultPackageStore(vault);
        var installed = store.Apply(store.PlanInstall(catalog, packageId, "1.2.0",
            [item.ExternalId], dryRun: false));
        if (!installed.Succeeded) throw new InvalidOperationException("Le Vault de preuve refuse l’installation.");
    }

    private static StudioUx8NSmokeResult FailedSmokeUx8N(string message) => new(false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, message);

    private bool TryBuildCuratedCandidateUx8N(ProjectDefinition project, StudioUx8NCuratedDraft draft,
        out ModuleRuntimeExtensionProfile? candidate, out StudioUx8NPreview? failure)
    {
        candidate = null;
        failure = null;
        try
        {
            var extensions = project.Module.RuntimePolicies.Extensions;
            var post = extensions.PostLaunch;
            var gameplay = post.Gameplay;
            var curated = new GameplayProductionProfile.CuratedPackSettings(gameplay.CuratedPacks.Id,
                draft.Channel, draft.Compatibility, draft.AllowedLicenses, draft.RequireTrustedSignature,
                draft.AllowOfflineFallback);
            var updatedGameplay = new GameplayProductionProfile(gameplay.Id, gameplay.SchemaVersion,
                gameplay.ZeroGravity, gameplay.Vehicles, gameplay.Mounts, gameplay.Achievements, curated,
                "studio:ux8-n:curated-packs");
            var updatedPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion, updatedGameplay,
                post.LiveDirector, post.Network, post.CreatorExperience, post.Production,
                "studio:ux8-n:curated-packs");
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
                extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments,
                extensions.SnapshotPolicy, "studio:ux8-n:curated-packs", extensions.ModernTooling,
                extensions.OperationalReadiness, updatedPost);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                           InvalidOperationException)
        {
            failure = FailedUx8N(HumanUx8N(exception.Message), "content.curated.policy_invalid",
                "module/runtimePolicies/extensions/postLaunch/gameplay/curatedPacks");
            return false;
        }
    }

    private bool TryBuildResourceCatalogUx8N(StudioUx8NResourceDraft draft,
        out StudioUx8GPackageDraft? packageDraft, out Guid resourceId, out StudioUx8NPreview? failure)
    {
        packageDraft = null;
        resourceId = draft.ResourceId;
        failure = null;
        try
        {
            var package = draft.Catalog.Packages.Single(item => item.Id == draft.PackageId);
            ContentPackageDefinition updated;
            if (draft.IsAsset)
            {
                var assets = package.Assets.Select(item => item.Id == draft.ResourceId
                    ? item with { Aliases = NormalizeAddressesUx8N(draft.Aliases), References = NormalizeAddressesUx8N(draft.References) }
                    : item).ToArray();
                if (assets.All(item => item.Id != draft.ResourceId)) throw new ArgumentException("La ressource asset est absente.");
                updated = RehashPackageUx8G(new ContentPackageDefinition(package.Id, package.SchemaVersion,
                    package.Provenance, package.PackageNamespace, package.Version, package.LicenseSpdx,
                    package.ContentSha256, package.Priority, package.SealedResources, package.Dependencies,
                    package.Sources, assets));
            }
            else
            {
                var sources = package.Sources.Select(item => item.Id == draft.ResourceId
                    ? item with { Aliases = NormalizeAddressesUx8N(draft.Aliases), References = NormalizeAddressesUx8N(draft.References) }
                    : item).ToArray();
                if (sources.All(item => item.Id != draft.ResourceId)) throw new ArgumentException("La ressource source est absente.");
                updated = RehashPackageUx8G(new ContentPackageDefinition(package.Id, package.SchemaVersion,
                    package.Provenance, package.PackageNamespace, package.Version, package.LicenseSpdx,
                    package.ContentSha256, package.Priority, package.SealedResources, package.Dependencies,
                    sources, package.Assets));
            }
            packageDraft = draft.Catalog with
            {
                Packages = draft.Catalog.Packages.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
                Approved = draft.Approved,
            };
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            failure = FailedUx8N(HumanUx8N(exception.Message), "content.resource.draft_invalid",
                "module/content/resources");
            return false;
        }
    }

    private bool TryBuildMigrationUx8N(StudioUx8NMigrationDraft draft, bool dryRun,
        out StudioUx8GPackageDraft? packageDraft, out AssetPackageInstallation? installation,
        out AssetPackageMigrationStep? step, out AssetPackageMigrationExecutionResult? execution,
        out StudioUx8NPreview? failure)
    {
        packageDraft = null;
        installation = null;
        step = null;
        execution = null;
        failure = null;
        var project = Project;
        var history = History;
        var root = Session?.RootDirectory;
        if (project is null || history is null || string.IsNullOrWhiteSpace(root) ||
            draft.Catalog.BaseRevision != history.State.Revision ||
            !string.Equals(draft.Catalog.SourceFingerprint, ProjectFingerprintUx8N(project), StringComparison.Ordinal))
        {
            failure = FailedUx8N("Le projet a changé ; rechargez la migration.",
                SetContentWorkspaceHandler.RevisionConflictCode, Ux8NPath);
            return false;
        }
        try
        {
            var package = draft.Catalog.Packages.Single(item => item.Id == draft.PackageId);
            var updated = RehashPackageUx8G(new ContentPackageDefinition(package.Id, package.SchemaVersion,
                "studio:ux8-n:migration", package.PackageNamespace, draft.TargetVersion,
                package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources,
                package.Dependencies, package.Sources, package.Assets));
            packageDraft = draft.Catalog with
            {
                Packages = draft.Catalog.Packages.Select(item => item.Id == package.Id ? updated : item).ToArray(),
                Approved = draft.Approved,
            };
            var files = package.Sources.Select(item => item.RelativePath)
                .Concat(package.Assets.Select(item => item.RelativePath))
                .Where(relative => File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var marker = $".forge/package-migrations/inputs/{package.Id:D}.txt";
            if (File.Exists(Path.Combine(root, marker.Replace('/', Path.DirectorySeparatorChar)))) files.Add(marker);
            installation = new AssetPackageInstallation(AssetPackageManifestId.From(package.Id), package.Version,
                package.Dependencies.Select(dependency => draft.Catalog.Packages.SingleOrDefault(item =>
                        item.PackageNamespace == dependency.PackageNamespace))
                    .Where(item => item is not null)
                    .Select(item => AssetPackageManifestId.From(item!.Id)).ToArray(), files.Distinct().ToArray());
            step = new AssetPackageMigrationStep(MigrationStepIdUx8N(package.Id, package.Version,
                    draft.TargetVersion), AssetPackageManifestId.From(package.Id), package.Version,
                draft.TargetVersion, true, true,
                "Étape Studio compilée de compatibilité métadonnées; aucun contenu arbitraire exécuté.");
            var plan = AssetPackageMigrationPlanner.Plan(installation, draft.TargetVersion, [step], dryRun);
            execution = AssetPackageMigrationStore.Execute(root, installation, plan,
                new StudioMetadataMigrationHostStepUx8N(step.Id));
            if (!execution.Succeeded)
            {
                failure = FailedUx8N(HumanUx8N(execution.Diagnostics.FirstOrDefault()?.Message),
                    execution.Diagnostics.FirstOrDefault()?.Code ?? AssetPackageMigrationStore.PlanInvalidCode,
                    Ux8NPath);
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           IOException or UnauthorizedAccessException)
        {
            failure = FailedUx8N(HumanUx8N(exception.Message), "content.package.migration_invalid", Ux8NPath);
            return false;
        }
    }

    private bool TryBuildUninstallUx8N(StudioUx8NUninstallDraft draft,
        out StudioUx8GPackageDraft? packageDraft, out AssetPackageUninstallImpact? impact,
        out ContentPackageDefinition? package, out ContentPackageDefinition? replacement,
        out AssetVaultPackageMutationPlan? vaultPlan, out StudioUx8NPreview? failure)
    {
        packageDraft = null;
        impact = null;
        package = null;
        replacement = null;
        vaultPlan = null;
        failure = null;
        var project = Project;
        var history = History;
        if (project is null || history is null || draft.Catalog.BaseRevision != history.State.Revision ||
            !string.Equals(draft.Catalog.SourceFingerprint, ProjectFingerprintUx8N(project), StringComparison.Ordinal))
        {
            failure = FailedUx8N("Le projet a changé ; rechargez la désinstallation.",
                SetContentWorkspaceHandler.RevisionConflictCode, Ux8NPath);
            return false;
        }
        try
        {
            var sourcePackage = draft.Catalog.Packages.Single(item => item.Id == draft.PackageId);
            var sourceReplacement = draft.ReplacementPackageId is null ? null : draft.Catalog.Packages.Single(item =>
                item.Id == draft.ReplacementPackageId.Value && item.Id != sourcePackage.Id);
            package = sourcePackage;
            replacement = sourceReplacement;
            var installations = draft.Catalog.Packages.Select(item => new AssetPackageInstallation(
                AssetPackageManifestId.From(item.Id), item.Version,
                item.Dependencies.Select(dependency => draft.Catalog.Packages.SingleOrDefault(candidate =>
                        candidate.PackageNamespace == dependency.PackageNamespace))
                    .Where(candidate => candidate is not null)
                    .Select(candidate => AssetPackageManifestId.From(candidate!.Id)).ToArray(),
                item.Sources.Select(source => source.RelativePath)
                    .Concat(item.Assets.Select(asset => asset.RelativePath)).ToArray())).ToArray();
            var replacements = sourceReplacement is null ? Array.Empty<AssetPackageReplacement>() :
                [new AssetPackageReplacement(AssetPackageManifestId.From(sourcePackage.Id),
                    AssetPackageManifestId.From(sourceReplacement.Id))];
            impact = AssetPackageUninstallAnalyzer.Analyze(AssetPackageManifestId.From(sourcePackage.Id),
                installations, replacements);
            if (!impact.CanUninstall)
            {
                failure = FailedUx8N("Des packages dépendent encore de cette entrée; choisissez un remplacement installé.",
                    impact.Diagnostics.FirstOrDefault()?.Code ?? AssetPackageUninstallAnalyzer.DependentPackageCode,
                    impact.Diagnostics.FirstOrDefault()?.Path ?? Ux8NPath);
                return false;
            }
            var remaining = draft.Catalog.Packages.Where(item => item.Id != sourcePackage.Id).Select(item =>
            {
                if (sourceReplacement is null || item.Dependencies.All(dependency =>
                        dependency.PackageNamespace != sourcePackage.PackageNamespace)) return item;
                var dependencies = item.Dependencies.Select(dependency =>
                    dependency.PackageNamespace == sourcePackage.PackageNamespace
                        ? dependency with
                        {
                            PackageNamespace = sourceReplacement.PackageNamespace,
                            VersionConstraint = $">={sourceReplacement.Version}",
                            Provenance = "studio:ux8-n:replacement",
                        }
                        : dependency).ToArray();
                return RehashPackageUx8G(new ContentPackageDefinition(item.Id, item.SchemaVersion,
                    item.Provenance, item.PackageNamespace, item.Version, item.LicenseSpdx,
                    item.ContentSha256, item.Priority, item.SealedResources, dependencies,
                    item.Sources, item.Assets));
            }).ToArray();
            packageDraft = draft.Catalog with { Packages = remaining, Approved = draft.Approved };
            if (draft.RemoveFromAssetVault)
            {
                if (!TryGetVaultStoreUx8N(out var store, out var error))
                {
                    failure = FailedUx8N(error, "asset.vault.unavailable", "assets/packages");
                    return false;
                }
                vaultPlan = store!.PlanUninstall(sourcePackage.PackageNamespace, true);
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           InvalidDataException)
        {
            failure = FailedUx8N(HumanUx8N(exception.Message), "content.package.uninstall_invalid", Ux8NPath);
            return false;
        }
    }

    private bool TryGetVaultStoreUx8N(out AssetVaultPackageStore? store, out string error)
    {
        store = null;
        error = string.Empty;
        if (_vaultOverrideUx8N is not null)
        {
            store = new AssetVaultPackageStore(_vaultOverrideUx8N);
            return true;
        }
        if (!EnsureAssetAuthoringStateUx3F(out error) || _catalogLoadUx3F is null) return false;
        store = new AssetVaultPackageStore(_catalogLoadUx3F.VaultRoot);
        return true;
    }

    private static StudioUx8NPreview FromPackagePreviewUx8N(StudioUx8GPackagePreview preview) => new(
        preview.Succeeded, preview.Changed, preview.CanonicalDryRun, preview.CanApply,
        preview.Message, preview.Patch,
        preview.ResourcePlan.Concat(preview.DependencyPlan).ToArray(),
        preview.Diagnostics.Select(item => new StudioUx8NDiagnostic(item.Code, item.Path, item.Message)).ToArray());

    private static StudioUx8NPreview FailedCommandUx8N(CommandResult result, string path) => new(
        false, false, false, false,
        HumanUx8N(result.Diagnostics.FirstOrDefault()?.Message), string.Empty, [],
        result.Diagnostics.Select(item => new StudioUx8NDiagnostic(item.Code, path, HumanUx8N(item.Message))).ToArray());

    private static StudioUx8NPreview FailedUx8N(string message, string code, string path) => new(
        false, false, false, false, message, string.Empty, [],
        [new StudioUx8NDiagnostic(code, path, message)]);

    private static StudioUx8NCuratedDraft FreezeCuratedUx8N(StudioUx8NCuratedDraft draft) =>
        draft with { AllowedLicenses = draft.AllowedLicenses.ToArray() };

    private static StudioUx8NMigrationDraft FreezeMigrationUx8N(StudioUx8NMigrationDraft draft) =>
        draft with { Catalog = draft.Catalog with { Packages = draft.Catalog.Packages.ToArray() } };

    private static StudioUx8NUninstallDraft FreezeUninstallUx8N(StudioUx8NUninstallDraft draft) =>
        draft with { Catalog = draft.Catalog with { Packages = draft.Catalog.Packages.ToArray() } };

    private static IReadOnlyList<string> NormalizeAddressesUx8N(IReadOnlyList<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value)).Select(ContentResourceRegistry.NormalizeAddress)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static bool SameCuratedUx8N(GameplayProductionProfile.CuratedPackSettings left,
        GameplayProductionProfile.CuratedPackSettings right) =>
        left.Id == right.Id && left.Channel == right.Channel && left.Compatibility == right.Compatibility &&
        left.AllowedLicenses.SequenceEqual(right.AllowedLicenses) &&
        left.RequireTrustedSignature == right.RequireTrustedSignature &&
        left.AllowOfflineFallback == right.AllowOfflineFallback;

    private static string CandidateFingerprintUx8N(ProjectDefinition project,
        ModuleRuntimeExtensionProfile candidate) => ProjectFingerprintUx8N(BuildProjectWithExtensionsUx8N(project, candidate));

    private static ProjectDefinition BuildProjectWithExtensionsUx8N(ProjectDefinition source,
        ModuleRuntimeExtensionProfile candidate)
    {
        var previous = source.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(previous.Id, previous.SchemaVersion,
            previous.Heartbeats, previous.Rest, previous.SequenceAbort, "runtime-extension-command",
            previous.Cinematics, previous.Gameplay, previous.Rules, candidate, previous.GameplayEvents,
            previous.World, previous.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(policies),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    private static string ProjectFingerprintUx8N(ProjectDefinition project) =>
        HashUx8N(ProjectJsonSerializer.Serialize(project));

    private static string MigrationFingerprintUx8N(StudioUx8NMigrationDraft draft,
        StudioUx8GPackageDraft candidate, AssetPackageMigrationStep step) => HashUx8N(
        $"{draft.Catalog.SourceFingerprint}\n{draft.PackageId:D}\n{draft.TargetVersion}\n" +
        $"{step.Id:D}\n{string.Join('\n', candidate.Packages.Select(item => item.ContentSha256))}");

    private static string UninstallFingerprintUx8N(StudioUx8NUninstallDraft draft,
        StudioUx8GPackageDraft candidate) => HashUx8N(
        $"{draft.Catalog.SourceFingerprint}\n{draft.PackageId:D}\n{draft.ReplacementPackageId:D}\n" +
        $"{draft.RemoveFromAssetVault}\n{string.Join('\n', candidate.Packages.Select(item => item.ContentSha256))}");

    private static string HashUx8N(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static Guid MigrationStepIdUx8N(Guid packageId, string from, string to)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:studio:migration:{packageId:D}:{from}:{to}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string NextPatchVersionUx8N(string version)
    {
        var parts = version.Split('.', StringSplitOptions.TrimEntries);
        return parts.Length == 3 && int.TryParse(parts[0], out var major) &&
               int.TryParse(parts[1], out var minor) && int.TryParse(parts[2], out var patch)
            ? $"{major}.{minor}.{patch + 1}"
            : version;
    }

    internal static string HumanUx8N(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return "aucun";
        return Regex.Replace(text,
            "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "identifiant protégé", RegexOptions.CultureInvariant);
    }

    private void CancelCuratedApprovalUx8N()
    {
        _approvedCuratedUx8N = null;
        _approvedCuratedRevisionUx8N = null;
        _approvedCuratedFingerprintUx8N = null;
    }

    private void CancelMigrationApprovalUx8N(bool cancelPackage = true)
    {
        _approvedMigrationUx8N = null;
        _approvedMigrationFingerprintUx8N = null;
        if (cancelPackage) CancelPackageApprovalUx8G();
    }

    private void CancelUninstallApprovalUx8N(bool cancelPackage = true)
    {
        _approvedUninstallUx8N = null;
        _approvedUninstallFingerprintUx8N = null;
        if (cancelPackage) CancelPackageApprovalUx8G();
    }

    internal void CancelAllUx8NApprovals()
    {
        CancelCuratedApprovalUx8N();
        CancelMigrationApprovalUx8N();
        CancelUninstallApprovalUx8N();
    }

    private void CloseUx8N()
    {
        CancelAllUx8NApprovals();
        _lastMigrationUx8N = null;
        _lastUninstallUx8N = null;
        _vaultOverrideUx8N = null;
    }

    private sealed class StudioMetadataMigrationHostStepUx8N(Guid stepId) : IAssetPackageMigrationHostStep
    {
        public Guid StepId { get; } = stepId;

        public void Apply(string workspaceRoot, IReadOnlyList<string> installedRelativePaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
            ArgumentNullException.ThrowIfNull(installedRelativePaths);
            foreach (var relative in installedRelativePaths)
            {
                var full = Path.GetFullPath(Path.Combine(workspaceRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(full)) throw new InvalidOperationException("Une entrée de migration a disparu.");
            }
        }
    }

    private sealed record StudioUx8NMigrationAction(
        StudioUx8NMigrationDraft Draft,
        AssetPackageInstallation Installation,
        AssetPackageMigrationStep Step,
        AssetPackageMigrationExecutionResult Execution,
        bool Undone);

    private sealed record StudioUx8NUninstallAction(
        StudioUx8NUninstallDraft Draft,
        string PackageNamespace,
        AssetVaultPackageMutationResult? VaultResult,
        bool Undone);
}
