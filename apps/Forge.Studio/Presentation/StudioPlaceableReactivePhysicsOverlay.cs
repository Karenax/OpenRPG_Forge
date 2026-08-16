using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableReactivePhysicsOverlay : PanelContainer
{
    private readonly StudioSessionController _session;
    private readonly OptionButton _placeable;
    private readonly Label _empty;
    private readonly ScrollContainer _formScroll;

    private readonly CheckBox _physicalEnabled;
    private readonly EventRow _physicalTargeted;
    private readonly EventRow _physicalResolved;
    private readonly Label _physicalPreviewState;

    private readonly CheckBox _spellEnabled;
    private readonly EventRow _abilityTargeted;
    private readonly EventRow _abilityImpacted;
    private readonly Label _spellPreviewState;

    private readonly CheckBox _schedulerEnabled;
    private readonly EventRow _heartbeat;
    private readonly SpinBox _heartbeatInterval;
    private readonly SpinBox _heartbeatBudget;
    private readonly CheckBox _suspendWhenUnobserved;
    private readonly LineEdit _customNamespace;
    private readonly LineEdit _customEventName;
    private readonly EventRow _customEvent;
    private readonly Label _heartbeatPreviewState;

    private readonly CheckBox _physicsEnabled;
    private readonly OptionButton _bodyMode;
    private readonly SpinBox _mass;
    private readonly LineEdit _constraints;
    private readonly CheckBox _replicated;
    private readonly SpinBox _sleepAfter;
    private readonly CheckBox _convertToStatic;
    private readonly OptionButton _authority;
    private readonly Label _physicsPreviewState;

    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly List<BlueprintId> _placeableIds = [];
    private StudioPlaceableReactivePhysicsDraft? _draft;
    private StudioUx1ZPreview? _preview;
    private bool _loading;

    internal event Action? CloseRequested;
    internal event Action? ProjectChanged;
    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal int PlaceableCount => _placeableIds.Count;
    internal bool PreviewReady => _preview?.Succeeded == true &&
                                  _physicalPreviewState.Text.StartsWith("Aperçu", StringComparison.Ordinal) &&
                                  _spellPreviewState.Text.StartsWith("Aperçu", StringComparison.Ordinal) &&
                                  _heartbeatPreviewState.Text.StartsWith("Aperçu", StringComparison.Ordinal) &&
                                  _physicsPreviewState.Text.StartsWith("Aperçu", StringComparison.Ordinal);

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ',
                Enumerable.Range(0, _placeable.ItemCount).Select(_placeable.GetItemText)
                    .Append(_empty.Text)
                    .Append(_physicalTargeted.HumanText)
                    .Append(_physicalResolved.HumanText)
                    .Append(_abilityTargeted.HumanText)
                    .Append(_abilityImpacted.HumanText)
                    .Append(_heartbeat.HumanText)
                    .Append(_customEvent.HumanText)
                    .Append(_customNamespace.Text)
                    .Append(_customEventName.Text)
                    .Append(_constraints.Text)
                    .Append(_physicalPreviewState.Text)
                    .Append(_spellPreviewState.Text)
                    .Append(_heartbeatPreviewState.Text)
                    .Append(_physicsPreviewState.Text)
                    .Append(_summary.Text)
                    .Append(_status.Text));
            return !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains('\\');
        }
    }

    internal StudioPlaceableReactivePhysicsOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioPlaceableReactivePhysicsOverlay";
        Visible = false;
        ZIndex = 20;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Chrome, StudioStyle.Border, 9, 2));

        var margin = StudioStyle.Margin(18, 14, 18, 14);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        root.AddChild(header);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("ÉVÉNEMENTS RÉACTIFS ET PHYSIQUE", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Faire réagir un placeable sans éditer ses données techniques", 20));
        titles.AddChild(StudioStyle.Label(
            "Attaques · capacités · rythme borné · événement personnalisé · politique physique · aperçu avant application",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon";
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        var workspace = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        workspace.AddThemeConstantOverride("separation", 10);
        root.AddChild(workspace);

        _formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
            CustomMinimumSize = new Vector2(720, 0),
        };
        workspace.AddChild(_formScroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 10);
        _formScroll.AddChild(form);

        var selection = AddCard(form, "PLACEABLE À CONFIGURER",
            "Tous les blueprints Placeable sont listés. Sans profil existant, l’application crée une fiche réactive minimale sans visuel inventé.");
        _placeable = AddOption(selection, "Objet");
        _empty = StudioStyle.Label(string.Empty, 10, StudioStyle.Warning, wrap: true);
        selection.AddChild(_empty);

        var physical = AddCard(form, "ONPHYSICALATTACKED",
            "Publier séparément le ciblage et le résultat d’une attaque physique.");
        _physicalEnabled = AddCheck(physical, "Activer la réaction aux attaques physiques");
        _physicalTargeted = new EventRow("Attaque ciblée", "Script appelé quand l’objet devient la cible");
        _physicalResolved = new EventRow("Attaque résolue", "Script appelé après résolution de l’attaque");
        physical.AddChild(_physicalTargeted.Root);
        physical.AddChild(_physicalResolved.Root);
        var previewPhysical = StudioStyle.Button("Prévisualiser l’attaque", compact: true);
        physical.AddChild(previewPhysical);
        _physicalPreviewState = PreviewLabel();
        physical.AddChild(_physicalPreviewState);

        var spell = AddCard(form, "ONSPELLCASTAT",
            "Réagir à une capacité générique ciblée puis à son impact, sans dépendre d’un système de sorts particulier.");
        _spellEnabled = AddCheck(spell, "Activer la réaction aux capacités");
        _abilityTargeted = new EventRow("Capacité ciblée", "Script appelé lors du ciblage");
        _abilityImpacted = new EventRow("Impact de capacité", "Script appelé quand l’impact est appliqué");
        spell.AddChild(_abilityTargeted.Root);
        spell.AddChild(_abilityImpacted.Root);
        var previewSpell = StudioStyle.Button("Prévisualiser la capacité", compact: true);
        spell.AddChild(previewSpell);
        _spellPreviewState = PreviewLabel();
        spell.AddChild(_spellPreviewState);

        var scheduler = AddCard(form, "ONHEARTBEAT ET ONUSERDEFINED",
            "Limiter la cadence, le budget et le rattrapage, puis nommer l’événement personnalisé dans son espace.");
        _schedulerEnabled = AddCheck(scheduler, "Activer le rythme et l’événement personnalisé");
        _heartbeat = new EventRow("Rythme du placeable", "Script périodique soumis à la cadence et au budget");
        scheduler.AddChild(_heartbeat.Root);
        var cadenceGrid = new GridContainer { Columns = 2 };
        cadenceGrid.AddThemeConstantOverride("h_separation", 10);
        cadenceGrid.AddThemeConstantOverride("v_separation", 6);
        scheduler.AddChild(cadenceGrid);
        _heartbeatInterval = AddSpin(cadenceGrid, "Intervalle", .25, 3600, .25, " s");
        _heartbeatBudget = AddSpin(cadenceGrid, "Budget par minute", 1, 10000, 1, string.Empty);
        _suspendWhenUnobserved = AddCheck(scheduler, "Suspendre quand aucun joueur n’observe l’objet");
        _customNamespace = AddLine(scheduler, "Espace de noms du planificateur", "placeable.zone");
        _customEventName = AddLine(scheduler, "Nom qualifié de l’événement", "custom:placeable:pulse");
        _customEvent = new EventRow("Événement personnalisé", "Script appelé pour ce nom qualifié");
        scheduler.AddChild(_customEvent.Root);
        var previewHeartbeat = StudioStyle.Button("Prévisualiser le rythme et l’événement", compact: true);
        scheduler.AddChild(previewHeartbeat);
        _heartbeatPreviewState = PreviewLabel();
        scheduler.AddChild(_heartbeatPreviewState);

        var physics = AddCard(form, "PHYSIQUE DYNAMIQUE",
            "Décrire une politique canonique. Cet aperçu ne lance ni corps moteur, ni simulation réseau.");
        _physicsEnabled = AddCheck(physics, "Activer la politique physique");
        _bodyMode = AddOption(physics, "Type de corps");
        _bodyMode.AddItem("Statique");
        _bodyMode.AddItem("Rigide");
        _bodyMode.AddItem("Cinématique");
        _mass = AddSpin(physics, "Masse", .01, 100000, .01, " kg");
        _constraints = AddLine(physics, "Contraintes", "lock-y, lock-rotation-x");
        _replicated = AddCheck(physics, "Demander la réplication de la politique");
        _sleepAfter = AddSpin(physics, "Sommeil après", 0, 3600, .25, " s");
        _convertToStatic = AddCheck(physics, "Convertir en statique après sommeil");
        _authority = AddOption(physics, "Autorité");
        _authority.AddItem("Serveur");
        _authority.AddItem("Locale");
        var previewPhysics = StudioStyle.Button("Prévisualiser la politique", compact: true);
        physics.AddChild(previewPhysics);
        _physicsPreviewState = PreviewLabel();
        physics.AddChild(_physicsPreviewState);

        var previewPanel = StudioStyle.Panel("Ux1ZPreview", StudioStyle.Surface, 8);
        previewPanel.CustomMinimumSize = new Vector2(470, 0);
        workspace.AddChild(previewPanel);
        var previewMargin = StudioStyle.Margin(14, 12, 14, 12);
        previewPanel.AddChild(previewMargin);
        var previewColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        previewColumn.AddThemeConstantOverride("separation", 9);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.Label("APERÇU GLOBAL", 10, StudioStyle.Accent));
        previewColumn.AddChild(StudioStyle.Label("Contrats avant application", 18));
        previewColumn.AddChild(StudioStyle.Label(
            "Chaque bouton de section projette le contrat canonique sans écrire. L’aperçu global autorise ensuite l’application transactionnelle.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _summary = StudioStyle.Label(
            "Aucun aperçu global. Les données du module restent inchangées.",
            11,
            StudioStyle.Muted,
            wrap: true);
        _summary.SizeFlagsVertical = SizeFlags.ExpandFill;
        _summary.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        previewColumn.AddChild(_summary);
        var previewAll = StudioStyle.Button("Prévisualiser les parcours actifs", primary: true);
        previewColumn.AddChild(previewAll);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.Disabled = true;
        previewColumn.AddChild(_apply);
        var history = new HBoxContainer();
        history.AddThemeConstantOverride("separation", 8);
        previewColumn.AddChild(history);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        history.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        history.AddChild(_redo);
        _status = StudioStyle.Label("Surface prête · aucune écriture effectuée.", 10, StudioStyle.Muted, wrap: true);
        previewColumn.AddChild(_status);

        _placeable.ItemSelected += _ => SelectPlaceable();
        _physicalEnabled.Toggled += _ => OnChanged();
        _spellEnabled.Toggled += _ => OnChanged();
        _schedulerEnabled.Toggled += _ => OnChanged();
        _physicsEnabled.Toggled += _ => OnChanged();
        _physicalTargeted.Changed += OnChanged;
        _physicalResolved.Changed += OnChanged;
        _abilityTargeted.Changed += OnChanged;
        _abilityImpacted.Changed += OnChanged;
        _heartbeat.Changed += OnChanged;
        _customEvent.Changed += OnChanged;
        _heartbeatInterval.ValueChanged += _ => OnChanged();
        _heartbeatBudget.ValueChanged += _ => OnChanged();
        _suspendWhenUnobserved.Toggled += _ => OnChanged();
        _customNamespace.TextChanged += _ => OnChanged();
        _customEventName.TextChanged += _ => OnChanged();
        _bodyMode.ItemSelected += _ => OnChanged();
        _mass.ValueChanged += _ => OnChanged();
        _constraints.TextChanged += _ => OnChanged();
        _replicated.Toggled += _ => OnChanged();
        _sleepAfter.ValueChanged += _ => OnChanged();
        _convertToStatic.Toggled += _ => OnChanged();
        _authority.ItemSelected += _ => OnChanged();
        previewPhysical.Pressed += PreviewPhysical;
        previewSpell.Pressed += PreviewSpell;
        previewHeartbeat.Pressed += PreviewHeartbeat;
        previewPhysics.Pressed += PreviewPhysics;
        previewAll.Pressed += PreviewAll;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
    }

    internal void Open(BlueprintId? requestedBlueprintId = null)
    {
        _session.CancelUx1ZPreview();
        Visible = true;
        _loading = true;
        _placeable.Clear();
        _placeableIds.Clear();
        _placeable.AddItem("Choisir un placeable…");
        var catalog = _session.CreateUx1ZCatalog();
        foreach (var option in catalog.Placeables)
        {
            _placeable.AddItem(option.HasProfile
                ? option.DisplayName
                : $"{option.DisplayName} · profil réactif minimal à créer · aucun visuel ajouté");
            _placeableIds.Add(option.BlueprintId);
        }
        _empty.Text = _placeableIds.Count == 0
            ? "État vide · ce module ne contient aucun blueprint Placeable."
            : string.Empty;
        _placeable.Disabled = _placeableIds.Count == 0;
        var requested = requestedBlueprintId is null
            ? -1
            : _placeableIds.FindIndex(id => id == requestedBlueprintId);
        _placeable.Select(requested < 0 ? 0 : requested + 1);
        _loading = false;
        if (_placeable.Selected > 0)
        {
            SelectPlaceable();
        }
        else
        {
            _draft = null;
            _preview = null;
            _apply.Disabled = true;
            _summary.Text = "Choisissez explicitement un placeable pour ouvrir sa configuration.";
            _status.Text = "Aucune sélection implicite · le module reste inchangé.";
        }
    }

    internal void Close()
    {
        _session.CloseUx1Z();
        _preview = null;
        _draft = null;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        if (_placeableIds.Count > 0)
        {
            _placeable.Select(1);
            SelectPlaceable();
        }
        if (_draft is null)
        {
            return false;
        }
        SetEvidenceValues();
        PreviewPhysical();
        PreviewSpell();
        PreviewHeartbeat();
        PreviewPhysics();
        PreviewAll();
        _formScroll.SetDeferred("scroll_vertical",
            mode.Contains("physics", StringComparison.OrdinalIgnoreCase) ? int.MaxValue : 0);
        return PreviewReady && ShowsHumanSurface;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        Open();
        if (_placeableIds.Count > 0)
        {
            _placeable.Select(1);
            SelectPlaceable();
        }
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        SetEvidenceValues();
        PreviewPhysical();
        PreviewSpell();
        PreviewHeartbeat();
        PreviewPhysics();
        PreviewAll();
        var visible = PreviewReady && ShowsHumanSurface;
        var applied = _session.ApplyApprovedUx1Z();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var physicsCanonical = profile?.Physics is
        {
            Mode: PlaceableWorldProfile.BodyMode.Rigid,
            Mass: 12,
            Replicated: true,
            Authority: "server",
        };
        var eventsCanonical = profile is not null && new[]
        {
            "physical-attack-targeted",
            "physical-attack-resolved",
            "ability-targeted",
            "ability-impacted",
            "heartbeat",
            "custom:reliquaire:pulse",
        }.All(eventName => profile.Reactive.Events.Any(item => item.Event == eventName));
        var schedulerCanonical = profile?.Reactive.Scheduler is
             {
                 Enabled: true,
                 IntervalSeconds: 5,
                 BudgetPerMinute: 12,
                 CustomNamespace: "reliquaire.veilleurs",
             };
        var canonical = applied.Succeeded && physicsCanonical && eventsCanonical && schedulerCanonical;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        var valid = visible && canonical && restored;
        if (!valid)
        {
            var physics = profile?.Physics;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1Z_UI_TRANSACTION_FAILED visible={visible.ToString().ToLowerInvariant()} " +
                $"apply_succeeded={applied.Succeeded.ToString().ToLowerInvariant()} canonical={canonical.ToString().ToLowerInvariant()} " +
                $"physics={physicsCanonical.ToString().ToLowerInvariant()} events={eventsCanonical.ToString().ToLowerInvariant()} " +
                $"scheduler={schedulerCanonical.ToString().ToLowerInvariant()} " +
                $"mode={physics?.Mode.ToString() ?? "missing"} mass={physics?.Mass.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "missing"} " +
                $"replicated={physics?.Replicated.ToString().ToLowerInvariant() ?? "missing"} authority={physics?.Authority ?? "missing"} " +
                $"undo_succeeded={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        return valid;
    }

    private void SelectPlaceable()
    {
        if (_loading || _placeable.Selected <= 0 || _placeable.Selected > _placeableIds.Count)
        {
            return;
        }
        Load(_session.CreateUx1ZDraft(_placeableIds[_placeable.Selected - 1]));
    }

    private void Load(StudioPlaceableReactivePhysicsDraft? draft)
    {
        _loading = true;
        _session.CancelUx1ZPreview();
        _draft = draft;
        _preview = null;
        _apply.Disabled = true;
        if (draft is null)
        {
            _summary.Text = "Ce profil de placeable n’est plus disponible.";
            _loading = false;
            return;
        }
        _physicalEnabled.ButtonPressed = draft.PhysicalAttackEnabled;
        _physicalTargeted.Load(draft.PhysicalAttackTargeted);
        _physicalResolved.Load(draft.PhysicalAttackResolved);
        _spellEnabled.ButtonPressed = draft.SpellCastEnabled;
        _abilityTargeted.Load(draft.AbilityTargeted);
        _abilityImpacted.Load(draft.AbilityImpacted);
        _schedulerEnabled.ButtonPressed = draft.SchedulerEnabled;
        _heartbeat.Load(draft.Heartbeat);
        _heartbeatInterval.Value = draft.HeartbeatIntervalSeconds;
        _heartbeatBudget.Value = draft.HeartbeatBudgetPerMinute;
        _suspendWhenUnobserved.ButtonPressed = draft.SuspendWhenUnobserved;
        _customNamespace.Text = draft.CustomNamespace;
        _customEventName.Text = draft.CustomEvent.EventName;
        _customEvent.Load(draft.CustomEvent);
        _physicsEnabled.ButtonPressed = draft.PhysicsEnabled;
        _bodyMode.Select((int)draft.BodyMode);
        _mass.Value = draft.Mass;
        _constraints.Text = draft.ConstraintsText;
        _replicated.ButtonPressed = draft.Replicated;
        _sleepAfter.Value = draft.SleepAfterSeconds;
        _convertToStatic.ButtonPressed = draft.ConvertToStaticWhenSleeping;
        _authority.Select(string.Equals(draft.Authority, "local", StringComparison.Ordinal) ? 1 : 0);
        ResetPreviewLabels();
        _summary.Text = "Aucun aperçu global. Les données du module restent inchangées.";
        _status.Text = "Configuration chargée sans mutation.";
        UpdateEnabledState();
        _loading = false;
    }

    private StudioPlaceableReactivePhysicsDraft? Capture()
    {
        if (_draft is null)
        {
            return null;
        }
        return _draft with
        {
            PhysicalAttackEnabled = _physicalEnabled.ButtonPressed,
            PhysicalAttackTargeted = _physicalTargeted.Capture(_draft.PhysicalAttackTargeted),
            PhysicalAttackResolved = _physicalResolved.Capture(_draft.PhysicalAttackResolved),
            SpellCastEnabled = _spellEnabled.ButtonPressed,
            AbilityTargeted = _abilityTargeted.Capture(_draft.AbilityTargeted),
            AbilityImpacted = _abilityImpacted.Capture(_draft.AbilityImpacted),
            SchedulerEnabled = _schedulerEnabled.ButtonPressed,
            Heartbeat = _heartbeat.Capture(_draft.Heartbeat),
            HeartbeatIntervalSeconds = _heartbeatInterval.Value,
            HeartbeatBudgetPerMinute = (int)_heartbeatBudget.Value,
            SuspendWhenUnobserved = _suspendWhenUnobserved.ButtonPressed,
            CustomNamespace = _customNamespace.Text,
            CustomEvent = _customEvent.Capture(_draft.CustomEvent) with { EventName = _customEventName.Text },
            PhysicsEnabled = _physicsEnabled.ButtonPressed,
            BodyMode = (PlaceableWorldProfile.BodyMode)_bodyMode.Selected,
            Mass = _mass.Value,
            ConstraintsText = _constraints.Text,
            Replicated = _replicated.ButtonPressed,
            SleepAfterSeconds = _sleepAfter.Value,
            ConvertToStaticWhenSleeping = _convertToStatic.ButtonPressed,
            Authority = _authority.Selected == 1 ? "local" : "server",
        };
    }

    private void PreviewPhysical() => PreviewPath(
        _session.PreviewUx1ZPhysicalAttack,
        _physicalPreviewState);

    private void PreviewSpell() => PreviewPath(
        _session.PreviewUx1ZSpellCast,
        _spellPreviewState);

    private void PreviewHeartbeat() => PreviewPath(
        _session.PreviewUx1ZHeartbeatAndCustom,
        _heartbeatPreviewState);

    private void PreviewPhysics() => PreviewPath(
        _session.PreviewUx1ZPhysics,
        _physicsPreviewState);

    private void PreviewPath(
        Func<StudioPlaceableReactivePhysicsDraft, StudioUx1ZPathPreview> preview,
        Label target)
    {
        var draft = Capture();
        if (draft is null)
        {
            return;
        }
        var result = preview(draft);
        target.Text = result.Summary;
        target.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Warning);
        SetStatus(result.Summary, !result.Succeeded);
    }

    private void PreviewAll()
    {
        var draft = Capture();
        if (draft is null)
        {
            return;
        }
        _preview = _session.PreviewUx1Z(draft);
        _summary.Text = _preview.Summary;
        _summary.AddThemeColorOverride("font_color", _preview.Succeeded ? StudioStyle.Success : StudioStyle.Warning);
        _apply.Disabled = !_preview.CanApply;
        SetStatus(_preview.Message, !_preview.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedUx1Z();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        var selected = _draft?.BlueprintId;
        ProjectChanged?.Invoke();
        Open(selected);
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            var selected = _draft?.BlueprintId;
            ProjectChanged?.Invoke();
            Open(selected);
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            var selected = _draft?.BlueprintId;
            ProjectChanged?.Invoke();
            Open(selected);
        }
    }

    private void OnChanged()
    {
        if (_loading)
        {
            return;
        }
        _session.CancelUx1ZPreview();
        _preview = null;
        _apply.Disabled = true;
        _summary.Text = "Aperçu à recalculer · aucune donnée n’a été appliquée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        const string status = "Brouillon modifié · prévisualisez à nouveau avant d’appliquer.";
        _status.Text = status;
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        StatusChanged?.Invoke(status, false);
        ResetPreviewLabels();
        UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        _physicalTargeted.SetEnabled(_physicalEnabled.ButtonPressed);
        _physicalResolved.SetEnabled(_physicalEnabled.ButtonPressed);
        _abilityTargeted.SetEnabled(_spellEnabled.ButtonPressed);
        _abilityImpacted.SetEnabled(_spellEnabled.ButtonPressed);
        _heartbeat.SetEnabled(_schedulerEnabled.ButtonPressed);
        _heartbeatInterval.Editable = _schedulerEnabled.ButtonPressed;
        _heartbeatBudget.Editable = _schedulerEnabled.ButtonPressed;
        _suspendWhenUnobserved.Disabled = !_schedulerEnabled.ButtonPressed;
        _customNamespace.Editable = _schedulerEnabled.ButtonPressed;
        _customEventName.Editable = _schedulerEnabled.ButtonPressed;
        _customEvent.SetEnabled(_schedulerEnabled.ButtonPressed);
        _bodyMode.Disabled = !_physicsEnabled.ButtonPressed;
        _mass.Editable = _physicsEnabled.ButtonPressed;
        _constraints.Editable = _physicsEnabled.ButtonPressed;
        _replicated.Disabled = !_physicsEnabled.ButtonPressed;
        _sleepAfter.Editable = _physicsEnabled.ButtonPressed;
        _convertToStatic.Disabled = !_physicsEnabled.ButtonPressed;
        _authority.Disabled = !_physicsEnabled.ButtonPressed;
    }

    private void SetEvidenceValues()
    {
        _loading = true;
        _physicalEnabled.ButtonPressed = true;
        _physicalTargeted.Set("scripts/reliquaire_attack_targeted", true, true);
        _physicalResolved.Set("scripts/reliquaire_attack_resolved", false, true);
        _spellEnabled.ButtonPressed = true;
        _abilityTargeted.Set("scripts/reliquaire_ability_targeted", true, true);
        _abilityImpacted.Set("scripts/reliquaire_ability_impacted", false, true);
        _schedulerEnabled.ButtonPressed = true;
        _heartbeat.Set("scripts/reliquaire_heartbeat", false, true);
        _heartbeatInterval.Value = 5;
        _heartbeatBudget.Value = 12;
        _suspendWhenUnobserved.ButtonPressed = true;
        _customNamespace.Text = "reliquaire.veilleurs";
        _customEventName.Text = "custom:reliquaire:pulse";
        _customEvent.Set("scripts/reliquaire_pulse", false, true);
        _physicsEnabled.ButtonPressed = true;
        _bodyMode.Select((int)PlaceableWorldProfile.BodyMode.Rigid);
        _mass.Value = 12;
        _constraints.Text = "lock-y, lock-rotation-x";
        _replicated.ButtonPressed = true;
        _sleepAfter.Value = 4;
        _convertToStatic.ButtonPressed = true;
        _authority.Select(0);
        UpdateEnabledState();
        _loading = false;
        OnChanged();
    }

    private void ResetPreviewLabels()
    {
        foreach (var label in new[]
                 { _physicalPreviewState, _spellPreviewState, _heartbeatPreviewState, _physicsPreviewState })
        {
            label.Text = "Aucun aperçu pour cette section.";
            label.AddThemeColorOverride("font_color", StudioStyle.Muted);
        }
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Warning : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private static VBoxContainer AddCard(Container parent, string title, string description)
    {
        var panel = StudioStyle.Panel($"Ux1Z{title.Replace(" ", string.Empty)}", StudioStyle.Surface, 8);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        column.AddChild(StudioStyle.Label(description, 10, StudioStyle.Muted, wrap: true));
        return column;
    }

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder };
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

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }

    private static SpinBox AddSpin(
        Container parent,
        string label,
        double minimum,
        double maximum,
        double step,
        string suffix)
    {
        var group = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        group.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Suffix = suffix,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        group.AddChild(spin);
        parent.AddChild(group);
        return spin;
    }

    private static Label PreviewLabel()
    {
        var label = StudioStyle.Label("Aucun aperçu pour cette section.", 10, StudioStyle.Muted, wrap: true);
        label.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        return label;
    }

    private sealed class EventRow
    {
        private readonly LineEdit _script;
        private readonly CheckBox _before;
        private readonly CheckBox _authoritative;
        private bool _loading;

        internal event Action? Changed;

        internal VBoxContainer Root { get; }

        internal string HumanText => string.Join(' ', _script.Text, _before.Text, _authoritative.Text);

        internal EventRow(string title, string description)
        {
            Root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            Root.AddThemeConstantOverride("separation", 5);
            Root.AddChild(StudioStyle.Label(title, 11));
            Root.AddChild(StudioStyle.Label(description, 10, StudioStyle.Muted, wrap: true));
            _script = AddLine(Root, "Script", "scripts/placeable_event");
            var flags = new HBoxContainer();
            flags.AddThemeConstantOverride("separation", 12);
            Root.AddChild(flags);
            _before = AddCheck(flags, "Avant la résolution");
            _authoritative = AddCheck(flags, "Autorité serveur");
            _script.TextChanged += _ => Notify();
            _before.Toggled += _ => Notify();
            _authoritative.Toggled += _ => Notify();
        }

        internal void Load(StudioUx1ZEventDraft draft) =>
            Set(draft.ScriptId, draft.Before, draft.Authoritative);

        internal void Set(string script, bool before, bool authoritative)
        {
            _loading = true;
            _script.Text = script;
            _before.ButtonPressed = before;
            _authoritative.ButtonPressed = authoritative;
            _loading = false;
        }

        internal StudioUx1ZEventDraft Capture(StudioUx1ZEventDraft source) => source with
        {
            ScriptId = _script.Text,
            Before = _before.ButtonPressed,
            Authoritative = _authoritative.ButtonPressed,
        };

        internal void SetEnabled(bool enabled)
        {
            _script.Editable = enabled;
            _before.Disabled = !enabled;
            _authoritative.Disabled = !enabled;
        }

        private void Notify()
        {
            if (!_loading)
            {
                Changed?.Invoke();
            }
        }
    }
}
