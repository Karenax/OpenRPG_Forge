using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableLockEditor : VBoxContainer
{
    private static readonly (string Id, string Label)[] KnownSkills =
    [
        ("", "Aucune compétence"),
        ("mechanics", "Mécanique"),
        ("lockpicking", "Crochetage"),
        ("hacking", "Informatique"),
        ("arcana", "Arcanes"),
    ];

    private static readonly (string Id, string Label)[] KnownPermissions =
    [
        ("lock.use", "Utiliser le verrou"),
        ("lock.pick", "Crocheter le verrou"),
        ("lock.hack", "Pirater le verrou"),
        ("lock.admin", "Administrer le verrou"),
    ];

    private readonly Label _state;
    private readonly OptionButton _initialState;
    private readonly OptionButton _keyItem;
    private readonly List<BlueprintId?> _keyItemIds = [];
    private readonly LineEdit _otherCredentials;
    private readonly OptionButton _skill;
    private readonly List<string> _skillIds = [];
    private readonly SpinBox _difficulty;
    private readonly CheckBox _codeEnabled;
    private readonly LineEdit _accessCode;
    private readonly Label _codeState;
    private readonly CheckBox _hackable;
    private readonly CheckBox _relockable;
    private readonly CheckBox _canJam;
    private readonly List<(string Id, CheckBox Check)> _permissionChecks = [];
    private readonly LineEdit _otherPermissions;
    private readonly CheckBox _runtimeRequired;
    private readonly OptionButton _previewMode;
    private readonly SpinBox _previewSkill;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private string? _existingCodeHash;
    private bool _existingProfile;
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal event Action? PreviewRequested;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal StudioPlaceableLockPreviewMode PreviewMode =>
        _previewMode.Selected >= 0 && _previewMode.Selected < Enum.GetValues<StudioPlaceableLockPreviewMode>().Length
            ? (StudioPlaceableLockPreviewMode)_previewMode.Selected
            : StudioPlaceableLockPreviewMode.Credential;

    internal double PreviewSkillResult => _previewSkill.Value;

    internal bool IsReady
    {
        get
        {
            var draft = Capture(new(
                Guid.NewGuid(),
                PlaceableAuthoredLockState.Locked,
                null,
                [],
                string.Empty,
                0,
                false,
                string.Empty,
                null,
                false,
                false,
                false,
                [],
                false));
            var mechanisms = (draft.KeyItemId is null ? 0 : 1) + draft.OtherCredentialIds.Count +
                             (string.IsNullOrWhiteSpace(draft.SkillId) ? 0 : 1) +
                             (draft.CodeEnabled ? 1 : 0) + draft.RequiredPermissions.Count +
                             (draft.Hackable ? 1 : 0);
            return mechanisms > 0 && (!draft.CodeEnabled ||
                                      !string.IsNullOrWhiteSpace(draft.AccessCode) ||
                                      !string.IsNullOrWhiteSpace(draft.ExistingCodeHash)) &&
                   (!draft.Hackable || !string.IsNullOrWhiteSpace(draft.SkillId));
        }
    }

    internal bool PreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        !_previewResult.Text.Contains("à recalculer", StringComparison.OrdinalIgnoreCase);

    internal string HumanText => string.Join(' ',
        _state.Text,
        "Verrouillage État initial Clé Justificatifs Compétence Difficulté Code d’accès Piratage Reverrouillage Brouillage Droits Aperçu runtime",
        _codeState.Text,
        _previewResult.Text,
        string.Join(' ', Enumerable.Range(0, _keyItem.ItemCount).Select(_keyItem.GetItemText)),
        string.Join(' ', Enumerable.Range(0, _skill.ItemCount).Select(_skill.GetItemText)));

    internal StudioPlaceableLockEditor()
    {
        Name = "StudioPlaceableLockEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("ÉTAT ET MOYENS D’OUVERTURE", 10, StudioStyle.Accent));
        _initialState = AddOption(this, "État au chargement");
        foreach (var state in Enum.GetValues<PlaceableAuthoredLockState>())
        {
            _initialState.AddItem(HumanState(state));
        }
        _keyItem = AddOption(this, "Clé principale du projet");
        _otherCredentials = AddLine(this, "Autres justificatifs", "membre-guilde, laissez-passer");
        _skill = AddOption(this, "Compétence de défi");
        _difficulty = AddSpin(this, "Difficulté", 0, 1000, 1);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("CODE ET PIRATAGE", 10, StudioStyle.Accent));
        _codeEnabled = AddCheck(this, "Accepter un code d’accès");
        _accessCode = AddLine(this, "Nouveau code", "Saisissez un code secret");
        _accessCode.Secret = true;
        _accessCode.SecretCharacter = "●";
        _codeState = StudioStyle.Label("Aucun code configuré.", 10, StudioStyle.Muted, wrap: true);
        AddChild(_codeState);
        _hackable = AddCheck(this, "Autoriser le piratage avec la compétence choisie");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("CYCLE DE VIE ET DROITS", 10, StudioStyle.Accent));
        _relockable = AddCheck(this, "Autoriser le reverrouillage");
        _canJam = AddCheck(this, "Autoriser le brouillage du mécanisme");
        var permissions = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        permissions.AddThemeConstantOverride("h_separation", 10);
        AddChild(permissions);
        foreach (var permission in KnownPermissions)
        {
            var check = AddCheck(permissions, permission.Label);
            _permissionChecks.Add((permission.Id, check));
            check.Toggled += _ => NotifyChanged();
        }
        _otherPermissions = AddLine(this, "Autres droits", "droit métier, un par virgule");

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewMode = AddOption(this, "Tentative à simuler");
        foreach (var mode in Enum.GetValues<StudioPlaceableLockPreviewMode>())
        {
            _previewMode.AddItem(HumanPreviewMode(mode));
        }
        _previewSkill = AddSpin(this, "Résultat de compétence simulé", 0, 1000, 1);
        _preview = StudioStyle.Button("Prévisualiser la tentative", compact: true);
        _preview.TooltipText = "Résoudre la tentative par le runtime canonique sans modifier le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label("Aucune tentative prévisualisée.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger ce verrou au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, un verrou absent produit un diagnostic au lieu d’un repli optionnel.";

        _initialState.ItemSelected += _ => NotifyChanged();
        _keyItem.ItemSelected += _ => NotifyChanged();
        _otherCredentials.TextChanged += _ => NotifyChanged();
        _skill.ItemSelected += _ =>
        {
            UpdateHackingState();
            NotifyChanged();
        };
        _difficulty.ValueChanged += _ => NotifyChanged();
        _codeEnabled.Toggled += _ =>
        {
            UpdateCodeState();
            NotifyChanged();
        };
        _accessCode.TextChanged += _ =>
        {
            UpdateCodeState();
            NotifyChanged();
        };
        _hackable.Toggled += _ => NotifyChanged();
        _relockable.Toggled += _ => NotifyChanged();
        _canJam.Toggled += _ => NotifyChanged();
        _otherPermissions.TextChanged += _ => NotifyChanged();
        _previewMode.ItemSelected += _ => PreviewInputChanged();
        _previewSkill.ValueChanged += _ => PreviewInputChanged();
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
        StudioPlaceableLockDraft draft,
        IReadOnlyList<StudioPlaceableItemOption> items,
        bool enabled,
        bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _existingCodeHash = draft.ExistingCodeHash;
        _initialState.Select((int)draft.InitialState);
        PopulateItems(items, draft.KeyItemId);
        PopulateSkills(draft.SkillId);
        _otherCredentials.Text = string.Join(", ", draft.OtherCredentialIds);
        _difficulty.Value = draft.Difficulty;
        _codeEnabled.ButtonPressed = draft.CodeEnabled;
        _accessCode.Text = string.Empty;
        _hackable.ButtonPressed = draft.Hackable;
        _relockable.ButtonPressed = draft.Relockable;
        _canJam.ButtonPressed = draft.CanJam;
        foreach (var permission in _permissionChecks)
        {
            permission.Check.ButtonPressed = draft.RequiredPermissions.Contains(permission.Id, StringComparer.Ordinal);
        }
        _otherPermissions.Text = string.Join(", ", draft.RequiredPermissions.Where(value =>
            KnownPermissions.All(permission => !string.Equals(permission.Id, value, StringComparison.Ordinal))));
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewMode.Select((int)StudioPlaceableLockPreviewMode.Credential);
        _previewSkill.Value = draft.Difficulty;
        _previewResult.Text = "Aucune tentative prévisualisée.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        UpdateCodeState();
        UpdateHackingState();
        _loading = false;
    }

    internal StudioPlaceableLockDraft Capture(StudioPlaceableLockDraft source)
    {
        var keyIndex = _keyItem.Selected;
        var skillIndex = _skill.Selected;
        return source with
        {
            InitialState = _initialState.Selected >= 0
                ? (PlaceableAuthoredLockState)_initialState.Selected
                : PlaceableAuthoredLockState.Locked,
            KeyItemId = keyIndex >= 0 && keyIndex < _keyItemIds.Count ? _keyItemIds[keyIndex] : null,
            OtherCredentialIds = SplitValues(_otherCredentials.Text),
            SkillId = skillIndex >= 0 && skillIndex < _skillIds.Count ? _skillIds[skillIndex] : string.Empty,
            Difficulty = _difficulty.Value,
            CodeEnabled = _codeEnabled.ButtonPressed,
            AccessCode = _accessCode.Text,
            ExistingCodeHash = _existingCodeHash,
            Hackable = _hackable.ButtonPressed,
            Relockable = _relockable.ButtonPressed,
            CanJam = _canJam.ButtonPressed,
            RequiredPermissions = _permissionChecks.Where(item => item.Check.ButtonPressed)
                .Select(item => item.Id)
                .Concat(SplitValues(_otherPermissions.Text))
                .ToArray(),
        };
    }

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = _existingProfile
            ? "Verrou canonique existant · les moyens d’ouverture non affichés restent conservés."
            : enabled
                ? "Nouveau verrou prêt · il reste dans le brouillon jusqu’à l’application validée."
                : "État vide · activez Verrou pour préparer une politique sûre avant validation.";
        _state.AddThemeColorOverride("font_color", _existingProfile || enabled ? StudioStyle.Success : StudioStyle.Warning);
    }

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusInitialState() => _initialState.GrabFocus();

    internal void FocusMechanisms() => _keyItem.GrabFocus();

    internal void FocusLifecycle() => _relockable.GrabFocus();

    internal void FocusPreview() => _previewMode.GrabFocus();

    internal void SetEvidenceValues(bool jamPreview)
    {
        _loading = true;
        _initialState.Select((int)PlaceableAuthoredLockState.Locked);
        if (_keyItemIds.Count > 1)
        {
            _keyItem.Select(1);
        }
        _otherCredentials.Text = "membre-guilde";
        SelectSkill("mechanics");
        _difficulty.Value = 14;
        _codeEnabled.ButtonPressed = true;
        _accessCode.Text = "4271";
        _hackable.ButtonPressed = true;
        _relockable.ButtonPressed = true;
        _canJam.ButtonPressed = true;
        foreach (var permission in _permissionChecks)
        {
            permission.Check.ButtonPressed = permission.Id is "lock.use" or "lock.admin";
        }
        _otherPermissions.Text = string.Empty;
        _runtimeRequired.ButtonPressed = true;
        _previewMode.Select((int)(jamPreview
            ? StudioPlaceableLockPreviewMode.Jam
            : StudioPlaceableLockPreviewMode.Credential));
        _previewSkill.Value = 16;
        UpdateCodeState();
        UpdateHackingState();
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableLockPreview? result)
    {
        _previewResult.Text = result?.Summary ?? "Aucune tentative prévisualisée.";
        _previewResult.AddThemeColorOverride(
            "font_color", result?.Succeeded == true ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void PopulateItems(IReadOnlyList<StudioPlaceableItemOption> items, BlueprintId? selected)
    {
        _keyItemIds.Clear();
        _keyItem.Clear();
        _keyItemIds.Add(null);
        _keyItem.AddItem("Aucune clé d’objet");
        var selectedIndex = 0;
        foreach (var item in items)
        {
            _keyItemIds.Add(item.Id);
            _keyItem.AddItem(item.DisplayName);
            if (item.Id == selected)
            {
                selectedIndex = _keyItem.ItemCount - 1;
            }
        }
        if (selected is not null && selectedIndex == 0)
        {
            _keyItemIds.Add(selected);
            _keyItem.AddItem("Clé existante indisponible");
            _keyItem.SetItemDisabled(_keyItem.ItemCount - 1, true);
            selectedIndex = _keyItem.ItemCount - 1;
        }
        _keyItem.Select(selectedIndex);
    }

    private void PopulateSkills(string selected)
    {
        _skillIds.Clear();
        _skill.Clear();
        foreach (var skill in KnownSkills)
        {
            _skillIds.Add(skill.Id);
            _skill.AddItem(skill.Label);
        }
        var index = _skillIds.FindIndex(value => string.Equals(value, selected, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(selected) && index < 0)
        {
            _skillIds.Add(selected);
            _skill.AddItem("Compétence existante");
            index = _skill.ItemCount - 1;
        }
        _skill.Select(index >= 0 ? index : 0);
    }

    private void SelectSkill(string skill)
    {
        var index = _skillIds.FindIndex(value => string.Equals(value, skill, StringComparison.Ordinal));
        _skill.Select(index >= 0 ? index : 0);
    }

    private void UpdateCodeState()
    {
        _accessCode.Editable = _codeEnabled.ButtonPressed;
        _codeState.Text = !_codeEnabled.ButtonPressed
            ? "Code d’accès désactivé."
            : !string.IsNullOrWhiteSpace(_accessCode.Text)
                ? "Nouveau code prêt · sa valeur restera masquée."
                : !string.IsNullOrWhiteSpace(_existingCodeHash)
                    ? "Code existant conservé · saisissez-en un nouveau seulement pour le remplacer."
                    : "Saisissez un code avant la prévisualisation.";
        _codeState.AddThemeColorOverride(
            "font_color", _codeEnabled.ButtonPressed && string.IsNullOrWhiteSpace(_accessCode.Text) &&
                          string.IsNullOrWhiteSpace(_existingCodeHash)
                ? StudioStyle.Warning
                : StudioStyle.Muted);
    }

    private void UpdateHackingState()
    {
        var skill = _skill.Selected >= 0 && _skill.Selected < _skillIds.Count
            ? _skillIds[_skill.Selected]
            : string.Empty;
        _hackable.Disabled = string.IsNullOrWhiteSpace(skill);
        if (_hackable.Disabled)
        {
            _hackable.ButtonPressed = false;
        }
    }

    private void PreviewInputChanged()
    {
        if (!_loading)
        {
            _previewResult.Text = "Aperçu à recalculer · le verrou canonique n’a pas été modifié.";
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

    private static IReadOnlyList<string> SplitValues(string text) => text
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();

    private static string HumanState(PlaceableAuthoredLockState state) => state switch
    {
        PlaceableAuthoredLockState.Unlocked => "Déverrouillé",
        PlaceableAuthoredLockState.Locked => "Verrouillé",
        PlaceableAuthoredLockState.Jammed => "Brouillé",
        PlaceableAuthoredLockState.Broken => "Brisé",
        _ => "État du verrou",
    };

    private static string HumanPreviewMode(StudioPlaceableLockPreviewMode mode) => mode switch
    {
        StudioPlaceableLockPreviewMode.Credential => "Présenter la clé ou un justificatif",
        StudioPlaceableLockPreviewMode.Skill => "Réussir le défi de compétence",
        StudioPlaceableLockPreviewMode.Code => "Saisir le bon code",
        StudioPlaceableLockPreviewMode.Hacking => "Pirater le mécanisme",
        StudioPlaceableLockPreviewMode.Permission => "Utiliser les droits requis",
        StudioPlaceableLockPreviewMode.GameMaster => "Dérogation du maître de jeu",
        StudioPlaceableLockPreviewMode.Relock => "Reverrouiller après ouverture",
        StudioPlaceableLockPreviewMode.Jam => "Brouiller après ouverture",
        _ => "Tentative de verrou",
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
