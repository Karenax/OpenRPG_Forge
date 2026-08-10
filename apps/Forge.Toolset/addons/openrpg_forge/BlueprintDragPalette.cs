#if TOOLS
using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Toolset.Editor;

public partial class BlueprintDragPalette : ItemList
{
    public const string PayloadKind = "openrpg_forge.blueprint";
    private readonly List<BlueprintDefinition> _blueprints = [];

    public BlueprintDragPalette()
    {
        CustomMinimumSize = new Vector2(190, 132);
        MaxColumns = 1;
        SameColumnWidth = true;
        SelectMode = SelectModeEnum.Single;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = "Glissez vers le Monde, un graphe, un inventaire, une propriété ou un package.";
    }

    public void SetBlueprints(IEnumerable<BlueprintDefinition> blueprints)
    {
        _blueprints.Clear();
        Clear();
        foreach (var blueprint in blueprints.OrderBy(candidate => candidate.DisplayName, StringComparer.Ordinal))
        {
            _blueprints.Add(blueprint);
            AddItem($"⋮⋮  {blueprint.DisplayName}");
            SetItemTooltip(ItemCount - 1, $"{blueprint.Kind} · {blueprint.Slug}");
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var index = GetItemAtPosition(atPosition, true);
        if (index < 0 || index >= _blueprints.Count)
        {
            return default;
        }

        Select(index);
        var preview = new Label
        {
            Text = $"  Placer {GetItemText(index)}  ",
        };
        preview.AddThemeColorOverride("font_color", Colors.White);
        preview.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("d84f32"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        SetDragPreview(preview);
        var blueprint = _blueprints[index];
        return new Godot.Collections.Dictionary
        {
            ["kind"] = PayloadKind,
            ["schemaVersion"] = Forge.Application.Blueprints.BlueprintDropPlanner.CurrentSchemaVersion,
            ["blueprintId"] = blueprint.Id.ToString(),
            ["version"] = blueprint.Authoring.Version,
            ["source"] = blueprint.Provenance.Source,
            ["author"] = blueprint.Provenance.Author,
            ["licenseSpdx"] = blueprint.Provenance.LicenseSpdx,
            ["permissionEvidence"] = blueprint.Provenance.PermissionEvidence,
            ["contentHash"] = blueprint.Provenance.ContentHash,
            ["provenance"] = "toolset:blueprint-drag",
        };
    }
}
#endif
