using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public sealed class AssetVaultPackageStore
{
    public const string StateRelativePath = "metadata/asset-vault-installations.v1.json";
    public const string CreditsMarkdownRelativePath = "metadata/CREDITS_ASSETS.md";
    public const string CreditsTextRelativePath = "metadata/CREDITS_ASSETS.txt";
    public const string AssetMissingCode = "asset.vault.install.asset_missing";
    public const string AssetUnavailableCode = "asset.vault.install.asset_unavailable";
    public const string LicenseRejectedCode = "asset.vault.install.license_rejected";
    public const string PackageAlreadyInstalledCode = "asset.vault.install.update_required";
    public const string PackageMissingCode = "asset.vault.uninstall.package_missing";
    public const string StateStaleCode = "asset.vault.state.stale";
    public const string ObjectCorruptCode = "asset.vault.object.corrupt";
    public const string SourceChangedCode = "asset.vault.source.changed";
    public const string RestoreInvalidCode = "asset.vault.restore.invalid";
    public const string RestoreBackupMissingCode = "asset.vault.restore.backup_missing";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly string _vaultRoot;

    public AssetVaultPackageStore(string vaultRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        _vaultRoot = Path.GetFullPath(vaultRoot);
    }

    public string VaultRoot => _vaultRoot;

    public AssetVaultInstallationState LoadState()
    {
        var path = ResolveFixed(StateRelativePath);
        if (!File.Exists(path))
        {
            return new AssetVaultInstallationState();
        }

        return ReadState(path);
    }

    private static AssetVaultInstallationState ReadState(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var document = JsonSerializer.Deserialize<StateDocument>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Asset Vault installation state is empty.");
            if (document.SchemaVersion != AssetVaultInstallationState.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Asset Vault installation schema '{document.SchemaVersion}' is not supported.");
            }

            if (document.Packages is null)
            {
                throw new InvalidDataException("Asset Vault installation state has no package collection.");
            }

            return new AssetVaultInstallationState(
                document.Packages.Select(MapPackage).ToArray(),
                document.SchemaVersion);
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or FormatException or InvalidOperationException)
        {
            throw new InvalidDataException("Asset Vault installation state is invalid.", exception);
        }
    }

    public AssetVaultPackageMutationPlan PlanInstall(
        AssetCatalog catalog,
        string packageId,
        string version,
        IReadOnlyList<string> assetExternalIds,
        bool dryRun = true)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(assetExternalIds);
        var normalizedPackageId = NormalizePackageId(packageId);
        var current = LoadState();
        var diagnostics = new List<DomainDiagnostic>();
        var existing = current.Packages.SingleOrDefault(package => string.Equals(
            package.PackageId,
            normalizedPackageId,
            StringComparison.Ordinal));
        var requestedIds = assetExternalIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (requestedIds.Length == 0)
        {
            diagnostics.Add(new DomainDiagnostic(
                AssetMissingCode,
                "Installing an Asset Vault package requires at least one catalog asset.",
                $"assets/packages/{normalizedPackageId}"));
        }

        var installedAssets = new List<AssetVaultInstalledAsset>();
        var plannedObjects = new Dictionary<string, AssetVaultPlannedObject>(StringComparer.Ordinal);
        foreach (var externalId in requestedIds)
        {
            var item = catalog.Items.SingleOrDefault(candidate => string.Equals(
                candidate.ExternalId,
                externalId,
                StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                diagnostics.Add(new DomainDiagnostic(
                    AssetMissingCode,
                    $"Catalog asset '{externalId}' does not exist.",
                    $"assets/packages/{normalizedPackageId}/{externalId}"));
                continue;
            }

            if (!item.LicenseVerified || !string.Equals(item.LicenseSpdx, "CC0-1.0", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DomainDiagnostic(
                    LicenseRejectedCode,
                    $"Asset '{item.ExternalId}' is outside the verified CC0 core policy.",
                    $"assets/{item.Id}/license"));
                continue;
            }

            if (item.Availability == AssetAvailability.CatalogOnly || string.IsNullOrWhiteSpace(item.RelativeModelPath))
            {
                diagnostics.Add(new DomainDiagnostic(
                    AssetUnavailableCode,
                    $"Asset '{item.ExternalId}' is catalog-only and cannot be installed yet.",
                    $"assets/{item.Id}/relativeModelPath"));
                continue;
            }

            var files = new List<AssetVaultInstalledFile>();
            TryPlanFile(
                item.RelativeModelPath,
                AssetVaultFileRole.Model,
                normalizedPackageId,
                item,
                files,
                plannedObjects,
                diagnostics,
                required: true);
            if (!string.IsNullOrWhiteSpace(item.RelativePreviewPath))
            {
                TryPlanFile(
                    item.RelativePreviewPath,
                    AssetVaultFileRole.Preview,
                    normalizedPackageId,
                    item,
                    files,
                    plannedObjects,
                    diagnostics,
                    required: false);
            }

            if (files.Any(file => file.Role == AssetVaultFileRole.Model))
            {
                installedAssets.Add(new AssetVaultInstalledAsset(
                    item.Id,
                    item.ExternalId,
                    item.DisplayName,
                    item.Source,
                    item.SourcePage,
                    item.Author,
                    item.LicenseSpdx,
                    item.LicenseUrl,
                    item.AttributionRequired,
                    item.AttributionText,
                    files));
            }
        }

        AssetVaultInstallationState resultingState;
        if (installedAssets.Count > 0)
        {
            var package = new AssetVaultInstalledPackage(normalizedPackageId, version, installedAssets);
            if (existing is not null && !PackagesEquivalent(existing, package))
            {
                diagnostics.Add(new DomainDiagnostic(
                    PackageAlreadyInstalledCode,
                    $"Package '{normalizedPackageId}' is already installed; use the governed update workflow.",
                    $"assets/packages/{normalizedPackageId}"));
                resultingState = current;
            }
            else
            {
                resultingState = current.WithPackage(package);
            }
        }
        else
        {
            resultingState = current;
        }

        return CreatePlan(
            current,
            AssetVaultMutationOperation.Install,
            normalizedPackageId,
            dryRun,
            resultingState,
            plannedObjects.Values,
            diagnostics);
    }

    public AssetVaultPackageMutationPlan PlanUninstall(string packageId, bool dryRun = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var normalizedPackageId = NormalizePackageId(packageId);
        var current = LoadState();
        var diagnostics = new List<DomainDiagnostic>();
        if (!current.Packages.Any(package => string.Equals(
            package.PackageId,
            normalizedPackageId,
            StringComparison.Ordinal)))
        {
            diagnostics.Add(new DomainDiagnostic(
                PackageMissingCode,
                $"Package '{normalizedPackageId}' is not installed.",
                $"assets/packages/{normalizedPackageId}"));
        }

        return CreatePlan(
            current,
            AssetVaultMutationOperation.Uninstall,
            normalizedPackageId,
            dryRun,
            diagnostics.Count == 0 ? current.WithoutPackage(normalizedPackageId) : current,
            [],
            diagnostics);
    }

    public AssetVaultPackageMutationResult Apply(AssetVaultPackageMutationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var current = LoadState();
        if (!string.Equals(ComputeRevision(current), plan.BaseRevision, StringComparison.Ordinal))
        {
            var diagnostic = new DomainDiagnostic(
                StateStaleCode,
                "Asset Vault installation state changed after the plan was created.",
                StateRelativePath);
            return Failure(plan, current, [diagnostic]);
        }

        if (!plan.CanApply)
        {
            return Failure(plan, current, plan.Diagnostics);
        }

        if (plan.DryRun)
        {
            return new AssetVaultPackageMutationResult(
                true,
                false,
                plan.PlanId,
                plan.Operation,
                plan.PackageId,
                current,
                0,
                plan.Objects.Count(item => item.Reused),
                0,
                plan.NewBytes,
                plan.ReusedBytes,
                null,
                null,
                plan.Credits,
                []);
        }

        var written = new List<string>();
        var stagedOrphans = new List<(string Source, string Backup)>();
        var reusedObjectCount = 0;
        var deleted = 0;
        string? backupRelativePath = null;
        string? receiptRelativePath = null;
        try
        {
            if (plan.Operation == AssetVaultMutationOperation.Install)
            {
                var preflightDiagnostics = new List<DomainDiagnostic>();
                foreach (var item in plan.Objects)
                {
                    var actualHash = HashFile(item.SourcePath);
                    if (!string.Equals(actualHash, item.ContentSha256, StringComparison.Ordinal))
                    {
                        preflightDiagnostics.Add(new DomainDiagnostic(
                            SourceChangedCode,
                            $"Asset source '{item.SourcePath}' changed after planning.",
                            item.SourcePath));
                        continue;
                    }

                    var target = ResolveFixed(item.ObjectRelativePath);
                    if (File.Exists(target))
                    {
                        if (!string.Equals(HashFile(target), item.ContentSha256, StringComparison.Ordinal))
                        {
                            preflightDiagnostics.Add(new DomainDiagnostic(
                                ObjectCorruptCode,
                                $"Content-addressed object '{item.ContentSha256}' is corrupt.",
                                item.ObjectRelativePath));
                        }
                    }
                }

                if (preflightDiagnostics.Count > 0)
                {
                    return Failure(plan, current, preflightDiagnostics);
                }

                foreach (var item in plan.Objects)
                {
                    var target = ResolveFixed(item.ObjectRelativePath);
                    if (File.Exists(target))
                    {
                        reusedObjectCount++;
                        continue;
                    }
                    CopyAtomically(item.SourcePath, target);
                    written.Add(target);
                }
            }
            else
            {
                backupRelativePath = $"metadata/backups/{plan.PlanId:D}/asset-vault-installations.v1.json";
                WriteAtomically(ResolveFixed(backupRelativePath), Serialize(current));
                StageOrphanObjects(current, plan.ResultingState, plan.PlanId, stagedOrphans);
                deleted = stagedOrphans.Count;
            }

            WriteCredits(plan.Credits);
            receiptRelativePath = $"metadata/receipts/{plan.PlanId:D}.json";
            WriteAtomically(
                ResolveFixed(receiptRelativePath),
                JsonSerializer.Serialize(new ReceiptDocument(
                    AssetVaultInstallationState.CurrentSchemaVersion,
                    plan.PlanId,
                    plan.Operation.ToString().ToLowerInvariant(),
                    plan.PackageId,
                    plan.BaseRevision,
                    ComputeRevision(plan.ResultingState),
                    written.Count,
                    reusedObjectCount,
                    deleted,
                    plan.NewBytes,
                    plan.ReusedBytes), JsonOptions) + Environment.NewLine);
            WriteAtomically(ResolveFixed(StateRelativePath), Serialize(plan.ResultingState));
            return new AssetVaultPackageMutationResult(
                true,
                true,
                plan.PlanId,
                plan.Operation,
                plan.PackageId,
                plan.ResultingState,
                written.Count,
                reusedObjectCount,
                deleted,
                plan.NewBytes,
                plan.ReusedBytes,
                receiptRelativePath,
                backupRelativePath,
                plan.Credits,
                []);
        }
        catch
        {
            if (receiptRelativePath is not null)
            {
                var receiptPath = ResolveFixed(receiptRelativePath);
                if (File.Exists(receiptPath))
                {
                    File.Delete(receiptPath);
                }
            }

            foreach (var orphan in stagedOrphans.AsEnumerable().Reverse())
            {
                if (!File.Exists(orphan.Backup))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(orphan.Source)!);
                File.Copy(orphan.Backup, orphan.Source, overwrite: false);
            }

            foreach (var path in written)
            {
                if (File.Exists(path) && !current.BlobReferenceCounts.ContainsKey(Path.GetFileName(path)))
                {
                    File.Delete(path);
                }
            }

            WriteCredits(AssetCreditCatalog.Aggregate(current));
            throw;
        }
    }

    public AssetVaultPackageMutationResult RestoreUninstall(AssetVaultPackageMutationResult uninstallResult)
    {
        ArgumentNullException.ThrowIfNull(uninstallResult);
        var current = LoadState();
        var restorePlanId = StableGuid($"restore|{uninstallResult.PlanId:D}|{ComputeRevision(current)}");
        if (!uninstallResult.Succeeded || !uninstallResult.Applied ||
            uninstallResult.Operation != AssetVaultMutationOperation.Uninstall ||
            uninstallResult.BackupRelativePath is null ||
            !string.Equals(ComputeRevision(current), ComputeRevision(uninstallResult.State), StringComparison.Ordinal))
        {
            return RestoreFailure(restorePlanId, uninstallResult.PackageId, current, RestoreInvalidCode,
                "Only an applied uninstall result matching the current Asset Vault state can be restored.");
        }

        var expectedBackup = $"metadata/backups/{uninstallResult.PlanId:D}/asset-vault-installations.v1.json";
        if (!string.Equals(uninstallResult.BackupRelativePath, expectedBackup, StringComparison.Ordinal))
        {
            return RestoreFailure(restorePlanId, uninstallResult.PackageId, current, RestoreInvalidCode,
                "The uninstall backup path does not match the governed plan identity.");
        }

        var backupPath = ResolveFixed(expectedBackup);
        if (!File.Exists(backupPath))
        {
            return RestoreFailure(restorePlanId, uninstallResult.PackageId, current, RestoreBackupMissingCode,
                "The governed Asset Vault uninstall backup is missing.");
        }

        AssetVaultInstallationState previous;
        try
        {
            previous = ReadState(backupPath);
        }
        catch (InvalidDataException exception)
        {
            return RestoreFailure(restorePlanId, uninstallResult.PackageId, current, RestoreInvalidCode,
                exception.Message);
        }

        if (current.Packages.Any(package => string.Equals(package.PackageId,
                uninstallResult.PackageId, StringComparison.Ordinal)) ||
            !previous.Packages.Any(package => string.Equals(package.PackageId,
                uninstallResult.PackageId, StringComparison.Ordinal)))
        {
            return RestoreFailure(restorePlanId, uninstallResult.PackageId, current, RestoreInvalidCode,
                "The uninstall backup does not restore exactly the removed package.");
        }

        var restoredObjects = new List<string>();
        string? receiptRelativePath = null;
        try
        {
            var currentHashes = current.BlobReferenceCounts;
            foreach (var hash in previous.BlobReferenceCounts.Keys
                         .Where(hash => !currentHashes.ContainsKey(hash))
                         .Order(StringComparer.Ordinal))
            {
                var source = ResolveFixed($"metadata/backups/{uninstallResult.PlanId:D}/objects/{hash}");
                if (!File.Exists(source) || !string.Equals(HashFile(source), hash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Governed object backup '{hash}' is missing or corrupt.");
                }

                var target = ResolveFixed(ObjectRelativePath(hash));
                if (File.Exists(target))
                {
                    if (!string.Equals(HashFile(target), hash, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Content-addressed object '{hash}' is corrupt.");
                    }
                    continue;
                }

                CopyAtomically(source, target);
                restoredObjects.Add(target);
            }

            WriteCredits(AssetCreditCatalog.Aggregate(previous));
            receiptRelativePath = $"metadata/receipts/{restorePlanId:D}.json";
            WriteAtomically(
                ResolveFixed(receiptRelativePath),
                JsonSerializer.Serialize(new ReceiptDocument(
                    AssetVaultInstallationState.CurrentSchemaVersion,
                    restorePlanId,
                    AssetVaultMutationOperation.Restore.ToString().ToLowerInvariant(),
                    uninstallResult.PackageId,
                    ComputeRevision(current),
                    ComputeRevision(previous),
                    restoredObjects.Count,
                    0,
                    0,
                    0,
                    0), JsonOptions) + Environment.NewLine);
            WriteAtomically(ResolveFixed(StateRelativePath), Serialize(previous));
            return new AssetVaultPackageMutationResult(
                true,
                true,
                restorePlanId,
                AssetVaultMutationOperation.Restore,
                uninstallResult.PackageId,
                previous,
                restoredObjects.Count,
                0,
                0,
                0,
                0,
                receiptRelativePath,
                expectedBackup,
                AssetCreditCatalog.Aggregate(previous),
                []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidDataException or InvalidOperationException)
        {
            if (receiptRelativePath is not null)
            {
                var receiptPath = ResolveFixed(receiptRelativePath);
                if (File.Exists(receiptPath)) File.Delete(receiptPath);
            }
            foreach (var path in restoredObjects)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            WriteCredits(AssetCreditCatalog.Aggregate(current));
            return RestoreFailure(restorePlanId, uninstallResult.PackageId, current,
                RestoreBackupMissingCode, exception.Message);
        }
    }

    public string RenderCreditsMarkdown(AssetVaultInstallationState? state = null)
    {
        var credits = AssetCreditCatalog.Aggregate(state ?? LoadState());
        var lines = new List<string>
        {
            "# Asset credits",
            string.Empty,
            "Generated from the assets currently installed in the OpenRPG Forge Asset Vault.",
            string.Empty,
        };
        if (credits.Count == 0)
        {
            lines.Add("No governed asset is currently installed.");
        }

        foreach (var credit in credits)
        {
            lines.Add($"## {credit.Author} — {credit.Source}");
            lines.Add(string.Empty);
            lines.Add($"- License: {credit.LicenseSpdx} ({credit.LicenseUrl})");
            lines.Add($"- Attribution: {(credit.AttributionRequired ? "required" : "not required; courtesy credit")}");
            if (!string.IsNullOrWhiteSpace(credit.AttributionText))
            {
                lines.Add($"- Attribution text: {credit.AttributionText}");
            }

            lines.Add($"- Installed assets: {string.Join(", ", credit.AssetExternalIds.Select(id => $"`{id}`"))}");
            lines.Add($"- Sources: {string.Join(", ", credit.SourcePages)}");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public string RenderCreditsText(AssetVaultInstallationState? state = null)
    {
        var credits = AssetCreditCatalog.Aggregate(state ?? LoadState());
        if (credits.Count == 0)
        {
            return "No governed asset is currently installed." + Environment.NewLine;
        }

        return string.Join(Environment.NewLine + Environment.NewLine, credits.Select(credit =>
            $"{credit.Author} ({credit.Source}) | {credit.LicenseSpdx} | " +
            $"attribution {(credit.AttributionRequired ? "required" : "not required")} | " +
            $"assets: {string.Join(", ", credit.AssetExternalIds)}")) + Environment.NewLine;
    }

    private AssetVaultPackageMutationPlan CreatePlan(
        AssetVaultInstallationState current,
        AssetVaultMutationOperation operation,
        string packageId,
        bool dryRun,
        AssetVaultInstallationState resultingState,
        IEnumerable<AssetVaultPlannedObject> objects,
        IReadOnlyList<DomainDiagnostic> diagnostics)
    {
        var orderedObjects = objects.OrderBy(item => item.ContentSha256, StringComparer.Ordinal).ToArray();
        var baseRevision = ComputeRevision(current);
        var planId = StableGuid(string.Join('|',
            baseRevision,
            operation,
            packageId,
            ComputeRevision(resultingState),
            string.Join(',', orderedObjects.Select(item => item.ContentSha256))));
        return new AssetVaultPackageMutationPlan(
            planId,
            baseRevision,
            operation,
            packageId,
            dryRun,
            resultingState,
            orderedObjects,
            AssetCreditCatalog.Aggregate(resultingState),
            diagnostics.OrderBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ToArray());
    }

    private void TryPlanFile(
        string relativePath,
        AssetVaultFileRole role,
        string packageId,
        AssetCatalogItem item,
        ICollection<AssetVaultInstalledFile> files,
        IDictionary<string, AssetVaultPlannedObject> objects,
        ICollection<DomainDiagnostic> diagnostics,
        bool required)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
        {
            diagnostics.Add(new DomainDiagnostic(
                AssetUnavailableCode,
                $"Asset '{item.ExternalId}' contains an unsafe source path.",
                $"assets/packages/{packageId}/{item.ExternalId}/{role.ToString().ToLowerInvariant()}"));
            return;
        }

        var sourcePath = ResolveContained(relativePath);
        if (!File.Exists(sourcePath))
        {
            if (required)
            {
                diagnostics.Add(new DomainDiagnostic(
                    AssetUnavailableCode,
                    $"Asset source '{relativePath}' does not exist in the configured Vault.",
                    relativePath));
            }

            return;
        }

        var hash = HashFile(sourcePath);
        var bytes = new FileInfo(sourcePath).Length;
        var objectRelativePath = ObjectRelativePath(hash);
        var objectPath = ResolveFixed(objectRelativePath);
        var reused = File.Exists(objectPath);
        if (reused && !string.Equals(HashFile(objectPath), hash, StringComparison.Ordinal))
        {
            diagnostics.Add(new DomainDiagnostic(
                ObjectCorruptCode,
                $"Content-addressed object '{hash}' is corrupt.",
                objectRelativePath));
            return;
        }

        files.Add(new AssetVaultInstalledFile(
            role,
            hash,
            bytes,
            relativePath,
            Path.GetFileName(sourcePath)));
        objects.TryAdd(hash, new AssetVaultPlannedObject(hash, bytes, sourcePath, objectRelativePath, reused));
    }

    private void StageOrphanObjects(
        AssetVaultInstallationState previous,
        AssetVaultInstallationState current,
        Guid planId,
        ICollection<(string Source, string Backup)> staged)
    {
        var retained = current.BlobReferenceCounts;
        var orphanHashes = previous.BlobReferenceCounts.Keys
            .Where(hash => !retained.ContainsKey(hash))
            .Order(StringComparer.Ordinal)
            .ToArray();
        foreach (var hash in orphanHashes)
        {
            var path = ResolveFixed(ObjectRelativePath(hash));
            if (!File.Exists(path))
            {
                continue;
            }

            var backup = ResolveFixed($"metadata/backups/{planId:D}/objects/{hash}");
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            if (File.Exists(backup))
            {
                if (!string.Equals(HashFile(path), HashFile(backup), StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Existing governed object backup '{hash}' is corrupt.");
                }
                File.Delete(path);
            }
            else
            {
                File.Move(path, backup, overwrite: false);
            }
            staged.Add((path, backup));
            var parent = Path.GetDirectoryName(path)!;
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            {
                Directory.Delete(parent);
            }
        }

    }

    private void WriteCredits(IReadOnlyList<AssetCreditEntry> credits)
    {
        var state = new AssetVaultInstallationState();
        var markdown = credits.Count == 0
            ? RenderCreditsMarkdown(state)
            : RenderCreditsMarkdownFromEntries(credits);
        var text = credits.Count == 0
            ? RenderCreditsText(state)
            : RenderCreditsTextFromEntries(credits);
        WriteAtomically(ResolveFixed(CreditsMarkdownRelativePath), markdown);
        WriteAtomically(ResolveFixed(CreditsTextRelativePath), text);
    }

    private static string RenderCreditsMarkdownFromEntries(IReadOnlyList<AssetCreditEntry> credits)
    {
        var lines = new List<string>
        {
            "# Asset credits",
            string.Empty,
            "Generated from the assets currently installed in the OpenRPG Forge Asset Vault.",
            string.Empty,
        };
        foreach (var credit in credits)
        {
            lines.Add($"## {credit.Author} — {credit.Source}");
            lines.Add(string.Empty);
            lines.Add($"- License: {credit.LicenseSpdx} ({credit.LicenseUrl})");
            lines.Add($"- Attribution: {(credit.AttributionRequired ? "required" : "not required; courtesy credit")}");
            if (!string.IsNullOrWhiteSpace(credit.AttributionText))
            {
                lines.Add($"- Attribution text: {credit.AttributionText}");
            }
            lines.Add($"- Installed assets: {string.Join(", ", credit.AssetExternalIds.Select(id => $"`{id}`"))}");
            lines.Add($"- Sources: {string.Join(", ", credit.SourcePages)}");
            lines.Add(string.Empty);
        }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string RenderCreditsTextFromEntries(IReadOnlyList<AssetCreditEntry> credits) =>
        string.Join(Environment.NewLine + Environment.NewLine, credits.Select(credit =>
            $"{credit.Author} ({credit.Source}) | {credit.LicenseSpdx} | " +
            $"attribution {(credit.AttributionRequired ? "required" : "not required")} | " +
            $"assets: {string.Join(", ", credit.AssetExternalIds)}")) + Environment.NewLine;

    private string ComputeRevision(AssetVaultInstallationState state) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(state))))
            .ToLowerInvariant();

    private static string Serialize(AssetVaultInstallationState state) =>
        JsonSerializer.Serialize(MapState(state), JsonOptions) + Environment.NewLine;

    private static StateDocument MapState(AssetVaultInstallationState state) => new(
        state.SchemaVersion,
        state.Packages.Select(package => new PackageDocument(
            package.PackageId,
            package.Version,
            package.Assets.Select(asset => new AssetDocument(
                asset.AssetId.ToString(),
                asset.ExternalId,
                asset.DisplayName,
                asset.Source,
                asset.SourcePage,
                asset.Author,
                asset.LicenseSpdx,
                asset.LicenseUrl,
                asset.AttributionRequired,
                asset.AttributionText,
                asset.Files.Select(file => new FileDocument(
                    file.Role.ToString().ToLowerInvariant(),
                    file.ContentSha256,
                    file.Bytes,
                    file.SourceRelativePath,
                    file.OriginalFileName)).ToArray())).ToArray())).ToArray());

    private static AssetVaultInstalledPackage MapPackage(PackageDocument package)
    {
        if (package.Assets is null || package.Assets.Any(asset => asset.Files is null))
        {
            throw new InvalidDataException("An installed Asset Vault package has incomplete asset data.");
        }

        return new AssetVaultInstalledPackage(
            package.PackageId,
            package.Version,
            package.Assets.Select(asset => new AssetVaultInstalledAsset(
                AssetCatalogItemId.Parse(asset.AssetId),
                asset.ExternalId,
                asset.DisplayName,
                asset.Source,
                asset.SourcePage,
                asset.Author,
                asset.LicenseSpdx,
                asset.LicenseUrl,
                asset.AttributionRequired,
                asset.AttributionText,
                asset.Files.Select(file => new AssetVaultInstalledFile(
                    Enum.Parse<AssetVaultFileRole>(file.Role, ignoreCase: true),
                    file.ContentSha256,
                    file.Bytes,
                    file.SourceRelativePath,
                    file.OriginalFileName)).ToArray())).ToArray());
    }

    private static bool PackagesEquivalent(
        AssetVaultInstalledPackage left,
        AssetVaultInstalledPackage right) => string.Equals(left.Version, right.Version, StringComparison.Ordinal) &&
        JsonSerializer.Serialize(MapState(new AssetVaultInstallationState([left])), JsonOptions) ==
        JsonSerializer.Serialize(MapState(new AssetVaultInstallationState([right])), JsonOptions);

    private string ResolveContained(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(
            _vaultRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(path);
        return path;
    }

    private string ResolveFixed(string relativePath)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
        {
            throw new InvalidDataException($"Unsafe Asset Vault path '{relativePath}'.");
        }

        var path = Path.GetFullPath(Path.Combine(
            _vaultRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(path);
        return path;
    }

    private void EnsureContained(string candidate)
    {
        var relative = Path.GetRelativePath(_vaultRoot, candidate);
        if (relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("Asset Vault path escapes the configured root.");
        }
    }

    private static string NormalizePackageId(string packageId) =>
        AssetVaultInstalledPackage.NormalizeId(packageId);

    private static string ObjectRelativePath(string hash) =>
        $"packages/objects/sha256/{hash[..2]}/{hash}";

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void CopyAtomically(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: false);
            File.Move(temporaryPath, targetPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteAtomically(string targetPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:asset-vault-plan:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static AssetVaultPackageMutationResult Failure(
        AssetVaultPackageMutationPlan plan,
        AssetVaultInstallationState state,
        IReadOnlyList<DomainDiagnostic> diagnostics) => new(
        false,
        false,
        plan.PlanId,
        plan.Operation,
        plan.PackageId,
        state,
        0,
        0,
        0,
        0,
        0,
        null,
        null,
        AssetCreditCatalog.Aggregate(state),
        diagnostics);

    private static AssetVaultPackageMutationResult RestoreFailure(
        Guid planId,
        string packageId,
        AssetVaultInstallationState state,
        string code,
        string message) => new(
        false,
        false,
        planId,
        AssetVaultMutationOperation.Restore,
        packageId,
        state,
        0,
        0,
        0,
        0,
        0,
        null,
        null,
        AssetCreditCatalog.Aggregate(state),
        [new DomainDiagnostic(code, message, $"assets/packages/{packageId}/restore")]);

    private sealed record StateDocument(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        IReadOnlyList<PackageDocument> Packages);

    private sealed record PackageDocument(
        [property: JsonPropertyName("package_id")] string PackageId,
        string Version,
        IReadOnlyList<AssetDocument> Assets);

    private sealed record AssetDocument(
        [property: JsonPropertyName("asset_id")] string AssetId,
        [property: JsonPropertyName("external_id")] string ExternalId,
        [property: JsonPropertyName("display_name")] string DisplayName,
        string Source,
        [property: JsonPropertyName("source_page")] string SourcePage,
        string Author,
        [property: JsonPropertyName("license_spdx")] string LicenseSpdx,
        [property: JsonPropertyName("license_url")] string LicenseUrl,
        [property: JsonPropertyName("attribution_required")] bool AttributionRequired,
        [property: JsonPropertyName("attribution_text")] string AttributionText,
        IReadOnlyList<FileDocument> Files);

    private sealed record FileDocument(
        string Role,
        [property: JsonPropertyName("content_sha256")] string ContentSha256,
        long Bytes,
        [property: JsonPropertyName("source_relative_path")] string SourceRelativePath,
        [property: JsonPropertyName("original_file_name")] string OriginalFileName);

    private sealed record ReceiptDocument(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("plan_id")] Guid PlanId,
        string Operation,
        [property: JsonPropertyName("package_id")] string PackageId,
        [property: JsonPropertyName("base_revision")] string BaseRevision,
        [property: JsonPropertyName("result_revision")] string ResultRevision,
        [property: JsonPropertyName("written_objects")] int WrittenObjects,
        [property: JsonPropertyName("reused_objects")] int ReusedObjects,
        [property: JsonPropertyName("deleted_objects")] int DeletedObjects,
        [property: JsonPropertyName("new_bytes")] long NewBytes,
        [property: JsonPropertyName("reused_bytes")] long ReusedBytes);
}
