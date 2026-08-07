using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public sealed record PlaceableWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    string? FactionId,
    Guid? DialogueId,
    PlaceableWorldProfile.CredentialPolicy? Credential,
    PlaceableWorldProfile.ReactivePolicy Reactive,
    PlaceableWorldProfile.DynamicPhysicsPolicy? Physics)
{
    public enum CredentialConsumption { Retain, Consume, Charge, Shared, VirtualPermission }
    public enum BodyMode { Static, Rigid, Kinematic }

    public sealed record CredentialPolicy(
        string CredentialId,
        CredentialConsumption Consumption,
        int Charges,
        string? VirtualPermission,
        bool AuditUse);

    public sealed record EventBinding(string Event, string ScriptId, bool Before, bool Authoritative);

    public sealed record SchedulerPolicy(
        bool Enabled,
        double IntervalSeconds,
        int BudgetPerMinute,
        bool SuspendWhenUnobserved,
        string CustomNamespace);

    public sealed record ReactivePolicy(
        IReadOnlyList<EventBinding> Events,
        SchedulerPolicy Scheduler);

    public sealed record DynamicPhysicsPolicy(
        BodyMode Mode,
        double Mass,
        IReadOnlyList<string> Constraints,
        bool Replicated,
        double SleepAfterSeconds,
        bool ConvertToStaticWhenSleeping,
        string Authority);
}
