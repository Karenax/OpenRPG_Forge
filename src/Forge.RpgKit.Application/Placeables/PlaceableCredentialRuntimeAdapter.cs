using Forge.Domain.WorldFeatures;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableCredentialHandle(
    Guid PlaceableId,
    string CredentialId,
    PlaceableWorldProfile.CredentialConsumption Consumption,
    int Charges,
    string? VirtualPermission,
    bool AuditUse);

public sealed record PlaceableCredentialResolution(
    bool Available,
    bool OptionalFallback,
    PlaceableCredentialHandle? Handle,
    string? DiagnosticCode);

public sealed class PlaceableCredentialRuntimeAdapter
{
    public PlaceableCredentialResolution Resolve(PlaceableWorldProfile placeable)
    {
        ArgumentNullException.ThrowIfNull(placeable);
        if (placeable.Credential is null)
        {
            return placeable.CredentialRequiredAtRuntime
                ? new(false, false, null, "WORLD_PLACEABLE_CREDENTIAL_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        var policy = placeable.Credential;
        if (string.IsNullOrWhiteSpace(policy.CredentialId) || policy.Charges < 0 ||
            policy.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge && policy.Charges < 1 ||
            policy.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission &&
            string.IsNullOrWhiteSpace(policy.VirtualPermission))
        {
            return new(false, false, null, "WORLD_PLACEABLE_CREDENTIAL_INVALID");
        }

        return new(true, false, new(
            placeable.Id,
            policy.CredentialId,
            policy.Consumption,
            policy.Charges,
            policy.VirtualPermission,
            policy.AuditUse), null);
    }

    public PlaceableWorldProfile.CredentialPolicy Build(
        PlaceableCredentialHandle handle,
        PlaceableWorldProfile placeable)
        => Require(handle, placeable);

    public WorldFeatureRuntimeResolver.AccessResult Preview(
        PlaceableCredentialHandle handle,
        PlaceableWorldProfile placeable,
        IReadOnlySet<string> credentials,
        IReadOnlySet<string> permissions,
        int availableCharges)
    {
        Require(handle, placeable);
        return WorldFeatureRuntimeResolver.ResolvePlaceableAccess(
            placeable,
            credentials,
            permissions,
            availableCharges);
    }

    private static PlaceableWorldProfile.CredentialPolicy Require(
        PlaceableCredentialHandle handle,
        PlaceableWorldProfile placeable)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(placeable);
        var policy = placeable.Credential;
        if (handle.PlaceableId != placeable.Id || policy is null ||
            handle.CredentialId != policy.CredentialId || handle.Consumption != policy.Consumption ||
            handle.Charges != policy.Charges || handle.VirtualPermission != policy.VirtualPermission ||
            handle.AuditUse != policy.AuditUse)
        {
            throw new InvalidOperationException("WORLD_PLACEABLE_CREDENTIAL_HANDLE_STALE");
        }

        return policy;
    }
}
