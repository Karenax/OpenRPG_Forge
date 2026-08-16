using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableContainerEventsEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly LineEdit _apiVersion;
    private readonly LineEdit _openedBinding;
    private readonly LineEdit _closedBinding;
    private readonly CheckBox _includeActor;
    private readonly CheckBox _includeResult;
    private readonly CheckBox _includePreviousState;
    private readonly CheckBox _includeAuthority;
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

    internal StudioPlaceableContainerEventPreviewMode PreviewMode =>
        _previewMode.Selected == (int)StudioPlaceableContainerEventPreviewMode.Close
            ? StudioPlaceableContainerEventPreviewMode.Close
            : StudioPlaceableContainerEventPreviewMode.Open;

    internal bool IsReady =>
        new[] { _apiVersion.Text, _openedBinding.Text, _closedBinding.Text }
            .All(value => !string.IsNullOrWhiteSpace(value)) &&
        _includeActor.ButtonPressed &&
        _includeResult.ButtonPressed &&
        _includePreviousState.ButtonPressed &&
        _includeAuthority.ButtonPressed;

    internal bool PreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        !_previewResult.Text.Contains("à recalculer", StringComparison.OrdinalIgnoreCase);

    internal string HumanText => string.Join(' ',
        _state.Text,
        "Ouverture Fermeture Version ForgeScript Événement d’ouverture Événement de fermeture " +
        "Acteur Résultat État précédent Autorité Aperçu sans écriture Exiger au runtime",
        _previewResult.Text,
        string.Join(' ', Enumerable.Range(0, _previewMode.ItemCount).Select(_previewMode.GetItemText)));

    internal StudioPlaceableContainerEventsEditor()
    {
        Name = "StudioPlaceableContainerEventsEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("LIAISONS FORGESCRIPT", 10, StudioStyle.Accent));
        _apiVersion = AddLine(this, "Version ForgeScript", "1.0.0");
        _openedBinding = AddLine(this, "Événement publié à l’ouverture", "ContainerOpened");
        _closedBinding = AddLine(this, "Événement publié à la fermeture", "ContainerClosed");

        AddChild(StudioStyle.Label("CONTEXTE TRANSMIS AUX SCRIPTS", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Ces quatre informations forment le contrat canonique et doivent rester présentes.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var payloadGrid = new GridContainer { Columns = 2 };
        payloadGrid.AddThemeConstantOverride("h_separation", 12);
        AddChild(payloadGrid);
        _includeActor = AddCheck(payloadGrid, "Acteur à l’origine de l’action");
        _includeResult = AddCheck(payloadGrid, "Résultat de la tentative");
        _includePreviousState = AddCheck(payloadGrid, "État précédent du conteneur");
        _includeAuthority = AddCheck(payloadGrid, "Autorité d’exécution");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewMode = AddOption(this, "Situation à simuler");
        _previewMode.AddItem("Ouverture réussie");
        _previewMode.AddItem("Fermeture réussie");
        _preview = StudioStyle.Button("Prévisualiser l’événement", compact: true);
        _preview.TooltipText =
            "Afficher la liaison et le contexte transmis sans ouvrir ni fermer un objet de la scène";
        AddChild(_preview);
        _previewResult = StudioStyle.Label("Aucun événement prévisualisé.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger ces événements au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, leur absence produit un diagnostic stable au lieu d’être ignorée.";

        _apiVersion.TextChanged += _ => NotifyChanged();
        _openedBinding.TextChanged += _ => NotifyChanged();
        _closedBinding.TextChanged += _ => NotifyChanged();
        _includeActor.Toggled += _ => NotifyChanged();
        _includeResult.Toggled += _ => NotifyChanged();
        _includePreviousState.Toggled += _ => NotifyChanged();
        _includeAuthority.Toggled += _ => NotifyChanged();
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

    internal void Load(
        StudioPlaceableContainerEventsDraft draft,
        bool enabled,
        bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _apiVersion.Text = draft.ForgeScriptApiVersion;
        _openedBinding.Text = draft.OpenedBinding;
        _closedBinding.Text = draft.ClosedBinding;
        _includeActor.ButtonPressed = draft.IncludeActor;
        _includeResult.ButtonPressed = draft.IncludeResult;
        _includePreviousState.ButtonPressed = draft.IncludePreviousState;
        _includeAuthority.ButtonPressed = draft.IncludeAuthority;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewMode.Select((int)StudioPlaceableContainerEventPreviewMode.Open);
        _previewResult.Text = "Aucun événement prévisualisé.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        _loading = false;
    }

    internal StudioPlaceableContainerEventsDraft Capture(StudioPlaceableContainerEventsDraft source) =>
        source with
        {
            ForgeScriptApiVersion = _apiVersion.Text,
            OpenedBinding = _openedBinding.Text,
            ClosedBinding = _closedBinding.Text,
            IncludeActor = _includeActor.ButtonPressed,
            IncludeResult = _includeResult.ButtonPressed,
            IncludePreviousState = _includePreviousState.ButtonPressed,
            IncludeAuthority = _includeAuthority.ButtonPressed,
        };

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = _existingProfile
            ? "Événements canoniques existants · les pièges, trésors et événements voisins seront préservés."
            : enabled
                ? "Nouveaux événements prêts · l’aperçu ne change aucun état de la scène."
                : "État vide · activez Ouverture / fermeture pour préparer les deux événements.";
        _state.AddThemeColorOverride("font_color", _existingProfile || enabled
            ? StudioStyle.Success
            : StudioStyle.Warning);
    }

    internal void FocusBindings() => _openedBinding.GrabFocus();

    internal void FocusPayload() => _includeActor.GrabFocus();

    internal void FocusPreview() => _previewMode.GrabFocus();

    internal void SetEvidenceValues(string mode)
    {
        _loading = true;
        _apiVersion.Text = "1.1.0";
        _openedBinding.Text = "CoffreOuvert";
        _closedBinding.Text = "CoffreFerme";
        _includeActor.ButtonPressed = true;
        _includeResult.ButtonPressed = true;
        _includePreviousState.ButtonPressed = true;
        _includeAuthority.ButtonPressed = true;
        _runtimeRequired.ButtonPressed = true;
        _previewMode.Select((int)(mode.Contains("close", StringComparison.OrdinalIgnoreCase)
            ? StudioPlaceableContainerEventPreviewMode.Close
            : StudioPlaceableContainerEventPreviewMode.Open));
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableContainerEventPreview? result)
    {
        _previewResult.Text = result?.Summary ?? "Aucun événement prévisualisé.";
        _previewResult.AddThemeColorOverride(
            "font_color", result?.Succeeded == true ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void PreviewInputChanged()
    {
        if (!_loading)
        {
            _previewResult.Text = "Aperçu à recalculer · aucun état n’a été modifié.";
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
