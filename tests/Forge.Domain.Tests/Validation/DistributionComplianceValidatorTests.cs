using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Validation;

public sealed class DistributionComplianceValidatorTests
{
    [Fact]
    public void DefaultConfiguration_HasFiveStableSeparatedTargetProfiles()
    {
        var moduleId = ModuleId.From(Guid.Parse("d7000000-0000-4000-8000-000000000001"));

        var first = ModuleDistributionConfiguration.CreateDefault(moduleId);
        var second = ModuleDistributionConfiguration.CreateDefault(moduleId);

        Assert.Equal(5, first.TargetProfiles.Count);
        Assert.Equal(Enum.GetValues<ModuleTargetKind>(), first.TargetProfiles.Select(profile => profile.Target));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(
            first.TargetProfiles.Select(profile => profile.Id),
            second.TargetProfiles.Select(profile => profile.Id));
        Assert.Equal(5, first.TargetProfiles.Select(profile => profile.SecretsProfile).Distinct().Count());
        Assert.False(first.GetProfile(ModuleTargetKind.Editor).RequireApprovedLicenses);
        Assert.True(first.GetProfile(ModuleTargetKind.Shipping).RequireApprovedLicenses);
    }

    [Fact]
    public void Shipping_BlocksUnknownLicensesAndProvenanceWhileEditorRemainsUsable()
    {
        var project = CreateProject(compliant: false);

        var editor = DistributionComplianceValidator.Validate(project, ModuleTargetKind.Editor);
        var shipping = DistributionComplianceValidator.Validate(project, ModuleTargetKind.Shipping);

        Assert.Empty(editor);
        Assert.Contains(shipping, item => item.Code == DistributionComplianceValidator.ModuleLicenseUnknownCode);
        Assert.Contains(shipping, item => item.Code == DistributionComplianceValidator.ModuleProvenanceUnknownCode);
        Assert.Contains(shipping, item => item.Code == DistributionComplianceValidator.BlueprintLicenseUnknownCode);
        Assert.Contains(shipping, item => item.Code == DistributionComplianceValidator.BlueprintProvenanceUnknownCode);
    }

    [Fact]
    public void Shipping_AcceptsApprovedModuleContentAndDependencyRegistry()
    {
        var project = CreateProject(compliant: true);

        var diagnostics = DistributionComplianceValidator.Validate(project, ModuleTargetKind.Shipping);

        Assert.Empty(diagnostics);
        var dependency = Assert.Single(project.Module.Distribution.Dependencies);
        Assert.Equal("1.4.0", dependency.Version);
        Assert.Equal("MIT", dependency.LicenseSpdx);
        Assert.Equal("quarterly", dependency.UpdateCadence);
        Assert.False(string.IsNullOrWhiteSpace(dependency.RemovalPlan));
    }

    [Fact]
    public void DisabledTarget_IsRejectedBeforeBuild()
    {
        var project = CreateProject(compliant: true);
        var distribution = project.Module.Distribution;
        var profiles = distribution.TargetProfiles.Select(profile =>
            profile.Target == ModuleTargetKind.Shipping
                ? new ModuleTargetProfile(
                    profile.Id,
                    profile.Target,
                    enabled: false,
                    profile.IncludeEditorData,
                    profile.IncludeDebugSymbols,
                    profile.OptimizeContent,
                    profile.RequireApprovedLicenses,
                    profile.OutputName,
                    profile.SecretsProfile,
                    "test")
                : profile).ToArray();
        var candidate = ReplaceDistribution(
            project,
            new ModuleDistributionConfiguration(distribution.Id, profiles, distribution.Dependencies, "test"));

        var diagnostic = Assert.Single(
            DistributionComplianceValidator.Validate(candidate, ModuleTargetKind.Shipping));

        Assert.Equal(DistributionComplianceValidator.TargetDisabledCode, diagnostic.Code);
    }

    [Fact]
    public void StableShipping_RequiresSignatureDeltaAndWindowsSafety()
    {
        var project = CreateProject(compliant: true);
        var distribution = project.Module.Distribution;
        var release = distribution.Release;
        var unsafeStable = new ModuleReleaseConfiguration(
            release.Id,
            ModuleReleaseChannel.Stable,
            release.Platforms,
            release.Signing,
            new ModuleDeltaConfiguration(true, 1024 * 1024, false, true, false, 2),
            new WindowsPackagingConfiguration(
                WindowsPackageMode.PortableZip,
                detectPrerequisites: false,
                writeInstallLog: false,
                includeUninstaller: false,
                []),
            "test");
        var candidate = ReplaceDistribution(
            project,
            new ModuleDistributionConfiguration(
                distribution.Id,
                distribution.TargetProfiles,
                distribution.Dependencies,
                "test",
                unsafeStable));

        var diagnostics = DistributionComplianceValidator.Validate(candidate, ModuleTargetKind.Shipping);

        Assert.Contains(diagnostics, item => item.Code == DistributionComplianceValidator.StableSignatureRequiredCode);
        Assert.Contains(diagnostics, item => item.Code == DistributionComplianceValidator.StableDeltaPolicyRequiredCode);
        Assert.Contains(diagnostics, item => item.Code == DistributionComplianceValidator.WindowsPackagingSafetyRequiredCode);
    }

    private static ProjectDefinition CreateProject(bool compliant)
    {
        var moduleId = ModuleId.From(Guid.Parse("d7000000-0000-4000-8000-000000000002"));
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("d7000000-0000-4000-8000-000000000003")),
            "distribution-area",
            "Distribution area",
            []);
        var blueprintId = BlueprintId.From(Guid.Parse("d7000000-0000-4000-8000-000000000004"));
        var blueprint = compliant
            ? new BlueprintDefinition(
                blueprintId,
                "approved-content",
                "Approved content",
                BlueprintKind.Placeable,
                provenance: new BlueprintProvenance(
                    BlueprintProvenance.CurrentSchemaVersion,
                    "project://approved-content",
                    "Forge team",
                    "CC0-1.0",
                    new string('a', 64)))
            : new BlueprintDefinition(blueprintId, "unknown-content", "Unknown content", BlueprintKind.Placeable);
        var defaultDistribution = ModuleDistributionConfiguration.CreateDefault(moduleId);
        var dependencies = compliant
            ? new[]
            {
                new ThirdPartyDependencyApproval(
                    ThirdPartyDependencyId.From(Guid.Parse("d7000000-0000-4000-8000-000000000005")),
                    "Approved library",
                    "1.4.0",
                    "MIT",
                    "Forge team",
                    "Required deterministic codec.",
                    "quarterly",
                    "Replace with the internal codec.",
                    "https://example.invalid/approved-library",
                    [ModuleTargetKind.Shipping],
                    "test"),
            }
            : [];
        var distribution = new ModuleDistributionConfiguration(
            defaultDistribution.Id,
            defaultDistribution.TargetProfiles,
            dependencies,
            compliant ? "test" : "migration-default");
        var metadata = compliant
            ? new ProjectMetadata(
                ProjectMetadataId.From(Guid.Parse("d7000000-0000-4000-8000-000000000006")),
                "en",
                [new ProjectLocalizedMetadata("en", "Distribution", "Summary", "Description", "Forge team")],
                "Forge team",
                "1.0.0",
                "MIT",
                imagePath: null,
                tags: [],
                "General",
                "test")
            : null;
        var module = new ModuleDefinition(
            moduleId,
            "distribution-module",
            "Distribution module",
            "Distribution compliance fixture",
            area.Id,
            metadata,
            distribution: distribution);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("d7000000-0000-4000-8000-000000000007")),
            "Distribution project",
            module,
            [blueprint],
            [area]);
    }

    private static ProjectDefinition ReplaceDistribution(
        ProjectDefinition project,
        ModuleDistributionConfiguration distribution)
    {
        var module = project.Module;
        return new ProjectDefinition(
            project.Id,
            project.Title,
            new ModuleDefinition(
                module.Id,
                module.Slug,
                module.DisplayName,
                module.Description,
                module.EntryAreaId,
                module.Metadata,
                module.Variables,
                distribution),
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);
    }
}
