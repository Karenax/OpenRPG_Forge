using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Godot;

namespace Forge.Runtime;

public partial class LiveDirectorAccessPanel : CanvasLayer
{
    private readonly Label _status = new() { Name = "Status" };
    private readonly ItemList _capabilities = new() { Name = "Capabilities", CustomMinimumSize = new(680, 180) };
    private readonly HBoxContainer _actions = new() { Name = "Actions" };

    public LiveDirectorAccessPanel()
    {
        Name = "LiveDirectorAccessPanel";
        var layout = new VBoxContainer { Name = "LiveDirectorAccessLayout", Visible = false };
        layout.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        layout.OffsetLeft = -720;
        layout.OffsetTop = 24;
        layout.OffsetRight = -24;
        layout.OffsetBottom = 470;
        layout.AddThemeConstantOverride("separation", 8);
        layout.AddChild(new Label { Text = "LIVE DIRECTOR · ACCÈS & SUPERVISION" });
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
        _status.Text = "Aucun profil Live Director · extension optionnelle ignorée";
        _capabilities.Clear();
        SetMeta("state", "empty");
    }

    public void ShowError(string code)
    {
        _status.Text = $"Erreur Live Director · {code}";
        SetMeta("state", "error");
    }

    public void Configure(LiveDirectorAccessProfile profile, LiveDirectorAccessAcceptanceResult scenario)
    {
        _capabilities.Clear();
        _capabilities.AddItem($"Authentification · GM + MFA · session {profile.Authentication.SessionLifetimeMinutes} min · révocation");
        _capabilities.AddItem($"Examiner · {scenario.InspectionSections} sections en lecture seule · propriété + historique");
        _capabilities.AddItem($"Joueurs · {profile.Players.VisibleColumns.Count} colonnes · latence {scenario.LatencyMs} ms · voix + modération");
        _capabilities.AddItem($"Audit · {profile.Audit.RetentionDays} jours · {scenario.AuditEntries} preuves · chaîne {scenario.AuditVerified}");
        _capabilities.AddItem($"Chat GM · {profile.Chat.Channels.Count} canaux · modération · logs immuables");
        _status.Text = $"Prêt · révision {profile.ServerRevision} · {scenario.PermissionCode} · {scenario.AuditTrail.Count} décisions";
        SetMeta("state", "ready");
    }
}
