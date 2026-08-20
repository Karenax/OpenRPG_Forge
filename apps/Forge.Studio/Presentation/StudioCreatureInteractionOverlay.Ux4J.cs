using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureInteractionOverlayUx4J : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureInteractionEvidenceSourceSnapshotUx4J? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureInteractionChoiceUx4J> _choices = [];
    private IReadOnlyList<StudioCreatureInteractionFactionChoiceUx4J> _factionChoices = [];
    private IReadOnlyList<StudioCreatureInteractionDialogueChoiceUx4J> _dialogueChoices = [];
    private StudioCreatureInteractionDraftUx4J? _draft;
    private StudioCreatureInteractionPreviewUx4J? _previewResult;
    private readonly List<CreatureInteractionFactionMembership> _memberships = [];
    private readonly List<CreatureInteractionSenseProfile> _senses = [];
    private readonly List<CreatureInteractionDialogueBinding> _bindings = [];
    private Guid _primaryFactionId;

    private readonly OptionButton _creature;
    private readonly Label _context;
    private readonly CheckBox _required;
    private readonly TabContainer _tabs;
    private readonly ItemList _membershipList;
    private readonly OptionButton _faction;
    private readonly LineEdit _rank;
    private readonly LineEdit _weight;
    private readonly LineEdit _reputation;
    private readonly CheckBox _inherited;
    private readonly Label _primaryFaction;
    private readonly ItemList _senseList;
    private readonly OptionButton _senseKind;
    private readonly LineEdit _senseRange;
    private readonly LineEdit _senseCone;
    private readonly LineEdit _stimuli;
    private readonly CheckBox _occlusion;
    private readonly LineEdit _memory;
    private readonly ItemList _bindingList;
    private readonly LineEdit _onConversationHandler;
    private readonly OptionButton _dialogue;
    private readonly LineEdit _dialogueContext;
    private readonly LineEdit _priority;
    private readonly LineEdit _interlocutor;
    private readonly LineEdit _requiredState;
    private readonly CheckBox _fallback;
    private readonly CheckBox _invulnerable;
    private readonly CheckBox _narrativeDeath;
    private readonly CheckBox _dialogueInterruptible;
    private readonly LineEdit _permissions;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? CreatureChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsFactions => _memberships.Count > 0 && _summary.Text.Contains("Factions", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsPerception => _senses.Count > 0 && _summary.Text.Contains("Perception", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsDialogue => _bindings.Count > 0 && _summary.Text.Contains("Dialogues", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsOnConversation =>
        _onConversationHandler.Text == "OnConversationWitness" &&
        _summary.Text.Contains("OnConversation=OnConversationWitness", StringComparison.Ordinal);
    internal bool ShowsProtection => _summary.Text.Contains("Protection", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Faction de créature", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Perception", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Conversation associée", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("OnConversation", StringComparison.Ordinal) &&
                   text.Contains("Plot, immortel et interruptibilité", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealProfileEvidence => _usesRealProfile;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreatureInteractionOverlayUx4J(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioCreatureInteractionOverlayUx4J";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; Visible = false;
        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureInteractionDialogUx4J", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 58; dialog.OffsetTop = 30; dialog.OffsetRight = -58; dialog.OffsetBottom = -30;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7); dialog.AddChild(root);
        var header = new HBoxContainer(); var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("FACTION, PERCEPTION, DIALOGUE ET PROTECTION · UX4-J", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Composition d’interaction typée · factions et dialogues humains, sans UUID ni JSON affiché.",
            10, StudioStyle.Muted, wrap: true)); header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; header.AddChild(close);
        root.AddChild(header);
        var contextRow = new HBoxContainer(); contextRow.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(390, 30) }; contextRow.AddChild(_creature);
        _required = new CheckBox { Text = "Requis au runtime" }; contextRow.AddChild(_required);
        _context = StudioStyle.Label("Sélection explicite requise · fondation de créature obligatoire.",
            9, StudioStyle.Muted, wrap: true); _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contextRow.AddChild(_context); root.AddChild(contextRow);
        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 410),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true
        }; root.AddChild(tabHost);
        _tabs = new TabContainer(); _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); tabHost.AddChild(_tabs);

        var factions = Tab(_tabs, "1 · Factions");
        factions.AddChild(StudioStyle.Label("Faction de créature", 11, StudioStyle.Accent));
        factions.AddChild(StudioStyle.Label(
            "Appartenances pondérées à des factions actives du module ; principale, rang, réputation et héritage.",
            9, StudioStyle.Muted, wrap: true));
        _primaryFaction = StudioStyle.Label("Faction principale : aucune", 10, StudioStyle.Text, wrap: true);
        factions.AddChild(_primaryFaction); _membershipList = List(factions, 112);
        var factionGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _faction = Choice(factionGrid, "Faction", []); _rank = Field(factionGrid, "Rang", "rank.member");
        _weight = Field(factionGrid, "Poids 0..1", "1"); _reputation = Field(factionGrid, "Réputation -100..100", "0");
        factions.AddChild(factionGrid); _inherited = new CheckBox { Text = "Appartenance héritée" };
        factions.AddChild(_inherited);
        var factionButtons = new HBoxContainer();
        var newMembership = StudioStyle.Button("Nouvelle appartenance", compact: true);
        var updateMembership = StudioStyle.Button("Mettre à jour", compact: true);
        var removeMembership = StudioStyle.Button("Retirer", compact: true);
        var makePrimary = StudioStyle.Button("Définir principale", compact: true);
        newMembership.Pressed += ClearMembership; updateMembership.Pressed += () => SaveMembership();
        removeMembership.Pressed += RemoveMembership; makePrimary.Pressed += SetPrimaryFaction;
        foreach (var button in new[] { newMembership, updateMembership, removeMembership, makePrimary }) factionButtons.AddChild(button);
        factions.AddChild(factionButtons);

        var perception = Tab(_tabs, "2 · Perception");
        perception.AddChild(StudioStyle.Label("Perception", 11, StudioStyle.Accent));
        perception.AddChild(StudioStyle.Label(
            "Vision, audition, odorat et magie : portée, cône, occlusion, stimuli et mémoire.",
            9, StudioStyle.Muted, wrap: true)); _senseList = List(perception, 112);
        var senseGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _senseKind = Choice(senseGrid, "Sens", ["Vision", "Audition", "Odorat", "Magie"]);
        _senseRange = Field(senseGrid, "Portée", "20"); _senseCone = Field(senseGrid, "Cône (degrés)", "120");
        _stimuli = Field(senseGrid, "Stimuli (séparés par virgule)", "stimulus.visual"); perception.AddChild(senseGrid);
        var senseFlags = new HBoxContainer(); _occlusion = new CheckBox { Text = "Occlusion active" };
        senseFlags.AddChild(_occlusion); _memory = Field(senseFlags, "Mémoire (s)", "10"); perception.AddChild(senseFlags);
        perception.AddChild(CollectionButtons("Nouveau sens", () => SaveSense(), RemoveSense, ClearSense));

        var dialogue = Tab(_tabs, "3 · Dialogues");
        dialogue.AddChild(StudioStyle.Label("Conversation associée", 11, StudioStyle.Accent));
        dialogue.AddChild(StudioStyle.Label(
            "Liaisons vers des dialogues réels : contexte, priorité, interlocuteur, état et fallback.",
            9, StudioStyle.Muted, wrap: true));
        _onConversationHandler = Field(dialogue, "Handler OnConversation serveur", "OnConversation");
        dialogue.AddChild(StudioStyle.Label(
            "Le handler ForgeScript est validé et persisté ; l’aperçu ne l’exécute pas.",
            9, StudioStyle.Muted, wrap: true));
        _bindingList = List(dialogue, 112);
        var dialogueGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _dialogue = Choice(dialogueGrid, "Dialogue", []); _dialogueContext = Field(dialogueGrid, "Contexte", "greeting");
        _priority = Field(dialogueGrid, "Priorité", "1"); _interlocutor = Field(dialogueGrid, "Interlocuteur optionnel", "player.hero");
        _requiredState = Field(dialogueGrid, "État requis optionnel", "state.hostile"); dialogueGrid.AddChild(new Control());
        dialogue.AddChild(dialogueGrid); _fallback = new CheckBox { Text = "Fallback du contexte" }; dialogue.AddChild(_fallback);
        dialogue.AddChild(CollectionButtons("Nouvelle liaison", () => SaveBinding(), RemoveBinding, ClearBinding));

        var protection = Tab(_tabs, "4 · Protection");
        protection.AddChild(StudioStyle.Label("Plot, immortel et interruptibilité", 11, StudioStyle.Accent));
        protection.AddChild(StudioStyle.Label(
            "Protection narrative explicite, interruption de dialogue et permissions maître de jeu.",
            9, StudioStyle.Muted, wrap: true));
        _invulnerable = new CheckBox { Text = "Invulnérable" };
        _narrativeDeath = new CheckBox { Text = "Mort narrative" };
        _dialogueInterruptible = new CheckBox { Text = "Dialogue interruptible" };
        protection.AddChild(_invulnerable); protection.AddChild(_narrativeDeath); protection.AddChild(_dialogueInterruptible);
        _permissions = Field(protection, "Permissions MJ (séparées par virgule)", "creature.plot.override");

        var review = Tab(_tabs, "5 · Aperçu");
        review.AddChild(StudioStyle.Label("Aperçu canonique", 11, StudioStyle.Accent));
        review.AddChild(StudioStyle.Label(
            "Projection Domain/RPG Kit uniquement : aucun acteur, dialogue, sens, script ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true)); _diagnostics = new VBoxContainer(); review.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 142),
            FitContent = false,
            SelectionEnabled = true
        }; root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit une commande annulable ; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer(); _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); RenderEmpty("Choisissez explicitement une créature."); SetEditingEnabled(false);
    }

    internal void Open()
    { if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence(); _session = _sourceSession; OpenBoundSession(); }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBoundSession(); }
    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null && _evidenceRoot is null) return;
        _session.CancelCreatureInteractionUx4J(); _previewResult = null; _draft = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBoundSession()
    {
        _session.CancelCreatureInteractionUx4J(); _previewResult = null; _draft = null; _applied = false;
        _choices = _session.CreatureInteractionChoicesUx4J; _factionChoices = _session.CreatureInteractionFactionChoicesUx4J;
        _dialogueChoices = _session.CreatureInteractionDialogueChoicesUx4J; _updating = true; _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…"); foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0); _updating = false; ClearControls(); Visible = true; SetEditingEnabled(false);
        _context.Text = _choices.Count == 0 ? "Aucune créature éditable dans ce module." :
            "Sélection explicite requise · fondation de créature obligatoire.";
        RenderEmpty(_choices.Count == 0 ? "État vide réel : aucune créature éditable." :
            "Choisissez une créature pour ouvrir ses factions, sa perception, ses dialogues et sa protection.");
    }
    private void SelectCreature(long selected)
    {
        if (_updating) return; _session.CancelCreatureInteractionUx4J(); _previewResult = null; _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        { _draft = null; ClearControls(); SetEditingEnabled(false); RenderEmpty("Sélection explicite requise."); return; }
        var choice = _choices[(int)selected - 1]; _draft = _session.CreateCreatureInteractionDraftUx4J(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls(); SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly ? "Créature installée en lecture seule : créez une variante projet." :
                _factionChoices.Count == 0 ? "Aucune faction active : la composition ne peut pas être créée." :
                "Cette créature ne possède pas de fondation éditable."); return;
        }
        Populate(_draft); SetEditingEnabled(true);
        _context.Text = $"{choice.Label} · {_factionChoices.Count} faction(s) · {_dialogueChoices.Count} dialogue(s).";
        RenderEmpty("Modifiez le brouillon puis prévisualisez la commande canonique.");
    }

    private void Populate(StudioCreatureInteractionDraftUx4J draft)
    {
        _updating = true; _required.ButtonPressed = draft.RequiredAtRuntime; _primaryFactionId = draft.Factions.PrimaryFactionId;
        _memberships.Clear(); _memberships.AddRange(draft.Factions.Memberships); _senses.Clear();
        _senses.AddRange(draft.Perception.Senses); _bindings.Clear(); _bindings.AddRange(draft.DialogueBindings);
        _onConversationHandler.Text = draft.OnConversationHandler;
        _memory.Text = Format(draft.Perception.MemorySeconds); _invulnerable.ButtonPressed = draft.Protection.Invulnerable;
        _narrativeDeath.ButtonPressed = draft.Protection.UsesNarrativeDeath;
        _dialogueInterruptible.ButtonPressed = draft.Protection.DialogueInterruptible;
        _permissions.Text = string.Join(", ", draft.Protection.GameMasterPermissions);
        PopulateFactionChoices(); PopulateDialogueChoices(); _updating = false;
        RebuildMembershipList(0); RebuildSenseList(0); RebuildBindingList(0);
    }
    private void PopulateFactionChoices()
    { _faction.Clear(); foreach (var choice in _factionChoices) _faction.AddItem(choice.Label); if (_factionChoices.Count > 0) _faction.Select(0); }
    private void PopulateDialogueChoices()
    { _dialogue.Clear(); foreach (var choice in _dialogueChoices) _dialogue.AddItem(choice.Label); if (_dialogueChoices.Count > 0) _dialogue.Select(0); }

    private void RebuildMembershipList(int selected)
    {
        _updating = true; _membershipList.Clear();
        foreach (var item in _memberships) _membershipList.AddItem(
            $"{HumanFaction(item.FactionId)} · poids {Format(item.Weight)} · {item.Rank} · réputation {Format(item.PersonalReputation)}" +
            $"{(item.Inherited ? " · héritée" : string.Empty)}{(item.FactionId == _primaryFactionId ? " · PRINCIPALE" : string.Empty)}");
        _primaryFaction.Text = $"Faction principale : {HumanFaction(_primaryFactionId)}";
        if (_memberships.Count > 0) { selected = Math.Clamp(selected, 0, _memberships.Count - 1); _membershipList.Select(selected); SelectMembership(selected); }
        else ClearMembership(); _updating = false;
    }
    private void SelectMembership(long selected)
    {
        if (selected < 0 || selected >= _memberships.Count) return; var item = _memberships[(int)selected]; _updating = true;
        var factionIndex = _factionChoices.ToList().FindIndex(choice => choice.FactionId == item.FactionId);
        if (factionIndex >= 0) _faction.Select(factionIndex); _rank.Text = item.Rank; _weight.Text = Format(item.Weight);
        _reputation.Text = Format(item.PersonalReputation); _inherited.ButtonPressed = item.Inherited; _updating = false;
    }
    private void ClearMembership()
    {
        _updating = true; _membershipList.DeselectAll(); if (_factionChoices.Count > 0) _faction.Select(0);
        _rank.Text = "rank.member"; _weight.Text = "1"; _reputation.Text = "0";
        _inherited.ButtonPressed = false; _updating = false;
    }
    private bool SaveMembership(bool invalidate = true)
    {
        if (_draft is null || _faction.Selected < 0 || _faction.Selected >= _factionChoices.Count ||
            !TryDouble(_weight.Text, out var weight) || !TryDouble(_reputation.Text, out var reputation)) return false;
        var selected = Selected(_membershipList); var factionId = _factionChoices[_faction.Selected].FactionId;
        if (selected < 0 && _memberships.Any(item => item.FactionId == factionId)) return false;
        var id = selected >= 0 && selected < _memberships.Count ? _memberships[selected].Id : Guid.NewGuid();
        var item = new CreatureInteractionFactionMembership(id, factionId, weight, _rank.Text, reputation,
            _inherited.ButtonPressed);
        if (selected >= 0 && selected < _memberships.Count) _memberships[selected] = item;
        else { _memberships.Add(item); selected = _memberships.Count - 1; if (_primaryFactionId == Guid.Empty) _primaryFactionId = factionId; }
        RebuildMembershipList(selected); if (invalidate) InvalidatePreview(); return true;
    }
    private void RemoveMembership()
    {
        var selected = Selected(_membershipList); if (selected < 0 || selected >= _memberships.Count) return;
        var removed = _memberships[selected].FactionId; _memberships.RemoveAt(selected);
        if (_primaryFactionId == removed) _primaryFactionId = _memberships.FirstOrDefault()?.FactionId ?? Guid.Empty;
        RebuildMembershipList(Math.Min(selected, _memberships.Count - 1)); InvalidatePreview();
    }
    private void SetPrimaryFaction()
    {
        var selected = Selected(_membershipList); if (selected < 0 || selected >= _memberships.Count) return;
        _primaryFactionId = _memberships[selected].FactionId; RebuildMembershipList(selected); InvalidatePreview();
    }

    private void RebuildSenseList(int selected)
    {
        _updating = true; _senseList.Clear();
        foreach (var item in _senses) _senseList.AddItem(
            $"{SenseLabel(item.Kind)} · portée {Format(item.Range)} · cône {Format(item.ConeDegrees)}° · " +
            $"{(item.UsesOcclusion ? "avec occlusion" : "sans occlusion")}");
        if (_senses.Count > 0) { selected = Math.Clamp(selected, 0, _senses.Count - 1); _senseList.Select(selected); SelectSense(selected); }
        else ClearSense(); _updating = false;
    }
    private void SelectSense(long selected)
    {
        if (selected < 0 || selected >= _senses.Count) return; var item = _senses[(int)selected]; _updating = true;
        _senseKind.Select((int)item.Kind); _senseRange.Text = Format(item.Range); _senseCone.Text = Format(item.ConeDegrees);
        _stimuli.Text = string.Join(", ", item.StimulusTags); _occlusion.ButtonPressed = item.UsesOcclusion; _updating = false;
    }
    private void ClearSense()
    {
        _updating = true; _senseList.DeselectAll(); _senseKind.Select(0); _senseRange.Text = "20";
        _senseCone.Text = "120"; _stimuli.Text = "stimulus.visual"; _occlusion.ButtonPressed = true; _updating = false;
    }
    private bool SaveSense(bool invalidate = true)
    {
        if (_draft is null || !TryDouble(_senseRange.Text, out var range) || !TryDouble(_senseCone.Text, out var cone)) return false;
        var selected = Selected(_senseList); var kind = (CreatureInteractionSenseKind)_senseKind.Selected;
        if (selected < 0 && _senses.Any(item => item.Kind == kind)) return false;
        var id = selected >= 0 && selected < _senses.Count ? _senses[selected].Id : Guid.NewGuid();
        var tags = SplitIdentifiers(_stimuli.Text);
        var item = new CreatureInteractionSenseProfile(id, kind, range, cone, _occlusion.ButtonPressed, tags);
        if (selected >= 0 && selected < _senses.Count) _senses[selected] = item;
        else { _senses.Add(item); selected = _senses.Count - 1; }
        RebuildSenseList(selected); if (invalidate) InvalidatePreview(); return true;
    }
    private void RemoveSense()
    { var selected = Selected(_senseList); if (selected < 0 || selected >= _senses.Count) return; _senses.RemoveAt(selected); RebuildSenseList(Math.Min(selected, _senses.Count - 1)); InvalidatePreview(); }

    private void RebuildBindingList(int selected)
    {
        _updating = true; _bindingList.Clear();
        foreach (var item in _bindings) _bindingList.AddItem(
            $"{HumanDialogue(item.DialogueId)} · {item.Context} · priorité {item.Priority}" +
            $"{(item.Fallback ? " · fallback" : string.Empty)}");
        if (_bindings.Count > 0) { selected = Math.Clamp(selected, 0, _bindings.Count - 1); _bindingList.Select(selected); SelectBinding(selected); }
        else ClearBinding(); _updating = false;
    }
    private void SelectBinding(long selected)
    {
        if (selected < 0 || selected >= _bindings.Count) return; var item = _bindings[(int)selected]; _updating = true;
        var dialogueIndex = _dialogueChoices.ToList().FindIndex(choice => choice.DialogueId == item.DialogueId);
        if (dialogueIndex >= 0) _dialogue.Select(dialogueIndex); _dialogueContext.Text = item.Context;
        _priority.Text = item.Priority.ToString(CultureInfo.InvariantCulture); _interlocutor.Text = item.InterlocutorId ?? string.Empty;
        _requiredState.Text = item.RequiredState ?? string.Empty; _fallback.ButtonPressed = item.Fallback; _updating = false;
    }
    private void ClearBinding()
    {
        _updating = true; _bindingList.DeselectAll(); if (_dialogueChoices.Count > 0) _dialogue.Select(0);
        _dialogueContext.Text = "greeting"; _priority.Text = "1"; _interlocutor.Text = string.Empty;
        _requiredState.Text = string.Empty; _fallback.ButtonPressed = false; _updating = false;
    }
    private bool SaveBinding(bool invalidate = true)
    {
        if (_draft is null || _dialogue.Selected < 0 || _dialogue.Selected >= _dialogueChoices.Count ||
            !TryInt(_priority.Text, out var priority)) return false;
        var selected = Selected(_bindingList); var id = selected >= 0 && selected < _bindings.Count
            ? _bindings[selected].Id : Guid.NewGuid();
        var item = new CreatureInteractionDialogueBinding(id, _dialogueContext.Text, priority,
            _dialogueChoices[_dialogue.Selected].DialogueId, EmptyToNull(_interlocutor.Text),
            EmptyToNull(_requiredState.Text), _fallback.ButtonPressed);
        if (selected >= 0 && selected < _bindings.Count) _bindings[selected] = item;
        else { _bindings.Add(item); selected = _bindings.Count - 1; }
        RebuildBindingList(selected); if (invalidate) InvalidatePreview(); return true;
    }
    private void RemoveBinding()
    { var selected = Selected(_bindingList); if (selected < 0 || selected >= _bindings.Count) return; _bindings.RemoveAt(selected); RebuildBindingList(Math.Min(selected, _bindings.Count - 1)); InvalidatePreview(); }

    private StudioCreatureInteractionDraftUx4J? ReadDraft()
    {
        if (_draft is null || !TryDouble(_memory.Text, out var memory)) return null;
        var selectedMembership = Selected(_membershipList); if (selectedMembership >= 0 && !SaveMembership(false)) return null;
        var selectedSense = Selected(_senseList); if (selectedSense >= 0 && !SaveSense(false)) return null;
        var selectedBinding = Selected(_bindingList); if (selectedBinding >= 0 && !SaveBinding(false)) return null;
        return _draft with
        {
            RequiredAtRuntime = _required.ButtonPressed,
            Factions = new(_draft.Factions.Id, _primaryFactionId, _memberships.ToArray()),
            Perception = new(_draft.Perception.Id, _senses.ToArray(), memory),
            DialogueBindings = _bindings.ToArray(),
            OnConversationHandler = _onConversationHandler.Text.Trim(),
            Protection = new(_draft.Protection.Id, _invulnerable.ButtonPressed, _narrativeDeath.ButtonPressed,
                _dialogueInterruptible.ButtonPressed, SplitIdentifiers(_permissions.Text)),
        };
    }
    private void Preview()
    {
        var draft = ReadDraft(); if (draft is null) { SetStatus("Valeurs ou références incomplètes.", true); return; }
        _draft = draft; _previewResult = _session.PreviewCreatureInteractionUx4J(draft);
        _previewDryRun = _previewResult.Succeeded; RenderPreview(_previewResult, _previewDryRun);
        _apply.Disabled = !_previewResult.CanApply; SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }
    private void Apply()
    {
        var draft = ReadDraft(); if (draft is null) { SetStatus("Brouillon incomplet.", true); return; }
        var result = _session.ApplyApprovedCreatureInteractionUx4J(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        CreatureChanged?.Invoke(); _draft = _session.CreateCreatureInteractionDraftUx4J(draft.BlueprintId);
        if (_draft is not null) Populate(_draft); _previewResult = null; _apply.Disabled = true;
    }
    private void Undo() { var result = _session.UndoCreatureInteractionUx4J(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Redo() { var result = _session.RedoCreatureInteractionUx4J(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void Reload()
    { if (_draft is null) return; _draft = _session.CreateCreatureInteractionDraftUx4J(_draft.BlueprintId); if (_draft is not null) Populate(_draft); _previewResult = null; _apply.Disabled = true; CreatureChanged?.Invoke(); }
    private void RenderPreview(StudioCreatureInteractionPreviewUx4J result, bool dryRun)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}", 9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "APERÇU REFUSÉ")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"{result.FactionSummary}\n{result.PerceptionSummary}\n{result.DialogueSummary}\n" +
                        $"{result.ProtectionSummary}\n{result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
    }
    private void RenderEmpty(string message)
    { foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _summary.Text = message; }
    private void InvalidatePreview()
    { if (_updating) return; _session.CancelCreatureInteractionUx4J(); _previewResult = null; _apply.Disabled = true; }
    private void SetEditingEnabled(bool enabled)
    { _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady; _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null; }
    private void ClearControls()
    {
        _updating = true; _memberships.Clear(); _senses.Clear(); _bindings.Clear(); _primaryFactionId = Guid.Empty;
        _membershipList.Clear(); _senseList.Clear(); _bindingList.Clear(); _faction.Clear(); _dialogue.Clear();
        _onConversationHandler.Text = string.Empty;
        _primaryFaction.Text = "Faction principale : aucune"; _updating = false;
    }
    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature; _membershipList.ItemSelected += SelectMembership;
        _senseList.ItemSelected += SelectSense; _bindingList.ItemSelected += SelectBinding;
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
        _required.Toggled += _ => InvalidatePreview(); _invulnerable.Toggled += _ => InvalidatePreview();
        _narrativeDeath.Toggled += _ => InvalidatePreview(); _dialogueInterruptible.Toggled += _ => InvalidatePreview();
        _onConversationHandler.TextChanged += _ => InvalidatePreview();
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) { _lastFailure = "release_previous"; return false; }
        if (!TryOpenEvidence()) { _lastFailure = _lastFailure == "none" ? "open" : _lastFailure; return false; }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0) { _lastFailure = "fixture_creature_missing"; Close(); return false; }
        _creature.Select(index + 1); SelectCreature(index + 1);
        if (_draft is null || _memberships.Count == 0 || _senses.Count == 0 || _bindings.Count == 0)
        { _lastFailure = "draft_collections"; Close(); return false; }
        _membershipList.Select(0); SelectMembership(0); _weight.Text = ".8"; _reputation.Text = "15";
        _inherited.ButtonPressed = true; if (!SaveMembership(false)) { _lastFailure = "membership"; Close(); return false; }
        _senseList.Select(0); SelectSense(0); _senseRange.Text = Format(_senses[0].Range + 1);
        if (!SaveSense(false)) { _lastFailure = "sense"; Close(); return false; }
        _memory.Text = "18"; _bindingList.Select(0); SelectBinding(0);
        _priority.Text = Math.Min(1000, _bindings[0].Priority + 1).ToString(CultureInfo.InvariantCulture);
        if (!SaveBinding(false)) { _lastFailure = "binding"; Close(); return false; }
        _onConversationHandler.Text = "OnConversationWitness";
        _permissions.Text = string.Join(", ", SplitIdentifiers(_permissions.Text).Append("creature.preview.inspect")
            .Distinct(StringComparer.Ordinal)); _required.ButtonPressed = true;
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · copie isolée, source strictement inchangée.\n{_summary.Text}";
        _tabs.CurrentTab = mode.Contains("perception", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("dialog", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("conversation", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("protect", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    ShowsFactions && ShowsPerception && ShowsDialogue && ShowsOnConversation && ShowsProtection && UsesIsolatedEvidence &&
                    EvidenceSourceUntouched && (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:factions={ShowsFactions}:perception={ShowsPerception}:dialogue={ShowsDialogue}:on_conversation={ShowsOnConversation}:protection={ShowsProtection}:isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4J_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-J isolée prête · aucune donnée source modifiée.", false); return true;
    }
    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-j-creature-interaction") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; var history = transaction.History;
            if (original is null || history is null) return false; var originalJson = ProjectJsonSerializer.Serialize(original); Apply();
            var appliedProject = transaction.Project; var applied = _applied && appliedProject is not null &&
                ProjectJsonSerializer.Serialize(appliedProject) != originalJson; var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo(); var undone = applied && transaction.Project is not null && ProjectJsonSerializer.Serialize(transaction.Project) == originalJson;
            if (undone) Redo(); var redoneProject = transaction.Project;
            var redone = undone && redoneProject is not null && ProjectJsonSerializer.Serialize(redoneProject) == appliedJson;
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            { reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded && ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson; }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched && EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4J_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        { reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close(); _sourceSession.RestoreCreatureInteractionApprovalUx4J(snapshot.Approval); }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4JEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4j-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root; _evidenceBlueprintId = evidence.BlueprintId;
            _session = session; _usesRealProfile = evidence.UsesRealProfile; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false; OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealProfile ^ _usesFallback);
        }
        catch (Exception exception)
        { _lastFailure = "open_exception:" + exception.GetType().Name; session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false; }
    }
    private StudioCreatureInteractionEvidenceSourceSnapshotUx4J? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureInteractionApprovalUx4J());
    }
    private bool SourceMatches(StudioCreatureInteractionEvidenceSourceSnapshotUx4J snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId && _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureInteractionApprovalUx4J() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceBlueprintId = null; _session = _sourceSession; _usesRealProfile = false;
        _usesFallback = false; _previewDryRun = false; var released = true; try { session?.Close(); } catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root); if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased; _sourceRestored = true; return _resourcesReleased;
    }
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4j-evidence-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true); return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private string HumanFaction(Guid id) => _factionChoices.FirstOrDefault(item => item.FactionId == id)?.Label ?? "Faction héritée protégée";
    private string HumanDialogue(DialogueId id) => _dialogueChoices.FirstOrDefault(item => item.DialogueId == id)?.Label ?? "Dialogue hérité protégé";
    private static string SenseLabel(CreatureInteractionSenseKind kind) => kind switch
    { CreatureInteractionSenseKind.Vision => "Vision", CreatureInteractionSenseKind.Hearing => "Audition", CreatureInteractionSenseKind.Smell => "Odorat", CreatureInteractionSenseKind.Magic => "Magie", _ => "Sens hérité protégé" };
    private static IReadOnlyList<string> SplitIdentifiers(string value) => value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static VBoxContainer Tab(TabContainer tabs, string title) { var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true }; var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 7); scroll.AddChild(content); tabs.AddChild(scroll); return content; }
    private static ItemList List(Control parent, float height) { var list = new ItemList { CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = SizeFlags.ExpandFill }; parent.AddChild(list); return list; }
    private static LineEdit Field(Control parent, string label, string placeholder) { var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted)); var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(field); parent.AddChild(box); return field; }
    private static OptionButton Choice(Control parent, string label, IEnumerable<string> choices) { var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted)); var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; foreach (var item in choices) choice.AddItem(item); box.AddChild(choice); parent.AddChild(box); return choice; }
    private static HBoxContainer CollectionButtons(string addLabel, Action add, Action remove, Action clear) { var row = new HBoxContainer(); var create = StudioStyle.Button(addLabel, compact: true); var update = StudioStyle.Button("Mettre à jour", compact: true); var delete = StudioStyle.Button("Retirer", compact: true); create.Pressed += clear; update.Pressed += add; delete.Pressed += remove; row.AddChild(create); row.AddChild(update); row.AddChild(delete); return row; }
    private void SetStatus(string message, bool error) { StatusChanged?.Invoke(message, error); }
    private static int Selected(ItemList list) => list.GetSelectedItems().FirstOrDefault(-1);
    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    private static bool TryDouble(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IEnumerable<string> VisibleText(Node node) { foreach (var child in node.GetChildren()) { if (child is Label { Visible: true } label) yield return label.Text; if (child is Button { Visible: true } button) yield return button.Text; foreach (var value in VisibleText(child)) yield return value; } }
    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
