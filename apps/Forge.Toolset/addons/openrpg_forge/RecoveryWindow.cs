#if TOOLS
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class RecoveryWindow : Window
{
    private Func<IReadOnlyList<RecoverySnapshotInfo>>? _list;
    private Func<RecoverySnapshotInfo, RecoveryComparison?>? _restore;
    private Action? _capture;
    private Action? _clear;
    private Action<Window>? _brand;
    private ItemList? _items;
    private Label? _status;
    private IReadOnlyList<RecoverySnapshotInfo> _snapshots = [];

    public void Initialize(Func<IReadOnlyList<RecoverySnapshotInfo>> list,
        Func<RecoverySnapshotInfo, RecoveryComparison?> restore,
        Action capture, Action clear, Action<Window> brand)
    {
        _list = list; _restore = restore; _capture = capture; _clear = clear; _brand = brand;
        Title = "Récupération · OpenRPG Forge"; MinSize = new Vector2I(760, 520); Size = MinSize; Transient = true; Visible = false;
        var root = new VBoxContainer { OffsetLeft = 22, OffsetTop = 20, OffsetRight = 738, OffsetBottom = 500 };
        AddChild(root); var heading = new Label { Text = "SAUVEGARDE AUTOMATIQUE & RÉCUPÉRATION", ThemeTypeVariation = "HeaderSmall" }; heading.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(heading);
        root.AddChild(new Label { Text = "Les instantanés sont isolés dans .forge/recovery. Restaurer charge une copie en mémoire ; Enregistrer reste une action explicite." });
        _items = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(_items);
        _status = new Label(); _status.AddThemeColorOverride("font_color", new Color("79d5ad")); root.AddChild(_status);
        var actions = new HBoxContainer(); var captureButton = new Button { Text = "Créer un instantané" }; captureButton.Pressed += () => { _capture?.Invoke(); Refresh(); }; actions.AddChild(captureButton);
        var restoreButton = new Button { Text = "Restaurer en mémoire" }; restoreButton.Pressed += Restore; actions.AddChild(restoreButton);
        var clearButton = new Button { Text = "Effacer le journal" }; clearButton.Pressed += () => { _clear?.Invoke(); Refresh(); }; actions.AddChild(clearButton);
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); var close = new Button { Text = "Fermer" }; close.Pressed += Hide; actions.AddChild(close); root.AddChild(actions);
    }

    public void Open() { Refresh(); PopupCentered(MinSize); _brand?.Invoke(this); }
    private void Refresh()
    {
        _snapshots = _list?.Invoke() ?? []; _items!.Clear();
        foreach (var item in _snapshots) _items.AddItem($"{item.CreatedAt.ToLocalTime():g} · révision {item.Revision} · {item.ProjectSha256[..12]}");
        if (_snapshots.Count > 0) _items.Select(0);
        _status!.Text = _snapshots.Count == 0 ? "Aucun instantané disponible" : $"{_snapshots.Count} instantané(s) vérifié(s)";
    }
    private void Restore()
    {
        var index = _items!.GetSelectedItems().FirstOrDefault(-1);
        if (index < 0 || index >= _snapshots.Count) return;
        var comparison = _restore?.Invoke(_snapshots[index]);
        _status!.Text = comparison is null ? "✕ Restauration refusée (hash ou format invalide)" :
            $"✓ Copie restaurée · zones {comparison.CurrentAreas}→{comparison.SnapshotAreas} · " +
            $"entités {comparison.CurrentEntities}→{comparison.SnapshotEntities} · " +
            $"blueprints {comparison.CurrentBlueprints}→{comparison.SnapshotBlueprints}";
    }
}
#endif
