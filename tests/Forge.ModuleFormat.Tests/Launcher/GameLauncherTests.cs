using System.Security.Cryptography;
using Forge.Domain.Blueprints;
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
    public void DirectConnect_RemainsAnExplicitUnavailablePreview()
    {
        var verification = CreateVerifiedLayout(GameLauncherMultiplayerMode.DirectConnectPreview);
        var settingsPath = Path.Combine(_temporaryRoot, "direct", "launcher-settings.json");
        var settings = GameLauncherUserSettings.CreateDefault(verification.Manifest) with
        {
            SessionMode = LauncherSessionMode.DirectConnectPreview,
            ServerAddress = "game.example.test",
        };
        GameLauncherSettingsStore.Save(settingsPath, settings);

        var exception = Assert.Throws<NotSupportedException>(() =>
            GameLaunchPlanBuilder.Create(verification, settings, settingsPath));

        Assert.Contains("network transport", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            "test:game-launcher");
        var distribution = new ModuleDistributionConfiguration(
            defaults.Id,
            defaults.TargetProfiles,
            [],
            "test:game-launcher",
            defaults.Release,
            launcher: launcher);
        var module = new ModuleDefinition(
            source.Module.Id,
            source.Module.Slug,
            source.Module.DisplayName,
            source.Module.Description,
            source.Module.EntryAreaId,
            approvedMetadata,
            source.Module.Variables,
            distribution);
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

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, recursive: true);
    }
}
