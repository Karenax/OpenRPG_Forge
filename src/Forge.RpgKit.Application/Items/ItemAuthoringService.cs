using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Application.Items;

public sealed record ItemWizardRequest(string Slug, string Archetype, Guid TypeId, string AppearanceAssetId, string LicenseSpdx, string ContentSha256, int Quality, int Level, IReadOnlySet<string> Slots, IReadOnlyList<ItemProperty> Properties, IReadOnlyList<LocalizedItemText> Texts, decimal BaseValue, string Provenance);
public sealed record ItemAuthoringState(long Revision, IReadOnlyList<ItemDefinition> Items);
public sealed record ItemAuthoringResult(ItemAuthoringState State, ItemDefinition Item, string Patch, string InversePatch, bool DryRun);

public static class ItemAuthoringService
{
    public static ItemAuthoringResult Create(ItemAuthoringState state, ItemWizardRequest request, IReadOnlyDictionary<Guid, ItemType> types, long expectedRevision, bool dryRun)
    {
        if (state.Revision != expectedRevision) throw new InvalidOperationException("RPG_ITEM_REVISION_CONFLICT");
        var item = new ItemDefinition(Guid.NewGuid(), 1, request.Provenance, request.Slug, request.TypeId, request.Archetype, request.AppearanceAssetId, request.LicenseSpdx, request.ContentSha256, request.Quality, request.Level, request.Slots, request.Properties, request.Texts, request.BaseValue);
        var diagnostics = ItemRules.Validate(item, types); if (diagnostics.Count > 0) throw new InvalidOperationException(diagnostics[0].Code);
        var next = dryRun ? state : new ItemAuthoringState(state.Revision + 1, state.Items.Concat([item]).ToArray());
        return new(next, item, $"add:/items/{item.Id}", $"remove:/items/{item.Id}", dryRun);
    }

    public static ItemAuthoringResult MigrateType(ItemAuthoringState state, Guid itemId, ItemType target, string currentRig, long expectedRevision, bool acceptLoss, bool dryRun)
    {
        if (state.Revision != expectedRevision) throw new InvalidOperationException("RPG_ITEM_REVISION_CONFLICT");
        var current = state.Items.FirstOrDefault(item => item.Id == itemId) ?? throw new InvalidOperationException("RPG_ITEM_NOT_FOUND");
        var preview = ItemRules.PreviewTypeMigration(current, target, currentRig);
        if (!acceptLoss && (preview.IncompatibleProperties.Count > 0 || preview.IncompatibleSlots.Count > 0 || preview.RequiresRigChange)) throw new InvalidOperationException("RPG_ITEM_MIGRATION_PREVIEW_REQUIRED");
        var migrated = current with { TypeId = target.Id, Properties = current.Properties.Except(preview.IncompatibleProperties).ToArray(), Slots = current.Slots.Except(preview.IncompatibleSlots).ToHashSet(StringComparer.Ordinal) };
        var items = state.Items.Select(item => item.Id == itemId ? migrated : item).ToArray();
        return new(dryRun ? state : new(state.Revision + 1, items), migrated, $"replace:/items/{itemId}/type", $"restore:/items/{itemId}/type/{current.TypeId}", dryRun);
    }
}
