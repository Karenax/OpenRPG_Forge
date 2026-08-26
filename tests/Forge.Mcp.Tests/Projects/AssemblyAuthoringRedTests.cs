using System.Security.Cryptography;
using System.Text;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Mcp.Assets;
using Forge.Mcp.Assistant;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class AssemblyAuthoringRedTests
{
    [Fact]
    public void LocalLogicalAssetCanBePlannedWithoutAPreexistingWorkspaceManifest()
    {
        using var fixture = new LocalLogicalAssetWorkspace();
        var initialBlueprintCount = ProjectSession.Open(fixture.WorkspaceRoot).Session!.Project.Blueprints.Count;
        var stale = fixture.Project.PlanAssetInstall(new AgentAssetInstallPlanRequest(
            1,
            99,
            AssetCatalogItemId.ForExternalId("test:medieval:market-stall").ToString()));

        var result = fixture.Project.PlanAssetInstall(new AgentAssetInstallPlanRequest(
            1,
            0,
            AssetCatalogItemId.ForExternalId("test:medieval:market-stall").ToString()));

        Assert.True(result.Succeeded,
            string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal("CC0-1.0", result.LicenseSpdx);
        Assert.Contains(stale.Diagnostics, item => item.Code == InstallAssetBlueprintHandler.RevisionConflictCode);
        Assert.False(File.Exists(Path.Combine(fixture.WorkspaceRoot, "assets", "asset-manifests.json")));

        var applied = fixture.Project.ApplyAssetInstallPlan(result.PlanId!);
        var manifest = Assert.Single(AssetPackageManifestStore.Load(fixture.WorkspaceRoot).Manifests);
        var reopened = ProjectSession.Open(fixture.WorkspaceRoot).Session!;

        Assert.True(applied.Succeeded,
            string.Join(" | ", applied.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(1, fixture.Project.GetCurrentRevision());
        Assert.Equal(3, manifest.Chunks.Count);
        Assert.All(manifest.Chunks, chunk => Assert.True(File.Exists(Path.Combine(
            fixture.WorkspaceRoot, chunk.RelativePath.Replace('/', Path.DirectorySeparatorChar)))));
        Assert.Equal(initialBlueprintCount + 1, reopened.Project.Blueprints.Count);
        Assert.Contains(reopened.Project.Blueprints, blueprint =>
            AssetBlueprintFactory.TryGetAssetId(blueprint, out var id) && id == manifest.AssetId);
        var repeated = fixture.Project.PlanAssetInstall(new AgentAssetInstallPlanRequest(
            1, fixture.Project.GetCurrentRevision(), manifest.AssetId.ToString()));
        Assert.True(repeated.Succeeded,
            string.Join(" | ", repeated.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.True(repeated.Reused);
        Assert.Equal(initialBlueprintCount + 1,
            ProjectSession.Open(fixture.WorkspaceRoot).Session!.Project.Blueprints.Count);

        var reverted = fixture.Project.RevertAssetInstallPlan(result.PlanId!);
        var revertedProject = ProjectSession.Open(fixture.WorkspaceRoot).Session!;

        Assert.True(reverted.Succeeded,
            string.Join(" | ", reverted.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.False(File.Exists(Path.Combine(fixture.WorkspaceRoot, "assets", "asset-manifests.json")));
        Assert.All(manifest.Chunks, chunk => Assert.False(File.Exists(Path.Combine(
            fixture.WorkspaceRoot, chunk.RelativePath.Replace('/', Path.DirectorySeparatorChar)))));
        Assert.Equal(initialBlueprintCount, revertedProject.Project.Blueprints.Count);
    }

    [Fact]
    public void AdvertisedAreaAddEntityRunsThroughTheGenericTransaction()
    {
        using var workspace = new TestWorkspace();
        var summary = workspace.Project.GetSummary();
        var areaId = Guid.Parse(summary.EntryAreaId);
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var blueprintId = project.Blueprints.First().Id.Value;
        var transactionId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var placement = new AgentAreaEntityPlacement(
            AgentAreaEntityPlacement.CurrentSchemaVersion,
            areaId,
            entityId,
            blueprintId,
            new TransformValue(
                new Vector3Value(3.5f, 1.25f, -7),
                new QuaternionValue(0, 0.70710677f, 0, 0.70710677f),
                new Vector3Value(1.5f, 2, .75f)),
            ["market", "interactive"],
            new EntityPropertyOverrides(1,
                [new EntityPropertyOverride("state.locked", EntityPropertyValueKind.Boolean, "false")]));
        var plan = new StructuredChangePlan(
            Guid.NewGuid(), transactionId, StructuredChangePlan.CurrentSchemaVersion,
            "test:generic-assembly-red", 0, AssistantPermissionLevel.ApplyApprovedPlan,
            "Place one governed blueprint with a complete transform",
            [new AgentSemanticCommand(Guid.NewGuid(), "area.add_entity", areaId,
                new Dictionary<string, string>(StringComparer.Ordinal),
                placement)],
            ["The area and blueprint were selected from canonical context."],
            [areaId],
            ["Placement can collide with existing content."],
            ["dry-run", "save-reopen", "rollback"]);
        var begun = workspace.Project.BeginAgentChangeTransaction(new AgentChangeTransactionBeginRequest(
            1, plan, "codex-test", "author:test", null, null, false, true, 32, 16, 0, 1));

        var preview = workspace.Project.PreviewAgentChangeTransaction(transactionId.ToString("D"));

        Assert.True(begun.Succeeded,
            string.Join(" | ", begun.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.True(preview.Succeeded,
            string.Join(" | ", preview.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(0, workspace.Project.GetCurrentRevision());

        var committed = workspace.Project.CommitAgentChangeTransaction(transactionId.ToString("D"));
        var reopened = ProjectSession.Open(workspace.RootDirectory).Session!;
        var entity = reopened.Project.Areas.Single(area => area.Id.Value == areaId)
            .Entities.Single(candidate => candidate.Id.Value == entityId);

        Assert.True(committed.Succeeded,
            string.Join(" | ", committed.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(new Forge.Domain.Spatial.Vector3Value(3.5f, 1.25f, -7f), entity.Transform.Position);
        Assert.Equal(new Forge.Domain.Spatial.QuaternionValue(0, 0.70710677f, 0, 0.70710677f), entity.Transform.Rotation);
        Assert.Equal(new Forge.Domain.Spatial.Vector3Value(1.5f, 2f, 0.75f), entity.Transform.Scale);
        Assert.Equal(["interactive", "market"], entity.Tags);
        Assert.Equal("false", Assert.Single(entity.Overrides.Properties).Value);

        var rolledBack = workspace.Project.RollbackAgentChangeTransaction(transactionId.ToString("D"));
        var afterRollback = ProjectSession.Open(workspace.RootDirectory).Session!;

        Assert.True(rolledBack.Succeeded,
            string.Join(" | ", rolledBack.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.DoesNotContain(afterRollback.Project.Areas.SelectMany(area => area.Entities),
            candidate => candidate.Id.Value == entityId);
    }

    private sealed class LocalLogicalAssetWorkspace : IDisposable
    {
        private readonly string _root;

        internal string WorkspaceRoot { get; }
        internal ForgeProjectAgentService Project { get; }

        internal LocalLogicalAssetWorkspace()
        {
            _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-local-logical-asset", Guid.NewGuid().ToString("N"));
            var repositoryRoot = Path.Combine(_root, "repository", "source");
            WorkspaceRoot = Path.Combine(repositoryRoot, "workspace");
            var vaultRoot = Path.Combine(_root, "OpenRPG_AssetVault");
            var catalogs = Path.Combine(repositoryRoot, "asset_manifests", "catalogs");
            Directory.CreateDirectory(catalogs);
            Directory.CreateDirectory(WorkspaceRoot);
            File.WriteAllText(Path.Combine(repositoryRoot, "AGENTS.md"), "# test repository\n");

            var sourceRepository = FindRepositoryRoot();
            var sample = Path.Combine(sourceRepository, "apps", "Forge.Runtime", "content", "crypt-of-ashes");
            File.Copy(Path.Combine(sample, "forge.project.json"), Path.Combine(WorkspaceRoot, "forge.project.json"));
            File.Copy(Path.Combine(sample, "project.json"), Path.Combine(WorkspaceRoot, "project.json"));
            Directory.CreateDirectory(Path.Combine(WorkspaceRoot, "areas"));
            foreach (var area in Directory.EnumerateFiles(Path.Combine(sample, "areas"), "*.json"))
                File.Copy(area, Path.Combine(WorkspaceRoot, "areas", Path.GetFileName(area)));

            var archivePath = Path.Combine(vaultRoot, "originals", "medieval.zip");
            var modelRoot = Path.Combine(vaultRoot, "normalized", "test", "medieval", "models");
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
            Directory.CreateDirectory(modelRoot);
            File.WriteAllBytes(archivePath, Encoding.UTF8.GetBytes("local archive fixture"));
            File.WriteAllBytes(Path.Combine(modelRoot, "market-stall.bin"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(modelRoot, "market-stall.png"), [0x89, 0x50, 0x4e, 0x47]);
            File.WriteAllText(Path.Combine(modelRoot, "market-stall.gltf"),
                "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"market-stall.bin\",\"byteLength\":4}],\"images\":[{\"uri\":\"market-stall.png\"}]}");
            var archiveHash = HashFile(archivePath);
            File.WriteAllText(Path.Combine(catalogs, "cc0_3d_all.json"), EmptyCatalog);
            File.WriteAllText(Path.Combine(catalogs, "kenney_3d_packs.json"), $$"""
                {
                  "schema_version": 1,
                  "catalog_date": "2026-08-26",
                  "license_policy": "cc0-only",
                  "packs": [
                    {
                      "pack_id": "test:medieval",
                      "schema_version": 1,
                      "name": "Medieval Test Pack",
                      "source": "test",
                      "source_page": "https://example.invalid/medieval",
                      "license": "CC0-1.0",
                      "license_url": "https://creativecommons.org/publicdomain/zero/1.0/",
                      "license_verified": true,
                      "status": "downloaded-and-extracted",
                      "total_bytes": 128,
                      "categories": ["Props"],
                      "tags": ["medieval", "market"],
                      "extracted_root": "normalized/test/medieval",
                      "model_files": ["models/market-stall.gltf"],
                      "files": [
                        { "path": "originals/medieval.zip", "sha256": "{{archiveHash}}" }
                      ]
                    }
                  ]
                }
                """);

            Project = ForgeProjectAgentService.Open(WorkspaceRoot).Service
                ?? throw new InvalidOperationException("The local logical asset fixture did not open.");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static string FindRepositoryRoot()
        {
            var configured = Environment.GetEnvironmentVariable("OPENRPG_FORGE_REPOSITORY_ROOT");
            if (!string.IsNullOrWhiteSpace(configured) &&
                File.Exists(Path.Combine(configured, "OpenRPGForge.sln")))
            {
                return Path.GetFullPath(configured);
            }

            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenRPGForge.sln")))
                    directory = directory.Parent;
                if (directory is not null) return directory.FullName;
            }
            throw new DirectoryNotFoundException();
        }

        private const string EmptyCatalog = """
            {
              "schema_version": 1,
              "catalog_date": "2026-08-26",
              "license_policy": "cc0-only",
              "assets": []
            }
            """;
    }
}
