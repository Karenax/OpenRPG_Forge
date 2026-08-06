using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Packages;

public sealed class ModulePackageTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExportValidateAndLoad_PreservesCanonicalCreatureLootLifecycle()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var loot = new CreatureLootProfile(
            Guid.Parse("a1410000-0000-4000-8000-000000000001"), 1, "author:test", true,
            [new(Guid.Parse("a1410000-0000-4000-8000-000000000002"), "warden-ember", "relic", 1, 1, 1,
                true, false, LootOwnershipMode.Party, LootInstancingMode.PerParty, null)],
            new(false, "stealth", "awareness", 10, 10, true, "crime.theft", 30, "loot.pickpocket"),
            new(CreatureCorpseMode.RagdollContainer, 60, "corpse.standard", "ragdoll.humanoid", true,
                "party", 8, CorpseCleanupMode.Timed), new Dictionary<string, string>());
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, loot);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"loot-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "loot.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "loot-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreatureLootProfile>(loaded.Project.Blueprints.Single().CreatureLoot);
        Assert.Equal(loot.Id, restored.Id);
        Assert.Equal(CreatureCorpseMode.RagdollContainer, restored.Corpse.Mode);
        Assert.Equal("warden-ember", Assert.Single(restored.Entries).ItemSlug);
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesCanonicalCreatureProgression()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var progression = new CreatureProgressionProfile(
            Guid.Parse("a1350000-0000-4000-8000-000000000001"), 1, "author:test", "forge.generic", true, true,
            [new("warden", "Gardien", 3, 20, 2_400, ["ember-mastery"])],
            [new("mercy", "Impitoyable", "Miséricordieux", 0.5)],
            [new("lore", "Savoir", "insight", 2, 10, 2, true, false, 12)],
            [new("ember-mastery", "Maîtrise des braises", 1, 3, true,
                [new("track", "warden", 2)], ["ember-strike"], ["combat.on-hit"])],
            new("prepared", AbilityLoadoutMode.Prepared, 2, ["ember-strike"],
                [new("ember-strike", 1, 2, 0.5, "focus", 1, ["empowered"])]),
            new Dictionary<string, string> { ["xp-rate"] = "1.0" });
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, progression);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"progression-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "progression.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "progression-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreatureProgressionProfile>(
            loaded.Project.Blueprints.Single().CreatureProgression);
        Assert.Equal(progression.Id, restored.Id);
        Assert.Equal("ember-strike", Assert.Single(restored.Loadout.PreparedAbilities).AbilityId);
    }

    [Fact]
    public void ExportValidateAndLoad_PreservesGovernedCreaturePresentation()
    {
        var source = TestProjectFactory.CreateValid();
        var portraitBytes = Encoding.UTF8.GetBytes("deterministic portrait fixture");
        var portraitHash = Convert.ToHexString(SHA256.HashData(portraitBytes)).ToLowerInvariant();
        var original = source.Blueprints.Single();
        var presentation = new CreaturePresentationProfile(
            Guid.Parse("a1330000-0000-4000-8000-000000000001"), 1, "author:test", "nonbinary", "humanoid-medium",
            new("iel", "iel", "son", "soi"),
            [new("neutral", "assets/portraits/warden.png", 256, 256, "CC0-1.0", portraitHash, "Gardien")],
            false, "rig.humanoid", [], [new("primary", "#7A5CFF")],
            [new("back", "wings", "asset.wings", "rig.humanoid")],
            new("voice.warden", "fr-FR", ["audio.warden"], ["calm"], "lipsync.fr-v1", ["a"], "CC0-1.0", "author-recorded"));
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance, presentation);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"presentation-{Guid.NewGuid():N}");
        var created = ProjectSession.Create(workspace, project);
        Assert.True(created.Succeeded, string.Join("; ", created.Diagnostics.Select(item => item.Message)));
        var portraitPath = Path.Combine(workspace, "assets", "portraits", "warden.png");
        Directory.CreateDirectory(Path.GetDirectoryName(portraitPath)!);
        File.WriteAllBytes(portraitPath, portraitBytes);
        var package = Path.Combine(_temporaryRoot, "presentation.orpmod");

        ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "presentation-cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        var restored = Assert.IsType<CreaturePresentationProfile>(loaded.Project.Blueprints.Single().CreaturePresentation);
        Assert.Equal(presentation.Id, restored.Id);
        Assert.True(File.Exists(Path.Combine(loaded.CachePath, "content", "assets", "portraits", "warden.png")));
    }

    [Fact]
    public void Export_RejectsMissingCreaturePortraitBeforeWritingPackage()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var profile = CreaturePresentationProfile.CreateDefault(
            Guid.Parse("a1330000-0000-4000-8000-000000000002"), "author:test");
        profile = new CreaturePresentationProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
            profile.GenderId, profile.BodyTypeId, profile.Pronouns,
            [new("neutral", "assets/portraits/missing.png", 256, 256, "CC0-1.0", new string('a', 64), "Missing")],
            false, profile.RigId, profile.BodySlots, profile.MaterialColors, profile.Attachments, profile.Voice);
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance, profile);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"missing-presentation-{Guid.NewGuid():N}");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var package = Path.Combine(_temporaryRoot, "missing-presentation.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() => ModulePackager.Export(workspace, package));

        Assert.Contains("missing or unsafe", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void ExportValidateAndLoad_RoundTripsAtomicallyThroughCache()
    {
        var workspace = CreateWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, ".forge"));
        File.WriteAllText(Path.Combine(workspace, ".forge", "editor-camera.json"), "author preference");
        var assetPath = Path.Combine(workspace, "assets", "imported", "test", "model.glb");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllBytes(assetPath, [1, 2, 3, 4]);
        var assetHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assetPath))).ToLowerInvariant();
        var assetId = AssetCatalogItemId.ForExternalId("test:packaged-model");
        var chunkId = Guid.Parse("b1100000-0000-4000-8000-000000000020");
        var assetManifest = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(assetId),
            assetId,
            "author:package-test",
            "CC0-1.0",
            new string('a', 64),
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Windows] = 1024 },
            [new AssetManifestChunk(
                chunkId,
                AssetChunkRole.Model,
                true,
                "assets/imported/test/model.glb",
                assetHash,
                4,
                4,
                AssetChunkCompression.None,
                [AssetTargetPlatform.Windows])],
            null,
            null,
            null,
            new AssetLodManifest(false, 0.1m, [new(0, chunkId, 1m, 1000)]));
        Assert.True(AssetPackageManifestStore.Save(workspace, [assetManifest]).Succeeded);
        var package = Path.Combine(_temporaryRoot, "crypt-of-ashes.orpmod");

        var export = ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var firstLoad = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "cache"));
        var secondLoad = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        Assert.Equal("crypt-of-ashes", validation.Manifest!.Slug);
        Assert.Equal("crypt-of-ashes", firstLoad.Project.Module.Slug);
        Assert.False(firstLoad.CacheHit);
        Assert.True(secondLoad.CacheHit);
        Assert.Equal(firstLoad.CachePath, secondLoad.CachePath);
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "project.json")));
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "scripts", "boss.forge")));
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "assets", "asset-manifests.json")));
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "assets", "imported", "test", "model.glb")));
        Assert.False(File.Exists(Path.Combine(firstLoad.CachePath, "content", ".forge", "editor-camera.json")));
        Assert.Equal(new FileInfo(package).Length, export.ArchiveBytes);
        Assert.Empty(Directory.EnumerateFiles(_temporaryRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Validate_RejectsPathTraversalFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "../escape.json", "{}");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.UnsafePathCode);
    }

    [Fact]
    public void Validate_RejectsZipBombRatioFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/assets/repetitive.txt", new string('0', 1024 * 1024));

        var validation = ModulePackageValidator.Validate(
            package,
            new ModulePackageLimits(MaximumCompressionRatio: 20));

        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.CompressionRatioCode);
    }

    [Fact]
    public void Validate_RejectsNativeDllFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/plugins/hostile.dll", "MZ-hostile");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.ForbiddenFileCode);
    }

    [Fact]
    public void Validate_RejectsHashMismatchFixture()
    {
        var package = ExportValidPackage();
        ReplaceEntry(package, "content/scripts/boss.forge", "void main() { say(\"tampered\"); }");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.HashMismatchCode);
    }

    [Fact]
    public void Validate_RejectsDuplicateBlueprintIdsFixture()
    {
        var package = ExportValidPackage();
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Update))
        {
            var projectEntry = archive.GetEntry("content/project.json")!;
            string json;
            using (var reader = new StreamReader(projectEntry.Open()))
            {
                json = reader.ReadToEnd();
            }

            projectEntry.Delete();
            json = json.Replace(
                "10000000-0000-4000-8000-000000000002",
                "10000000-0000-4000-8000-000000000001",
                StringComparison.Ordinal);
            var replacement = archive.CreateEntry("content/project.json", CompressionLevel.Optimal);
            using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            writer.Write(json);
        }

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.InvalidProjectCode);
    }

    [Fact]
    public void ShippingExport_BlocksUnknownLicensesBeforeWritingAnArchive()
    {
        var package = Path.Combine(_temporaryRoot, "blocked-shipping.orpmod");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModulePackager.Export(CreateWorkspace(), package, target: ModuleTargetKind.Shipping));

        Assert.Contains(DistributionComplianceValidator.ModuleLicenseUnknownCode, exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void ShippingExport_EmitsCanonicalNoticeAndApprovedDependencyManifest()
    {
        var workspace = CreateShippingWorkspace();
        var package = Path.Combine(_temporaryRoot, "approved-shipping.orpmod");

        var export = ModulePackager.Export(workspace, package, target: ModuleTargetKind.Shipping);
        var validation = ModulePackageValidator.Validate(package);
        using var archive = ZipFile.OpenRead(package);
        var noticeEntry = archive.GetEntry(DistributionNoticeBuilder.PackagePath);
        using var reader = new StreamReader(Assert.IsType<ZipArchiveEntry>(noticeEntry).Open());
        var notice = reader.ReadToEnd();

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        Assert.Equal(ModuleTargetKind.Shipping, export.Manifest.Target);
        Assert.Equal("Approved library", Assert.Single(export.Manifest.ThirdPartyDependencies).Name);
        Assert.Contains("Approved library 1.4.0 | MIT", notice, StringComparison.Ordinal);
        Assert.Contains("Update cadence: quarterly", notice, StringComparison.Ordinal);
        Assert.Contains("Removal plan:", notice, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, true);
        }
    }

    private string ExportValidPackage()
    {
        var package = Path.Combine(_temporaryRoot, $"{Guid.NewGuid():N}.orpmod");
        ModulePackager.Export(CreateWorkspace(), package);
        return package;
    }

    private string CreateWorkspace()
    {
        var workspace = Path.Combine(_temporaryRoot, $"workspace-{Guid.NewGuid():N}");
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        Directory.CreateDirectory(Path.Combine(workspace, "scripts"));
        Directory.CreateDirectory(Path.Combine(workspace, "areas"));
        File.Copy(Path.Combine(fixture, "forge.project.json"), Path.Combine(workspace, "forge.project.json"));
        File.Copy(Path.Combine(fixture, "project.json"), Path.Combine(workspace, "project.json"));
        foreach (var areaDocument in Directory.EnumerateFiles(Path.Combine(fixture, "areas"), "*.json"))
        {
            File.Copy(areaDocument, Path.Combine(workspace, "areas", Path.GetFileName(areaDocument)));
        }
        File.WriteAllText(
            Path.Combine(workspace, "scripts", "boss.forge"),
            "void main() { say(\"Braise\"); }",
            new UTF8Encoding(false));
        return workspace;
    }

    private string CreateShippingWorkspace()
    {
        var source = TestProjectFactory.CreateValid();
        var metadata = source.Module.Metadata;
        var approvedMetadata = new ProjectMetadata(
            metadata.Id,
            metadata.DefaultLocale,
            metadata.Localizations,
            metadata.Author,
            metadata.Version,
            "MIT",
            metadata.ImagePath,
            metadata.Tags,
            metadata.Classification,
            "test-approved");
        var blueprints = source.Blueprints.Select(blueprint => new BlueprintDefinition(
            blueprint.Id,
            blueprint.Slug,
            blueprint.DisplayName,
            blueprint.Kind,
            blueprint.Combat,
            blueprint.PaletteOrigin,
            blueprint.IsFavorite,
            new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                $"project://blueprints/{blueprint.Id}/{blueprint.Slug}",
                "Forge team",
                "CC0-1.0",
                new string('a', 64)))).ToArray();
        var defaults = source.Module.Distribution;
        var distribution = new ModuleDistributionConfiguration(
            defaults.Id,
            defaults.TargetProfiles,
            [new ThirdPartyDependencyApproval(
                ThirdPartyDependencyId.From(Guid.Parse("d7130000-0000-4000-8000-000000000011")),
                "Approved library",
                "1.4.0",
                "MIT",
                "Forge team",
                "Required deterministic codec.",
                "quarterly",
                "Replace with the internal codec.",
                "https://example.invalid/approved-library",
                [ModuleTargetKind.Shipping],
                "test")],
            "test");
        var module = new ModuleDefinition(
            source.Module.Id,
            source.Module.Slug,
            source.Module.DisplayName,
            source.Module.Description,
            source.Module.EntryAreaId,
            approvedMetadata,
            source.Module.Variables,
            distribution);
        var project = new ProjectDefinition(
            source.Id,
            source.Title,
            module,
            blueprints,
            source.Areas,
            source.Dialogues,
            source.Quests);
        var workspace = Path.Combine(_temporaryRoot, $"shipping-{Guid.NewGuid():N}");
        var created = ProjectSession.Create(workspace, project);
        Assert.True(created.Succeeded, string.Join("; ", created.Diagnostics.Select(item => item.Message)));
        return workspace;
    }

    private static void AddEntry(string package, string path, string content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update);
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void ReplaceEntry(string package, string path, string content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update);
        archive.GetEntry(path)!.Delete();
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string JoinDiagnostics(ModulePackageValidationResult validation) =>
        string.Join(Environment.NewLine, validation.Diagnostics.Select(diagnostic => diagnostic.Message));
}
