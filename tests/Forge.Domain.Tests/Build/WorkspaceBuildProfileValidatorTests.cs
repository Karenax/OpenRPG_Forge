using Forge.Domain.Build;

namespace Forge.Domain.Tests.Build;

public sealed class WorkspaceBuildProfileValidatorTests
{
    [Fact]
    public void DefaultProfileSatisfiesCanonicalRepositoryLayoutCliDeterminismAndIncrementalContracts()
    {
        Assert.Empty(WorkspaceBuildProfileValidator.Validate(WorkspaceBuildProfile.CreateDefault()));
    }

    [Fact]
    public void EveryInvalidContractReturnsAStableLocalizedDiagnostic()
    {
        var source = WorkspaceBuildProfile.CreateDefault();
        var invalid = new WorkspaceBuildProfile(
            Guid.Empty,
            99,
            string.Empty,
            false,
            false,
            false,
            false,
            new Dictionary<string, string> { ["cache"] = "../outside" },
            ["build", "build"],
            false,
            false,
            false,
            false,
            false,
            false,
            "../outside",
            false);

        var diagnostics = WorkspaceBuildProfileValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.SchemaUnsupportedCode && item.Path == "build/schemaVersion");
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.IdentityInvalidCode && item.Path == "build/id");
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.ProvenanceMissingCode && item.Path == "build/provenance");
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.RepositoryPolicyInvalidCode && item.Path == "build/repository");
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.LayoutInvalidCode && item.Path == "build/layout");
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.CliContractInvalidCode && item.Path == "build/cliOperations");
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.DeterminismInvalidCode && item.Path == "build/determinism");
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildProfileValidator.IncrementalInvalidCode && item.Path == "build/incremental");
        Assert.NotEqual(source.Id, invalid.Id);
    }

    [Fact]
    public void GovernanceDefaultsCoverFiveP0FamiliesAndInvalidSectionsAreLocalized()
    {
        var valid = WorkspaceBuildProfile.CreateDefault().Governance;
        Assert.Empty(WorkspaceBuildGovernanceValidator.Validate(valid));
        Assert.Equal(Enum.GetValues<ReferenceFixtureKind>().Length,
            valid.ReferenceFixtures.Fixtures.Select(item => item.Kind).Distinct().Count());

        var invalid = new WorkspaceBuildGovernanceProfile(
            Guid.Empty,
            99,
            string.Empty,
            new(false, false, false, string.Empty, "../owner", []),
            new("../ci", false, false, false, false, false, false, false),
            new("../fixtures", []),
            new("dev", new Dictionary<string, string>(), "latest", new Dictionary<string, string>(), [], "../changes"),
            new("../adr", "../template", [], false, false, false));

        var diagnostics = WorkspaceBuildGovernanceValidator.Validate(invalid);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceValidator.SchemaUnsupportedCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceValidator.IdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceValidator.WorktreeInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceValidator.CiInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceValidator.FixturesInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceValidator.VersioningInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceValidator.AdrInvalidCode);
        Assert.All(diagnostics, item => Assert.StartsWith("build/governance/", item.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void CollaborationDefaultsCoverAllR023ContractsAndUnsafePoliciesAreLocalized()
    {
        var valid = WorkspaceBuildCollaborationProfile.CreateDefault();
        Assert.Empty(WorkspaceBuildCollaborationProfileValidator.Validate(valid));
        Assert.True(valid.Cache.ContentAddressed);
        Assert.True(valid.Normalization.StableNumericFormatting);
        Assert.True(valid.Migrations.RequireIdempotence);
        Assert.True(valid.References.IncludeReverseReferences);
        Assert.True(valid.SemanticDiff.Areas && valid.SemanticDiff.Graphs && valid.SemanticDiff.Entities &&
                    valid.SemanticDiff.Packages && valid.SemanticDiff.Scripts);

        var invalid = valid with
        {
            Id = Guid.Empty,
            SchemaVersion = 99,
            Provenance = string.Empty,
            Cache = valid.Cache with { NeverCanonical = false },
            Normalization = valid.Normalization with { LineFeedEndings = false },
            Migrations = valid.Migrations with { BackupDirectory = "../outside" },
            References = valid.References with { IncludeReverseReferences = false },
            SemanticDiff = valid.SemanticDiff with { JsonOutput = false },
        };
        var codes = WorkspaceBuildCollaborationProfileValidator.Validate(invalid)
            .Select(item => item.Code).ToArray();
        Assert.Contains(WorkspaceBuildCollaborationProfileValidator.IdentityInvalidCode, codes);
        Assert.Contains(WorkspaceBuildCollaborationProfileValidator.SchemaUnsupportedCode, codes);
        Assert.Contains(WorkspaceBuildCollaborationProfileValidator.CacheInvalidCode, codes);
        Assert.Contains(WorkspaceBuildCollaborationProfileValidator.NormalizationInvalidCode, codes);
        Assert.Contains(WorkspaceBuildCollaborationProfileValidator.MigrationInvalidCode, codes);
        Assert.Contains(WorkspaceBuildCollaborationProfileValidator.ReferencesInvalidCode, codes);
        Assert.Contains(WorkspaceBuildCollaborationProfileValidator.SemanticDiffInvalidCode, codes);
    }

    [Fact]
    public void CompatibilityDefaultsAreVersionedAndInvalidValuesAreLocalized()
    {
        var valid = ProjectCompatibilityProfile.CreateDefault();
        Assert.Empty(ProjectCompatibilityProfileValidator.Validate(valid));
        Assert.True(valid.AutomaticMigrations);
        Assert.True(valid.RequiredAtRuntime);

        var invalid = valid with
        {
            Id = Guid.Empty,
            SchemaVersion = 99,
            Provenance = string.Empty,
            MinimumRuntimeVersion = "latest",
            ProjectFormatVersion = "dev",
            AutomaticMigrations = false,
        };
        var diagnostics = ProjectCompatibilityProfileValidator.Validate(invalid);
        Assert.Contains(diagnostics, item => item.Code == ProjectCompatibilityProfileValidator.SchemaUnsupportedCode &&
                                             item.Path == "build/compatibility/schemaVersion");
        Assert.Contains(diagnostics, item => item.Code == ProjectCompatibilityProfileValidator.IdentityInvalidCode);
        Assert.Equal(2, diagnostics.Count(item => item.Code == ProjectCompatibilityProfileValidator.VersionInvalidCode));
        Assert.Contains(diagnostics, item =>
            item.Code == ProjectCompatibilityProfileValidator.AutomaticMigrationRequiredCode);
    }
}
