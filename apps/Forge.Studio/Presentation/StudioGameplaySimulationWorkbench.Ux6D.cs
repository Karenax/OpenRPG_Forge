using Forge.Domain.Gameplay;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioGameplaySimulationWorkbenchUx6D : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6DEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profile;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly CheckBox _flightEnabled;
    private readonly CheckBox _flightRequired;
    private readonly SpinBox _flightSpeed;
    private readonly SpinBox _flightAcceleration;
    private readonly SpinBox _flightAltitude;
    private readonly SpinBox _flightBanking;
    private readonly SpinBox _flightLift;
    private readonly CheckBox _landingSurface;
    private readonly CheckBox _volumetricNavigation;
    private readonly SpinBox _landingProbe;
    private readonly LineEdit _flightInput;

    private readonly CheckBox _projectileEnabled;
    private readonly CheckBox _projectileRequired;
    private readonly OptionButton _projectileMode;
    private readonly SpinBox _projectileSpeed;
    private readonly SpinBox _projectileGravity;
    private readonly SpinBox _projectileLifetime;
    private readonly SpinBox _homingStrength;
    private readonly SpinBox _collisionRadius;
    private readonly CheckBox _projectileAuthority;
    private readonly CheckBox _projectilePrediction;
    private readonly LineEdit _projectileVfx;
    private readonly LineEdit _impactVfx;
    private readonly LineEdit _collisionMask;
    private readonly SpinBox _projectileRevision;
    private readonly LineEdit _projectileAudit;

    private readonly CheckBox _physicsEnabled;
    private readonly CheckBox _physicsRequired;
    private readonly SpinBox _fixedStep;
    private readonly OptionButton _bodyChoice;
    private readonly LineEdit _bodyName;
    private readonly OptionButton _bodyMode;
    private readonly SpinBox _bodyMass;
    private readonly SpinBox _bodyDamping;
    private readonly SpinBox _bodyGravity;
    private readonly CheckBox _bodyTrigger;
    private readonly CheckBox _bodyCharacters;
    private readonly SpinBox[] _bodyMinimum;
    private readonly SpinBox[] _bodyMaximum;
    private readonly OptionButton _bodyNetwork;
    private readonly CheckBox _bodyRequired;
    private readonly Button _removeBody;
    private readonly OptionButton _jointChoice;
    private readonly LineEdit _jointName;
    private readonly OptionButton _jointMode;
    private readonly OptionButton _jointBodyA;
    private readonly OptionButton _jointBodyB;
    private readonly SpinBox _jointBreakForce;
    private readonly CheckBox _jointCollisionExcluded;
    private readonly Button _removeJoint;

    private readonly CheckBox _targetingEnabled;
    private readonly CheckBox _targetingRequired;
    private readonly OptionButton _targetingMode;
    private readonly SpinBox _targetingRange;
    private readonly SpinBox _targetingRadius;
    private readonly SpinBox _targetingAngle;
    private readonly SpinBox _maximumTargets;
    private readonly CheckBox _lineOfSight;
    private readonly LineEdit _targetingInput;
    private readonly LineEdit _previewColor;
    private readonly TextEdit _requiredTags;
    private readonly TextEdit _blockedTags;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx6DProfileChoice> _choices = [];
    private StudioUx6DDraft? _draft;
    private List<StudioUx6DPhysicsBodyDraft> _bodies = [];
    private List<StudioUx6DPhysicsJointDraft> _joints = [];
    private int _bodyIndex = -1;
    private int _jointIndex = -1;
    private bool _updating;
    private bool _previewReady;
    private string? _previewFingerprint;
    private StudioUx6DPreview? _previewResult;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? GameplaySimulationChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _draft is not null && _profile.Selected > 0;
    internal bool PreviewReady => _previewReady && _previewResult is
    { Succeeded: true, CanonicalDryRun: true, RuntimeClaimsBounded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6D();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsFourWorkflows => _tabs.GetTabCount() == 4 &&
        Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle).SequenceEqual(
            new[] { "Vol libre", "Projectiles", "Physique 3D", "Ciblage" });
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6D(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Simulation gameplay", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   text.Contains("Vol libre", StringComparison.Ordinal) &&
                   text.Contains("Projectiles", StringComparison.Ordinal) &&
                   text.Contains("Physique 3D", StringComparison.Ordinal) &&
                   text.Contains("Ciblage", StringComparison.Ordinal) && !ContainsUuidUx6D(text);
        }
    }

    internal StudioGameplaySimulationWorkbenchUx6D(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6DGameplaySimulationDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Simulation gameplay", 18));
        headingText.AddChild(StudioStyle.Label(
            "Vol libre, projectiles, physique 3D et ciblage · aperçu sec canonique · aucune exécution moteur.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _profile = StudioUx2EnvBControls.Options();
        _profile.ItemSelected += SelectProfileUx6D;
        root.AddChild(FieldUx6D("Module et zone de contexte · sélection explicite", _profile, 620));
        _selection = StudioStyle.Label("Choisissez explicitement le module et une zone de contexte.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 320),
            CustomMaximumSize = new Vector2(-1, 600),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var flightTab = ScrollTabUx6D("Vol libre");
        var flightFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _flightEnabled = StudioUx2EnvBControls.Check("Vol activé");
        _flightRequired = StudioUx2EnvBControls.Check("Profil requis au runtime");
        _landingSurface = StudioUx2EnvBControls.Check("Atterrissage sur surface requis");
        _volumetricNavigation = StudioUx2EnvBControls.Check("Navigation volumétrique");
        foreach (var item in new[] { _flightEnabled, _flightRequired, _landingSurface, _volumetricNavigation })
            flightFlags.AddChild(item);
        flightTab.AddChild(flightFlags);
        var flightNumbers = GridUx6D(4);
        _flightSpeed = NumberUx6D(0, 10_000, .1, true, true);
        _flightAcceleration = NumberUx6D(0, 10_000, .1, true, true);
        _flightAltitude = NumberUx6D(0, 100_000, .5, true, true);
        _flightBanking = NumberUx6D(0, 89, .5, true, true);
        _flightLift = NumberUx6D(0, 10_000, .1, true, true);
        _landingProbe = NumberUx6D(0, 10_000, .05, true, true);
        flightNumbers.AddChild(FieldUx6D("Vitesse maximale (m/s)", _flightSpeed));
        flightNumbers.AddChild(FieldUx6D("Accélération (m/s²)", _flightAcceleration));
        flightNumbers.AddChild(FieldUx6D("Altitude maximale (m)", _flightAltitude));
        flightNumbers.AddChild(FieldUx6D("Inclinaison maximale (°)", _flightBanking));
        flightNumbers.AddChild(FieldUx6D("Portance", _flightLift));
        flightNumbers.AddChild(FieldUx6D("Sonde d’atterrissage (m)", _landingProbe));
        flightTab.AddChild(StudioUx2EnvBControls.Card("Ux6DFlightMotion", "Mouvement aérien",
            "Le résolveur borne position, vitesse, portance et atterrissage dans un volume copié.",
            flightNumbers));
        _flightInput = StudioUx2EnvBControls.Text("flight");
        flightTab.AddChild(FieldUx6D("Contexte d’entrée", _flightInput));
        flightTab.AddChild(StudioStyle.Label(
            "Aucun acteur, navigation 3D, animation, IK, collision ou caméra n’est instancié par cet aperçu.",
            10, StudioStyle.Muted, wrap: true));

        var projectileTab = ScrollTabUx6D("Projectiles");
        var projectileFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _projectileEnabled = StudioUx2EnvBControls.Check("Projectiles activés");
        _projectileRequired = StudioUx2EnvBControls.Check("Profil requis au runtime");
        _projectileAuthority = StudioUx2EnvBControls.Check("Autorité serveur");
        _projectilePrediction = StudioUx2EnvBControls.Check("Prédiction client");
        foreach (var item in new[] { _projectileEnabled, _projectileRequired, _projectileAuthority,
                     _projectilePrediction }) projectileFlags.AddChild(item);
        projectileTab.AddChild(projectileFlags);
        var projectileMotion = GridUx6D(4);
        _projectileMode = StudioUx2EnvBControls.Options("Balistique", "Guidé", "Impact instantané");
        _projectileSpeed = NumberUx6D(0, 100_000, .1, true, true);
        _projectileGravity = NumberUx6D(0, 100_000, .05, true, true);
        _projectileLifetime = NumberUx6D(0, 86_400, .05, true, true);
        _homingStrength = NumberUx6D(0, 100_000, .05, true, true);
        _collisionRadius = NumberUx6D(0, 100_000, .01, true, true);
        projectileMotion.AddChild(FieldUx6D("Mode", _projectileMode));
        projectileMotion.AddChild(FieldUx6D("Vitesse (m/s)", _projectileSpeed));
        projectileMotion.AddChild(FieldUx6D("Gravité", _projectileGravity));
        projectileMotion.AddChild(FieldUx6D("Durée de vie (s)", _projectileLifetime));
        projectileMotion.AddChild(FieldUx6D("Guidage", _homingStrength));
        projectileMotion.AddChild(FieldUx6D("Rayon de collision (m)", _collisionRadius));
        projectileTab.AddChild(StudioUx2EnvBControls.Card("Ux6DProjectileMotion", "Trajectoire et collision",
            "Le calcul pur compare trajectoire, impact et refus d’autorité; aucun projectile natif n’est créé.",
            projectileMotion));
        var projectileAssets = GridUx6D(3);
        _projectileVfx = StudioUx2EnvBControls.Text("vfx:projectile-trail");
        _impactVfx = StudioUx2EnvBControls.Text("vfx:projectile-impact");
        _collisionMask = StudioUx2EnvBControls.Text("world,creature,placeable");
        projectileAssets.AddChild(FieldUx6D("VFX de trajet", _projectileVfx));
        projectileAssets.AddChild(FieldUx6D("VFX d’impact", _impactVfx));
        projectileAssets.AddChild(FieldUx6D("Masque de collision", _collisionMask));
        projectileTab.AddChild(projectileAssets);
        var projectileAudit = GridUx6D(2);
        _projectileRevision = NumberUx6D(0, long.MaxValue, 1, true, true);
        _projectileAudit = StudioUx2EnvBControls.Text("gameplay.projectile");
        projectileAudit.AddChild(FieldUx6D("Révision serveur", _projectileRevision));
        projectileAudit.AddChild(FieldUx6D("Étiquette d’audit", _projectileAudit));
        projectileTab.AddChild(projectileAudit);

        var physicsTab = ScrollTabUx6D("Physique 3D");
        var physicsFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _physicsEnabled = StudioUx2EnvBControls.Check("Physique activée");
        _physicsRequired = StudioUx2EnvBControls.Check("Scène requise au runtime");
        _fixedStep = NumberUx6D(0, 1, .001, true, true);
        physicsFlags.AddChild(_physicsEnabled); physicsFlags.AddChild(_physicsRequired);
        physicsFlags.AddChild(FieldUx6D("Pas fixe (s)", _fixedStep, 180));
        physicsTab.AddChild(physicsFlags);

        var bodyHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bodyChoice = StudioUx2EnvBControls.Options();
        _bodyChoice.ItemSelected += SelectBodyUx6D;
        bodyHeader.AddChild(FieldUx6D("Corps physique · identité stable protégée", _bodyChoice, 520));
        var addBody = StudioStyle.Button("Ajouter un corps", compact: true);
        addBody.Pressed += AddBodyUx6D;
        _removeBody = StudioStyle.Button("Retirer", compact: true);
        _removeBody.Pressed += RemoveBodyUx6D;
        bodyHeader.AddChild(addBody); bodyHeader.AddChild(_removeBody);
        physicsTab.AddChild(bodyHeader);
        var bodyIdentity = GridUx6D(4);
        _bodyName = StudioUx2EnvBControls.Text("Corps physique");
        _bodyMode = StudioUx2EnvBControls.Options("Statique", "Cinématique", "Dynamique");
        _bodyNetwork = StudioUx2EnvBControls.Options("Serveur", "Propriétaire", "Aperçu local");
        _bodyRequired = StudioUx2EnvBControls.Check("Corps requis au runtime");
        bodyIdentity.AddChild(FieldUx6D("Nom humain", _bodyName));
        bodyIdentity.AddChild(FieldUx6D("Mode", _bodyMode));
        bodyIdentity.AddChild(FieldUx6D("Politique réseau", _bodyNetwork));
        bodyIdentity.AddChild(_bodyRequired);
        physicsTab.AddChild(bodyIdentity);
        var bodyNumbers = GridUx6D(4);
        _bodyMass = NumberUx6D(0, 1_000_000, .1, true, true);
        _bodyDamping = NumberUx6D(0, 1_000_000, .01, true, true);
        _bodyGravity = NumberUx6D(0, 1_000_000, .01, true, true);
        _bodyTrigger = StudioUx2EnvBControls.Check("Volume déclencheur");
        _bodyCharacters = StudioUx2EnvBControls.Check("Interaction personnages");
        bodyNumbers.AddChild(FieldUx6D("Masse", _bodyMass));
        bodyNumbers.AddChild(FieldUx6D("Amortissement linéaire", _bodyDamping));
        bodyNumbers.AddChild(FieldUx6D("Échelle de gravité", _bodyGravity));
        bodyNumbers.AddChild(_bodyTrigger); bodyNumbers.AddChild(_bodyCharacters);
        physicsTab.AddChild(bodyNumbers);
        _bodyMinimum = [AxisUx6D(), AxisUx6D(), AxisUx6D()];
        _bodyMaximum = [AxisUx6D(), AxisUx6D(), AxisUx6D()];
        var bounds = GridUx6D(3);
        bounds.AddChild(VectorFieldUx6D("Limite minimale", _bodyMinimum));
        bounds.AddChild(VectorFieldUx6D("Limite maximale", _bodyMaximum));
        physicsTab.AddChild(StudioUx2EnvBControls.Card("Ux6DBodyBounds", "Bornes de simulation",
            "Les axes doivent former un volume strictement positif; aucune boîte Godot n’est créée.", bounds));

        var jointHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _jointChoice = StudioUx2EnvBControls.Options();
        _jointChoice.ItemSelected += SelectJointUx6D;
        jointHeader.AddChild(FieldUx6D("Liaison physique · identité stable protégée", _jointChoice, 520));
        var addJoint = StudioStyle.Button("Ajouter une liaison", compact: true);
        addJoint.Pressed += AddJointUx6D;
        _removeJoint = StudioStyle.Button("Retirer", compact: true);
        _removeJoint.Pressed += RemoveJointUx6D;
        jointHeader.AddChild(addJoint); jointHeader.AddChild(_removeJoint);
        physicsTab.AddChild(jointHeader);
        var jointGrid = GridUx6D(4);
        _jointName = StudioUx2EnvBControls.Text("Liaison physique");
        _jointMode = StudioUx2EnvBControls.Options("Fixe", "Charnière", "Glissière", "Ressort");
        _jointBodyA = StudioUx2EnvBControls.Options();
        _jointBodyB = StudioUx2EnvBControls.Options();
        _jointBreakForce = NumberUx6D(0, 1_000_000_000, 1, true, true);
        _jointCollisionExcluded = StudioUx2EnvBControls.Check("Collision mutuelle exclue");
        jointGrid.AddChild(FieldUx6D("Nom humain", _jointName));
        jointGrid.AddChild(FieldUx6D("Mode", _jointMode));
        jointGrid.AddChild(FieldUx6D("Corps A", _jointBodyA));
        jointGrid.AddChild(FieldUx6D("Corps B", _jointBodyB));
        jointGrid.AddChild(FieldUx6D("Force de rupture", _jointBreakForce));
        jointGrid.AddChild(_jointCollisionExcluded);
        physicsTab.AddChild(jointGrid);
        physicsTab.AddChild(StudioStyle.Label(
            "Les corps et liaisons sont édités par identités internes stables; aucun ordre de liste n’est utilisé comme identité.",
            10, StudioStyle.Muted, wrap: true));

        var targetingTab = ScrollTabUx6D("Ciblage");
        var targetFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _targetingEnabled = StudioUx2EnvBControls.Check("Ciblage activé");
        _targetingRequired = StudioUx2EnvBControls.Check("Profil requis au runtime");
        _lineOfSight = StudioUx2EnvBControls.Check("Ligne de vue requise");
        targetFlags.AddChild(_targetingEnabled); targetFlags.AddChild(_targetingRequired);
        targetFlags.AddChild(_lineOfSight); targetingTab.AddChild(targetFlags);
        var targetGrid = GridUx6D(4);
        _targetingMode = StudioUx2EnvBControls.Options(
            "Soi-même", "Entité", "Point", "Direction", "Zone", "Cône", "Ligne", "Chaîne");
        _targetingRange = NumberUx6D(0, 1_000_000, .1, true, true);
        _targetingRadius = NumberUx6D(0, 1_000_000, .1, true, true);
        _targetingAngle = NumberUx6D(0, 360, .5, true, true);
        _maximumTargets = NumberUx6D(1, int.MaxValue, 1, true, true);
        _targetingInput = StudioUx2EnvBControls.Text("target");
        _previewColor = StudioUx2EnvBControls.Text("#55d6be");
        targetGrid.AddChild(FieldUx6D("Mode", _targetingMode));
        targetGrid.AddChild(FieldUx6D("Portée maximale (m)", _targetingRange));
        targetGrid.AddChild(FieldUx6D("Rayon (m)", _targetingRadius));
        targetGrid.AddChild(FieldUx6D("Angle (°)", _targetingAngle));
        targetGrid.AddChild(FieldUx6D("Cibles maximales", _maximumTargets));
        targetGrid.AddChild(FieldUx6D("Action d’entrée", _targetingInput));
        targetGrid.AddChild(FieldUx6D("Couleur d’aperçu", _previewColor));
        targetingTab.AddChild(targetGrid);
        var targetTags = GridUx6D(2);
        _requiredTags = StudioUx2EnvBControls.Lines("Un tag requis par ligne", 105);
        _blockedTags = StudioUx2EnvBControls.Lines("Un tag bloqué par ligne", 105);
        targetTags.AddChild(FieldUx6D("Tags requis", _requiredTags));
        targetTags.AddChild(FieldUx6D("Tags bloqués", _blockedTags));
        targetingTab.AddChild(targetTags);
        targetingTab.AddChild(StudioStyle.Label(
            "La géométrie et l’éligibilité sont calculées en mémoire; aucune caméra, raycast ou sélection live n’est exécutée.",
            10, StudioStyle.Muted, wrap: true));

        WireInvalidationUx6D();

        var reviewHost = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 132),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _diff = ReviewColumnUx6D(reviewHost, "Diff sémantique");
        _runtime = ReviewColumnUx6D(reviewHost, "Résolveurs déterministes");
        _diagnostics = ReviewColumnUx6D(reviewHost, "Diagnostics");
        root.AddChild(reviewHost);
        _summary = StudioStyle.Label("Aucun aperçu sec frais.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Aperçu sec", compact: true); _preview.Pressed += PreviewUx6D;
        _apply = StudioStyle.Button("Appliquer", compact: true); _apply.Pressed += ApplyUx6D;
        _undo = StudioStyle.Button("Annuler", compact: true); _undo.Pressed += UndoUx6D;
        _redo = StudioStyle.Button("Rétablir", compact: true); _redo.Pressed += RedoUx6D;
        _save = StudioStyle.Button("Enregistrer", compact: true); _save.Pressed += SaveUx6D;
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(118, 34);
            actions.AddChild(button);
        }
        actions.AddChild(StudioStyle.Label(
            "Aperçu en mémoire · application unique · historique · sauvegarde et réouverture canoniques.",
            10, StudioStyle.Muted, wrap: true));
        root.AddChild(actions);

        RenderPreviewUx6D(null);
        UpdateButtonsUx6D();
    }

    internal void Open() => OpenBoundUx6D(_sourceSession, false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6D(_sourceSession, false, cancelApproval: false);

    private void OpenBoundUx6D(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6DApproval();
        _choices = _session.GameplaySimulationChoicesUx6D;
        ResetControlsUx6D();
        _profile.Clear();
        _profile.AddItem("Choisir explicitement un contexte…");
        foreach (var choice in _choices) _profile.AddItem(choice.Label);
        _profile.Select(0);
        _selection.Text = evidence
            ? "Copie temporaire isolée · la source reste strictement inchangée."
            : "Choisissez explicitement le module et une zone de contexte.";
        Visible = true;
        MoveToFront();
        UpdateBoundsUx6D();
        UpdateButtonsUx6D();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null)
            ReleaseEvidenceUx6D();
        else
        {
            _session.CancelUx6DApproval();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControlsUx6D();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidenceUx6D();
        Visible = false;
        ResetControlsUx6D();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        _sourceSnapshot = CaptureSourceUx6D();
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux6-d-{Guid.NewGuid():N}");
        _evidenceSession = new StudioSessionController();
        _evidenceReleased = false;
        var created = ProjectSession.Create(_evidenceRoot, _sourceSession.Project);
        if (!created.Succeeded || !_evidenceSession.Open(_evidenceRoot).Succeeded)
        {
            ReleaseEvidenceUx6D();
            return false;
        }
        OpenBoundUx6D(_evidenceSession, true);
        if (_choices.Count == 0)
        {
            CloseEvidence();
            return false;
        }
        _profile.Select(1);
        SelectProfileUx6D(1);
        ConfigureEvidenceUx6D();
        PreviewUx6D();
        var ready = PreviewReady && UsesIsolatedEvidence && HasExplicitSelection &&
                    ShowsFourWorkflows && ShowsHumanSurface && SourceMatchesUx6D();
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceUx6D();
        var prepared = false;
        try
        {
            prepared = PrepareEvidence("ux6-d-gameplay-simulation");
            if (!prepared || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady) return false;
            var before = _evidenceSession.Project!;
            var history = _evidenceSession.History!;
            var journalCount = history.Journal.Count;
            ApplyUx6D();
            var applied = history.Journal.Count == journalCount + 1 &&
                          history.Journal.Last().CommandName == "set_gameplay_control_profile" &&
                          !ReferenceEquals(before, _evidenceSession.Project);
            var after = _evidenceSession.Project!;
            UndoUx6D();
            var undone = ReferenceEquals(before, _evidenceSession.Project);
            RedoUx6D();
            var redone = ReferenceEquals(after, _evidenceSession.Project);
            var json = ProjectJsonSerializer.Serialize(after);
            SaveUx6D();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded && reopened.Project is not null &&
                            string.Equals(json, ProjectJsonSerializer.Serialize(reopened.Project),
                                StringComparison.Ordinal);
            reopened.Close();
            var sourceMatches = SourceMatchesUx6D(source);
            if (!(applied && undone && redone && persisted && sourceMatches))
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_D_UI_TRANSACTION_FAILED " +
                            $"applied={applied.ToString().ToLowerInvariant()} " +
                            $"undone={undone.ToString().ToLowerInvariant()} " +
                            $"redone={redone.ToString().ToLowerInvariant()} " +
                            $"persisted={persisted.ToString().ToLowerInvariant()} " +
                            $"source={sourceMatches.ToString().ToLowerInvariant()}");
            return applied && undone && redone && persisted && sourceMatches;
        }
        finally
        {
            if (prepared || _evidenceSession is not null || _evidenceRoot is not null) CloseEvidence();
            _sourceSession.RestoreGameplaySimulationApprovalUx6D(source.Approval);
        }
    }

    private void SelectProfileUx6D(long selected)
    {
        if (_updating) return;
        _session.CancelUx6DApproval();
        _previewReady = false; _previewFingerprint = null; _previewResult = null;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ResetControlsUx6D();
            _selection.Text = "Sélection explicite requise.";
            return;
        }
        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateGameplaySimulationDraftUx6D(choice.ContextAreaId, true);
        if (_draft is null)
        {
            ResetControlsUx6D();
            _selection.Text = "Le profil de simulation gameplay n’est pas disponible.";
            return;
        }
        PopulateUx6D(_draft);
        _selection.Text = $"Profil chargé · {choice.Label} · identités internes protégées.";
        SetStatusUx6D("Profil chargé. Modifiez puis lancez l’aperçu sec.", false);
    }

    private void PopulateUx6D(StudioUx6DDraft draft)
    {
        _updating = true;
        _flightEnabled.ButtonPressed = draft.Flight.Enabled;
        _flightRequired.ButtonPressed = draft.Flight.Required;
        _flightSpeed.Value = DisplayUx6D(draft.Flight.MaximumSpeed);
        _flightAcceleration.Value = DisplayUx6D(draft.Flight.Acceleration);
        _flightAltitude.Value = DisplayUx6D(draft.Flight.MaximumAltitude);
        _flightBanking.Value = DisplayUx6D(draft.Flight.BankingDegrees);
        _flightLift.Value = DisplayUx6D(draft.Flight.Lift);
        _landingSurface.ButtonPressed = draft.Flight.RequiresLandingSurface;
        _volumetricNavigation.ButtonPressed = draft.Flight.VolumetricNavigation;
        _landingProbe.Value = DisplayUx6D(draft.Flight.LandingProbeDistance);
        _flightInput.Text = draft.Flight.InputContext;

        _projectileEnabled.ButtonPressed = draft.Projectiles.Enabled;
        _projectileRequired.ButtonPressed = draft.Projectiles.Required;
        _projectileMode.Select((int)draft.Projectiles.Mode);
        _projectileSpeed.Value = DisplayUx6D(draft.Projectiles.Speed);
        _projectileGravity.Value = DisplayUx6D(draft.Projectiles.Gravity);
        _projectileLifetime.Value = DisplayUx6D(draft.Projectiles.LifetimeSeconds);
        _homingStrength.Value = DisplayUx6D(draft.Projectiles.HomingStrength);
        _collisionRadius.Value = DisplayUx6D(draft.Projectiles.CollisionRadius);
        _projectileAuthority.ButtonPressed = draft.Projectiles.ServerAuthoritative;
        _projectilePrediction.ButtonPressed = draft.Projectiles.Prediction;
        _projectileVfx.Text = draft.Projectiles.VfxAssetId;
        _impactVfx.Text = draft.Projectiles.ImpactVfxAssetId;
        _collisionMask.Text = draft.Projectiles.CollisionMask;
        _projectileRevision.Value = draft.Projectiles.ServerRevision;
        _projectileAudit.Text = draft.Projectiles.AuditTag;

        _physicsEnabled.ButtonPressed = draft.Physics.Enabled;
        _physicsRequired.ButtonPressed = draft.Physics.Required;
        _fixedStep.Value = DisplayUx6D(draft.Physics.FixedStepSeconds);
        _bodies = draft.Physics.Bodies.ToList();
        _joints = draft.Physics.Joints.ToList();
        _bodyIndex = _bodies.Count == 0 ? -1 : 0;
        _jointIndex = _joints.Count == 0 ? -1 : 0;
        RebuildPhysicsChoicesUx6D();

        _targetingEnabled.ButtonPressed = draft.Targeting.Enabled;
        _targetingRequired.ButtonPressed = draft.Targeting.Required;
        _targetingMode.Select((int)draft.Targeting.Mode);
        _targetingRange.Value = DisplayUx6D(draft.Targeting.MaximumRange);
        _targetingRadius.Value = DisplayUx6D(draft.Targeting.Radius);
        _targetingAngle.Value = DisplayUx6D(draft.Targeting.AngleDegrees);
        _maximumTargets.Value = draft.Targeting.MaximumTargets;
        _lineOfSight.ButtonPressed = draft.Targeting.RequiresLineOfSight;
        _targetingInput.Text = draft.Targeting.InputAction;
        _previewColor.Text = draft.Targeting.PreviewColor;
        _requiredTags.Text = string.Join('\n', draft.Targeting.RequiredTags);
        _blockedTags.Text = string.Join('\n', draft.Targeting.BlockedTags);
        _updating = false;
        PopulateBodyUx6D(); PopulateJointUx6D();
        InvalidatePreviewUx6D();
    }

    private StudioUx6DDraft? ReadDraftUx6D()
    {
        if (_draft is null) return null;
        FlushBodyUx6D(); FlushJointUx6D();
        return _draft with
        {
            Flight = new(_flightEnabled.ButtonPressed, _flightRequired.ButtonPressed,
                (float)_flightSpeed.Value, (float)_flightAcceleration.Value,
                (float)_flightAltitude.Value, (float)_flightBanking.Value, (float)_flightLift.Value,
                _landingSurface.ButtonPressed, _volumetricNavigation.ButtonPressed,
                (float)_landingProbe.Value, _flightInput.Text),
            Projectiles = new(_projectileEnabled.ButtonPressed, _projectileRequired.ButtonPressed,
                (ProjectileMode)_projectileMode.Selected, (float)_projectileSpeed.Value,
                (float)_projectileGravity.Value, (float)_projectileLifetime.Value,
                (float)_homingStrength.Value, (float)_collisionRadius.Value,
                _projectileAuthority.ButtonPressed, _projectilePrediction.ButtonPressed,
                _projectileVfx.Text, _impactVfx.Text, _collisionMask.Text,
                (long)_projectileRevision.Value, _projectileAudit.Text),
            Physics = new(_physicsEnabled.ButtonPressed, _physicsRequired.ButtonPressed,
                (float)_fixedStep.Value, _bodies.ToArray(), _joints.ToArray()),
            Targeting = new(_targetingEnabled.ButtonPressed, _targetingRequired.ButtonPressed,
                (TargetingMode)_targetingMode.Selected, (float)_targetingRange.Value,
                (float)_targetingRadius.Value, (float)_targetingAngle.Value,
                (int)_maximumTargets.Value, _lineOfSight.ButtonPressed, _targetingInput.Text,
                _previewColor.Text, LinesUx6D(_requiredTags.Text), LinesUx6D(_blockedTags.Text)),
        };
    }

    private void PreviewUx6D()
    {
        var draft = ReadDraftUx6D();
        if (draft is null) { SetStatusUx6D("Choisissez explicitement un profil.", true); return; }
        _draft = draft;
        _previewResult = _session.PreviewGameplaySimulationUx6D(draft);
        _previewReady = _previewResult.Succeeded;
        _previewFingerprint = _session.GameplaySimulationDraftFingerprintUx6D(draft);
        RenderPreviewUx6D(_previewResult);
        SetStatusUx6D(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtonsUx6D();
    }

    private void ApplyUx6D()
    {
        var current = ReadDraftUx6D();
        if (current is null || !_previewReady ||
            !string.Equals(_previewFingerprint,
                _session.GameplaySimulationDraftFingerprintUx6D(current), StringComparison.Ordinal))
        {
            _session.CancelUx6DApproval();
            _previewReady = false;
            SetStatusUx6D("Le brouillon a changé. Relancez l’aperçu sec.", true);
            UpdateButtonsUx6D();
            return;
        }
        var result = _session.ApplyApprovedGameplaySimulationUx6D();
        SetStatusUx6D(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            GameplaySimulationChanged?.Invoke();
            ReloadSelectedUx6D();
        }
        UpdateButtonsUx6D();
    }

    private void UndoUx6D()
    {
        var result = _session.Undo();
        SetStatusUx6D(result.Message, !result.Succeeded);
        if (result.Succeeded) { GameplaySimulationChanged?.Invoke(); ReloadSelectedUx6D(); }
    }

    private void RedoUx6D()
    {
        var result = _session.Redo();
        SetStatusUx6D(result.Message, !result.Succeeded);
        if (result.Succeeded) { GameplaySimulationChanged?.Invoke(); ReloadSelectedUx6D(); }
    }

    private void SaveUx6D()
    {
        var result = _session.Save();
        SetStatusUx6D(result.Message, !result.Succeeded);
        UpdateButtonsUx6D();
    }

    private void ReloadSelectedUx6D()
    {
        var selected = _profile.Selected;
        _session.CancelUx6DApproval();
        _previewReady = false; _previewFingerprint = null;
        if (selected > 0) SelectProfileUx6D(selected);
    }

    private void ConfigureEvidenceUx6D()
    {
        _flightSpeed.Value = Math.Min(9_999, _flightSpeed.Value + .125);
        _projectileSpeed.Value = Math.Min(99_999, _projectileSpeed.Value + .125);
        _targetingRange.Value = Math.Min(999_999, _targetingRange.Value + .125);
        if (_bodyIndex >= 0)
            _bodyMass.Value = Math.Min(999_999, _bodyMass.Value + .125);
    }

    private void SelectBodyUx6D(long selected)
    {
        if (_updating) return;
        FlushBodyUx6D();
        _bodyIndex = (int)selected;
        PopulateBodyUx6D();
        InvalidatePreviewUx6D();
    }

    private void SelectJointUx6D(long selected)
    {
        if (_updating) return;
        FlushJointUx6D();
        _jointIndex = (int)selected;
        PopulateJointUx6D();
        InvalidatePreviewUx6D();
    }

    private void RebuildPhysicsChoicesUx6D()
    {
        var previousUpdating = _updating;
        _updating = true;
        _bodyChoice.Clear();
        foreach (var body in _bodies)
            _bodyChoice.AddItem($"{HumanValueUx6D(body.Name)} · {HumanBodyModeUx6D(body.Mode)}");
        _bodyIndex = _bodies.Count == 0 ? -1 : Math.Clamp(_bodyIndex, 0, _bodies.Count - 1);
        if (_bodyIndex >= 0) _bodyChoice.Select(_bodyIndex);

        _jointChoice.Clear();
        foreach (var joint in _joints)
            _jointChoice.AddItem($"{HumanValueUx6D(joint.Name)} · {HumanJointModeUx6D(joint.Mode)}");
        _jointIndex = _joints.Count == 0 ? -1 : Math.Clamp(_jointIndex, 0, _joints.Count - 1);
        if (_jointIndex >= 0) _jointChoice.Select(_jointIndex);

        var bodyA = _jointIndex >= 0 && _jointIndex < _joints.Count ? _joints[_jointIndex].BodyAId : Guid.Empty;
        var bodyB = _jointIndex >= 0 && _jointIndex < _joints.Count ? _joints[_jointIndex].BodyBId : Guid.Empty;
        _jointBodyA.Clear(); _jointBodyB.Clear();
        foreach (var body in _bodies)
        {
            var label = HumanValueUx6D(body.Name);
            _jointBodyA.AddItem(label); _jointBodyB.AddItem(label);
        }
        var indexA = _bodies.FindIndex(item => item.Id == bodyA);
        var indexB = _bodies.FindIndex(item => item.Id == bodyB);
        if (indexA >= 0) _jointBodyA.Select(indexA);
        if (indexB >= 0) _jointBodyB.Select(indexB);
        _removeBody.Disabled = _bodyIndex < 0;
        _removeJoint.Disabled = _jointIndex < 0;
        _updating = previousUpdating;
    }

    private void PopulateBodyUx6D()
    {
        if (_bodyIndex < 0 || _bodyIndex >= _bodies.Count) return;
        _updating = true;
        var item = _bodies[_bodyIndex];
        _bodyName.Text = item.Name;
        _bodyMode.Select((int)item.Mode);
        _bodyMass.Value = DisplayUx6D(item.Mass);
        _bodyDamping.Value = DisplayUx6D(item.LinearDamping);
        _bodyGravity.Value = DisplayUx6D(item.GravityScale);
        _bodyTrigger.ButtonPressed = item.IsTrigger;
        _bodyCharacters.ButtonPressed = item.InteractsWithCharacters;
        SetVectorUx6D(_bodyMinimum, item.BoundaryMinimum);
        SetVectorUx6D(_bodyMaximum, item.BoundaryMaximum);
        _bodyNetwork.Select(NetworkIndexUx6D(item.NetworkPolicy));
        _bodyRequired.ButtonPressed = item.RequiredAtRuntime;
        _updating = false;
    }

    private void FlushBodyUx6D()
    {
        if (_updating || _bodyIndex < 0 || _bodyIndex >= _bodies.Count) return;
        _bodies[_bodyIndex] = _bodies[_bodyIndex] with
        {
            Name = _bodyName.Text,
            Mode = (PhysicsBodyMode)_bodyMode.Selected,
            Mass = (float)_bodyMass.Value,
            LinearDamping = (float)_bodyDamping.Value,
            GravityScale = (float)_bodyGravity.Value,
            IsTrigger = _bodyTrigger.ButtonPressed,
            InteractsWithCharacters = _bodyCharacters.ButtonPressed,
            BoundaryMinimum = ReadVectorUx6D(_bodyMinimum),
            BoundaryMaximum = ReadVectorUx6D(_bodyMaximum),
            NetworkPolicy = NetworkValueUx6D(_bodyNetwork.Selected),
            RequiredAtRuntime = _bodyRequired.ButtonPressed,
        };
    }

    private void PopulateJointUx6D()
    {
        if (_jointIndex < 0 || _jointIndex >= _joints.Count) return;
        _updating = true;
        var item = _joints[_jointIndex];
        _jointName.Text = item.Name;
        _jointMode.Select((int)item.Mode);
        _jointBodyA.Select(Math.Max(0, _bodies.FindIndex(body => body.Id == item.BodyAId)));
        _jointBodyB.Select(Math.Max(0, _bodies.FindIndex(body => body.Id == item.BodyBId)));
        _jointBreakForce.Value = DisplayUx6D(item.BreakForce);
        _jointCollisionExcluded.ButtonPressed = item.CollisionExcluded;
        _updating = false;
    }

    private void FlushJointUx6D()
    {
        if (_updating || _jointIndex < 0 || _jointIndex >= _joints.Count || _bodies.Count == 0) return;
        var indexA = Math.Clamp(_jointBodyA.Selected, 0, _bodies.Count - 1);
        var indexB = Math.Clamp(_jointBodyB.Selected, 0, _bodies.Count - 1);
        _joints[_jointIndex] = _joints[_jointIndex] with
        {
            Name = _jointName.Text,
            Mode = (PhysicsJointMode)_jointMode.Selected,
            BodyAId = _bodies[indexA].Id,
            BodyBId = _bodies[indexB].Id,
            BreakForce = (float)_jointBreakForce.Value,
            CollisionExcluded = _jointCollisionExcluded.ButtonPressed,
        };
    }

    private void AddBodyUx6D()
    {
        FlushBodyUx6D(); FlushJointUx6D();
        var index = _bodies.Count + 1;
        _bodies.Add(new(Guid.NewGuid(), $"Corps {index}", PhysicsBodyMode.Rigid, 1, .1f, 1,
            false, true, new(-10, -2, -10), new(10, 20, 10), "server", false));
        _bodyIndex = _bodies.Count - 1;
        RebuildPhysicsChoicesUx6D(); PopulateBodyUx6D(); PopulateJointUx6D();
        InvalidatePreviewUx6D();
    }

    private void RemoveBodyUx6D()
    {
        if (_bodyIndex < 0 || _bodyIndex >= _bodies.Count) return;
        var body = _bodies[_bodyIndex];
        if (_joints.Any(item => item.BodyAId == body.Id || item.BodyBId == body.Id))
        {
            SetStatusUx6D("Retirez d’abord les liaisons qui utilisent ce corps.", true);
            return;
        }
        _bodies.RemoveAt(_bodyIndex);
        _bodyIndex = _bodies.Count == 0 ? -1 : Math.Min(_bodyIndex, _bodies.Count - 1);
        RebuildPhysicsChoicesUx6D(); PopulateBodyUx6D(); PopulateJointUx6D();
        InvalidatePreviewUx6D();
    }

    private void AddJointUx6D()
    {
        FlushBodyUx6D(); FlushJointUx6D();
        if (_bodies.Count < 2)
        {
            SetStatusUx6D("Deux corps sont requis pour créer une liaison.", true);
            return;
        }
        var index = _joints.Count + 1;
        _joints.Add(new(Guid.NewGuid(), $"Liaison {index}", PhysicsJointMode.Fixed,
            _bodies[0].Id, _bodies[1].Id, 100, true));
        _jointIndex = _joints.Count - 1;
        RebuildPhysicsChoicesUx6D(); PopulateJointUx6D();
        InvalidatePreviewUx6D();
    }

    private void RemoveJointUx6D()
    {
        if (_jointIndex < 0 || _jointIndex >= _joints.Count) return;
        _joints.RemoveAt(_jointIndex);
        _jointIndex = _joints.Count == 0 ? -1 : Math.Min(_jointIndex, _joints.Count - 1);
        RebuildPhysicsChoicesUx6D(); PopulateJointUx6D();
        InvalidatePreviewUx6D();
    }

    private void InvalidatePreviewUx6D()
    {
        if (_updating) return;
        _previewReady = false; _previewFingerprint = null;
        _session.CancelUx6DApproval(); UpdateButtonsUx6D();
    }

    private void ResetControlsUx6D()
    {
        _updating = true;
        _draft = null; _bodies = []; _joints = [];
        _bodyChoice.Clear(); _jointChoice.Clear(); _jointBodyA.Clear(); _jointBodyB.Clear();
        _bodyIndex = _jointIndex = -1;
        _previewReady = false; _previewFingerprint = null; _previewResult = null;
        RenderPreviewUx6D(null);
        _updating = false;
        UpdateButtonsUx6D();
    }

    private void UpdateButtonsUx6D()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !PreviewReady || _previewResult is not { CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
        _removeBody.Disabled = _bodyIndex < 0;
        _removeJoint.Disabled = _jointIndex < 0;
    }

    private void RenderPreviewUx6D(StudioUx6DPreview? preview)
    {
        ClearChildrenUx6D(_diff); ClearChildrenUx6D(_runtime); ClearChildrenUx6D(_diagnostics);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Le diff sémantique apparaîtra après l’aperçu sec.",
                10, StudioStyle.Muted, wrap: true));
            _runtime.AddChild(StudioStyle.Label("Les quatre résolutions pures apparaîtront ici.",
                10, StudioStyle.Muted, wrap: true));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic frais.", 10, StudioStyle.Muted));
            _summary.Text = "Aucun aperçu sec frais.";
            return;
        }
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted));
        foreach (var item in preview.Diff)
            _diff.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        foreach (var item in preview.RuntimePreview)
            _runtime.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Conforme · dry-run canonique", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10,
                item.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        _summary.Text = preview.Message + "\n" + preview.PatchSummary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
    }

    private StudioUx6DEvidenceSourceSnapshot CaptureSourceUx6D()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureGameplaySimulationApprovalUx6D());
    }

    private bool SourceMatchesUx6D(StudioUx6DEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureGameplaySimulationApprovalUx6D() == snapshot.Approval;
    }

    private bool ReleaseEvidenceUx6D()
    {
        var isolated = _evidenceSession; var root = _evidenceRoot; var sessionReleased = true;
        try { isolated?.Close(); }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_D_RELEASE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx6DTemporaryWorkspace(root);
        _evidenceSession = null; _evidenceRoot = rootReleased ? null : root; _sourceSnapshot = null;
        _session = _sourceSession; _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void WireInvalidationUx6D()
    {
        foreach (var item in new[] { _flightEnabled, _flightRequired, _landingSurface,
                     _volumetricNavigation, _projectileEnabled, _projectileRequired,
                     _projectileAuthority, _projectilePrediction, _physicsEnabled, _physicsRequired,
                     _bodyTrigger, _bodyCharacters, _bodyRequired, _jointCollisionExcluded,
                     _targetingEnabled, _targetingRequired, _lineOfSight })
            item.Toggled += _ => InvalidatePreviewUx6D();
        foreach (var item in new[] { _flightSpeed, _flightAcceleration, _flightAltitude,
                     _flightBanking, _flightLift, _landingProbe, _projectileSpeed, _projectileGravity,
                     _projectileLifetime, _homingStrength, _collisionRadius, _projectileRevision,
                     _fixedStep, _bodyMass, _bodyDamping, _bodyGravity, _jointBreakForce,
                     _targetingRange, _targetingRadius, _targetingAngle, _maximumTargets }
                 .Concat(_bodyMinimum).Concat(_bodyMaximum))
            item.ValueChanged += _ => InvalidatePreviewUx6D();
        foreach (var item in new[] { _projectileMode, _bodyMode, _bodyNetwork, _jointMode,
                     _jointBodyA, _jointBodyB, _targetingMode })
            item.ItemSelected += _ => InvalidatePreviewUx6D();
        foreach (var item in new[] { _flightInput, _projectileVfx, _impactVfx, _collisionMask,
                     _projectileAudit, _bodyName, _jointName, _targetingInput, _previewColor })
            item.TextChanged += _ => InvalidatePreviewUx6D();
        _requiredTags.TextChanged += InvalidatePreviewUx6D;
        _blockedTags.TextChanged += InvalidatePreviewUx6D;
    }

    private void UpdateBoundsUx6D()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.68f : viewport.X >= 2_300 ? 1.22f : 1f;
        var width = Math.Clamp((viewport.X - 48f) / scale, 1_180f, 1_650f);
        var height = Math.Clamp((viewport.Y - 38f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(16f, (viewport.Y - height * scale) * .46f));
    }

    private VBoxContainer ScrollTabUx6D(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1_060, 0),
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content); _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private static GridContainer GridUx6D(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 9);
        grid.AddThemeConstantOverride("v_separation", 7);
        return grid;
    }

    private static SpinBox NumberUx6D(double min, double max, double step,
        bool allowGreater = false, bool allowLesser = false) =>
        StudioUx2EnvBControls.Number(min, max, step, allowGreater, allowLesser);

    private static SpinBox AxisUx6D() => NumberUx6D(-1_000_000, 1_000_000, .1, true, true);

    private static VBoxContainer FieldUx6D(string label, Control control, int minimum = 118) =>
        StudioUx2EnvBControls.Field(label, control, minimum);

    private static VBoxContainer VectorFieldUx6D(string label, IReadOnlyList<SpinBox> values)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        for (var index = 0; index < values.Count; index++)
            row.AddChild(FieldUx6D(new[] { "X", "Y", "Z" }[index], values[index], 92));
        return FieldUx6D(label, row, 330);
    }

    private static VBoxContainer ReviewColumnUx6D(HBoxContainer host, string title)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 0),
        };
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body); column.AddChild(scroll); host.AddChild(column);
        return body;
    }

    private static IReadOnlyList<string> LinesUx6D(string text) => (text ?? string.Empty)
        .Replace("\r", string.Empty).Split('\n').Select(item => item.Trim())
        .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    private static string HumanValueUx6D(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Sans nom";
        if (Guid.TryParse(value, out _)) return "Identité héritée protégée";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string HumanBodyModeUx6D(PhysicsBodyMode mode) => mode switch
    {
        PhysicsBodyMode.Static => "statique",
        PhysicsBodyMode.Kinematic => "cinématique",
        PhysicsBodyMode.Rigid => "dynamique",
        _ => "mode inconnu",
    };

    private static string HumanJointModeUx6D(PhysicsJointMode mode) => mode switch
    {
        PhysicsJointMode.Fixed => "fixe",
        PhysicsJointMode.Hinge => "charnière",
        PhysicsJointMode.Slider => "glissière",
        PhysicsJointMode.Spring => "ressort",
        _ => "mode inconnu",
    };

    private static int NetworkIndexUx6D(string value) => value switch
    {
        "server" => 0,
        "owner" => 1,
        "local-preview" => 2,
        _ => 0,
    };

    private static string NetworkValueUx6D(int index) => index switch
    {
        1 => "owner",
        2 => "local-preview",
        _ => "server",
    };

    private static double DisplayUx6D(float value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static Vector3Value ReadVectorUx6D(IReadOnlyList<SpinBox> values) =>
        new((float)values[0].Value, (float)values[1].Value, (float)values[2].Value);

    private static void SetVectorUx6D(IReadOnlyList<SpinBox> controls, Vector3Value value)
    {
        controls[0].Value = DisplayUx6D(value.X);
        controls[1].Value = DisplayUx6D(value.Y);
        controls[2].Value = DisplayUx6D(value.Z);
    }

    private static void ClearChildrenUx6D(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private void SetStatusUx6D(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static IEnumerable<string> DescendantTextUx6D(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var nested in DescendantTextUx6D(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx6D(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6D();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close(); GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) Close();
    }
}
