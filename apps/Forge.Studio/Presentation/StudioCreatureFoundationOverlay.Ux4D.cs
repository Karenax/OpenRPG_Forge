using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureFoundationOverlayUx4D : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureFoundationEvidenceSourceSnapshotUx4D? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealCrypt;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureChoiceUx4D> _choices = [];
    private IReadOnlyList<StudioCreatureFactionChoiceUx4D> _factionChoices = [];
    private StudioCreatureFoundationDraftUx4D? _draft;
    private StudioCreatureFoundationPreviewUx4D? _previewResult;

    private readonly OptionButton _creature;
    private readonly Label _context;
    private readonly TabContainer _tabs;
    private readonly CheckBox _required;
    private readonly LineEdit _spawnHandler;
    private readonly LineEdit _frenchName;
    private readonly LineEdit _englishName;
    private readonly LineEdit _aliases;
    private readonly LineEdit _pronouns;
    private readonly TextEdit _frenchBiography;
    private readonly TextEdit _englishBiography;
    private readonly TextEdit _authorNotes;
    private readonly LineEdit _narrativeTags;
    private readonly LineEdit _assetId;
    private readonly LineEdit _archetype;
    private readonly LineEdit _ruleset;
    private readonly LineEdit _rig;
    private readonly OptionButton _faction;
    private readonly OptionButton _role;
    private readonly LineEdit _threat;
    private readonly LineEdit _visualKit;
    private readonly LineEdit _combatTags;
    private readonly LineEdit _species;
    private readonly LineEdit _parents;
    private readonly TextEdit _traitSets;
    private readonly LineEdit _variants;
    private readonly LineEdit _skeleton;
    private readonly Label _visualGlyph;
    private readonly Label _visualCaption;
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
    internal bool ShowsFoundation => _context.Text.Contains("fondation", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsSpawnBinding => _summary.Text.Contains("OnSpawn OnSpawnWitness", StringComparison.Ordinal) &&
                                       _spawnHandler.Text == "OnSpawnWitness";
    internal bool ShowsLocalizedIdentity => !string.IsNullOrWhiteSpace(_frenchName.Text) &&
                                            _summary.Text.Contains("Identité", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsWizard => _summary.Text.Contains("Assistant", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsRoleAndThreat => _summary.Text.Contains("menace", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsSpecies => _summary.Text.Contains("Espèce", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Blueprint et identité", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("OnSpawn", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Assistant guidé", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Race, espèce et sous-type", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu et validation", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealCryptEvidence => _usesRealCrypt;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreatureFoundationOverlayUx4D(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioCreatureFoundationOverlayUx4D";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureFoundationDialogUx4D", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 58;
        dialog.OffsetTop = 30;
        dialog.OffsetRight = -58;
        dialog.OffsetBottom = -30;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7);
        dialog.AddChild(root);

        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("ASSISTANT DE CRÉATURE · UX4-D", 17, StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Blueprint, identité localisée, rôle, menace et espèce · aperçu canonique obligatoire · aucun UUID affiché.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var contextRow = new HBoxContainer();
        contextRow.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(330, 30) };
        contextRow.AddChild(_creature);
        _context = StudioStyle.Label("Sélection explicite requise · aucune fondation créée automatiquement.",
            9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contextRow.AddChild(_context);
        root.AddChild(contextRow);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 370),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer();
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(_tabs);

        var identityTab = Tab(_tabs, "1 · Blueprint et identité");
        identityTab.AddChild(StudioStyle.Label("Blueprint et identité", 11, StudioStyle.Accent));
        identityTab.AddChild(StudioStyle.Label(
            "Le slug et les UUID restent protégés. Les textes localisés sont édités sans toucher aux profils voisins.",
            9, StudioStyle.Muted, wrap: true));
        _required = new CheckBox { Text = "Fondation requise au runtime" };
        identityTab.AddChild(_required);
        _spawnHandler = Field(identityTab, "OnSpawn · handler après validation complète", "OnSpawned");
        _frenchName = Field(identityTab, "Nom français obligatoire", "Gardien des Cendres");
        _englishName = Field(identityTab, "Nom anglais facultatif", "Ash Warden");
        _aliases = Field(identityTab, "Aliases", "gardien, veilleur");
        _pronouns = Field(identityTab, "Pronoms", "iel");
        _frenchBiography = Area(identityTab, "Biographie française", 78);
        _englishBiography = Area(identityTab, "Biographie anglaise facultative", 64);
        _authorNotes = Area(identityTab, "Notes auteur", 64);
        _narrativeTags = Field(identityTab, "Tags narratifs", "boss, crypt");

        var wizardTab = Tab(_tabs, "2 · Assistant guidé");
        wizardTab.AddChild(StudioStyle.Label("Assistant guidé", 11, StudioStyle.Accent));
        wizardTab.AddChild(StudioStyle.Label(
            "Ces valeurs alimentent le profil RPG Kit data-driven. Aucune formule de règles propriétaire n’est imposée.",
            9, StudioStyle.Muted, wrap: true));
        _assetId = Field(wizardTab, "Asset canonique", "asset.creature.guardian");
        _archetype = Field(wizardTab, "Archétype", "archetype.guardian");
        _ruleset = Field(wizardTab, "Ruleset", "ruleset.core");
        _rig = Field(wizardTab, "Rig", "rig.humanoid");
        wizardTab.AddChild(StudioStyle.Label("Faction humaine", 9, StudioStyle.Muted));
        _faction = new OptionButton();
        wizardTab.AddChild(_faction);
        wizardTab.AddChild(StudioStyle.Label("Rôle de rencontre", 9, StudioStyle.Muted));
        _role = new OptionButton();
        foreach (var value in Enum.GetValues<CreatureAuthoringEncounterRole>()) _role.AddItem(RoleLabel(value));
        wizardTab.AddChild(_role);
        _threat = Field(wizardTab, "Niveau de menace 0..1000", "3");
        _visualKit = Field(wizardTab, "Kit visuel", "visual.guardian");
        _combatTags = Field(wizardTab, "Tags de combat", "guardian, melee");

        var speciesTab = Tab(_tabs, "3 · Race, espèce et sous-type");
        speciesTab.AddChild(StudioStyle.Label("Race, espèce et sous-type", 11, StudioStyle.Accent));
        speciesTab.AddChild(StudioStyle.Label(
            "Héritage contrôlé, ensembles de traits complets, variantes et squelette requis.",
            9, StudioStyle.Muted, wrap: true));
        _species = Field(speciesTab, "Espèce", "species.human");
        _parents = Field(speciesTab, "Espèces parentes", "species.humanoid");
        _traitSets = Area(speciesTab, "Ensembles de traits · une ligne identifiant=trait,trait", 115);
        _variants = Field(speciesTab, "Variantes / sous-types", "standard");
        _skeleton = Field(speciesTab, "Squelette requis", "rig.humanoid");

        var reviewTab = Tab(_tabs, "4 · Aperçu et validation");
        reviewTab.AddChild(StudioStyle.Label("Aperçu et validation", 11, StudioStyle.Accent));
        reviewTab.AddChild(StudioStyle.Label(
            "Aperçu de composition éditorial : aucun modèle, script, moteur ou réseau n’est chargé dans cette surface.",
            9, StudioStyle.Muted, wrap: true));
        var visual = StudioStyle.Panel("CreatureVisualSummaryUx4D", StudioStyle.Surface, 12, 1);
        visual.CustomMinimumSize = new Vector2(0, 108);
        reviewTab.AddChild(visual);
        var visualRow = new HBoxContainer();
        visual.AddChild(visualRow);
        _visualGlyph = StudioStyle.Label("◆", 44, StudioStyle.Accent);
        _visualGlyph.CustomMinimumSize = new Vector2(80, 70);
        visualRow.AddChild(_visualGlyph);
        _visualCaption = StudioStyle.Label("Sélectionnez une créature pour composer son aperçu.", 12,
            StudioStyle.Text, wrap: true);
        _visualCaption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        visualRow.AddChild(_visualCaption);
        _diagnostics = new VBoxContainer();
        _diagnostics.AddThemeConstantOverride("separation", 4);
        reviewTab.AddChild(_diagnostics);

        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 135),
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        var footerNote = StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit une seule commande annulable; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true);
        root.AddChild(footerNote);
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34);
            footer.AddChild(button);
        }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        root.AddChild(footer);

        ConnectEvents();
        RenderEmpty("Choisissez explicitement une créature. Une fondation absente ne sera initialisée qu’après aperçu et application.");
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (ReferenceEquals(_session, _sourceSession)) Visible = false;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null && _evidenceRoot is null)
            return;
        _session.CancelCreatureFoundationUx4D();
        _previewResult = null;
        _draft = null;
        Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    private void OpenBoundSession()
    {
        _session.CancelCreatureFoundationUx4D();
        _previewResult = null;
        _draft = null;
        _applied = false;
        _choices = _session.CreatureChoicesUx4D;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0);
        _updating = false;
        _context.Text = _choices.Count == 0
            ? "Aucune créature éditable dans ce module."
            : "Sélection explicite requise · aucune fondation créée automatiquement.";
        ClearControls();
        Visible = true;
        SetEditingEnabled(false);
        RenderEmpty(_choices.Count == 0
            ? "État vide réel : ajoutez d’abord un blueprint Créature dans le parcours de contenu."
            : "Choisissez une créature pour ouvrir sa fondation canonique.");
    }

    private void SelectCreature(long selected)
    {
        if (_updating) return;
        _session.CancelCreatureFoundationUx4D();
        _previewResult = null;
        _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty("Sélection explicite requise.");
            return;
        }
        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateCreatureFoundationDraftUx4D(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly
                ? "Ce blueprint installé est en lecture seule. Créez d’abord une variante de projet."
                : "La fondation ne peut pas être ouverte.");
            return;
        }
        Populate(_draft);
        _context.Text = $"{choice.Label} · {(choice.HasFoundation ? "fondation existante" : "initialisation proposée")} · " +
                        "slug protégé";
        SetEditingEnabled(true);
        RenderEmpty(choice.HasFoundation
            ? "Fondation chargée. Modifiez la fiche puis prévisualisez."
            : "Fondation absente : l’assistant propose une initialisation complète, sans écriture avant Appliquer.");
    }

    private void Populate(StudioCreatureFoundationDraftUx4D draft)
    {
        _updating = true;
        _required.ButtonPressed = draft.RequiredAtRuntime;
        _spawnHandler.Text = draft.SpawnHandler;
        _frenchName.Text = draft.FrenchName;
        _englishName.Text = draft.EnglishName;
        _aliases.Text = draft.Aliases;
        _pronouns.Text = draft.Pronouns;
        _frenchBiography.Text = draft.FrenchBiography;
        _englishBiography.Text = draft.EnglishBiography;
        _authorNotes.Text = draft.AuthorNotes;
        _narrativeTags.Text = draft.NarrativeTags;
        _assetId.Text = draft.AssetId;
        _archetype.Text = draft.ArchetypeId;
        _ruleset.Text = draft.RulesetId;
        _rig.Text = draft.RigId;
        _role.Select((int)draft.EncounterRole);
        _threat.Text = draft.ThreatRating;
        _visualKit.Text = draft.VisualKitId;
        _combatTags.Text = draft.CombatTags;
        _species.Text = draft.SpeciesId;
        _parents.Text = draft.ParentSpeciesIds;
        _traitSets.Text = draft.TraitSets;
        _variants.Text = draft.Variants;
        _skeleton.Text = draft.RequiredSkeletonId;
        RebuildFactionChoices(draft);
        UpdateVisualSummary();
        _updating = false;
    }

    private void RebuildFactionChoices(StudioCreatureFoundationDraftUx4D draft)
    {
        _factionChoices = _session.CreatureFactionChoicesUx4D(draft.BlueprintId);
        _faction.Clear();
        foreach (var choice in _factionChoices) _faction.AddItem(choice.Label + (choice.IsProtected ? " · protégée" : string.Empty));
        var index = _factionChoices.ToList().FindIndex(item => item.Value == draft.FactionId);
        _faction.Select(index >= 0 ? index : 0);
    }

    private StudioCreatureFoundationDraftUx4D? ReadDraft()
    {
        if (_draft is null) return null;
        var factionIndex = _faction.Selected;
        var faction = factionIndex >= 0 && factionIndex < _factionChoices.Count
            ? _factionChoices[factionIndex].Value
            : _draft.FactionId;
        return _draft with
        {
            RequiredAtRuntime = _required.ButtonPressed,
            SpawnHandler = _spawnHandler.Text,
            FrenchName = _frenchName.Text,
            EnglishName = _englishName.Text,
            Aliases = _aliases.Text,
            Pronouns = _pronouns.Text,
            FrenchBiography = _frenchBiography.Text,
            EnglishBiography = _englishBiography.Text,
            AuthorNotes = _authorNotes.Text,
            NarrativeTags = _narrativeTags.Text,
            AssetId = _assetId.Text,
            ArchetypeId = _archetype.Text,
            RulesetId = _ruleset.Text,
            RigId = _rig.Text,
            FactionId = faction,
            EncounterRole = (CreatureAuthoringEncounterRole)Math.Clamp(_role.Selected, 0,
                Enum.GetValues<CreatureAuthoringEncounterRole>().Length - 1),
            ThreatRating = _threat.Text,
            VisualKitId = _visualKit.Text,
            CombatTags = _combatTags.Text,
            SpeciesId = _species.Text,
            ParentSpeciesIds = _parents.Text,
            TraitSets = _traitSets.Text,
            Variants = _variants.Text,
            RequiredSkeletonId = _skeleton.Text,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Choisissez d’abord une créature.", true);
            return;
        }
        _draft = draft;
        var project = _session.Project;
        var history = _session.History;
        var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? [];
        var dirty = _session.IsDirty;
        _previewResult = _session.PreviewCreatureFoundationUx4D(draft);
        var dryRun = project is not null && history is not null && ReferenceEquals(project, _session.Project) &&
                     ReferenceEquals(history, _session.History) && history.State.Revision == revision &&
                     history.Journal.SequenceEqual(journal) && _session.IsDirty == dirty;
        RenderPreview(_previewResult, dryRun);
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null || !CanonicalPreviewReady)
        {
            SetStatus("Prévisualisez une fiche valide avant de l’appliquer.", true);
            return;
        }
        var selectedBlueprint = draft.BlueprintId;
        var result = _session.ApplyApprovedCreatureFoundationUx4D(draft);
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _applied = true;
        CreatureChanged?.Invoke();
        RefreshAfterMutation(selectedBlueprint);
    }

    private void Undo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.UndoCreatureFoundationUx4D();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CreatureChanged?.Invoke();
            RefreshAfterMutation(selected);
        }
    }

    private void Redo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.RedoCreatureFoundationUx4D();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CreatureChanged?.Invoke();
            RefreshAfterMutation(selected);
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void RefreshAfterMutation(BlueprintId? selected)
    {
        _choices = _session.CreatureChoicesUx4D;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        var index = selected is null ? -1 : _choices.ToList().FindIndex(item => item.BlueprintId == selected);
        _creature.Select(index >= 0 ? index + 1 : 0);
        _updating = false;
        SelectCreature(index >= 0 ? index + 1 : 0);
    }

    private void RenderPreview(StudioCreatureFoundationPreviewUx4D result, bool dryRun)
    {
        _summary.Text =
            $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "CONFIGURATION REFUSÉE")}[/b]\n" +
            $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
            $"[color=#b8d9ff]Identité[/color] · {result.IdentitySummary}\n" +
            $"[color=#b8d9ff]Assistant[/color] · {result.WizardSummary}\n" +
            $"[color=#b8d9ff]Espèce[/color] · {result.SpeciesSummary}\n" +
            $"[color=#b8d9ff]Runtime[/color] · {result.RuntimeSummary}\n" +
            $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        if (result.Diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success, true));
        }
        else
        {
            foreach (var item in result.Diagnostics.Take(6))
                _diagnostics.AddChild(StudioStyle.Label($"{item.Code}\n{item.HumanMessage}", 9, StudioStyle.Danger, true));
        }
        _apply.Disabled = !result.CanApply;
        UpdateVisualSummary();
    }

    private void RenderEmpty(string message)
    {
        _summary.Text = $"[color=#b8a8ce]{message}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _diagnostics.AddChild(StudioStyle.Label("Aucune écriture sans aperçu approuvé.", 10, StudioStyle.Muted, true));
        _apply.Disabled = true;
        UpdateVisualSummary();
    }

    private void UpdateVisualSummary()
    {
        if (_draft is null)
        {
            _visualGlyph.Text = "◇";
            _visualCaption.Text = "Aucune créature sélectionnée · aucun modèle chargé.";
            return;
        }
        var role = (CreatureAuthoringEncounterRole)Math.Clamp(_role.Selected, 0,
            Enum.GetValues<CreatureAuthoringEncounterRole>().Length - 1);
        _visualGlyph.Text = role == CreatureAuthoringEncounterRole.Boss ? "◆" : "◈";
        _visualCaption.Text =
            $"{_frenchName.Text}\n{RoleLabel(role)} · menace {_threat.Text}\n{_species.Text} · kit {_visualKit.Text}\n" +
            "Composition éditoriale uniquement · aucun asset 3D chargé.";
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelCreatureFoundationUx4D();
        _previewResult = null;
        _apply.Disabled = true;
        UpdateVisualSummary();
    }

    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private void ClearControls()
    {
        _updating = true;
        foreach (var field in new[] { _frenchName, _englishName, _aliases, _pronouns, _narrativeTags, _assetId,
                     _archetype, _ruleset, _rig, _threat, _visualKit, _combatTags, _species, _parents, _variants,
                     _skeleton, _spawnHandler }) field.Text = string.Empty;
        foreach (var area in new[] { _frenchBiography, _englishBiography, _authorNotes, _traitSets })
            area.Text = string.Empty;
        _faction.Clear();
        _factionChoices = [];
        _required.ButtonPressed = false;
        _role.Select(0);
        _updating = false;
        UpdateVisualSummary();
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        _required.Toggled += _ => InvalidatePreview();
        _faction.ItemSelected += _ => InvalidatePreview();
        _role.ItemSelected += _ => InvalidatePreview();
        foreach (var field in new[] { _frenchName, _englishName, _aliases, _pronouns, _narrativeTags, _assetId,
                     _archetype, _ruleset, _rig, _threat, _visualKit, _combatTags, _species, _parents, _variants,
                     _skeleton, _spawnHandler }) field.TextChanged += _ => InvalidatePreview();
        foreach (var area in new[] { _frenchBiography, _englishBiography, _authorNotes, _traitSets })
            area.TextChanged += InvalidatePreview;
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        _lastFailure = "none";
        _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence())
        {
            _lastFailure = "release_previous";
            return false;
        }
        if (!TryOpenEvidence())
        {
            _lastFailure = _lastFailure == "none" ? "open" : _lastFailure;
            return false;
        }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0)
        {
            _lastFailure = "fixture_creature_missing";
            Close();
            return false;
        }
        _creature.Select(index + 1);
        SelectCreature(index + 1);
        if (_draft is null)
        {
            _lastFailure = "draft";
            Close();
            return false;
        }
        _frenchName.Text = _frenchName.Text + " · preuve isolée";
        _authorNotes.Text = "Capture Studio UX4-D · source inchangée";
        _spawnHandler.Text = "OnSpawnWitness";
        _threat.Text = "8.5";
        _role.Select((int)CreatureAuthoringEncounterRole.Controller);
        var project = _session.Project!;
        var history = _session.History!;
        var revision = history.State.Revision;
        var journal = history.Journal.ToArray();
        var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealCrypt ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"copie isolée, source strictement inchangée.\n{_summary.Text}";
        _tabs.CurrentTab = mode.Contains("wizard", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("species", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    ShowsFoundation && ShowsSpawnBinding && ShowsLocalizedIdentity && ShowsWizard &&
                    ShowsRoleAndThreat && ShowsSpecies &&
                    UsesIsolatedEvidence && EvidenceSourceUntouched && (_usesRealCrypt ^ _usesFallback);
        if (!ready)
        {
            _lastFailure =
                $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:" +
                $"foundation={ShowsFoundation}:spawn={ShowsSpawnBinding}:identity={ShowsLocalizedIdentity}:wizard={ShowsWizard}:" +
                $"role={ShowsRoleAndThreat}:species={ShowsSpecies}:isolated={UsesIsolatedEvidence}:" +
                $"source={EvidenceSourceUntouched}:origin={_usesRealCrypt ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4D_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-D isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-d-creature-foundation") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session;
            var original = transaction.Project;
            var history = transaction.History;
            if (original is null || history is null) return false;
            Apply();
            var applied = _applied && transaction.Project is { } appliedProject && !ReferenceEquals(original, appliedProject);
            if (applied) Undo();
            var undone = applied && ReferenceEquals(original, transaction.Project);
            if (undone) Redo();
            var redoneProject = transaction.Project;
            var redone = undone && redoneProject is not null && !ReferenceEquals(original, redoneProject);
            var saved = redone && transaction.Save().Succeeded;
            var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new StudioSessionController();
                persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(redoneProject!);
            }
            var sourceUntouched = SourceMatches(snapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4D_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureFoundationApprovalUx4D(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4DEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4d-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            {
                session.Close();
                _resourcesReleased = DeleteEvidenceRoot(root);
                return false;
            }
            _sourceSnapshot = snapshot;
            _evidenceSession = session;
            _evidenceRoot = root;
            _evidenceBlueprintId = evidence.BlueprintId;
            _session = session;
            _usesRealCrypt = evidence.UsesRealCryptSource;
            _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false;
            _resourcesReleased = false;
            _sourceRestored = false;
            OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealCrypt ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name;
            session.Close();
            _resourcesReleased = DeleteEvidenceRoot(root);
            _session = _sourceSession;
            _sourceRestored = true;
            return false;
        }
    }

    private StudioCreatureFoundationEvidenceSourceSnapshotUx4D? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureFoundationApprovalUx4D());
    }

    private bool SourceMatches(StudioCreatureFoundationEvidenceSourceSnapshotUx4D snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureFoundationApprovalUx4D() == snapshot.Approval;
    }

    private bool ReleaseEvidence()
    {
        var session = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _sourceSnapshot = null;
        _evidenceBlueprintId = null;
        _session = _sourceSession;
        _usesRealCrypt = false;
        _usesFallback = false;
        _previewDryRun = false;
        var released = true;
        try { session?.Close(); } catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root);
        if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased;
        _sourceRestored = true;
        return _resourcesReleased;
    }

    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4d-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        tabs.AddChild(scroll);
        var margin = StudioStyle.Margin(8, 8, 10, 10);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);
        return content;
    }

    private static LineEdit Field(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(field);
        parent.AddChild(field);
        return field;
    }

    private static TextEdit Area(VBoxContainer parent, string label, float height)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var area = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        parent.AddChild(area);
        return area;
    }

    private static string RoleLabel(CreatureAuthoringEncounterRole role) => role switch
    {
        CreatureAuthoringEncounterRole.Minion => "Sbire",
        CreatureAuthoringEncounterRole.Skirmisher => "Escarmoucheur",
        CreatureAuthoringEncounterRole.Defender => "Défenseur",
        CreatureAuthoringEncounterRole.Controller => "Contrôleur",
        CreatureAuthoringEncounterRole.Support => "Soutien",
        CreatureAuthoringEncounterRole.Boss => "Boss",
        CreatureAuthoringEncounterRole.NonCombatant => "Non-combattant",
        _ => role.ToString(),
    };

    private static IEnumerable<string> VisibleText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case RichTextLabel rich when rich.Visible: yield return rich.Text; break;
                case LineEdit line when line.Visible: yield return line.Text; break;
                case TextEdit area when area.Visible: yield return area.Text; break;
                case OptionButton option when option.Visible && option.Selected >= 0:
                    yield return option.GetItemText(option.Selected);
                    break;
                case TabContainer tabs when tabs.Visible:
                    for (var index = 0; index < tabs.GetTabCount(); index++) yield return tabs.GetTabTitle(index);
                    break;
            }
            foreach (var value in VisibleText(child)) yield return value;
        }
    }

    private static bool ContainsGuid(string value)
    {
        foreach (var token in value.Split([' ', '\r', '\n', '\t', '/', ':', '(', ')', '[', ']', ','],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token.Trim('.', ';'), out _)) return true;
        }
        return false;
    }
}
