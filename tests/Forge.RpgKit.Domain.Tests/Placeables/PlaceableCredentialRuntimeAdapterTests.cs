using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableCredentialRuntimeAdapterTests
{
    [Theory]
    [InlineData(PlaceableWorldProfile.CredentialConsumption.Retain, true, false, 3)]
    [InlineData(PlaceableWorldProfile.CredentialConsumption.Consume, true, true, 3)]
    [InlineData(PlaceableWorldProfile.CredentialConsumption.Charge, true, true, 2)]
    [InlineData(PlaceableWorldProfile.CredentialConsumption.Shared, true, false, 3)]
    public void Projects_item_credential_consumption_decisions(
        PlaceableWorldProfile.CredentialConsumption consumption,
        bool allowed,
        bool consume,
        int remaining)
    {
        var placeable = Placeable(new("item:key", consumption, consumption ==
            PlaceableWorldProfile.CredentialConsumption.Charge ? 3 : 0, null, true));
        var adapter = new PlaceableCredentialRuntimeAdapter();
        var resolution = adapter.Resolve(placeable);

        var result = adapter.Preview(
            resolution.Handle!,
            placeable,
            new HashSet<string> { "item:key" },
            new HashSet<string>(),
            3);

        Assert.True(resolution.Available);
        Assert.Equal(allowed, result.Allowed);
        Assert.Equal(consume, result.Consume);
        Assert.Equal(remaining, result.RemainingCharges);
        Assert.Contains("audited", result.Audit);
    }

    [Fact]
    public void Resolves_virtual_permission_without_inventory_credential()
    {
        var placeable = Placeable(new(
            "permission:archive",
            PlaceableWorldProfile.CredentialConsumption.VirtualPermission,
            0,
            "archive.enter",
            true));
        var adapter = new PlaceableCredentialRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;

        var denied = adapter.Preview(handle, placeable, new HashSet<string>(), new HashSet<string>(), 0);
        var allowed = adapter.Preview(
            handle,
            placeable,
            new HashSet<string>(),
            new HashSet<string> { "archive.enter" },
            0);

        Assert.False(denied.Allowed);
        Assert.True(allowed.Allowed);
        Assert.False(allowed.Consume);
    }

    [Fact]
    public void Resolves_optional_and_required_absence()
    {
        var adapter = new PlaceableCredentialRuntimeAdapter();
        var optional = Placeable(null);
        var required = optional with { CredentialRequiredAtRuntime = true };

        Assert.True(adapter.Resolve(optional).OptionalFallback);
        Assert.True(WorldFeatureRuntimeResolver.ResolvePlaceableAccess(
            optional, new HashSet<string>(), new HashSet<string>(), 0).Allowed);
        Assert.Equal("WORLD_PLACEABLE_CREDENTIAL_REQUIRED_MISSING", adapter.Resolve(required).DiagnosticCode);
        Assert.False(WorldFeatureRuntimeResolver.ResolvePlaceableAccess(
            required, new HashSet<string>(), new HashSet<string>(), 0).Allowed);
    }

    [Fact]
    public void Rejects_invalid_policy_and_stale_handle()
    {
        var adapter = new PlaceableCredentialRuntimeAdapter();
        var placeable = Placeable(new(
            "item:key",
            PlaceableWorldProfile.CredentialConsumption.Charge,
            2,
            null,
            false));
        var handle = adapter.Resolve(placeable).Handle!;
        var invalid = placeable with
        {
            Credential = placeable.Credential! with { Charges = 0 },
        };

        Assert.Equal("WORLD_PLACEABLE_CREDENTIAL_INVALID", adapter.Resolve(invalid).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(handle with { Charges = 1 }, placeable));
    }

    private static PlaceableWorldProfile Placeable(PlaceableWorldProfile.CredentialPolicy? credential) => new(
        Guid.Parse("21000000-0000-4000-8000-000000000001"),
        1,
        "test:ux1-q",
        BlueprintId.From(Guid.Parse("21000000-0000-4000-8000-000000000002")),
        null,
        null,
        credential,
        new([], new(false, 1, 1, true, "placeable")),
        null);
}
