using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Godot;

namespace Forge.Runtime;

public partial class NetworkAssurancePanel : CanvasLayer
{
    private readonly Label _status = new() { Name = "Status" };
    private readonly ItemList _capabilities = new() { Name = "Capabilities", CustomMinimumSize = new(640, 130) };
    private readonly HBoxContainer _actions = new() { Name = "Actions" };

    public NetworkAssurancePanel()
    {
        Name = "NetworkAssurancePanel";
        var layout = new VBoxContainer { Name = "NetworkAssuranceLayout", Visible = false };
        layout.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        layout.OffsetLeft = -680;
        layout.OffsetTop = 24;
        layout.OffsetRight = -24;
        layout.OffsetBottom = 410;
        layout.AddThemeConstantOverride("separation", 8);
        layout.AddChild(new Label { Text = "NETWORK ASSURANCE · RUNTIME" });
        layout.AddChild(_status);
        layout.AddChild(_capabilities);
        layout.AddChild(_actions);
        foreach (var (name, text) in new[]
                 {
                     ("ConfigureButton", "Configurer"), ("PreviewButton", "Prévisualiser"),
                     ("ApplyButton", "Appliquer"), ("UndoButton", "Annuler"),
                     ("RedoButton", "Rétablir"), ("AuditButton", "Journal d'audit")
                 })
            _actions.AddChild(new Button { Name = name, Text = text, FocusMode = Control.FocusModeEnum.All });
        AddChild(layout);
    }

    public void ShowEmpty()
    {
        _status.Text = "Aucun profil assurance · extension optionnelle ignorée";
        _capabilities.Clear();
        SetMeta("state", "empty");
    }

    public void ShowError(string code)
    {
        _status.Text = $"Erreur assurance réseau · {code}";
        SetMeta("state", "error");
    }

    public void Configure(NetworkAssuranceProfile profile, NetworkAssuranceAcceptanceResult scenario)
    {
        _capabilities.Clear();
        _capabilities.AddItem($"Logs · {profile.Logs.OutputFormat} · {profile.Logs.RotationFiles} segments · {scenario.RemoteDeliveries} sink(s)");
        _capabilities.AddItem($"Anti-cheat · signatures · RNG serveur · seuil {profile.AntiCheat.AnomalyThreshold} · sauvegardes client refusées");
        _capabilities.AddItem($"Tests · {profile.DeterministicTests.ClientCount} clients · {profile.DeterministicTests.ReplayPasses} replays · {scenario.StateAssertions} assertions");
        _status.Text = $"Prêt · révision {profile.ServerRevision} · {scenario.TestCode} · audit {scenario.Audit.Count}";
        SetMeta("state", "ready");
    }
}
