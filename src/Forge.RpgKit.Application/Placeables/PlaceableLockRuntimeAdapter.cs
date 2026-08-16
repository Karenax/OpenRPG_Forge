using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableLockHandle(Guid PlaceableId, Guid OperationsId, Guid LockId);

public sealed record PlaceableLockResolution(
    bool Available,
    bool OptionalFallback,
    PlaceableLockHandle? Handle,
    string? DiagnosticCode);

public sealed class PlaceableLockRuntimeAdapter
{
    public PlaceableLockResolution Resolve(PlaceableWorldProfile placeable)
    {
        if (placeable.Operations?.Lock is not { } lockProfile)
        {
            var required = placeable.Foundation?.Components.HasLock == true;
            return required
                ? new(false, false, null, "WORLD_PLACEABLE_LOCK_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        return new(
            true,
            false,
            new(placeable.Id, placeable.Operations.Id, lockProfile.Id),
            null);
    }

    public LockComponent Build(PlaceableLockHandle handle, PlaceableWorldProfile placeable)
    {
        var profile = Require(handle, placeable);
        return new(
            MapState(profile.InitialState),
            profile.CredentialIds.ToHashSet(StringComparer.Ordinal),
            profile.SkillId ?? string.Empty,
            profile.Difficulty,
            profile.CodeHash,
            profile.Hackable,
            profile.Relockable,
            profile.CanJam,
            profile.RequiredPermissions.ToHashSet(StringComparer.Ordinal));
    }

    public LockResult PreviewUnlock(
        PlaceableLockHandle handle,
        PlaceableWorldProfile placeable,
        LockAttempt attempt)
        => PlaceableSecurityService.AttemptUnlock(Build(handle, placeable), attempt);

    public LockResult PreviewRelock(
        PlaceableLockHandle handle,
        PlaceableWorldProfile placeable,
        LockComponent current,
        bool jam,
        string actorId)
    {
        Require(handle, placeable);
        return PlaceableSecurityService.Relock(current, jam, actorId);
    }

    private static PlaceableLockProfile Require(
        PlaceableLockHandle handle,
        PlaceableWorldProfile placeable)
    {
        if (handle.PlaceableId != placeable.Id ||
            placeable.Operations is not { } operations ||
            handle.OperationsId != operations.Id ||
            operations.Lock is not { } lockProfile ||
            handle.LockId != lockProfile.Id)
        {
            throw new InvalidOperationException("WORLD_PLACEABLE_LOCK_HANDLE_STALE");
        }

        return lockProfile;
    }

    private static LockState MapState(PlaceableAuthoredLockState state) => state switch
    {
        PlaceableAuthoredLockState.Unlocked => LockState.Unlocked,
        PlaceableAuthoredLockState.Locked => LockState.Locked,
        PlaceableAuthoredLockState.Jammed => LockState.Jammed,
        PlaceableAuthoredLockState.Broken => LockState.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
