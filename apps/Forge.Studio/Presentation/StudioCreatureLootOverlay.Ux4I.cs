using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureLootOverlayUx4I : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureLootEvidenceSourceSnapshotUx4I? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureLootChoiceUx4I> _choices = [];
    private IReadOnlyList<StudioCreatureLootItemChoiceUx4I> _itemChoices = [];
    private IReadOnlyList<StudioCreatureLootSkillChoiceUx4I> _skillChoices = [];
    private StudioCreatureLootDraftUx4I? _draft;
    private StudioCreatureLootPreviewUx4I? _previewResult;
    private readonly List<CreatureLootProfile.LootEntryPolicy> _entries = [];

    private readonly OptionButton _creature;
    private readonly Label _context;
    private readonly CheckBox _required;
    private readonly TabContainer _tabs;
    private readonly ItemList _entryList;
    private readonly OptionButton _item;
    private readonly LineEdit _slot;
    private readonly LineEdit _minimum;
    private readonly LineEdit _maximum;
    private readonly LineEdit _chance;
    private readonly CheckBox _dropOnDeath;
    private readonly CheckBox _pickpocketable;
    private readonly OptionButton _ownership;
    private readonly OptionButton _instancing;
    private readonly LineEdit _fixedOwner;
    private readonly CheckBox _theftEnabled;
    private readonly OptionButton _theftSkill;
    private readonly OptionButton _perceptionSkill;
    private readonly LineEdit _theftDifficulty;
    private readonly LineEdit _perceptionDifficulty;
    private readonly CheckBox _serverConsent;
    private readonly LineEdit _crime;
    private readonly LineEdit _cooldown;
    private readonly LineEdit _audit;
    private readonly OptionButton _corpseMode;
    private readonly LineEdit _lifetime;
    private readonly LineEdit _container;
    private readonly LineEdit _ragdoll;
    private readonly CheckBox _resurrection;
    private readonly LineEdit _corpseOwnership;
    private readonly LineEdit _maximumItems;
    private readonly OptionButton _cleanup;
    private readonly RichTextLabel _summary;
    private readonly VBoxContainer _diagnostics;
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
    internal bool ShowsDrops => _entries.Count > 0 && _summary.Text.Contains("Drops", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsPickpocket => _theftEnabled.ButtonPressed && _summary.Text.Contains("Pickpocket", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsCorpse => _corpseMode.Selected > 0 && _summary.Text.Contains("Cadavre", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Objets droppables", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Pickpocket", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Cadavre et loot corpse", StringComparison.OrdinalIgnoreCase) && !ContainsGuid(text);
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

    internal StudioCreatureLootOverlayUx4I(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioCreatureLootOverlayUx4I";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; Visible = false;
        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureLootDialogUx4I", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 58; dialog.OffsetTop = 30; dialog.OffsetRight = -58; dialog.OffsetBottom = -30;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7); dialog.AddChild(root);
        var header = new HBoxContainer(); var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("LOOT, PICKPOCKET ET CADAVRE · UX4-I", 17, StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Drops de la table de combat, vol et corpse · aucune référence UUID ni JSON affichée.",
            10, StudioStyle.Muted, wrap: true)); header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; header.AddChild(close);
        root.AddChild(header);
        var contextRow = new HBoxContainer(); contextRow.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(390, 30) }; contextRow.AddChild(_creature);
        _required = new CheckBox { Text = "Requis au runtime" }; contextRow.AddChild(_required);
        _context = StudioStyle.Label("Sélection explicite requise · une table de combat doit exister.",
            9, StudioStyle.Muted, wrap: true); _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contextRow.AddChild(_context); root.AddChild(contextRow);
        var tabHost = new Control { CustomMinimumSize = new Vector2(0, 410), SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true }; root.AddChild(tabHost);
        _tabs = new TabContainer(); _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); tabHost.AddChild(_tabs);

        var drops = Tab(_tabs, "1 · Objets droppables");
        drops.AddChild(StudioStyle.Label("Objets droppables", 11, StudioStyle.Accent));
        drops.AddChild(StudioStyle.Label(
            "Entrées issues uniquement de la table de combat ; quantité, chance, ownership et instanciation.",
            9, StudioStyle.Muted, wrap: true));
        _entryList = List(drops, 120);
        var dropGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _item = Choice(dropGrid, "Objet de combat", []); _slot = Field(dropGrid, "Slot optionnel", "relic");
        _minimum = Field(dropGrid, "Quantité min", "1"); _maximum = Field(dropGrid, "Quantité max", "1");
        _chance = Field(dropGrid, "Chance 0..1", "1");
        _ownership = Choice(dropGrid, "Propriété", ["Tueur", "Groupe", "Libre", "Propriétaire fixe", "Serveur"]);
        _instancing = Choice(dropGrid, "Instanciation", ["Partagé", "Par joueur", "Par groupe"]);
        _fixedOwner = Field(dropGrid, "Propriétaire fixe", "owner.key"); drops.AddChild(dropGrid);
        var dropFlags = new HBoxContainer(); _dropOnDeath = new CheckBox { Text = "Drop au décès" };
        _pickpocketable = new CheckBox { Text = "Pickpocketable" }; dropFlags.AddChild(_dropOnDeath);
        dropFlags.AddChild(_pickpocketable); drops.AddChild(dropFlags);
        drops.AddChild(CollectionButtons("Nouvelle entrée", () => SaveEntry(), RemoveEntry, ClearEntry));

        var theft = Tab(_tabs, "2 · Pickpocket"); theft.AddChild(StudioStyle.Label("Pickpocket", 11, StudioStyle.Accent));
        theft.AddChild(StudioStyle.Label(
            "Compétences réelles de la progression, consentement serveur, crime, cooldown et audit.",
            9, StudioStyle.Muted, wrap: true));
        _theftEnabled = new CheckBox { Text = "Pickpocket activé" }; theft.AddChild(_theftEnabled);
        var theftGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _theftSkill = Choice(theftGrid, "Compétence de vol", []);
        _perceptionSkill = Choice(theftGrid, "Compétence de perception", []);
        _theftDifficulty = Field(theftGrid, "Difficulté", "10");
        _perceptionDifficulty = Field(theftGrid, "Difficulté de détection", "10");
        _crime = Field(theftGrid, "Crime", "crime.theft"); _cooldown = Field(theftGrid, "Cooldown (s)", "30");
        _audit = Field(theftGrid, "Tag d’audit", "loot.pickpocket"); theft.AddChild(theftGrid);
        _serverConsent = new CheckBox { Text = "Consentement serveur obligatoire" }; theft.AddChild(_serverConsent);

        var corpse = Tab(_tabs, "3 · Cadavre et loot corpse");
        corpse.AddChild(StudioStyle.Label("Cadavre et loot corpse", 11, StudioStyle.Accent));
        corpse.AddChild(StudioStyle.Label(
            "Mode, durée, conteneur, ragdoll, résurrection, ownership, budget et nettoyage.",
            9, StudioStyle.Muted, wrap: true));
        var corpseGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _corpseMode = Choice(corpseGrid, "Mode", ["Aucun", "Conteneur", "Ragdoll + conteneur", "Ragdoll visuel"]);
        _lifetime = Field(corpseGrid, "Durée (s)", "60"); _container = Field(corpseGrid, "Conteneur", "corpse.standard");
        _ragdoll = Field(corpseGrid, "Profil ragdoll", "ragdoll.humanoid");
        _corpseOwnership = Field(corpseGrid, "Politique propriétaire", "party");
        _maximumItems = Field(corpseGrid, "Objets max", "8");
        _cleanup = Choice(corpseGrid, "Nettoyage", ["Immédiat si vide", "Temporisé", "Budget distance", "Persistant"]);
        corpseGrid.AddChild(new Control()); corpse.AddChild(corpseGrid);
        _resurrection = new CheckBox { Text = "Résurrection autorisée" }; corpse.AddChild(_resurrection);

        var review = Tab(_tabs, "4 · Aperçu et validation");
        review.AddChild(StudioStyle.Label("Aperçu et validation", 11, StudioStyle.Accent));
        review.AddChild(StudioStyle.Label(
            "Projection Domain/Application uniquement : aucun objet, vol, cadavre, acteur ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true)); _diagnostics = new VBoxContainer(); review.AddChild(_diagnostics);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(0, 145),
            FitContent = false, SelectionEnabled = true }; root.AddChild(_summary);
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
        _session.CancelCreatureLootUx4I(); _previewResult = null; _draft = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBoundSession()
    {
        _session.CancelCreatureLootUx4I(); _previewResult = null; _draft = null; _applied = false;
        _choices = _session.CreatureLootChoicesUx4I; _updating = true; _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…"); foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0); _updating = false; ClearControls(); Visible = true; SetEditingEnabled(false);
        _context.Text = _choices.Count == 0 ? "Aucune créature éditable dans ce module." :
            "Sélection explicite requise · une table de combat doit exister.";
        RenderEmpty(_choices.Count == 0 ? "État vide réel : aucune créature éditable." :
            "Choisissez une créature pour ouvrir son loot, son pickpocket et son cadavre.");
    }
    private void SelectCreature(long selected)
    {
        if (_updating) return; _session.CancelCreatureLootUx4I(); _previewResult = null; _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        { _draft = null; ClearControls(); SetEditingEnabled(false); RenderEmpty("Sélection explicite requise."); return; }
        var choice = _choices[(int)selected - 1]; _draft = _session.CreateCreatureLootDraftUx4I(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls(); SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly ? "Créature installée en lecture seule : créez une variante projet." :
                "Cette créature ne possède pas de table de loot de combat éditable."); return;
        }
        _itemChoices = _session.CreatureLootItemChoicesUx4I(choice.BlueprintId);
        _skillChoices = _session.CreatureLootSkillChoicesUx4I(choice.BlueprintId);
        Populate(_draft); SetEditingEnabled(true);
        _context.Text = $"{choice.Label} · {_itemChoices.Count} objet(s) de combat · {_skillChoices.Count} compétence(s).";
        RenderEmpty("Modifiez le brouillon puis prévisualisez la commande canonique.");
    }

    private void Populate(StudioCreatureLootDraftUx4I draft)
    {
        _updating = true; _entries.Clear(); _entries.AddRange(draft.Entries); _required.ButtonPressed = draft.RequiredAtRuntime;
        _theftEnabled.ButtonPressed = draft.Theft.Enabled; PopulateItems(); PopulateSkills();
        SelectSkill(_theftSkill, draft.Theft.SkillId); SelectSkill(_perceptionSkill, draft.Theft.PerceptionSkillId);
        SetText(_theftDifficulty, Format(draft.Theft.Difficulty)); SetText(_perceptionDifficulty, Format(draft.Theft.PerceptionDifficulty));
        _serverConsent.ButtonPressed = draft.Theft.RequireServerConsent; SetText(_crime, draft.Theft.CrimeId);
        SetText(_cooldown, Format(draft.Theft.CooldownSeconds)); SetText(_audit, draft.Theft.AuditTag);
        _corpseMode.Select((int)draft.Corpse.Mode); SetText(_lifetime, Format(draft.Corpse.LifetimeSeconds));
        SetText(_container, draft.Corpse.ContainerId ?? string.Empty); SetText(_ragdoll, draft.Corpse.RagdollProfileId ?? string.Empty);
        _resurrection.ButtonPressed = draft.Corpse.AllowResurrection; SetText(_corpseOwnership, draft.Corpse.OwnershipPolicy);
        SetText(_maximumItems, draft.Corpse.MaximumItems.ToString(CultureInfo.InvariantCulture));
        _cleanup.Select((int)draft.Corpse.CleanupMode); _updating = false; RebuildEntryList(0);
    }
    private void PopulateItems()
    { _item.Clear(); foreach (var choice in _itemChoices) _item.AddItem(choice.Label); if (_itemChoices.Count > 0) _item.Select(0); }
    private void PopulateSkills()
    {
        foreach (var field in new[] { _theftSkill, _perceptionSkill })
        { field.Clear(); foreach (var choice in _skillChoices) field.AddItem(choice.Label); if (_skillChoices.Count > 0) field.Select(0); }
    }
    private void RebuildEntryList(int selected)
    {
        _updating = true; _entryList.Clear();
        foreach (var entry in _entries) _entryList.AddItem(
            $"{HumanItem(entry.ItemSlug)} · {entry.MinimumQuantity}–{entry.MaximumQuantity} · {entry.Chance:P0} · " +
            $"{(entry.DropOnDeath ? "décès" : "sans drop")} · {(entry.Pickpocketable ? "volable" : "protégé")}");
        if (_entries.Count > 0) { selected = Math.Clamp(selected, 0, _entries.Count - 1); _entryList.Select(selected); SelectEntry(selected); }
        else ClearEntry(); _updating = false;
    }
    private void SelectEntry(long selected)
    {
        if (selected < 0 || selected >= _entries.Count) return; var entry = _entries[(int)selected]; _updating = true;
        var itemIndex = _itemChoices.ToList().FindIndex(item => item.ItemSlug == entry.ItemSlug); if (itemIndex >= 0) _item.Select(itemIndex);
        SetText(_slot, entry.SlotId ?? string.Empty); SetText(_minimum, entry.MinimumQuantity.ToString(CultureInfo.InvariantCulture));
        SetText(_maximum, entry.MaximumQuantity.ToString(CultureInfo.InvariantCulture)); SetText(_chance, Format(entry.Chance));
        _dropOnDeath.ButtonPressed = entry.DropOnDeath; _pickpocketable.ButtonPressed = entry.Pickpocketable;
        _ownership.Select((int)entry.Ownership); _instancing.Select((int)entry.Instancing);
        SetText(_fixedOwner, entry.FixedOwnerId ?? string.Empty); _updating = false;
    }
    private void ClearEntry()
    {
        _updating = true; if (_itemChoices.Count > 0) _item.Select(0); SetText(_slot, string.Empty);
        SetText(_minimum, "1"); SetText(_maximum, "1"); SetText(_chance, "1");
        _dropOnDeath.ButtonPressed = true; _pickpocketable.ButtonPressed = false; _ownership.Select(0);
        _instancing.Select(0); SetText(_fixedOwner, string.Empty); _entryList.DeselectAll(); _updating = false;
    }
    private bool SaveEntry(bool invalidate = true)
    {
        if (_draft is null || _item.Selected < 0 || _item.Selected >= _itemChoices.Count ||
            !TryInt(_minimum.Text, out var minimum) || !TryInt(_maximum.Text, out var maximum) ||
            !TryDouble(_chance.Text, out var chance)) return false;
        var selected = Selected(_entryList); var id = selected >= 0 && selected < _entries.Count
            ? _entries[selected].Id : StudioSessionController.StableCreatureRulesIdUx4F(_draft.BlueprintId.Value,
                "ux4i-ui-entry:" + _itemChoices[_item.Selected].ItemSlug);
        var entry = new CreatureLootProfile.LootEntryPolicy(id, _itemChoices[_item.Selected].ItemSlug,
            EmptyToNull(_slot.Text), minimum, maximum, chance, _dropOnDeath.ButtonPressed,
            _pickpocketable.ButtonPressed, (LootOwnershipMode)_ownership.Selected,
            (LootInstancingMode)_instancing.Selected, EmptyToNull(_fixedOwner.Text));
        if (selected >= 0 && selected < _entries.Count) _entries[selected] = entry; else { _entries.Add(entry); selected = _entries.Count - 1; }
        RebuildEntryList(selected); if (invalidate) InvalidatePreview(); return true;
    }
    private void RemoveEntry()
    { var selected = Selected(_entryList); if (selected < 0 || selected >= _entries.Count) return; _entries.RemoveAt(selected); RebuildEntryList(Math.Min(selected, _entries.Count - 1)); InvalidatePreview(); }

    private StudioCreatureLootDraftUx4I? ReadDraft()
    {
        if (_draft is null || !TryDouble(_theftDifficulty.Text, out var theftDifficulty) ||
            !TryDouble(_perceptionDifficulty.Text, out var perceptionDifficulty) ||
            !TryDouble(_cooldown.Text, out var cooldown) || !TryDouble(_lifetime.Text, out var lifetime) ||
            !TryInt(_maximumItems.Text, out var maximumItems)) return null;
        var selected = Selected(_entryList); if (selected >= 0 && !SaveEntry(false)) return null;
        var skill = _theftSkill.Selected >= 0 && _theftSkill.Selected < _skillChoices.Count
            ? _skillChoices[_theftSkill.Selected].SkillId : string.Empty;
        var perception = _perceptionSkill.Selected >= 0 && _perceptionSkill.Selected < _skillChoices.Count
            ? _skillChoices[_perceptionSkill.Selected].SkillId : string.Empty;
        return _draft with
        {
            RequiredAtRuntime = _required.ButtonPressed, Entries = _entries.ToArray(),
            Theft = new(_theftEnabled.ButtonPressed, skill, perception, theftDifficulty, perceptionDifficulty,
                _serverConsent.ButtonPressed, _crime.Text, cooldown, _audit.Text),
            Corpse = new((CreatureCorpseMode)_corpseMode.Selected, lifetime, EmptyToNull(_container.Text),
                EmptyToNull(_ragdoll.Text), _resurrection.ButtonPressed, _corpseOwnership.Text, maximumItems,
                (CorpseCleanupMode)_cleanup.Selected),
        };
    }

    private void Preview()
    {
        var draft = ReadDraft(); if (draft is null) { SetStatus("Valeurs numériques ou références incomplètes.", true); return; }
        _draft = draft; _previewResult = _session.PreviewCreatureLootUx4I(draft);
        _previewDryRun = _previewResult.Succeeded; RenderPreview(_previewResult, _previewDryRun);
        _apply.Disabled = !_previewResult.CanApply; SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }
    private void Apply()
    {
        var draft = ReadDraft(); if (draft is null) { SetStatus("Brouillon incomplet.", true); return; }
        var result = _session.ApplyApprovedCreatureLootUx4I(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        CreatureChanged?.Invoke(); _draft = _session.CreateCreatureLootDraftUx4I(draft.BlueprintId);
        if (_draft is not null) Populate(_draft); _previewResult = null; _apply.Disabled = true;
    }
    private void Undo() { var result = _session.UndoCreatureLootUx4I(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Redo() { var result = _session.RedoCreatureLootUx4I(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void Reload()
    {
        if (_draft is null) return; _draft = _session.CreateCreatureLootDraftUx4I(_draft.BlueprintId);
        if (_draft is not null) Populate(_draft); _previewResult = null; _apply.Disabled = true; CreatureChanged?.Invoke();
    }
    private void RenderPreview(StudioCreatureLootPreviewUx4I result, bool dryRun)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}", 9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "APERÇU REFUSÉ")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"{result.DropSummary}\n{result.TheftSummary}\n{result.CorpseSummary}\n{result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
    }
    private void RenderEmpty(string message) { foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _summary.Text = message; }
    private void InvalidatePreview() { if (_updating) return; _session.CancelCreatureLootUx4I(); _previewResult = null; _apply.Disabled = true; }
    private void SetEditingEnabled(bool enabled)
    { _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady; _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null; }
    private void ClearControls()
    { _updating = true; _entries.Clear(); _entryList.Clear(); _item.Clear(); foreach (var field in new[] { _theftSkill, _perceptionSkill }) field.Clear(); _updating = false; }
    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature; _entryList.ItemSelected += SelectEntry;
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) { _lastFailure = "release_previous"; return false; }
        if (!TryOpenEvidence()) { _lastFailure = _lastFailure == "none" ? "open" : _lastFailure; return false; }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0) { _lastFailure = "fixture_creature_missing"; Close(); return false; }
        _creature.Select(index + 1); SelectCreature(index + 1);
        if (_draft is null || _itemChoices.Count == 0 || _skillChoices.Count == 0)
        { _lastFailure = "draft_items_or_skills"; Close(); return false; }
        _draft = StudioSessionController.ChangedCreatureLootDraftUx4I(_draft, _itemChoices, _skillChoices); Populate(_draft);
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · copie isolée, source strictement inchangée.\n{_summary.Text}";
        _tabs.CurrentTab = mode.Contains("pick", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("corpse", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    ShowsDrops && ShowsPickpocket && ShowsCorpse && UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:drops={ShowsDrops}:pickpocket={ShowsPickpocket}:corpse={ShowsCorpse}:isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4I_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-I isolée prête · aucune donnée source modifiée.", false); return true;
    }
    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-i-creature-loot") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; var history = transaction.History;
            if (original is null || history is null) return false; Apply();
            var applied = _applied && transaction.Project is { } appliedProject && !ReferenceEquals(original, appliedProject);
            if (applied) Undo(); var undone = applied && ReferenceEquals(original, transaction.Project);
            if (undone) Redo(); var redoneProject = transaction.Project;
            var redone = undone && redoneProject is not null && !ReferenceEquals(original, redoneProject);
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            { reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded && ProjectJsonSerializer.Serialize(reopened.Project!) == ProjectJsonSerializer.Serialize(redoneProject!); }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched && EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4I_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        { reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close(); _sourceSession.RestoreCreatureLootApprovalUx4I(snapshot.Approval); }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4IEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4i-evidence-" + Guid.NewGuid().ToString("N"));
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
    private StudioCreatureLootEvidenceSourceSnapshotUx4I? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureLootApprovalUx4I());
    }
    private bool SourceMatches(StudioCreatureLootEvidenceSourceSnapshotUx4I snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId && _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureLootApprovalUx4I() == snapshot.Approval;
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
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4i-evidence-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true); return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private string HumanItem(string slug) => _itemChoices.FirstOrDefault(item => item.ItemSlug == slug)?.Label ?? "Objet hérité protégé";
    private void SelectSkill(OptionButton field, string id) { var index = _skillChoices.ToList().FindIndex(item => item.SkillId == id); if (index >= 0) field.Select(index); }
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
    private static void SetText(LineEdit field, string text) => field.Text = text;
    private static IEnumerable<string> VisibleText(Node node) { foreach (var child in node.GetChildren()) { if (child is Label { Visible: true } label) yield return label.Text; if (child is Button { Visible: true } button) yield return button.Text; foreach (var value in VisibleText(child)) yield return value; } }
    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
