using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Modules;

public sealed class ModuleReleaseConfigurationTests
{
    [Fact]
    public void Default_IsStableForSameDistributionAndWindowsReady()
    {
        var distributionId = ModuleDistributionConfigurationId.From(
            Guid.Parse("d1100000-0000-4000-8000-000000000001"));

        var first = ModuleReleaseConfiguration.CreateDefault(distributionId);
        var second = ModuleReleaseConfiguration.CreateDefault(distributionId);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(ModuleReleaseChannel.Development, first.Channel);
        Assert.Equal([ModuleDistributionPlatform.WindowsX64], first.Platforms);
        Assert.Equal(ModuleSigningMode.Optional, first.Signing.Mode);
        Assert.True(first.Signing.AllowUnsignedDevelopment);
        Assert.True(first.Delta.Enabled);
        Assert.True(first.Delta.AllowResume);
        Assert.True(first.Delta.DeduplicateByHash);
        Assert.True(first.Delta.AllowRollback);
        Assert.Equal(WindowsPackageMode.PortableAndInstaller, first.WindowsPackaging.Mode);
        Assert.True(first.WindowsPackaging.DetectPrerequisites);
        Assert.True(first.WindowsPackaging.WriteInstallLog);
        Assert.True(first.WindowsPackaging.IncludeUninstaller);
    }

    [Fact]
    public void Signing_RejectsRequiredPolicyWithoutTrustAndConflictingRevocation()
    {
        Assert.Throws<ArgumentException>(() => new ModuleSigningConfiguration(
            ModuleSigningMode.Required,
            "shipping",
            [],
            [],
            allowUnsignedDevelopment: false));

        var fingerprint = new string('a', 64);
        Assert.Throws<ArgumentException>(() => new ModuleSigningConfiguration(
            ModuleSigningMode.Required,
            "shipping",
            [fingerprint],
            [fingerprint],
            allowUnsignedDevelopment: false));
    }

    [Fact]
    public void Delta_RequiresBoundedPowerOfTwoChunksAndRetention()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleDeltaConfiguration(
            true,
            100_000,
            true,
            true,
            true,
            2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleDeltaConfiguration(
            true,
            1024 * 1024,
            true,
            true,
            true,
            0));
    }
}
