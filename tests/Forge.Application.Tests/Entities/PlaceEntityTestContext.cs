using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Entities;

internal sealed class PlaceEntityTestContext
{
    internal static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 9, 0, 0, TimeSpan.Zero);

    internal AreaId AreaId { get; } = AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525"));

    internal BlueprintId BlueprintId { get; } = BlueprintId.From(Guid.Parse("373fbd0f-dd40-4054-9d8f-e9f7bb7a36bc"));

    internal EntityId EntityId { get; } = EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56"));

    internal ProjectState State { get; }

    internal PlaceEntityHandler Handler { get; } = new(new FixedClock(Timestamp));

    internal PlaceEntityTestContext()
    {
        var blueprint = new BlueprintDefinition(
            BlueprintId,
            "ash-warden",
            "Ash Warden",
            BlueprintKind.Creature);
        var area = new AreaDefinition(AreaId, "crypt", "Crypt", []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("69ddeadc-4cc1-42d0-aed6-9d99244479cf")),
            "Test project",
            [blueprint],
            [area]);
        State = new ProjectState(project, new ProjectRevision(7));
    }

    internal PlaceEntityCommand CreateCommand(bool dryRun = false, ProjectRevision? revision = null) =>
        new(
            CommandId.From(Guid.Parse("3ac290e9-51ee-45dd-8f8f-bfeb9d5fa07d")),
            revision ?? State.Revision,
            AreaId,
            EntityId,
            BlueprintId,
            new TransformValue(
                new Vector3Value(2, 0, 3),
                QuaternionValue.Identity,
                Vector3Value.One),
            dryRun);
}
