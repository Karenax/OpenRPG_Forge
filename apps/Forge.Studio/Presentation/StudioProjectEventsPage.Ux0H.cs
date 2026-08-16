using Forge.Domain.Lifecycle;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioProjectEventsPage
{
    private CheckBox _sequenceAbortEnabled = null!;
    private CheckBox _restoreCamera = null!;
    private CheckBox _restoreControl = null!;
    private CheckBox _restoreInvulnerability = null!;
    private CheckBox _restoreLocks = null!;
    private CheckBox _restoreNarrative = null!;
    private SpinBox _maximumNarrativeRollback = null!;
    private BindingEditor _sequenceAbortBinding = null!;
    private SpinBox _daysPerMonth = null!;
    private SpinBox _monthsPerYear = null!;
    private SpinBox _hoursPerDay = null!;
    private SpinBox _minutesPerHour = null!;
    private SpinBox _worldSpeed = null!;
    private CheckBox _worldPaused = null!;
    private SpinBox _initialYear = null!;
    private SpinBox _initialMonth = null!;
    private SpinBox _initialDay = null!;
    private SpinBox _initialHour = null!;
    private SpinBox _initialMinute = null!;
    private VBoxContainer _astronomyList = null!;
    private VBoxContainer _timeZoneList = null!;
    private Label _rulesetName = null!;
    private CheckBox _shareProgression = null!;
    private SpinBox _groupMultiplier = null!;
    private SpinBox _difficultyMultiplier = null!;
    private VBoxContainer _progressionCurveList = null!;
    private VBoxContainer _progressionSourceList = null!;
    private LineEdit _newProgressionSource = null!;
    private readonly Dictionary<WorldAstronomicalEventKind, AstronomyEditor> _astronomyEditors = [];
    private readonly List<TimeZoneEditor> _timeZoneEditors = [];
    private readonly List<ProgressionCurveEditor> _progressionCurveEditors = [];
    private readonly Dictionary<string, ProgressionSourceEditor> _progressionSourceEditors =
        new(StringComparer.Ordinal);

    internal int Ux0HBindingRowCount => 1;

    internal bool ShowsHumanUx0HSurface =>
        !_sequenceAbortBinding.ScriptLabel.Text.Contains("scripts/", StringComparison.OrdinalIgnoreCase) &&
        !_sequenceAbortBinding.ScriptLabel.Text.Contains(".forge", StringComparison.OrdinalIgnoreCase) &&
        !_sequenceAbortBinding.ScriptLabel.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_rulesetName.Text.Contains("forge.", StringComparison.OrdinalIgnoreCase) &&
        _timeZoneEditors.All(item => !Guid.TryParse(item.DisplayName.Text, out _)) &&
        _progressionSourceEditors.Values.All(item =>
            !item.DisplayName.Text.Contains("_", StringComparison.Ordinal) &&
            !item.DisplayName.Text.Contains("hash", StringComparison.OrdinalIgnoreCase));

    private void BuildUx0HCards(VBoxContainer cards)
    {
        var abort = AddCard(cards, "sequence-abort", "Abandon de cinématique",
            "Rendez immédiatement la caméra et les contrôles, puis restaurez la narration de façon bornée.",
            expanded: false);
        var abortOptions = new HBoxContainer();
        abortOptions.AddThemeConstantOverride("separation", 10);
        _sequenceAbortEnabled = Check("Autoriser l’abandon");
        abortOptions.AddChild(_sequenceAbortEnabled);
        abortOptions.AddChild(StudioStyle.Label("Restaurer", 10, StudioStyle.Muted));
        _restoreCamera = Check("Caméra");
        _restoreControl = Check("Contrôles");
        _restoreInvulnerability = Check("Protection temporaire");
        _restoreLocks = Check("Verrous");
        _restoreNarrative = Check("Narration");
        abortOptions.AddChild(_restoreCamera);
        abortOptions.AddChild(_restoreControl);
        abortOptions.AddChild(_restoreInvulnerability);
        abortOptions.AddChild(_restoreLocks);
        abortOptions.AddChild(_restoreNarrative);
        abort.AddChild(abortOptions);
        var rollback = new HBoxContainer();
        rollback.AddThemeConstantOverride("separation", 8);
        rollback.AddChild(StudioStyle.Label("Maximum d’étapes narratives à annuler", 10, StudioStyle.Muted));
        _maximumNarrativeRollback = Spin(0, 10_000, 1, " étapes", 64);
        _maximumNarrativeRollback.CustomMinimumSize = new Vector2(145, StudioStyle.ControlHeight);
        rollback.AddChild(_maximumNarrativeRollback);
        var startupHint = StudioStyle.Label(
            "La séquence de démarrage reste configurée dans Cinématiques.", 10, StudioStyle.Accent,
            wrap: true);
        startupHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        startupHint.CustomMinimumSize = new Vector2(390, 0);
        rollback.AddChild(startupHint);
        abort.AddChild(rollback);
        _sequenceAbortBinding = AddBindingRow(abort, "Après l’abandon",
            "Restauration terminée · Serveur autoritaire");

        var world = AddCard(cards, "world-time", "Temps et calendrier du monde",
            "Réglez le calendrier, l’heure initiale, la vitesse, les astres et le décalage de chaque zone.",
            expanded: false);
        var worldOptions = new HBoxContainer();
        worldOptions.AddThemeConstantOverride("separation", 12);
        _worldEventsEnabled = Check("Activer l’horloge et les services globaux");
        _worldEventsRequired = Check("Configuration obligatoire au lancement");
        _worldPaused = Check("Démarrer en pause");
        worldOptions.AddChild(_worldEventsEnabled);
        worldOptions.AddChild(_worldEventsRequired);
        worldOptions.AddChild(_worldPaused);
        world.AddChild(worldOptions);

        var calendar = new HBoxContainer();
        calendar.AddThemeConstantOverride("separation", 7);
        calendar.AddChild(StudioStyle.Label("Calendrier", 11));
        _daysPerMonth = AddNumber(calendar, "Jours / mois", 1, 366, 1, 28, 92);
        _monthsPerYear = AddNumber(calendar, "Mois / année", 1, 100, 1, 12, 92);
        _hoursPerDay = AddNumber(calendar, "Heures / jour", 1, 100, 1, 24, 92);
        _minutesPerHour = AddNumber(calendar, "Minutes / heure", 1, 1_000, 1, 60, 102);
        _worldSpeed = AddNumber(calendar, "Vitesse", 0, 10_000, .05, 1, 112, " ×");
        world.AddChild(calendar);

        var initial = new HBoxContainer();
        initial.AddThemeConstantOverride("separation", 7);
        initial.AddChild(StudioStyle.Label("Date initiale", 11));
        _initialYear = AddNumber(initial, "Année", 1, 10_000, 1, 1, 92);
        _initialMonth = AddNumber(initial, "Mois", 1, 100, 1, 1, 82);
        _initialDay = AddNumber(initial, "Jour", 1, 366, 1, 1, 82);
        _initialHour = AddNumber(initial, "Heure", 0, 99, 1, 0, 82);
        _initialMinute = AddNumber(initial, "Minute", 0, 999, 1, 0, 88);
        world.AddChild(initial);

        world.AddChild(StudioStyle.Label("Repères astronomiques", 11));
        _astronomyList = new VBoxContainer();
        _astronomyList.AddThemeConstantOverride("separation", 4);
        world.AddChild(_astronomyList);
        foreach (var kind in Enum.GetValues<WorldAstronomicalEventKind>())
        {
            AddAstronomyEditor(kind);
        }

        world.AddChild(StudioStyle.Label("Décalage des zones", 11));
        _timeZoneList = new VBoxContainer();
        _timeZoneList.AddThemeConstantOverride("separation", 4);
        world.AddChild(_timeZoneList);
        _worldEventsEnabled.Toggled += enabled => SynchronizeProfileToggle(enabled, _worldEventsRequired);
        _daysPerMonth.ValueChanged += _ => SynchronizeCalendarBounds();
        _monthsPerYear.ValueChanged += _ => SynchronizeCalendarBounds();
        _hoursPerDay.ValueChanged += _ => SynchronizeCalendarBounds();
        _minutesPerHour.ValueChanged += _ => SynchronizeCalendarBounds();

        var progression = AddCard(cards, "progression-scale", "Échelle de progression",
            "Ajustez les paliers d’expérience et leur modulation par groupe, difficulté et source.",
            expanded: false);
        var progressionHeader = new HBoxContainer();
        progressionHeader.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        progressionHeader.AddThemeConstantOverride("separation", 10);
        progressionHeader.AddChild(StudioStyle.Label("Ruleset", 10, StudioStyle.Muted));
        _rulesetName = StudioStyle.Label("Règles génériques Forge", 11, StudioStyle.Accent);
        _rulesetName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _rulesetName.CustomMinimumSize = new Vector2(220, 0);
        progressionHeader.AddChild(_rulesetName);
        _shareProgression = Check("Partager au groupe");
        progressionHeader.AddChild(_shareProgression);
        _groupMultiplier = AddNumber(progressionHeader, "Groupe", .01, 100, .05, 1, 105, " ×");
        _difficultyMultiplier = AddNumber(progressionHeader, "Difficulté", .01, 100, .05, 1, 112, " ×");
        progression.AddChild(progressionHeader);

        var curveHeader = new HBoxContainer();
        curveHeader.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        curveHeader.AddThemeConstantOverride("separation", 8);
        var curveTitle = StudioStyle.Label("Paliers de niveau", 11);
        curveTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        curveHeader.AddChild(curveTitle);
        var addLevel = StudioStyle.Button("Ajouter un palier", primary: true, compact: true);
        addLevel.Pressed += AddProgressionLevelFromUi;
        curveHeader.AddChild(addLevel);
        progression.AddChild(curveHeader);
        _progressionCurveList = new VBoxContainer();
        _progressionCurveList.AddThemeConstantOverride("separation", 4);
        progression.AddChild(_progressionCurveList);

        var sourceHeader = new HBoxContainer();
        sourceHeader.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        sourceHeader.AddThemeConstantOverride("separation", 8);
        var sourceTitle = StudioStyle.Label("Sources d’expérience", 11);
        sourceTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sourceHeader.AddChild(sourceTitle);
        _newProgressionSource = new LineEdit
        {
            PlaceholderText = "Nouvelle source, par ex. Artisanat",
            CustomMinimumSize = new Vector2(230, StudioStyle.ControlHeight),
        };
        StudioStyle.ApplyLineEdit(_newProgressionSource);
        sourceHeader.AddChild(_newProgressionSource);
        var addSource = StudioStyle.Button("Ajouter la source", primary: true, compact: true);
        addSource.Pressed += AddProgressionSourceFromUi;
        sourceHeader.AddChild(addSource);
        progression.AddChild(sourceHeader);
        _progressionSourceList = new VBoxContainer();
        _progressionSourceList.AddThemeConstantOverride("separation", 4);
        progression.AddChild(_progressionSourceList);
    }

    private SpinBox AddNumber(
        HBoxContainer parent,
        string label,
        double minimum,
        double maximum,
        double step,
        double value,
        float width,
        string suffix = "")
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var input = Spin(minimum, maximum, step, suffix, value);
        input.CustomMinimumSize = new Vector2(width, StudioStyle.ControlHeight);
        parent.AddChild(input);
        return input;
    }

    private void AddAstronomyEditor(WorldAstronomicalEventKind kind)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var enabled = Check(AstronomyLabel(kind));
        enabled.CustomMinimumSize = new Vector2(145, StudioStyle.ControlHeight);
        row.AddChild(enabled);
        row.AddChild(StudioStyle.Label("à", 10, StudioStyle.Muted));
        var hour = Spin(0, 99, 1, " h", 0);
        hour.CustomMinimumSize = new Vector2(90, StudioStyle.ControlHeight);
        row.AddChild(hour);
        var minute = Spin(0, 999, 1, " min", 0);
        minute.CustomMinimumSize = new Vector2(100, StudioStyle.ControlHeight);
        row.AddChild(minute);
        var hint = StudioStyle.Label("Événement autoritaire du calendrier", 9, StudioStyle.Muted);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.CustomMinimumSize = new Vector2(260, 0);
        row.AddChild(hint);
        _astronomyList.AddChild(row);
        _astronomyEditors.Add(kind, new AstronomyEditor(enabled, hour, minute));
        enabled.Toggled += active =>
        {
            hour.Editable = active;
            minute.Editable = active;
        };
    }

    private void RefreshUx0HFromSource(StudioProjectEventConfigurationDraft source)
    {
        var abort = source.SequenceAbort;
        _sequenceAbortEnabled.ButtonPressed = abort.Enabled;
        _restoreCamera.ButtonPressed = abort.Restore.HasFlag(SequenceAbortRestoreFlags.Camera);
        _restoreControl.ButtonPressed = abort.Restore.HasFlag(SequenceAbortRestoreFlags.PlayerControl);
        _restoreInvulnerability.ButtonPressed =
            abort.Restore.HasFlag(SequenceAbortRestoreFlags.TemporaryInvulnerability);
        _restoreLocks.ButtonPressed = abort.Restore.HasFlag(SequenceAbortRestoreFlags.Locks);
        _restoreNarrative.ButtonPressed = abort.Restore.HasFlag(SequenceAbortRestoreFlags.NarrativeTransaction);
        _maximumNarrativeRollback.Value = abort.MaximumNarrativeRollbackSteps;
        _sequenceAbortBinding.Load(abort.Handler, abort.AutomaticHandler, abort.ScriptPath,
            abort.RequiredAtRuntime, DisplayNameForScript(abort.ScriptPath));

        var clock = source.WorldClock;
        _daysPerMonth.Value = clock.DaysPerMonth;
        _monthsPerYear.Value = clock.MonthsPerYear;
        _hoursPerDay.Value = clock.HoursPerDay;
        _minutesPerHour.Value = clock.MinutesPerHour;
        _worldSpeed.Value = clock.Speed;
        _worldPaused.ButtonPressed = clock.Paused;
        LoadInitialDate(clock);
        foreach (var (kind, editor) in _astronomyEditors)
        {
            var present = clock.AstronomicalMinutes.TryGetValue(kind, out var minuteOfDay);
            editor.Enabled.ButtonPressed = present;
            editor.Hour.Value = present ? minuteOfDay / clock.MinutesPerHour : 0;
            editor.Minute.Value = present ? minuteOfDay % clock.MinutesPerHour : 0;
            editor.Hour.Editable = present;
            editor.Minute.Editable = present;
        }
        ClearDynamicEditors(_timeZoneList);
        _timeZoneEditors.Clear();
        foreach (var zone in clock.AreaTimeZones)
        {
            AddTimeZoneEditor(zone);
        }
        SynchronizeCalendarBounds();

        var progression = source.Progression;
        _rulesetName.Text = progression.RulesetDisplayName;
        _shareProgression.ButtonPressed = progression.ShareWithParty;
        _groupMultiplier.Value = progression.GroupMultiplier;
        _difficultyMultiplier.Value = progression.DifficultyMultiplier;
        ClearDynamicEditors(_progressionCurveList);
        _progressionCurveEditors.Clear();
        foreach (var point in progression.Curve)
        {
            AddProgressionCurveEditor(point);
        }
        ClearDynamicEditors(_progressionSourceList);
        _progressionSourceEditors.Clear();
        foreach (var item in progression.Sources)
        {
            AddProgressionSourceEditor(item);
        }
    }

    private StudioProjectEventConfigurationDraft BuildUx0HDraft(
        StudioProjectEventConfigurationDraft draft)
    {
        var restore = SequenceAbortRestoreFlags.None;
        if (_restoreCamera.ButtonPressed) restore |= SequenceAbortRestoreFlags.Camera;
        if (_restoreControl.ButtonPressed) restore |= SequenceAbortRestoreFlags.PlayerControl;
        if (_restoreInvulnerability.ButtonPressed) restore |= SequenceAbortRestoreFlags.TemporaryInvulnerability;
        if (_restoreLocks.ButtonPressed) restore |= SequenceAbortRestoreFlags.Locks;
        if (_restoreNarrative.ButtonPressed) restore |= SequenceAbortRestoreFlags.NarrativeTransaction;
        if (restore == SequenceAbortRestoreFlags.None)
        {
            restore = SequenceAbortRestoreFlags.Camera;
        }

        return draft with
        {
            SequenceAbort = draft.SequenceAbort with
            {
                Enabled = _sequenceAbortEnabled.ButtonPressed,
                Restore = restore,
                MaximumNarrativeRollbackSteps = (int)_maximumNarrativeRollback.Value,
                Handler = _sequenceAbortBinding.Handler,
                ScriptPath = _sequenceAbortBinding.ScriptPath,
                RequiredAtRuntime = _sequenceAbortBinding.Required.ButtonPressed,
            },
            WorldClock = draft.WorldClock with
            {
                DaysPerMonth = (int)_daysPerMonth.Value,
                MonthsPerYear = (int)_monthsPerYear.Value,
                HoursPerDay = (int)_hoursPerDay.Value,
                MinutesPerHour = (int)_minutesPerHour.Value,
                Speed = _worldSpeed.Value,
                Paused = _worldPaused.ButtonPressed,
                InitialMinute = BuildInitialMinute(),
                AreaTimeZones = _timeZoneEditors.Select(item => item.BuildDraft()).ToArray(),
                AstronomicalMinutes = _astronomyEditors.Where(item => item.Value.Enabled.ButtonPressed)
                    .ToDictionary(item => item.Key, item =>
                        (int)item.Value.Hour.Value * (int)_minutesPerHour.Value +
                        (int)item.Value.Minute.Value),
            },
            Progression = draft.Progression with
            {
                Curve = _progressionCurveEditors.Select(item => item.BuildPoint()).ToArray(),
                GroupMultiplier = _groupMultiplier.Value,
                DifficultyMultiplier = _difficultyMultiplier.Value,
                Sources = _progressionSourceEditors.Values.Select(item => item.BuildDraft()).ToArray(),
                ShareWithParty = _shareProgression.ButtonPressed,
            },
        };
    }

    private void AddTimeZoneEditor(StudioWorldTimeZoneDraft draft)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = StudioStyle.Label(draft.DisplayName, 10);
        name.CustomMinimumSize = new Vector2(220, StudioStyle.ControlHeight);
        row.AddChild(name);
        row.AddChild(StudioStyle.Label("Décalage local", 9, StudioStyle.Muted));
        var offset = Spin(-100_000, 100_000, 1, " min", draft.OffsetMinutes);
        offset.CustomMinimumSize = new Vector2(125, StudioStyle.ControlHeight);
        row.AddChild(offset);
        var hint = StudioStyle.Label("0 = heure du monde", 9, StudioStyle.Muted);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.CustomMinimumSize = new Vector2(180, 0);
        row.AddChild(hint);
        _timeZoneList.AddChild(row);
        _timeZoneEditors.Add(new TimeZoneEditor(draft, name, offset));
    }

    private void AddProgressionLevelFromUi()
    {
        var previous = _progressionCurveEditors.LastOrDefault()?.BuildPoint() ?? new ProgressionCurvePoint(0, 0);
        var prior = _progressionCurveEditors.Count > 1
            ? _progressionCurveEditors[^2].BuildPoint().RequiredExperience
            : 0;
        var increment = Math.Max(100, previous.RequiredExperience - prior);
        AddProgressionCurveEditor(new ProgressionCurvePoint(previous.Level + 1,
            previous.RequiredExperience + increment));
        SetStatus("Palier ajouté au formulaire · prévisualisez avant d’appliquer");
    }

    private void AddProgressionCurveEditor(ProgressionCurvePoint point)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(StudioStyle.Label("Niveau", 9, StudioStyle.Muted));
        var level = Spin(1, 10_000, 1, string.Empty, point.Level);
        level.CustomMinimumSize = new Vector2(90, StudioStyle.ControlHeight);
        row.AddChild(level);
        row.AddChild(StudioStyle.Label("Expérience totale requise", 9, StudioStyle.Muted));
        var experience = Spin(0, 1_000_000_000, 1, " XP", point.RequiredExperience);
        experience.CustomMinimumSize = new Vector2(165, StudioStyle.ControlHeight);
        row.AddChild(experience);
        var remove = StudioStyle.Button("Retirer", compact: true);
        row.AddChild(remove);
        var hint = StudioStyle.Label(point.Level == 1 ? "Point de départ obligatoire" : string.Empty,
            9, StudioStyle.Muted);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.CustomMinimumSize = new Vector2(180, 0);
        row.AddChild(hint);
        var editor = new ProgressionCurveEditor(row, level, experience);
        _progressionCurveEditors.Add(editor);
        if (point.Level == 1)
        {
            level.Editable = false;
            experience.Editable = false;
            remove.Disabled = true;
        }
        remove.Pressed += () => RemoveProgressionCurveEditor(editor);
        _progressionCurveList.AddChild(row);
    }

    private void RemoveProgressionCurveEditor(ProgressionCurveEditor editor)
    {
        if (_progressionCurveEditors.Count <= 1)
        {
            SetStatus("Conservez au moins le niveau de départ.", error: true);
            return;
        }
        _progressionCurveEditors.Remove(editor);
        _progressionCurveList.RemoveChild(editor.Root);
        editor.Root.QueueFree();
        SetStatus("Palier retiré du formulaire · prévisualisez avant d’appliquer");
    }

    private void AddProgressionSourceFromUi()
    {
        var visible = _newProgressionSource.Text.Trim();
        if (visible.Length < 2)
        {
            SetStatus("Donnez un nom visible à la source d’expérience.", error: true);
            return;
        }
        var key = UniqueProgressionSourceKey(Slugify(visible));
        AddProgressionSourceEditor(new StudioProgressionSourceDraft(key, visible, 1));
        _newProgressionSource.Text = string.Empty;
        SetStatus("Source ajoutée au formulaire · prévisualisez avant d’appliquer");
    }

    private void AddProgressionSourceEditor(StudioProgressionSourceDraft draft)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = StudioStyle.Label(draft.DisplayName, 10);
        name.CustomMinimumSize = new Vector2(220, StudioStyle.ControlHeight);
        row.AddChild(name);
        row.AddChild(StudioStyle.Label("Récompense", 9, StudioStyle.Muted));
        var multiplier = Spin(.01, 100, .05, " ×", draft.Multiplier);
        multiplier.CustomMinimumSize = new Vector2(110, StudioStyle.ControlHeight);
        row.AddChild(multiplier);
        var remove = StudioStyle.Button("Retirer", compact: true);
        row.AddChild(remove);
        var hint = StudioStyle.Label("Appliqué avant les réglages de groupe et de difficulté",
            9, StudioStyle.Muted);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.CustomMinimumSize = new Vector2(320, 0);
        row.AddChild(hint);
        var editor = new ProgressionSourceEditor(draft, row, name, multiplier);
        _progressionSourceEditors.Add(draft.Key, editor);
        remove.Pressed += () => RemoveProgressionSourceEditor(draft.Key);
        _progressionSourceList.AddChild(row);
    }

    private void RemoveProgressionSourceEditor(string key)
    {
        if (_progressionSourceEditors.Count <= 1)
        {
            SetStatus("Conservez au moins une source d’expérience.", error: true);
            return;
        }
        if (!_progressionSourceEditors.Remove(key, out var editor)) return;
        _progressionSourceList.RemoveChild(editor.Root);
        editor.Root.QueueFree();
        SetStatus("Source retirée du formulaire · prévisualisez avant d’appliquer");
    }

    private string UniqueProgressionSourceKey(string requested)
    {
        if (!_progressionSourceEditors.ContainsKey(requested)) return requested;
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{requested}_{index}";
            if (!_progressionSourceEditors.ContainsKey(candidate)) return candidate;
        }
        return requested + "_nouvelle";
    }

    private void SynchronizeCalendarBounds()
    {
        var days = Math.Max(1, (int)_daysPerMonth.Value);
        var months = Math.Max(1, (int)_monthsPerYear.Value);
        var hours = Math.Max(1, (int)_hoursPerDay.Value);
        var minutes = Math.Max(1, (int)_minutesPerHour.Value);
        _initialDay.MaxValue = days;
        _initialMonth.MaxValue = months;
        _initialHour.MaxValue = hours - 1;
        _initialMinute.MaxValue = minutes - 1;
        _initialDay.Value = Math.Min(_initialDay.Value, days);
        _initialMonth.Value = Math.Min(_initialMonth.Value, months);
        _initialHour.Value = Math.Min(_initialHour.Value, hours - 1);
        _initialMinute.Value = Math.Min(_initialMinute.Value, minutes - 1);
        foreach (var editor in _astronomyEditors.Values)
        {
            editor.Hour.MaxValue = hours - 1;
            editor.Minute.MaxValue = minutes - 1;
            editor.Hour.Value = Math.Min(editor.Hour.Value, hours - 1);
            editor.Minute.Value = Math.Min(editor.Minute.Value, minutes - 1);
        }
        var dayMinutes = (long)hours * minutes;
        foreach (var editor in _timeZoneEditors)
        {
            editor.Offset.MinValue = -(dayMinutes - 1);
            editor.Offset.MaxValue = dayMinutes - 1;
            editor.Offset.Value = Math.Clamp(editor.Offset.Value, editor.Offset.MinValue, editor.Offset.MaxValue);
        }
    }

    private void LoadInitialDate(StudioWorldClockDraft clock)
    {
        var minutesPerDay = (long)clock.HoursPerDay * clock.MinutesPerHour;
        var daysPerYear = (long)clock.DaysPerMonth * clock.MonthsPerYear;
        var totalDays = clock.InitialMinute / minutesPerDay;
        var minuteInDay = clock.InitialMinute % minutesPerDay;
        _initialYear.Value = totalDays / daysPerYear + 1;
        var dayInYear = totalDays % daysPerYear;
        _initialMonth.Value = dayInYear / clock.DaysPerMonth + 1;
        _initialDay.Value = dayInYear % clock.DaysPerMonth + 1;
        _initialHour.Value = minuteInDay / clock.MinutesPerHour;
        _initialMinute.Value = minuteInDay % clock.MinutesPerHour;
    }

    private long BuildInitialMinute()
    {
        var days = (long)_daysPerMonth.Value;
        var months = (long)_monthsPerYear.Value;
        var hours = (long)_hoursPerDay.Value;
        var minutes = (long)_minutesPerHour.Value;
        var totalDays = (((long)_initialYear.Value - 1) * months +
                         ((long)_initialMonth.Value - 1)) * days +
                        ((long)_initialDay.Value - 1);
        return checked(totalDays * hours * minutes + (long)_initialHour.Value * minutes +
                       (long)_initialMinute.Value);
    }

    private void PrepareUx0HEvidence(string mode)
    {
        if (string.Equals(mode, "ux0-h-abort", StringComparison.OrdinalIgnoreCase) &&
            _sequenceAbortBinding.ScriptPath is null && _scripts.Count > 0)
        {
            _sequenceAbortBinding.UseScript(_scripts[0]);
            _sequenceAbortBinding.Required.ButtonPressed = true;
        }
        if (string.Equals(mode, "ux0-h-time", StringComparison.OrdinalIgnoreCase))
        {
            _worldSpeed.Value = 4;
            _worldPaused.ButtonPressed = false;
        }
        if (string.Equals(mode, "ux0-h-progression", StringComparison.OrdinalIgnoreCase) &&
            _progressionCurveEditors.Count < 4)
        {
            AddProgressionLevelFromUi();
        }
    }

    internal bool RunUx0HTransactionEvidence()
    {
        RefreshFromSession();
        var original = _session.Project;
        var originalSpeed = _worldSpeed.Value;
        _worldSpeed.Value = Math.Min(10_000, originalSpeed + .25);
        var draft = BuildDraft();
        var preview = _session.PreviewProjectEventConfiguration(draft);
        var previewDidNotMutate = ReferenceEquals(original, _session.Project);
        var applied = _session.ApplyProjectEventConfiguration(draft);
        var configured = applied.Succeeded && _session.Project!.Module.RuntimePolicies.World.Clock.Speed
            .Equals(originalSpeed + .25);
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(original, _session.Project) && !_session.IsDirty;
        RefreshFromSession();
        return preview.Succeeded && previewDidNotMutate && configured && restored;
    }

    private static string AstronomyLabel(WorldAstronomicalEventKind kind) => kind switch
    {
        WorldAstronomicalEventKind.Dawn => "Aube",
        WorldAstronomicalEventKind.Sunrise => "Lever du soleil",
        WorldAstronomicalEventKind.Sunset => "Coucher du soleil",
        WorldAstronomicalEventKind.Dusk => "Crépuscule",
        WorldAstronomicalEventKind.Moonrise => "Lever de lune",
        WorldAstronomicalEventKind.Midnight => "Minuit",
        _ => kind.ToString(),
    };

    private sealed record AstronomyEditor(CheckBox Enabled, SpinBox Hour, SpinBox Minute);

    private sealed record TimeZoneEditor(
        StudioWorldTimeZoneDraft Source,
        Label DisplayName,
        SpinBox Offset)
    {
        internal StudioWorldTimeZoneDraft BuildDraft() => Source with
        {
            OffsetMinutes = (int)Offset.Value,
            Configured = Source.Configured || (int)Offset.Value != 0,
        };
    }

    private sealed record ProgressionCurveEditor(Control Root, SpinBox Level, SpinBox Experience)
    {
        internal ProgressionCurvePoint BuildPoint() => new((int)Level.Value, Experience.Value);
    }

    private sealed record ProgressionSourceEditor(
        StudioProgressionSourceDraft Source,
        Control Root,
        Label DisplayName,
        SpinBox Multiplier)
    {
        internal StudioProgressionSourceDraft BuildDraft() => Source with
        {
            Multiplier = Multiplier.Value,
        };
    }
}
