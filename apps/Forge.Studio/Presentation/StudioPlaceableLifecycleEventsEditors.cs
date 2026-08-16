using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableOnUsedEventEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly LineEdit _apiVersion;
    private readonly LineEdit _eventBinding;
    private readonly LineEdit _beforeScript;
    private readonly LineEdit _afterScript;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private bool _loading;

    internal event Action? Changed;
    internal event Action? PreviewRequested;

    internal string ApiVersion => _apiVersion.Text;
    internal string EventBinding => _eventBinding.Text;
    internal string BeforeScript => _beforeScript.Text;
    internal string AfterScript => _afterScript.Text;
    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;
    internal bool IsReady => new[] { _apiVersion.Text, _eventBinding.Text }.All(value =>
        !string.IsNullOrWhiteSpace(value));
    internal bool PreviewReady => PreviewIsReady(_previewResult.Text);
    internal string HumanText => string.Join(' ', _state.Text,
        "OnUsed Version ForgeScript Événement publié Script avant Script après Acteur Cible Résultat " +
        "Aperçu sans écriture Exiger au runtime", _previewResult.Text);

    internal StudioPlaceableOnUsedEventEditor()
    {
        Name = "StudioPlaceableOnUsedEventEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);
        _state = StateLabel();
        AddChild(_state);
        _apiVersion = AddLine(this, "Version ForgeScript", "1.0.0");
        _eventBinding = AddLine(this, "Événement publié", "InteractionExecuted");
        _beforeScript = AddLine(this, "Script avant", "scripts/avant_utilisation");
        _afterScript = AddLine(this, "Script après", "scripts/apres_utilisation");
        _preview = StudioStyle.Button("Prévisualiser l’utilisation", compact: true);
        _preview.TooltipText = "Produire l’événement typé sans utiliser un objet de la scène";
        AddChild(_preview);
        _previewResult = PreviewLabel("Aucune utilisation prévisualisée.");
        AddChild(_previewResult);
        _runtimeRequired = AddCheck(this, "Exiger OnUsed au runtime");

        foreach (var line in new[] { _apiVersion, _eventBinding, _beforeScript, _afterScript })
            line.TextChanged += _ => NotifyChanged();
        _runtimeRequired.Toggled += _ => NotifyChanged();
        _preview.Pressed += () => PreviewRequested?.Invoke();
    }

    internal void Load(StudioPlaceableDraft draft, bool enabled)
    {
        _loading = true;
        _apiVersion.Text = draft.OnUsedForgeScriptApiVersion;
        _eventBinding.Text = draft.OnUsedEventBinding;
        _beforeScript.Text = draft.BeforeUseScript;
        _afterScript.Text = draft.AfterUseScript;
        _runtimeRequired.ButtonPressed = draft.RequireOnUsedRuntime;
        _previewResult.Text = "Aucune utilisation prévisualisée.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        _loading = false;
    }

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = enabled
            ? "OnUsed actif · l’aperçu utilise le même contrat typé que le runtime."
            : "OnUsed désactivé · activez sa publication pour configurer les scripts.";
        _state.AddThemeColorOverride("font_color", enabled ? StudioStyle.Success : StudioStyle.Warning);
        foreach (var line in new[] { _apiVersion, _eventBinding, _beforeScript, _afterScript })
            line.Editable = enabled;
        _runtimeRequired.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled) _runtimeRequired.ButtonPressed = false;
    }

    internal void FocusBindings() => _eventBinding.GrabFocus();

    internal void SetEvidenceValues(string apiVersion = "1.1.0", string eventBinding = "ObjetUtilise")
    {
        _loading = true;
        _apiVersion.Text = apiVersion;
        _eventBinding.Text = eventBinding;
        _beforeScript.Text = "scripts/placeable_before_use";
        _afterScript.Text = "scripts/placeable_after_use";
        _runtimeRequired.ButtonPressed = true;
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableOnUsedEventPreview? result)
        => SetPreview(_previewResult, result?.Summary, result?.Succeeded == true,
            "Aucune utilisation prévisualisée.");

    private void NotifyChanged()
    {
        if (_loading) return;
        SetPreview(_previewResult, "Aperçu à recalculer · aucun objet n’a été utilisé.", false,
            "Aperçu à recalculer · aucun objet n’a été utilisé.");
        Changed?.Invoke();
    }

    internal static Label StateLabel()
    {
        var label = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        label.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        return label;
    }

    internal static Label PreviewLabel(string text)
    {
        var label = StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);
        label.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        return label;
    }

    internal static void SetPreview(Label label, string? text, bool succeeded, string fallback)
    {
        label.Text = text ?? fallback;
        label.AddThemeColorOverride("font_color", succeeded ? StudioStyle.Success : StudioStyle.Muted);
    }

    internal static bool PreviewIsReady(string text) =>
        text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        !text.Contains("à recalculer", StringComparison.OrdinalIgnoreCase) &&
        !text.StartsWith("Aucun", StringComparison.OrdinalIgnoreCase);

    internal static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    internal static CheckBox AddCheck(Container parent, string label)
    {
        var check = new CheckBox { Text = label, FocusMode = Control.FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }
}

internal sealed partial class StudioPlaceableContentsChangedEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly LineEdit _apiVersion;
    private readonly LineEdit _eventBinding;
    private readonly CheckBox _delta;
    private readonly CheckBox _cause;
    private readonly CheckBox _revision;
    private readonly CheckBox _actor;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private bool _loading;

    internal event Action? Changed;
    internal event Action? PreviewRequested;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;
    internal bool IsReady => new[] { _apiVersion.Text, _eventBinding.Text }.All(value =>
        !string.IsNullOrWhiteSpace(value)) && _delta.ButtonPressed && _cause.ButtonPressed &&
        _revision.ButtonPressed && _actor.ButtonPressed;
    internal bool PreviewReady => StudioPlaceableOnUsedEventEditor.PreviewIsReady(_previewResult.Text);
    internal string HumanText => string.Join(' ', _state.Text,
        "OnDisturbed Contenu modifié Version ForgeScript Événement publié Delta transactionnel Cause " +
        "Révisions Acteur Aperçu sans écriture Exiger au runtime", _previewResult.Text);

    internal StudioPlaceableContentsChangedEditor()
    {
        Name = "StudioPlaceableContentsChangedEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);
        _state = StudioPlaceableOnUsedEventEditor.StateLabel();
        AddChild(_state);
        _apiVersion = StudioPlaceableOnUsedEventEditor.AddLine(this, "Version ForgeScript", "1.0.0");
        _eventBinding = StudioPlaceableOnUsedEventEditor.AddLine(this, "Événement publié", "ContainerContentsChanged");
        var payload = new GridContainer { Columns = 2 };
        payload.AddThemeConstantOverride("h_separation", 12);
        AddChild(payload);
        _delta = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Delta transactionnel");
        _cause = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Cause de la modification");
        _revision = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Révisions avant et après");
        _actor = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Acteur à l’origine");
        _preview = StudioStyle.Button("Prévisualiser le contenu modifié", compact: true);
        _preview.TooltipText = "Produire OnDisturbed sans modifier un inventaire de la scène";
        AddChild(_preview);
        _previewResult = StudioPlaceableOnUsedEventEditor.PreviewLabel("Aucune modification prévisualisée.");
        AddChild(_previewResult);
        _runtimeRequired = StudioPlaceableOnUsedEventEditor.AddCheck(this,
            "Exiger l’événement de contenu modifié au runtime");

        foreach (var line in new[] { _apiVersion, _eventBinding }) line.TextChanged += _ => NotifyChanged();
        foreach (var check in new[] { _delta, _cause, _revision, _actor, _runtimeRequired })
            check.Toggled += _ => NotifyChanged();
        _preview.Pressed += () => PreviewRequested?.Invoke();
    }

    internal void Load(StudioPlaceableContentsChangedDraft draft, bool enabled, bool requiredAtRuntime)
    {
        _loading = true;
        _apiVersion.Text = draft.ForgeScriptApiVersion;
        _eventBinding.Text = draft.EventBinding;
        _delta.ButtonPressed = draft.IncludeTransactionalDelta;
        _cause.ButtonPressed = draft.IncludeCause;
        _revision.ButtonPressed = draft.IncludeRevision;
        _actor.ButtonPressed = draft.IncludeActor;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewResult.Text = "Aucune modification prévisualisée.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        _loading = false;
    }

    internal StudioPlaceableContentsChangedDraft Capture(StudioPlaceableContentsChangedDraft source) => source with
    {
        ForgeScriptApiVersion = _apiVersion.Text,
        EventBinding = _eventBinding.Text,
        IncludeTransactionalDelta = _delta.ButtonPressed,
        IncludeCause = _cause.ButtonPressed,
        IncludeRevision = _revision.ButtonPressed,
        IncludeActor = _actor.ButtonPressed,
    };

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = enabled
            ? "OnDisturbed actif · le delta reste transactionnel et révisionné."
            : "État vide · activez Contenu modifié pour préparer OnDisturbed.";
        _state.AddThemeColorOverride("font_color", enabled ? StudioStyle.Success : StudioStyle.Warning);
        _apiVersion.Editable = enabled;
        _eventBinding.Editable = enabled;
        foreach (var check in new[] { _delta, _cause, _revision, _actor }) check.Disabled = !enabled;
        _runtimeRequired.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled) _runtimeRequired.ButtonPressed = false;
    }

    internal void FocusBindings() => _eventBinding.GrabFocus();

    internal void SetEvidenceValues()
    {
        _loading = true;
        _apiVersion.Text = "1.1.0";
        _eventBinding.Text = "ContenuObjetModifie";
        _delta.ButtonPressed = true;
        _cause.ButtonPressed = true;
        _revision.ButtonPressed = true;
        _actor.ButtonPressed = true;
        _runtimeRequired.ButtonPressed = true;
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableContentsChangedPreview? result)
        => StudioPlaceableOnUsedEventEditor.SetPreview(_previewResult, result?.Summary,
            result?.Succeeded == true, "Aucune modification prévisualisée.");

    private void NotifyChanged()
    {
        if (_loading) return;
        StudioPlaceableOnUsedEventEditor.SetPreview(_previewResult,
            "Aperçu à recalculer · aucun inventaire n’a été modifié.", false,
            "Aperçu à recalculer · aucun inventaire n’a été modifié.");
        Changed?.Invoke();
    }
}

internal sealed partial class StudioPlaceableDamageEventsEditor : VBoxContainer
{
    private readonly Label _state;
    private readonly LineEdit _apiVersion;
    private readonly LineEdit _damagedBinding;
    private readonly LineEdit _destroyedBinding;
    private readonly CheckBox _source;
    private readonly CheckBox _amount;
    private readonly CheckBox _damageType;
    private readonly CheckBox _previousHealth;
    private readonly CheckBox _currentHealth;
    private readonly CheckBox _context;
    private readonly OptionButton _previewMode;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private bool _loading;

    internal event Action? Changed;
    internal event Action? PreviewRequested;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;
    internal StudioPlaceableDamageEventPreviewMode PreviewMode =>
        _previewMode.Selected == (int)StudioPlaceableDamageEventPreviewMode.Destroyed
            ? StudioPlaceableDamageEventPreviewMode.Destroyed
            : StudioPlaceableDamageEventPreviewMode.Damaged;
    internal bool IsReady => new[] { _apiVersion.Text, _damagedBinding.Text, _destroyedBinding.Text }.All(value =>
        !string.IsNullOrWhiteSpace(value)) && new[]
        {
            _source, _amount, _damageType, _previousHealth, _currentHealth, _context,
        }.All(check => check.ButtonPressed);
    internal bool PreviewReady => StudioPlaceableOnUsedEventEditor.PreviewIsReady(_previewResult.Text);
    internal string HumanText => string.Join(' ', _state.Text,
        "OnDamaged OnDeath Dégâts Mort Version ForgeScript Événement de dégâts Événement de mort " +
        "Source Quantité Type Santé avant Santé après Contexte Aperçu sans écriture Exiger au runtime",
        _previewResult.Text, string.Join(' ', Enumerable.Range(0, _previewMode.ItemCount)
            .Select(_previewMode.GetItemText)));

    internal StudioPlaceableDamageEventsEditor()
    {
        Name = "StudioPlaceableDamageEventsEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);
        _state = StudioPlaceableOnUsedEventEditor.StateLabel();
        AddChild(_state);
        _apiVersion = StudioPlaceableOnUsedEventEditor.AddLine(this, "Version ForgeScript", "1.0.0");
        _damagedBinding = StudioPlaceableOnUsedEventEditor.AddLine(this, "Événement de dégâts", "DamageApplied");
        _destroyedBinding = StudioPlaceableOnUsedEventEditor.AddLine(this, "Événement de mort", "EntityDestroyed");
        var payload = new GridContainer { Columns = 2 };
        payload.AddThemeConstantOverride("h_separation", 12);
        AddChild(payload);
        _source = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Source des dégâts");
        _amount = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Quantité appliquée");
        _damageType = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Type de dégâts");
        _previousHealth = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Santé avant");
        _currentHealth = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Santé après");
        _context = StudioPlaceableOnUsedEventEditor.AddCheck(payload, "Contexte additionnel");
        _previewMode = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_previewMode);
        _previewMode.AddItem("Objet endommagé");
        _previewMode.AddItem("Objet détruit");
        AddChild(_previewMode);
        _preview = StudioStyle.Button("Prévisualiser les dégâts ou la mort", compact: true);
        _preview.TooltipText = "Produire OnDamaged ou OnDeath sans toucher à la santé d’un objet de la scène";
        AddChild(_preview);
        _previewResult = StudioPlaceableOnUsedEventEditor.PreviewLabel("Aucun dégât prévisualisé.");
        AddChild(_previewResult);
        _runtimeRequired = StudioPlaceableOnUsedEventEditor.AddCheck(this,
            "Exiger les événements de dégâts et de mort au runtime");

        foreach (var line in new[] { _apiVersion, _damagedBinding, _destroyedBinding })
            line.TextChanged += _ => NotifyChanged();
        foreach (var check in new[]
                 {
                     _source, _amount, _damageType, _previousHealth, _currentHealth, _context, _runtimeRequired,
                 })
            check.Toggled += _ => NotifyChanged();
        _previewMode.ItemSelected += _ => NotifyChanged();
        _preview.Pressed += () => PreviewRequested?.Invoke();
    }

    internal void Load(StudioPlaceableDamageEventsDraft draft, bool enabled, bool requiredAtRuntime)
    {
        _loading = true;
        _apiVersion.Text = draft.ForgeScriptApiVersion;
        _damagedBinding.Text = draft.DamagedBinding;
        _destroyedBinding.Text = draft.DestroyedBinding;
        _source.ButtonPressed = draft.IncludeSource;
        _amount.ButtonPressed = draft.IncludeAmount;
        _damageType.ButtonPressed = draft.IncludeDamageType;
        _previousHealth.ButtonPressed = draft.IncludePreviousHealth;
        _currentHealth.ButtonPressed = draft.IncludeCurrentHealth;
        _context.ButtonPressed = draft.IncludeContext;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewMode.Select((int)StudioPlaceableDamageEventPreviewMode.Damaged);
        _previewResult.Text = "Aucun dégât prévisualisé.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEnabledState(enabled);
        _loading = false;
    }

    internal StudioPlaceableDamageEventsDraft Capture(StudioPlaceableDamageEventsDraft source) => source with
    {
        ForgeScriptApiVersion = _apiVersion.Text,
        DamagedBinding = _damagedBinding.Text,
        DestroyedBinding = _destroyedBinding.Text,
        IncludeSource = _source.ButtonPressed,
        IncludeAmount = _amount.ButtonPressed,
        IncludeDamageType = _damageType.ButtonPressed,
        IncludePreviousHealth = _previousHealth.ButtonPressed,
        IncludeCurrentHealth = _currentHealth.ButtonPressed,
        IncludeContext = _context.ButtonPressed,
    };

    internal void SetEnabledState(bool enabled)
    {
        _state.Text = enabled
            ? "OnDamaged et OnDeath actifs · les deux événements partagent le même payload typé."
            : "État vide · activez Dégâts / mort pour préparer les deux événements.";
        _state.AddThemeColorOverride("font_color", enabled ? StudioStyle.Success : StudioStyle.Warning);
        foreach (var line in new[] { _apiVersion, _damagedBinding, _destroyedBinding }) line.Editable = enabled;
        foreach (var check in new[] { _source, _amount, _damageType, _previousHealth, _currentHealth, _context })
            check.Disabled = !enabled;
        _previewMode.Disabled = !enabled;
        _runtimeRequired.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled) _runtimeRequired.ButtonPressed = false;
    }

    internal void FocusBindings() => _damagedBinding.GrabFocus();

    internal void SetEvidenceValues(string mode)
    {
        _loading = true;
        _apiVersion.Text = "1.1.0";
        _damagedBinding.Text = "ObjetEndommage";
        _destroyedBinding.Text = "ObjetDetruit";
        foreach (var check in new[] { _source, _amount, _damageType, _previousHealth, _currentHealth, _context })
            check.ButtonPressed = true;
        _runtimeRequired.ButtonPressed = true;
        _previewMode.Select((int)(mode.Contains("death", StringComparison.OrdinalIgnoreCase)
            ? StudioPlaceableDamageEventPreviewMode.Destroyed
            : StudioPlaceableDamageEventPreviewMode.Damaged));
        _loading = false;
        Changed?.Invoke();
        PreviewRequested?.Invoke();
    }

    internal void SetPreviewResult(StudioPlaceableDamageEventPreview? result)
        => StudioPlaceableOnUsedEventEditor.SetPreview(_previewResult, result?.Summary,
            result?.Succeeded == true, "Aucun dégât prévisualisé.");

    private void NotifyChanged()
    {
        if (_loading) return;
        StudioPlaceableOnUsedEventEditor.SetPreview(_previewResult,
            "Aperçu à recalculer · aucune santé n’a été modifiée.", false,
            "Aperçu à recalculer · aucune santé n’a été modifiée.");
        Changed?.Invoke();
    }
}
