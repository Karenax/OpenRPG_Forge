#if TOOLS
using Forge.Application.Assets;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class MultiAreaSplitViewWindow : Window
{
    public int ViewportCount => 2;

    private Func<ProjectState?>? _getState;
    private Func<string?>? _getWorkspaceRoot;
    private Func<AssetPackageManifestState?>? _getManifestState;
    private Action<Window>? _brand;
    private AreaId? _leftArea;
    private AreaId? _rightArea;
    private readonly ProjectPreview3D _left = new();
    private readonly ProjectPreview3D _right = new();
    private readonly Label _title = new();

    public void Initialize(Func<ProjectState?> getState, Func<string?> getWorkspaceRoot,
        Func<AssetPackageManifestState?> getManifestState, Action<Window> brand)
    {
        _getState = getState;
        _getWorkspaceRoot = getWorkspaceRoot;
        _getManifestState = getManifestState;
        _brand = brand;
        Title = "Vue partagée multi-zones · OpenRPG Forge";
        MinSize = new Vector2I(1680, 780);
        Size = MinSize;
        Transient = true;
        Visible = false;
        var root = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);
        _title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(_title);
        var views = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        views.AddThemeConstantOverride("separation", 10);
        root.AddChild(views);
        views.AddChild(ViewCard("ZONE ACTIVE · ÉDITION", _left));
        views.AddChild(ViewCard("COMPARAISON · LECTURE SEULE", _right));
        _left.Initialize(getState, getWorkspaceRoot, getManifestState, () => _leftArea, () => 1,
            _ => { }, (_, _) => false, (_, _) => null, (_, _) => false, (_, _) => { });
        _right.Initialize(getState, getWorkspaceRoot, getManifestState, () => _rightArea, () => 1,
            _ => { }, (_, _) => false, (_, _) => null, (_, _) => false, (_, _) => { });
    }

    public void Open(AreaId leftArea, AreaId rightArea, EditorSplitAxis axis)
    {
        var state = _getState?.Invoke();
        if (state is null || leftArea == rightArea ||
            state.Project.Areas.All(area => area.Id != leftArea) ||
            state.Project.Areas.All(area => area.Id != rightArea)) return;
        _leftArea = leftArea;
        _rightArea = rightArea;
        var leftName = state.Project.Areas.Single(area => area.Id == leftArea).DisplayName;
        var rightName = state.Project.Areas.Single(area => area.Id == rightArea).DisplayName;
        _title.Text = $"VUE PARTAGÉE {axis.ToString().ToUpperInvariant()} · {leftName} ↔ {rightName} · cible active protégée";
        _left.Rebuild(state.Project);
        _right.Rebuild(state.Project);
        PopupCentered(MinSize);
        _brand?.Invoke(this);
    }

    private static PanelContainer ViewCard(string title, ProjectPreview3D preview)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("100c18"),
            BorderColor = new Color("62458a"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1
        });
        var column = new VBoxContainer();
        column.AddChild(new Label { Text = title });
        column.AddChild(preview);
        panel.AddChild(column);
        return panel;
    }
}
#endif
