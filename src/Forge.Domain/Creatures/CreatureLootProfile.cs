namespace Forge.Domain.Creatures;

public enum LootOwnershipMode { Killer, Party, FreeForAll, FixedOwner, ServerAssigned }
public enum LootInstancingMode { Shared, PerPlayer, PerParty }
public enum CreatureCorpseMode { None, Container, RagdollContainer, RagdollVisual }
public enum CorpseCleanupMode { ImmediateWhenEmpty, Timed, DistanceBudget, Persistent }

public sealed record CreatureLootProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool RequiredAtRuntime { get; }
    public IReadOnlyList<LootEntryPolicy> Entries { get; }
    public TheftProfile Theft { get; }
    public CreatureCorpseProfile Corpse { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }

    public CreatureLootProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool requiredAtRuntime,
        IReadOnlyList<LootEntryPolicy>? entries,
        TheftProfile theft,
        CreatureCorpseProfile corpse,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(theft);
        ArgumentNullException.ThrowIfNull(corpse);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        RequiredAtRuntime = requiredAtRuntime;
        Entries = (entries ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.ItemSlug, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        Theft = theft.Normalize();
        Corpse = corpse.Normalize();
        Overrides = (overrides ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
    }

    public static CreatureLootProfile CreateDefault(Guid id, string provenance) => new(
        id, CurrentSchemaVersion, provenance, false, [],
        new TheftProfile(false, "stealth", "awareness", 10, 10, true, "crime.theft", 30, "loot.pickpocket"),
        new CreatureCorpseProfile(CreatureCorpseMode.None, 0, null, null, false, "none", 0,
            CorpseCleanupMode.ImmediateWhenEmpty),
        new Dictionary<string, string>());

    public sealed record LootEntryPolicy(
        Guid Id,
        string ItemSlug,
        string? SlotId,
        int MinimumQuantity,
        int MaximumQuantity,
        double Chance,
        bool DropOnDeath,
        bool Pickpocketable,
        LootOwnershipMode Ownership,
        LootInstancingMode Instancing,
        string? FixedOwnerId)
    {
        internal LootEntryPolicy Normalize() => this with
        {
            ItemSlug = CreaturePresentationProfile.NormalizeIdentifier(ItemSlug),
            SlotId = string.IsNullOrWhiteSpace(SlotId) ? null : CreaturePresentationProfile.NormalizeIdentifier(SlotId),
            FixedOwnerId = string.IsNullOrWhiteSpace(FixedOwnerId) ? null : FixedOwnerId.Trim(),
        };
    }

    public sealed record TheftProfile(
        bool Enabled,
        string SkillId,
        string PerceptionSkillId,
        double Difficulty,
        double PerceptionDifficulty,
        bool RequireServerConsent,
        string CrimeId,
        double CooldownSeconds,
        string AuditTag)
    {
        internal TheftProfile Normalize() => this with
        {
            SkillId = CreaturePresentationProfile.NormalizeIdentifier(SkillId),
            PerceptionSkillId = CreaturePresentationProfile.NormalizeIdentifier(PerceptionSkillId),
            CrimeId = CreaturePresentationProfile.NormalizeIdentifier(CrimeId),
            AuditTag = CreaturePresentationProfile.NormalizeIdentifier(AuditTag),
        };
    }

    public sealed record CreatureCorpseProfile(
        CreatureCorpseMode Mode,
        double LifetimeSeconds,
        string? ContainerId,
        string? RagdollProfileId,
        bool AllowResurrection,
        string OwnershipPolicy,
        int MaximumItems,
        CorpseCleanupMode CleanupMode)
    {
        internal CreatureCorpseProfile Normalize() => this with
        {
            ContainerId = string.IsNullOrWhiteSpace(ContainerId)
                ? null : CreaturePresentationProfile.NormalizeIdentifier(ContainerId),
            RagdollProfileId = string.IsNullOrWhiteSpace(RagdollProfileId)
                ? null : CreaturePresentationProfile.NormalizeIdentifier(RagdollProfileId),
            OwnershipPolicy = CreaturePresentationProfile.NormalizeIdentifier(OwnershipPolicy),
        };
    }
}
