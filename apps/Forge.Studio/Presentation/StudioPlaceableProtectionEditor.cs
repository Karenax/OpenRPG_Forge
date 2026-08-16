using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableProtectionEditor : VBoxContainer
{
    private sealed class ExtraValueRow(
        PanelContainer card,
        LineEdit value,
        Button remove)
    {
        internal PanelContainer Card { get; } = card;
        internal LineEdit Value { get; } = value;
        internal Button Remove { get; } = remove;
    }

    private static readonly (string Id, string Label)[] KnownActions =
    [
        ("destroy", "Empêcher la destruction"),
        ("move", "Empêcher le déplacement"),
        ("use", "Empêcher l’utilisation"),
        ("container.open", "Empêcher l’ouverture"),
        ("lock.change", "Empêcher le changement de verrou"),
    ];

    private static readonly (string Id, string Label)[] KnownPermissions =
    [
        ("placeable.plot.override", "Responsable du scénario"),
        ("placeable.maintain", "Maintenance du module"),
        ("placeable.admin", "Administration du module"),
    ];

    private readonly Label _state;
    private readonly List<(string Id, CheckBox Check)> _actionChecks = [];
    private readonly VBoxContainer _extraActionsHost;
    private readonly Label _extraActionsEmpty;
    private readonly Button _addAction;
    private readonly List<ExtraValueRow> _extraActions = [];
    private readonly List<(string Id, CheckBox Check)> _permissionChecks = [];
    private readonly VBoxContainer _extraPermissionsHost;
    private readonly Label _extraPermissionsEmpty;
    private readonly Button _addPermission;
    private readonly List<ExtraValueRow> _extraPermissions = [];
    private readonly LineEdit _feedback;
    private readonly CheckBox _allowGameMasterOverride;
    private readonly Label _auditState;
    private readonly OptionButton _previewAction;
    private readonly List<string> _previewActionIds = [];
    private readonly CheckBox _previewAllPermissions;
    private readonly CheckBox _previewGameMasterOverride;
    private readonly LineEdit _previewReason;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private string _auditBinding = "PlaceableProtectionOverride";
    private bool _existingProfile;
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal bool IsReady
    {
        get
        {
            var actions = CaptureActions();
            var permissions = CapturePermissions();
            return Visible && actions.Count > 0 && permissions.Count > 0 &&
                   actions.All(value => !string.IsNullOrWhiteSpace(value)) &&
                   permissions.All(value => !string.IsNullOrWhiteSpace(value)) &&
                   actions.Distinct(StringComparer.Ordinal).Count() == actions.Count &&
                   permissions.Distinct(StringComparer.Ordinal).Count() == permissions.Count &&
                   !string.IsNullOrWhiteSpace(_feedback.Text) &&
                   !string.IsNullOrWhiteSpace(_auditBinding);
        }
    }

    internal bool PolicyPreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        (_previewResult.Text.Contains("bloquée", StringComparison.OrdinalIgnoreCase) ||
         _previewResult.Text.Contains("autorisée", StringComparison.OrdinalIgnoreCase));

    internal string HumanText => string.Join(' ',
        new[]
        {
            _state.Text,
            "Protection plot Actions protégées Droits requis Feedback Override maître de jeu Journal d’audit Aperçu joueur permissions motif runtime",
            _feedback.Text,
            _auditState.Text,
            _previewResult.Text,
        }.Concat(_extraActions.Select(row => row.Value.Text))
            .Concat(_extraPermissions.Select(row => row.Value.Text)));

    internal StudioPlaceableProtectionEditor()
    {
        Name = "StudioPlaceableProtectionEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("ACTIONS PROTÉGÉES", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Chaque action cochée est refusée au joueur tant que tous les droits requis ne sont pas présents.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var actions = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("h_separation", 10);
        AddChild(actions);
        foreach (var item in KnownActions)
        {
            var check = AddCheck(actions, item.Label);
            _actionChecks.Add((item.Id, check));
            check.Toggled += _ => PolicyChanged();
        }
        _extraActionsEmpty = StudioStyle.Label(
            "Aucune action personnalisée.", 10, StudioStyle.Muted, wrap: true);
        AddChild(_extraActionsEmpty);
        _extraActionsHost = new VBoxContainer();
        _extraActionsHost.AddThemeConstantOverride("separation", 5);
        AddChild(_extraActionsHost);
        _addAction = StudioStyle.Button("Ajouter une action personnalisée", compact: true);
        _addAction.TooltipText = "Conserver ou ajouter une action métier qui ne figure pas dans les choix usuels";
        AddChild(_addAction);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("DROITS DE CONTOURNEMENT", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Tous les droits cochés sont nécessaires. Une dérogation MJ reste séparée et toujours journalisée.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var permissions = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        permissions.AddThemeConstantOverride("h_separation", 10);
        AddChild(permissions);
        foreach (var item in KnownPermissions)
        {
            var check = AddCheck(permissions, item.Label);
            _permissionChecks.Add((item.Id, check));
            check.Toggled += _ => PolicyChanged();
        }
        _extraPermissionsEmpty = StudioStyle.Label(
            "Aucun droit personnalisé.", 10, StudioStyle.Muted, wrap: true);
        AddChild(_extraPermissionsEmpty);
        _extraPermissionsHost = new VBoxContainer();
        _extraPermissionsHost.AddThemeConstantOverride("separation", 5);
        AddChild(_extraPermissionsHost);
        _addPermission = StudioStyle.Button("Ajouter un droit personnalisé", compact: true);
        _addPermission.TooltipText = "Conserver ou ajouter un droit métier nommé";
        AddChild(_addPermission);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("RETOUR JOUEUR ET DÉROGATION MJ", 10, StudioStyle.Accent));
        _feedback = AddLine(this, "Message lorsque l’action est refusée", "Cet objet est protégé par le scénario.");
        _allowGameMasterOverride = AddCheck(this, "Autoriser une dérogation du maître de jeu");
        _auditState = StudioStyle.Label(
            "Journal d’audit gouverné · acteur, action et motif sont enregistrés pour chaque dérogation MJ.",
            10,
            StudioStyle.Success,
            wrap: true);
        _auditState.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_auditState);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewAction = AddOption(this, "Action tentée");
        _previewAllPermissions = AddCheck(this, "Accorder tous les droits requis pour cet aperçu");
        _previewGameMasterOverride = AddCheck(this, "Simuler une dérogation du maître de jeu");
        _previewReason = AddLine(this, "Motif de la dérogation", "Restaurer la mise en scène");
        _preview = StudioStyle.Button("Prévisualiser la décision", compact: true);
        _preview.TooltipText = "Calculer autorisation, feedback et trace d’audit sans modifier le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label(
            "Aucune décision prévisualisée.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger cette politique de protection au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, l’absence des opérations produit un diagnostic au lieu d’un repli optionnel.";

        _addAction.Pressed += () =>
        {
            AddExtraRow(_extraActionsHost, _extraActions, "action.personnalisee");
            UpdateEmptyStates();
            PolicyChanged();
        };
        _addPermission.Pressed += () =>
        {
            AddExtraRow(_extraPermissionsHost, _extraPermissions, "placeable.droit.personnalise");
            UpdateEmptyStates();
            PolicyChanged();
        };
        _feedback.TextChanged += _ => NotifyChanged();
        _allowGameMasterOverride.Toggled += _ =>
        {
            UpdatePreviewControls();
            NotifyChanged();
        };
        _previewAction.ItemSelected += _ => PreviewInputChanged();
        _previewAllPermissions.Toggled += _ => PreviewInputChanged();
        _previewGameMasterOverride.Toggled += _ =>
        {
            UpdatePreviewControls();
            PreviewInputChanged();
        };
        _previewReason.TextChanged += _ => PreviewInputChanged();
        _preview.Pressed += PreviewDecision;
        _runtimeRequired.Toggled += value =>
        {
            if (!_loading)
            {
                RuntimeRequirementChanged?.Invoke(value);
                Changed?.Invoke();
            }
        };
    }

    internal void Load(StudioPlaceableProtectionDraft draft, bool enabled, bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _auditBinding = string.IsNullOrWhiteSpace(draft.AuditBinding)
            ? "PlaceableProtectionOverride"
            : draft.AuditBinding;
        foreach (var item in _actionChecks)
        {
            item.Check.ButtonPressed = draft.ProtectedActions.Contains(item.Id, StringComparer.Ordinal);
        }
        ClearRows(_extraActionsHost, _extraActions);
        foreach (var action in draft.ProtectedActions.Where(value =>
                     KnownActions.All(known => !string.Equals(known.Id, value, StringComparison.Ordinal))))
        {
            AddExtraRow(_extraActionsHost, _extraActions, action);
        }
        foreach (var item in _permissionChecks)
        {
            item.Check.ButtonPressed = draft.RequiredPermissions.Contains(item.Id, StringComparer.Ordinal);
        }
        ClearRows(_extraPermissionsHost, _extraPermissions);
        foreach (var permission in draft.RequiredPermissions.Where(value =>
                     KnownPermissions.All(known => !string.Equals(known.Id, value, StringComparison.Ordinal))))
        {
            AddExtraRow(_extraPermissionsHost, _extraPermissions, permission);
        }
        _feedback.Text = draft.Feedback;
        _allowGameMasterOverride.ButtonPressed = draft.AllowGameMasterOverride;
        _previewAllPermissions.ButtonPressed = false;
        _previewGameMasterOverride.ButtonPressed = false;
        _previewReason.Text = "Restaurer la mise en scène";
        _previewResult.Text = "Aucune décision prévisualisée.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        UpdateEnabledState(enabled);
        UpdateEmptyStates();
        RebuildPreviewActions();
        UpdatePreviewControls();
        _loading = false;
    }

    internal void SetEnabledState(bool enabled) => UpdateEnabledState(enabled);

    internal StudioPlaceableProtectionDraft Capture(StudioPlaceableProtectionDraft source) => source with
    {
        ProtectedActions = CaptureActions(),
        RequiredPermissions = CapturePermissions(),
        Feedback = _feedback.Text,
        AllowGameMasterOverride = _allowGameMasterOverride.ButtonPressed,
        AuditBinding = _auditBinding,
    };

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusFirstAction() => _actionChecks[0].Check.GrabFocus();

    internal void FocusPermissions() => _permissionChecks[0].Check.GrabFocus();

    internal void FocusOverride() => _allowGameMasterOverride.GrabFocus();

    internal void SetEvidenceValues(bool gameMasterOverridePreview)
    {
        _loading = true;
        foreach (var item in _actionChecks)
        {
            item.Check.ButtonPressed = item.Id is "destroy" or "move" or "use";
        }
        ClearRows(_extraActionsHost, _extraActions);
        foreach (var item in _permissionChecks)
        {
            item.Check.ButtonPressed = item.Id is "placeable.plot.override" or "placeable.maintain";
        }
        ClearRows(_extraPermissionsHost, _extraPermissions);
        _feedback.Text = "Cet objet est protégé par le scénario principal.";
        _allowGameMasterOverride.ButtonPressed = true;
        _auditBinding = "PlaceableProtectionOverride";
        _previewAllPermissions.ButtonPressed = false;
        _previewGameMasterOverride.ButtonPressed = gameMasterOverridePreview;
        _previewReason.Text = "Restaurer la mise en scène après un blocage";
        _runtimeRequired.ButtonPressed = true;
        UpdateEmptyStates();
        RebuildPreviewActions();
        SelectPreviewAction("destroy");
        UpdatePreviewControls();
        _loading = false;
        PreviewDecision();
        Changed?.Invoke();
    }

    private IReadOnlyList<string> CaptureActions() => _actionChecks
        .Where(item => item.Check.ButtonPressed)
        .Select(item => item.Id)
        .Concat(_extraActions.Select(row => row.Value.Text.Trim()))
        .ToArray();

    private IReadOnlyList<string> CapturePermissions() => _permissionChecks
        .Where(item => item.Check.ButtonPressed)
        .Select(item => item.Id)
        .Concat(_extraPermissions.Select(row => row.Value.Text.Trim()))
        .ToArray();

    private void AddExtraRow(
        VBoxContainer host,
        List<ExtraValueRow> rows,
        string value)
    {
        var card = StudioStyle.Panel("PlaceableProtectionExtra", StudioStyle.SurfaceRaised, 6);
        host.AddChild(card);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        card.AddChild(margin);
        var rowHost = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rowHost.AddThemeConstantOverride("separation", 6);
        margin.AddChild(rowHost);
        var line = new LineEdit
        {
            Text = value,
            PlaceholderText = "Nom métier stable",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(line);
        rowHost.AddChild(line);
        var remove = StudioStyle.Button("Retirer", compact: true);
        rowHost.AddChild(remove);
        var row = new ExtraValueRow(card, line, remove);
        rows.Add(row);
        line.TextChanged += _ => PolicyChanged();
        remove.Pressed += () =>
        {
            rows.Remove(row);
            host.RemoveChild(card);
            card.QueueFree();
            UpdateEmptyStates();
            PolicyChanged();
        };
    }

    private static void ClearRows(VBoxContainer host, List<ExtraValueRow> rows)
    {
        foreach (var row in rows)
        {
            host.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        rows.Clear();
    }

    private void PolicyChanged()
    {
        if (_loading)
        {
            return;
        }
        RebuildPreviewActions();
        Changed?.Invoke();
    }

    private void RebuildPreviewActions()
    {
        var selected = _previewAction.Selected >= 0 && _previewAction.Selected < _previewActionIds.Count
            ? _previewActionIds[_previewAction.Selected]
            : "destroy";
        _previewActionIds.Clear();
        _previewAction.Clear();
        foreach (var action in CaptureActions().Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            _previewActionIds.Add(action);
            _previewAction.AddItem(HumanizeAction(action));
        }
        if (!_previewActionIds.Contains("inspect", StringComparer.Ordinal))
        {
            _previewActionIds.Add("inspect");
            _previewAction.AddItem("Examiner · action non protégée");
        }
        SelectPreviewAction(selected);
    }

    private void SelectPreviewAction(string action)
    {
        var index = _previewActionIds.FindIndex(value => string.Equals(value, action, StringComparison.Ordinal));
        _previewAction.Select(index >= 0 ? index : 0);
    }

    private void PreviewDecision()
    {
        if (_previewAction.Selected < 0 || _previewAction.Selected >= _previewActionIds.Count)
        {
            _previewResult.Text = "Aperçu refusé · choisissez une action à tester.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Danger);
            return;
        }
        var action = _previewActionIds[_previewAction.Selected];
        var label = HumanizeAction(action);
        if (!CaptureActions().Contains(action, StringComparer.Ordinal))
        {
            _previewResult.Text = $"Aperçu · action autorisée · {label} n’est pas protégée.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Success);
            return;
        }
        if (_previewAllPermissions.ButtonPressed)
        {
            _previewResult.Text =
                $"Aperçu · action autorisée par les {CapturePermissions().Count} droit(s) requis · {label}.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Success);
            return;
        }
        if (_previewGameMasterOverride.ButtonPressed && _allowGameMasterOverride.ButtonPressed)
        {
            var reason = _previewReason.Text.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                _previewResult.Text = "Aperçu refusé · la dérogation MJ exige un motif journalisé.";
                _previewResult.AddThemeColorOverride("font_color", StudioStyle.Danger);
                return;
            }
            _previewResult.Text =
                $"Aperçu · dérogation MJ autorisée · {label} · motif « {reason} » · trace d’audit prête.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Warning);
            return;
        }
        _previewResult.Text =
            $"Aperçu · action bloquée pour le joueur · {label}. Message : « {_feedback.Text.Trim()} »";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Warning);
    }

    private void UpdatePreviewControls()
    {
        _previewGameMasterOverride.Disabled = !_allowGameMasterOverride.ButtonPressed;
        if (_previewGameMasterOverride.Disabled)
        {
            _previewGameMasterOverride.ButtonPressed = false;
        }
        _previewReason.Editable = _previewGameMasterOverride.ButtonPressed;
    }

    private void PreviewInputChanged()
    {
        if (!_loading)
        {
            _previewResult.Text = "Aperçu à recalculer · la politique canonique n’a pas été modifiée.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        }
    }

    private void UpdateEmptyStates()
    {
        _extraActionsEmpty.Visible = _extraActions.Count == 0;
        _extraPermissionsEmpty.Visible = _extraPermissions.Count == 0;
    }

    private void UpdateEnabledState(bool enabled)
    {
        _state.Text = _existingProfile
            ? "Politique canonique existante · les actions et droits supplémentaires seront conservés."
            : enabled
                ? "Nouvelle politique prête · elle reste dans le brouillon jusqu’à l’application validée."
                : "État vide · activez Protection plot pour préparer une politique sûre avant validation.";
        _state.AddThemeColorOverride(
            "font_color", _existingProfile || enabled ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void NotifyChanged()
    {
        if (!_loading)
        {
            Changed?.Invoke();
        }
    }

    private static string HumanizeAction(string action) => action switch
    {
        "destroy" => "Détruire l’objet",
        "move" => "Déplacer l’objet",
        "use" => "Utiliser l’objet",
        "container.open" => "Ouvrir le conteneur",
        "lock.change" => "Modifier le verrou",
        "inspect" => "Examiner l’objet",
        _ => "Action personnalisée",
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
        var option = new OptionButton();
        StudioStyle.ApplyOptionButton(option);
        parent.AddChild(option);
        return option;
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
