using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;

namespace Forge.Application.Items;

public readonly record struct ItemFoundationHandle(Guid Value);

public sealed record ItemFoundationScriptView(
    ItemFoundationHandle Handle,
    string BlueprintId,
    string Slug,
    string Type,
    int DefinitionVersion,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyList<string> Slots,
    IReadOnlyList<string> AllowedOverrides);

public static class ItemFoundationScriptService
{
    public static ItemFoundationScriptView? Read(ProjectDefinition project, BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var profile = blueprint?.ItemFoundation;
        if (blueprint is null || profile is null) return null;
        var resolved = ItemFoundationValidator.Resolve(profile, profile.RequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null) return null;
        return new(new(profile.Id), blueprint.Id.ToString(), blueprint.Slug, profile.Type.Slug,
            profile.DefinitionVersion,
            profile.Texts.ToDictionary(item => item.Locale, item => item.Name, StringComparer.Ordinal),
            profile.Slots, profile.AllowedInstanceOverrides);
    }

    public static IReadOnlyList<ItemFoundationDiagnostic> ValidateInstance(
        ItemFoundationHandle handle,
        ItemFoundationProfile profile,
        ItemFoundationInstance instance)
    {
        if (handle.Value != profile.Id)
            return [new("item.foundation.handle_stale", "itemInstance", "The typed item handle is stale.")];
        return ItemFoundationValidator.ValidateInstance(profile, instance);
    }
}
