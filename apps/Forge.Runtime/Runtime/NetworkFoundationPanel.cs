using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Godot;

namespace Forge.Runtime;

public partial class NetworkFoundationPanel : CanvasLayer
{
    private readonly Label _status = new() { Name = "Status" };
    private readonly ItemList _capabilities = new() { Name = "Capabilities", CustomMinimumSize = new(560, 160) };
    private readonly HBoxContainer _actions = new() { Name = "Actions" };

    public NetworkFoundationPanel()
    {
        Name = "NetworkFoundationPanel";
        var layout = new VBoxContainer { Name = "NetworkFoundationLayout", Visible = false };
        layout.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        layout.OffsetLeft = -600;
        layout.OffsetTop = 24;
        layout.OffsetRight = -24;
        layout.OffsetBottom = 430;
        layout.AddThemeConstantOverride("separation", 8);
        layout.AddChild(new Label { Text = "NETWORK FOUNDATION · RUNTIME" });
        layout.AddChild(_status);
        layout.AddChild(_capabilities);
        layout.AddChild(_actions);
        foreach (var (name, text) in new[]
                 {
                     ("ConfigureButton", "Configurer"), ("PreviewButton", "Prévisualiser"),
                     ("ApplyButton", "Appliquer"), ("UndoButton", "Annuler"),
                     ("RedoButton", "Rétablir"), ("AuditButton", "Journal d’audit")
                 })
        {
            _actions.AddChild(new Button { Name = name, Text = text, FocusMode = Control.FocusModeEnum.All });
        }
        AddChild(layout);
    }

    public void ShowEmpty()
    {
        _status.Text = "Aucun profil réseau · extension optionnelle ignorée";
        _capabilities.Clear();
        SetMeta("state", "empty");
    }

    public void ShowError(string code)
    {
        _status.Text = $"Erreur réseau · {code}";
        SetMeta("state", "error");
    }

    public void Configure(NetworkFoundationProfile profile, NetworkFoundationAcceptanceResult scenario)
    {
        _capabilities.Clear();
        _capabilities.AddItem($"Serveur dédié · {profile.Dedicated.ServiceMode} · {profile.Dedicated.HealthCheckPath}");
        _capabilities.AddItem("Autorité · intentions validées · état répliqué · audit");
        _capabilities.AddItem($"Groupe · {scenario.PartyMembers} membres · loot {profile.Party.Loot} · partage quêtes/marqueurs");
        _capabilities.AddItem($"Réplication · {profile.Replication.Components.Count} composants · {scenario.PredictionCode}");
        _capabilities.AddItem("Intérêt · zone · distance · visibilité · groupe · propriété · explicite");
        _status.Text = $"Prêt · révision {profile.ServerRevision} · {scenario.PermissionCode} · audit {scenario.AuditEntries}";
        SetMeta("state", "ready");
    }
}
