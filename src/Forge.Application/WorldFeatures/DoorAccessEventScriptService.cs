using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

public sealed class DoorAccessEventScriptService
{
    public sealed record LockView(
        Guid Id,
        string State,
        IReadOnlyList<string> CredentialIds,
        string SkillId,
        double Difficulty,
        bool Relockable,
        bool ConsumeCredential);

    public sealed record RejectionView(
        Guid Id,
        string EventBinding,
        IReadOnlyList<string> Locales,
        string SoundAssetId,
        string AnimationId,
        string ScriptId);

    public sealed record TrapView(
        Guid Id,
        string PayloadId,
        double DetectionDifficulty,
        double DisarmDifficulty,
        IReadOnlyList<string> Sides,
        IReadOnlyList<string> Events);

    public sealed record EventView(Guid Id, string ApiVersion, string BeforeOrRequested, string AfterOrCompleted);

    public sealed record LockEventView(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string ApiVersion,
        string LockedBinding,
        string UnlockedBinding);

    public sealed record Handle(
        int SchemaVersion,
        long Revision,
        Guid DoorId,
        string BlueprintId,
        string AssetId,
        string Provenance,
        LockView Lock,
        RejectionView Rejection,
        TrapView Trap,
        EventView PortalUse,
        EventView StateEvents,
        LockEventView? LockStateEvents,
        bool LockStateEventsRequiredAtRuntime,
        IReadOnlyDictionary<string, string> Overrides);

    public Handle Get(ProjectState state, Guid doorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var door = state.Project.Module.WorldFeatures.Doors.SingleOrDefault(item => item.Id == doorId)
            ?? throw new KeyNotFoundException($"Door world profile '{doorId:D}' was not found.");
        var resolution = DoorAccessEventProfileValidator.Resolve(door);
        var profile = resolution.Profile ?? throw new InvalidOperationException(
            resolution.Diagnostics.FirstOrDefault()?.Code ?? "WORLD_DOOR_ACCESS_EVENTS_MISSING");
        if (!resolution.Available)
            throw new InvalidOperationException(string.Join('|', resolution.Diagnostics.Select(item => item.Code)));

        return new(profile.SchemaVersion, state.Revision.Value, door.Id, door.BlueprintId.ToString(),
            profile.AssetId, profile.Provenance,
            new(profile.Lock.Id, profile.Lock.State.ToString(), profile.Lock.CredentialIds, profile.Lock.SkillId,
                profile.Lock.Difficulty, profile.Lock.Relockable, profile.Lock.ConsumeCredential),
            new(profile.Rejection.Id, profile.Rejection.EventBinding,
                profile.Rejection.Texts.Select(item => item.Locale).ToArray(), profile.Rejection.SoundAssetId,
                profile.Rejection.AnimationId, profile.Rejection.ScriptId),
            new(profile.Trap.Id, profile.Trap.PayloadId, profile.Trap.DetectionDifficulty,
                profile.Trap.DisarmDifficulty, profile.Trap.Sides, profile.Trap.Events),
            new(profile.PortalUse.Id, profile.PortalUse.ForgeScriptApiVersion,
                profile.PortalUse.RequestedBinding, profile.PortalUse.CompletedBinding),
            new(profile.StateEvents.Id, profile.StateEvents.ForgeScriptApiVersion,
                profile.StateEvents.OpenedBinding, profile.StateEvents.ClosedBinding),
            profile.LockStateEvents is null ? null : new(profile.LockStateEvents.Id,
                profile.LockStateEvents.SchemaVersion, profile.LockStateEvents.Provenance,
                profile.LockStateEvents.ForgeScriptApiVersion, profile.LockStateEvents.LockedBinding,
                profile.LockStateEvents.UnlockedBinding), profile.LockStateEventsRequiredAtRuntime, profile.Overrides);
    }
}
