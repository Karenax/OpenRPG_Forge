using System.Security.Cryptography;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Launcher;

public sealed class GameLauncherTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-launcher-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void DefaultDisplay_IsFullscreenAndLegacyWindowedModesAreBorderlessFullscreen()
    {
        var verification = CreateVerifiedLayout(GameLauncherMultiplayerMode.LocalHost);

        var settings = GameLauncherUserSettings.CreateDefault(verification.Manifest);

        Assert.Equal(LauncherWindowMode.Fullscreen, settings.WindowMode);
        Assert.False(LauncherWindowModePolicy.UsesBorderlessFullscreen(settings.WindowMode));
        Assert.True(LauncherWindowModePolicy.UsesBorderlessFullscreen(LauncherWindowMode.Windowed));
        Assert.True(LauncherWindowModePolicy.UsesBorderlessFullscreen(LauncherWindowMode.Borderless));
        Assert.Equal(
            LauncherWindowMode.Windowed,
            LauncherWindowModePolicy.Canonicalize(LauncherWindowMode.Borderless));
    }

    [Fact]
    public void SettingsAndLocalHostLaunchPlan_RoundTripWithoutSecrets()
    {
        var verification = CreateVerifiedLayout(GameLauncherMultiplayerMode.LocalHost);
        var settingsPath = Path.Combine(_temporaryRoot, "user", "launcher-settings.json");
        var settings = GameLauncherUserSettings.CreateDefault(verification.Manifest) with
        {
            ResolutionWidth = 2560,
            ResolutionHeight = 1440,
            SessionMode = LauncherSessionMode.LocalHost,
            PlayerName = "Elyra",
            ServerPort = 6121,
            ReduceMotion = true,
        };

        GameLauncherSettingsStore.Save(settingsPath, settings);
        var restored = GameLauncherSettingsStore.Load(settingsPath);
        var plan = GameLaunchPlanBuilder.Create(verification, restored, settingsPath);

        Assert.Equal(settings, restored);
        Assert.Equal(verification.RuntimePath, plan.ExecutablePath);
        Assert.Equal("--", plan.Arguments[0]);
        Assert.Contains("local-host", plan.Arguments);
        Assert.Contains("Elyra", plan.Arguments);
        Assert.Contains("6121", plan.Arguments);
        Assert.Contains("--reduce-motion", plan.Arguments);
        Assert.DoesNotContain(restored.ServerAddress, plan.Arguments);
    }

    [Fact]
    public void DirectConnect_CreatesAnInternetJoinLaunchPlan()
    {
        var verification = CreateVerifiedLayout(GameLauncherMultiplayerMode.DirectConnectPreview);
        var settingsPath = Path.Combine(_temporaryRoot, "direct", "launcher-settings.json");
        var settings = GameLauncherUserSettings.CreateDefault(verification.Manifest) with
        {
            SessionMode = LauncherSessionMode.DirectConnectPreview,
            ServerAddress = "game.example.test",
            NetworkJoinToken = "direct-connect-token-20260828",
            ServerCertificateSha256 = new string('a', 64),
        };
        GameLauncherSettingsStore.Save(settingsPath, settings);

        var plan = GameLaunchPlanBuilder.Create(verification, settings, settingsPath);

        Assert.Contains("internet-join", plan.Arguments);
        Assert.Contains(GameLaunchPlanBuilder.ServerAddressArgument, plan.Arguments);
        Assert.Contains("game.example.test", plan.Arguments);
        Assert.Contains("5121", plan.Arguments);
    }

    [Fact]
    public void InternetCapableLocalHost_CreatesAnInternetHostLaunchPlan()
    {
        var verification = CreateVerifiedLayout(GameLauncherMultiplayerMode.DirectConnectPreview);
        var settingsPath = Path.Combine(_temporaryRoot, "internet-host", "launcher-settings.json");
        var certificatePath = Path.Combine(_temporaryRoot, "internet-host", "internet-host-certificate.pfx");
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
        File.WriteAllBytes(certificatePath, [1, 2, 3]);
        var settings = GameLauncherUserSettings.CreateDefault(verification.Manifest) with
        {
            SessionMode = LauncherSessionMode.LocalHost,
            PlayerName = "Elyra",
            NetworkJoinToken = "internet-host-token-20260828",
            ServerCertificateSha256 = new string('b', 64),
            HostCertificatePath = certificatePath,
            HostCertificatePassword = "internet-host-password-20260828",
        };
        GameLauncherSettingsStore.Save(settingsPath, settings);

        var plan = GameLaunchPlanBuilder.Create(verification, settings, settingsPath);

        Assert.Contains("internet-host", plan.Arguments);
        Assert.DoesNotContain(GameLaunchPlanBuilder.ServerAddressArgument, plan.Arguments);
    }

    [Fact]
    public void BundleBuilder_CreatesAndVerifiesACompleteWindowsGameDistribution()
    {
        Directory.CreateDirectory(_temporaryRoot);
        var workspace = Path.Combine(_temporaryRoot, "workspace");
        var created = ProjectSession.Create(workspace, CreateDistributableProject());
        Assert.True(created.Succeeded, string.Join("; ", created.Diagnostics.Select(item => item.Message)));
        var branding = Path.Combine(workspace, "branding");
        Directory.CreateDirectory(branding);
        File.WriteAllBytes(Path.Combine(branding, "hero.png"), [0x89, 0x50, 0x4e, 0x47]);
        File.WriteAllBytes(Path.Combine(branding, "calm.wav"), [0x52, 0x49, 0x46, 0x46]);
        var package = Path.Combine(_temporaryRoot, "game.orpmod");
        ModulePackager.Export(workspace, package, target: ModuleTargetKind.Client);

        var template = Path.Combine(_temporaryRoot, "template");
        var runtime = Path.Combine(template, "runtime");
        Directory.CreateDirectory(runtime);
        File.WriteAllBytes(Path.Combine(template, GameLauncherBundleBuilder.TemplateExecutableName), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(runtime, "OpenRPG_Forge_Runtime.exe"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(runtime, "OpenRPG_Forge_Runtime.pck"), [7, 8, 9]);
        var destination = Path.Combine(_temporaryRoot, "Cendres-windows-x64");

        var bundle = GameLauncherBundleBuilder.Build(
            package,
            template,
            destination,
            created.Session!.Project.Module.Distribution.Launcher);
        var distribution = GameLauncherManifestStore.LoadAndVerifyDistribution(destination);

        Assert.Equal("Cendres.exe", bundle.Manifest.ExecutableFile);
        Assert.True(File.Exists(Path.Combine(destination, "Cendres.exe")));
        Assert.True(File.Exists(Path.Combine(destination, "branding", "launcher-background.png")));
        Assert.True(File.Exists(Path.Combine(destination, "branding", "launcher-music.wav")));
        Assert.Equal("branding/launcher-music.wav", bundle.Manifest.Music);
        Assert.True(File.Exists(Path.Combine(destination, "game", "main-module.orpmod")));
        Assert.Equal(bundle.Manifest.GameId, distribution.GameId);
        Assert.NotEmpty(distribution.Files);

        GameLauncherManifestStore.SaveDistribution(
            destination,
            distribution with
            {
                Files = distribution.Files
                    .Where(file => file.Path != GameLauncherBundleBuilder.RuntimePackageRelativePath)
                    .ToArray(),
            });
        Assert.Throws<InvalidDataException>(() =>
            GameLauncherManifestStore.LoadAndVerifyDistribution(destination));
        GameLauncherManifestStore.SaveDistribution(destination, distribution);

        File.WriteAllText(Path.Combine(destination, "runtime", "OpenRPG_Forge_Runtime.pck"), "tampered");
        Assert.Throws<InvalidDataException>(() =>
            GameLauncherManifestStore.LoadAndVerifyDistribution(destination));
    }

    [Fact]
    public void ExecutableSpecificManifestsCanCoexistWithoutCrossLoadingAnotherGame()
    {
        var root = Path.Combine(_temporaryRoot, "multi-game-layout");
        Directory.CreateDirectory(Path.Combine(root, "runtime"));
        Directory.CreateDirectory(Path.Combine(root, "game"));
        var launcher = Path.Combine(root, "Cendres.exe");
        var runtime = Path.Combine(root, "runtime", "OpenRPG_Forge_Runtime.exe");
        var runtimePackage = Path.Combine(root, "runtime", "OpenRPG_Forge_Runtime.pck");
        var module = Path.Combine(root, "game", "crypt-of-ashes-current.orpmod");
        var defaultLauncher = Path.Combine(root, "Solaren.exe");
        var defaultModule = Path.Combine(root, "game", "solaren.orpmod");
        File.WriteAllBytes(launcher, [1]);
        File.WriteAllBytes(defaultLauncher, [5]);
        File.WriteAllBytes(runtime, [2]);
        File.WriteAllBytes(runtimePackage, [3]);
        File.WriteAllBytes(module, [4]);
        File.WriteAllBytes(defaultModule, [6]);
        var manifest = new GameLauncherManifest(
            GameLauncherManifest.CurrentSchemaVersion,
            Guid.Parse("b9f40000-0000-4000-8000-000000000020"),
            "crypt-of-ashes",
            "La Crypte des Cendres",
            "Les braises vous attendent.",
            "0.8.0",
            "#C77A45",
            "Cendres.exe",
            "runtime/OpenRPG_Forge_Runtime.exe",
            "game/crypt-of-ashes-current.orpmod",
            Hash(launcher),
            Hash(runtime),
            Hash(module),
            null,
            null,
            null,
            true,
            GameLauncherMultiplayerMode.LocalHost,
            5121);
        var files = new[]
        {
            Entry(root, "Cendres.exe"),
            Entry(root, "runtime/OpenRPG_Forge_Runtime.exe"),
            Entry(root, "runtime/OpenRPG_Forge_Runtime.pck"),
            Entry(root, "game/crypt-of-ashes-current.orpmod"),
        };
        var distribution = new GameDistributionManifest(
            GameDistributionManifest.CurrentSchemaVersion,
            manifest.GameId,
            manifest.DisplayName,
            manifest.Version,
            "windows-x64",
            files);
        var fileSet = GameLauncherManifestStore.ForExecutable("Cendres.exe");
        GameLauncherManifestStore.Save(root, manifest, fileSet.ManifestFileName);
        GameLauncherManifestStore.SaveDistribution(root, distribution, fileSet.DistributionFileName);
        files = [.. files, Entry(root, fileSet.ManifestFileName)];
        GameLauncherManifestStore.SaveDistribution(root,
            distribution with { Files = files }, fileSet.DistributionFileName);

        var defaultManifest = manifest with
        {
            GameId = Guid.Parse("b9f40000-0000-4000-8000-000000000021"),
            Slug = "solaren",
            DisplayName = "Solaren",
            ExecutableFile = "Solaren.exe",
            ModulePackage = "game/solaren.orpmod",
            LauncherSha256 = Hash(defaultLauncher),
            ModuleSha256 = Hash(defaultModule),
        };
        GameLauncherManifestStore.Save(root, defaultManifest);
        var defaultFiles = new[]
        {
            Entry(root, "Solaren.exe"),
            Entry(root, "runtime/OpenRPG_Forge_Runtime.exe"),
            Entry(root, "runtime/OpenRPG_Forge_Runtime.pck"),
            Entry(root, "game/solaren.orpmod"),
            Entry(root, GameLauncherManifestStore.FileName),
        };
        GameLauncherManifestStore.SaveDistribution(root, distribution with
        {
            GameId = defaultManifest.GameId,
            Product = defaultManifest.DisplayName,
            Files = defaultFiles,
        });

        var selected = GameLauncherManifestStore.ResolveFileSet(root, "Cendres.exe");
        var verified = GameLauncherManifestStore.LoadAndVerifyDistribution(
            root, selected.ManifestFileName, selected.DistributionFileName);
        var defaultSelected = GameLauncherManifestStore.ResolveFileSet(root, "Solaren.exe");

        Assert.Equal(fileSet, selected);
        Assert.Equal(new GameLauncherManifestFileSet(
            GameLauncherManifestStore.FileName,
            GameLauncherManifestStore.DistributionFileName), defaultSelected);
        Assert.Equal(manifest.GameId, verified.GameId);
        Assert.Equal(manifest, GameLauncherManifestStore.Load(root, manifestFileName: selected.ManifestFileName));
        Assert.Throws<InvalidDataException>(() =>
            GameLauncherManifestStore.ResolveFileSet(root, "Renamed.exe"));
    }

    private GameLauncherVerification CreateVerifiedLayout(GameLauncherMultiplayerMode multiplayer)
    {
        var root = Path.Combine(_temporaryRoot, $"layout-{multiplayer}");
        Directory.CreateDirectory(Path.Combine(root, "runtime"));
        Directory.CreateDirectory(Path.Combine(root, "game"));
        var launcher = Path.Combine(root, "Cendres.exe");
        var runtime = Path.Combine(root, "runtime", "OpenRPG_Forge_Runtime.exe");
        var module = Path.Combine(root, "game", "cendres.orpmod");
        File.WriteAllBytes(launcher, [1]);
        File.WriteAllBytes(runtime, [2]);
        File.WriteAllBytes(module, [3]);
        var manifest = new GameLauncherManifest(
            GameLauncherManifest.CurrentSchemaVersion,
            Guid.Parse("b9f40000-0000-4000-8000-000000000010"),
            "cendres",
            "La Crypte",
            "Le feu attend sous la pierre.",
            "1.0.0",
            "#A36B42",
            "Cendres.exe",
            "runtime/OpenRPG_Forge_Runtime.exe",
            "game/cendres.orpmod",
            Hash(launcher),
            Hash(runtime),
            Hash(module),
            null,
            null,
            null,
            true,
            multiplayer,
            5121);
        GameLauncherManifestStore.Save(root, manifest);
        return GameLauncherManifestStore.Verify(root, manifest);
    }

    private static ProjectDefinition CreateDistributableProject()
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
            "test:game-launcher");
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
                "Forge test",
                "CC0-1.0",
                new string('a', 64)))).ToArray();
        var defaults = source.Module.Distribution;
        var launcher = new GameLauncherConfiguration(
            defaults.Launcher.Id,
            enabled: true,
            "La Crypte",
            "Le feu attend sous la pierre.",
            "Cendres",
            "#A36B42",
            "branding/hero.png",
            "https://example.com/news",
            "https://example.com/support",
            settingsEnabled: true,
            GameLauncherMultiplayerMode.LocalHost,
            5121,
            "test:game-launcher",
            musicPath: "branding/calm.wav");
        var distribution = new ModuleDistributionConfiguration(
            defaults.Id,
            defaults.TargetProfiles,
            [],
            "test:game-launcher",
            defaults.Release,
            launcher: launcher);
        var originalContent = source.Module.Content;
        var content = new ContentWorkspaceConfiguration(
            originalContent.Id,
            new ContentBatchConfiguration(
                originalContent.Batch.Glob,
                [.. originalContent.Batch.Extensions, ".wav"],
                originalContent.Batch.CollisionPolicy,
                originalContent.Batch.Provenance,
                originalContent.Batch.LicenseSpdx),
            originalContent.DataTables,
            originalContent.MergePolicy,
            originalContent.Provenance,
            packages: originalContent.Packages);
        var module = new ModuleDefinition(
            source.Module.Id,
            source.Module.Slug,
            source.Module.DisplayName,
            source.Module.Description,
            source.Module.EntryAreaId,
            approvedMetadata,
            source.Module.Variables,
            distribution,
            content: content);
        return new ProjectDefinition(
            source.Id,
            source.Title,
            module,
            blueprints,
            source.Areas,
            source.Dialogues,
            source.Quests);
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static GameDistributionFile Entry(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return new(relativePath, new FileInfo(path).Length, Hash(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, recursive: true);
    }
}
