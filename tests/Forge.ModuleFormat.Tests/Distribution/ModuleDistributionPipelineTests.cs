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

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
