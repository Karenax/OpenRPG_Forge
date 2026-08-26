using System.Globalization;
using Forge.Domain.Gameplay;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioGameplayFoundationWorkbenchUx6B : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6BEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profile;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly CheckBox _movementEnabled;
    private readonly CheckBox _movementRequired;
    private readonly SpinBox _walk;
    private readonly SpinBox _run;
    private readonly SpinBox _acceleration;
    private readonly SpinBox _deceleration;
    private readonly SpinBox _slope;
    private readonly SpinBox _step;
    private readonly SpinBox _jump;
    private readonly SpinBox _crouch;
    private readonly CheckBox _rootMotion;
    private readonly CheckBox _prediction;

    private readonly CheckBox _interactionEnabled;
    private readonly CheckBox _interactionRequired;
    private readonly OptionButton _interactionAction;
    private readonly OptionButton _targeting;
    private readonly SpinBox _focusDistance;
    private readonly SpinBox _viewAngle;
    private readonly CheckBox _showPrompt;
    private readonly OptionButton _verbChoice;
    private readonly LineEdit _verbKey;
    private readonly LineEdit _verbLabel;
    private readonly LineEdit _verbCommand;
    private readonly SpinBox _verbHold;
    private readonly TextEdit _verbTags;
    private readonly CheckBox _verbAuthority;

    private readonly CheckBox _camerasRequired;
    private readonly OptionButton _defaultCamera;
    private readonly OptionButton _cameraChoice;
    private readonly LineEdit _cameraKey;
    private readonly LineEdit _cameraName;
    private readonly OptionButton _cameraMode;
    private readonly SpinBox _fov;
    private readonly SpinBox _distance;
    private readonly SpinBox _minDistance;
    private readonly SpinBox _maxDistance;
    private readonly SpinBox _pitch;
    private readonly SpinBox _minimumPitch;
    private readonly SpinBox _maximumPitch;
    private readonly SpinBox _focusX;
    private readonly SpinBox _focusY;
    private readonly SpinBox _focusZ;
    private readonly CheckBox _collision;
    private readonly CheckBox _alignFacing;
    private readonly SpinBox _terrainClearance;
    private readonly SpinBox _terrainProbeSamples;
    private readonly SpinBox _initialYaw;
    private readonly OptionButton _occlusionStrategy;
    private readonly SpinBox _collisionRetractSharpness;
    private readonly SpinBox _collisionReleaseSharpness;
    private readonly SpinBox _worldLabelNearDistance;
    private readonly SpinBox _worldLabelFarDistance;
    private readonly SpinBox _worldLabelMaximumDistance;
    private readonly SpinBox _worldLabelNearScale;
    private readonly SpinBox _worldLabelFarScale;
    private readonly CheckBox _hideLocalHeroLabelInFirstPerson;

    private readonly CheckBox _inputRequired;
    private readonly CheckBox _allowRebinding;
    private readonly CheckBox _perPlayer;
    private readonly TextEdit _contexts;
    private readonly OptionButton _actionChoice;
    private readonly LineEdit _actionKey;
    private readonly LineEdit _actionLabel;
    private readonly LineEdit _actionContext;
    private readonly CheckBox _actionAccessible;
    private readonly OptionButton _bindingChoice;
    private readonly OptionButton _bindingAction;
    private readonly OptionButton _bindingDevice;
    private readonly LineEdit _bindingValue;
    private readonly LineEdit _bindingGlyph;
    private readonly CheckBox _bindingPrimary;

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

    private IReadOnlyList<StudioUx6BProfileChoice> _choices = [];
    private StudioUx6BDraft? _draft;
    private List<StudioUx6BVerbDraft> _verbs = [];
    private List<StudioUx6BCameraDraft> _cameras = [];
    private List<StudioUx6BActionDraft> _actions = [];
    private List<StudioUx6BBindingDraft> _bindings = [];
    private int _verbIndex = -1;
    private int _cameraIndex = -1;
    private int _actionIndex = -1;
    private int _bindingIndex = -1;
    private bool _updating;
    private bool _previewReady;
    private string? _previewFingerprint;
    private StudioUx6BPreview? _previewResult;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? GameplayFoundationChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _draft is not null && _profile.Selected > 0;
    internal bool PreviewReady => _previewReady && _previewResult is
    { Succeeded: true, CanonicalDryRun: true, RuntimeClaimsBounded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6B();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsFourWorkflows => _tabs.GetTabCount() == 4 &&
        Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle).SequenceEqual(
            new[] { "Déplacement", "Interactions", "Caméras", "Entrées" });
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6B(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Fondations du gameplay", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   text.Contains("Déplacement", StringComparison.Ordinal) &&
                   text.Contains("Interactions", StringComparison.Ordinal) &&
                   text.Contains("Caméras", StringComparison.Ordinal) &&
                   text.Contains("Entrées", StringComparison.Ordinal) && !ContainsUuidUx6B(text);
        }
    }

    internal StudioGameplayFoundationWorkbenchUx6B(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6BGameplayFoundationDialog", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("Fondations du gameplay", 18));
        headingText.AddChild(StudioStyle.Label(
            "Déplacement, interactions, caméras et entrées · une transaction canonique · aucun acteur ni nœud runtime manipulé.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _profile = StudioUx2EnvBControls.Options();
        _profile.ItemSelected += SelectProfileUx6B;
        root.AddChild(StudioUx2EnvBControls.Field("Module et zone de contexte · sélection explicite", _profile, 620));
        _selection = StudioStyle.Label("Choisissez le module et sa zone de contexte.", 10,
            StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 610),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var movementTab = ScrollTabUx6B("Déplacement");
        var movementFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _movementEnabled = StudioUx2EnvBControls.Check("Déplacement activé");
        _movementRequired = StudioUx2EnvBControls.Check("Profil requis au runtime");
        _rootMotion = StudioUx2EnvBControls.Check("Root motion");
        _prediction = StudioUx2EnvBControls.Check("Prédiction réseau configurée");
        movementFlags.AddChild(_movementEnabled); movementFlags.AddChild(_movementRequired);
        movementFlags.AddChild(_rootMotion); movementFlags.AddChild(_prediction);
        movementTab.AddChild(movementFlags);
        var speedGrid = GridUx6B(4);
        _walk = NumberUx6B(.01, 50, .05); _run = NumberUx6B(.01, 100, .05);
        _acceleration = NumberUx6B(.01, 500, .1); _deceleration = NumberUx6B(.01, 500, .1);
        speedGrid.AddChild(FieldUx6B("Marche (m/s)", _walk)); speedGrid.AddChild(FieldUx6B("Course (m/s)", _run));
        speedGrid.AddChild(FieldUx6B("Accélération", _acceleration)); speedGrid.AddChild(FieldUx6B("Décélération", _deceleration));
        movementTab.AddChild(StudioUx2EnvBControls.Card("Ux6BMovementSpeed", "Vitesse & inertie",
            "Le moteur runtime résout l’accélération et la décélération sans déplacer d’acteur dans Studio.", speedGrid));
        var terrainGrid = GridUx6B(4);
        _slope = NumberUx6B(0, 89, .5); _step = NumberUx6B(0, 5, .05);
        _jump = NumberUx6B(0, 100, .1); _crouch = NumberUx6B(.01, 1, .01);
        terrainGrid.AddChild(FieldUx6B("Pente maximale (°)", _slope));
        terrainGrid.AddChild(FieldUx6B("Marche d’escalier (m)", _step));
        terrainGrid.AddChild(FieldUx6B("Impulsion de saut", _jump));
        terrainGrid.AddChild(FieldUx6B("Vitesse accroupie", _crouch));
        movementTab.AddChild(StudioUx2EnvBControls.Card("Ux6BMovementTerrain", "Sol, saut & posture",
            "Valeurs brutes conservées; aucune grille ou normalisation silencieuse.", terrainGrid));

        var interactionTab = ScrollTabUx6B("Interactions");
        var interactionFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _interactionEnabled = StudioUx2EnvBControls.Check("Interactions activées");
        _interactionRequired = StudioUx2EnvBControls.Check("Profil requis");
        _showPrompt = StudioUx2EnvBControls.Check("Afficher l’invite");
        interactionFlags.AddChild(_interactionEnabled); interactionFlags.AddChild(_interactionRequired);
        interactionFlags.AddChild(_showPrompt); interactionTab.AddChild(interactionFlags);
        var focusGrid = GridUx6B(4);
        _interactionAction = StudioUx2EnvBControls.Options();
        _targeting = StudioUx2EnvBControls.Options("Plus proche", "Cône de vue", "Curseur");
        _focusDistance = NumberUx6B(.01, 100, .05); _viewAngle = NumberUx6B(.01, 360, 1);
        focusGrid.AddChild(FieldUx6B("Action d’entrée", _interactionAction));
        focusGrid.AddChild(FieldUx6B("Ciblage", _targeting));
        focusGrid.AddChild(FieldUx6B("Portée (m)", _focusDistance));
        focusGrid.AddChild(FieldUx6B("Angle de vue (°)", _viewAngle));
        interactionTab.AddChild(StudioUx2EnvBControls.Card("Ux6BInteractionFocus", "Focus contextuel",
            "La sélection déterministe filtre portée, angle, tags et verbes disponibles.", focusGrid));
        var verbHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _verbChoice = StudioUx2EnvBControls.Options(); _verbChoice.ItemSelected += SelectVerbUx6B;
        verbHeader.AddChild(FieldUx6B("Verbe sélectionné", _verbChoice, 360));
        var addVerb = StudioStyle.Button("Ajouter un verbe", compact: true); addVerb.Pressed += AddVerbUx6B;
        var removeVerb = StudioStyle.Button("Retirer", compact: true); removeVerb.Pressed += RemoveVerbUx6B;
        verbHeader.AddChild(addVerb); verbHeader.AddChild(removeVerb); interactionTab.AddChild(verbHeader);
        var verbGrid = GridUx6B(4);
        _verbKey = StudioUx2EnvBControls.Text("inspect"); _verbLabel = StudioUx2EnvBControls.Text("Examiner");
        _verbCommand = StudioUx2EnvBControls.Text("inspect"); _verbHold = NumberUx6B(0, 30, .05);
        verbGrid.AddChild(FieldUx6B("Clé", _verbKey)); verbGrid.AddChild(FieldUx6B("Libellé humain", _verbLabel));
        verbGrid.AddChild(FieldUx6B("Commande", _verbCommand)); verbGrid.AddChild(FieldUx6B("Maintien (s)", _verbHold));
        interactionTab.AddChild(verbGrid);
        _verbTags = StudioUx2EnvBControls.Lines("Un tag cible par ligne", 82);
        _verbAuthority = StudioUx2EnvBControls.Check("Autorité serveur requise");
        interactionTab.AddChild(FieldUx6B("Cibles", _verbTags)); interactionTab.AddChild(_verbAuthority);

        var cameraTab = ScrollTabUx6B("Caméras");
        _camerasRequired = StudioUx2EnvBControls.Check("Catalogue requis au runtime");
        cameraTab.AddChild(_camerasRequired);
        var cameraHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _cameraChoice = StudioUx2EnvBControls.Options(); _cameraChoice.ItemSelected += SelectCameraUx6B;
        _defaultCamera = StudioUx2EnvBControls.Options();
        cameraHeader.AddChild(FieldUx6B("Profil sélectionné", _cameraChoice, 300));
        cameraHeader.AddChild(FieldUx6B("Profil par défaut", _defaultCamera, 300));
        var addCamera = StudioStyle.Button("Ajouter", compact: true); addCamera.Pressed += AddCameraUx6B;
        var removeCamera = StudioStyle.Button("Retirer", compact: true); removeCamera.Pressed += RemoveCameraUx6B;
        cameraHeader.AddChild(addCamera); cameraHeader.AddChild(removeCamera); cameraTab.AddChild(cameraHeader);
        var cameraIdentity = GridUx6B(3);
        _cameraKey = StudioUx2EnvBControls.Text("orbit"); _cameraName = StudioUx2EnvBControls.Text("Orbite");
        _cameraMode = StudioUx2EnvBControls.Options("Isométrique", "Orbite", "Épaule", "Première personne",
            "Vue du dessus", "Libre", "Véhicule", "Cinématique");
        cameraIdentity.AddChild(FieldUx6B("Clé", _cameraKey)); cameraIdentity.AddChild(FieldUx6B("Nom humain", _cameraName));
        cameraIdentity.AddChild(FieldUx6B("Mode", _cameraMode)); cameraTab.AddChild(cameraIdentity);
        var optics = GridUx6B(4);
        _fov = NumberUx6B(20, 140, .5); _distance = NumberUx6B(0, 1000, .1);
        _minDistance = NumberUx6B(0, 1000, .1); _maxDistance = NumberUx6B(0, 1000, .1);
        _pitch = NumberUx6B(-89, 89, .5);
        _minimumPitch = NumberUx6B(-89, 89, .5, true, true);
        _maximumPitch = NumberUx6B(-89, 89, .5, true, true);
        optics.AddChild(FieldUx6B("Champ de vue (°)", _fov)); optics.AddChild(FieldUx6B("Distance", _distance));
        optics.AddChild(FieldUx6B("Minimum", _minDistance)); optics.AddChild(FieldUx6B("Maximum", _maxDistance));
        optics.AddChild(FieldUx6B("Inclinaison initiale (°)", _pitch));
        optics.AddChild(FieldUx6B("Limite vers le ciel (°)", _minimumPitch));
        optics.AddChild(FieldUx6B("Limite vers le sol (°)", _maximumPitch));
        cameraTab.AddChild(StudioUx2EnvBControls.Card("Ux6BCameraOptics", "Optique & distance",
            "Une limite vers le ciel négative autorise une vue ascendante au ras du terrain. L’aperçu résout le profil; aucune Camera3D n’est déplacée.", optics));
        var focusOffset = GridUx6B(3);
        _focusX = NumberUx6B(-10000, 10000, .05, true, true);
        _focusY = NumberUx6B(-10000, 10000, .05, true, true);
        _focusZ = NumberUx6B(-10000, 10000, .05, true, true);
        focusOffset.AddChild(FieldUx6B("Décalage X", _focusX)); focusOffset.AddChild(FieldUx6B("Décalage Y", _focusY));
        focusOffset.AddChild(FieldUx6B("Décalage Z", _focusZ)); cameraTab.AddChild(focusOffset);
        _collision = StudioUx2EnvBControls.Check("Évitement de collision");
        _alignFacing = StudioUx2EnvBControls.Check("Aligner le personnage");
        cameraTab.AddChild(_collision); cameraTab.AddChild(_alignFacing);
        var terrainCollision = GridUx6B(3);
        _terrainClearance = NumberUx6B(.05, 20, .05);
        _terrainProbeSamples = NumberUx6B(4, 128, 1);
        _initialYaw = NumberUx6B(-360, 360, 1, true, true);
        terrainCollision.AddChild(FieldUx6B("Marge au-dessus du terrain", _terrainClearance));
        terrainCollision.AddChild(FieldUx6B("Échantillons du relief", _terrainProbeSamples));
        terrainCollision.AddChild(FieldUx6B("Orientation initiale (°)", _initialYaw));
        cameraTab.AddChild(StudioUx2EnvBControls.Card("Ux6BCameraTerrain", "Relief extérieur",
            "La caméra conserve sa distance et se relève au-dessus des pentes selon ces valeurs du module.",
            terrainCollision));
        var occlusion = GridUx6B(3);
        _occlusionStrategy = StudioUx2EnvBControls.Options("Retrait de la caméra", "Fondu de l’obstacle",
            "Masquage de l’obstacle");
        _collisionRetractSharpness = NumberUx6B(.05, 100, .05);
        _collisionReleaseSharpness = NumberUx6B(.05, 100, .05);
        occlusion.AddChild(FieldUx6B("Stratégie", _occlusionStrategy));
        occlusion.AddChild(FieldUx6B("Vitesse de retrait", _collisionRetractSharpness));
        occlusion.AddChild(FieldUx6B("Vitesse de libération", _collisionReleaseSharpness));
        cameraTab.AddChild(StudioUx2EnvBControls.Card("Ux6BCameraOcclusion", "Obstacle entre joueur et caméra",
            "Le retrait rapproche la caméra; le fondu et le masquage gardent le recul et traitent seulement l’obstacle courant.",
            occlusion));
        var worldLabels = GridUx6B(3);
        _worldLabelNearDistance = NumberUx6B(0, 100, .25);
        _worldLabelFarDistance = NumberUx6B(.25, 250, .25);
        _worldLabelMaximumDistance = NumberUx6B(.5, 500, .25);
        _worldLabelNearScale = NumberUx6B(.05, 4, .05);
        _worldLabelFarScale = NumberUx6B(.05, 4, .05);
        _hideLocalHeroLabelInFirstPerson = StudioUx2EnvBControls.Check("Masquer le label du héros local en première personne");
        worldLabels.AddChild(FieldUx6B("Distance proche", _worldLabelNearDistance));
        worldLabels.AddChild(FieldUx6B("Distance lisible", _worldLabelFarDistance));
        worldLabels.AddChild(FieldUx6B("Distance de masquage", _worldLabelMaximumDistance));
        worldLabels.AddChild(FieldUx6B("Échelle proche", _worldLabelNearScale));
        worldLabels.AddChild(FieldUx6B("Échelle distante", _worldLabelFarScale));
        worldLabels.AddChild(_hideLocalHeroLabelInFirstPerson);
        cameraTab.AddChild(StudioUx2EnvBControls.Card("Ux6BWorldLabels", "Labels du monde",
            "Les labels proches rétrécissent, restent lisibles à distance puis disparaissent progressivement.",
            worldLabels));

        var inputTab = ScrollTabUx6B("Entrées");
        var inputFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inputRequired = StudioUx2EnvBControls.Check("Profil requis");
        _allowRebinding = StudioUx2EnvBControls.Check("Reconfiguration autorisée");
        _perPlayer = StudioUx2EnvBControls.Check("Réglages locaux par joueur");
        inputFlags.AddChild(_inputRequired); inputFlags.AddChild(_allowRebinding); inputFlags.AddChild(_perPlayer);
        inputTab.AddChild(inputFlags);
        _contexts = StudioUx2EnvBControls.Lines("Un contexte par ligne : exploration\ndialogue\ncombat\nsystem", 80);
        inputTab.AddChild(FieldUx6B("Contextes", _contexts));
        var actionHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _actionChoice = StudioUx2EnvBControls.Options(); _actionChoice.ItemSelected += SelectActionUx6B;
        actionHeader.AddChild(FieldUx6B("Action sélectionnée", _actionChoice, 330));
        var addAction = StudioStyle.Button("Ajouter une action", compact: true); addAction.Pressed += AddActionUx6B;
        var removeAction = StudioStyle.Button("Retirer", compact: true); removeAction.Pressed += RemoveActionUx6B;
        actionHeader.AddChild(addAction); actionHeader.AddChild(removeAction); inputTab.AddChild(actionHeader);
        var actionGrid = GridUx6B(3);
        _actionKey = StudioUx2EnvBControls.Text("interact"); _actionLabel = StudioUx2EnvBControls.Text("Interagir");
        _actionContext = StudioUx2EnvBControls.Text("exploration");
        actionGrid.AddChild(FieldUx6B("Clé", _actionKey)); actionGrid.AddChild(FieldUx6B("Libellé", _actionLabel));
        actionGrid.AddChild(FieldUx6B("Contexte", _actionContext)); inputTab.AddChild(actionGrid);
        _actionAccessible = StudioUx2EnvBControls.Check("Liaison requise pour l’accessibilité");
        inputTab.AddChild(_actionAccessible);
        var bindingHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bindingChoice = StudioUx2EnvBControls.Options(); _bindingChoice.ItemSelected += SelectBindingUx6B;
        bindingHeader.AddChild(FieldUx6B("Liaison sélectionnée", _bindingChoice, 330));
        var addBinding = StudioStyle.Button("Ajouter une liaison", compact: true); addBinding.Pressed += AddBindingUx6B;
        var removeBinding = StudioStyle.Button("Retirer", compact: true); removeBinding.Pressed += RemoveBindingUx6B;
        bindingHeader.AddChild(addBinding); bindingHeader.AddChild(removeBinding); inputTab.AddChild(bindingHeader);
        var bindingGrid = GridUx6B(4);
        _bindingAction = StudioUx2EnvBControls.Options();
        _bindingDevice = StudioUx2EnvBControls.Options("Clavier / souris", "Manette");
        _bindingValue = StudioUx2EnvBControls.Text("E"); _bindingGlyph = StudioUx2EnvBControls.Text("key-e");
        bindingGrid.AddChild(FieldUx6B("Action", _bindingAction)); bindingGrid.AddChild(FieldUx6B("Périphérique", _bindingDevice));
        bindingGrid.AddChild(FieldUx6B("Commande physique", _bindingValue)); bindingGrid.AddChild(FieldUx6B("Glyphe", _bindingGlyph));
        inputTab.AddChild(bindingGrid);
        _bindingPrimary = StudioUx2EnvBControls.Check("Liaison principale"); inputTab.AddChild(_bindingPrimary);

        var review = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 142),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _diff = ReviewColumnUx6B(review, "DIFF HUMAIN");
        _runtime = ReviewColumnUx6B(review, "APERÇU RUNTIME PUR");
        _diagnostics = ReviewColumnUx6B(review, "DIAGNOSTICS");
        root.AddChild(review);
        _summary = StudioStyle.Label("Aucun aperçu sec frais.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Aperçu sec", primary: true); _preview.Pressed += PreviewUx6B;
        _apply = StudioStyle.Button("Appliquer"); _apply.Pressed += ApplyUx6B;
        _undo = StudioStyle.Button("Annuler"); _undo.Pressed += UndoUx6B;
        _redo = StudioStyle.Button("Rétablir"); _redo.Pressed += RedoUx6B;
        _save = StudioStyle.Button("Enregistrer"); _save.Pressed += SaveUx6B;
        actions.AddChild(_preview); actions.AddChild(_apply); actions.AddChild(_undo);
        actions.AddChild(_redo); actions.AddChild(_save); root.AddChild(actions);
        ResetControlsUx6B();
    }

    internal void Open() => OpenBoundUx6B(_sourceSession, false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() => OpenBoundUx6B(_sourceSession, false, cancelApproval: false);

    private void OpenBoundUx6B(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        if (cancelApproval) _session.CancelUx6BApproval();
        _session = session;
        _choices = session.GameplayFoundationChoicesUx6B;
        _profile.Clear();
        _profile.AddItem("Choisir explicitement le module et sa zone…");
        foreach (var choice in _choices) _profile.AddItem(choice.Label);
        _profile.Select(0);
        _draft = null;
        _selection.Text = evidence
            ? "Session de preuve isolée · choisissez le contexte copié; la source reste strictement inchangée."
            : "Choisissez le module et sa zone de contexte.";
        ResetControlsUx6B();
        Visible = true;
        MoveToFront();
        UpdateBoundsUx6B();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null &&
            ReferenceEquals(_session, _sourceSession)) return;
        if (_evidenceSession is not null || _evidenceRoot is not null)
        {
            ReleaseEvidenceUx6B();
        }
        else
        {
            _session.CancelUx6BApproval();
            _session = _sourceSession;
        }
        Visible = false;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidenceUx6B();
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidenceUx6B();
        _sourceSnapshot = CaptureSourceUx6B();
        _evidenceReleased = false;
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-b-{Guid.NewGuid():N}");
        var created = ProjectSession.Create(_evidenceRoot, _sourceSession.Project);
        if (!created.Succeeded) return ReleaseEvidenceUx6B() && false;
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded) return ReleaseEvidenceUx6B() && false;
        OpenBoundUx6B(_evidenceSession, true);
        if (_choices.Count == 0) return ReleaseEvidenceUx6B() && false;
        _profile.Select(1);
        SelectProfileUx6B(1);
        if (_draft is null) return ReleaseEvidenceUx6B() && false;
        ConfigureEvidenceUx6B();
        PreviewUx6B();
        var ready = PreviewReady && ShowsFourWorkflows && ShowsHumanSurface && EvidenceSourceUntouched;
        if (!ready) ReleaseEvidenceUx6B();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceUx6B();
        var prepared = false;
        try
        {
            prepared = PrepareEvidence("ux6-b-gameplay-foundation");
            if (!prepared || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady) return false;
            var before = _evidenceSession.Project!;
            var history = _evidenceSession.History!;
            var journalCount = history.Journal.Count;
            ApplyUx6B();
            var applied = history.Journal.Count == journalCount + 1 &&
                          history.Journal.Last().CommandName == "set_gameplay_control_profile" &&
                          !ReferenceEquals(before, _evidenceSession.Project);
            var after = _evidenceSession.Project!;
            UndoUx6B();
            var undone = ReferenceEquals(before, _evidenceSession.Project);
            RedoUx6B();
            var redone = ReferenceEquals(after, _evidenceSession.Project);
            var json = ProjectJsonSerializer.Serialize(after);
            SaveUx6B();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded && reopened.Project is not null &&
                            string.Equals(json, ProjectJsonSerializer.Serialize(reopened.Project),
                                StringComparison.Ordinal);
            reopened.Close();
            return applied && undone && redone && persisted && SourceMatchesUx6B(source);
        }
        finally
        {
            if (prepared || _evidenceSession is not null || _evidenceRoot is not null) CloseEvidence();
            _sourceSession.RestoreGameplayFoundationApprovalUx6B(source.Approval);
        }
    }

    private void SelectProfileUx6B(long selected)
    {
        if (_updating) return;
        _session.CancelUx6BApproval();
        _previewReady = false;
        _previewFingerprint = null;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ResetControlsUx6B();
            return;
        }
        _draft = _session.CreateGameplayFoundationDraftUx6B(_choices[(int)selected - 1].ContextAreaId, true);
        if (_draft is null)
        {
            SetStatusUx6B("Le profil gameplay ne peut pas être chargé.", true);
            return;
        }
        PopulateUx6B(_draft);
        _selection.Text = $"{_choices[(int)selected - 1].Label} · sauvegarde et profils voisins protégés.";
        SetStatusUx6B("Profil chargé · modifiez puis lancez l’aperçu sec.", false);
    }

    private void PopulateUx6B(StudioUx6BDraft draft)
    {
        _updating = true;
        _movementEnabled.ButtonPressed = draft.Movement.Enabled;
        _movementRequired.ButtonPressed = draft.Movement.Required;
        _walk.Value = DisplayUx6B(draft.Movement.WalkSpeed); _run.Value = DisplayUx6B(draft.Movement.RunSpeed);
        _acceleration.Value = DisplayUx6B(draft.Movement.Acceleration);
        _deceleration.Value = DisplayUx6B(draft.Movement.Deceleration);
        _slope.Value = DisplayUx6B(draft.Movement.MaximumSlopeDegrees);
        _step.Value = DisplayUx6B(draft.Movement.StepHeight);
        _jump.Value = DisplayUx6B(draft.Movement.JumpVelocity);
        _crouch.Value = DisplayUx6B(draft.Movement.CrouchSpeedMultiplier);
        _rootMotion.ButtonPressed = draft.Movement.RootMotion;
        _prediction.ButtonPressed = draft.Movement.NetworkPrediction;
        _interactionEnabled.ButtonPressed = draft.Interaction.Enabled;
        _interactionRequired.ButtonPressed = draft.Interaction.Required;
        _targeting.Select((int)draft.Interaction.Targeting);
        _focusDistance.Value = DisplayUx6B(draft.Interaction.FocusDistance);
        _viewAngle.Value = DisplayUx6B(draft.Interaction.ViewAngleDegrees);
        _showPrompt.ButtonPressed = draft.Interaction.ShowPrompt;
        _camerasRequired.ButtonPressed = draft.Cameras.Required;
        _inputRequired.ButtonPressed = draft.Input.Required;
        _allowRebinding.ButtonPressed = draft.Input.AllowRebinding;
        _perPlayer.ButtonPressed = draft.Input.PerPlayerLocal;
        _contexts.Text = string.Join('\n', draft.Input.Contexts);
        _verbs = draft.Interaction.Verbs.ToList(); _cameras = draft.Cameras.Profiles.ToList();
        _actions = draft.Input.Actions.ToList(); _bindings = draft.Input.Bindings.ToList();
        RebuildCollectionsUx6B(draft.Interaction.InputAction, draft.Cameras.DefaultProfileKey);
        _updating = false;
        UpdateButtonsUx6B();
    }

    private void RebuildCollectionsUx6B(string interactionAction, string defaultCamera)
    {
        _verbChoice.Clear(); foreach (var item in _verbs) _verbChoice.AddItem(HumanValueUx6B(item.Label));
        _cameraChoice.Clear(); foreach (var item in _cameras) _cameraChoice.AddItem(HumanValueUx6B(item.DisplayName));
        _actionChoice.Clear(); foreach (var item in _actions) _actionChoice.AddItem(HumanValueUx6B(item.Label));
        _bindingChoice.Clear(); foreach (var item in _bindings)
            _bindingChoice.AddItem($"{HumanValueUx6B(item.ActionKey)} · {HumanValueUx6B(item.Device.ToString())} · {HumanValueUx6B(item.Binding)}");
        RefreshActionReferencesUx6B(interactionAction);
        _defaultCamera.Clear(); foreach (var item in _cameras) _defaultCamera.AddItem(HumanValueUx6B(item.DisplayName));
        var defaultIndex = _cameras.FindIndex(item => item.Key == defaultCamera);
        if (defaultIndex >= 0) _defaultCamera.Select(defaultIndex);
        _verbIndex = _verbs.Count > 0 ? 0 : -1; _cameraIndex = _cameras.Count > 0 ? 0 : -1;
        _actionIndex = _actions.Count > 0 ? 0 : -1; _bindingIndex = _bindings.Count > 0 ? 0 : -1;
        if (_verbIndex >= 0) { _verbChoice.Select(0); PopulateVerbUx6B(); }
        if (_cameraIndex >= 0) { _cameraChoice.Select(0); PopulateCameraUx6B(); }
        if (_actionIndex >= 0) { _actionChoice.Select(0); PopulateActionUx6B(); }
        if (_bindingIndex >= 0) { _bindingChoice.Select(0); PopulateBindingUx6B(); }
    }

    private void RefreshActionReferencesUx6B(string interactionAction)
    {
        _interactionAction.Clear(); _bindingAction.Clear();
        foreach (var item in _actions)
        {
            _interactionAction.AddItem(HumanValueUx6B(item.Label));
            _bindingAction.AddItem(HumanValueUx6B(item.Label));
        }
        var interactionIndex = _actions.FindIndex(item => item.Key == interactionAction);
        if (interactionIndex >= 0) _interactionAction.Select(interactionIndex);
    }

    private StudioUx6BDraft? ReadDraftUx6B()
    {
        if (_draft is null) return null;
        FlushVerbUx6B(); FlushCameraUx6B(); FlushActionUx6B(); FlushBindingUx6B();
        var interactionAction = _interactionAction.Selected >= 0 && _interactionAction.Selected < _actions.Count
            ? _actions[_interactionAction.Selected].Key : string.Empty;
        var defaultCamera = _defaultCamera.Selected >= 0 && _defaultCamera.Selected < _cameras.Count
            ? _cameras[_defaultCamera.Selected].Key : string.Empty;
        return _draft with
        {
            Movement = new StudioUx6BMovementDraft(_movementEnabled.ButtonPressed, _movementRequired.ButtonPressed,
                (float)_walk.Value, (float)_run.Value, (float)_acceleration.Value, (float)_deceleration.Value,
                (float)_slope.Value, (float)_step.Value, (float)_jump.Value, (float)_crouch.Value,
                _rootMotion.ButtonPressed, _prediction.ButtonPressed),
            Interaction = new StudioUx6BInteractionDraft(_interactionEnabled.ButtonPressed,
                _interactionRequired.ButtonPressed, interactionAction,
                (InteractionTargetingMode)_targeting.Selected, (float)_focusDistance.Value,
                (float)_viewAngle.Value, _showPrompt.ButtonPressed, _verbs.ToArray()),
            Cameras = new StudioUx6BCameraCatalogDraft(_camerasRequired.ButtonPressed,
                defaultCamera, _cameras.ToArray()),
            Input = new StudioUx6BInputDraft(_inputRequired.ButtonPressed, _allowRebinding.ButtonPressed,
                _perPlayer.ButtonPressed, LinesUx6B(_contexts.Text), _actions.ToArray(), _bindings.ToArray()),
        };
    }

    private void PreviewUx6B()
    {
        var draft = ReadDraftUx6B();
        if (draft is null) { SetStatusUx6B("Choisissez explicitement un contexte.", true); return; }
        _previewResult = _session.PreviewGameplayFoundationUx6B(draft);
        _previewReady = _previewResult.Succeeded;
        _previewFingerprint = _previewReady ? _session.GameplayFoundationDraftFingerprintUx6B(draft) : null;
        RenderPreviewUx6B(_previewResult);
        SetStatusUx6B(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtonsUx6B();
    }

    private void ApplyUx6B()
    {
        var current = ReadDraftUx6B();
        var currentFingerprint = current is null ? string.Empty :
            _session.GameplayFoundationDraftFingerprintUx6B(current);
        if (!_previewReady || !string.Equals(currentFingerprint, _previewFingerprint, StringComparison.Ordinal))
        {
            _session.CancelUx6BApproval();
            _previewReady = false;
            SetStatusUx6B("Le brouillon a changé; relancez l’aperçu sec.", true);
            UpdateButtonsUx6B();
            return;
        }
        var result = _session.ApplyApprovedGameplayFoundationUx6B();
        SetStatusUx6B(result.Message, !result.Succeeded);
        _previewReady = false; _previewFingerprint = null;
        if (result.Succeeded)
        {
            GameplayFoundationChanged?.Invoke();
            ReloadSelectedUx6B();
        }
        UpdateButtonsUx6B();
    }

    private void UndoUx6B()
    {
        var result = _session.Undo();
        SetStatusUx6B(result.Message, !result.Succeeded);
        if (result.Succeeded) { GameplayFoundationChanged?.Invoke(); ReloadSelectedUx6B(); }
    }

    private void RedoUx6B()
    {
        var result = _session.Redo();
        SetStatusUx6B(result.Message, !result.Succeeded);
        if (result.Succeeded) { GameplayFoundationChanged?.Invoke(); ReloadSelectedUx6B(); }
    }

    private void SaveUx6B()
    {
        var result = _session.Save();
        SetStatusUx6B(result.Message, !result.Succeeded);
        UpdateButtonsUx6B();
    }

    private void ReloadSelectedUx6B()
    {
        var selected = _profile.Selected;
        _session.CancelUx6BApproval();
        _previewReady = false; _previewFingerprint = null;
        if (selected > 0) SelectProfileUx6B(selected);
    }

    private void ConfigureEvidenceUx6B()
    {
        _run.Value = Math.Min(99, _run.Value + .25);
        if (_verbIndex >= 0) _verbHold.Value = Math.Min(29.9, _verbHold.Value + .1);
        if (_cameraIndex >= 0) _fov.Value = Math.Min(139, _fov.Value + 1);
        if (_bindingIndex >= 0)
        {
            _bindingValue.Text += " + Studio";
            _bindingGlyph.Text += "-studio";
        }
    }

    private void SelectVerbUx6B(long selected)
    {
        if (_updating) return; FlushVerbUx6B(); _verbIndex = (int)selected; PopulateVerbUx6B(); InvalidatePreviewUx6B();
    }
    private void SelectCameraUx6B(long selected)
    {
        if (_updating) return; FlushCameraUx6B(); _cameraIndex = (int)selected; PopulateCameraUx6B(); InvalidatePreviewUx6B();
    }
    private void SelectActionUx6B(long selected)
    {
        if (_updating) return; FlushActionUx6B(); _actionIndex = (int)selected; PopulateActionUx6B(); InvalidatePreviewUx6B();
    }
    private void SelectBindingUx6B(long selected)
    {
        if (_updating) return; FlushBindingUx6B(); _bindingIndex = (int)selected; PopulateBindingUx6B(); InvalidatePreviewUx6B();
    }

    private void PopulateVerbUx6B()
    {
        if (_verbIndex < 0 || _verbIndex >= _verbs.Count) return;
        _updating = true; var item = _verbs[_verbIndex];
        _verbKey.Text = item.Key; _verbLabel.Text = item.Label; _verbCommand.Text = item.Command;
        _verbHold.Value = DisplayUx6B(item.HoldSeconds); _verbTags.Text = string.Join('\n', item.TargetTags);
        _verbAuthority.ButtonPressed = item.ServerAuthoritative; _updating = false;
    }
    private void FlushVerbUx6B()
    {
        if (_updating || _verbIndex < 0 || _verbIndex >= _verbs.Count) return;
        var item = _verbs[_verbIndex];
        _verbs[_verbIndex] = item with
        {
            Key = _verbKey.Text,
            Label = _verbLabel.Text,
            Command = _verbCommand.Text,
            HoldSeconds = (float)_verbHold.Value,
            TargetTags = LinesUx6B(_verbTags.Text),
            ServerAuthoritative = _verbAuthority.ButtonPressed
        };
    }
    private void PopulateCameraUx6B()
    {
        if (_cameraIndex < 0 || _cameraIndex >= _cameras.Count) return;
        _updating = true; var item = _cameras[_cameraIndex];
        _cameraKey.Text = item.Key; _cameraName.Text = item.DisplayName; _cameraMode.Select((int)item.Mode);
        _fov.Value = DisplayUx6B(item.FieldOfViewDegrees);
        _distance.Value = DisplayUx6B(item.Distance);
        _minDistance.Value = DisplayUx6B(item.MinimumDistance);
        _maxDistance.Value = DisplayUx6B(item.MaximumDistance);
        _pitch.Value = DisplayUx6B(item.PitchDegrees);
        _minimumPitch.Value = DisplayUx6B(item.MinimumPitchDegrees);
        _maximumPitch.Value = DisplayUx6B(item.MaximumPitchDegrees);
        _focusX.Value = DisplayUx6B(item.FocusOffset.X);
        _focusY.Value = DisplayUx6B(item.FocusOffset.Y);
        _focusZ.Value = DisplayUx6B(item.FocusOffset.Z);
        _collision.ButtonPressed = item.CollisionAvoidance; _alignFacing.ButtonPressed = item.AlignCharacterFacing;
        _terrainClearance.Value = DisplayUx6B(item.TerrainClearance);
        _terrainProbeSamples.Value = item.TerrainProbeSamples;
        _initialYaw.Value = DisplayUx6B(item.InitialYawDegrees);
        _occlusionStrategy.Select((int)item.OcclusionStrategy);
        _collisionRetractSharpness.Value = DisplayUx6B(item.CollisionRetractSharpness);
        _collisionReleaseSharpness.Value = DisplayUx6B(item.CollisionReleaseSharpness);
        _worldLabelNearDistance.Value = DisplayUx6B(item.WorldLabelNearDistance);
        _worldLabelFarDistance.Value = DisplayUx6B(item.WorldLabelFarDistance);
        _worldLabelMaximumDistance.Value = DisplayUx6B(item.WorldLabelMaximumDistance);
        _worldLabelNearScale.Value = DisplayUx6B(item.WorldLabelNearScale);
        _worldLabelFarScale.Value = DisplayUx6B(item.WorldLabelFarScale);
        _hideLocalHeroLabelInFirstPerson.ButtonPressed = item.HideLocalHeroLabelInFirstPerson;
        _updating = false;
    }
    private void FlushCameraUx6B()
    {
        if (_updating || _cameraIndex < 0 || _cameraIndex >= _cameras.Count) return;
        var item = _cameras[_cameraIndex];
        _cameras[_cameraIndex] = item with
        {
            Key = _cameraKey.Text,
            DisplayName = _cameraName.Text,
            Mode = (GameplayCameraMode)_cameraMode.Selected,
            FieldOfViewDegrees = (float)_fov.Value,
            Distance = (float)_distance.Value,
            MinimumDistance = (float)_minDistance.Value,
            MaximumDistance = (float)_maxDistance.Value,
            PitchDegrees = (float)_pitch.Value,
            MinimumPitchDegrees = (float)_minimumPitch.Value,
            MaximumPitchDegrees = (float)_maximumPitch.Value,
            FocusOffset = new Vector3Value((float)_focusX.Value, (float)_focusY.Value, (float)_focusZ.Value),
            CollisionAvoidance = _collision.ButtonPressed,
            AlignCharacterFacing = _alignFacing.ButtonPressed,
            TerrainClearance = (float)_terrainClearance.Value,
            TerrainProbeSamples = checked((int)_terrainProbeSamples.Value),
            InitialYawDegrees = (float)_initialYaw.Value,
            OcclusionSchemaVersion = 1,
            OcclusionStrategy = (CameraOcclusionStrategy)_occlusionStrategy.Selected,
            CollisionRetractSharpness = (float)_collisionRetractSharpness.Value,
            CollisionReleaseSharpness = (float)_collisionReleaseSharpness.Value,
            WorldLabelSchemaVersion = 1,
            WorldLabelNearDistance = (float)_worldLabelNearDistance.Value,
            WorldLabelFarDistance = (float)_worldLabelFarDistance.Value,
            WorldLabelMaximumDistance = (float)_worldLabelMaximumDistance.Value,
            WorldLabelNearScale = (float)_worldLabelNearScale.Value,
            WorldLabelFarScale = (float)_worldLabelFarScale.Value,
            HideLocalHeroLabelInFirstPerson = _hideLocalHeroLabelInFirstPerson.ButtonPressed
        };
    }
    private void PopulateActionUx6B()
    {
        if (_actionIndex < 0 || _actionIndex >= _actions.Count) return;
        _updating = true; var item = _actions[_actionIndex];
        _actionKey.Text = item.Key; _actionLabel.Text = item.Label; _actionContext.Text = item.Context;
        _actionAccessible.ButtonPressed = item.AccessibilityRequired; _updating = false;
    }
    private void FlushActionUx6B()
    {
        if (_updating || _actionIndex < 0 || _actionIndex >= _actions.Count) return;
        var oldKey = _actions[_actionIndex].Key;
        var item = _actions[_actionIndex] with
        {
            Key = _actionKey.Text,
            Label = _actionLabel.Text,
            Context = _actionContext.Text,
            AccessibilityRequired = _actionAccessible.ButtonPressed
        };
        _actions[_actionIndex] = item;
        if (!string.Equals(oldKey, item.Key, StringComparison.Ordinal))
            _bindings = _bindings.Select(binding => binding.ActionKey == oldKey
                ? binding with { ActionKey = item.Key } : binding).ToList();
    }
    private void PopulateBindingUx6B()
    {
        if (_bindingIndex < 0 || _bindingIndex >= _bindings.Count) return;
        _updating = true; var item = _bindings[_bindingIndex];
        var actionIndex = _actions.FindIndex(action => action.Key == item.ActionKey);
        if (actionIndex >= 0) _bindingAction.Select(actionIndex);
        _bindingDevice.Select((int)item.Device); _bindingValue.Text = item.Binding; _bindingGlyph.Text = item.Glyph;
        _bindingPrimary.ButtonPressed = item.Primary; _updating = false;
    }
    private void FlushBindingUx6B()
    {
        if (_updating || _bindingIndex < 0 || _bindingIndex >= _bindings.Count) return;
        var actionKey = _bindingAction.Selected >= 0 && _bindingAction.Selected < _actions.Count
            ? _actions[_bindingAction.Selected].Key : string.Empty;
        _bindings[_bindingIndex] = _bindings[_bindingIndex] with
        {
            ActionKey = actionKey,
            Device = (RuntimeInputDevice)_bindingDevice.Selected,
            Binding = _bindingValue.Text,
            Glyph = _bindingGlyph.Text,
            Primary = _bindingPrimary.ButtonPressed
        };
    }

    private void AddVerbUx6B()
    {
        FlushVerbUx6B(); var n = _verbs.Count + 1;
        _verbs.Add(new(Guid.NewGuid(), $"verb-{n}", $"Nouveau verbe {n}", $"verb-{n}", 0,
            ["world"], false)); RebuildCollectionsPreservingUx6B(); _verbIndex = _verbs.Count - 1;
        _verbChoice.Select(_verbIndex); PopulateVerbUx6B(); InvalidatePreviewUx6B();
    }
    private void RemoveVerbUx6B()
    {
        if (_verbIndex < 0) return; _verbs.RemoveAt(_verbIndex); RebuildCollectionsPreservingUx6B(); InvalidatePreviewUx6B();
    }
    private void AddCameraUx6B()
    {
        FlushCameraUx6B(); var n = _cameras.Count + 1;
        _cameras.Add(new(Guid.NewGuid(), $"camera-{n}", $"Caméra {n}", GameplayCameraMode.Orbit,
            60, 8, 1, 40, 20, new(0, 1.25f, 0), true, false, .9f, 32, 39, -85, 85,
            1, CameraOcclusionStrategy.Retract, 24, 3.25f,
            1, 2, 28, 42, .55f, 1.15f, true));
        RebuildCollectionsPreservingUx6B(); _cameraIndex = _cameras.Count - 1;
        _cameraChoice.Select(_cameraIndex); PopulateCameraUx6B(); InvalidatePreviewUx6B();
    }
    private void RemoveCameraUx6B()
    {
        if (_cameraIndex < 0) return; _cameras.RemoveAt(_cameraIndex); RebuildCollectionsPreservingUx6B(); InvalidatePreviewUx6B();
    }
    private void AddActionUx6B()
    {
        FlushActionUx6B(); var n = _actions.Count + 1;
        _actions.Add(new(Guid.NewGuid(), $"action-{n}", $"Nouvelle action {n}",
            LinesUx6B(_contexts.Text).FirstOrDefault() ?? "exploration", false));
        RebuildCollectionsPreservingUx6B(); _actionIndex = _actions.Count - 1;
        _actionChoice.Select(_actionIndex); PopulateActionUx6B(); InvalidatePreviewUx6B();
    }
    private void RemoveActionUx6B()
    {
        if (_actionIndex < 0) return; var key = _actions[_actionIndex].Key;
        _actions.RemoveAt(_actionIndex); _bindings.RemoveAll(item => item.ActionKey == key);
        RebuildCollectionsPreservingUx6B(); InvalidatePreviewUx6B();
    }
    private void AddBindingUx6B()
    {
        FlushBindingUx6B(); if (_actions.Count == 0) { SetStatusUx6B("Ajoutez d’abord une action.", true); return; }
        _bindings.Add(new(Guid.NewGuid(), _actions[0].Key, RuntimeInputDevice.KeyboardMouse,
            "Nouvelle touche", "key-new", true)); RebuildCollectionsPreservingUx6B();
        _bindingIndex = _bindings.Count - 1; _bindingChoice.Select(_bindingIndex); PopulateBindingUx6B(); InvalidatePreviewUx6B();
    }
    private void RemoveBindingUx6B()
    {
        if (_bindingIndex < 0) return; _bindings.RemoveAt(_bindingIndex); RebuildCollectionsPreservingUx6B(); InvalidatePreviewUx6B();
    }

    private void RebuildCollectionsPreservingUx6B()
    {
        var interaction = _interactionAction.Selected >= 0 && _interactionAction.Selected < _actions.Count
            ? _actions[_interactionAction.Selected].Key : _actions.FirstOrDefault()?.Key ?? string.Empty;
        var defaultKey = _defaultCamera.Selected >= 0 && _defaultCamera.Selected < _cameras.Count
            ? _cameras[_defaultCamera.Selected].Key : _cameras.FirstOrDefault()?.Key ?? string.Empty;
        RebuildCollectionsUx6B(interaction, defaultKey);
    }

    private void InvalidatePreviewUx6B()
    {
        if (_updating) return;
        _previewReady = false; _previewFingerprint = null; _session.CancelUx6BApproval(); UpdateButtonsUx6B();
    }

    private void ResetControlsUx6B()
    {
        _updating = true;
        _verbs = []; _cameras = []; _actions = []; _bindings = [];
        _verbChoice.Clear(); _cameraChoice.Clear(); _defaultCamera.Clear(); _actionChoice.Clear();
        _bindingChoice.Clear(); _interactionAction.Clear(); _bindingAction.Clear();
        _verbIndex = _cameraIndex = _actionIndex = _bindingIndex = -1;
        _previewReady = false; _previewFingerprint = null; _previewResult = null;
        RenderPreviewUx6B(null); _updating = false; UpdateButtonsUx6B();
    }

    private void UpdateButtonsUx6B()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !PreviewReady || _previewResult is not { CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
    }

    private void RenderPreviewUx6B(StudioUx6BPreview? preview)
    {
        ClearChildrenUx6B(_diff); ClearChildrenUx6B(_runtime); ClearChildrenUx6B(_diagnostics);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Le diff sémantique apparaîtra après l’aperçu sec.", 10, StudioStyle.Muted, wrap: true));
            _runtime.AddChild(StudioStyle.Label("Les quatre résolveurs purs apparaîtront ici.", 10, StudioStyle.Muted, wrap: true));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic frais.", 10, StudioStyle.Muted));
            _summary.Text = "Aucun aperçu sec frais."; return;
        }
        if (preview.Diff.Count == 0) _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted));
        foreach (var item in preview.Diff) _diff.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        foreach (var item in preview.RuntimePreview) _runtime.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        if (preview.Diagnostics.Count == 0) _diagnostics.AddChild(StudioStyle.Label("Conforme · dry-run canonique", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics) _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10,
            item.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        _summary.Text = preview.Message + "\n" + preview.PatchSummary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
    }

    private StudioUx6BEvidenceSourceSnapshot CaptureSourceUx6B()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureGameplayFoundationApprovalUx6B());
    }

    private bool SourceMatchesUx6B(StudioUx6BEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision && (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureGameplayFoundationApprovalUx6B() == snapshot.Approval;
    }

    private bool ReleaseEvidenceUx6B()
    {
        var isolated = _evidenceSession; var root = _evidenceRoot; var sessionReleased = true;
        try { isolated?.Close(); }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_B_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx6BTemporaryWorkspace(root);
        _evidenceSession = null; _evidenceRoot = rootReleased ? null : root; _sourceSnapshot = null;
        _session = _sourceSession; _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void UpdateBoundsUx6B()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.68f : viewport.X >= 2_300 ? 1.22f : 1f;
        var width = Math.Clamp((viewport.X - 48f) / scale, 1_180f, 1_650f);
        var height = Math.Clamp((viewport.Y - 38f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale; _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(16f, (viewport.Y - height * scale) * .46f));
    }

    private VBoxContainer ScrollTabUx6B(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1_060, 0)
        };
        content.AddThemeConstantOverride("separation", 8); scroll.AddChild(content); _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title); return content;
    }

    private static GridContainer GridUx6B(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 9); grid.AddThemeConstantOverride("v_separation", 7); return grid;
    }

    private static SpinBox NumberUx6B(double min, double max, double step, bool allowGreater = false,
        bool allowLesser = false) => StudioUx2EnvBControls.Number(min, max, step, allowGreater, allowLesser);
    private static VBoxContainer FieldUx6B(string label, Control control, int minimum = 118) =>
        StudioUx2EnvBControls.Field(label, control, minimum);

    private static VBoxContainer ReviewColumnUx6B(HBoxContainer host, string title)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 0)
        };
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body); column.AddChild(scroll); host.AddChild(column); return body;
    }

    private static IReadOnlyList<string> LinesUx6B(string text) => (text ?? string.Empty)
        .Replace("\r", string.Empty).Split('\n').Select(item => item.Trim()).Where(item => item.Length > 0)
        .Distinct(StringComparer.Ordinal).ToArray();
    private static string HumanValueUx6B(string value) => string.IsNullOrWhiteSpace(value) ? "Sans nom" :
        string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static double DisplayUx6B(float value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
    private static void ClearChildrenUx6B(Node node) { foreach (var child in node.GetChildren()) child.QueueFree(); }
    private void SetStatusUx6B(string message, bool error)
    {
        _status.Text = message; _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }
    private static IEnumerable<string> DescendantTextUx6B(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var nested in DescendantTextUx6B(child)) yield return nested;
        }
    }
    private static bool ContainsUuidUx6B(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6B();
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { Close(); GetViewport().SetInputAsHandled(); }
    }
    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) Close();
    }
}
