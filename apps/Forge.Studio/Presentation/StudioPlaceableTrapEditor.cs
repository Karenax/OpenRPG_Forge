using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableTrapEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly LineEdit _trigger;
    private readonly LineEdit _payload;
    private readonly SpinBox _detection;
    private readonly SpinBox _disarm;
    private readonly SpinBox _resetDelay;
    private readonly LineEdit _owner;
    private readonly LineEdit _visualEffect;
    private readonly LineEdit _friendlyPolicy;
    private readonly CheckBox _armed;
    private readonly CheckBox _oneShot;
    private readonly CheckBox _runtimeRequired;
    private readonly OptionButton _previewMode;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private bool _existingProfile;
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal event Action? PreviewRequested;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal StudioPlaceableTrapPreviewMode PreviewMode =>
        _previewMode.Selected >= 0 &&
        _previewMode.Selected < Enum.GetValues<StudioPlaceableTrapPreviewMode>().Length
            ? (StudioPlaceableTrapPreviewMode)_previewMode.Selected
            : StudioPlaceableTrapPreviewMode.DetectSuccess;

    internal bool IsReady =>
        new[] { _trigger.Text, _payload.Text, _owner.Text, _visualEffect.Text, _friendlyPolicy.Text }
            .All(value => !string.IsNullOrWhiteSpace(value)) &&
        _detection.Value >= 0 && _disarm.Value >= 0 && _resetDelay.Value >= 0;

    internal bool PreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        !_previewResult.Text.Contains("à recalculer", StringComparison.OrdinalIgnoreCase);

    internal string HumanText => string.Join(' ',
        _state.Text,
        "Piège Déclencheur Effet Détection Désamorçage Réarmement Propriétaire Alliés VFX Armé Usage unique Aperçu runtime",
        _previewResult.Text,
        string.Join(' ', Enumerable.Range(0, _previewMode.ItemCount).Select(_previewMode.GetItemText)));

    internal StudioPlaceableTrapEditor()
    {
        Name = "StudioPlaceableTrapEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("DÉCLENCHEMENT ET EFFET", 10, StudioStyle.Accent));
        _trigger = AddLine(this, "Quand le piège réagit", "interaction:open");
        _payload = AddLine(this, "Effet appliqué", "damage:physical");
        _detection = AddSpin(this, "Difficulté de détection", 0, 1000, 1);
        _disarm = AddSpin(this, "Difficulté de désamorçage", 0, 1000, 1);
        _resetDelay = AddSpin(this, "Délai de réarmement (secondes)", 0, 86400, 1);

        AddChild(StudioStyle.Label("APPARTENANCE ET PRÉSENTATION", 10, StudioStyle.Accent));
        _owner = AddLine(this, "Propriétaire", "module.owner");
        _friendlyPolicy = AddLine(this, "Faction alliée ignorée", "module.owner");
        _visualEffect = AddLine(this, "Effet visuel", "vfx:trap");
        _armed = AddCheck(this, "Armé au chargement");
        _oneShot = AddCheck(this, "Usage unique après déclenchement");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewMode = AddOption(this, "Situation à simuler");
        foreach (var mode in Enum.GetValues<StudioPlaceableTrapPreviewMode>())
        {
            _previewMode.AddItem(HumanPreviewMode(mode));
        }
        _preview = StudioStyle.Button("Prévisualiser le piège", compact: true);
        _preview.TooltipText = "Évaluer la détection, le désamorçage ou le déclenchement sans modifier le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label("Aucun piège prévisualisé.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger ce piège au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, un piège absent produit un diagnostic stable au lieu d’être ignoré.";

        _trigger.TextChanged += _ => NotifyChanged();
        _payload.TextChanged += _ => NotifyChanged();
        _detection.ValueChanged += _ => NotifyChanged();
        _disarm.ValueChanged += _ => NotifyChanged();
        _resetDelay.ValueChanged += _ => NotifyChanged();
        _owner.TextChanged += _ => NotifyChanged();
        _visualEffect.TextChanged += _ => NotifyChanged();
        _friendlyPolicy.TextChanged += _ => NotifyChanged();
        _armed.Toggled += _ => NotifyChanged();
        _oneShot.Toggled += _ => NotifyChanged();
        _previewMode.ItemSelected += _ => PreviewInputChanged();
        _preview.Pressed += () => PreviewRequested?.Invoke();
        _runtimeRequired.Toggled += value =>
        {
            if (!_loading)
            {
                RuntimeRequirementChanged?.Invoke(value);
                Changed?.Invoke();
            }
        };
    }

    internal void Load(StudioPlaceableTrapDraft draft, bool enabled, bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _trigger.Text = draft.TriggerId;
        _payload.Text = draft.PayloadId;
        _detection.Value = draft.DetectionDifficulty;
        _disarm.Value = draft.DisarmDifficulty;
        _resetDelay.Value = draft.ResetDelaySeconds;
        _owner.Text = draft.OwnerId;
        _visualEffect.Text = draft.VisualEffectId;
        _friendlyPolicy.Text = draft.FriendlyPolicy;
        _armed.ButtonPressed = draft.Armed;
        _oneShot.ButtonPressed = draft.OneShot;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewMode.Select((int)StudioPlaceableTrapPreviewMode.DetectSuccess);
        _previewResult.Text = "Aucun piège prévisualisé.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        _loading = false;
    }

    internal StudioPlaceableTrapDraft Capture(StudioPlaceableTrapDraft source) => source with
    {
        TriggerId = _trigger.Text,
        PayloadId = _payload.Text,
        DetectionDifficulty = _detection.Value,
        DisarmDifficulty = _disarm.Value,
        ResetDelaySeconds = _resetDelay.Value,
        OwnerId = _owner.Text,
        VisualEffectId = _visualEffect.Text,
        FriendlyPolicy = _friendlyPolicy.Text,
        Armed = _armed.ButtonPressed,
        OneShot = _oneShot.ButtonPressed,
    };

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = _existingProfile
            ? "Piège canonique existant · les autres événements du placeable seront préservés."
            : enabled
                ? "Nouveau piège prêt · l’aperçu ne déclenche aucun effet dans la scène."
                : "État vide · activez Piège pour préparer sa politique.";
        _state.AddThemeColorOverride("font_color", _existingProfile || enabled
            ? StudioStyle.Success
            : StudioStyle.Warning);
    }

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusTrap() => _trigger.GrabFocus();

    internal void FocusDifficulties() => _detection.GetLineEdit().GrabFocus();

    internal void FocusOwnership() => _owner.GrabFocus();

    internal void FocusPreview() => _previewMode.GrabFocus();

    internal void SetEvidenceValues(string mode)
    {
        _loading = true;
        _trigger.Text = mode.Contains("contact", StringComparison.OrdinalIgnoreCase)
            ? "contact:enter"
            : "interaction:open";
        _payload.Text = "damage:thorn-burst";
        _detection.Value = 14;
        _disarm.Value = 16;
        _resetDelay.Value = 45;
        _owner.Text = "gardiens-forestiers";
        _friendlyPolicy.Text = "gardiens-forestiers";
        _visualEffect.Text = "vfx:thorn-burst";
        _armed.ButtonPressed = true;
        _oneShot.ButtonPressed = mode.Contains("one-shot", StringComparison.OrdinalIgnoreCase);
        _runtimeRequired.ButtonPressed = true;
        _previewMode.Select((int)(mode.Contains("friendly", StringComparison.OrdinalIgnoreCase)
            ? StudioPlaceableTrapPreviewMode.TriggerFriendly
            : mode.Contains("disarm", StringComparison.OrdinalIgnoreCase)
                ? StudioPlaceableTrapPreviewMode.DisarmSuccess
                : mode.Contains("trigger", StringComparison.OrdinalIgnoreCase)
                    ? StudioPlaceableTrapPreviewMode.TriggerHostile
                    : StudioPlaceableTrapPreviewMode.DetectSuccess));
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableTrapPreview? result)
    {
        _previewResult.Text = result?.Summary ?? "Aucun piège prévisualisé.";
        _previewResult.AddThemeColorOverride(
            "font_color", result?.Succeeded == true && result.OperationSucceeded
                ? StudioStyle.Success
                : StudioStyle.Warning);
    }

    private void PreviewInputChanged()
    {
        if (!_loading)
        {
            _previewResult.Text = "Aperçu à recalculer · aucun effet n’a été déclenché.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        }
    }

    private void NotifyChanged()
    {
        if (!_loading)
        {
            PreviewInputChanged();
            Changed?.Invoke();
        }
    }

    private static string HumanPreviewMode(StudioPlaceableTrapPreviewMode mode) => mode switch
    {
        StudioPlaceableTrapPreviewMode.DetectHidden => "Chercher avec une perception insuffisante",
        StudioPlaceableTrapPreviewMode.DetectSuccess => "Chercher avec une perception suffisante",
        StudioPlaceableTrapPreviewMode.DisarmFailure => "Désamorcer avec une compétence insuffisante",
        StudioPlaceableTrapPreviewMode.DisarmSuccess => "Désamorcer avec une compétence suffisante",
        StudioPlaceableTrapPreviewMode.TriggerFriendly => "Déclencher avec un allié",
        StudioPlaceableTrapPreviewMode.TriggerHostile => "Déclencher avec un adversaire",
        StudioPlaceableTrapPreviewMode.Reset => "Réarmer après déclenchement",
        _ => "Situation du piège",
    };

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    private static OptionButton AddOption(Container parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        parent.AddChild(option);
        return option;
    }

    private static SpinBox AddSpin(Container parent, string label, double minimum, double maximum, double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        parent.AddChild(spin);
        return spin;
    }

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }
}
