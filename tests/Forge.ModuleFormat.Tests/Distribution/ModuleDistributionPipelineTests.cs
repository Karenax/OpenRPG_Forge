using System.IO.Compression;
using System.Security.Cryptography;
using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Distribution;

public sealed class ModuleDistributionPipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"forge-distribution-{Guid.NewGuid():N}");

    public ModuleDistributionPipelineTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Signature_VerifiesTrustedKeyAndRejectsTamperedPackageAndRevocation()
    {
        var package = Path.Combine(_root, "module.orpmod");
        var keyPath = Path.Combine(_root, "signing-key.pem");
        var signaturePath = Path.Combine(_root, "module.signature.json");
        File.WriteAllBytes(package, "OpenRPG Forge signed module"u8.ToArray());
        using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem());
        }

        var fingerprint = ModulePackageSignatureService.GetPublicKeyFingerprint(keyPath);
        var trusted = new ModuleSigningConfiguration(
            ModuleSigningMode.Required,
            "shipping",
            [fingerprint],
            [],
            allowUnsignedDevelopment: false);

        var signature = ModulePackageSignatureService.Sign(package, keyPath, signaturePath);
        var verified = ModulePackageSignatureService.Verify(package, signaturePath, trusted);

        Assert.Equal(fingerprint, signature.KeyFingerprint);
        Assert.True(verified.IsValid);
        Assert.True(verified.IsTrusted);

        File.AppendAllText(package, "tampered");
        var tampered = ModulePackageSignatureService.Verify(package, signaturePath, trusted);
        Assert.False(tampered.IsValid);
        Assert.Equal(ModulePackageSignatureService.PackageHashMismatchCode, tampered.DiagnosticCode);

        File.WriteAllBytes(package, "OpenRPG Forge signed module"u8.ToArray());
        var revokedPolicy = new ModuleSigningConfiguration(
            ModuleSigningMode.Optional,
            "preview",
            [],
            [fingerprint],
            allowUnsignedDevelopment: true);
        var revoked = ModulePackageSignatureService.Verify(package, signaturePath, revokedPolicy);
        Assert.False(revoked.IsValid);
        Assert.True(revoked.IsRevoked);
        Assert.Equal(ModulePackageSignatureService.KeyRevokedCode, revoked.DiagnosticCode);
    }

    [Fact]
    public void Delta_ReusesChunksResumesRebuildAndRollsBack()
    {
        var basePackage = Path.Combine(_root, "base.orpmod");
        var targetPackage = Path.Combine(_root, "target.orpmod");
        var deltaPath = Path.Combine(_root, "update.orpdelta");
        var rebuiltPath = Path.Combine(_root, "rebuilt.orpmod");
        var rollbackPath = Path.Combine(_root, "rollback.orpmod");
        var cachePath = Path.Combine(_root, "resume-cache");
        var baseBytes = Enumerable.Repeat((byte)'A', 65_536)
            .Concat(Enumerable.Repeat((byte)'B', 65_536))
            .Concat(Enumerable.Repeat((byte)'C', 20_000))
            .ToArray();
        var targetBytes = Enumerable.Repeat((byte)'A', 65_536)
            .Concat(Enumerable.Repeat((byte)'X', 65_536))
            .Concat(Enumerable.Repeat((byte)'C', 20_000))
            .ToArray();
        File.WriteAllBytes(basePackage, baseBytes);
        File.WriteAllBytes(targetPackage, targetBytes);
        var configuration = new ModuleDeltaConfiguration(true, 65_536, true, true, true, 3);

        var result = ModuleDeltaPackageBuilder.Create(basePackage, targetPackage, deltaPath, configuration);
        var manifest = ModuleDeltaPackageBuilder.Apply(basePackage, deltaPath, rebuiltPath, cachePath);
        ModuleDeltaPackageBuilder.Rollback(basePackage, manifest, rollbackPath);

        Assert.Equal(1, result.IncludedChunks);
        Assert.Equal(2, result.ReusedChunks);
        Assert.Equal(targetBytes, File.ReadAllBytes(rebuiltPath));
        Assert.Equal(baseBytes, File.ReadAllBytes(rollbackPath));
        Assert.NotEmpty(Directory.GetFiles(cachePath, "*.bin"));
    }

    [Fact]
    public void WindowsBundle_IsDeterministicAndContainsGovernedInstallLifecycle()
    {
        var workspace = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(workspace);
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("d1100000-0000-4000-8000-000000000010")),
            "release-area",
            "Release area",
            []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("d1100000-0000-4000-8000-000000000011")),
            "Release project",
            [],
            [area]);
        File.WriteAllText(Path.Combine(workspace, "project.json"), ProjectJsonSerializer.Serialize(project));
        var package = Path.Combine(_root, "release.orpmod");
        ModulePackager.Export(workspace, package);
        var first = Path.Combine(_root, "release-first.zip");
        var second = Path.Combine(_root, "release-second.zip");
        var configuration = project.Module.Distribution.Release;

        var result = ModuleReleaseBundleBuilder.Build(
            package,
            first,
            ModuleTargetKind.Editor,
            ModuleDistributionPlatform.WindowsX64,
            configuration);
        ModuleReleaseBundleBuilder.Build(
            package,
            second,
            ModuleTargetKind.Editor,
            ModuleDistributionPlatform.WindowsX64,
            configuration);

        using var archive = ZipFile.OpenRead(first);
        Assert.NotNull(archive.GetEntry("release-manifest.json"));
        Assert.NotNull(archive.GetEntry("packages/release.orpmod"));
        Assert.NotNull(archive.GetEntry("install.ps1"));
        Assert.NotNull(archive.GetEntry("uninstall.ps1"));
        Assert.Equal(ModuleReleaseChannel.Development, result.Manifest.Channel);
        Assert.True(result.Manifest.PrerequisitesDetection);
        Assert.True(result.Manifest.InstallLog);
        Assert.True(result.Manifest.Uninstaller);
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(first)), SHA256.HashData(File.ReadAllBytes(second)));
    }

    [Fact]
    public void Windows_bundle_honors_portable_mode_and_installer_prerequisite_detection()
    {
        var workspace = Path.Combine(_root, "modes-workspace");
        Directory.CreateDirectory(workspace);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Modes", [],
            [new AreaDefinition(AreaId.From(Guid.NewGuid()), "area", "Area", [])]);
        File.WriteAllText(Path.Combine(workspace, "project.json"), ProjectJsonSerializer.Serialize(project));
        var package = Path.Combine(_root, "modes.orpmod");
        ModulePackager.Export(workspace, package);
        var release = project.Module.Distribution.Release;
        var portableConfiguration = new ModuleReleaseConfiguration(release.Id, release.Channel,
            release.Platforms, release.Signing, release.Delta,
            new WindowsPackagingConfiguration(WindowsPackageMode.PortableZip, true, false, false,
                ["windows-x64"]), "test:portable");
        var portable = Path.Combine(_root, "portable.zip");
        ModuleReleaseBundleBuilder.Build(package, portable, ModuleTargetKind.Editor,
            ModuleDistributionPlatform.WindowsX64, portableConfiguration);
        using (var archive = ZipFile.OpenRead(portable))
        {
            Assert.NotNull(archive.GetEntry("packages/modes.orpmod"));
            Assert.Null(archive.GetEntry("install.ps1"));
            Assert.Null(archive.GetEntry("uninstall.ps1"));
        }

        var installer = Path.Combine(_root, "installer.zip");
        ModuleReleaseBundleBuilder.Build(package, installer, ModuleTargetKind.Editor,
            ModuleDistributionPlatform.WindowsX64, release);
        using var installerArchive = ZipFile.OpenRead(installer);
        using var reader = new StreamReader(installerArchive.GetEntry("install.ps1")!.Open());
        var script = reader.ReadToEnd();
        Assert.Contains("OPENRPG_FORGE_WINDOWS_PREREQUISITES_PASS", script, StringComparison.Ordinal);
        Assert.Contains("[Environment]::Is64BitOperatingSystem", script, StringComparison.Ordinal);
        Assert.Contains("[Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[System.OperatingSystem]::IsWindows()", script, StringComparison.Ordinal);
        Assert.Contains("openrpg-forge-install.log", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ModuleDistributionPlatform.LinuxX64, "Linux", "x86_64")]
    [InlineData(ModuleDistributionPlatform.MacOSArm64, "Darwin", "arm64")]
    public void UnixBundles_AreDeterministicAndContainVerifiedLifecycle(
        ModuleDistributionPlatform platform, string kernel, string machine)
    {
        var workspace = Path.Combine(_root, $"unix-{platform}");
        Directory.CreateDirectory(workspace);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Unix", [],
            [new AreaDefinition(AreaId.From(Guid.NewGuid()), "area", "Area", [])]);
        File.WriteAllText(Path.Combine(workspace, "project.json"), ProjectJsonSerializer.Serialize(project));
        var package = Path.Combine(_root, $"{platform}.orpmod");
        ModulePackager.Export(workspace, package);
        var release = project.Module.Distribution.Release;
        var configuration = new ModuleReleaseConfiguration(release.Id, release.Channel,
            [platform], release.Signing, release.Delta, release.WindowsPackaging, "test:unix");
        var first = Path.Combine(_root, $"{platform}-first.zip");
        var second = Path.Combine(_root, $"{platform}-second.zip");

        var result = ModuleReleaseBundleBuilder.Build(package, first, ModuleTargetKind.Client, platform, configuration);
        ModuleReleaseBundleBuilder.Build(package, second, ModuleTargetKind.Client, platform, configuration);

        using var archive = ZipFile.OpenRead(first);
        using var reader = new StreamReader(archive.GetEntry("install.sh")!.Open());
        var script = reader.ReadToEnd();
        Assert.NotNull(archive.GetEntry("uninstall.sh"));
        Assert.Contains($"uname -s)\" = \"{kernel}", script, StringComparison.Ordinal);
        Assert.Contains($"uname -m)\" = \"{machine}", script, StringComparison.Ordinal);
        Assert.Contains(result.Manifest.PackageSha256, script, StringComparison.Ordinal);
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(first)), SHA256.HashData(File.ReadAllBytes(second)));
    }

    [Fact]
    public void ReleasePromotion_RequiresSequentialChannelAndCompleteCompatibilityMatrix()
    {
        var releaseId = ModuleReleaseConfigurationId.From(Guid.NewGuid());
        var targets = Enum.GetValues<ModuleTargetKind>();
        var platforms = Enum.GetValues<ModuleDistributionPlatform>();
        var evidence = (from target in targets
                        from platform in platforms
                        select new ModuleCompatibilityEvidence("1.0.0", "1.1.0", target, platform,
                            true, true, true, new string('a', 64))).ToArray();
        var valid = new ModuleReleasePromotionRequest(releaseId, ModuleReleaseChannel.Preview,
            ModuleReleaseChannel.Stable, targets, platforms, true, true, evidence);

        var accepted = ModuleReleasePromotionService.Preview(valid);
        var incomplete = ModuleReleasePromotionService.Preview(valid with
        {
            CompatibilityEvidence = evidence.Skip(1).ToArray(),
        });
        var unsigned = ModuleReleasePromotionService.Preview(valid with { SignatureTrusted = false });

        Assert.True(accepted.Allowed);
        Assert.Equal("ORF-BUILD-024-PROMOTION-READY", accepted.DiagnosticCode);
        Assert.False(incomplete.Allowed);
        Assert.Single(incomplete.MissingMatrixEntries);
        Assert.Equal("ORF-BUILD-024-SIGNATURE", unsigned.DiagnosticCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
