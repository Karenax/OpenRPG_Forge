using Forge.Domain.Entities;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class EntityOverridesAuthoringTests
{
    [Fact]
    public void Existing_entity_overrides_plan_apply_reopen_and_revert_without_direct_json_edits()
    {
        using var workspace = new TestWorkspace();
        var opened = ProjectSession.Open(workspace.RootDirectory).Session!;
        var area = opened.Project.Areas.First();
        var entity = area.Entities.First();
        var original = entity.Overrides;
        var properties = original.Properties
            .Where(item => !string.Equals(item.Key, "visual.asset", StringComparison.Ordinal))
            .Concat([
                new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String,
                    "11111111-1111-4111-8111-111111111111"),
                new EntityPropertyOverride("visual.color", EntityPropertyValueKind.String, "#7a4b35"),
            ])
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        var replacement = new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties);

        var plan = workspace.Project.PlanEntityOverrides(new EntityOverridesPlanRequest(
            EntityOverridesPlanRequest.CurrentSchemaVersion,
            workspace.Project.GetCurrentRevision(),
            area.Id.ToString(),
            entity.Id.ToString(),
            replacement));

        Assert.True(plan.Succeeded,
            string.Join(" | ", plan.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.NotNull(plan.PlanId);
        Assert.NotNull(plan.Diff);
        Assert.Equal(original, ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas.First()
            .Entities.Single(item => item.Id == entity.Id).Overrides);

        var applied = workspace.Project.ApplyEntityOverridesPlan(plan.PlanId!);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas.First()
            .Entities.Single(item => item.Id == entity.Id);

        Assert.True(applied.Succeeded,
            string.Join(" | ", applied.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(replacement.SchemaVersion, persisted.Overrides.SchemaVersion);
        Assert.Equal(replacement.Properties, persisted.Overrides.Properties);
        Assert.Equal(1, workspace.Project.GetCurrentRevision());

        var reverted = workspace.Project.RevertEntityOverridesPlan(plan.PlanId!);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas.First()
            .Entities.Single(item => item.Id == entity.Id);

        Assert.True(reverted.Succeeded,
            string.Join(" | ", reverted.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(original.SchemaVersion, restored.Overrides.SchemaVersion);
        Assert.Equal(original.Properties, restored.Overrides.Properties);
    }
}
