using System.Globalization;
using Forge.Domain.Factions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioFactionRuntimeOverlayUx4B : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioFactionRuntimeEvidenceSourceSnapshotUx4B? _sourceSnapshot;
    private bool _usesRealCrypt;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private string _lastFailure = "none";
    private StudioFactionRuntimeDraftUx4B? _draft;
    private StudioFactionRuntimePreviewUx4B? _previewResult;
    private IReadOnlyList<StudioFactionChoiceUx4A> _choices = [];
    private readonly List<PersonalReputationOverlay> _personal = [];
    private readonly List<ReputationTransaction> _transactions = [];
    private readonly List<FactionWitnessPolicy> _witnesses = [];
    private readonly List<FactionMembership> _memberships = [];
    private readonly List<DynamicFactionDefinition> _dynamics = [];
    private readonly List<FactionDiplomacyState> _diplomacy = [];
    private bool _updating;
    private bool _applied;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _focusFaction;
    private readonly Label _context;
    private readonly CheckBox _personalConfigured;
    private readonly CheckBox _advancedConfigured;
    private readonly CheckBox _advancedRequired;
    private readonly CheckBox _reputationConfigured;
    private readonly CheckBox _reputationRequired;
    private readonly ItemList _personalList;
    private readonly OptionButton _personalKind;
    private readonly LineEdit _personalSubject;
    private readonly OptionButton _personalSource;
    private readonly OptionButton _personalTarget;
    private readonly LineEdit _personalDelta;
    private readonly LineEdit _personalExpiry;
    private readonly LineEdit _personalCause;
    private readonly ItemList _transactionList;
    private readonly OptionButton _transactionScope;
    private readonly LineEdit _transactionScopeId;
    private readonly OptionButton _transactionSource;
    private readonly OptionButton _transactionTarget;
    private readonly LineEdit _transactionDelta;
    private readonly LineEdit _transactionCause;
    private readonly LineEdit _transactionDate;
    private readonly ItemList _witnessList;
    private readonly OptionButton _witnessFaction;
    private readonly LineEdit _witnessPerception;
    private readonly LineEdit _witnessEvidence;
    private readonly LineEdit _witnessRadius;
    private readonly LineEdit _witnessMemory;
    private readonly LineEdit _witnessDelta;
    private readonly CheckBox _witnessAllies;
    private readonly ItemList _membershipList;
    private readonly LineEdit _membershipActor;
    private readonly OptionButton _membershipFaction;
    private readonly LineEdit _membershipRank;
    private readonly LineEdit _membershipCover;
    private readonly LineEdit _membershipLoyalty;
    private readonly LineEdit _membershipWeight;
    private readonly CheckBox _membershipPrimary;
    private readonly LineEdit _membershipExpiry;
    private readonly ItemList _dynamicList;
    private readonly OptionButton _dynamicFaction;
    private readonly OptionButton _dynamicKind;
    private readonly LineEdit _dynamicCreated;
    private readonly LineEdit _dynamicExpiry;
    private readonly CheckBox _dynamicActive;
    private readonly LineEdit _dynamicRevision;
    private readonly ItemList _diplomacyList;
    private readonly OptionButton _diplomacyTarget;
    private readonly OptionButton _diplomacyStatus;
    private readonly LineEdit _diplomacySince;
    private readonly LineEdit _diplomacyExpiry;
    private readonly LineEdit _diplomacyCause;
    private readonly RichTextLabel _summary;
    private readonly VBoxContainer _diagnostics;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? FactionChanged;
    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _focusFaction.Selected > 0;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsPersonalReputation => _personalList.ItemCount > 0;
    internal bool ShowsGlobalReputation => _transactionList.ItemCount > 0;
    internal bool ShowsWitnesses => _witnessList.ItemCount > 0;
    internal bool ShowsMemberships => _membershipList.ItemCount > 1;
    internal bool ShowsDynamicFactions => _dynamicList.ItemCount > 0 && _diplomacyList.ItemCount > 0;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Réputation personnelle", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Réputation globale", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Témoins", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Affiliations", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Factions dynamiques", StringComparison.OrdinalIgnoreCase) &&
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

    internal StudioFactionRuntimeOverlayUx4B(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioFactionRuntimeOverlayUx4B";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.015f, 0.02f, 0.035f, 0.93f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("FactionRuntimeDialogUx4B", StudioStyle.Chrome, 14, 2);
        _dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dialog.OffsetLeft = 64;
        _dialog.OffsetTop = 34;
        _dialog.OffsetRight = -64;
        _dialog.OffsetBottom = -34;
        AddChild(_dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 9);
        _dialog.AddChild(root);

        var heading = new HBoxContainer();
        heading.AddChild(StudioStyle.Label("FACTIONS VIVANTES · UX4-B", 20, StudioStyle.Accent));
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(spacer);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);
        root.AddChild(StudioStyle.Label(
            "Réputation personnelle/globale, témoins, affiliations multiples et diplomatie dynamique · aperçu sec obligatoire.",
            10, StudioStyle.Muted, wrap: true));

        var focusRow = new HBoxContainer();
        focusRow.AddChild(StudioStyle.Label("Contexte faction", 10, StudioStyle.Muted));
        _focusFaction = new OptionButton { CustomMinimumSize = new Vector2(270, 34) };
        focusRow.AddChild(_focusFaction);
        _context = StudioStyle.Label("Sélectionnez une faction pour déverrouiller la fiche.", 10, StudioStyle.Muted, true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        focusRow.AddChild(_context);
        root.AddChild(focusRow);

        var policyRow = new HBoxContainer();
        _personalConfigured = new CheckBox { Text = "Réputation personnelle configurée" };
        _advancedConfigured = new CheckBox { Text = "Profil avancé configuré" };
        _advancedRequired = new CheckBox { Text = "Avancé requis au runtime" };
        _reputationConfigured = new CheckBox { Text = "Réputation globale configurée" };
        _reputationRequired = new CheckBox { Text = "Réputation requise au runtime" };
        policyRow.AddChild(_personalConfigured);
        policyRow.AddChild(_advancedConfigured);
        policyRow.AddChild(_advancedRequired);
        policyRow.AddChild(_reputationConfigured);
        policyRow.AddChild(_reputationRequired);
        root.AddChild(policyRow);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 360),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        var tabs = new TabContainer();
        tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(tabs);
        root.AddChild(tabHost);

        var personalTab = Tab(tabs, "1 · Réputation personnelle");
        _personalList = List(personalTab, 92);
        _personalKind = EnumOption<PersonalReputationSubjectKind>();
        _personalSubject = Field("Sujet humain", personalTab, "hero, player-1 ou group-scouts");
        _personalSource = new OptionButton();
        _personalTarget = new OptionButton();
        _personalDelta = Field("Variation -100..100", personalTab, "35");
        _personalExpiry = Field("Expiration ISO facultative", personalTab, "2030-08-01T00:00:00Z");
        _personalCause = Field("Cause", personalTab, "trêve du gardien");
        AddOptionsRow(personalTab, "Type / source / cible", _personalKind, _personalSource, _personalTarget);
        AddEditorActions(personalTab, "Ajouter ou mettre à jour", SavePersonal, RemovePersonal);

        var globalTab = Tab(tabs, "2 · Réputation globale");
        _transactionList = List(globalTab, 92);
        _transactionScope = EnumOption<ReputationScope>();
        _transactionScopeId = Field("Portée nommée", globalTab, "world, campaign ou player");
        _transactionSource = new OptionButton();
        _transactionTarget = new OptionButton();
        _transactionDelta = Field("Variation -100..100", globalTab, "15");
        _transactionCause = Field("Cause", globalTab, "pacte restauré");
        _transactionDate = Field("Date ISO", globalTab, "2026-08-17T12:00:00Z");
        AddOptionsRow(globalTab, "Scope / source / cible", _transactionScope, _transactionSource, _transactionTarget);
        AddEditorActions(globalTab, "Ajouter ou mettre à jour", SaveTransaction, RemoveTransaction);

        var witnessTab = Tab(tabs, "3 · Témoins et affiliations");
        witnessTab.AddChild(StudioStyle.Label("TÉMOINS", 11, StudioStyle.Accent));
        _witnessList = List(witnessTab, 72);
        _witnessFaction = new OptionButton();
        _witnessPerception = Field("Perception min 0..1", witnessTab, "0.55");
        _witnessEvidence = Field("Preuve min 0..1", witnessTab, "0.45");
        _witnessRadius = Field("Rayon communication (m)", witnessTab, "35");
        _witnessMemory = Field("Mémoire (s)", witnessTab, "900");
        _witnessDelta = Field("Variation réputation", witnessTab, "-12");
        _witnessAllies = new CheckBox { Text = "Partager avec les alliés" };
        witnessTab.AddChild(_witnessFaction);
        witnessTab.AddChild(_witnessAllies);
        AddEditorActions(witnessTab, "Ajouter ou mettre à jour le témoin", SaveWitness, RemoveWitness);
        witnessTab.AddChild(new HSeparator());
        witnessTab.AddChild(StudioStyle.Label("AFFILIATIONS MULTIPLES", 11, StudioStyle.Accent));
        _membershipList = List(witnessTab, 84);
        _membershipActor = Field("Acteur", witnessTab, "hero");
        _membershipFaction = new OptionButton();
        _membershipRank = Field("Rang", witnessTab, "éclaireur");
        _membershipCover = Field("Couverture facultative", witnessTab, "Pèlerin des cendres");
        _membershipLoyalty = Field("Loyauté -100..100", witnessTab, "85");
        _membershipWeight = Field("Poids 0..1", witnessTab, "1");
        _membershipPrimary = new CheckBox { Text = "Affiliation principale" };
        _membershipExpiry = Field("Expiration ISO facultative", witnessTab, string.Empty);
        witnessTab.AddChild(_membershipFaction);
        witnessTab.AddChild(_membershipPrimary);
        AddEditorActions(witnessTab, "Ajouter ou mettre à jour l’affiliation", SaveMembership, RemoveMembership);

        var dynamicTab = Tab(tabs, "4 · Factions dynamiques");
        dynamicTab.AddChild(StudioStyle.Label(
            "Décision authorée et simulation serveur locale · aucun transport réseau n’est exécuté.",
            10, StudioStyle.Muted, true));
        _dynamicList = List(dynamicTab, 78);
        _dynamicFaction = new OptionButton();
        _dynamicKind = EnumOption<DynamicFactionKind>();
        _dynamicCreated = Field("Créée à (ISO)", dynamicTab, "2026-08-17T12:00:00Z");
        _dynamicExpiry = Field("Expiration ISO facultative", dynamicTab, "2030-08-01T00:00:00Z");
        _dynamicActive = new CheckBox { Text = "Active · réplication autoritaire requise" };
        _dynamicRevision = Field("Révision", dynamicTab, "1");
        AddOptionsRow(dynamicTab, "Faction / type", _dynamicFaction, _dynamicKind);
        dynamicTab.AddChild(_dynamicActive);
        AddEditorActions(dynamicTab, "Ajouter ou mettre à jour la faction dynamique", SaveDynamic, RemoveDynamic);
        dynamicTab.AddChild(new HSeparator());
        dynamicTab.AddChild(StudioStyle.Label("DIPLOMATIE DE LA FACTION SÉLECTIONNÉE", 11, StudioStyle.Accent));
        _diplomacyList = List(dynamicTab, 72);
        _diplomacyTarget = new OptionButton();
        _diplomacyStatus = EnumOption<FactionDiplomacyStatus>();
        _diplomacySince = Field("Depuis (ISO)", dynamicTab, "2026-08-17T12:00:00Z");
        _diplomacyExpiry = Field("Expiration ISO facultative", dynamicTab, string.Empty);
        _diplomacyCause = Field("Cause", dynamicTab, "trêve négociée");
        AddOptionsRow(dynamicTab, "Cible / statut", _diplomacyTarget, _diplomacyStatus);
        AddEditorActions(dynamicTab, "Ajouter ou mettre à jour la diplomatie", SaveDiplomacy, RemoveDiplomacy);

        var review = StudioStyle.Panel("FactionRuntimeReviewUx4B", StudioStyle.Surface, 9, 1);
        review.CustomMinimumSize = new Vector2(0, 142);
        var reviewRow = new HBoxContainer();
        review.AddChild(reviewRow);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(620, 126),
        };
        reviewRow.AddChild(_summary);
        _diagnostics = new VBoxContainer { CustomMinimumSize = new Vector2(360, 126) };
        reviewRow.AddChild(_diagnostics);
        root.AddChild(review);

        root.AddChild(StudioStyle.Label(
            "Aperçu sec : les services calculent réputation, rapports, affiliations et révisions ; aucune IA, aucun acteur et aucun réseau ne sont mutés.",
            9, StudioStyle.Muted, true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", primary: true, compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(118, 34);
            footer.AddChild(button);
        }
        root.AddChild(footer);

        ConnectEvents();
        SetEditingEnabled(false);
        RenderEmpty("Sélectionnez une faction pour charger les politiques vivantes du module.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null)
        {
            ReleaseEvidence();
        }
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession()
    {
        _session.CancelFactionRuntimeUx4B();
        _draft = _session.CreateFactionRuntimeDraftUx4B();
        _choices = _session.FactionChoicesUx4A;
        RebuildFactionOptions();
        Visible = true;
        _applied = false;
        _previewResult = null;
        if (_draft is null)
        {
            SetEditingEnabled(false);
            RenderEmpty("Deux factions actives au minimum sont requises pour cette surface.");
            return;
        }
        LoadCollections(_draft);
        _focusFaction.Select(0);
        _context.Text = "Sélectionnez une faction : aucune donnée n’est chargée automatiquement.";
        SetEditingEnabled(false);
        RenderEmpty("Contexte prêt · sélection explicite requise avant l’aperçu.");
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null && ReferenceEquals(_session, _sourceSession))
        {
            return;
        }
        _session.CancelFactionRuntimeUx4B();
        Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null)
        {
            ReleaseEvidence();
        }
        else
        {
            _session = _sourceSession;
            _sourceRestored = true;
        }
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void SelectFocus(long selected)
    {
        if (_updating || selected <= 0 || selected > _choices.Count)
        {
            SetEditingEnabled(false);
            return;
        }
        var choice = _choices[(int)selected - 1];
        _context.Text = $"{choice.Label} · politiques globales du module · identité technique protégée";
        SetEditingEnabled(true);
        InvalidatePreview();
    }

    private void LoadCollections(StudioFactionRuntimeDraftUx4B draft)
    {
        _updating = true;
        _personalConfigured.ButtonPressed = draft.PersonalConfigured;
        _advancedConfigured.ButtonPressed = draft.AdvancedConfigured;
        _advancedRequired.ButtonPressed = draft.RequireAdvancedRuntime;
        _reputationConfigured.ButtonPressed = draft.ReputationConfigured;
        _reputationRequired.ButtonPressed = draft.RequireReputationRuntime;
        _personal.Clear(); _personal.AddRange(draft.PersonalReputation);
        _transactions.Clear(); _transactions.AddRange(draft.ReputationTransactions);
        _witnesses.Clear(); _witnesses.AddRange(draft.WitnessPolicies);
        _memberships.Clear(); _memberships.AddRange(draft.Memberships);
        _dynamics.Clear();
        _dynamics.AddRange(draft.DynamicFactions.Select(item => item with { Diplomacy = item.Diplomacy.ToArray() }));
        _diplomacy.Clear();
        RebuildAllLists();
        _updating = false;
    }

    private StudioFactionRuntimeDraftUx4B? ReadDraft() => _draft is null ? null : _draft with
    {
        PersonalConfigured = _personalConfigured.ButtonPressed,
        PersonalReputation = _personal.ToArray(),
        AdvancedConfigured = _advancedConfigured.ButtonPressed,
        RequireAdvancedRuntime = _advancedRequired.ButtonPressed,
        WitnessPolicies = _witnesses.ToArray(),
        Memberships = _memberships.ToArray(),
        DynamicFactions = _dynamics.Select(item => item with { Diplomacy = item.Diplomacy.ToArray() }).ToArray(),
        ReputationConfigured = _reputationConfigured.ButtonPressed,
        RequireReputationRuntime = _reputationRequired.ButtonPressed,
        ReputationTransactions = _transactions.ToArray(),
    };

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null || !HasExplicitSelection)
        {
            RenderEmpty("Sélectionnez une faction avant de prévisualiser.");
            return;
        }
        var project = _session.Project;
        var history = _session.History;
        var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? [];
        var dirty = _session.IsDirty;
        var result = _session.PreviewFactionRuntimeUx4B(draft);
        _previewResult = result;
        var dryRun = project is not null && history is not null && ReferenceEquals(project, _session.Project) &&
                     ReferenceEquals(history, _session.History) && history.State.Revision == revision &&
                     history.Journal.SequenceEqual(journal) && _session.IsDirty == dirty;
        RenderPreview(result, dryRun);
        SetEditingEnabled(true);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            return;
        }
        var result = _session.ApplyApprovedFactionRuntimeUx4B(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            InvalidatePreview();
            return;
        }
        FactionChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoFactionRuntimeUx4B();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { FactionChanged?.Invoke(); ReloadAfterMutation(); }
    }

    private void Redo()
    {
        var result = _session.RedoFactionRuntimeUx4B();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { FactionChanged?.Invoke(); ReloadAfterMutation(); }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        _draft = _session.CreateFactionRuntimeDraftUx4B();
        if (_draft is not null)
        {
            LoadCollections(_draft);
        }
        _previewResult = null;
        RenderEmpty("Projet rechargé · prévisualisez avant toute nouvelle application.");
        _apply.Disabled = true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
    }

    private void SavePersonal()
    {
        if (!TryFaction(_personalSource, out var source) || !TryFaction(_personalTarget, out var target) ||
            !TryDouble(_personalDelta.Text, out var delta) || !TryOptionalTime(_personalExpiry.Text, out var expiry) ||
            string.IsNullOrWhiteSpace(_personalSubject.Text) || string.IsNullOrWhiteSpace(_personalCause.Text))
        {
            SetStatus("Réputation personnelle incomplète ou mal formée.", true); return;
        }
        var index = SelectedIndex(_personalList, _personal.Count);
        var id = index >= 0 ? _personal[index].Id : Guid.NewGuid();
        var value = new PersonalReputationOverlay(id, 1, "studio:ux4-b", _personalSubject.Text.Trim(),
            source, target, delta, expiry, _personalCause.Text.Trim(),
            (PersonalReputationSubjectKind)_personalKind.Selected);
        Upsert(_personal, index, value); RebuildPersonalList(); InvalidatePreview();
    }

    private void RemovePersonal() { RemoveSelected(_personalList, _personal); RebuildPersonalList(); InvalidatePreview(); }

    private void SaveTransaction()
    {
        if (!TryFaction(_transactionSource, out var source) || !TryFaction(_transactionTarget, out var target) ||
            !TryDouble(_transactionDelta.Text, out var delta) || !TryRequiredTime(_transactionDate.Text, out var at) ||
            string.IsNullOrWhiteSpace(_transactionScopeId.Text) || string.IsNullOrWhiteSpace(_transactionCause.Text))
        {
            SetStatus("Transaction globale incomplète ou mal formée.", true); return;
        }
        var index = SelectedIndex(_transactionList, _transactions.Count);
        var id = index >= 0 ? _transactions[index].Id : Guid.NewGuid();
        var value = new ReputationTransaction(id, 1, "studio:ux4-b", (ReputationScope)_transactionScope.Selected,
            _transactionScopeId.Text.Trim(), source, target, delta, _transactionCause.Text.Trim(), at);
        Upsert(_transactions, index, value); RebuildTransactionList(); InvalidatePreview();
    }

    private void RemoveTransaction() { RemoveSelected(_transactionList, _transactions); RebuildTransactionList(); InvalidatePreview(); }

    private void SaveWitness()
    {
        if (!TryFaction(_witnessFaction, out var faction) || !TryDouble(_witnessPerception.Text, out var perception) ||
            !TryDouble(_witnessEvidence.Text, out var evidence) || !TryDouble(_witnessRadius.Text, out var radius) ||
            !int.TryParse(_witnessMemory.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var memory) ||
            !TryDouble(_witnessDelta.Text, out var delta))
        {
            SetStatus("Politique de témoin incomplète ou mal formée.", true); return;
        }
        var index = SelectedIndex(_witnessList, _witnesses.Count);
        var id = index >= 0 ? _witnesses[index].Id : Guid.NewGuid();
        Upsert(_witnesses, index, new(id, faction, perception, evidence, radius, memory, delta,
            _witnessAllies.ButtonPressed));
        RebuildWitnessList(); InvalidatePreview();
    }

    private void RemoveWitness() { RemoveSelected(_witnessList, _witnesses); RebuildWitnessList(); InvalidatePreview(); }

    private void SaveMembership()
    {
        if (!TryFaction(_membershipFaction, out var faction) ||
            !TryDouble(_membershipLoyalty.Text, out var loyalty) || !TryDouble(_membershipWeight.Text, out var weight) ||
            !TryOptionalTime(_membershipExpiry.Text, out var expiry) || string.IsNullOrWhiteSpace(_membershipActor.Text) ||
            string.IsNullOrWhiteSpace(_membershipRank.Text))
        {
            SetStatus("Affiliation incomplète ou mal formée.", true); return;
        }
        var index = SelectedIndex(_membershipList, _memberships.Count);
        var id = index >= 0 ? _memberships[index].Id : Guid.NewGuid();
        Upsert(_memberships, index, new(id, 1, "studio:ux4-b", _membershipActor.Text.Trim(), faction,
            _membershipRank.Text.Trim(), Optional(_membershipCover.Text), loyalty, weight,
            _membershipPrimary.ButtonPressed, expiry));
        RebuildMembershipList(); InvalidatePreview();
    }

    private void RemoveMembership() { RemoveSelected(_membershipList, _memberships); RebuildMembershipList(); InvalidatePreview(); }

    private void SaveDynamic()
    {
        if (!TryFaction(_dynamicFaction, out var faction) || !TryRequiredTime(_dynamicCreated.Text, out var created) ||
            !TryOptionalTime(_dynamicExpiry.Text, out var expiry) ||
            !long.TryParse(_dynamicRevision.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision))
        {
            SetStatus("Faction dynamique incomplète ou mal formée.", true); return;
        }
        var index = SelectedIndex(_dynamicList, _dynamics.Count);
        var id = index >= 0 ? _dynamics[index].Id : Guid.NewGuid();
        var diplomacy = index >= 0 ? _diplomacy.ToArray() : Array.Empty<FactionDiplomacyState>();
        Upsert(_dynamics, index, new(id, 1, "studio:ux4-b", faction, (DynamicFactionKind)_dynamicKind.Selected,
            created, expiry, _dynamicActive.ButtonPressed, true, revision, diplomacy));
        RebuildDynamicList(); InvalidatePreview();
    }

    private void RemoveDynamic()
    {
        RemoveSelected(_dynamicList, _dynamics); _diplomacy.Clear(); RebuildDynamicList(); RebuildDiplomacyList();
        InvalidatePreview();
    }

    private void SaveDiplomacy()
    {
        var dynamicIndex = SelectedIndex(_dynamicList, _dynamics.Count);
        if (dynamicIndex < 0 || !TryFaction(_diplomacyTarget, out var target) ||
            !TryRequiredTime(_diplomacySince.Text, out var since) ||
            !TryOptionalTime(_diplomacyExpiry.Text, out var expiry) || string.IsNullOrWhiteSpace(_diplomacyCause.Text))
        {
            SetStatus("Sélectionnez une faction dynamique puis complétez sa diplomatie.", true); return;
        }
        var index = SelectedIndex(_diplomacyList, _diplomacy.Count);
        var id = index >= 0 ? _diplomacy[index].Id : Guid.NewGuid();
        Upsert(_diplomacy, index, new(id, target, (FactionDiplomacyStatus)_diplomacyStatus.Selected,
            since, expiry, _diplomacyCause.Text.Trim()));
        _dynamics[dynamicIndex] = _dynamics[dynamicIndex] with { Diplomacy = _diplomacy.ToArray() };
        RebuildDiplomacyList(); RebuildDynamicList(dynamicIndex); InvalidatePreview();
    }

    private void RemoveDiplomacy()
    {
        var dynamicIndex = SelectedIndex(_dynamicList, _dynamics.Count);
        RemoveSelected(_diplomacyList, _diplomacy);
        if (dynamicIndex >= 0) _dynamics[dynamicIndex] = _dynamics[dynamicIndex] with { Diplomacy = _diplomacy.ToArray() };
        RebuildDiplomacyList(); RebuildDynamicList(dynamicIndex); InvalidatePreview();
    }

    private void SelectPersonal(long index)
    {
        if (_updating || index < 0 || index >= _personal.Count) return;
        var item = _personal[(int)index];
        _personalKind.Select((int)item.SubjectKind); _personalSubject.Text = item.SubjectId;
        SelectFaction(_personalSource, item.SourceFactionId); SelectFaction(_personalTarget, item.TargetFactionId);
        _personalDelta.Text = Number(item.Delta); _personalExpiry.Text = Time(item.ExpiresAt); _personalCause.Text = item.Cause;
    }

    private void SelectTransaction(long index)
    {
        if (_updating || index < 0 || index >= _transactions.Count) return;
        var item = _transactions[(int)index];
        _transactionScope.Select((int)item.Scope); _transactionScopeId.Text = item.ScopeId;
        SelectFaction(_transactionSource, item.SourceFactionId); SelectFaction(_transactionTarget, item.TargetFactionId);
        _transactionDelta.Text = Number(item.Delta); _transactionCause.Text = item.Cause;
        _transactionDate.Text = Time(item.OccurredAt);
    }

    private void SelectWitness(long index)
    {
        if (_updating || index < 0 || index >= _witnesses.Count) return;
        var item = _witnesses[(int)index]; SelectFaction(_witnessFaction, item.FactionId);
        _witnessPerception.Text = Number(item.MinimumPerception); _witnessEvidence.Text = Number(item.MinimumEvidence);
        _witnessRadius.Text = Number(item.CommunicationRadiusMeters);
        _witnessMemory.Text = item.MemorySeconds.ToString(CultureInfo.InvariantCulture);
        _witnessDelta.Text = Number(item.ReputationDelta); _witnessAllies.ButtonPressed = item.ShareWithAllies;
    }

    private void SelectMembership(long index)
    {
        if (_updating || index < 0 || index >= _memberships.Count) return;
        var item = _memberships[(int)index]; _membershipActor.Text = item.ActorId;
        SelectFaction(_membershipFaction, item.FactionId); _membershipRank.Text = item.Rank;
        _membershipCover.Text = item.CoverIdentity ?? string.Empty; _membershipLoyalty.Text = Number(item.Loyalty);
        _membershipWeight.Text = Number(item.Weight); _membershipPrimary.ButtonPressed = item.IsPrimary;
        _membershipExpiry.Text = Time(item.ExpiresAt);
    }

    private void SelectDynamic(long index)
    {
        if (_updating || index < 0 || index >= _dynamics.Count) return;
        var item = _dynamics[(int)index]; SelectFaction(_dynamicFaction, item.FactionId);
        _dynamicKind.Select((int)item.Kind); _dynamicCreated.Text = Time(item.CreatedAt);
        _dynamicExpiry.Text = Time(item.ExpiresAt); _dynamicActive.ButtonPressed = item.Active;
        _dynamicRevision.Text = item.Revision.ToString(CultureInfo.InvariantCulture);
        _diplomacy.Clear(); _diplomacy.AddRange(item.Diplomacy); RebuildDiplomacyList();
    }

    private void SelectDiplomacy(long index)
    {
        if (_updating || index < 0 || index >= _diplomacy.Count) return;
        var item = _diplomacy[(int)index]; SelectFaction(_diplomacyTarget, item.TargetFactionId);
        _diplomacyStatus.Select((int)item.Status); _diplomacySince.Text = Time(item.Since);
        _diplomacyExpiry.Text = Time(item.ExpiresAt); _diplomacyCause.Text = item.Cause;
    }

    private void RebuildAllLists()
    {
        RebuildPersonalList(); RebuildTransactionList(); RebuildWitnessList(); RebuildMembershipList();
        RebuildDynamicList(); RebuildDiplomacyList();
    }

    private void RebuildPersonalList() => Rebuild(_personalList, _personal.Select(item =>
        $"{item.SubjectKind} · {item.SubjectId} · {FactionName(item.SourceFactionId)} → {FactionName(item.TargetFactionId)} · {Number(item.Delta)}"));
    private void RebuildTransactionList() => Rebuild(_transactionList, _transactions.Select(item =>
        $"{item.Scope}/{item.ScopeId} · {FactionName(item.SourceFactionId)} → {FactionName(item.TargetFactionId)} · {Number(item.Delta)}"));
    private void RebuildWitnessList() => Rebuild(_witnessList, _witnesses.Select(item =>
        $"{FactionName(item.FactionId)} · perception {Number(item.MinimumPerception)} · preuve {Number(item.MinimumEvidence)} · {item.MemorySeconds}s"));
    private void RebuildMembershipList() => Rebuild(_membershipList, _memberships.Select(item =>
        $"{item.ActorId} · {FactionName(item.FactionId)} · {item.Rank} · {(item.IsPrimary ? "principale" : "secondaire")}"));
    private void RebuildDynamicList(int select = -1)
    {
        Rebuild(_dynamicList, _dynamics.Select(item =>
            $"{FactionName(item.FactionId)} · {item.Kind} · {(item.Active ? "active" : "inactive")} · rév. {item.Revision} · {item.Diplomacy.Count} état(s)"));
        if (select >= 0 && select < _dynamicList.ItemCount) _dynamicList.Select(select);
    }
    private void RebuildDiplomacyList() => Rebuild(_diplomacyList, _diplomacy.Select(item =>
        $"{FactionName(item.TargetFactionId)} · {item.Status} · {item.Cause}"));

    private void RebuildFactionOptions()
    {
        _updating = true;
        _focusFaction.Clear(); _focusFaction.AddItem("Choisir explicitement…");
        foreach (var choice in _choices) _focusFaction.AddItem(choice.Label);
        foreach (var option in new[] { _personalSource, _personalTarget, _transactionSource, _transactionTarget,
                     _witnessFaction, _membershipFaction, _dynamicFaction, _diplomacyTarget })
        {
            option.Clear();
            foreach (var choice in _choices) option.AddItem(choice.Label);
        }
        _updating = false;
    }

    private void RenderPreview(StudioFactionRuntimePreviewUx4B result, bool dryRun)
    {
        _summary.Text =
            $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "CONFIGURATION REFUSÉE")}[/b]\n" +
            $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
            $"[color=#b8d9ff]Réputation personnelle[/color] · {result.PersonalSummary}\n" +
            $"[color=#b8d9ff]Réputation globale[/color] · {result.ReputationSummary}\n" +
            $"[color=#b8d9ff]Témoins[/color] · {result.WitnessSummary}\n" +
            $"[color=#b8d9ff]Affiliations[/color] · {result.MembershipSummary}\n" +
            $"[color=#b8d9ff]Factions dynamiques[/color] · {result.DynamicSummary}\n" +
            $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        if (result.Diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success, true));
        }
        else
        {
            foreach (var item in result.Diagnostics.Take(5))
                _diagnostics.AddChild(StudioStyle.Label($"{item.Code}\n{item.HumanMessage}", 9, StudioStyle.Danger, true));
        }
        _apply.Disabled = !result.CanApply;
    }

    private void RenderEmpty(string message)
    {
        _summary.Text = $"[color=#b8a8ce]{message}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _diagnostics.AddChild(StudioStyle.Label("Aucune écriture sans aperçu approuvé.", 10, StudioStyle.Muted, true));
        _apply.Disabled = true;
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelFactionRuntimeUx4B(); _previewResult = null; _apply.Disabled = true;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null;
    }

    private void ConnectEvents()
    {
        _focusFaction.ItemSelected += SelectFocus;
        foreach (var check in new[] { _personalConfigured, _advancedConfigured, _advancedRequired,
                     _reputationConfigured, _reputationRequired }) check.Toggled += _ => InvalidatePreview();
        _personalList.ItemSelected += SelectPersonal; _transactionList.ItemSelected += SelectTransaction;
        _witnessList.ItemSelected += SelectWitness; _membershipList.ItemSelected += SelectMembership;
        _dynamicList.ItemSelected += SelectDynamic; _diplomacyList.ItemSelected += SelectDiplomacy;
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo; _redo.Pressed += Redo;
        _save.Pressed += Save;
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode; _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence())
        {
            _lastFailure = "release_previous"; return false;
        }
        if (!TryOpenEvidence()) { _lastFailure = _lastFailure == "none" ? "open" : _lastFailure; return false; }
        var index = _choices.ToList().FindIndex(item => item.Slug == "ash-watch");
        if (index < 0) index = 0;
        _focusFaction.Select(index + 1); SelectFocus(index + 1);
        if (_draft is null || _personal.Count == 0 || _transactions.Count == 0 || _witnesses.Count == 0 ||
            _memberships.Count < 2 || _dynamics.Count == 0)
        {
            _lastFailure = "fixture_incomplete"; Close(); return false;
        }
        _personal[0] = _personal[0] with { Delta = _personal[0].Delta - 1, Cause = "aperçu UX4-B isolé" };
        _transactions[0] = _transactions[0] with { Delta = _transactions[0].Delta + 1 };
        _witnesses[0] = _witnesses[0] with { MinimumEvidence = Math.Min(1, _witnesses[0].MinimumEvidence + 0.01) };
        RebuildAllLists();
        _dynamicList.Select(0);
        SelectDynamic(0);
        var history = _session.History!; var project = _session.Project!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealCrypt ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"copie isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    ShowsPersonalReputation && ShowsGlobalReputation && ShowsWitnesses && ShowsMemberships &&
                    ShowsDynamicFactions && UsesIsolatedEvidence && EvidenceSourceUntouched && (_usesRealCrypt ^ _usesFallback);
        if (!ready)
        {
            _lastFailure =
                $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:" +
                $"personal={ShowsPersonalReputation}:global={ShowsGlobalReputation}:witness={ShowsWitnesses}:" +
                $"memberships={ShowsMemberships}:dynamic={ShowsDynamicFactions}:isolated={UsesIsolatedEvidence}:" +
                $"source={EvidenceSourceUntouched}:origin={_usesRealCrypt ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4B_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-B isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-b-faction-runtime") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession)) return false;
            var transaction = _session; var original = transaction.Project; var history = transaction.History;
            if (original is null || history is null || !CanonicalPreviewReady) return false;
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
                            ProjectJsonSerializer.Serialize(reopened.Project!) == ProjectJsonSerializer.Serialize(redoneProject!);
            }
            var sourceUntouched = SourceMatches(snapshot);
            reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4B_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreFactionRuntimeApprovalUx4B(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4BEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4b-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            {
                session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false;
            }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root; _session = session;
            _usesRealCrypt = evidence.UsesRealCryptSource; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false;
            OpenBoundSession();
            return _draft is not null && SourceMatches(snapshot) && (_usesRealCrypt ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name;
            session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession;
            _sourceRestored = true; return false;
        }
    }

    private StudioFactionRuntimeEvidenceSourceSnapshotUx4B? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureFactionRuntimeApprovalUx4B());
    }

    private bool SourceMatches(StudioFactionRuntimeEvidenceSourceSnapshotUx4B snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureFactionRuntimeApprovalUx4B() == snapshot.Approval;
    }

    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot;
        _evidenceSession = null; _evidenceRoot = null; _sourceSnapshot = null; _session = _sourceSession;
        _usesRealCrypt = false; _usesFallback = false; _previewDryRun = false;
        var released = true;
        try { session?.Close(); } catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root);
        if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased; _sourceRestored = true;
        return _resourcesReleased;
    }

    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4b-evidence-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
    private string FactionName(Guid id) => _choices.FirstOrDefault(item => item.FactionId == id)?.Label ?? "Faction héritée protégée";
    private bool TryFaction(OptionButton option, out Guid id)
    {
        var index = option.Selected; id = index >= 0 && index < _choices.Count ? _choices[index].FactionId : Guid.Empty;
        return id != Guid.Empty;
    }
    private void SelectFaction(OptionButton option, Guid id)
    {
        var index = _choices.ToList().FindIndex(item => item.FactionId == id); if (index >= 0) option.Select(index);
    }
    private static int SelectedIndex(ItemList list, int count) { var selected = list.GetSelectedItems(); return selected.Length == 1 && selected[0] < count ? selected[0] : -1; }
    private static void Upsert<T>(List<T> list, int index, T value) { if (index >= 0) list[index] = value; else list.Add(value); }
    private static void RemoveSelected<T>(ItemList view, List<T> list) { var index = SelectedIndex(view, list.Count); if (index >= 0) list.RemoveAt(index); }
    private static void Rebuild(ItemList list, IEnumerable<string> labels) { list.Clear(); foreach (var label in labels) list.AddItem(label); }
    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool TryDouble(string value, out double result) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    private static bool TryRequiredTime(string value, out DateTimeOffset result) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
    private static bool TryOptionalTime(string value, out DateTimeOffset? result) { if (string.IsNullOrWhiteSpace(value)) { result = null; return true; } var ok = TryRequiredTime(value, out var parsed); result = ok ? parsed : null; return ok; }
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Time(DateTimeOffset? value) => value?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Time(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static bool ContainsGuid(string value) => value.Split([' ', '/', ':', '\'', '"', '(', ')', '[', ']', ',', '.', ';', '='], StringSplitOptions.RemoveEmptyEntries).Any(item => Guid.TryParse(item, out _));

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6); scroll.AddChild(body); tabs.AddChild(scroll); return body;
    }
    private static ItemList List(Node parent, float height) { var list = new ItemList { CustomMinimumSize = new Vector2(0, height), SelectMode = ItemList.SelectModeEnum.Single }; parent.AddChild(list); return list; }
    private static LineEdit Field(string label, Node parent, string placeholder) { parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted)); var field = new LineEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, 30) }; parent.AddChild(field); return field; }
    private static OptionButton EnumOption<T>() where T : struct, Enum { var option = new OptionButton(); foreach (var value in Enum.GetValues<T>()) option.AddItem(value.ToString()); return option; }
    private static void AddOptionsRow(Node parent, string label, params OptionButton[] options) { parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted)); var row = new HBoxContainer(); foreach (var option in options) { option.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(option); } parent.AddChild(row); }
    private static void AddEditorActions(Node parent, string saveText, Action save, Action remove) { var row = new HBoxContainer(); var saveButton = StudioStyle.Button(saveText, compact: true); var removeButton = StudioStyle.Button("Retirer la ligne", compact: true); saveButton.Pressed += save; removeButton.Pressed += remove; row.AddChild(saveButton); row.AddChild(removeButton); parent.AddChild(row); }
    private static IEnumerable<string> VisibleText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case ItemList list when list.Visible:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case TabContainer tabs when tabs.Visible:
                    for (var index = 0; index < tabs.GetTabCount(); index++) yield return tabs.GetTabTitle(index);
                    break;
            }
            foreach (var text in VisibleText(child)) yield return text;
        }
    }
}
