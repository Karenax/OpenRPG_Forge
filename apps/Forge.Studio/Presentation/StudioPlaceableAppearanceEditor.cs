using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableAppearanceEditor : VBoxContainer
{
    private readonly LineEdit _meshes;
    private readonly LineEdit _materials;
    private readonly LineEdit _icon;
    private readonly LineEdit _portrait;
    private readonly LineEdit _visualEffects;
    private bool _loading;

    internal event Action? Changed;

    internal bool IsReady =>
        !string.IsNullOrWhiteSpace(_meshes.Text) &&
        !string.IsNullOrWhiteSpace(_icon.Text) &&
        !string.IsNullOrWhiteSpace(_portrait.Text);

    internal string HumanText =>
        $"{_meshes.Text} {_materials.Text} {_icon.Text} {_portrait.Text} {_visualEffects.Text}";

    internal StudioPlaceableAppearanceEditor()
    {
        AddThemeConstantOverride("separation", 4);
        _meshes = AddLine("Meshes", "mesh:statue-principale, mesh:socle");
        _materials = AddLine("Matériaux", "material:pierre, material:runes");
        _icon = AddLine("Icône", "asset:statue-icon");
        _portrait = AddLine("Portrait", "asset:statue-portrait");
        _visualEffects = AddLine("Effets visuels", "vfx:runes-lueur");
        foreach (var line in new[] { _meshes, _materials, _icon, _portrait, _visualEffects })
        {
            line.TextChanged += _ =>
            {
                if (!_loading)
                {
                    Changed?.Invoke();
                }
            };
        }
    }

    internal void Load(StudioPlaceableAppearanceDraft appearance)
    {
        _loading = true;
        _meshes.Text = appearance.MeshIdsText;
        _materials.Text = appearance.MaterialIdsText;
        _icon.Text = appearance.IconAssetId;
        _portrait.Text = appearance.PortraitAssetId;
        _visualEffects.Text = appearance.VisualEffectIdsText;
        _loading = false;
    }

    internal StudioPlaceableAppearanceDraft Capture() => new(
        _meshes.Text,
        _materials.Text,
        _icon.Text,
        _portrait.Text,
        _visualEffects.Text);

    internal void SetEvidenceValues(
        string meshes,
        string materials,
        string icon,
        string portrait,
        string visualEffects) => Load(new(meshes, materials, icon, portrait, visualEffects));

    internal void FocusMeshes() => _meshes.GrabFocus();

    internal void FocusPortrait() => _portrait.GrabFocus();

    private LineEdit AddLine(string label, string placeholder)
    {
        AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        AddChild(line);
        return line;
    }
}
