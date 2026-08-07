using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public sealed record DoorWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    DoorWorldProfile.DamagePolicy? Damage,
    DoorWorldProfile.LoadingPresentation? Loading,
    IReadOnlyList<DoorWorldProfile.EventBinding> Events,
    Guid? DialogueId,
    string? FactionId,
    DoorWorldProfile.AssemblyPolicy? Assembly)
{
    public enum AssemblyKind { Single, Double, Portcullis, Elevator, Airlock }

    public sealed record DamagePolicy(
        double MaximumHealth,
        double Hardness,
        bool PlotProtected,
        string DamagedVisual,
        string BreachedVisual,
        bool UpdateNavigation,
        string DebrisAssetId,
        string RepairServiceId);

    public sealed record LoadingPresentation(
        string ImageAssetId,
        string TitleKey,
        string TipKey,
        bool UseDestinationFallback,
        double MinimumDisplaySeconds);

    public sealed record EventBinding(string Event, string ScriptId, bool Before, bool Authoritative);

    public sealed record Panel(
        Guid Id,
        string Role,
        double OpenOffset,
        double DelaySeconds,
        bool Reversed);

    public sealed record AssemblyPolicy(
        AssemblyKind Kind,
        IReadOnlyList<Panel> Panels,
        string Sequence,
        bool Interlocked,
        string FailureRecovery);
}
