using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCompanionAuthoringOverlayUx4U : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCompanionEvidenceSourceSnapshotUx4U? _sourceSnapshot;
    private Guid? _evidenceCompanionId;
    private bool _usesRealModule;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _applied;
    private bool _updating;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCompanionChoiceUx4U> _choices = [];
    private readonly Dictionary<string, BlueprintId> _creatureReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _dialogueReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _questReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BlueprintId> _itemReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AreaId> _areaReferences = new(StringComparer.Ordinal);
    private StudioCompanionDraftUx4U? _draft;
    private StudioCompanionPreviewUx4U? _previewResult;

    private readonly OptionButton _companion;
    private readonly OptionButton _creature;
    private readonly LineEdit _dialoguePolicy;
    private readonly LineEdit _recruitable;
    private readonly LineEdit _preRecruitment;
    private readonly LineEdit _ownerDialogue;
    private readonly LineEdit _membership;
    private readonly LineEdit _partyPolicy;
    private readonly LineEdit _ownership;
    private readonly LineEdit _movement;
    private readonly LineEdit _tactical;
    private readonly LineEdit _ai;
    private readonly LineEdit _formation;
    private readonly LineEdit _equipmentControl;
    private readonly TextEdit _equipmentItems;
    private readonly LineEdit _validationEquipment;
    private readonly LineEdit _progression;
    private readonly LineEdit _relationship;
    private readonly TextEdit _banterParticipants;
    private readonly LineEdit _banter;
    private readonly LineEdit _personalQuest;
    private readonly LineEdit _death;
    private readonly LineEdit _traps;
    private readonly LineEdit _abilities;
    private readonly LineEdit _directControl;
    private readonly LineEdit _partySplit;
    private readonly LineEdit _cohesion;
    private readonly LineEdit _hud;
    private readonly LineEdit _validationRecruitment;
    private readonly LineEdit _validationOwnership;
    private readonly LineEdit _validationCommands;
    private readonly LineEdit _validationDeath;
    private readonly TextEdit _validationDialogues;
    private readonly TextEdit _crossZoneAreas;
    private readonly LineEdit _crossZone;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? CompanionChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int CompanionChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _companion.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsRecruitment =>
        _draft?.Profile.Foundation is { Recruitable.Enabled: true } foundation &&
        foundation.PreRecruitment.DialogueId != Guid.Empty;
    internal bool ShowsControl => _draft?.Profile.Control is
    { Movement.CommandKinds.Count: 6, Tactical.CommandKinds.Count: 6 };
    internal bool ShowsNarrative => _draft?.Profile is
    { Banter.RequiredParticipants.Count: > 0, PersonalQuest: not null };
    internal bool ShowsExperience => _draft?.Profile.Experience?.Hud is
    { Keyboard: true, Gamepad: true, ScreenReaderLabels: true };
    internal bool ShowsValidation => _draft?.Profile.Validation is
    { CommandIds.Count: > 0, DialogueContexts.Count: >= 2 };
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Auteur de compagnon complet", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Recrutement et dialogues", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Commandes et IA", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Équipement et progression", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Récit et cycle de vie", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Groupe et validation", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModule;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCompanionAuthoringOverlayUx4U(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioCompanionAuthoringOverlayUx4U";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Visible = false; MouseFilter = MouseFilterEnum.Stop;
        var veil = new ColorRect { Color = new Color(0, 0, 0, .74f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(veil);
        var dialog = StudioStyle.Panel("CompanionAuthoringDialogUx4U", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, margin: 24); AddChild(dialog);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 7); dialog.AddChild(root);
        var header = new HBoxContainer(); var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("AUTEUR DE COMPAGNON COMPLET · UX4-U", 14, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label(
            "Une commande catalogue · aperçu sec · les identités techniques restent internes.", 9,
            StudioStyle.Muted));
        heading.AddChild(StudioStyle.Label(
            "Recrutement et dialogues · Commandes et IA · Équipement et progression · Récit et cycle de vie · Groupe et validation",
            8, StudioStyle.Muted, wrap: true));
        header.AddChild(heading); var close = StudioStyle.Button("Fermer", compact: true);
        close.CustomMinimumSize = new Vector2(76, 34); close.Pressed += Close; header.AddChild(close); root.AddChild(header);
        _companion = Choice(root, "Compagnon à éditer ou création explicite");

        var tabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 350)
        };
        var recruitTab = Tab(tabs, "Recrutement et dialogues");
        _creature = Choice(recruitTab, "Créature recrutée");
        _dialoguePolicy = Field(recruitTab,
            "Dialogue général · dialogue humain | maître | groupe | tiers | MJ | hostile", string.Empty);
        _recruitable = Field(recruitTab,
            "Recrutable · actif | conditions | coût | monnaie | réputation | conséquences | dialogue | script | chemin .forge",
            string.Empty);
        _preRecruitment = Field(recruitTab,
            "Avant recrutement · dialogue humain | contexte | conditions | nœud offre | refus | conséquences", string.Empty);
        _ownerDialogue = Field(recruitTab,
            "Dialogue maître · dialogue humain | contexte | commandes | relation | équipement | progression | départ", string.Empty);
        _membership = Field(recruitTab,
            "Recruter/renvoyer · capacité | propriété | inventaire | position | persistance | inventaire | ancre | atomique",
            string.Empty);
        _partyPolicy = Field(recruitTab,
            "Capacité groupe · compagnons | invocations | familiers | alliés | limite serveur | rôles", string.Empty);

        var controlTab = Tab(tabs, "Commandes et IA");
        _ownership = Field(controlTab,
            "Propriété · type | contrôleurs | transférable | cibles autorisées | révision", string.Empty);
        _movement = Field(controlTab,
            "Déplacement · six ordres | formation | requête waypoint | priorité min | max", string.Empty);
        _tactical = Field(controlTab,
            "Combat · six ordres | quatre postures | capacités | priorité min | max", string.Empty);
        _ai = Field(controlTab,
            "IA autonome · autonomie | risque | ressources | tirs alliés | rôle | distance | tags capacités | diff | autorisation",
            string.Empty);
        _formation = Field(controlTab,
            "Formation · identifiant | place | espacement | obstacles | intérieur | synchronisation", string.Empty);

        var equipmentTab = Tab(tabs, "Équipement et progression");
        _equipmentControl = Field(equipmentTab,
            "Équipement · permission | comparaison | propriétaire inventaire", string.Empty);
        _equipmentItems = Multi(equipmentTab,
            "Loadout suggéré · slot | objet humain | verrouillé", 92);
        _validationEquipment = Field(equipmentTab,
            "Contrat équipement · inventaire | politique | slots", string.Empty);
        _progression = Field(equipmentTab,
            "Progression · mode | niveau | maximum | aperçu | respec | jalons", string.Empty);
        _relationship = Field(equipmentTab,
            "Relation · affinité | confiance | peur | loyauté | tags | historique", string.Empty);

        var narrativeTab = Tab(tabs, "Récit et cycle de vie");
        _banterParticipants = Multi(narrativeTab, "Participants aux interjections · une créature humaine par ligne", 72);
        _banter = Field(narrativeTab,
            "Banter · priorité | cooldown s | lieux | interruptible | dialogue humain", string.Empty);
        _personalQuest = Field(narrativeTab,
            "Quête personnelle · active | quête humaine | disponibilité | départ | loyauté min | conséquence", string.Empty);
        _death = Field(narrativeTab,
            "Mort · mode | durée inconscience | ressource résurrection | remplaçant | mémorial | drop inventaire", string.Empty);
        _traps = Field(narrativeTab,
            "Pièges · détecter | désarmer | risque max | sens | compétence | ordre joueur requis", string.Empty);
        _abilities = Field(narrativeTab,
            "Sorts/capacités · autorisées | réservées | seuil santé | cible | override joueur", string.Empty);
        _directControl = Field(narrativeTab,
            "Contrôle direct · actif | possession | changement personnage | retour IA | permission multi", string.Empty);

        var partyTab = Tab(tabs, "Groupe et validation");
        _partySplit = Field(partyTab,
            "Séparation/réunion · suivre maître | regroupement | instance | quêtes synchronisées | délai max", string.Empty);
        _cohesion = Field(partyTab,
            "Rattrapage · seuil nav | portail | téléport | suivre portails | téléport sûr | tags ancres", string.Empty);
        _hud = Field(partyTab,
            "Interface groupe · portraits | santé | ressources | statuts | ordres | réordre | clavier | manette | lecteur | contraste | ressources",
            string.Empty);
        _validationRecruitment = Field(partyTab,
            "Validation recrutement · dialogue | script | chemin | monnaie | coût | réputation", string.Empty);
        _validationOwnership = Field(partyTab,
            "Validation propriété · type | autorité | transfert", string.Empty);
        _validationCommands = Field(partyTab, "Commandes autorisées complètes", string.Empty);
        _validationDeath = Field(partyTab,
            "Validation mort · politique | mode | récupération s", string.Empty);
        _validationDialogues = Multi(partyTab,
            "Contextes dialogue · contexte | dialogue humain | binding", 82);
        _crossZoneAreas = Multi(partyTab, "Zones autorisées · une zone humaine par ligne", 72);
        _crossZone = Field(partyTab,
            "Navigation inter-zones · active | suivre portails | fallback sûr | tag ancre | timeout s", string.Empty);
        partyTab.AddChild(StudioStyle.Label(
            "L’aperçu évalue les contrats et plans en mémoire. Il ne déplace aucun compagnon et ne joue aucun dialogue.",
            9, StudioStyle.Muted, wrap: true));
        tabs.CurrentTab = 0; root.AddChild(tabs);

        _diagnostics = new VBoxContainer(); root.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 128),
            FitContent = false,
            SelectionEnabled = true
        }; root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer remplace le compagnon dans le catalogue en une commande annulable.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer(); _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); ClearControls(); SetEditingEnabled(false);
    }

    internal void Open()
    { if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence(); _session = _sourceSession; OpenBound(); }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBound(); }
    internal void HideSourceSurfaceAfterSmoke() { Visible = false; }
    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null && ReferenceEquals(_session, _sourceSession)) return;
        _session.CancelCompanionUx4U(); _draft = null; _previewResult = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBound()
    {
        _session.CancelCompanionUx4U(); _choices = _session.CompanionChoicesUx4U; _updating = true; _companion.Clear();
        _companion.AddItem("Choisir un compagnon…"); _companion.AddItem("Créer un nouveau compagnon…");
        foreach (var choice in _choices) _companion.AddItem(choice.Label); _companion.Select(0); _updating = false;
        ClearControls(); Visible = true; SetEditingEnabled(false);
    }
    private void SelectionChanged(long _)
    {
        if (_updating) return; _session.CancelCompanionUx4U(); _previewResult = null; _apply.Disabled = true;
        if (_companion.Selected <= 0) { _draft = null; ClearControls(); SetEditingEnabled(false); return; }
        _draft = _companion.Selected == 1 ? _session.CreateCompanionDraftUx4U() :
            _session.CreateCompanionDraftUx4U(_choices[_companion.Selected - 2].CompanionId);
        if (_draft is null)
        { ClearControls(); SetEditingEnabled(false); RenderEmpty("Créature, dialogue, quête, objet ou zone manquante."); return; }
        _draft = _session.CompleteCompanionDraftUx4U(_draft); Populate(_draft); SetEditingEnabled(true);
        RenderEmpty("Complétez les cinq onglets puis prévisualisez le catalogue compagnon.");
    }

    private void Populate(StudioCompanionDraftUx4U draft)
    {
        _updating = true; _creatureReferences.Clear(); _dialogueReferences.Clear(); _questReferences.Clear();
        _itemReferences.Clear(); _areaReferences.Clear(); _creature.Clear();
        foreach (var item in _session.CompanionBlueprintChoicesUx4U)
        { _creatureReferences[item.Label] = item.BlueprintId; _creature.AddItem(item.Label); }
        foreach (var item in _session.CompanionDialogueChoicesUx4U) _dialogueReferences[item.Label] = item.DialogueId;
        foreach (var item in _session.CompanionQuestChoicesUx4U) _questReferences[item.Label] = item.QuestId;
        foreach (var item in _session.CompanionItemChoicesUx4U) _itemReferences[item.Label] = item.BlueprintId;
        foreach (var item in _session.CompanionAreaChoicesUx4U) _areaReferences[item.Label] = item.AreaId;
        var profile = draft.Profile; var creatureLabel = CreatureReferenceLabel(profile.BlueprintId);
        if (!_creatureReferences.ContainsKey(creatureLabel)) _creature.AddItem(creatureLabel);
        for (var index = 0; index < _creature.ItemCount; index++)
            if (string.Equals(_creature.GetItemText(index), creatureLabel, StringComparison.Ordinal))
                _creature.Select(index);
        _dialoguePolicy.Text = Join(DialogueReferenceLabel(profile.Dialogue.DialogueId),
            profile.Dialogue.OwnerBehavior, profile.Dialogue.PartyBehavior, profile.Dialogue.OutsiderBehavior,
            profile.Dialogue.GameMasterBehavior, profile.Dialogue.HostileBehavior);
        var foundation = profile.Foundation!; var recruitable = foundation.Recruitable;
        _recruitable.Text = Join(recruitable.Enabled, Csv(recruitable.Conditions), recruitable.Cost,
            recruitable.CurrencyId, recruitable.MinimumReputation, Csv(recruitable.ConsequenceIds),
            recruitable.AllowDialogue, recruitable.AllowScript, recruitable.ScriptId);
        var pre = foundation.PreRecruitment;
        _preRecruitment.Text = Join(DialogueReferenceLabel(pre.DialogueId), pre.ContextId,
            Csv(pre.Conditions), pre.OfferNodeId, pre.RefusalNodeId, Csv(pre.ConsequenceIds));
        var owner = foundation.OwnerDialogue;
        _ownerDialogue.Text = Join(DialogueReferenceLabel(owner.DialogueId), owner.ContextId,
            Csv(owner.CommandIds), owner.ExposeRelationship, owner.ExposeEquipment, owner.ExposeProgression,
            owner.DepartureActionId);
        var membership = foundation.Membership;
        _membership.Text = Join(membership.EnforcePartyCapacity, membership.TransferOwnership,
            membership.TransferInventory, membership.PreserveDismissPosition, membership.Persistence,
            membership.InventoryId, membership.DismissAnchorTag, membership.Atomic);
        var party = foundation.Party;
        _partyPolicy.Text = Join(party.CompanionSlots, party.SummonSlots, party.PetSlots,
            party.TemporaryAllySlots, party.ServerActorLimit, Pairs(party.RoleLimits));
        var control = profile.Control!; var ownership = control.Ownership;
        _ownership.Text = Join(ownership.Kind, Csv(ownership.ControllerIds), ownership.TransferAllowed,
            Csv(ownership.AllowedTargetKinds), ownership.Revision);
        _movement.Text = Join(Csv(control.Movement.CommandKinds), control.Movement.DefaultFormationId,
            control.Movement.WaypointQuery, control.Movement.MinimumPriority, control.Movement.MaximumPriority);
        _tactical.Text = Join(Csv(control.Tactical.CommandKinds), Csv(control.Tactical.Stances),
            Csv(control.Tactical.AbilityIds), control.Tactical.MinimumPriority, control.Tactical.MaximumPriority);
        _ai.Text = Join(control.Ai.Autonomy, control.Ai.RiskTolerance, control.Ai.ResourceUsage,
            control.Ai.AllowFriendlyFire, control.Ai.Role, control.Ai.FollowDistance,
            Csv(control.Ai.AllowedAbilityTags), control.Ai.SemanticDiff, control.Ai.AuthorizationLevel);
        _formation.Text = Join(profile.Formation.FormationId, profile.Formation.Slot, profile.Formation.Spacing,
            profile.Formation.AdaptToObstacles, profile.Formation.IndoorMode,
            profile.Formation.NetworkSynchronized);
        _equipmentControl.Text = Join(control.Equipment.Permission, control.Equipment.AllowCompare,
            control.Equipment.InventoryOwnerId);
        _equipmentItems.Text = string.Join('\n', control.Equipment.SuggestedLoadout.Select(item =>
            Join(item.Key, ItemReferenceLabel(BlueprintId.From(item.Value)),
                control.Equipment.LockedItemIds.Contains(item.Value))));
        _validationEquipment.Text = Join(profile.Validation!.Equipment.InventoryId,
            profile.Validation.Equipment.EquipmentPolicyId, Csv(profile.Validation.Equipment.Slots));
        _progression.Text = Join(profile.Progression.Mode, profile.Progression.CurrentLevel,
            profile.Progression.MaximumLevel, profile.Progression.PreviewEnabled, profile.Progression.AllowRespec,
            string.Join(',', profile.Progression.Milestones));
        _relationship.Text = Join(profile.Relationship.Affinity, profile.Relationship.Trust,
            profile.Relationship.Fear, profile.Relationship.Loyalty, Csv(profile.Relationship.Tags),
            Csv(profile.Relationship.History));
        _banterParticipants.Text = string.Join('\n', profile.Banter.RequiredParticipants.Select(CreatureReferenceLabel));
        _banter.Text = Join(profile.Banter.Priority, profile.Banter.CooldownSeconds,
            Csv(profile.Banter.Locations), profile.Banter.Interruptible,
            DialogueReferenceLabel(profile.Banter.DialogueId));
        _personalQuest.Text = profile.PersonalQuest is null ? "false |  |  |  | 0 | " : Join(true,
            QuestReferenceLabel(profile.PersonalQuest.QuestId), profile.PersonalQuest.AvailabilityCondition,
            profile.PersonalQuest.DepartureState, profile.PersonalQuest.MinimumLoyalty,
            profile.PersonalQuest.PartyConsequence);
        var death = profile.Experience!.Death;
        _death.Text = Join(death.Mode, death.DownedDurationSeconds, death.ResurrectionResourceId ?? string.Empty,
            death.ReplacementCreatureId ?? string.Empty, death.MemorialId ?? string.Empty, death.DropInventory);
        _traps.Text = Join(profile.Traps.Detect, profile.Traps.Disarm, profile.Traps.MaximumRisk,
            profile.Traps.RequiredSense, profile.Traps.SkillId, profile.Traps.RequirePlayerCommand);
        _abilities.Text = Join(Csv(profile.Abilities.Allowed), Csv(profile.Abilities.Reserved),
            profile.Abilities.HealthThreshold, profile.Abilities.TargetRule, profile.Abilities.AllowPlayerOverrides);
        _directControl.Text = Join(profile.DirectControl.Enabled, profile.DirectControl.AllowPossession,
            profile.DirectControl.AllowCharacterSwitch, profile.DirectControl.ReturnToAi,
            profile.DirectControl.MultiplayerPermission);
        _partySplit.Text = Join(profile.PartySplit.FollowOwner, profile.PartySplit.AllowRegroupRequest,
            profile.PartySplit.InstanceSelection, profile.PartySplit.SynchronizeQuests,
            profile.PartySplit.MaximumSeparationSeconds);
        var cohesion = profile.Experience.Cohesion;
        _cohesion.Text = Join(cohesion.NavigateThreshold, cohesion.PortalThreshold,
            cohesion.TeleportThreshold, cohesion.FollowPortals, cohesion.AllowSafeTeleport,
            Csv(cohesion.SafeAnchorTags));
        var hud = profile.Experience.Hud;
        _hud.Text = Join(hud.Portraits, hud.Health, hud.Resources, hud.Statuses, hud.QuickCommands,
            hud.Reorder, hud.Keyboard, hud.Gamepad, hud.ScreenReaderLabels, hud.HighContrast,
            Csv(hud.VisibleResourceIds));
        var validation = profile.Validation;
        _validationRecruitment.Text = Join(validation.Recruitment.AllowDialogue,
            validation.Recruitment.AllowScript, validation.Recruitment.ScriptId,
            validation.Recruitment.CurrencyId, validation.Recruitment.Cost,
            validation.Recruitment.MinimumReputation);
        _validationOwnership.Text = Join(validation.Ownership.Kind, validation.Ownership.Authority,
            validation.Ownership.TransferAllowed);
        _validationCommands.Text = Csv(validation.CommandIds);
        _validationDeath.Text = Join(validation.Death.PolicyId, validation.Death.Mode,
            validation.Death.RecoverySeconds);
        _validationDialogues.Text = string.Join('\n', validation.DialogueContexts.Select(item =>
            Join(item.ContextId, DialogueReferenceLabel(item.DialogueId), item.Binding)));
        _crossZoneAreas.Text = string.Join('\n', validation.CrossZoneNavigation.AllowedAreaIds.Select(AreaReferenceLabel));
        _crossZone.Text = Join(validation.CrossZoneNavigation.Enabled,
            validation.CrossZoneNavigation.FollowPortals, validation.CrossZoneNavigation.SafeFallback,
            validation.CrossZoneNavigation.FallbackAnchorTag, validation.CrossZoneNavigation.TimeoutSeconds);
        _updating = false;
    }

    private StudioCompanionDraftUx4U? ReadDraft(out string error)
    {
        error = string.Empty; var source = _draft;
        if (source is null) { error = "Choisissez un compagnon."; return null; }
        try
        {
            var profile = source.Profile; var foundation = profile.Foundation!; var control = profile.Control!;
            var experience = profile.Experience!; var validation = profile.Validation!;
            var blueprintId = ResolveCreature(_creature.GetItemText(_creature.Selected));
            var dialogue = Parts(_dialoguePolicy.Text, 6);
            var dialoguePolicy = profile.Dialogue with
            {
                DialogueId = ResolveDialogue(dialogue[0]),
                OwnerBehavior = dialogue[1],
                PartyBehavior = dialogue[2],
                OutsiderBehavior = dialogue[3],
                GameMasterBehavior = dialogue[4],
                HostileBehavior = dialogue[5]
            };
            var recruit = Parts(_recruitable.Text, 9);
            var recruitable = foundation.Recruitable with
            {
                Enabled = Bool(recruit[0]),
                Conditions = CsvValues(recruit[1]),
                Cost = Decimal(recruit[2]),
                CurrencyId = recruit[3],
                MinimumReputation = Double(recruit[4]),
                ConsequenceIds = CsvValues(recruit[5]),
                AllowDialogue = Bool(recruit[6]),
                AllowScript = Bool(recruit[7]),
                ScriptId = recruit[8]
            };
            var pre = Parts(_preRecruitment.Text, 6);
            var preRecruitment = foundation.PreRecruitment with
            {
                DialogueId = ResolveDialogue(pre[0]),
                ContextId = pre[1],
                Conditions = CsvValues(pre[2]),
                OfferNodeId = pre[3],
                RefusalNodeId = pre[4],
                ConsequenceIds = CsvValues(pre[5])
            };
            var owner = Parts(_ownerDialogue.Text, 7);
            var ownerDialogue = foundation.OwnerDialogue with
            {
                DialogueId = ResolveDialogue(owner[0]),
                ContextId = owner[1],
                CommandIds = CsvValues(owner[2]),
                ExposeRelationship = Bool(owner[3]),
                ExposeEquipment = Bool(owner[4]),
                ExposeProgression = Bool(owner[5]),
                DepartureActionId = owner[6]
            };
            var member = Parts(_membership.Text, 8);
            var membership = foundation.Membership with
            {
                EnforcePartyCapacity = Bool(member[0]),
                TransferOwnership = Bool(member[1]),
                TransferInventory = Bool(member[2]),
                PreserveDismissPosition = Bool(member[3]),
                Persistence = Enum.Parse<CompanionWorldProfile.CompanionPersistence>(member[4], true),
                InventoryId = member[5],
                DismissAnchorTag = member[6],
                Atomic = Bool(member[7])
            };
            var partyParts = Parts(_partyPolicy.Text, 6);
            var party = foundation.Party with
            {
                CompanionSlots = Int(partyParts[0]),
                SummonSlots = Int(partyParts[1]),
                PetSlots = Int(partyParts[2]),
                TemporaryAllySlots = Int(partyParts[3]),
                ServerActorLimit = Int(partyParts[4]),
                RoleLimits = IntPairs(partyParts[5])
            };
            foundation = foundation with
            {
                Recruitable = recruitable,
                PreRecruitment = preRecruitment,
                OwnerDialogue = ownerDialogue,
                Membership = membership,
                Party = party
            };

            var own = Parts(_ownership.Text, 5);
            var ownership = control.Ownership with
            {
                Kind = own[0],
                ControllerIds = CsvValues(own[1]),
                TransferAllowed = Bool(own[2]),
                AllowedTargetKinds = CsvValues(own[3]),
                Revision = Long(own[4])
            };
            var move = Parts(_movement.Text, 5);
            var movement = control.Movement with
            {
                CommandKinds = CsvValues(move[0]),
                DefaultFormationId = move[1],
                WaypointQuery = move[2],
                MinimumPriority = Int(move[3]),
                MaximumPriority = Int(move[4])
            };
            var tacticalParts = Parts(_tactical.Text, 5);
            var tactical = control.Tactical with
            {
                CommandKinds = CsvValues(tacticalParts[0]),
                Stances = CsvValues(tacticalParts[1]),
                AbilityIds = CsvValues(tacticalParts[2]),
                MinimumPriority = Int(tacticalParts[3]),
                MaximumPriority = Int(tacticalParts[4])
            };
            var aiParts = Parts(_ai.Text, 9);
            var ai = control.Ai with
            {
                Autonomy = Double(aiParts[0]),
                RiskTolerance = Double(aiParts[1]),
                ResourceUsage = Double(aiParts[2]),
                AllowFriendlyFire = Bool(aiParts[3]),
                Role = aiParts[4],
                FollowDistance = Double(aiParts[5]),
                AllowedAbilityTags = CsvValues(aiParts[6]),
                SemanticDiff = aiParts[7],
                AuthorizationLevel = aiParts[8]
            };
            var equipmentParts = Parts(_equipmentControl.Text, 3);
            var equipmentLines = Lines(_equipmentItems.Text); if (equipmentLines.Count == 0)
                throw new FormatException("Au moins un objet suggéré est requis.");
            var suggested = new Dictionary<string, Guid>(StringComparer.Ordinal); var locked = new List<Guid>();
            foreach (var line in equipmentLines)
            {
                var item = Parts(line, 3); var itemId = ResolveItem(item[1]).Value;
                suggested.Add(item[0], itemId); if (Bool(item[2])) locked.Add(itemId);
            }
            var equipment = control.Equipment with
            {
                Permission = equipmentParts[0],
                AllowCompare = Bool(equipmentParts[1]),
                InventoryOwnerId = equipmentParts[2],
                SuggestedLoadout = suggested,
                LockedItemIds = locked
            };
            control = control with
            {
                Ownership = ownership,
                Movement = movement,
                Tactical = tactical,
                Ai = ai,
                Equipment = equipment
            };
            var formationParts = Parts(_formation.Text, 6);
            var formation = profile.Formation with
            {
                FormationId = formationParts[0],
                Slot = Int(formationParts[1]),
                Spacing = Double(formationParts[2]),
                AdaptToObstacles = Bool(formationParts[3]),
                IndoorMode = Bool(formationParts[4]),
                NetworkSynchronized = Bool(formationParts[5])
            };
            var progressionParts = Parts(_progression.Text, 6);
            var progression = profile.Progression with
            {
                Mode = Enum.Parse<CompanionWorldProfile.ProgressionMode>(progressionParts[0], true),
                CurrentLevel = Int(progressionParts[1]),
                MaximumLevel = Int(progressionParts[2]),
                PreviewEnabled = Bool(progressionParts[3]),
                AllowRespec = Bool(progressionParts[4]),
                Milestones = CsvValues(progressionParts[5]).Select(Int).ToArray()
            };
            var relation = Parts(_relationship.Text, 6);
            var relationship = profile.Relationship with
            {
                Affinity = Double(relation[0]),
                Trust = Double(relation[1]),
                Fear = Double(relation[2]),
                Loyalty = Double(relation[3]),
                Tags = CsvValues(relation[4]),
                History = CsvValues(relation[5])
            };
            var participants = Lines(_banterParticipants.Text).Select(ResolveCreature).ToArray();
            if (participants.Length == 0) throw new FormatException("Au moins un participant au banter est requis.");
            var banterParts = Parts(_banter.Text, 5);
            var banter = profile.Banter with
            {
                RequiredParticipants = participants,
                Priority = Int(banterParts[0]),
                CooldownSeconds = Double(banterParts[1]),
                Locations = CsvValues(banterParts[2]),
                Interruptible = Bool(banterParts[3]),
                DialogueId = ResolveDialogue(banterParts[4])
            };
            var questParts = Parts(_personalQuest.Text, 6);
            CompanionWorldProfile.QuestPolicy? quest = Bool(questParts[0])
                ? new(ResolveQuest(questParts[1]), questParts[2], questParts[3], Double(questParts[4]), questParts[5])
                : null;
            var deathParts = Parts(_death.Text, 6);
            var death = experience.Death with
            {
                Mode = deathParts[0],
                DownedDurationSeconds = Double(deathParts[1]),
                ResurrectionResourceId = Empty(deathParts[2]),
                ReplacementCreatureId = Empty(deathParts[3]),
                MemorialId = Empty(deathParts[4]),
                DropInventory = Bool(deathParts[5])
            };
            var trapParts = Parts(_traps.Text, 6);
            var traps = profile.Traps with
            {
                Detect = Bool(trapParts[0]),
                Disarm = Bool(trapParts[1]),
                MaximumRisk = Int(trapParts[2]),
                RequiredSense = trapParts[3],
                SkillId = trapParts[4],
                RequirePlayerCommand = Bool(trapParts[5])
            };
            var abilityParts = Parts(_abilities.Text, 5);
            var abilities = profile.Abilities with
            {
                Allowed = CsvValues(abilityParts[0]),
                Reserved = CsvValues(abilityParts[1]),
                HealthThreshold = Double(abilityParts[2]),
                TargetRule = abilityParts[3],
                AllowPlayerOverrides = Bool(abilityParts[4])
            };
            var directParts = Parts(_directControl.Text, 5);
            var direct = profile.DirectControl with
            {
                Enabled = Bool(directParts[0]),
                AllowPossession = Bool(directParts[1]),
                AllowCharacterSwitch = Bool(directParts[2]),
                ReturnToAi = Bool(directParts[3]),
                MultiplayerPermission = directParts[4]
            };
            var splitParts = Parts(_partySplit.Text, 5);
            var split = profile.PartySplit with
            {
                FollowOwner = Bool(splitParts[0]),
                AllowRegroupRequest = Bool(splitParts[1]),
                InstanceSelection = splitParts[2],
                SynchronizeQuests = Bool(splitParts[3]),
                MaximumSeparationSeconds = Double(splitParts[4])
            };
            var cohesionParts = Parts(_cohesion.Text, 6);
            var cohesion = experience.Cohesion with
            {
                NavigateThreshold = Double(cohesionParts[0]),
                PortalThreshold = Double(cohesionParts[1]),
                TeleportThreshold = Double(cohesionParts[2]),
                FollowPortals = Bool(cohesionParts[3]),
                AllowSafeTeleport = Bool(cohesionParts[4]),
                SafeAnchorTags = CsvValues(cohesionParts[5])
            };
            var hudParts = Parts(_hud.Text, 11);
            var hud = experience.Hud with
            {
                Portraits = Bool(hudParts[0]),
                Health = Bool(hudParts[1]),
                Resources = Bool(hudParts[2]),
                Statuses = Bool(hudParts[3]),
                QuickCommands = Bool(hudParts[4]),
                Reorder = Bool(hudParts[5]),
                Keyboard = Bool(hudParts[6]),
                Gamepad = Bool(hudParts[7]),
                ScreenReaderLabels = Bool(hudParts[8]),
                HighContrast = Bool(hudParts[9]),
                VisibleResourceIds = CsvValues(hudParts[10])
            };
            experience = experience with { Death = death, Cohesion = cohesion, Hud = hud };
            var validationRecruit = Parts(_validationRecruitment.Text, 6);
            var recruitmentValidation = validation.Recruitment with
            {
                AllowDialogue = Bool(validationRecruit[0]),
                AllowScript = Bool(validationRecruit[1]),
                ScriptId = validationRecruit[2],
                CurrencyId = validationRecruit[3],
                Cost = Decimal(validationRecruit[4]),
                MinimumReputation = Double(validationRecruit[5])
            };
            var validationOwner = Parts(_validationOwnership.Text, 3);
            var ownershipValidation = validation.Ownership with
            {
                Kind = validationOwner[0],
                Authority = validationOwner[1],
                TransferAllowed = Bool(validationOwner[2])
            };
            var validationEquipment = Parts(_validationEquipment.Text, 3);
            var equipmentValidation = validation.Equipment with
            {
                InventoryId = validationEquipment[0],
                EquipmentPolicyId = validationEquipment[1],
                Slots = CsvValues(validationEquipment[2])
            };
            var validationDeath = Parts(_validationDeath.Text, 3);
            var deathValidation = validation.Death with
            {
                PolicyId = validationDeath[0],
                Mode = validationDeath[1],
                RecoverySeconds = Double(validationDeath[2])
            };
            var contextLines = Lines(_validationDialogues.Text); if (contextLines.Count < 2)
                throw new FormatException("Deux contextes dialogue sont requis.");
            var contexts = contextLines.Select(line =>
            {
                var p = Parts(line, 3);
                return new CompanionWorldProfile.DialogueContextValidation(p[0], ResolveDialogue(p[1]), p[2]);
            }).ToArray();
            var areas = Lines(_crossZoneAreas.Text).Select(ResolveArea).ToArray();
            if (areas.Length == 0) throw new FormatException("Au moins une zone autorisée est requise.");
            var navigationParts = Parts(_crossZone.Text, 5);
            var navigation = validation.CrossZoneNavigation with
            {
                Enabled = Bool(navigationParts[0]),
                AllowedAreaIds = areas,
                FollowPortals = Bool(navigationParts[1]),
                SafeFallback = Bool(navigationParts[2]),
                FallbackAnchorTag = navigationParts[3],
                TimeoutSeconds = Double(navigationParts[4])
            };
            validation = validation with
            {
                Recruitment = recruitmentValidation,
                Ownership = ownershipValidation,
                CommandIds = CsvValues(_validationCommands.Text),
                Equipment = equipmentValidation,
                Death = deathValidation,
                DialogueContexts = contexts,
                CrossZoneNavigation = navigation
            };
            var rebuilt = profile with
            {
                BlueprintId = blueprintId,
                Dialogue = dialoguePolicy,
                Formation = formation,
                Progression = progression,
                Relationship = relationship,
                Banter = banter,
                PersonalQuest = quest,
                Traps = traps,
                Abilities = abilities,
                DirectControl = direct,
                PartySplit = split,
                Validation = validation,
                ValidationRequiredAtRuntime = true,
                Foundation = foundation,
                FoundationRequiredAtRuntime = true,
                Control = control,
                ControlRequiredAtRuntime = true,
                Experience = experience,
                ExperienceRequiredAtRuntime = true
            };
            return source with { Profile = rebuilt };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        { error = exception.Message; return null; }
    }

    private void Preview()
    {
        var draft = ReadDraft(out var error); if (draft is null)
        { _lastFailure = $"draft:{error}"; SetStatus($"Brouillon invalide : {error}", true); return; }
        _draft = draft; _previewResult = _session.PreviewCompanionUx4U(draft); _previewDryRun = _previewResult.Succeeded;
        RenderPreview(_previewResult); _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }
    private void Apply()
    {
        var draft = ReadDraft(out var error); if (draft is null) { SetStatus(error, true); return; }
        var result = _session.ApplyApprovedCompanionUx4U(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        CompanionChanged?.Invoke(); _previewResult = null; _apply.Disabled = true;
    }
    private void Undo() { var result = _session.UndoCompanionUx4U(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Redo() { var result = _session.RedoCompanionUx4U(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void Reload()
    {
        if (_draft is null) return; _draft = _session.CreateCompanionDraftUx4U(_draft.CompanionId);
        if (_draft is not null) { _draft = _session.CompleteCompanionDraftUx4U(_draft); Populate(_draft); }
        _previewResult = null; _apply.Disabled = true; CompanionChanged?.Invoke();
    }
    private void RenderPreview(StudioCompanionPreviewUx4U result)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics.Take(5))
            _diagnostics.AddChild(StudioStyle.Label(Sanitize($"{diagnostic.HumanMessage} · {diagnostic.Path}"),
                9, StudioStyle.Danger, wrap: true));
        _summary.Text = Sanitize($"[b]{(result.CanApply ? "APERÇU CANONIQUE · COMPAGNON COMPLET" : "APERÇU REFUSÉ")}[/b]\n" +
            $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n" +
            $"Recrutement : {result.RecruitmentSummary}\nContrôle : {result.ControlSummary}\n" +
            $"Récit : {result.NarrativeSummary}\nGroupe : {result.ExperienceSummary}\n" +
            $"{result.RuntimeBoundary}\n[color={(_previewDryRun ? "#8ce0bd" : "#ff7777")}]" +
            $"Aperçu sans mutation : {_previewDryRun.ToString().ToLowerInvariant()}[/color]");
    }
    private void RenderEmpty(string text)
    { foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _summary.Text = text; }
    private void ClearControls()
    {
        _updating = true; _creature.Clear(); foreach (var field in LineFields()) field.Text = string.Empty;
        foreach (var field in MultiFields()) field.Text = string.Empty;
        _updating = false; RenderEmpty("Choisissez explicitement un compagnon ou sa création.");
    }
    private void SetEditingEnabled(bool enabled)
    {
        _creature.Disabled = !enabled; _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !CanonicalPreviewReady; _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null;
    }
    private void InvalidatePreview()
    { if (_updating) return; _session.CancelCompanionUx4U(); _previewResult = null; _apply.Disabled = true; }
    private void ConnectEvents()
    {
        _companion.ItemSelected += SelectionChanged; _creature.ItemSelected += _ => InvalidatePreview();
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo; _redo.Pressed += Redo;
        _save.Pressed += Save;
        foreach (var field in LineFields()) field.TextChanged += _ => InvalidatePreview();
        foreach (var field in MultiFields()) field.TextChanged += InvalidatePreview;
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) return false;
        if (!TryOpenEvidence()) return false;
        var index = _choices.ToList().FindIndex(item => item.CompanionId == _evidenceCompanionId);
        if (index < 0) { _lastFailure = "companion_missing"; Close(); return false; }
        _updating = true; _companion.Select(index + 2); _updating = false; SelectionChanged(0);
        if (_draft is null) { _lastFailure = "draft"; Close(); return false; }
        _draft = _session.CompleteCompanionDraftUx4U(_draft); Populate(_draft);
        var formation = Parts(_formation.Text, 6); formation[2] = (Double(formation[2]) + .125)
            .ToString(CultureInfo.InvariantCulture); _formation.Text = string.Join(" | ", formation);
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched && CanonicalPreviewReady;
        _summary.Text = $"[b]{(_usesRealModule ? "MODULE RÉEL COPIÉ" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"session isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && HasExplicitSelection && ShowsRecruitment && ShowsControl &&
                    ShowsNarrative && ShowsExperience && ShowsValidation && ShowsHumanSurface &&
                    UsesIsolatedEvidence && EvidenceSourceUntouched && (_usesRealModule ^ _usesFallback);
        if (!ready)
        {
            var priorFailure = _lastFailure;
            var diagnostics = _previewResult is null
                ? "preview_absent"
                : string.Join(',', _previewResult.Diagnostics.Select(item => $"{item.Code}@{item.Path}"));
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"recruit={ShowsRecruitment}:control={ShowsControl}:narrative={ShowsNarrative}:" +
                           $"experience={ShowsExperience}:validation={ShowsValidation}:human={ShowsHumanSurface}:" +
                           $"isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealModule ^ _usesFallback}:" +
                           $"diagnostics={diagnostics}:prior={priorFailure}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4U_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-U isolée prête · aucun acteur live n’est modifié.", false); return true;
    }
    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-u-companion-authoring") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; if (original is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original); Apply(); var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null &&
                          ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo(); var undone = applied && ProjectJsonSerializer.Serialize(transaction.Project!) == originalJson;
            if (undone) Redo(); var redone = undone && ProjectJsonSerializer.Serialize(transaction.Project!) == appliedJson;
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                    ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
            }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4U_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        {
            reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCompanionApprovalUx4U(snapshot.Approval);
        }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4UEvidenceProject();
        if (snapshot is null || evidence is null || _sourceSession.Session is not { } sourceWorkspace) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4u-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !StudioSessionController.CopyCompanionScriptsUx4U(sourceWorkspace.RootDirectory, root) ||
                !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root;
            _evidenceCompanionId = evidence.CompanionId; _session = session;
            _usesRealModule = evidence.UsesRealModule; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false; OpenBound();
            return SourceMatches(snapshot) && (_usesRealModule ^ _usesFallback);
        }
        catch
        { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false; }
    }
    private StudioCompanionEvidenceSourceSnapshotUx4U? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureCompanionApprovalUx4U());
    }
    private bool SourceMatches(StudioCompanionEvidenceSourceSnapshotUx4U snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.Dirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveEntity &&
               _sourceSession.CaptureCompanionApprovalUx4U() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceCompanionId = null; _session = _sourceSession; _usesRealModule = false;
        _usesFallback = false; _previewDryRun = false; var released = true;
        try { session?.Close(); } catch { released = false; }
        released &= DeleteEvidenceRoot(root); _resourcesReleased = released; _sourceRestored = true; return released;
    }

    private IEnumerable<LineEdit> LineFields() => [_dialoguePolicy, _recruitable, _preRecruitment,
        _ownerDialogue, _membership, _partyPolicy, _ownership, _movement, _tactical, _ai, _formation,
        _equipmentControl, _validationEquipment, _progression, _relationship, _banter, _personalQuest,
        _death, _traps, _abilities, _directControl, _partySplit, _cohesion, _hud,
        _validationRecruitment, _validationOwnership, _validationCommands, _validationDeath, _crossZone];
    private IEnumerable<TextEdit> MultiFields() => [_equipmentItems, _banterParticipants,
        _validationDialogues, _crossZoneAreas];
    private string CreatureReferenceLabel(BlueprintId id)
    {
        var choice = _session.CompanionBlueprintChoicesUx4U.FirstOrDefault(item => item.BlueprintId == id);
        var label = choice?.Label ?? $"Créature héritée protégée {_creatureReferences.Count + 1}";
        _creatureReferences[label] = id; return label;
    }
    private string DialogueReferenceLabel(Guid id)
    {
        var choice = _session.CompanionDialogueChoicesUx4U.FirstOrDefault(item => item.DialogueId == id);
        var label = choice?.Label ?? $"Dialogue hérité protégé {_dialogueReferences.Count + 1}";
        _dialogueReferences[label] = id; return label;
    }
    private string QuestReferenceLabel(Guid id)
    {
        var choice = _session.CompanionQuestChoicesUx4U.FirstOrDefault(item => item.QuestId == id);
        var label = choice?.Label ?? $"Quête héritée protégée {_questReferences.Count + 1}";
        _questReferences[label] = id; return label;
    }
    private string ItemReferenceLabel(BlueprintId id)
    {
        var choice = _session.CompanionItemChoicesUx4U.FirstOrDefault(item => item.BlueprintId == id);
        var label = choice?.Label ?? $"Objet hérité protégé {_itemReferences.Count + 1}";
        _itemReferences[label] = id; return label;
    }
    private string AreaReferenceLabel(AreaId id)
    {
        var choice = _session.CompanionAreaChoicesUx4U.FirstOrDefault(item => item.AreaId == id);
        var label = choice?.Label ?? $"Zone héritée protégée {_areaReferences.Count + 1}";
        _areaReferences[label] = id; return label;
    }
    private BlueprintId ResolveCreature(string label) => _creatureReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Créature inconnue : {label}");
    private Guid ResolveDialogue(string label) => _dialogueReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Dialogue inconnu : {label}");
    private Guid ResolveQuest(string label) => _questReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Quête inconnue : {label}");
    private BlueprintId ResolveItem(string label) => _itemReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Objet inconnu : {label}");
    private AreaId ResolveArea(string label) => _areaReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Zone inconnue : {label}");
    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true
        };
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); scroll.AddChild(form); tabs.AddChild(scroll); return form;
    }
    private static OptionButton Choice(Container parent, string label)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var result = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static LineEdit Field(Container parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted, wrap: true));
        var result = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static TextEdit Multi(Container parent, string label, float height)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted, wrap: true));
        var result = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static IReadOnlyList<string> Lines(string value) => value.Replace("\r", string.Empty,
        StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[] Parts(string value, int count)
    {
        var parts = value.Split('|').Select(item => DecodeCell(item.Trim())).ToArray();
        if (parts.Length != count) throw new FormatException($"{count} colonnes attendues, {parts.Length} reçues.");
        return parts;
    }
    private static IReadOnlyList<string> CsvValues(string value) => value.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
    private static string Csv(IEnumerable<string> values) => string.Join(',', values);
    private static string Pairs<T>(IReadOnlyDictionary<string, T> values) => string.Join(',', values
        .OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
            $"{item.Key}={Convert.ToString(item.Value, CultureInfo.InvariantCulture)}"));
    private static Dictionary<string, int> IntPairs(string value) => PairValues(value)
        .ToDictionary(item => item.Key, item => Int(item.Value), StringComparer.Ordinal);
    private static IEnumerable<KeyValuePair<string, string>> PairValues(string value)
    {
        foreach (var item in CsvValues(value))
        {
            var separator = item.IndexOf('=');
            if (separator <= 0) throw new FormatException($"Paire clé=valeur invalide : {item}");
            yield return new(item[..separator].Trim(), item[(separator + 1)..].Trim());
        }
    }
    private static int Int(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static long Long(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static double Double(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static decimal Decimal(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static bool Bool(string value) => bool.Parse(value);
    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Join(params object?[] values) => string.Join(" | ", values.Select(value => value switch
    {
        string text => EncodeCell(text),
        bool boolean => boolean.ToString().ToLowerInvariant(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => EncodeCell(value?.ToString() ?? string.Empty)
    }));
    private static string EncodeCell(string value) => value.Replace("%", "%25", StringComparison.Ordinal)
        .Replace("|", "%7C", StringComparison.Ordinal);
    private static string DecodeCell(string value) => value.Replace("%7C", "|", StringComparison.Ordinal)
        .Replace("%25", "%", StringComparison.Ordinal);
    private static IEnumerable<string> VisibleText(Node node)
    {
        if (node is Label label && label.Visible) yield return label.Text;
        if (node is Button button && button.Visible) yield return button.Text;
        foreach (var child in node.GetChildren()) foreach (var text in VisibleText(child)) yield return text;
    }
    private static string Sanitize(string value) => Regex.Replace(value,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}",
        "identité interne");
    private static bool ContainsGuid(string value) => !string.Equals(value, Sanitize(value), StringComparison.Ordinal);
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
}
