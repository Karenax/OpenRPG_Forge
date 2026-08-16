using Forge.Domain.RuntimePolicies;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioProjectEventsPage
{
    private CheckBox _replicateEquipmentAndProgression = null!;
    private CheckBox _restEnabled = null!;
    private OptionButton _restPartyPolicy = null!;
    private SpinBox _restDuration = null!;
    private CheckBox _worldEventsEnabled = null!;
    private CheckBox _worldEventsRequired = null!;
    private VBoxContainer _heartbeatList = null!;
    private VBoxContainer _customEventList = null!;
    private LineEdit _newCustomEventName = null!;
    private OptionButton _newCustomEventTemplate = null!;
    private readonly Dictionary<string, BindingEditor> _restEditors = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, HeartbeatEditor> _heartbeatEditors = [];
    private readonly Dictionary<Guid, CustomEventEditor> _customEventEditors = [];

    internal int Ux0GBindingRowCount => 2 + _restEditors.Count + _heartbeatEditors.Count +
                                        _customEventEditors.Count;

    internal bool ShowsHumanUx0GSurface =>
        _restEditors.Values
            .Concat(_heartbeatEditors.Values.Select(item => item.Binding))
            .Concat(_customEventEditors.Values.Select(item => item.Binding))
            .All(editor => !editor.ScriptLabel.Text.Contains("scripts/", StringComparison.OrdinalIgnoreCase) &&
                           !editor.ScriptLabel.Text.Contains(".forge", StringComparison.OrdinalIgnoreCase) &&
                           !editor.ScriptLabel.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                           !editor.ScriptLabel.Text.Contains("hash", StringComparison.OrdinalIgnoreCase));

    private void BuildUx0GCards(VBoxContainer cards)
    {
        var equipment = AddCard(cards, "equipment-progression", "Équipement et progression",
            "Réagissez aux changements d’équipement et de niveau sans exposer les identifiants techniques.",
            expanded: false);
        _replicateEquipmentAndProgression = Check("Synchroniser ces changements avec les joueurs");
        _replicateEquipmentAndProgression.TooltipText =
            "Le serveur reste autoritaire et réplique les changements validés.";
        equipment.AddChild(_replicateEquipmentAndProgression);
        AddGameplayRow(equipment, "equipment.changed", "Équipement modifié",
            "Objet équipé ou retiré · emplacement et cause disponibles");
        AddGameplayRow(equipment, "progression.changed", "Niveau modifié",
            "Ancien niveau, nouveau niveau et source disponibles");

        var rest = AddCard(cards, "rest", "Repos du groupe",
            "Configurez la demande, le début, l’interruption et la fin d’un repos autoritaire.", expanded: false);
        var restOptions = new HBoxContainer();
        restOptions.AddThemeConstantOverride("separation", 10);
        _restEnabled = Check("Autoriser le repos");
        restOptions.AddChild(_restEnabled);
        restOptions.AddChild(StudioStyle.Label("Participants", 10, StudioStyle.Muted));
        _restPartyPolicy = Option("Personnage seul", "Membres proches", "Tout le groupe");
        _restPartyPolicy.CustomMinimumSize = new Vector2(156, StudioStyle.ControlHeight);
        restOptions.AddChild(_restPartyPolicy);
        restOptions.AddChild(StudioStyle.Label("Durée", 10, StudioStyle.Muted));
        _restDuration = Spin(1, 86_400, 1, " s", 8);
        _restDuration.CustomMinimumSize = new Vector2(112, StudioStyle.ControlHeight);
        restOptions.AddChild(_restDuration);
        rest.AddChild(restOptions);
        AddRestRow(rest, "rest.requested", "Repos demandé", "Avant l’évaluation des règles");
        AddRestRow(rest, "rest.started", "Repos commencé", "Après validation par le serveur");
        AddRestRow(rest, "rest.interrupted", "Repos interrompu", "Danger, déplacement ou règle de zone");
        AddRestRow(rest, "rest.completed", "Repos terminé", "Après la durée prévue");

        var heartbeat = AddCard(cards, "heartbeat", "Rythmes globaux",
            "Déclenchez périodiquement une simulation globale avec cadence et budget bornés.", expanded: false);
        var heartbeatActions = new HBoxContainer();
        heartbeatActions.AddThemeConstantOverride("separation", 8);
        var addHeartbeat = StudioStyle.Button("Ajouter un rythme", primary: true, compact: true);
        addHeartbeat.Pressed += AddHeartbeatFromUi;
        heartbeatActions.AddChild(addHeartbeat);
        var heartbeatHint = StudioStyle.Label(
            "Le serveur limite le rattrapage et suspend les rythmes inactifs selon votre choix.",
            10, StudioStyle.Muted, wrap: true);
        heartbeatHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heartbeatHint.CustomMinimumSize = new Vector2(320, 0);
        heartbeatActions.AddChild(heartbeatHint);
        heartbeat.AddChild(heartbeatActions);
        _heartbeatList = new VBoxContainer();
        _heartbeatList.AddThemeConstantOverride("separation", 6);
        heartbeat.AddChild(_heartbeatList);

        var custom = AddCard(cards, "custom-event", "Signaux utilisateur",
            "Créez des événements nommés et typés ; les clés techniques sont générées automatiquement.",
            expanded: false);
        var newEvent = new HBoxContainer();
        newEvent.AddThemeConstantOverride("separation", 8);
        _newCustomEventName = new LineEdit
        {
            PlaceholderText = "Nom visible, par ex. Portail ouvert",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(_newCustomEventName);
        newEvent.AddChild(_newCustomEventName);
        _newCustomEventTemplate = Option("Message texte", "Compteur", "Cible du monde");
        _newCustomEventTemplate.CustomMinimumSize = new Vector2(155, StudioStyle.ControlHeight);
        newEvent.AddChild(_newCustomEventTemplate);
        var addCustom = StudioStyle.Button("Ajouter le signal", primary: true, compact: true);
        addCustom.Pressed += AddCustomEventFromUi;
        newEvent.AddChild(addCustom);
        custom.AddChild(newEvent);
        _customEventList = new VBoxContainer();
        _customEventList.AddThemeConstantOverride("separation", 6);
        custom.AddChild(_customEventList);
    }

    private void AddRestRow(VBoxContainer parent, string eventName, string title, string context)
    {
        _restEditors.Add(eventName,
            AddBindingRow(parent, title, context + " · Serveur autoritaire"));
    }

    private void RefreshUx0GFromSource(StudioProjectEventConfigurationDraft source)
    {
        _replicateEquipmentAndProgression.ButtonPressed = source.ReplicateEquipmentAndProgression;
        _restEnabled.ButtonPressed = source.RestEnabled;
        _restPartyPolicy.Selected = (int)source.RestPartyPolicy;
        _restDuration.Value = source.RestDurationSeconds;
        foreach (var draft in source.RestBindings)
        {
            _restEditors[draft.EventName].Load(draft.Handler, draft.AutomaticHandler, draft.ScriptPath,
                draft.RequiredAtRuntime, DisplayNameForScript(draft.ScriptPath));
        }

        ClearDynamicEditors(_heartbeatList);
        _heartbeatEditors.Clear();
        foreach (var draft in source.Heartbeats)
        {
            AddHeartbeatEditor(draft);
        }

        _worldEventsEnabled.ButtonPressed = source.WorldEventsEnabled;
        _worldEventsRequired.ButtonPressed = source.WorldEventsRequiredAtRuntime;
        SynchronizeProfileToggle(source.WorldEventsEnabled, _worldEventsRequired);
        ClearDynamicEditors(_customEventList);
        _customEventEditors.Clear();
        foreach (var draft in source.CustomWorldEvents)
        {
            AddCustomEventEditor(draft, createdInStudio: false);
        }
    }

    private StudioProjectEventConfigurationDraft BuildUx0GDraft(
        StudioProjectEventConfigurationDraft draft) => draft with
        {
            ReplicateEquipmentAndProgression = _replicateEquipmentAndProgression.ButtonPressed,
            RestEnabled = _restEnabled.ButtonPressed,
            RestPartyPolicy = (PartyRestPolicy)_restPartyPolicy.Selected,
            RestDurationSeconds = (int)_restDuration.Value,
            RestBindings = draft.RestBindings.Select(item =>
            {
                var editor = _restEditors[item.EventName];
                return item with
                {
                    Handler = editor.Handler,
                    ScriptPath = editor.ScriptPath,
                    RequiredAtRuntime = editor.Required.ButtonPressed,
                };
            }).ToArray(),
            Heartbeats = _heartbeatEditors.Values.Select(item => item.BuildDraft()).ToArray(),
            WorldEventsEnabled = _worldEventsEnabled.ButtonPressed,
            WorldEventsRequiredAtRuntime = _worldEventsRequired.ButtonPressed,
            CustomWorldEvents = _customEventEditors.Values.Select(item => item.BuildDraft()).ToArray(),
        };

    private void AddHeartbeatFromUi()
    {
        var name = UniqueVisibleName(_heartbeatEditors.Values.Select(item => item.Name.Text),
            "Simulation globale");
        AddHeartbeatEditor(new StudioHeartbeatScheduleDraft(
            Guid.NewGuid(), name, true, 1_000, 20, true,
            "OnGlobalHeartbeat", "OnGlobalHeartbeat", null, false));
        SetStatus("Nouveau rythme ajouté au formulaire · prévisualisez avant d’appliquer");
    }

    private void AddHeartbeatEditor(StudioHeartbeatScheduleDraft draft)
    {
        var panel = StudioStyle.Panel("GlobalHeartbeat", StudioStyle.Chrome, 6);
        _heartbeatList.AddChild(panel);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        margin.AddChild(body);
        var settings = new HBoxContainer();
        settings.AddThemeConstantOverride("separation", 8);
        var enabled = Check("Actif");
        settings.AddChild(enabled);
        var name = new LineEdit { PlaceholderText = "Nom du rythme", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(name);
        settings.AddChild(name);
        settings.AddChild(StudioStyle.Label("Toutes les", 10, StudioStyle.Muted));
        var interval = Spin(.1, 86_400, .1, " s", 1);
        interval.CustomMinimumSize = new Vector2(110, StudioStyle.ControlHeight);
        settings.AddChild(interval);
        settings.AddChild(StudioStyle.Label("Budget", 10, StudioStyle.Muted));
        var budget = Spin(1, 1_000, 1, " ms", 20);
        budget.CustomMinimumSize = new Vector2(105, StudioStyle.ControlHeight);
        settings.AddChild(budget);
        var suspend = Check("Suspendre si inactif");
        settings.AddChild(suspend);
        var remove = StudioStyle.Button("Retirer", compact: true);
        settings.AddChild(remove);
        body.AddChild(settings);
        var binding = AddBindingRow(body, "À chaque rythme", "Simulation globale · Serveur autoritaire");
        binding.Load(draft.Handler, draft.AutomaticHandler, draft.ScriptPath, draft.RequiredAtRuntime,
            DisplayNameForScript(draft.ScriptPath));
        enabled.ButtonPressed = draft.Enabled;
        name.Text = draft.Name;
        interval.Value = draft.IntervalMilliseconds / 1_000d;
        budget.Value = draft.BudgetMilliseconds;
        suspend.ButtonPressed = draft.SuspendWhenInactive;
        var editor = new HeartbeatEditor(draft, panel, enabled, name, interval, budget, suspend, binding);
        _heartbeatEditors.Add(draft.Id, editor);
        remove.Pressed += () => RemoveHeartbeat(draft.Id);
    }

    private void RemoveHeartbeat(Guid id)
    {
        if (!_heartbeatEditors.Remove(id, out var editor))
        {
            return;
        }
        _heartbeatList.RemoveChild(editor.Root);
        editor.Root.QueueFree();
        SetStatus("Rythme retiré du formulaire · prévisualisez avant d’appliquer");
    }

    private void AddCustomEventFromUi()
    {
        var label = _newCustomEventName.Text.Trim();
        if (label.Length < 2)
        {
            SetStatus("Donnez un nom visible au signal avant de l’ajouter.", error: true);
            return;
        }
        var slug = UniqueSlug(_customEventEditors.Values.Select(item => item.BuildDraft().Name), Slugify(label));
        var fields = FieldsForTemplate(_newCustomEventTemplate.Selected);
        AddCustomEventEditor(new StudioCustomWorldEventDraft(
            Guid.NewGuid(), "module", slug, 1, fields, false,
            "OnCustomWorldEvent", "OnCustomWorldEvent", null, false), createdInStudio: true, label);
        _newCustomEventName.Text = string.Empty;
        SetStatus("Nouveau signal ajouté au formulaire · prévisualisez avant d’appliquer");
    }

    private void AddCustomEventEditor(
        StudioCustomWorldEventDraft draft,
        bool createdInStudio,
        string? visibleName = null)
    {
        var panel = StudioStyle.Panel("CustomWorldEvent", StudioStyle.Chrome, 6);
        _customEventList.AddChild(panel);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        margin.AddChild(body);
        var settings = new HBoxContainer();
        settings.AddThemeConstantOverride("separation", 8);
        var initialVisibleName = visibleName ?? HumanIdentifier(draft.Name);
        var name = new LineEdit
        {
            Text = initialVisibleName,
            PlaceholderText = "Nom visible du signal",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(name);
        settings.AddChild(name);
        settings.AddChild(StudioStyle.Label("Données", 10, StudioStyle.Muted));
        var template = Option("Message texte", "Compteur", "Cible du monde", "Structure existante");
        template.CustomMinimumSize = new Vector2(160, StudioStyle.ControlHeight);
        var templateIndex = TemplateForFields(draft.Fields);
        template.Selected = templateIndex;
        template.Disabled = templateIndex == 3;
        settings.AddChild(template);
        var remove = StudioStyle.Button("Retirer", compact: true);
        settings.AddChild(remove);
        body.AddChild(settings);
        var binding = AddBindingRow(body, "Quand le signal est émis",
            "Charge utile vérifiée · Serveur autoritaire");
        binding.Load(draft.Handler, draft.AutomaticHandler, draft.ScriptPath, draft.RequiredAtRuntime,
            DisplayNameForScript(draft.ScriptPath));
        var editor = new CustomEventEditor(draft, panel, name, initialVisibleName, template, templateIndex,
            createdInStudio, binding);
        _customEventEditors.Add(draft.Id, editor);
        remove.Pressed += () => RemoveCustomEvent(draft.Id);
    }

    private void RemoveCustomEvent(Guid id)
    {
        if (!_customEventEditors.Remove(id, out var editor))
        {
            return;
        }
        _customEventList.RemoveChild(editor.Root);
        editor.Root.QueueFree();
        SetStatus("Signal retiré du formulaire · prévisualisez avant d’appliquer");
    }

    private void PrepareUx0GEvidence(string mode)
    {
        if (string.Equals(mode, "ux0-g-heartbeat", StringComparison.OrdinalIgnoreCase) &&
            _heartbeatEditors.Count == 0)
        {
            var script = _scripts.FirstOrDefault();
            AddHeartbeatEditor(new StudioHeartbeatScheduleDraft(
                Guid.NewGuid(), "Simulation de Clairval", true, 5_000, 25, true,
                script is null ? "OnGlobalHeartbeat" : "main", "OnGlobalHeartbeat",
                script?.RelativePath, script is not null));
        }
        if (string.Equals(mode, "ux0-g-user-event", StringComparison.OrdinalIgnoreCase) &&
            _customEventEditors.Count == 0)
        {
            var script = _scripts.FirstOrDefault();
            AddCustomEventEditor(new StudioCustomWorldEventDraft(
                Guid.NewGuid(), "module", "portail_ouvert", 1,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "string" }, false,
                script is null ? "OnCustomWorldEvent" : "main", "OnCustomWorldEvent",
                script?.RelativePath, script is not null), createdInStudio: true, "Portail ouvert");
        }
    }

    internal bool RunUx0GTransactionEvidence()
    {
        RefreshFromSession();
        if (_scripts.Count == 0)
        {
            return false;
        }
        var original = _session.Project;
        var originalIds = _heartbeatEditors.Keys.ToHashSet();
        AddHeartbeatFromUi();
        var added = _heartbeatEditors.Single(item => !originalIds.Contains(item.Key));
        added.Value.Binding.UseScript(_scripts[0]);
        added.Value.Binding.Required.ButtonPressed = true;
        var draft = BuildDraft();
        var preview = _session.PreviewProjectEventConfiguration(draft);
        var previewDidNotMutate = ReferenceEquals(original, _session.Project);
        var applied = _session.ApplyProjectEventConfiguration(draft);
        var configured = applied.Succeeded && _session.Project!.Module.RuntimePolicies.Heartbeats
            .Single(item => item.Id == added.Key).ScriptPath == _scripts[0].RelativePath;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(original, _session.Project) && !_session.IsDirty;
        RefreshFromSession();
        return preview.Succeeded && previewDidNotMutate && configured && restored;
    }

    private static void ClearDynamicEditors(VBoxContainer container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        StudioStyle.ApplyOptionButton(option);
        foreach (var label in labels)
        {
            option.AddItem(label);
        }
        return option;
    }

    private static SpinBox Spin(double minimum, double maximum, double step, string suffix, double value)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Suffix = suffix,
            Value = value,
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static IReadOnlyDictionary<string, string> FieldsForTemplate(int index) => index switch
    {
        1 => new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "int" },
        2 => new Dictionary<string, string>(StringComparer.Ordinal) { ["target"] = "guid" },
        _ => new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "string" },
    };

    private static int TemplateForFields(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.Count == 1 && fields.TryGetValue("message", out var message) && message == "string") return 0;
        if (fields.Count == 1 && fields.TryGetValue("value", out var value) && value == "int") return 1;
        if (fields.Count == 1 && fields.TryGetValue("target", out var target) && target == "guid") return 2;
        return 3;
    }

    private static string HumanIdentifier(string value)
    {
        var words = value.Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? "Signal sans nom"
            : string.Join(' ', words.Select((word, index) => index == 0
                ? char.ToUpperInvariant(word[0]) + word[1..]
                : word));
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var characters = normalized.Where(character =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_')
            .ToArray();
        var slug = string.Join('_', new string(characters)
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.IsNullOrWhiteSpace(slug) ? "signal" : slug;
    }

    private static string UniqueSlug(IEnumerable<string> existing, string requested)
    {
        var values = existing.ToHashSet(StringComparer.Ordinal);
        if (!values.Contains(requested)) return requested;
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{requested}_{index}";
            if (!values.Contains(candidate)) return candidate;
        }
        return requested + "_nouveau";
    }

    private static string UniqueVisibleName(IEnumerable<string> existing, string requested)
    {
        var values = existing.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!values.Contains(requested)) return requested;
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{requested} {index}";
            if (!values.Contains(candidate)) return candidate;
        }
        return requested + " nouveau";
    }

    private sealed record HeartbeatEditor(
        StudioHeartbeatScheduleDraft Source,
        Control Root,
        CheckBox Enabled,
        LineEdit Name,
        SpinBox Interval,
        SpinBox Budget,
        CheckBox Suspend,
        BindingEditor Binding)
    {
        internal StudioHeartbeatScheduleDraft BuildDraft() => Source with
        {
            Name = Name.Text.Trim(),
            Enabled = Enabled.ButtonPressed,
            IntervalMilliseconds = (int)Math.Round(Interval.Value * 1_000,
                MidpointRounding.AwayFromZero),
            BudgetMilliseconds = (int)Budget.Value,
            SuspendWhenInactive = Suspend.ButtonPressed,
            Handler = Binding.Handler,
            ScriptPath = Binding.ScriptPath,
            RequiredAtRuntime = Binding.Required.ButtonPressed,
        };
    }

    private sealed record CustomEventEditor(
        StudioCustomWorldEventDraft Source,
        Control Root,
        LineEdit Name,
        string InitialVisibleName,
        OptionButton Template,
        int InitialTemplate,
        bool CreatedInStudio,
        BindingEditor Binding)
    {
        internal StudioCustomWorldEventDraft BuildDraft()
        {
            var renamed = !string.Equals(Name.Text.Trim(), InitialVisibleName, StringComparison.Ordinal);
            var templateChanged = CreatedInStudio || Template.Selected != InitialTemplate;
            return Source with
            {
                Name = renamed ? Slugify(Name.Text) : Source.Name,
                Fields = templateChanged && Template.Selected < 3
                    ? FieldsForTemplate(Template.Selected)
                    : Source.Fields,
                Handler = Binding.Handler,
                ScriptPath = Binding.ScriptPath,
                RequiredAtRuntime = Binding.Required.ButtonPressed,
            };
        }
    }
}
