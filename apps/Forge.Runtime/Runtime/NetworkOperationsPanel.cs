using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Godot;

namespace Forge.Runtime;

public partial class NetworkOperationsPanel : CanvasLayer
{
    private readonly Label _status = new() { Name = "Status" };
    private readonly ItemList _capabilities = new() { Name = "Capabilities", CustomMinimumSize = new(640, 180) };
    private readonly HBoxContainer _actions = new() { Name = "Actions" };

    public NetworkOperationsPanel()
    {
        Name = "NetworkOperationsPanel";
        var layout = new VBoxContainer { Name = "NetworkOperationsLayout", Visible = false };
        layout.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        layout.OffsetLeft = -680;
        layout.OffsetTop = 24;
        layout.OffsetRight = -24;
        layout.OffsetBottom = 460;
        layout.AddThemeConstantOverride("separation", 8);
        layout.AddChild(new Label { Text = "NETWORK OPERATIONS · RUNTIME" });
        layout.AddChild(_status);
        layout.AddChild(_capabilities);
        layout.AddChild(_actions);
        foreach (var (name, text) in new[]
                 {
                     ("ConfigureButton", "Configurer"), ("PreviewButton", "Prévisualiser"),
                     ("ApplyButton", "Appliquer"), ("UndoButton", "Annuler"),
                     ("RedoButton", "Rétablir"), ("AuditButton", "Journal d’audit")
                 })
            _actions.AddChild(new Button { Name = name, Text = text, FocusMode = Control.FocusModeEnum.All });
        AddChild(layout);
    }

    public void ShowEmpty()
    {
        _status.Text = "Aucun profil opérations · extension optionnelle ignorée";
        _capabilities.Clear();
        SetMeta("state", "empty");
    }

    public void ShowError(string code)
    {
        _status.Text = $"Erreur opérations réseau · {code}";
        SetMeta("state", "error");
    }

    public void Configure(NetworkOperationsProfile profile, NetworkOperationsAcceptanceResult scenario)
    {
        _capabilities.Clear();
        _capabilities.AddItem($"Contenu · {profile.Content.ChunkBytes / 1024} Kio · reprise · delta · confiance");
        _capabilities.AddItem($"Verrou · {profile.VersionLock.ModuleId}@{profile.VersionLock.ModuleVersion} · avant join");
        _capabilities.AddItem($"Accès · {profile.Roles.Grants.Count} rôles · ABAC · capacités custom");
        _capabilities.AddItem($"Chat · {profile.Chat.Channels.Count} canaux · {profile.Chat.LocalRadius:0} m · bridge accessible");
        _capabilities.AddItem($"Événements · {profile.Events.Subscriptions.Count} abonnements · trace · {scenario.BackpressureCode}");
        _status.Text = $"Prêt · révision {profile.ServerRevision} · {scenario.PermissionCode} · audit {scenario.AuditEntries}";
        SetMeta("state", "ready");
    }
}
