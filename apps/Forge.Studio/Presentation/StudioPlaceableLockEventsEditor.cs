using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableLockEventsEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly LineEdit _apiVersion;
    private readonly LineEdit _changingBinding;
    private readonly LineEdit _changedBinding;
    private readonly CheckBox _includeMethod;
    private readonly CheckBox _includeCredential;
    private readonly CheckBox _includeActor;
    private readonly CheckBox _controlledVeto;
    private readonly OptionButton _previewMode;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private bool _existingProfile;
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal event Action? PreviewRequested;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal StudioPlaceableLockEventPreviewMode PreviewMode =>
        _previewMode.Selected == (int)StudioPlaceableLockEventPreviewMode.ControlledVeto
            ? StudioPlaceableLockEventPreviewMode.ControlledVeto
            : StudioPlaceableLockEventPreviewMode.AcceptedTransition;

    internal bool IsReady =>
        new[] { _apiVersion.Text, _changingBinding.Text, _changedBinding.Text }
            .All(value => !string.IsNullOrWhiteSpace(value)) &&
        _includeMethod.ButtonPressed &&
        _includeCredential.ButtonPressed &&
        _includeActor.ButtonPressed &&
        _controlledVeto.ButtonPressed;

    internal bool PreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        !_previewResult.Text.Contains("à recalculer", StringComparison.OrdinalIgnoreCase);

    internal string HumanText => string.Join(' ',
        _state.Text,
        "Verrouillage Déverrouillage Version ForgeScript Avant le changement Après le changement " +
        "Méthode Justificatif Acteur Veto contrôlé Transition acceptée Aperçu sans écriture Exiger au runtime",
        _previewResult.Text,
        string.Join(' ', Enumerable.Range(0, _previewMode.ItemCount).Select(_previewMode.GetItemText)));

    internal StudioPlaceableLockEventsEditor()
    {
        Name = "StudioPlaceableLockEventsEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("LIAISONS FORGESCRIPT", 10, StudioStyle.Accent));
        _apiVersion = AddLine(this, "Version ForgeScript", "1.0.0");
        _changingBinding = AddLine(this, "Événement avant le changement", "LockStateChanging");
        _changedBinding = AddLine(this, "Événement après le changement", "LockStateChanged");

        AddChild(StudioStyle.Label("CONTEXTE TRANSMIS AUX SCRIPTS", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Ces quatre garanties forment le contrat canonique et doivent rester présentes.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var payloadGrid = new GridContainer { Columns = 2 };
        payloadGrid.AddThemeConstantOverride("h_separation", 12);
        AddChild(payloadGrid);
        _includeMethod = AddCheck(payloadGrid, "Méthode employée");
        _includeCredential = AddCheck(payloadGrid, "Justificatif présenté");
        _includeActor = AddCheck(payloadGrid, "Acteur à l’origine de l’action");
        _controlledVeto = AddCheck(payloadGrid, "Autoriser un veto contrôlé");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewMode = AddOption(this, "Situation à simuler");
        _previewMode.AddItem("Transition acceptée");
        _previewMode.AddItem("Veto contrôlé avant changement");
        _preview = StudioStyle.Button("Prévisualiser le changement", compact: true);
        _preview.TooltipText =
            "Afficher les événements avant et après sans modifier le verrou d’un objet de la scène";
        AddChild(_preview);
        _previewResult = StudioStyle.Label("Aucun changement prévisualisé.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger ces événements au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, leur absence produit un diagnostic stable au lieu d’être ignorée.";

        _apiVersion.TextChanged += _ => NotifyChanged();
        _changingBinding.TextChanged += _ => NotifyChanged();
        _changedBinding.TextChanged += _ => NotifyChanged();
        _includeMethod.Toggled += _ => NotifyChanged();
        _includeCredential.Toggled += _ => NotifyChanged();
        _includeActor.Toggled += _ => NotifyChanged();
        _controlledVeto.Toggled += _ => NotifyChanged();
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

    internal void Load(StudioPlaceableLockEventsDraft draft, bool enabled, bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _apiVersion.Text = draft.ForgeScriptApiVersion;
        _changingBinding.Text = draft.ChangingBinding;
        _changedBinding.Text = draft.ChangedBinding;
        _includeMethod.ButtonPressed = draft.IncludeMethod;
        _includeCredential.ButtonPressed = draft.IncludeCredential;
        _includeActor.ButtonPressed = draft.IncludeActor;
        _controlledVeto.ButtonPressed = draft.ControlledVeto;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewMode.Select((int)StudioPlaceableLockEventPreviewMode.AcceptedTransition);
        _previewResult.Text = "Aucun changement prévisualisé.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        _loading = false;
    }

    internal StudioPlaceableLockEventsDraft Capture(StudioPlaceableLockEventsDraft source) => source with
    {
        ForgeScriptApiVersion = _apiVersion.Text,
        ChangingBinding = _changingBinding.Text,
        ChangedBinding = _changedBinding.Text,
        IncludeMethod = _includeMethod.ButtonPressed,
        IncludeCredential = _includeCredential.ButtonPressed,
        IncludeActor = _includeActor.ButtonPressed,
        ControlledVeto = _controlledVeto.ButtonPressed,
    };

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = _existingProfile
            ? "Événements canoniques existants · les pièges, trésors et événements voisins seront préservés."
            : enabled
                ? "Nouveaux événements prêts · l’aperçu ne change aucun verrou de la scène."
                : "État vide · activez Verrouillage / déverrouillage pour préparer les événements.";
        _state.AddThemeColorOverride("font_color", _existingProfile || enabled
            ? StudioStyle.Success
            : StudioStyle.Warning);
    }

    internal void FocusBindings() => _changingBinding.GrabFocus();

    internal void FocusPayload() => _includeMethod.GrabFocus();

    internal void FocusPreview() => _previewMode.GrabFocus();

    internal void SetEvidenceValues(string mode)
    {
        _loading = true;
        _apiVersion.Text = "1.1.0";
        _changingBinding.Text = "VerrouAvant";
        _changedBinding.Text = "VerrouApres";
        _includeMethod.ButtonPressed = true;
        _includeCredential.ButtonPressed = true;
        _includeActor.ButtonPressed = true;
        _controlledVeto.ButtonPressed = true;
        _runtimeRequired.ButtonPressed = true;
        _previewMode.Select((int)(mode.Contains("veto", StringComparison.OrdinalIgnoreCase)
            ? StudioPlaceableLockEventPreviewMode.ControlledVeto
            : StudioPlaceableLockEventPreviewMode.AcceptedTransition));
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableLockEventPreview? result)
    {
        _previewResult.Text = result?.Summary ?? "Aucun changement prévisualisé.";
        _previewResult.AddThemeColorOverride(
            "font_color", result?.Succeeded == true ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void PreviewInputChanged()
    {
        if (!_loading)
        {
            _previewResult.Text = "Aperçu à recalculer · aucun verrou n’a été modifié.";
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

    private static CheckBox AddCheck(Container parent, string label)
    {
        var check = new CheckBox { Text = label, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }
}
