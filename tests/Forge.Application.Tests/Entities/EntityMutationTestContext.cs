using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Entities;

internal sealed class EntityMutationTestContext
{
    internal static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    internal AreaId AreaId { get; } = AreaId.From(Guid.Parse("13a5f729-7e44-48a4-93ee-832caa2d367a"));

    internal BlueprintId BlueprintId { get; } = BlueprintId.From(Guid.Parse("fdf16afa-951b-4d1c-a1c1-579a45060bfd"));

    internal EntityId EntityId { get; } = EntityId.From(Guid.Parse("9fc9bb43-a452-4c45-8470-d69a61e811ed"));

    internal ProjectState State { get; }

    internal TransformEntityHandler TransformHandler { get; } = new(new FixedClock(Timestamp));

    internal RemoveEntityHandler RemoveHandler { get; } = new(new FixedClock(Timestamp));

    internal SetEntityOverridesHandler OverridesHandler { get; } = new(new FixedClock(Timestamp));

    internal EntityMutationTestContext()
    {
        var blueprint = new BlueprintDefinition(
            BlueprintId,
            "ash-pillar",
            "Ash Pillar",
            BlueprintKind.Placeable);
        var entity = new EntityInstance(
            EntityId,
            BlueprintId,
            TransformValue.Identity,
            ["architecture", "solid"],
            new EntityPropertyOverrides(
                EntityPropertyOverrides.CurrentSchemaVersion,
                [new EntityPropertyOverride("locked", EntityPropertyValueKind.Boolean, "false")]));
        var area = new AreaDefinition(AreaId, "crypt", "Crypt", [entity]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("b8d76f40-fec5-458b-88f3-46d1649b83e0")),
            "Mutation test project",
            [blueprint],
            [area]);
        State = new ProjectState(project, new ProjectRevision(3));
    }

    internal TransformEntityCommand CreateTransformCommand(
        bool dryRun = false,
        ProjectRevision? revision = null,
        EntityId? entityId = null) =>
        new(
            CommandId.From(Guid.Parse("835b527a-0bfd-48ad-9515-f025fa236ac4")),
            revision ?? State.Revision,
            AreaId,
            entityId ?? EntityId,
            new TransformValue(
                new Vector3Value(4, 0, -2),
                new QuaternionValue(0, 0.70710677f, 0, 0.70710677f),
                Vector3Value.One),
            dryRun);

    internal RemoveEntityCommand CreateRemoveCommand(
        bool dryRun = false,
        ProjectRevision? revision = null,
        EntityId? entityId = null) =>
        new(
            CommandId.From(Guid.Parse("6fe6ce4a-f20e-426c-b66e-fc27661de5f6")),
            revision ?? State.Revision,
            AreaId,
            entityId ?? EntityId,
            dryRun);

    internal SetEntityOverridesCommand CreateOverridesCommand(
        bool dryRun = false,
        ProjectRevision? revision = null,
        AreaId? areaId = null,
        EntityId? entityId = null) =>
        new(
            CommandId.From(Guid.Parse("a888e887-ecf4-453a-854d-2b925a81f4ba")),
            revision ?? State.Revision,
            areaId ?? AreaId,
            entityId ?? EntityId,
            new EntityPropertyOverrides(
                EntityPropertyOverrides.CurrentSchemaVersion,
                [
                    new EntityPropertyOverride("displayName", EntityPropertyValueKind.String, "Pilier fendu"),
                    new EntityPropertyOverride("locked", EntityPropertyValueKind.Boolean, "true"),
                ]),
            dryRun);
}
