using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Assistant;
using Forge.Application.Creator;
using Forge.Application.Assets;
using Forge.Application.Areas;
using Forge.Application.Blueprints;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Application.Quests;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.WorldFeatures;
using Forge.Application.Modules;
using Forge.Application.Localization;
using Forge.Application.Navigation;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Items;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Localization;
using Forge.Domain.Quests;
using Forge.Domain.Quality;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Mcp.Abilities;
using Forge.Mcp.Assistant;
using Forge.Mcp.Build;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Bosses;
using Forge.Mcp.Quests;
using Forge.Mcp.Assets;
using Forge.Mcp.Creatures;
using Forge.Mcp.Factions;
using Forge.Mcp.Items;
using Forge.Mcp.Navigation;
using Forge.Mcp.Quality;
using Forge.Mcp.Scripts;
using Forge.Mcp.WorldFeatures;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Domain.Doors;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Quality;
using Forge.Scripting;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Documentation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;

namespace Forge.Mcp.Projects;


public sealed partial class ForgeProjectAgentService
{
    public AgentAssetCatalogResult GetAssets(AgentAssetCatalogQuery? query = null)
    {
        query ??= new AgentAssetCatalogQuery();
        lock (_gate)
        {
            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(_session.RootDirectory) ??
                AssetCatalogLoader.FindRepositoryRoot(AppContext.BaseDirectory);
            if (repositoryRoot is null)
            {
                return new AgentAssetCatalogResult(
                    1,
                    0,
                    0,
                    [],
                    [],
                    [],
                    [new AgentDiagnostic(
                        AssetCatalogLoader.CatalogMissingCode,
                        "The OpenRPG Forge Asset Vault catalogs could not be located.",
                        "asset_manifests/catalogs")]);
            }

            var load = AssetCatalogLoader.Load(repositoryRoot);
            if (load.Catalog is null)
            {
                return new AgentAssetCatalogResult(
                    1,
                    0,
                    0,
                    [],
                    [],
                    [],
                    load.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }

            var browser = new AssetCatalogBrowser(load.Catalog);
            var items = browser.Search(new AssetCatalogQuery(
                query.Text,
                query.Source,
                query.Category,
                query.LicenseSpdx,
                ParseAssetAvailability(query.Availability),
                query.MaximumResults,
                query.Installed,
                query.Tag,
                query.MaximumWidth,
                query.MaximumHeight,
                query.MaximumDepth,
                query.Style,
                query.Skeleton,
                query.Material,
                query.MaximumTriangleCount,
                query.Author,
                query.Compatibility,
                query.Variant));
            return new AgentAssetCatalogResult(
                load.Catalog.SchemaVersion,
                load.CatalogEntryCount,
                load.Catalog.Items.Count(item => item.Availability != AssetAvailability.CatalogOnly),
                browser.Sources,
                browser.Categories,
                items.Select(item => new AgentAssetCatalogItem(
                    item.Id.ToString(),
                    item.SchemaVersion,
                    item.ExternalId,
                    item.DisplayName,
                    item.Source,
                    item.SourceAssetId,
                    item.SourcePage,
                    item.LicenseSpdx,
                    item.LicenseVerified,
                    item.ContentSha256,
                    item.TotalBytes,
                    item.Categories,
                    item.Tags,
                    item.CatalogStatus,
                    item.Availability.ToString(),
                    item.RelativeModelPath,
                    item.RelativePreviewPath,
                    item.ThumbnailUrl,
                    item.CanPlanImport,
                    item.Provenance,
                    item.Author,
                    item.AttributionRequired,
                    item.AttributionText,
                    item.Installed,
                    item.InstalledPackIds,
                    new AgentAssetTechnicalMetadata(
                        item.TechnicalMetadata.Dimensions?.Width,
                        item.TechnicalMetadata.Dimensions?.Height,
                        item.TechnicalMetadata.Dimensions?.Depth,
                        item.TechnicalMetadata.Style,
                        item.TechnicalMetadata.Skeleton,
                        item.TechnicalMetadata.Materials,
                        item.TechnicalMetadata.TriangleCount,
                        item.TechnicalMetadata.Variants,
                        item.TechnicalMetadata.Animations,
                        item.TechnicalMetadata.Compatibility))).ToArray(),
                load.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public AgentAssetInstallPlanResult PlanAssetInstall(AgentAssetInstallPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    "asset_install_plan.schema_unsupported", "Asset install plan schema must be 1.");

            AssetCatalogItemId assetId;
            try
            {
                assetId = AssetCatalogItemId.Parse(request.AssetId);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    "asset_install_plan.asset_invalid", exception.Message);
            }

            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(_session.RootDirectory) ??
                AssetCatalogLoader.FindRepositoryRoot(AppContext.BaseDirectory);
            if (repositoryRoot is null)
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    AssetCatalogLoader.CatalogMissingCode,
                    "The OpenRPG Forge Asset Vault catalogs could not be located.");
            var catalogLoad = AssetCatalogLoader.Load(repositoryRoot);
            if (catalogLoad.Catalog is null)
            {
                var diagnostic = catalogLoad.Diagnostics.FirstOrDefault();
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    diagnostic?.Code ?? AssetCatalogLoader.CatalogInvalidCode,
                    diagnostic?.Message ?? "The Asset Vault catalog is invalid.");
            }

            var item = catalogLoad.Catalog.Items.SingleOrDefault(candidate => candidate.Id == assetId);
            if (item is null)
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    "asset_install_plan.asset_missing", $"Catalog asset '{request.AssetId}' does not exist.");
            var manifestLoad = AssetPackageManifestStore.Load(_session.RootDirectory);
            if (!manifestLoad.Succeeded)
            {
                var diagnostic = manifestLoad.Diagnostics[0];
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    diagnostic.Code, diagnostic.Message);
            }

            var existingManifest = manifestLoad.Manifests.SingleOrDefault(candidate => candidate.AssetId == item.Id);
            LocalAssetPackageInstallPlan? localPackage = null;
            AssetPackageManifest manifest;
            if (existingManifest is not null)
            {
                manifest = existingManifest;
            }
            else
            {
                var localPlan = LocalAssetPackageInstaller.Plan(
                    repositoryRoot,
                    catalogLoad.VaultRoot,
                    _session.RootDirectory,
                    item);
                if (!localPlan.Succeeded)
                    return new AgentAssetInstallPlanResult(
                        false, null, request.BaseRevision, item.Id.ToString(), item.LicenseSpdx,
                        null, null,
                        localPlan.Diagnostics.Select(diagnostic =>
                            new AgentDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.Path)).ToArray(),
                        false, false, [], false);
                localPackage = localPlan.Plan!;
                manifest = localPackage.Manifest;
            }
            var command = new InstallAssetBlueprintCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                item,
                manifest,
                DryRun: true);
            var preview = _assetInstallHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new AgentAssetInstallPlanResult(
                    false, null, request.BaseRevision, item.Id.ToString(), item.LicenseSpdx,
                    preview.Patch, preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            if (existingManifest is not null)
            {
                var governedModel = GovernedAssetModelVerifier.Verify(_session.RootDirectory, existingManifest);
                if (!governedModel.Succeeded)
                    return new AgentAssetInstallPlanResult(
                        false, null, request.BaseRevision, item.Id.ToString(), item.LicenseSpdx,
                        preview.Patch, preview.Inverse,
                        [new AgentDiagnostic(governedModel.DiagnosticCode!, governedModel.DiagnosticMessage!)],
                        false, false, []);
            }

            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAssetInstallPlan(
                planId,
                command,
                preview.Patch!,
                preview.Inverse!,
                [AssetInstallAudit(planId, "planned", request.BaseRevision, true, item)],
                catalogLoad.VaultRoot,
                localPackage,
                manifestLoad.Manifests,
                manifestLoad.FileMissing);
            _assetInstallPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AgentAssetInstallPlanResult ApplyAssetInstallPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assetInstallPlans.TryGetValue(planId, out var plan))
                return AssetInstallFailure(_history.State.Revision.Value, null,
                    "asset_install_plan.missing", $"Asset install plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.already_applied", $"Asset install plan '{planId}' is already applied.");

            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _assetInstallHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(AssetInstallAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.Command.Item));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }

            LocalAssetPackageApplyResult? materialized = null;
            if (plan.LocalPackage is not null)
            {
                materialized = LocalAssetPackageInstaller.Apply(
                    plan.VaultRoot, _session.RootDirectory, plan.LocalPackage);
                if (!materialized.Succeeded)
                {
                    plan.Audit.Add(AssetInstallAudit(planId, "apply", _history.State.Revision.Value, false,
                        plan.Command.Item));
                    return ToResult(plan) with
                    {
                        Succeeded = false,
                        Diagnostics = materialized.Diagnostics.Select(item =>
                            new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                    };
                }

                plan.PackageExecution = materialized;
            }

            var manifests = plan.PreviousManifests
                .Where(candidate => candidate.AssetId != plan.Command.Item.Id)
                .Append(plan.Command.Manifest)
                .OrderBy(candidate => candidate.Id.ToString(), StringComparer.Ordinal)
                .ToArray();
            var manifestSaved = AssetPackageManifestStore.Save(_session.RootDirectory, manifests);
            if (!manifestSaved.Succeeded)
            {
                if (materialized is not null)
                {
                    LocalAssetPackageInstaller.Revert(_session.RootDirectory, materialized);
                    plan.PackageExecution = null;
                }
                plan.Audit.Add(AssetInstallAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.Command.Item));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = manifestSaved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            }

            var artifact = GovernedAssetModelVerifier.Verify(_session.RootDirectory, plan.Command.Manifest);
            if (!artifact.Succeeded)
            {
                RestoreAssetInstallMaterialization(plan);
                plan.Audit.Add(AssetInstallAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.Command.Item));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = [new AgentDiagnostic(artifact.DiagnosticCode!, artifact.DiagnosticMessage!)],
                };
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                RestoreAssetInstallMaterialization(plan);
                plan.Audit.Add(AssetInstallAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.Command.Item));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            }

            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(AssetInstallAudit(planId, "apply", plan.AppliedRevision.Value, true,
                plan.Command.Item));
            return ToResult(plan);
        }
    }

    public AgentAssetInstallPlanResult RevertAssetInstallPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assetInstallPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssetInstallFailure(_history.State.Revision.Value, null,
                    "asset_install_plan.not_applied", $"Asset install plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.save_failed", "The reverted project could not be saved.");
            }

            var restored = RestoreAssetInstallMaterialization(plan);
            if (restored.Count > 0)
            {
                _history.Redo();
                _session.Save(_history.State.Project);
                plan.Audit.Add(AssetInstallAudit(planId, "revert", _history.State.Revision.Value, false,
                    plan.Command.Item));
                return ToResult(plan) with { Succeeded = false, Diagnostics = restored };
            }

            plan.Reverted = true;
            plan.Audit.Add(AssetInstallAudit(planId, "revert", undone.State.Revision.Value, true,
                plan.Command.Item));
            return ToResult(plan);
        }
    }

}
