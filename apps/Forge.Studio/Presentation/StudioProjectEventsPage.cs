using Forge.Domain.Lifecycle;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioProjectEventsPage : Control
{
    private static readonly IReadOnlyDictionary<RuntimeAuthority, string> AuthorityLabels =
        new Dictionary<RuntimeAuthority, string>
        {
            [RuntimeAuthority.Editor] = "Prévisualisation dans le Studio",
            [RuntimeAuthority.SinglePlayer] = "Partie solo",
            [RuntimeAuthority.Client] = "Client connecté",
            [RuntimeAuthority.Server] = "Serveur autoritaire",
        };

    private readonly StudioSessionController _session;
    private readonly ScrollContainer _scroll;
    private readonly CheckBox _lifecycleEnabled;
    private readonly CheckBox _lifecycleRequired;
    private readonly CheckBox _gameplayEnabled;
    private readonly CheckBox _gameplayRequired;
    private readonly CheckBox _useDownedState;
    private readonly CheckBox _autoRespawn;
    private readonly PanelContainer _pickerPanel;
    private readonly Label _pickerTitle;
    private readonly LineEdit _pickerSearch;
    private readonly ItemList _pickerList;
    private readonly Label _pickerEmpty;
    private readonly Label _status;
    private readonly Dictionary<(RuntimeLifecycleEventKind Event, RuntimeAuthority Authority), BindingEditor>
        _lifecycleEditors = [];
    private readonly Dictionary<string, BindingEditor> _gameplayEditors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VBoxContainer> _cardBodies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _cardHeaders = new(StringComparer.Ordinal);
    private IReadOnlyList<StudioEventScriptChoice> _scripts = [];
    private IReadOnlyList<StudioEventScriptChoice> _filteredScripts = [];
    private StudioProjectEventConfigurationDraft? _source;
    private BindingEditor? _activePickerEditor;
    private bool _loading;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ProjectChanged;

    internal int CardCount => _cardBodies.Count;

    internal int BindingRowCount => _lifecycleEditors.Count + _gameplayEditors.Count +
                                    Ux0GBindingRowCount + Ux0HBindingRowCount;

    internal int Ux0FCardCount => 5;

    internal int Ux0FBindingRowCount => 20;

    internal int ScriptChoiceCount => _scripts.Count;

    internal bool ShowsHumanSurface => CardCount >= 12 && Ux0FBindingRowCount == 20 &&
        ShowsHumanUx0GSurface && ShowsHumanUx0HSurface &&
        _lifecycleEditors.Values.Concat(_gameplayEditors.Values).All(editor =>
            !editor.ScriptLabel.Text.Contains("scripts/", StringComparison.OrdinalIgnoreCase) &&
            !editor.ScriptLabel.Text.Contains(".forge", StringComparison.OrdinalIgnoreCase) &&
            !editor.ScriptLabel.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
            !editor.ScriptLabel.Text.Contains("hash", StringComparison.OrdinalIgnoreCase));

    internal StudioProjectEventsPage(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioProjectEventsPage";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = StudioStyle.Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var rootMargin = StudioStyle.Margin(10, 8, 10, 8);
        rootMargin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(rootMargin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        rootMargin.AddChild(root);

        var banner = StudioStyle.Panel("ProjectEventsBanner", StudioStyle.AccentDark, 8);
        root.AddChild(banner);
        var bannerMargin = StudioStyle.Margin(12, 8, 12, 8);
        banner.AddChild(bannerMargin);
        var bannerRow = new HBoxContainer();
        bannerRow.AddThemeConstantOverride("separation", 12);
        bannerMargin.AddChild(bannerRow);
        var bannerText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bannerText.AddChild(StudioStyle.Label("Événements du module", 16));
        bannerText.AddChild(StudioStyle.Label(
            "Reliez les moments importants et les rythmes globaux à un script existant, ou gardez le comportement automatique.",
            10,
            StudioStyle.Muted,
            wrap: true));
        bannerRow.AddChild(bannerText);
        var authority = StudioStyle.Label("JEU AUTORITAIRE CÔTÉ SERVEUR", 10, StudioStyle.Success);
        authority.TooltipText = "Les événements de jeu sont validés et exécutés par le serveur.";
        bannerRow.AddChild(authority);

        _pickerPanel = StudioStyle.Panel("InlineScriptPicker", StudioStyle.SurfaceRaised, 8, 2);
        _pickerPanel.Visible = false;
        root.AddChild(_pickerPanel);
        var pickerMargin = StudioStyle.Margin(10, 8, 10, 8);
        _pickerPanel.AddChild(pickerMargin);
        var picker = new VBoxContainer();
        picker.AddThemeConstantOverride("separation", 6);
        pickerMargin.AddChild(picker);
        var pickerHeading = new HBoxContainer();
        _pickerTitle = StudioStyle.Label("Choisir un script", 14);
        _pickerTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pickerHeading.AddChild(_pickerTitle);
        var closePicker = StudioStyle.Button("Fermer", compact: true);
        closePicker.Pressed += ClosePicker;
        pickerHeading.AddChild(closePicker);
        picker.AddChild(pickerHeading);
        _pickerSearch = new LineEdit
        {
            PlaceholderText = "Rechercher par nom…",
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(_pickerSearch);
        _pickerSearch.TextChanged += _ => RebuildPickerList();
        picker.AddChild(_pickerSearch);
        _pickerList = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0, 116),
        };
        _pickerList.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        _pickerList.ItemActivated += _ => UseSelectedScript();
        picker.AddChild(_pickerList);
        _pickerEmpty = StudioStyle.Label(string.Empty, 10, StudioStyle.Warning, wrap: true);
        picker.AddChild(_pickerEmpty);
        var pickerActions = new HBoxContainer();
        pickerActions.AddThemeConstantOverride("separation", 6);
        var useScript = StudioStyle.Button("Utiliser ce script", primary: true, compact: true);
        useScript.Pressed += UseSelectedScript;
        pickerActions.AddChild(useScript);
        var useAutomatic = StudioStyle.Button("Revenir au comportement automatique", compact: true);
        useAutomatic.Pressed += UseAutomaticForActiveEditor;
        pickerActions.AddChild(useAutomatic);
        picker.AddChild(pickerActions);

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(_scroll);
        var cards = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("separation", 8);
        _scroll.AddChild(cards);

        var lifecycleBody = AddCard(cards, "lifecycle", "Ouverture et démarrage du monde",
            "Deux moments, distingués pour le Studio, le solo, le client et le serveur.", expanded: true);
        var lifecycleToggles = new HBoxContainer();
        lifecycleToggles.AddThemeConstantOverride("separation", 14);
        _lifecycleEnabled = Check("Activer ces événements");
        _lifecycleRequired = Check("Configuration obligatoire au lancement");
        lifecycleToggles.AddChild(_lifecycleEnabled);
        lifecycleToggles.AddChild(_lifecycleRequired);
        lifecycleBody.AddChild(lifecycleToggles);
        lifecycleBody.AddChild(StudioStyle.Label(
            "Un script différent peut répondre au chargement du projet et au monde prêt dans chaque contexte.",
            10,
            StudioStyle.Muted,
            wrap: true));
        foreach (var @event in new[]
                 {
                     RuntimeLifecycleEventKind.ProjectLoaded,
                     RuntimeLifecycleEventKind.WorldStarted,
                 })
        {
            foreach (var authorityValue in Enum.GetValues<RuntimeAuthority>())
            {
                var eventLabel = @event == RuntimeLifecycleEventKind.ProjectLoaded
                    ? "Projet chargé"
                    : "Monde prêt";
                var editor = AddBindingRow(lifecycleBody, eventLabel, AuthorityLabels[authorityValue]);
                _lifecycleEditors.Add((@event, authorityValue), editor);
            }
        }

        var connectionBody = AddCard(cards, "connection", "Connexion des joueurs",
            "Arrivée, joueur prêt, départ volontaire et déconnexion imprévue.", expanded: false);
        var gameplayToggles = new HBoxContainer();
        gameplayToggles.AddThemeConstantOverride("separation", 14);
        _gameplayEnabled = Check("Activer les événements de jeu");
        _gameplayRequired = Check("Configuration obligatoire en jeu");
        gameplayToggles.AddChild(_gameplayEnabled);
        gameplayToggles.AddChild(_gameplayRequired);
        connectionBody.AddChild(gameplayToggles);
        AddGameplayRow(connectionBody, "player.joining", "Joueur en cours d’arrivée", "Avant son entrée dans la partie");
        AddGameplayRow(connectionBody, "player.ready", "Joueur prêt", "Après la synchronisation initiale");
        AddGameplayRow(connectionBody, "player.leaving", "Joueur quitte la partie", "Départ demandé proprement");
        AddGameplayRow(connectionBody, "player.disconnected", "Connexion interrompue", "Départ réseau imprévu");

        var inventoryBody = AddCard(cards, "inventory", "Objets acquis ou perdus",
            "Un même événement indique s’il s’agit d’une acquisition ou d’une perte.", expanded: false);
        AddGameplayRow(inventoryBody, "inventory.changed", "Inventaire modifié", "Objet acquis ou perdu · quantité et source disponibles");

        var useBody = AddCard(cards, "use", "Objets utilisés et capacités activées",
            "Réagissez à l’usage d’un objet ou au déclenchement de sa capacité.", expanded: false);
        AddGameplayRow(useBody, "item.used", "Objet utilisé", "Objet, cible et cause disponibles");
        AddGameplayRow(useBody, "ability.activated", "Capacité activée", "Capacité, cible et coût disponibles");

        var lifeBody = AddCard(cards, "life", "Défaite, mort et réapparition",
            "Configurez les étapes de fin de vie et le retour en jeu.", expanded: false);
        var lifeOptions = new HBoxContainer();
        lifeOptions.AddThemeConstantOverride("separation", 14);
        _useDownedState = Check("Passer par l’état à terre");
        _autoRespawn = Check("Réapparition automatique");
        lifeOptions.AddChild(_useDownedState);
        lifeOptions.AddChild(_autoRespawn);
        lifeBody.AddChild(lifeOptions);
        AddGameplayRow(lifeBody, "life.downed", "Personnage à terre", "Encore récupérable");
        AddGameplayRow(lifeBody, "life.dying", "Personnage mourant", "Transition vers la mort");
        AddGameplayRow(lifeBody, "life.dead", "Personnage mort", "État final confirmé par le serveur");
        AddGameplayRow(lifeBody, "life.respawnrequested", "Réapparition demandée", "Avant le retour dans le monde");
        AddGameplayRow(lifeBody, "life.respawned", "Personnage réapparu", "Après restauration dans le monde");

        BuildUx0GCards(cards);
        BuildUx0HCards(cards);

        var actionPanel = StudioStyle.Panel("ProjectEventActions", StudioStyle.Chrome, 7);
        root.AddChild(actionPanel);
        var actionMargin = StudioStyle.Margin(8, 6, 8, 6);
        actionPanel.AddChild(actionMargin);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        actionMargin.AddChild(actions);
        var preview = StudioStyle.Button("Prévisualiser sans modifier", compact: true);
        preview.Pressed += PreviewConfiguration;
        actions.AddChild(preview);
        var apply = StudioStyle.Button("Appliquer au projet", primary: true, compact: true);
        apply.Pressed += ApplyConfiguration;
        actions.AddChild(apply);
        _status = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.CustomMinimumSize = new Vector2(260, 0);
        actions.AddChild(_status);

        _lifecycleEnabled.Toggled += enabled => SynchronizeProfileToggle(enabled, _lifecycleRequired);
        _gameplayEnabled.Toggled += enabled => SynchronizeProfileToggle(enabled, _gameplayRequired);
        RefreshFromSession();
    }

    internal void RefreshFromSession()
    {
        if (!_session.IsOpen || _session.Project is null)
        {
            return;
        }
        _loading = true;
        _source = _session.CaptureProjectEventConfiguration();
        _scripts = _session.FindForgeScripts();
        _lifecycleEnabled.ButtonPressed = _source.LifecycleEnabled;
        _lifecycleRequired.ButtonPressed = _source.LifecycleRequiredAtRuntime;
        _gameplayEnabled.ButtonPressed = _source.GameplayEnabled;
        _gameplayRequired.ButtonPressed = _source.GameplayRequiredAtRuntime;
        _useDownedState.ButtonPressed = _source.UseDownedState;
        _autoRespawn.ButtonPressed = _source.AutoRespawn;
        foreach (var draft in _source.LifecycleBindings)
        {
            _lifecycleEditors[(draft.Event, draft.Authority)].Load(
                draft.Handler,
                draft.AutomaticHandler,
                draft.ScriptPath,
                draft.RequiredAtRuntime,
                DisplayNameForScript(draft.ScriptPath));
        }
        foreach (var draft in _source.GameplayBindings)
        {
            if (_gameplayEditors.TryGetValue(draft.EventName, out var editor))
            {
                editor.Load(draft.Handler, draft.AutomaticHandler, draft.ScriptPath,
                    draft.RequiredAtRuntime, DisplayNameForScript(draft.ScriptPath));
            }
        }
        RefreshUx0GFromSource(_source);
        RefreshUx0HFromSource(_source);
        SynchronizeProfileToggle(_source.LifecycleEnabled, _lifecycleRequired);
        SynchronizeProfileToggle(_source.GameplayEnabled, _gameplayRequired);
        RebuildPickerList();
        _loading = false;
        SetStatus(_scripts.Count == 0
            ? "Aucun script ForgeScript dans ce projet · les comportements automatiques restent disponibles"
            : $"{_scripts.Count} script(s) disponible(s) · aucune modification appliquée",
            error: false);
    }

    internal bool PrepareEvidence(string mode)
    {
        RefreshFromSession();
        var card = mode.ToLowerInvariant() switch
        {
            "ux0-f-connections" => "connection",
            "ux0-f-inventory" => "inventory",
            "ux0-f-use" => "use",
            "ux0-f-life" => "life",
            "ux0-g-equipment" => "equipment-progression",
            "ux0-g-rest" => "rest",
            "ux0-g-heartbeat" => "heartbeat",
            "ux0-g-user-event" => "custom-event",
            "ux0-h-abort" => "sequence-abort",
            "ux0-h-time" => "world-time",
            "ux0-h-progression" => "progression-scale",
            _ => "lifecycle",
        };
        PrepareUx0GEvidence(mode);
        PrepareUx0HEvidence(mode);
        OpenOnlyCard(card);
        _scroll.ScrollVertical = 0;
        if (card != "lifecycle")
        {
            var target = _cardBodies[card];
            Callable.From(() => _scroll.EnsureControlVisible(target)).CallDeferred();
        }
        if (string.Equals(mode, "ux0-f-picker", StringComparison.OrdinalIgnoreCase))
        {
            OpenPicker(_lifecycleEditors[(RuntimeLifecycleEventKind.ProjectLoaded, RuntimeAuthority.Server)],
                "Projet chargé · Serveur autoritaire");
        }
        else
        {
            ClosePicker();
        }
        return ShowsHumanSurface && _source is not null;
    }

    internal bool RunTransactionEvidence()
    {
        RefreshFromSession();
        if (_scripts.Count == 0)
        {
            return false;
        }
        var original = _session.Project;
        var editor = _lifecycleEditors[(RuntimeLifecycleEventKind.ProjectLoaded, RuntimeAuthority.Server)];
        editor.UseScript(_scripts[0]);
        var draft = BuildDraft();
        var preview = _session.PreviewProjectEventConfiguration(draft);
        var previewDidNotMutate = ReferenceEquals(original, _session.Project);
        var applied = _session.ApplyProjectEventConfiguration(draft);
        var configured = applied.Succeeded && _session.Project!.Module.RuntimePolicies.LifecycleEvents.Bindings
            .Single(binding => binding.Event == RuntimeLifecycleEventKind.ProjectLoaded &&
                               binding.Authority == RuntimeAuthority.Server).ScriptPath == _scripts[0].RelativePath;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(original, _session.Project) && !_session.IsDirty;
        RefreshFromSession();
        return preview.Succeeded && previewDidNotMutate && configured && restored;
    }

    internal void PreviewActiveConfiguration() => PreviewConfiguration();

    private VBoxContainer AddCard(
        VBoxContainer parent,
        string id,
        string title,
        string subtitle,
        bool expanded)
    {
        var panel = StudioStyle.Panel("EventCard_" + id, StudioStyle.Surface, 8);
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(10, 7, 10, 8);
        panel.AddChild(margin);
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 6);
        margin.AddChild(card);
        var header = StudioStyle.Button(string.Empty, compact: true);
        header.Alignment = HorizontalAlignment.Left;
        header.Pressed += () => SetCardOpen(id, !_cardBodies[id].Visible);
        card.AddChild(header);
        card.AddChild(StudioStyle.Label(subtitle, 10, StudioStyle.Muted, wrap: true));
        var body = new VBoxContainer { Visible = expanded };
        body.AddThemeConstantOverride("separation", 6);
        card.AddChild(body);
        _cardBodies.Add(id, body);
        _cardHeaders.Add(id, header);
        header.SetMeta("title", title);
        UpdateCardHeader(id);
        return body;
    }

    private BindingEditor AddBindingRow(VBoxContainer parent, string title, string context)
    {
        var rowPanel = StudioStyle.Panel("EventBinding", StudioStyle.Chrome, 6);
        rowPanel.CustomMinimumSize = new Vector2(0, 44);
        parent.AddChild(rowPanel);
        var margin = StudioStyle.Margin(8, 5, 8, 5);
        rowPanel.AddChild(margin);
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        margin.AddChild(row);
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 8);
        var name = StudioStyle.Label(title, 11);
        name.CustomMinimumSize = new Vector2(150, 0);
        heading.AddChild(name);
        var contextLabel = StudioStyle.Label(context, 9, StudioStyle.Accent);
        contextLabel.CustomMinimumSize = new Vector2(190, 0);
        heading.AddChild(contextLabel);
        var script = StudioStyle.Label("Comportement automatique", 10, StudioStyle.Success, wrap: true);
        script.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        script.CustomMinimumSize = new Vector2(180, 0);
        heading.AddChild(script);
        var required = Check("Obligatoire");
        heading.AddChild(required);
        var choose = StudioStyle.Button("Choisir…", compact: true);
        heading.AddChild(choose);
        var automatic = StudioStyle.Button("Automatique", compact: true);
        heading.AddChild(automatic);
        row.AddChild(heading);
        var editor = new BindingEditor(script, required);
        choose.Pressed += () => OpenPicker(editor, $"{title} · {context}");
        automatic.Pressed += () => editor.UseAutomatic();
        return editor;
    }

    private void AddGameplayRow(VBoxContainer parent, string eventName, string title, string context)
    {
        var editor = AddBindingRow(parent, title, context + " · Serveur autoritaire");
        _gameplayEditors.Add(eventName, editor);
    }

    private void OpenPicker(BindingEditor editor, string targetLabel)
    {
        _activePickerEditor = editor;
        _pickerTitle.Text = "Choisir un script pour « " + targetLabel + " »";
        _pickerSearch.Text = string.Empty;
        RebuildPickerList();
        _pickerPanel.Visible = true;
        _pickerSearch.GrabFocus();
    }

    private void ClosePicker()
    {
        _pickerPanel.Visible = false;
        _activePickerEditor = null;
    }

    private void RebuildPickerList()
    {
        if (_pickerList is null)
        {
            return;
        }
        var query = _pickerSearch?.Text.Trim() ?? string.Empty;
        _filteredScripts = _scripts.Where(item => query.Length == 0 ||
                item.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
        _pickerList.Clear();
        foreach (var script in _filteredScripts)
        {
            _pickerList.AddItem(script.DisplayName);
        }
        if (_filteredScripts.Count > 0)
        {
            _pickerList.Select(0);
        }
        _pickerEmpty.Text = _scripts.Count == 0
            ? "Aucun script n’est encore présent. Créez-le dans la page Scripts, puis revenez ici."
            : _filteredScripts.Count == 0
                ? "Aucun script ne correspond à cette recherche."
                : string.Empty;
        _pickerEmpty.Visible = _filteredScripts.Count == 0;
    }

    private void UseSelectedScript()
    {
        var selectedItems = _pickerList.GetSelectedItems();
        if (_activePickerEditor is null || selectedItems.Length == 0)
        {
            SetStatus("Sélectionnez un script dans la liste.", error: true);
            return;
        }
        var index = (int)selectedItems[0];
        if (index < 0 || index >= _filteredScripts.Count)
        {
            SetStatus("Le script choisi n’est plus disponible.", error: true);
            return;
        }
        var choice = _filteredScripts[index];
        _activePickerEditor.UseScript(choice);
        ClosePicker();
        SetStatus($"« {choice.DisplayName} » choisi dans le formulaire · prévisualisez avant d’appliquer");
    }

    private void UseAutomaticForActiveEditor()
    {
        if (_activePickerEditor is null)
        {
            return;
        }
        _activePickerEditor.UseAutomatic();
        ClosePicker();
        SetStatus("Comportement automatique choisi dans le formulaire · prévisualisez avant d’appliquer");
    }

    private void PreviewConfiguration()
    {
        var result = _session.PreviewProjectEventConfiguration(BuildDraft());
        SetStatus(result.Succeeded ? $"{result.Message} · {result.Summary}" : result.Message,
            error: !result.Succeeded);
    }

    private void ApplyConfiguration()
    {
        var result = _session.ApplyProjectEventConfiguration(BuildDraft());
        SetStatus(result.Succeeded ? $"{result.Message} · {result.Summary}" : result.Message,
            error: !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshFromSession();
            ProjectChanged?.Invoke();
        }
    }

    private StudioProjectEventConfigurationDraft BuildDraft()
    {
        var source = _source ?? _session.CaptureProjectEventConfiguration();
        var baseDraft = source with
        {
            LifecycleEnabled = _lifecycleEnabled.ButtonPressed,
            LifecycleRequiredAtRuntime = _lifecycleRequired.ButtonPressed,
            LifecycleBindings = source.LifecycleBindings.Select(draft =>
            {
                var editor = _lifecycleEditors[(draft.Event, draft.Authority)];
                return draft with
                {
                    Handler = editor.Handler,
                    ScriptPath = editor.ScriptPath,
                    RequiredAtRuntime = editor.Required.ButtonPressed,
                };
            }).ToArray(),
            GameplayEnabled = _gameplayEnabled.ButtonPressed,
            GameplayRequiredAtRuntime = _gameplayRequired.ButtonPressed,
            UseDownedState = _useDownedState.ButtonPressed,
            AutoRespawn = _autoRespawn.ButtonPressed,
            GameplayBindings = source.GameplayBindings.Select(draft =>
            {
                if (!_gameplayEditors.TryGetValue(draft.EventName, out var editor))
                {
                    return draft;
                }
                return draft with
                {
                    Handler = editor.Handler,
                    ScriptPath = editor.ScriptPath,
                    RequiredAtRuntime = editor.Required.ButtonPressed,
                };
            }).ToArray(),
        };
        return BuildUx0HDraft(BuildUx0GDraft(baseDraft));
    }

    private void OpenOnlyCard(string id)
    {
        foreach (var candidate in _cardBodies.Keys)
        {
            SetCardOpen(candidate, string.Equals(candidate, id, StringComparison.Ordinal));
        }
    }

    private string DisplayNameForScript(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "Comportement automatique";
        }
        return _scripts.FirstOrDefault(item =>
                   string.Equals(item.RelativePath, relativePath, StringComparison.Ordinal))?.DisplayName ??
               "Script existant indisponible";
    }

    private void SetCardOpen(string id, bool open)
    {
        _cardBodies[id].Visible = open;
        UpdateCardHeader(id);
    }

    private void UpdateCardHeader(string id)
    {
        var header = _cardHeaders[id];
        var title = header.GetMeta("title").AsString();
        header.Text = (_cardBodies[id].Visible ? "▼  " : "▶  ") + title;
    }

    private void SynchronizeProfileToggle(bool enabled, CheckBox required)
    {
        if (_loading)
        {
            required.Disabled = !enabled;
            return;
        }
        required.Disabled = !enabled;
        if (!enabled)
        {
            required.ButtonPressed = false;
        }
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private static CheckBox Check(string text) => new()
    {
        Text = text,
        FocusMode = FocusModeEnum.All,
    };

    private sealed class BindingEditor
    {
        internal Label ScriptLabel { get; }
        internal CheckBox Required { get; }
        internal string Handler { get; private set; } = "main";
        internal string AutomaticHandler { get; private set; } = "main";
        internal string? ScriptPath { get; private set; }

        internal BindingEditor(Label scriptLabel, CheckBox required)
        {
            ScriptLabel = scriptLabel;
            Required = required;
            Required.Toggled += enabled =>
            {
                if (enabled && ScriptPath is null)
                {
                    Required.ButtonPressed = false;
                }
            };
        }

        internal void Load(
            string handler,
            string automaticHandler,
            string? scriptPath,
            bool required,
            string displayName)
        {
            Handler = handler;
            AutomaticHandler = automaticHandler;
            ScriptPath = scriptPath;
            ScriptLabel.Text = displayName;
            ScriptLabel.AddThemeColorOverride("font_color",
                scriptPath is null ? StudioStyle.Success :
                displayName == "Script existant indisponible" ? StudioStyle.Danger : StudioStyle.Accent);
            Required.Disabled = scriptPath is null;
            Required.ButtonPressed = scriptPath is not null && required;
        }

        internal void UseScript(StudioEventScriptChoice choice)
        {
            Handler = "main";
            ScriptPath = choice.RelativePath;
            ScriptLabel.Text = choice.DisplayName;
            ScriptLabel.AddThemeColorOverride("font_color", StudioStyle.Accent);
            Required.Disabled = false;
        }

        internal void UseAutomatic()
        {
            Handler = AutomaticHandler;
            ScriptPath = null;
            ScriptLabel.Text = "Comportement automatique";
            ScriptLabel.AddThemeColorOverride("font_color", StudioStyle.Success);
            Required.ButtonPressed = false;
            Required.Disabled = true;
        }
    }
}
