using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDoorReactiveComplexOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _door;
    private readonly Button _eventsTab;
    private readonly Button _referencesTab;
    private readonly Button _assemblyTab;
    private readonly VBoxContainer _eventsPanel;
    private readonly VBoxContainer _referencesPanel;
    private readonly VBoxContainer _assemblyPanel;
    private readonly CheckBox _abilityTargetedEnabled;
    private readonly LineEdit _abilityTargetedScript;
    private readonly CheckBox _abilityImpactedEnabled;
    private readonly LineEdit _abilityImpactedScript;
    private readonly CheckBox _heartbeatEnabled;
    private readonly LineEdit _heartbeatScript;
    private readonly OptionButton _customChoice;
    private readonly CheckBox _customEnabled;
    private readonly LineEdit _customNamespace;
    private readonly LineEdit _customName;
    private readonly LineEdit _customScript;
    private readonly CheckBox _customBefore;
    private readonly CheckBox _customAuthority;
    private readonly OptionButton _dialogue;
    private readonly OptionButton _faction;
    private readonly CheckBox _assemblyEnabled;
    private readonly OptionButton _assemblyKind;
    private readonly LineEdit _sequence;
    private readonly CheckBox _interlocked;
    private readonly LineEdit _failureRecovery;
    private readonly StudioDoorAssemblyPanelEditorUx1AD _panelEditor;
    private readonly Label _previewSummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;

    private IReadOnlyList<StudioDoorReactiveComplexChoice> _choices = [];
    private IReadOnlyList<StudioDoorDialogueChoice> _dialogueChoices = [];
    private IReadOnlyList<StudioDoorFactionChoice> _factionChoices = [];
    private IReadOnlyList<StudioDoorCustomEventChoice> _customChoices = [];
    private StudioDoorReactiveComplexDraft? _draft;
    private StudioDoorReactiveComplexDraft? _approvedDraft;
    private bool _updating;
    private bool _operationApplied;
    private bool _editingEnabled;
    private bool _selectionRequiredAtOpen;
    private int _section;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? DoorChanged;

    internal bool IsOpen => Visible;
    internal int SectionCount => 3;
    internal bool PreviewReady => Visible && _approvedDraft is not null && !_apply.Disabled;
    internal bool UiTransactionSourceUntouched { get; private set; }
    internal bool ExplicitSelectionProven =>
        _selectionRequiredAtOpen && _door.Selected > 0 && _draft is not null;
    internal bool ExplicitCustomSelectionProven =>
        _customChoice.Selected > 0 && _draft is not null;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(
                ' ',
                _eventsTab.Text,
                _referencesTab.Text,
                _assemblyTab.Text,
                _previewSummary.Text,
                _status.Text);
            return SectionCount == 3 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioDoorReactiveComplexOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioDoorReactiveComplexOverlay";
        Visible = false;
        ZIndex = 454;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("DoorReactiveComplexDialog", StudioStyle.Chrome, 12, 2);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("PORTE · RÉACTIONS ET ASSEMBLAGE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Capacités, conversation et panneaux coordonnés", 20));
        heading.AddChild(StudioStyle.Label(
            "Choisissez une porte · préparez un aperçu sans écriture · appliquez par commande annulable",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le changement en attente";
        close.Pressed += Close;
        header.AddChild(close);

        var sourceRow = new HBoxContainer();
        sourceRow.AddThemeConstantOverride("separation", 10);
        root.AddChild(sourceRow);
        sourceRow.AddChild(StudioStyle.Label("Porte à configurer", 10, StudioStyle.Muted));
        _door = Option();
        _door.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _door.TooltipText = "Sélection explicite de la porte à modifier.";
        _door.ItemSelected += _ => LoadSelectedDoor();
        sourceRow.AddChild(_door);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _eventsTab = TabButton("1 · Réactions", "Configurer capacités, rythme et un événement personnalisé choisi.");
        _eventsTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_eventsTab);
        _referencesTab = TabButton("2 · Conversation", "Lier une conversation et une faction disponibles.");
        _referencesTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_referencesTab);
        _assemblyTab = TabButton("3 · Assemblage", "Orchestrer panneaux, ordre, délais et récupération.");
        _assemblyTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_assemblyTab);

        var body = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _eventsPanel = PanelColumn();
        panels.AddChild(_eventsPanel);
        _eventsPanel.AddChild(StudioStyle.Label("Réagir aux capacités et au rythme", 17));
        _eventsPanel.AddChild(StudioStyle.Label(
            "Les réactions standard s’exécutent côté serveur. Le rythme est limité à une fois par seconde et soixante fois par minute.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var eventGrid = Grid();
        _eventsPanel.AddChild(eventGrid);
        _abilityTargetedEnabled = AddField(eventGrid, "Capacité ciblée", Check("Réagir au ciblage"));
        _abilityTargetedScript = AddField(eventGrid, "Script au ciblage", Text("Ex. scripts/door_ability_targeted"));
        _abilityImpactedEnabled = AddField(eventGrid, "Capacité reçue", Check("Réagir après l’impact"));
        _abilityImpactedScript = AddField(eventGrid, "Script après impact", Text("Ex. scripts/door_ability_impacted"));
        _heartbeatEnabled = AddField(eventGrid, "Mise à jour périodique", Check("Activer le rythme limité"));
        _heartbeatScript = AddField(eventGrid, "Script périodique", Text("Ex. scripts/door_heartbeat"));

        _eventsPanel.AddChild(StudioStyle.SectionLabel("ÉVÉNEMENT PERSONNALISÉ CHOISI"));
        _eventsPanel.AddChild(StudioStyle.Label(
            "Une seule entrée explicitement choisie est modifiée. Les autres événements personnalisés restent intacts.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var customGrid = Grid();
        _eventsPanel.AddChild(customGrid);
        _customChoice = AddField(customGrid, "Entrée à éditer", Option("Choisir ou créer…"));
        _customChoice.ItemSelected += _ => LoadSelectedCustomEvent();
        _customEnabled = AddField(customGrid, "État", Check("Conserver cette réaction"));
        _customNamespace = AddField(customGrid, "Domaine", Text("Ex. porte"));
        _customName = AddField(customGrid, "Nom", Text("Ex. signal"));
        _customScript = AddField(customGrid, "Script", Text("Ex. scripts/door_custom"));
        _customBefore = AddField(customGrid, "Moment", Check("Avant l’action"));
        _customAuthority = AddField(customGrid, "Autorité", Check("Exécuter côté serveur"));

        _referencesPanel = PanelColumn();
        panels.AddChild(_referencesPanel);
        _referencesPanel.AddChild(StudioStyle.Label("Relier la porte au récit", 17));
        _referencesPanel.AddChild(StudioStyle.Label(
            "Les choix présentent des noms lisibles et ne proposent que les contenus du module.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var referencesGrid = Grid();
        _referencesPanel.AddChild(referencesGrid);
        _dialogue = AddField(referencesGrid, "Conversation au contact", Option("Aucune conversation"));
        _faction = AddField(referencesGrid, "Faction associée", Option("Aucune faction"));

        _assemblyPanel = PanelColumn();
        panels.AddChild(_assemblyPanel);
        _assemblyPanel.AddChild(StudioStyle.Label("Orchestrer une porte complexe", 17));
        _assemblyPanel.AddChild(StudioStyle.Label(
            "Décrivez jusqu’à huit panneaux, leur décalage, leur délai et la conduite à tenir en cas d’échec.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _assemblyEnabled = AddCheck(_assemblyPanel, "Activer un assemblage coordonné");
        var assemblyGrid = Grid();
        _assemblyPanel.AddChild(assemblyGrid);
        _assemblyKind = AddField(
            assemblyGrid,
            "Type",
            Option("Simple", "Double", "Herse", "Élévateur", "Sas"));
        _sequence = AddField(assemblyGrid, "Ordre de mouvement", Text("Ex. gauche puis droite"));
        _interlocked = AddField(assemblyGrid, "Coordination", Check("Interdire les ouvertures incompatibles"));
        _failureRecovery = AddField(
            assemblyGrid,
            "Récupération d’échec",
            Text("Ex. refermer les panneaux engagés"));
        _panelEditor = new StudioDoorAssemblyPanelEditorUx1AD();
        _panelEditor.Changed += InvalidatePreview;
        _assemblyPanel.AddChild(_panelEditor);

        var previewCard = StudioStyle.Panel("DoorReactiveComplexPreview", StudioStyle.SurfaceRaised, 8);
        previewCard.CustomMinimumSize = new Vector2(0, 142);
        root.AddChild(previewCard);
        var previewMargin = StudioStyle.Margin(14, 11, 14, 11);
        previewCard.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 7);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("APERÇU SANS ÉCRITURE"));
        _previewSummary = StudioStyle.Label(
            "Choisissez une porte puis prévisualisez ses réactions, références et panneaux.",
            11,
            StudioStyle.Muted,
            wrap: true);
        previewColumn.AddChild(_previewSummary);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _status = StudioStyle.Label("Prêt · aucun changement appliqué", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
    }

    internal void Open()
    {
        _session.CancelDoorReactiveComplexPreviewUx1AD();
        _choices = _session.DoorReactiveComplexChoicesUx1AD;
        _dialogueChoices = _session.DoorDialogueChoicesUx1AD;
        _factionChoices = _session.DoorFactionChoicesUx1AD;
        _updating = true;
        _door.Clear();
        _door.AddItem("Choisir une porte…");
        foreach (var choice in _choices)
        {
            _door.AddItem($"{choice.Label} · {(choice.Configured ? "configurée" : "à configurer")}");
        }
        _door.Selected = 0;
        _updating = false;
        _draft = null;
        _approvedDraft = null;
        _operationApplied = false;
        _selectionRequiredAtOpen = true;
        Visible = true;
        SetEditingEnabled(false);
        _door.Disabled = _choices.Count == 0;
        _preview.Disabled = true;
        _apply.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
        ShowSection(0);
        _previewSummary.Text = "Choisissez explicitement une porte avant de préparer un aperçu.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus(
            _choices.Count == 0
                ? "Ajoutez d’abord un blueprint de porte au module."
                : "Choisissez une porte · aucun brouillon n’est ouvert automatiquement.",
            _choices.Count == 0);
    }

    internal void Close()
    {
        _session.CancelDoorReactiveComplexPreviewUx1AD();
        _draft = null;
        _approvedDraft = null;
        _selectionRequiredAtOpen = false;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        if (!Visible || !SelectDoorExplicitlyForEvidence() || _draft is null)
        {
            return false;
        }
        SetEvidenceValues("preuve-ad");
        ShowSection(mode.EndsWith("references", StringComparison.OrdinalIgnoreCase)
            ? 1
            : mode.EndsWith("assembly", StringComparison.OrdinalIgnoreCase)
                ? 2
                : 0);
        Preview();
        return PreviewReady && ShowsHumanSurface && ExplicitSelectionProven &&
               ExplicitCustomSelectionProven;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        UiTransactionSourceUntouched = false;
        if (originalProject is null || originalHistory is null)
        {
            return false;
        }

        var originalDirty = _session.IsDirty;
        var originalRevision = originalHistory.State.Revision;
        var originalJournal = originalHistory.Journal.ToArray();
        var originalCanUndo = originalHistory.CanUndo;
        var originalCanRedo = originalHistory.CanRedo;
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ad-ui-" + Guid.NewGuid().ToString("N"));
        var isolatedSession = new StudioSessionController();
        StudioDoorReactiveComplexOverlay? isolatedOverlay = null;
        try
        {
            var created = ProjectSession.Create(tempRoot, originalProject);
            if (!created.Succeeded ||
                !isolatedSession.Open(tempRoot, _session.ActiveAreaId?.ToString()).Succeeded)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX1AD_UI_TRANSACTION_FAILED stage=isolated-open created={created.Succeeded.ToString().ToLowerInvariant()}");
                return false;
            }

            isolatedOverlay = new StudioDoorReactiveComplexOverlay(isolatedSession);
            AddChild(isolatedOverlay);
            var isolatedEvidence = isolatedOverlay.RunBoundUiTransactionEvidence();
            var currentHistory = _session.History;
            UiTransactionSourceUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                           _session.IsDirty == originalDirty &&
                                           currentHistory is not null &&
                                           ReferenceEquals(originalHistory, currentHistory) &&
                                           currentHistory.State.Revision == originalRevision &&
                                           currentHistory.CanUndo == originalCanUndo &&
                                           currentHistory.CanRedo == originalCanRedo &&
                                           currentHistory.Journal.SequenceEqual(originalJournal);
            var valid = isolatedEvidence && UiTransactionSourceUntouched;
            if (!valid)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX1AD_UI_TRANSACTION_FAILED stage=isolated-transaction " +
                    $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                    $"source_untouched={UiTransactionSourceUntouched.ToString().ToLowerInvariant()}");
            }
            return valid;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AD_UI_TRANSACTION_FAILED stage=exception " +
                $"type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            if (isolatedOverlay is not null && GodotObject.IsInstanceValid(isolatedOverlay))
            {
                isolatedOverlay.Close();
                isolatedOverlay.GetParent()?.RemoveChild(isolatedOverlay);
                isolatedOverlay.Free();
            }
            isolatedSession.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private bool RunBoundUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        Open();
        var selectionRequired = Visible && originalProject is not null &&
                                _door.Selected == 0 && _draft is null &&
                                _preview.Disabled && _apply.Disabled &&
                                ReferenceEquals(originalProject, _session.Project);
        if (!selectionRequired || !SelectDoorExplicitlyForEvidence() || _draft is not { } selectedDraft ||
            originalProject is null)
        {
            Close();
            return false;
        }

        var blueprintId = selectedDraft.BlueprintId;
        var explicitSelection = ExplicitSelectionProven;
        SetEvidenceValues("transaction-ad");
        var customSelection = ExplicitCustomSelectionProven;
        Preview();
        var previewed = PreviewReady && ShowsHumanSurface;
        Apply();
        var appliedProfile = _session.Project?.Module.WorldFeatures.Doors.SingleOrDefault(profile =>
            profile.BlueprintId == blueprintId);
        var reactionsApplied = appliedProfile is { } reactive &&
                               reactive.Events.Any(binding =>
                                   binding.Event == AbilityTargetedEventName &&
                                   binding.ScriptId == "scripts/transaction-ad_ability_targeted" &&
                                   binding.Before && binding.Authoritative) &&
                               reactive.Events.Any(binding =>
                                   binding.Event == AbilityImpactedEventName &&
                                   binding.ScriptId == "scripts/transaction-ad_ability_impacted" &&
                                   !binding.Before && binding.Authoritative) &&
                               reactive.Events.Any(binding =>
                                   binding.Event == HeartbeatEventName &&
                                   binding.ScriptId == "scripts/transaction-ad_heartbeat" &&
                                   binding.Authoritative) &&
                               reactive.Events.Any(binding =>
                                   binding.Event == "custom:transaction-ad:signal" &&
                                   binding.ScriptId == "scripts/transaction-ad_custom" &&
                                   binding.Authoritative);
        var assemblyApplied = appliedProfile?.Assembly is
        {
            Kind: DoorWorldProfile.AssemblyKind.Airlock,
            Interlocked: true,
            Sequence: "extérieur puis verrou puis intérieur",
            FailureRecovery: "refermer le sas et libérer le verrou",
        } assembly && assembly.Panels.Count == 3;
        var historyReady = !_undo.Disabled;
        var applied = reactionsApplied && assemblyApplied && historyReady;
        Undo();
        var undone = ReferenceEquals(originalProject, _session.Project) &&
                     _session.IsDirty == originalDirty && !_redo.Disabled;
        Redo();
        var redone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var restored = ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        var valid = selectionRequired && explicitSelection && customSelection && previewed && applied &&
                    undone && redone && restored;
        if (!valid)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AD_UI_TRANSACTION_FAILED selection_required={selectionRequired.ToString().ToLowerInvariant()} " +
                $"explicit_selection={explicitSelection.ToString().ToLowerInvariant()} custom_selection={customSelection.ToString().ToLowerInvariant()} " +
                $"preview={previewed.ToString().ToLowerInvariant()} reactions={reactionsApplied.ToString().ToLowerInvariant()} " +
                $"assembly={assemblyApplied.ToString().ToLowerInvariant()} history={historyReady.ToString().ToLowerInvariant()} " +
                $"undone={undone.ToString().ToLowerInvariant()} redone={redone.ToString().ToLowerInvariant()} " +
                $"restored={restored.ToString().ToLowerInvariant()}");
        }
        return valid;
    }

    private void LoadSelectedDoor()
    {
        if (_updating || _choices.Count == 0)
        {
            return;
        }
        _session.CancelDoorReactiveComplexPreviewUx1AD();
        _approvedDraft = null;
        if (_door.Selected <= 0)
        {
            _draft = null;
            SetEditingEnabled(false);
            _door.Disabled = false;
            _preview.Disabled = true;
            _apply.Disabled = true;
            SetStatus("Choisissez une porte · aucun brouillon n’est ouvert automatiquement.", false);
            return;
        }

        var index = Math.Clamp(_door.Selected - 1, 0, _choices.Count - 1);
        var draft = _session.CreateDoorReactiveComplexDraftUx1AD(_choices[index].BlueprintId);
        _draft = draft;
        if (draft is null)
        {
            SetStatus("Cette porte reste indisponible tant que sa fiche est ambiguë.", true);
            SetEditingEnabled(false);
            _door.Disabled = false;
            _preview.Disabled = true;
            _apply.Disabled = true;
            return;
        }
        _customChoices = _session.DoorCustomEventChoicesUx1AD(draft.BlueprintId);
        _factionChoices = _session.DoorFactionChoicesUx1AD;
        LoadDraft();
        SetEditingEnabled(true);
        _preview.Disabled = false;
        SetStatus("Porte sélectionnée · prévisualisation obligatoire avant application.", false);
    }

    private bool SelectDoorExplicitlyForEvidence()
    {
        if (_choices.Count == 0 || _door.Selected != 0 || _draft is not null)
        {
            return false;
        }
        var configuredIndex = _choices.ToList().FindIndex(choice => choice.Configured);
        _door.Selected = (configuredIndex >= 0 ? configuredIndex : 0) + 1;
        LoadSelectedDoor();
        return ExplicitSelectionProven;
    }

    private void LoadDraft()
    {
        var draft = _draft!;
        _updating = true;
        _abilityTargetedEnabled.ButtonPressed = draft.AbilityTargetedEnabled;
        _abilityTargetedScript.Text = draft.AbilityTargetedScriptId;
        _abilityImpactedEnabled.ButtonPressed = draft.AbilityImpactedEnabled;
        _abilityImpactedScript.Text = draft.AbilityImpactedScriptId;
        _heartbeatEnabled.ButtonPressed = draft.HeartbeatEnabled;
        _heartbeatScript.Text = draft.HeartbeatScriptId;

        _customChoice.Clear();
        _customChoice.AddItem("Choisir ou créer…");
        _customChoice.AddItem("Créer un événement personnalisé");
        foreach (var choice in _customChoices)
        {
            _customChoice.AddItem(choice.Label);
        }
        _customChoice.Selected = 0;
        _customEnabled.ButtonPressed = draft.CustomEvent.Enabled;
        _customNamespace.Text = draft.CustomEvent.Namespace;
        _customName.Text = draft.CustomEvent.Name;
        _customScript.Text = draft.CustomEvent.ScriptId;
        _customBefore.ButtonPressed = draft.CustomEvent.Before;
        _customAuthority.ButtonPressed = draft.CustomEvent.Authoritative;

        _dialogue.Clear();
        _dialogue.AddItem("Aucune conversation");
        foreach (var choice in _dialogueChoices) _dialogue.AddItem(choice.Label);
        var dialogueIndex = draft.DialogueId is null
            ? -1
            : _dialogueChoices.ToList().FindIndex(choice => choice.Id == draft.DialogueId.Value);
        _dialogue.Selected = dialogueIndex + 1;

        _faction.Clear();
        _faction.AddItem("Aucune faction");
        if (!string.IsNullOrWhiteSpace(draft.FactionId) && _factionChoices.All(choice =>
                !string.Equals(choice.Slug, draft.FactionId, StringComparison.Ordinal)))
        {
            _factionChoices = _factionChoices.Append(new StudioDoorFactionChoice(
                draft.FactionId,
                "Faction existante non cataloguée")).ToArray();
        }
        foreach (var choice in _factionChoices) _faction.AddItem(choice.Label);
        var factionIndex = string.IsNullOrWhiteSpace(draft.FactionId)
            ? -1
            : _factionChoices.ToList().FindIndex(choice => choice.Slug == draft.FactionId);
        _faction.Selected = factionIndex + 1;

        _assemblyEnabled.ButtonPressed = draft.AssemblyEnabled;
        _assemblyKind.Selected = (int)draft.AssemblyKind;
        _sequence.Text = draft.Sequence;
        _interlocked.ButtonPressed = draft.Interlocked;
        _failureRecovery.Text = draft.FailureRecovery;
        _panelEditor.Load(draft.Panels);
        _updating = false;
        _operationApplied = false;
        _undo.Disabled = true;
        _redo.Disabled = true;
        UpdateFieldStates();
        InvalidatePreview();
    }

    private void LoadSelectedCustomEvent()
    {
        var draft = _draft;
        if (_updating || draft is null)
        {
            return;
        }
        _updating = true;
        if (_customChoice.Selected == 1)
        {
            _customEnabled.ButtonPressed = true;
            _customNamespace.Text = "door";
            _customName.Text = "signal";
            _customScript.Text = "scripts/door_custom";
            _customBefore.ButtonPressed = false;
            _customAuthority.ButtonPressed = true;
        }
        else if (_customChoice.Selected >= 2)
        {
            var index = Math.Clamp(_customChoice.Selected - 2, 0, _customChoices.Count - 1);
            var choice = _customChoices[index];
            _customEnabled.ButtonPressed = true;
            _customNamespace.Text = choice.Namespace;
            _customName.Text = choice.Name;
            _customScript.Text = choice.ScriptId;
            _customBefore.ButtonPressed = choice.Before;
            _customAuthority.ButtonPressed = choice.Authoritative;
        }
        else
        {
            _customEnabled.ButtonPressed = false;
            _customNamespace.Text = draft.CustomEvent.Namespace;
            _customName.Text = draft.CustomEvent.Name;
            _customScript.Text = draft.CustomEvent.ScriptId;
            _customBefore.ButtonPressed = draft.CustomEvent.Before;
            _customAuthority.ButtonPressed = draft.CustomEvent.Authoritative;
        }
        _updating = false;
        UpdateFieldStates();
        InvalidatePreview();
    }

    private StudioDoorReactiveComplexDraft ReadDraft()
    {
        var customIndex = _customChoice.Selected;
        var originalCustom = customIndex >= 2 && customIndex - 2 < _customChoices.Count
            ? _customChoices[customIndex - 2].EventName
            : null;
        return _draft! with
        {
            AbilityTargetedEnabled = _abilityTargetedEnabled.ButtonPressed,
            AbilityTargetedScriptId = _abilityTargetedScript.Text,
            AbilityImpactedEnabled = _abilityImpactedEnabled.ButtonPressed,
            AbilityImpactedScriptId = _abilityImpactedScript.Text,
            HeartbeatEnabled = _heartbeatEnabled.ButtonPressed,
            HeartbeatScriptId = _heartbeatScript.Text,
            CustomEvent = new(
                customIndex > 0,
                originalCustom,
                _customEnabled.ButtonPressed,
                _customNamespace.Text,
                _customName.Text,
                _customScript.Text,
                _customBefore.ButtonPressed,
                _customAuthority.ButtonPressed),
            DialogueId = _dialogue.Selected <= 0 ? null : _dialogueChoices[_dialogue.Selected - 1].Id,
            FactionId = _faction.Selected <= 0 ? null : _factionChoices[_faction.Selected - 1].Slug,
            AssemblyEnabled = _assemblyEnabled.ButtonPressed,
            AssemblyKind = (DoorWorldProfile.AssemblyKind)Math.Clamp(
                _assemblyKind.Selected,
                0,
                Enum.GetValues<DoorWorldProfile.AssemblyKind>().Length - 1),
            Panels = _panelEditor.Read(),
            Sequence = _sequence.Text,
            Interlocked = _interlocked.ButtonPressed,
            FailureRecovery = _failureRecovery.Text,
        };
    }

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Sélectionnez une porte avant de lancer l’aperçu.", true);
            return;
        }
        var candidate = ReadDraft();
        var result = _session.PreviewDoorReactiveComplexUx1AD(candidate);
        _previewSummary.Text = result.Succeeded
            ? result.Summary
            : "Aucun aperçu validé · corrigez les champs signalés puis recommencez.";
        _previewSummary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _approvedDraft = result.CanApply ? candidate : null;
        _apply.Disabled = _approvedDraft is null;
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Apply()
    {
        if (_approvedDraft is null)
        {
            SetStatus("Relancez l’aperçu avant d’appliquer.", true);
            return;
        }
        var result = _session.ApplyApprovedDoorReactiveComplexUx1AD();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _approvedDraft = null;
            _apply.Disabled = true;
            return;
        }
        _operationApplied = true;
        SetEditingEnabled(false);
        _preview.Disabled = true;
        _apply.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        DoorChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(
            result.Succeeded ? "Modification annulée · la porte précédente est restaurée." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            DoorChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(
            result.Succeeded ? "Modification rétablie · enregistrez pour écrire le module." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            DoorChanged?.Invoke();
        }
    }

    private void SetEvidenceValues(string stem)
    {
        _updating = true;
        _abilityTargetedEnabled.ButtonPressed = true;
        _abilityTargetedScript.Text = $"scripts/{stem}_ability_targeted";
        _abilityImpactedEnabled.ButtonPressed = true;
        _abilityImpactedScript.Text = $"scripts/{stem}_ability_impacted";
        _heartbeatEnabled.ButtonPressed = true;
        _heartbeatScript.Text = $"scripts/{stem}_heartbeat";
        _customChoice.Selected = 1;
        _customEnabled.ButtonPressed = true;
        _customNamespace.Text = stem;
        _customName.Text = "signal";
        _customScript.Text = $"scripts/{stem}_custom";
        _customBefore.ButtonPressed = false;
        _customAuthority.ButtonPressed = true;
        if (_dialogueChoices.Count > 0) _dialogue.Selected = 1;
        if (_factionChoices.Count > 0) _faction.Selected = 1;
        _assemblyEnabled.ButtonPressed = true;
        _assemblyKind.Selected = (int)DoorWorldProfile.AssemblyKind.Airlock;
        _sequence.Text = "extérieur puis verrou puis intérieur";
        _interlocked.ButtonPressed = true;
        _failureRecovery.Text = "refermer le sas et libérer le verrou";
        _panelEditor.SetEvidenceValues();
        _updating = false;
        UpdateFieldStates();
        InvalidatePreview();
    }

    private void ConnectInvalidation()
    {
        foreach (var check in new[]
                 {
                     _abilityTargetedEnabled,
                     _abilityImpactedEnabled,
                     _heartbeatEnabled,
                     _customEnabled,
                     _customBefore,
                     _customAuthority,
                     _assemblyEnabled,
                     _interlocked,
                 })
        {
            check.Toggled += _ =>
            {
                if (_updating) return;
                UpdateFieldStates();
                InvalidatePreview();
            };
        }
        foreach (var edit in new[]
                 {
                     _abilityTargetedScript,
                     _abilityImpactedScript,
                     _heartbeatScript,
                     _customNamespace,
                     _customName,
                     _customScript,
                     _sequence,
                     _failureRecovery,
                 })
        {
            edit.TextChanged += _ => InvalidatePreview();
        }
        foreach (var option in new[] { _dialogue, _faction, _assemblyKind })
        {
            option.ItemSelected += _ => InvalidatePreview();
        }
    }

    private void InvalidatePreview()
    {
        if (_updating || _operationApplied || _draft is null)
        {
            return;
        }
        _approvedDraft = null;
        _session.CancelDoorReactiveComplexPreviewUx1AD();
        _apply.Disabled = true;
        _preview.Disabled = false;
        _previewSummary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _door.Disabled = !enabled;
        _eventsTab.Disabled = !enabled;
        _referencesTab.Disabled = !enabled;
        _assemblyTab.Disabled = !enabled;
        UpdateFieldStates();
    }

    private void UpdateFieldStates()
    {
        foreach (var check in new[] { _abilityTargetedEnabled, _abilityImpactedEnabled, _heartbeatEnabled })
            check.Disabled = !_editingEnabled;
        _abilityTargetedScript.Editable = _editingEnabled && _abilityTargetedEnabled.ButtonPressed;
        _abilityImpactedScript.Editable = _editingEnabled && _abilityImpactedEnabled.ButtonPressed;
        _heartbeatScript.Editable = _editingEnabled && _heartbeatEnabled.ButtonPressed;

        _customChoice.Disabled = !_editingEnabled;
        var customSelected = _editingEnabled && _customChoice.Selected > 0;
        _customEnabled.Disabled = !customSelected;
        var customEnabled = customSelected && _customEnabled.ButtonPressed;
        foreach (var edit in new[] { _customNamespace, _customName, _customScript })
            edit.Editable = customEnabled;
        _customBefore.Disabled = !customEnabled;
        _customAuthority.Disabled = !customEnabled;

        _dialogue.Disabled = !_editingEnabled;
        _faction.Disabled = !_editingEnabled;
        _assemblyEnabled.Disabled = !_editingEnabled;
        var assemblyEnabled = _editingEnabled && _assemblyEnabled.ButtonPressed;
        _assemblyKind.Disabled = !assemblyEnabled;
        _sequence.Editable = assemblyEnabled;
        _interlocked.Disabled = !assemblyEnabled;
        _failureRecovery.Editable = assemblyEnabled;
        _panelEditor.SetEditingEnabled(assemblyEnabled);
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _eventsPanel.Visible = _section == 0;
        _referencesPanel.Visible = _section == 1;
        _assemblyPanel.Visible = _section == 2;
        ApplyTabStyle(_eventsTab, _section == 0);
        ApplyTabStyle(_referencesTab, _section == 1);
        ApplyTabStyle(_assemblyTab, _section == 2);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.72f : Size.X >= 2300 ? 1.24f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 940, 1210);
        var height = Math.Clamp((Size.Y - 72) / scale, 720, 900);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 9);
        return panel;
    }

    private static Button TabButton(string text, string tooltip)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.TooltipText = tooltip;
        return button;
    }

    private static void ApplyTabStyle(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(
                selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                selected ? StudioStyle.Success : StudioStyle.Border,
                6,
                selected ? 2 : 1));
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(250, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = Check(text);
        parent.AddChild(check);
        return check;
    }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        return check;
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        foreach (var label in labels) option.AddItem(label);
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static LineEdit Text(string placeholder)
    {
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private const string AbilityTargetedEventName = "ability-targeted";
    private const string AbilityImpactedEventName = "ability-impacted";
    private const string HeartbeatEventName = "heartbeat";
}

internal sealed class StudioDoorAssemblyPanelEditorUx1AD : VBoxContainer
{
    private readonly IReadOnlyList<PanelRow> _rows;
    private bool _updating;

    internal event Action? Changed;

    internal StudioDoorAssemblyPanelEditorUx1AD()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 5);
        AddChild(StudioStyle.SectionLabel("PANNEAUX · 1 À 8"));
        var rows = new List<PanelRow>();
        for (var index = 0; index < 8; index++)
        {
            var row = new PanelRow(index);
            rows.Add(row);
            AddChild(row.Root);
            row.Active.Toggled += _ => OnChanged();
            row.Role.TextChanged += _ => OnChanged();
            row.Offset.ValueChanged += _ => OnChanged();
            row.Delay.ValueChanged += _ => OnChanged();
            row.Reversed.Toggled += _ => OnChanged();
        }
        _rows = rows;
    }

    internal void Load(IReadOnlyList<StudioDoorAssemblyPanelDraft> panels)
    {
        _updating = true;
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            var panel = index < panels.Count ? panels[index] : null;
            row.Id = panel?.Id ?? Guid.NewGuid();
            row.Active.ButtonPressed = panel is not null;
            row.Role.Text = panel?.Role ?? $"panneau {index + 1}";
            row.Offset.Value = panel?.OpenOffset ?? 0;
            row.Delay.Value = panel?.DelaySeconds ?? 0;
            row.Reversed.ButtonPressed = panel?.Reversed ?? false;
        }
        _updating = false;
        UpdateStates();
    }

    internal IReadOnlyList<StudioDoorAssemblyPanelDraft> Read() => _rows
        .Where(row => row.Active.ButtonPressed)
        .Select(row => new StudioDoorAssemblyPanelDraft(
            row.Id,
            row.Role.Text,
            row.Offset.Value,
            row.Delay.Value,
            row.Reversed.ButtonPressed))
        .ToArray();

    internal void SetEvidenceValues()
    {
        _updating = true;
        var roles = new[] { "vantail extérieur", "vantail intérieur", "verrou central" };
        var offsets = new[] { -1.4, 1.4, 0d };
        var delays = new[] { 0d, .2, .4 };
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            row.Active.ButtonPressed = index < 3;
            if (index < 3)
            {
                row.Role.Text = roles[index];
                row.Offset.Value = offsets[index];
                row.Delay.Value = delays[index];
                row.Reversed.ButtonPressed = index == 1;
            }
        }
        _updating = false;
        UpdateStates();
        Changed?.Invoke();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        foreach (var row in _rows)
        {
            row.Active.Disabled = !enabled;
            var active = enabled && row.Active.ButtonPressed;
            row.Role.Editable = active;
            row.Offset.Editable = active;
            row.Delay.Editable = active;
            row.Reversed.Disabled = !active;
        }
    }

    private void OnChanged()
    {
        if (_updating) return;
        UpdateStates();
        Changed?.Invoke();
    }

    private void UpdateStates()
    {
        foreach (var row in _rows)
        {
            var active = !row.Active.Disabled && row.Active.ButtonPressed;
            row.Role.Editable = active;
            row.Offset.Editable = active;
            row.Delay.Editable = active;
            row.Reversed.Disabled = !active;
        }
    }

    private sealed class PanelRow
    {
        internal HBoxContainer Root { get; }
        internal CheckBox Active { get; }
        internal LineEdit Role { get; }
        internal SpinBox Offset { get; }
        internal SpinBox Delay { get; }
        internal CheckBox Reversed { get; }
        internal Guid Id { get; set; }

        internal PanelRow(int index)
        {
            Id = Guid.NewGuid();
            Root = new HBoxContainer();
            Root.AddThemeConstantOverride("separation", 6);
            Active = new CheckBox
            {
                Text = $"{index + 1}",
                TooltipText = "Inclure ce panneau dans l’assemblage.",
                CustomMinimumSize = new Vector2(44, 0),
            };
            Root.AddChild(Active);
            Role = new LineEdit
            {
                PlaceholderText = "Rôle du panneau",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            StudioStyle.ApplyLineEdit(Role);
            Root.AddChild(Role);
            Offset = Number(-100, 100, .05, " m");
            Offset.CustomMinimumSize = new Vector2(130, 0);
            Offset.TooltipText = "Décalage d’ouverture.";
            Root.AddChild(Offset);
            Delay = Number(0, 300, .05, " s");
            Delay.CustomMinimumSize = new Vector2(130, 0);
            Delay.TooltipText = "Délai avant mouvement.";
            Root.AddChild(Delay);
            Reversed = new CheckBox { Text = "Inversé" };
            Root.AddChild(Reversed);
        }

        private static SpinBox Number(double minimum, double maximum, double step, string suffix)
        {
            var spin = new SpinBox
            {
                MinValue = minimum,
                MaxValue = maximum,
                Step = step,
                Suffix = suffix,
            };
            StudioStyle.ApplySpinBox(spin);
            return spin;
        }
    }
}
