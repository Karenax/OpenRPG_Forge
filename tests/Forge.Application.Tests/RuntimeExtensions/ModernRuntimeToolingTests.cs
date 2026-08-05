using Forge.Application.Combat;
using Forge.Application.Entities;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.RuntimeExtensions;

public sealed class ModernRuntimeToolingTests
{
    [Fact]
    public void Runtime_copy_remaps_identity_references_owner_and_allowlisted_state_atomically()
    {
        var policy = ModuleRuntimeExtensionProfile.CreateDefault(ModuleId.From(Guid.NewGuid()))
            .ModernTooling.RuntimeCopy;
        var sourceId = EntityId.From(Guid.NewGuid()); var destinationId = EntityId.From(Guid.NewGuid());
        var source = new EntityInstance(sourceId, BlueprintId.From(Guid.NewGuid()), TransformValue.Identity,
            ["actor"]);
        var request = new RuntimeEntityCopyRequest(
            new RuntimeEntityCopySource(source, "server", [sourceId],
                [new RuntimeEntityStateComponent("transform", "{}"), new RuntimeEntityStateComponent("secret", "true")]),
            destinationId, new Vector3Value(2, 0, 3), "player", true,
            new Dictionary<EntityId, EntityId> { [sourceId] = destinationId });

        var result = Assert.Single(new RuntimeEntityCopyService(policy).CopyTransaction([request], new HashSet<EntityId>()));

        Assert.True(result.Succeeded);
        Assert.Equal(destinationId, result.Entity!.Id);
        Assert.Equal(new Vector3Value(2, 0, 3), result.Entity.Transform.Position);
        Assert.Equal("server", result.OwnerId);
        Assert.Equal(destinationId, Assert.Single(result.References));
        Assert.Equal("transform", Assert.Single(result.State).Name);
    }

    [Fact]
    public void Runtime_copy_rejects_transaction_without_server_authority()
    {
        var policy = ModuleRuntimeExtensionProfile.CreateDefault(ModuleId.From(Guid.NewGuid()))
            .ModernTooling.RuntimeCopy;
        var source = new EntityInstance(EntityId.From(Guid.NewGuid()), BlueprintId.From(Guid.NewGuid()), TransformValue.Identity);
        var request = new RuntimeEntityCopyRequest(new RuntimeEntityCopySource(source, "server", [], []),
            EntityId.From(Guid.NewGuid()), Vector3Value.Zero, "player", false, new Dictionary<EntityId, EntityId>());

        var result = Assert.Single(new RuntimeEntityCopyService(policy).CopyTransaction([request], new HashSet<EntityId>()));

        Assert.False(result.Succeeded);
        Assert.Equal(RuntimeEntityCopyService.AuthorityRequiredCode, result.Code);
    }

    [Fact]
    public void Aoe_preview_filters_shape_height_cover_and_server_confirmation()
    {
        var profile = ModuleRuntimeExtensionProfile.CreateDefault(ModuleId.From(Guid.NewGuid()))
            .ModernTooling.AreaEffectIndicator;
        var visible = EntityId.From(Guid.NewGuid()); var covered = EntityId.From(Guid.NewGuid());
        var service = new AreaEffectPreviewService(profile);
        var request = new AreaEffectPreviewRequest(AreaEffectShape.Cone, Vector3Value.Zero,
            new Vector3Value(0, 0, 1), 10, 2, 2, 90, true,
            [new AreaEffectTargetCandidate(visible, new Vector3Value(0, 1, 4), 0, true),
             new AreaEffectTargetCandidate(covered, new Vector3Value(1, 1, 4), .9f, true),
             new AreaEffectTargetCandidate(EntityId.From(Guid.NewGuid()), new Vector3Value(0, 30, 4), 0, true)]);

        var result = service.Preview(request);
        var unconfirmed = service.Preview(request with { ServerConfirmed = false });

        Assert.True(result.IsValid);
        Assert.Equal(visible, Assert.Single(result.ValidTargets));
        Assert.Equal(covered, Assert.Single(result.BlockedTargets));
        Assert.False(unconfirmed.IsValid);
        Assert.Equal(AreaEffectPreviewService.ConfirmationRequiredCode, unconfirmed.Code);
    }
}
