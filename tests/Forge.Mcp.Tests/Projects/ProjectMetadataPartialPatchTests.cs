using Forge.Domain.Modules;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class ProjectMetadataPartialPatchTests
{
    [Fact]
    public void MetadataPlanReportsBudgetsPersistsPreservesOmittedFieldsAndReverts()
    {
        using var workspace = new TestWorkspace();
        var before = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var resource = workspace.Project.GetProjectFoundation();
        var beforeLocalization = before.Module.Metadata.GetDefaultLocalization();
        var longSummary = string.Join(' ', Enumerable.Repeat("résumé", 15));
        var request = new ProjectFoundationPlanRequest(
            2,
            resource.Revision,
            MetadataPatch: new([], Localizations:
            [
                new(before.Module.Metadata.DefaultLocale,
                    [ProjectLocalizedMetadataPatchField.Summary], Summary: longSummary),
            ]));

        var planned = workspace.Project.PlanProjectFoundation(request);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(resource.Revision, workspace.Project.GetCurrentRevision());
        Assert.Equal([$"module/metadata/localizations/{before.Module.Metadata.DefaultLocale}/summary"],
            planned.ChangedPaths);
        var review = Assert.Single(planned.LocalizationReview,
            item => item.MessageKey == "module.summary" && item.Locale == before.Module.Metadata.DefaultLocale);
        Assert.Equal(96, review.CharacterBudget);
        Assert.True(review.OverflowRisk);
        Assert.NotEmpty(review.PseudoText);
        Assert.True(review.PseudoEstimatedCharacters > longSummary.Length);

        var stale = workspace.Project.PlanProjectFoundation(request with { BaseRevision = resource.Revision + 1 });
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");

        var applied = workspace.Project.ApplyProjectFoundationPlan(planned.PlanId!);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var localized = persisted.Module.Metadata.GetDefaultLocalization();
        Assert.Equal(longSummary, localized.Summary);
        Assert.Equal(beforeLocalization.Name, localized.Name);
        Assert.Equal(beforeLocalization.Description, localized.Description);
        Assert.Equal(beforeLocalization.Credits, localized.Credits);
        Assert.Equal(beforeLocalization.Licenses, localized.Licenses);
        Assert.Equal(beforeLocalization.Compatibility, localized.Compatibility);
        var reverted = workspace.Project.RevertProjectFoundationPlan(planned.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.Equal(ProjectJsonSerializer.Serialize(before), ProjectJsonSerializer.Serialize(restored));
    }
}
