#if TOOLS
using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Toolset.Editor;

public partial class BlueprintDragPalette : ItemList
{
    public const string PayloadKind = "openrpg_forge.blueprint";
    private readonly List<BlueprintId> _blueprintIds = [];

    public BlueprintDragPalette()
    {
        CustomMinimumSize = new Vector2(190, 132);
        MaxColumns = 1;
        SameColumnWidth = true;
        SelectMode = SelectModeEnum.Single;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = "Faites glisser un blueprint dans le Monde pour le placer.";
    }

    public void SetBlueprints(IEnumerable<BlueprintDefinition> blueprints)
    {
        _blueprintIds.Clear();
        Clear();
        foreach (var blueprint in blueprints.OrderBy(candidate => candidate.DisplayName, StringComparer.Ordinal))
        {
            _blueprintIds.Add(blueprint.Id);
            AddItem($"⋮⋮  {blueprint.DisplayName}");
            SetItemTooltip(ItemCount - 1, $"{blueprint.Kind} · {blueprint.Slug}");
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var index = GetItemAtPosition(atPosition, true);
        if (index < 0 || index >= _blueprintIds.Count)
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
        return new Godot.Collections.Dictionary
        {
            ["kind"] = PayloadKind,
            ["blueprintId"] = _blueprintIds[index].ToString(),
        };
    }
}
#endif
