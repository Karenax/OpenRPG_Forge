using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Packages;

namespace Forge.ModuleFormat.Tests.Packages;

public sealed class ModuleManifestValidatorTests
{
    [Fact]
    public void Validate_AcceptsVersionedManifestSchema()
    {
        var manifest = CreateManifest();

        Assert.Empty(ModuleManifestValidator.Validate(manifest));
        var restored = ModuleManifestJson.Deserialize(ModuleManifestJson.Serialize(manifest));
        Assert.Equal(manifest.ModuleId, restored.ModuleId);
        Assert.Equal(manifest.EntryAreaId, restored.EntryAreaId);
        Assert.Equal(manifest.Slug, restored.Slug);
        Assert.Equal(manifest.Dependencies, restored.Dependencies);
        Assert.Equal(ModuleTargetKind.Editor, restored.Target);
        Assert.Empty(restored.ThirdPartyDependencies);
        Assert.Equal(DistributionNoticeBuilder.PackagePath, restored.LicenseNoticePath);
        Assert.Equal(manifest.ContentSha256, restored.ContentSha256);
    }

    [Fact]
    public void Validate_ReportsInvalidSchemaVersionSlugHashAndDependencies()
    {
        var invalid = CreateManifest() with
        {
            FormatVersion = "99",
            Slug = "Bad Slug",
            ModuleVersion = "latest",
            ContentSha256 = "not-a-hash",
            Dependencies = ["base", "base"],
        };

        var diagnostics = ModuleManifestValidator.Validate(invalid);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == ModuleManifestValidator.InvalidFormatCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == ModuleManifestValidator.InvalidSlugCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == ModuleManifestValidator.InvalidVersionCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == ModuleManifestValidator.InvalidHashCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == ModuleManifestValidator.DuplicateDependencyCode);
    }

    private static ModuleManifest CreateManifest() => new(
        ModuleManifest.CurrentFormatVersion,
        ModuleId.From(Guid.Parse("30000000-0000-4000-8000-000000000001")),
        "crypt-of-ashes",
        "0.1.0",
        "0.1.0",
        "1.0.0",
        AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
        ["core-rules@1.0.0"],
        ModuleTargetKind.Editor,
        [],
        DistributionNoticeBuilder.PackagePath,
        new string('a', 64));
}
