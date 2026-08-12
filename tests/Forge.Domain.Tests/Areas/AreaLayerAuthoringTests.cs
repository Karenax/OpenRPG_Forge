using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaLayerAuthoringTests
{
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("a0560000-0000-4000-8000-000000000001"));
    private static readonly EntityId EntityId = EntityId.From(Guid.Parse("a0560000-0000-4000-8000-000000000002"));

    [Fact]
    public void R056HierarchyInheritsVisibilityAndLockAndResolvesRuntimeHandle()
    {
        var root = Guid.Parse("a0560000-0000-4000-8000-000000000010");
        var child = Guid.Parse("a0560000-0000-4000-8000-000000000011");
        var area = Area([
            new(root, "Architecture", null, false, true, "builder", [], provenance: "test:r056"),
            new(child, "Secrets", root, true, false, "writer", [EntityId], provenance: "test:r056"),
        ], required: true);

        var hierarchy = AreaLayerHierarchy.Resolve(area.Authoring);
        var childState = hierarchy.Single(item => item.LayerId == child);
        var runtime = AreaLayerRuntimeResolver.Resolve(area, required: true);

        Assert.False(childState.Visible);
        Assert.True(childState.Locked);
        Assert.Equal(1, childState.Depth);
        Assert.Equal("writer", childState.Owner);
        Assert.Equal(2, runtime.Handle!.Layers.Count);
        Assert.Empty(runtime.Diagnostics);
    }

    [Fact]
    public void R056InvalidHierarchySchemaProvenanceAndOwnershipHaveStableDiagnostics()
    {
        var first = Guid.Parse("a0560000-0000-4000-8000-000000000020");
        var second = Guid.Parse("a0560000-0000-4000-8000-000000000021");
        var area = Area([
            new(first, "Duplicate", second, true, false, "one", [EntityId], 99, string.Empty),
            new(second, "Duplicate", first, true, false, "two", [EntityId], provenance: "test:r056"),
        ], required: true);

        var diagnostics = AreaLayerAuthoringValidator.Validate(area);

        Assert.Contains(diagnostics, item => item.Code == AreaLayerAuthoringValidator.SchemaCode);
        Assert.Contains(diagnostics, item => item.Code == AreaLayerAuthoringValidator.ProvenanceCode);
        Assert.Contains(diagnostics, item => item.Code == AreaLayerAuthoringValidator.CycleCode);
        Assert.Contains(diagnostics, item => item.Code == AreaLayerAuthoringValidator.EntityOwnershipCode);
    }

    [Fact]
    public void R056MissingOptionalLayersAreIgnoredButRequiredLayersAreDiagnosed()
    {
        var area = Area([], required: false);

        var optional = AreaLayerRuntimeResolver.Resolve(area, required: false);
        var required = AreaLayerRuntimeResolver.Resolve(area, required: true);

        Assert.Null(optional.Handle);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal(AreaLayerRuntimeResolver.RequiredMissingCode, Assert.Single(required.Diagnostics).Code);
    }

    private static AreaDefinition Area(IReadOnlyList<AreaEditorLayer> layers, bool required)
    {
        var entity = new EntityInstance(EntityId,
            BlueprintId.From(Guid.Parse("a0560000-0000-4000-8000-000000000003")), TransformValue.Identity);
        var source = AreaAuthoringProfile.CreateDefault(AreaId);
        var authoring = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, source.Map, layers,
            source.Experience, required);
        return new(AreaId, "r056", "R056", [entity], authoring: authoring);
    }
}
