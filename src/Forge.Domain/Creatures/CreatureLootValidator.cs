namespace Forge.Domain.Creatures;

public static class CreatureLootValidator
{
    public static IReadOnlyList<CreatureLootDiagnostic> Validate(
        CreatureLootProfile profile,
        string path = "creatureLoot",
        IReadOnlySet<string>? availableItemSlugs = null,
        IReadOnlySet<string>? availableSkillIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureLootDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.loot.uuid_required", path, "A stable loot profile UUID is required.");
        Add(profile.SchemaVersion != CreatureLootProfile.CurrentSchemaVersion, "creature.loot.schema_unsupported", path,
            "The creature loot schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.loot.provenance_required", path,
            "Loot profile provenance is required.");

        var ids = profile.Entries.Select(item => item.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length || profile.Entries.Any(item =>
                item.Id == Guid.Empty || !CreaturePresentationProfile.IsCanonicalIdentifier(item.ItemSlug) ||
                item.SlotId is not null && !CreaturePresentationProfile.IsCanonicalIdentifier(item.SlotId) ||
                item.MinimumQuantity < 1 || item.MaximumQuantity < item.MinimumQuantity || item.MaximumQuantity > 999 ||
                !double.IsFinite(item.Chance) || item.Chance is < 0 or > 1 ||
                item.Ownership == LootOwnershipMode.FixedOwner && string.IsNullOrWhiteSpace(item.FixedOwnerId)))
        {
            diagnostics.Add(new("creature.loot.entry_invalid", $"{path}/entries",
                "Loot entries require unique UUIDs, resolved item IDs, quantities, probability and ownership."));
        }

        if (availableItemSlugs is not null && profile.Entries.Any(item => !availableItemSlugs.Contains(item.ItemSlug)))
        {
            diagnostics.Add(new("creature.loot.item_missing", $"{path}/entries",
                "Every loot entry must resolve to the creature combat loot table."));
        }

        ValidateTheft(profile, path, availableSkillIds, diagnostics);
        ValidateCorpse(profile, path, diagnostics);
        if (profile.Overrides.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.Key) ||
                                          string.IsNullOrWhiteSpace(item.Value)))
        {
            diagnostics.Add(new("creature.loot.override_invalid", $"{path}/overrides",
                "Overrides require canonical keys and explicit non-empty values."));
        }
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    private static void ValidateTheft(
        CreatureLootProfile profile,
        string path,
        IReadOnlySet<string>? availableSkillIds,
        ICollection<CreatureLootDiagnostic> diagnostics)
    {
        var theft = profile.Theft;
        if (!theft.Enabled) return;
        if (!profile.Entries.Any(item => item.Pickpocketable) ||
            !CreaturePresentationProfile.IsCanonicalIdentifier(theft.SkillId) ||
            !CreaturePresentationProfile.IsCanonicalIdentifier(theft.PerceptionSkillId) ||
            !double.IsFinite(theft.Difficulty) || theft.Difficulty < 0 ||
            !double.IsFinite(theft.PerceptionDifficulty) || theft.PerceptionDifficulty < 0 ||
            !double.IsFinite(theft.CooldownSeconds) || theft.CooldownSeconds < 0 ||
            !CreaturePresentationProfile.IsCanonicalIdentifier(theft.CrimeId) ||
            !CreaturePresentationProfile.IsCanonicalIdentifier(theft.AuditTag))
        {
            diagnostics.Add(new("creature.loot.theft_invalid", $"{path}/theft",
                "Pickpocket requires eligible content, skill/perception checks, crime, cooldown and audit tag."));
        }

        if (availableSkillIds is not null &&
            (!availableSkillIds.Contains(theft.SkillId) || !availableSkillIds.Contains(theft.PerceptionSkillId)))
        {
            diagnostics.Add(new("creature.loot.theft_skill_missing", $"{path}/theft",
                "Pickpocket and perception skills must resolve to the creature progression profile."));
        }
    }

    private static void ValidateCorpse(
        CreatureLootProfile profile,
        string path,
        ICollection<CreatureLootDiagnostic> diagnostics)
    {
        var corpse = profile.Corpse;
        var hasContainer = corpse.Mode is CreatureCorpseMode.Container or CreatureCorpseMode.RagdollContainer;
        var hasRagdoll = corpse.Mode is CreatureCorpseMode.RagdollContainer or CreatureCorpseMode.RagdollVisual;
        if (!double.IsFinite(corpse.LifetimeSeconds) || corpse.LifetimeSeconds < 0 ||
            corpse.Mode != CreatureCorpseMode.None && corpse.LifetimeSeconds <= 0 && corpse.CleanupMode != CorpseCleanupMode.Persistent ||
            hasContainer && !CreaturePresentationProfile.IsCanonicalIdentifier(corpse.ContainerId ?? string.Empty) ||
            hasRagdoll && !CreaturePresentationProfile.IsCanonicalIdentifier(corpse.RagdollProfileId ?? string.Empty) ||
            !CreaturePresentationProfile.IsCanonicalIdentifier(corpse.OwnershipPolicy) ||
            corpse.MaximumItems is < 0 or > 4096 || hasContainer && corpse.MaximumItems < 1 ||
            corpse.AllowResurrection && corpse.Mode == CreatureCorpseMode.None)
        {
            diagnostics.Add(new("creature.loot.corpse_invalid", $"{path}/corpse",
                "Corpse mode, duration, container/ragdoll, resurrection, ownership and cleanup budget are incoherent."));
        }
    }
}
