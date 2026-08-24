using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableBlueprintOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly Label _eyebrow;
    private readonly Label _heading;
    private readonly Label _subtitle;
    private readonly OptionButton _blueprint;
    private readonly Label _blueprintSummary;
    private readonly Label _empty;
    private readonly OptionButton _visual;
    private readonly ScrollContainer _formScroll;
    private readonly LineEdit _slug;
    private readonly LineEdit _name;
    private readonly LineEdit _prompt;
    private readonly StudioPlaceableLocalizationEditor _localizationEditor;
    private readonly LineEdit _tags;
    private readonly LineEdit _notes;
    private readonly StudioPlaceableAppearanceEditor _appearanceEditor;
    private readonly OptionButton _role;
    private readonly CheckBox _collisionEnabled;
    private readonly CheckBox _navigationObstacle;
    private readonly OptionButton _collisionShape;
    private readonly SpinBox _collisionX;
    private readonly SpinBox _collisionY;
    private readonly SpinBox _collisionZ;
    private readonly VBoxContainer _staticFields;
    private readonly CheckBox _batched;
    private readonly CheckBox _instanced;
    private readonly CheckBox _occluder;
    private readonly CheckBox _mergeEligible;
    private readonly CheckBox _staticCollision;
    private readonly Label _staticHint;
    private readonly VBoxContainer _interactionFields;
    private readonly StudioPlaceableVerbEditor _verbEditor;
    private readonly SpinBox _interactionDistance;
    private readonly SpinBox _interactionAngle;
    private readonly CheckBox _requiresFocus;
    private readonly SpinBox _interactionDuration;
    private readonly OptionButton _interactionAuthority;
    private readonly LineEdit _interactionConditions;
    private readonly CheckBox _onUsedEnabled;
    private readonly StudioPlaceableOnUsedEventEditor _onUsedEventEditor;
    private readonly CheckBox _health;
    private readonly StudioPlaceableDamageableEditor _damageableEditor;
    private readonly CheckBox _protection;
    private readonly StudioPlaceableProtectionEditor _protectionEditor;
    private readonly CheckBox _factionOwnership;
    private readonly StudioPlaceableFactionEditor _factionEditor;
    private readonly CheckBox _conversation;
    private readonly StudioPlaceableConversationEditor _conversationEditor;
    private readonly CheckBox _container;
    private readonly StudioPlaceableContainerEditor _containerEditor;
    private readonly CheckBox _lock;
    private readonly StudioPlaceableLockEditor _lockEditor;
    private readonly CheckBox _credentialPolicy;
    private readonly StudioPlaceableCredentialEditor _credentialEditor;
    private readonly CheckBox _trap;
    private readonly StudioPlaceableTrapEditor _trapEditor;
    private readonly CheckBox _loot;
    private readonly StudioPlaceableLootEditor _lootEditor;
    private readonly CheckBox _containerEvents;
    private readonly StudioPlaceableContainerEventsEditor _containerEventsEditor;
    private readonly CheckBox _lockEvents;
    private readonly StudioPlaceableLockEventsEditor _lockEventsEditor;
    private readonly CheckBox _contentsChanged;
    private readonly StudioPlaceableContentsChangedEditor _contentsChangedEditor;
    private readonly CheckBox _damageEvents;
    private readonly StudioPlaceableDamageEventsEditor _damageEventsEditor;
    private readonly CheckBox _physics;
    private readonly LineEdit _scripts;
    private readonly SpinBox _positionX;
    private readonly SpinBox _positionY;
    private readonly SpinBox _positionZ;
    private readonly SpinBox _rotationX;
    private readonly SpinBox _rotationY;
    private readonly SpinBox _rotationZ;
    private readonly SpinBox _scaleX;
    private readonly SpinBox _scaleY;
    private readonly SpinBox _scaleZ;
    private readonly SpinBox _pivotX;
    private readonly SpinBox _pivotY;
    private readonly SpinBox _pivotZ;
    private readonly SpinBox _snap;
    private readonly OptionButton _parent;
    private readonly CheckBox _alignToSurface;
    private readonly LineEdit _transformAnimation;
    private readonly LineEdit _overrides;
    private readonly CheckBox _requireRuntime;
    private readonly StudioPlaceableStateMachineEditor _stateMachineEditor;
    private readonly Label _previewSummary;
    private readonly Label _previewState;
    private readonly Label _scenePreviewTitle;
    private readonly Label _scenePreviewDescription;
    private readonly Button _selectInstances;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;
    private readonly List<BlueprintId> _blueprintIds = [];
    private readonly List<string> _visualIds = [];
    private readonly List<Guid?> _parentIds = [];
    private StudioPlaceableCatalog? _catalog;
    private StudioPlaceableDraft? _draft;
    private StudioPlaceablePreview? _result;
    private int? _evidenceScrollTarget;
    private Control? _evidenceScrollControl;
    private bool _updating;
    private bool _initialAnimationFocus;
    private bool _destructibilityFocus;
    private bool _plotProtectionFocus;
    private bool _factionFocus;
    private bool _conversationFocus;
    private bool _lockFocus;
    private bool _credentialFocus;
    private bool _trapFocus;
    private bool _lootFocus;
    private bool _containerEventsFocus;
    private bool _lockEventsFocus;
    private bool _lifecycleEventsFocus;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ProjectChanged;

    internal event Action<IReadOnlyList<EntityId>, EntityId?>? InstancesRequested;

    internal bool IsOpen => Visible;

    internal int BlueprintCount => _blueprintIds.Count;

    internal int AssetOptionCount => _visualIds.Count;

    internal bool HasEmptyState => _empty.Visible;

    internal bool PreviewReady => _result?.Succeeded == true;

    internal bool InstanceSelectionAvailable => _result?.InstanceIds.Count > 0 && !_selectInstances.Disabled;

    internal bool StaticRoleSelected =>
        _role.Selected == (int)StudioPlaceableRole.StaticDecoration &&
        _staticFields.Visible &&
        !_interactionFields.Visible;

    internal bool StaticPreviewReady =>
        StaticRoleSelected &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Optimisation", StringComparison.OrdinalIgnoreCase);

    internal bool InteractableRoleSelected =>
        _role.Selected == (int)StudioPlaceableRole.Interactable &&
        _interactionFields.Visible &&
        !_staticFields.Visible;

    internal int InteractionVerbCount => _verbEditor.VerbCount;

    internal bool InteractionPreviewReady =>
        InteractableRoleSelected &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Interaction", StringComparison.OrdinalIgnoreCase);

    internal bool ContainerEnabled => _container.ButtonPressed && _containerEditor.Visible;

    internal bool ContainerPreviewReady =>
        ContainerEnabled &&
        _containerEditor.IsReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Conteneur", StringComparison.OrdinalIgnoreCase);

    internal bool IdentityAppearancePreviewReady =>
        _appearanceEditor.IsReady &&
        _localizationEditor.LocalizationCount >= 1 &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Identité", StringComparison.OrdinalIgnoreCase);

    internal bool PreciseTransformPreviewReady =>
        _parent.ItemCount >= 1 &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Transformation", StringComparison.OrdinalIgnoreCase) &&
        _previewSummary.Text.Contains("Pivot", StringComparison.OrdinalIgnoreCase);

    internal bool InitialAnimationPreviewReady =>
        _stateMachineEditor.Enabled &&
        _stateMachineEditor.IsReady &&
        _stateMachineEditor.TransitionPreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Animation initiale", StringComparison.OrdinalIgnoreCase);

    internal bool DestructibilityPreviewReady =>
        _health.ButtonPressed &&
        _damageableEditor.Visible &&
        _damageableEditor.IsReady &&
        _damageableEditor.DamagePreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Destructibilité", StringComparison.OrdinalIgnoreCase);

    internal bool PlotProtectionPreviewReady =>
        _protection.ButtonPressed &&
        _protectionEditor.Visible &&
        _protectionEditor.IsReady &&
        _protectionEditor.PolicyPreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Protection plot", StringComparison.OrdinalIgnoreCase);

    internal bool FactionOwnershipPreviewReady =>
        _factionOwnership.ButtonPressed &&
        _factionEditor.Visible &&
        _factionEditor.IsReady &&
        _factionEditor.DecisionPreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Faction et propriété", StringComparison.OrdinalIgnoreCase);

    internal bool ConversationPreviewReady =>
        _conversation.ButtonPressed &&
        _conversationEditor.Visible &&
        _conversationEditor.IsReady &&
        _conversationEditor.PreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Conversation", StringComparison.OrdinalIgnoreCase);

    internal bool LockPreviewReady =>
        _lock.ButtonPressed &&
        _lockEditor.Visible &&
        _lockEditor.IsReady &&
        _lockEditor.PreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Verrouillage", StringComparison.OrdinalIgnoreCase);

    internal bool CredentialPreviewReady =>
        _credentialPolicy.ButtonPressed &&
        _credentialEditor.Visible &&
        _credentialEditor.IsReady &&
        _credentialEditor.PreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Justificatif", StringComparison.OrdinalIgnoreCase);

    internal bool TrapPreviewReady =>
        _trap.ButtonPressed &&
        _trapEditor.Visible &&
        _trapEditor.IsReady &&
        _trapEditor.PreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Piège", StringComparison.OrdinalIgnoreCase);

    internal bool LootPreviewReady =>
        _loot.ButtonPressed &&
        _lootEditor.Visible &&
        _lootEditor.IsReady &&
        _lootEditor.StableRowsReady &&
        _lootEditor.PreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Trésor initial", StringComparison.OrdinalIgnoreCase);

    internal bool ContainerEventsPreviewReady =>
        _containerEvents.ButtonPressed &&
        _containerEventsEditor.Visible &&
        _containerEventsEditor.IsReady &&
        _containerEventsEditor.PreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Ouverture et fermeture", StringComparison.OrdinalIgnoreCase);

    internal bool LockEventsPreviewReady =>
        _lock.ButtonPressed &&
        _lockEditor.Visible &&
        _lockEditor.IsReady &&
        _lockEvents.ButtonPressed &&
        _lockEventsEditor.Visible &&
        _lockEventsEditor.IsReady &&
        _lockEventsEditor.PreviewReady &&
        _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Verrouillage et déverrouillage", StringComparison.OrdinalIgnoreCase);

    internal bool LifecycleEventsPreviewReady =>
        _role.Selected == (int)StudioPlaceableRole.Interactable &&
        _onUsedEnabled.ButtonPressed && _onUsedEventEditor.IsReady && _onUsedEventEditor.PreviewReady &&
        _contentsChanged.ButtonPressed && _contentsChangedEditor.IsReady && _contentsChangedEditor.PreviewReady &&
        _health.ButtonPressed && _damageEvents.ButtonPressed && _damageEventsEditor.IsReady &&
        _damageEventsEditor.PreviewReady && _result?.Succeeded == true &&
        _previewSummary.Text.Contains("Contenu modifié", StringComparison.OrdinalIgnoreCase) &&
        _previewSummary.Text.Contains("Dégâts et mort", StringComparison.OrdinalIgnoreCase);

    internal bool ShowsHumanSurface
    {
        get
        {
            var choices = Enumerable.Range(0, _blueprint.ItemCount).Select(index => _blueprint.GetItemText(index))
                .Concat(Enumerable.Range(0, _visual.ItemCount).Select(index => _visual.GetItemText(index)))
                .Concat(Enumerable.Range(0, _parent.ItemCount).Select(index => _parent.GetItemText(index)));
            var text = string.Join(' ', choices.Append(_blueprintSummary.Text).Append(_empty.Text)
                .Append(_previewSummary.Text).Append(_previewState.Text).Append(_status.Text)
                .Append(_verbEditor.HumanText).Append(_containerEditor.HumanText)
                .Append(_localizationEditor.HumanText).Append(_appearanceEditor.HumanText)
                .Append(_stateMachineEditor.HumanText).Append(_damageableEditor.HumanText)
                .Append(_protectionEditor.HumanText).Append(_factionEditor.HumanText)
                .Append(_conversationEditor.HumanText).Append(_lockEditor.HumanText)
                .Append(_credentialEditor.HumanText).Append(_trapEditor.HumanText)
                .Append(_lootEditor.HumanText).Append(_containerEventsEditor.HumanText)
                .Append(_lockEventsEditor.HumanText).Append(_onUsedEventEditor.HumanText)
                .Append(_contentsChangedEditor.HumanText).Append(_damageEventsEditor.HumanText));
            return !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("project.json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("\\", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioPlaceableBlueprintOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioPlaceableBlueprintOverlay";
        Visible = false;
        ZIndex = 460;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .8f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("PlaceableBlueprintDialog", StudioStyle.Chrome, 12, 2);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _eyebrow = StudioStyle.Label("BLUEPRINT DE DÉCOR", 10, StudioStyle.Accent);
        _heading = StudioStyle.Label("Composer un placeable sans quitter la scène", 20);
        _subtitle = StudioStyle.Label(
            "Visuel · collision · rôle · composants · scripts · transformation · aperçu avant application",
            10,
            StudioStyle.Muted);
        heading.AddChild(_eyebrow);
        heading.AddChild(_heading);
        heading.AddChild(_subtitle);
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var workspace = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(workspace);
        var content = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.AddThemeConstantOverride("separation", 10);
        workspace.AddChild(content);

        var catalogPanel = StudioStyle.Panel("PlaceableCatalog", StudioStyle.Surface, 8);
        catalogPanel.CustomMinimumSize = new Vector2(245, 0);
        content.AddChild(catalogPanel);
        var catalogMargin = StudioStyle.Margin(12, 12, 12, 12);
        catalogPanel.AddChild(catalogMargin);
        var catalogColumn = new VBoxContainer();
        catalogColumn.AddThemeConstantOverride("separation", 8);
        catalogMargin.AddChild(catalogColumn);
        catalogColumn.AddChild(StudioStyle.SectionLabel("BLUEPRINT À CONFIGURER"));
        _blueprint = new OptionButton { TooltipText = "Blueprints de type décor présents dans le module" };
        StudioStyle.ApplyOptionButton(_blueprint);
        catalogColumn.AddChild(_blueprint);
        _blueprintSummary = StudioStyle.Label(string.Empty, 11, StudioStyle.Muted, wrap: true);
        catalogColumn.AddChild(_blueprintSummary);
        _empty = StudioStyle.Label(string.Empty, 11, StudioStyle.Warning, wrap: true);
        _empty.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.AccentDark, StudioStyle.Warning, 6));
        catalogColumn.AddChild(_empty);
        catalogColumn.AddChild(new HSeparator());
        catalogColumn.AddChild(StudioStyle.SectionLabel("VISUEL 3D"));
        _visual = new OptionButton { TooltipText = "Assets 3D du module, triés par usage puis par nom" };
        StudioStyle.ApplyOptionButton(_visual);
        catalogColumn.AddChild(_visual);
        catalogColumn.AddChild(StudioStyle.Label(
            "Le visuel déjà employé par les instances est proposé en premier. Les identifiants restent masqués.",
            10,
            StudioStyle.Muted,
            wrap: true));
        catalogColumn.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _selectInstances = StudioStyle.Button("Voir les instances en 3D", compact: true);
        _selectInstances.Disabled = true;
        _selectInstances.TooltipText = "Fermer cette fiche et sélectionner toutes les instances de ce blueprint";
        catalogColumn.AddChild(_selectInstances);

        _formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
            CustomMinimumSize = new Vector2(430, 0),
        };
        content.AddChild(_formScroll);
        var formPanel = StudioStyle.Panel("PlaceableForm", StudioStyle.Surface, 8);
        formPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _formScroll.AddChild(formPanel);
        var formMargin = StudioStyle.Margin(12, 10, 8, 10);
        formPanel.AddChild(formMargin);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7);
        formMargin.AddChild(form);

        form.AddChild(StudioStyle.SectionLabel("IDENTITÉ VISIBLE"));
        _name = AddLine(form, "Nom principal (fr-FR)", "Nom affiché dans le Studio");
        _slug = AddLine(form, "Nom court", "nom-court-stable");
        _prompt = AddLine(form, "Invitation principale", "Texte proposé au joueur");
        _localizationEditor = new StudioPlaceableLocalizationEditor();
        form.AddChild(_localizationEditor);
        _tags = AddLine(form, "Tags", "décor, pierre, intérieur");
        _notes = AddLine(form, "Notes d’auteur", "Intention de mise en scène");
        _overrides = AddLine(form, "Surcharges explicites", "nom=valeur; autre=valeur");

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("APPARENCE DÉTAILLÉE"));
        form.AddChild(StudioStyle.Label(
            "Le visuel 3D choisi à gauche reste la source principale ; meshes, matériaux, icône, portrait et VFX sont conservés séparément.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _appearanceEditor = new StudioPlaceableAppearanceEditor();
        form.AddChild(_appearanceEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("COLLISION ET RÔLE"));
        _collisionEnabled = AddCheck(form, "Collision active");
        _navigationObstacle = AddCheck(form, "Obstacle de navigation");
        _collisionShape = AddOption(form, "Forme", ["Boîte", "Capsule", "Cylindre", "Sphère"]);
        (_collisionX, _collisionY, _collisionZ) = AddVector(form, "Taille", .05, 100, .05);
        _role = AddOption(form, "Rôle", ["Décor statique", "Objet utilisable"]);

        _staticFields = new VBoxContainer();
        _staticFields.AddThemeConstantOverride("separation", 4);
        form.AddChild(_staticFields);
        _staticFields.AddChild(StudioStyle.Label("Optimisation du décor", 11, StudioStyle.Muted));
        _batched = AddCheck(_staticFields, "Regrouper les rendus compatibles");
        _instanced = AddCheck(_staticFields, "Instancier le même modèle");
        _occluder = AddCheck(_staticFields, "Utiliser comme occludeur");
        _mergeEligible = AddCheck(_staticFields, "Autoriser la fusion de géométrie");
        _staticCollision = AddCheck(_staticFields, "Conserver une collision par instance");
        _staticHint = StudioStyle.Label(
            "Sécurité · activez le regroupement ou l’instanciation. Une fusion exclut la collision par instance.",
            10,
            StudioStyle.Warning,
            wrap: true);
        _staticFields.AddChild(_staticHint);

        _interactionFields = new VBoxContainer();
        _interactionFields.AddThemeConstantOverride("separation", 4);
        form.AddChild(_interactionFields);
        _interactionFields.AddChild(StudioStyle.Label("Interaction du joueur", 11, StudioStyle.Muted));
        _verbEditor = new StudioPlaceableVerbEditor();
        _interactionFields.AddChild(_verbEditor);
        _interactionDistance = AddSpin(_interactionFields, "Portée (m)", .05, 100, .05);
        _interactionAngle = AddSpin(_interactionFields, "Angle maximal", 1, 360, 1);
        _interactionDuration = AddSpin(_interactionFields, "Durée (s)", 0, 120, .05);
        _requiresFocus = AddCheck(_interactionFields, "Exiger le ciblage du joueur");
        _interactionAuthority = AddOption(_interactionFields, "Autorité",
            ["Serveur", "Propriétaire de l’objet", "Client local"]);
        _interactionConditions = AddLine(
            _interactionFields,
            "Conditions globales",
            "joueur conscient, ligne de vue");
        _interactionFields.AddChild(new HSeparator());
        _interactionFields.AddChild(StudioStyle.Label("ÉVÉNEMENT D’UTILISATION", 10, StudioStyle.Accent));
        _onUsedEnabled = AddCheck(_interactionFields, "Publier l’événement OnUsed");
        _onUsedEventEditor = new StudioPlaceableOnUsedEventEditor();
        _interactionFields.AddChild(_onUsedEventEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("COMPOSANTS ET SCRIPTS"));
        var componentGrid = new GridContainer { Columns = 2 };
        componentGrid.AddThemeConstantOverride("h_separation", 12);
        form.AddChild(componentGrid);
        _health = AddCheck(componentGrid, "Santé / dégâts");
        _protection = AddCheck(componentGrid, "Protection plot");
        _factionOwnership = AddCheck(componentGrid, "Faction et propriété");
        _conversation = AddCheck(componentGrid, "Conversation");
        _container = AddCheck(componentGrid, "Conteneur");
        _lock = AddCheck(componentGrid, "Verrou");
        _credentialPolicy = AddCheck(componentGrid, "Clé consommée");
        _trap = AddCheck(componentGrid, "Piège");
        _loot = AddCheck(componentGrid, "Trésor initial");
        _containerEvents = AddCheck(componentGrid, "Ouverture / fermeture");
        _lockEvents = AddCheck(componentGrid, "Verrouillage / déverrouillage");
        _contentsChanged = AddCheck(componentGrid, "Contenu modifié / OnDisturbed");
        _damageEvents = AddCheck(componentGrid, "Dégâts / mort");
        _physics = AddCheck(componentGrid, "Physique");
        _containerEditor = new StudioPlaceableContainerEditor();
        form.AddChild(_containerEditor);
        _scripts = AddLine(form, "Liaisons de script", "événement=route; autre=route");

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("TRANSFORMATION INDÉPENDANTE DU MOTEUR"));
        (_positionX, _positionY, _positionZ) = AddVector(form, "Position", -1_000_000, 1_000_000, .01);
        (_rotationX, _rotationY, _rotationZ) = AddVector(form, "Rotation", -3600, 3600, 1);
        (_scaleX, _scaleY, _scaleZ) = AddVector(form, "Échelle", .01, 100, .01);
        (_pivotX, _pivotY, _pivotZ) = AddVector(form, "Pivot", -1_000_000, 1_000_000, .01);
        _snap = AddSpin(form, "Pas de grille", 0, 10_000, .01);
        _parent = AddOption(form, "Parent hiérarchique", ["Aucun parent · racine de la scène"]);
        _alignToSurface = AddCheck(form, "Aligner sur la surface");
        _transformAnimation = AddLine(form, "Animation de transformation", "animation:levée · facultatif");
        form.AddChild(StudioStyle.Label(
            "Le parent est choisi par son nom. Le bouton « Sélectionner les instances » ouvre ensuite le gizmo 3D réel.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _requireRuntime = AddCheck(form, "Exiger cette fiche au runtime");
        _requireRuntime.TooltipText =
            "Si activé, une fiche absente devient un diagnostic runtime. Sinon le runtime applique un repli propre.";

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("ANIMATION INITIALE ET ÉTATS VISUELS"));
        form.AddChild(StudioStyle.Label(
            "Choisissez l’apparence au chargement, réglez les six états usuels puis prévisualisez leurs transitions.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _stateMachineEditor = new StudioPlaceableStateMachineEditor();
        form.AddChild(_stateMachineEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("DESTRUCTIBILITÉ"));
        form.AddChild(StudioStyle.Label(
            "Réglez santé, armure, résistances, étapes visuelles, destruction, débris et réparation, puis simulez un impact sans écrire le module.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _damageableEditor = new StudioPlaceableDamageableEditor();
        form.AddChild(_damageableEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("PROTECTION PLOT"));
        form.AddChild(StudioStyle.Label(
            "Choisissez les actions protégées et les droits de contournement, puis simulez un joueur ou une dérogation MJ auditée sans écrire le module.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _protectionEditor = new StudioPlaceableProtectionEditor();
        form.AddChild(_protectionEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("FACTION ET PROPRIÉTÉ"));
        form.AddChild(StudioStyle.Label(
            "Choisissez la faction propriétaire et ses appartenances, puis projetez hostilité, crime, accès et ciblage sans écrire le module.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _factionEditor = new StudioPlaceableFactionEditor();
        form.AddChild(_factionEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("CONVERSATION"));
        form.AddChild(StudioStyle.Label(
            "Reliez ce placeable à des dialogues selon l’interaction, son état, l’utilisateur, un objet présenté ou un événement, puis simulez la résolution sans écrire le module.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _conversationEditor = new StudioPlaceableConversationEditor();
        form.AddChild(_conversationEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("VERROUILLAGE"));
        form.AddChild(StudioStyle.Label(
            "Choisissez l’état initial, les clés et justificatifs, le défi, le code, le piratage, les droits et le cycle de reverrouillage, puis simulez le runtime sans écrire le module.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _lockEditor = new StudioPlaceableLockEditor();
        form.AddChild(_lockEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("JUSTIFICATIF ET CONSOMMATION"));
        form.AddChild(StudioStyle.Label(
            "Décidez si la clé reste, disparaît, dépense une charge, se partage ou devient une permission virtuelle, puis prévisualisez la décision sans toucher à l’inventaire.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _credentialEditor = new StudioPlaceableCredentialEditor();
        form.AddChild(_credentialEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("PIÈGE"));
        form.AddChild(StudioStyle.Label(
            "Définissez le déclencheur, l’effet, la détection, le désamorçage, le réarmement et les alliés, puis simulez le résultat sans déclencher la scène.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _trapEditor = new StudioPlaceableTrapEditor();
        form.AddChild(_trapEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("TRÉSOR INITIAL"));
        form.AddChild(StudioStyle.Label(
            "Composez la table avec les objets du projet, choisissez son moment et sa graine, puis simulez le tirage sans écrire le module.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _lootEditor = new StudioPlaceableLootEditor();
        form.AddChild(_lootEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("OUVERTURE ET FERMETURE"));
        form.AddChild(StudioStyle.Label(
            "Nommez les deux événements ForgeScript et prévisualisez le contexte acteur, résultat, état précédent et autorité sans modifier la scène.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _containerEventsEditor = new StudioPlaceableContainerEventsEditor();
        form.AddChild(_containerEventsEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("VERROUILLAGE ET DÉVERROUILLAGE"));
        form.AddChild(StudioStyle.Label(
            "Nommez les événements publiés avant et après le changement, conservez méthode, justificatif et acteur, puis prévisualisez une transition ou un veto sans modifier la scène.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _lockEventsEditor = new StudioPlaceableLockEventsEditor();
        form.AddChild(_lockEventsEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("CONTENU MODIFIÉ · ONDISTURBED"));
        form.AddChild(StudioStyle.Label(
            "Publiez un delta transactionnel avec sa cause, ses révisions et son acteur, puis prévisualisez-le sans modifier l’inventaire.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _contentsChangedEditor = new StudioPlaceableContentsChangedEditor();
        form.AddChild(_contentsChangedEditor);

        form.AddChild(new HSeparator());
        form.AddChild(StudioStyle.SectionLabel("DÉGÂTS ET MORT · ONDAMAGED / ONDEATH"));
        form.AddChild(StudioStyle.Label(
            "Nommez les événements de dégâts et de mort et conservez un payload uniforme, puis simulez les deux issues sans toucher à la santé de la scène.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _damageEventsEditor = new StudioPlaceableDamageEventsEditor();
        form.AddChild(_damageEventsEditor);

        var previewPanel = StudioStyle.Panel("PlaceablePreview", StudioStyle.Surface, 8);
        previewPanel.CustomMinimumSize = new Vector2(305, 0);
        content.AddChild(previewPanel);
        var previewMargin = StudioStyle.Margin(12, 12, 12, 12);
        previewPanel.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 9);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("APERÇU AVANT APPLICATION"));
        var sceneCard = StudioStyle.Panel("PlaceableSceneCard", StudioStyle.SurfaceRaised, 8);
        sceneCard.CustomMinimumSize = new Vector2(0, 190);
        previewColumn.AddChild(sceneCard);
        var sceneMargin = StudioStyle.Margin(14, 14, 14, 14);
        sceneCard.AddChild(sceneMargin);
        var scene = new VBoxContainer();
        scene.AddThemeConstantOverride("separation", 8);
        sceneMargin.AddChild(scene);
        scene.AddChild(StudioStyle.Label("◈  APERÇU DE SCÈNE", 11, StudioStyle.Accent));
        _scenePreviewTitle = StudioStyle.Label("Gizmo de pivot et pas de grille", 15, wrap: true);
        scene.AddChild(_scenePreviewTitle);
        _scenePreviewDescription = StudioStyle.Label(
            "La sélection 3D utilise les instances réelles du blueprint. La position affichée est celle résolue par le runtime canonique.",
            10,
            StudioStyle.Muted,
            wrap: true);
        scene.AddChild(_scenePreviewDescription);
        _previewState = StudioStyle.Label("Prévisualisez pour calculer la pose et les références.", 11,
            StudioStyle.Warning, wrap: true);
        scene.AddChild(_previewState);
        _previewSummary = StudioStyle.Label("Aucun changement appliqué.", 12, StudioStyle.Muted, wrap: true);
        previewColumn.AddChild(_previewSummary);
        previewColumn.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _preview = StudioStyle.Button("Prévisualiser", primary: true);
        _preview.TooltipText = "Valider la fiche et calculer son résultat sans écrire le module";
        previewColumn.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer au module");
        _apply.Disabled = true;
        _apply.TooltipText = "Appliquer atomiquement l’aperçu validé au document canonique";
        previewColumn.AddChild(_apply);
        var history = new HBoxContainer();
        history.AddThemeConstantOverride("separation", 6);
        previewColumn.AddChild(history);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        history.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        history.AddChild(_redo);
        _status = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        previewColumn.AddChild(_status);

        WireEvents();
        Resized += UpdateDialogBounds;
    }

    internal void Open()
    {
        SetPresentation(staticFocus: false);
        _updating = true;
        _catalog = _session.CreatePlaceableCatalog();
        _blueprintIds.Clear();
        _blueprint.Clear();
        foreach (var option in _catalog.Blueprints)
        {
            _blueprintIds.Add(option.Id);
            _blueprint.AddItem(option.DisplayName);
            _blueprint.SetItemTooltip(_blueprint.ItemCount - 1,
                $"{option.InstanceCount} instance(s) · {(option.HasProfile ? "fiche existante" : "fiche à créer")}");
        }
        _updating = false;
        Visible = true;
        _evidenceScrollTarget = null;
        _evidenceScrollControl = null;
        _formScroll.ScrollVertical = 0;
        UpdateDialogBounds();
        if (_blueprintIds.Count == 0)
        {
            _draft = null;
            _blueprintSummary.Text = "Aucun blueprint de décor dans ce module.";
            _empty.Text = "Créez d’abord un blueprint de type décor depuis la palette.";
            _empty.Visible = true;
            SetEditingEnabled(false);
            SetStatus("Aucun blueprint de décor disponible.", error: true);
            return;
        }
        _blueprint.Select(0);
        LoadBlueprint(_blueprintIds[0]);
        _name.GrabFocus();
    }

    internal void OpenIdentityAppearance()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, identityFocus: true);
        _name.GrabFocus();
        SetStatus("Identité et apparence prêtes · localisez les noms et reliez les ressources puis prévisualisez.",
            error: false);
    }

    internal void OpenPreciseTransform()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, transformFocus: true);
        _positionX.GetLineEdit().GrabFocus();
        SetStatus(
            "Transformation précise prête · réglez pose, pivot, grille, parent, surface et animation puis prévisualisez.",
            error: false);
    }

    internal void OpenInitialAnimation()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, initialAnimationFocus: true);
        _stateMachineEditor.EnsureEnabled();
        _stateMachineEditor.FocusInitialState();
        SetStatus(
            "Animation initiale prête · choisissez l’état chargé, ses apparences et les transitions à prévisualiser.",
            error: false);
    }

    internal void OpenDestructibility()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, destructibilityFocus: true);
        _updating = true;
        _health.ButtonPressed = true;
        _updating = false;
        UpdateDamageFields();
        _damageableEditor.FocusHealth();
        SetStatus(
            "Destructibilité prête · réglez santé, résistances, étapes, destruction et réparation puis prévisualisez les dégâts.",
            error: false);
    }

    internal void OpenPlotProtection()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, plotProtectionFocus: true);
        _updating = true;
        _protection.ButtonPressed = true;
        _updating = false;
        UpdateProtectionFields();
        _protectionEditor.FocusFirstAction();
        SetStatus(
            "Protection plot prête · choisissez actions, droits, feedback et dérogation MJ puis prévisualisez la décision.",
            error: false);
    }

    internal void OpenFactionOwnership()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, factionFocus: true);
        _updating = true;
        _factionOwnership.ButtonPressed = true;
        _updating = false;
        UpdateFactionFields();
        _factionEditor.FocusPrimaryFaction();
        SetStatus(
            "Faction et propriété prêtes · choisissez propriétaire, appartenances et effets puis prévisualisez une décision.",
            error: false);
    }

    internal void OpenConversation()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, conversationFocus: true);
        _updating = true;
        _conversation.ButtonPressed = true;
        _updating = false;
        UpdateConversationFields();
        _conversationEditor.FocusBindings();
        SetStatus(
            "Conversation prête · choisissez dialogues, situations, points d’entrée et priorités puis prévisualisez la résolution.",
            error: false);
    }

    internal void OpenLock()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, lockFocus: true);
        _updating = true;
        _lock.ButtonPressed = true;
        _updating = false;
        UpdateLockFields();
        _lockEditor.FocusInitialState();
        SetStatus(
            "Verrouillage prêt · choisissez état, clés, défi, code, piratage, droits et cycle de vie puis prévisualisez une tentative.",
            error: false);
    }

    internal void OpenCredentialPolicy()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, credentialFocus: true);
        _updating = true;
        _credentialPolicy.ButtonPressed = true;
        _updating = false;
        UpdateCredentialFields();
        _credentialEditor.FocusPolicy();
        SetStatus(
            "Clé consommée prête · choisissez conservation, consommation, charges, partage ou permission virtuelle puis prévisualisez l’accès.",
            error: false);
    }

    internal void OpenTrap()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, trapFocus: true);
        _updating = true;
        _trap.ButtonPressed = true;
        _updating = false;
        UpdateTrapFields();
        _trapEditor.FocusTrap();
        SetStatus(
            "Piège prêt · réglez déclencheur, effet, difficultés, réarmement et alliés puis prévisualisez une situation.",
            error: false);
    }

    internal void OpenInitialTreasure()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, lootFocus: true);
        _updating = true;
        _loot.ButtonPressed = true;
        _updating = false;
        UpdateLootFields();
        _lootEditor.FocusTable();
        SetStatus(
            "Trésor initial prêt · choisissez objets, catégories, poids, quantités, moment et graine puis prévisualisez le tirage.",
            error: false);
    }

    internal void OpenContainerEvents()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, containerEventsFocus: true);
        _updating = true;
        _containerEvents.ButtonPressed = true;
        _updating = false;
        UpdateContainerEventsFields();
        _containerEventsEditor.FocusBindings();
        SetStatus(
            "Ouverture et fermeture prêtes · nommez les événements, vérifiez leur contexte puis prévisualisez une situation.",
            error: false);
    }

    internal void OpenLockEvents()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, lockEventsFocus: true);
        _updating = true;
        _lock.ButtonPressed = true;
        _lockEvents.ButtonPressed = true;
        _updating = false;
        UpdateLockFields();
        UpdateLockEventsFields();
        _lockEventsEditor.FocusBindings();
        SetStatus(
            "Verrouillage et déverrouillage prêts · nommez les événements avant et après, vérifiez le contexte puis prévisualisez une transition ou un veto.",
            error: false);
    }

    internal void OpenStaticDecoration()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: true);
        _updating = true;
        _role.Select((int)StudioPlaceableRole.StaticDecoration);
        _updating = false;
        UpdateRoleFields();
        _batched.GrabFocus();
        SetStatus("Décor statique prêt · prévisualisez l’impact de rendu avant application.", error: false);
    }

    internal void OpenInteractable()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, interactionFocus: true);
        _updating = true;
        _role.Select((int)StudioPlaceableRole.Interactable);
        _updating = false;
        UpdateRoleFields();
        _verbEditor.FocusFirstAction();
        SetStatus("Objet utilisable prêt · configurez ses actions puis prévisualisez l’événement.", error: false);
    }

    internal void OpenContainer()
    {
        Open();
        if (_draft is null)
        {
            return;
        }
        SetPresentation(staticFocus: false, containerFocus: true);
        _updating = true;
        _container.ButtonPressed = true;
        _updating = false;
        UpdateContainerFields();
        _containerEditor.FocusCapacity();
        SetStatus("Conteneur prêt · configurez ses limites, ses accès et ses références puis prévisualisez.",
            error: false);
    }

    internal void Close()
    {
        _session.CancelPlaceablePreview();
        _result = null;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        var staticMode = mode.StartsWith("ux1-f", StringComparison.OrdinalIgnoreCase);
        var interactionMode = mode.StartsWith("ux1-g", StringComparison.OrdinalIgnoreCase);
        var containerMode = mode.StartsWith("ux1-h", StringComparison.OrdinalIgnoreCase);
        var identityMode = mode.StartsWith("ux1-i", StringComparison.OrdinalIgnoreCase);
        var transformMode = mode.StartsWith("ux1-j", StringComparison.OrdinalIgnoreCase);
        var initialAnimationMode = mode.StartsWith("ux1-k", StringComparison.OrdinalIgnoreCase);
        var destructibilityMode = mode.StartsWith("ux1-l", StringComparison.OrdinalIgnoreCase);
        var plotProtectionMode = mode.StartsWith("ux1-m", StringComparison.OrdinalIgnoreCase);
        var factionMode = mode.StartsWith("ux1-n", StringComparison.OrdinalIgnoreCase);
        var conversationMode = mode.StartsWith("ux1-o", StringComparison.OrdinalIgnoreCase);
        var lockMode = mode.StartsWith("ux1-p", StringComparison.OrdinalIgnoreCase);
        var credentialMode = mode.StartsWith("ux1-q", StringComparison.OrdinalIgnoreCase);
        var trapMode = mode.StartsWith("ux1-r", StringComparison.OrdinalIgnoreCase);
        var lootMode = mode.StartsWith("ux1-s", StringComparison.OrdinalIgnoreCase);
        var containerEventsMode = mode.StartsWith("ux1-t", StringComparison.OrdinalIgnoreCase);
        var lockEventsMode = mode.StartsWith("ux1-u", StringComparison.OrdinalIgnoreCase);
        var lifecycleEventsMode = mode.StartsWith("ux1-v", StringComparison.OrdinalIgnoreCase);
        if (lifecycleEventsMode)
        {
            Open();
            OpenLifecycleEvents();
        }
        else if (lockEventsMode)
        {
            OpenLockEvents();
        }
        else if (containerEventsMode)
        {
            OpenContainerEvents();
        }
        else if (lootMode)
        {
            OpenInitialTreasure();
        }
        else if (trapMode)
        {
            OpenTrap();
        }
        else if (credentialMode)
        {
            OpenCredentialPolicy();
        }
        else if (lockMode)
        {
            OpenLock();
        }
        else if (conversationMode)
        {
            OpenConversation();
        }
        else if (factionMode)
        {
            OpenFactionOwnership();
        }
        else if (plotProtectionMode)
        {
            OpenPlotProtection();
        }
        else if (destructibilityMode)
        {
            OpenDestructibility();
        }
        else if (initialAnimationMode)
        {
            OpenInitialAnimation();
        }
        else if (transformMode)
        {
            OpenPreciseTransform();
        }
        else if (identityMode)
        {
            OpenIdentityAppearance();
        }
        else if (containerMode)
        {
            OpenContainer();
        }
        else if (staticMode)
        {
            OpenStaticDecoration();
        }
        else if (interactionMode)
        {
            OpenInteractable();
        }
        else
        {
            Open();
        }
        if (_draft is null)
        {
            return false;
        }
        if (lifecycleEventsMode)
        {
            _role.Select((int)StudioPlaceableRole.Interactable);
            _onUsedEnabled.ButtonPressed = true;
            _contentsChanged.ButtonPressed = true;
            _health.ButtonPressed = true;
            _damageEvents.ButtonPressed = true;
            _onUsedEventEditor.SetEvidenceValues();
            _contentsChangedEditor.SetEvidenceValues();
            _damageEventsEditor.SetEvidenceValues(mode);
            _notes.Text =
                "Événements OnUsed, OnDisturbed, OnDamaged et OnDeath gouvernés, typés et prévisualisés sans modifier la scène.";
            UpdateRoleFields();
            UpdateDamageFields();
            UpdateContentsChangedFields();
            UpdateDamageEventsFields();
        }
        else if (lockEventsMode)
        {
            _lock.ButtonPressed = true;
            _lockEvents.ButtonPressed = true;
            _lockEditor.SetEvidenceValues(jamPreview: false);
            _lockEventsEditor.SetEvidenceValues(mode);
            _notes.Text =
                "Changement de verrou gouverné avec méthode, justificatif, acteur et veto contrôlé, prévisualisé sans changer la scène.";
            UpdateLockFields();
            UpdateLockEventsFields();
        }
        else if (containerEventsMode)
        {
            _containerEvents.ButtonPressed = true;
            _containerEventsEditor.SetEvidenceValues(mode);
            _notes.Text =
                "Événements gouvernés avec acteur, résultat, état précédent et autorité, prévisualisés sans changer la scène.";
            UpdateContainerEventsFields();
        }
        else if (lootMode)
        {
            _loot.ButtonPressed = true;
            _lootEditor.SetEvidenceValues(mode);
            _notes.Text =
                "Trésor gouverné par objets métier, catégories pondérées, quantité, qualité, moment, graine et état généré déterministe.";
            UpdateLootFields();
        }
        else if (trapMode)
        {
            _trap.ButtonPressed = true;
            _trapEditor.SetEvidenceValues(mode);
            _notes.Text =
                "Piège gouverné avec déclenchement, effet, détection, désamorçage, réarmement, propriété, VFX et politique alliée.";
            UpdateTrapFields();
        }
        else if (credentialMode)
        {
            _credentialPolicy.ButtonPressed = true;
            _credentialEditor.SetEvidenceValues(mode);
            _notes.Text =
                "Politique de justificatif gouvernée avec conservation, consommation, charges, partage, permission virtuelle et audit.";
            UpdateCredentialFields();
        }
        else if (lockMode)
        {
            _lock.ButtonPressed = true;
            _lockEditor.SetEvidenceValues(mode.Contains("jam", StringComparison.OrdinalIgnoreCase));
            _notes.Text =
                "Verrou gouverné par clé, compétence, code, piratage, permissions, reverrouillage et brouillage.";
            UpdateLockFields();
        }
        else if (conversationMode)
        {
            _conversation.ButtonPressed = true;
            _conversationEditor.SetEvidenceValues(
                mode.Contains("used", StringComparison.OrdinalIgnoreCase));
            _notes.Text =
                "Conversations gouvernées par interaction, état, utilisateur, objet et événement, avec résolution prévisualisée.";
            UpdateConversationFields();
        }
        else if (factionMode)
        {
            _factionOwnership.ButtonPressed = true;
            _factionEditor.SetEvidenceValues(
                mode.Contains("decision", StringComparison.OrdinalIgnoreCase));
            _notes.Text = "Propriété de faction gouvernée avec hostilité, crime, accès et ciblage prévisualisés.";
            UpdateFactionFields();
        }
        else if (plotProtectionMode)
        {
            _protection.ButtonPressed = true;
            _protectionEditor.SetEvidenceValues(
                mode.Contains("override", StringComparison.OrdinalIgnoreCase));
            _notes.Text = "Protection narrative gouvernée avec feedback et dérogation MJ auditée.";
            UpdateProtectionFields();
        }
        else if (destructibilityMode)
        {
            _health.ButtonPressed = true;
            _damageableEditor.SetEvidenceValues(
                mode.Contains("destruction", StringComparison.OrdinalIgnoreCase));
            _notes.Text = "Destructibilité gouvernée avec dégâts, débris et réparation prévisualisés.";
            UpdateDamageFields();
        }
        else if (initialAnimationMode)
        {
            _stateMachineEditor.SetEvidenceValues();
            _notes.Text = "États visuels gouvernés avec aperçu de transition avant application.";
        }
        else if (transformMode)
        {
            _positionX.Value = 1.13;
            _positionY.Value = .25;
            _positionZ.Value = -.13;
            _rotationX.Value = 0;
            _rotationY.Value = 37;
            _rotationZ.Value = 0;
            _scaleX.Value = 1.1;
            _scaleY.Value = 1.25;
            _scaleZ.Value = .9;
            _pivotX.Value = 0;
            _pivotY.Value = 1.2;
            _pivotZ.Value = 0;
            _snap.Value = .25;
            if (_parent.ItemCount > 1)
            {
                _parent.Select(1);
            }
            _alignToSurface.ButtonPressed = true;
            _transformAnimation.Text = "animation:stele-rise";
            _notes.Text = "Transformation précise avec parentage et animation gouvernés.";
        }
        else if (identityMode)
        {
            _name.Text = "Stèle du veilleur";
            _slug.Text = "stele-du-veilleur";
            _prompt.Text = "Examiner les runes";
            _localizationEditor.SetEvidenceValues([
                new("en-US", "Watcher stele", "Examine the runes"),
                new("de-DE", "Stele des Wächters", "Runen untersuchen"),
            ]);
            _tags.Text = "décor, runes, veilleur";
            _notes.Text = "Repère localisé avec apparence gouvernée et VFX discret.";
            _appearanceEditor.SetEvidenceValues(
                "mesh:stele, mesh:socle",
                "material:pierre, material:runes",
                "asset:stele-icon",
                "asset:stele-portrait",
                "vfx:runes-lueur");
        }
        else if (staticMode)
        {
            _role.Select((int)StudioPlaceableRole.StaticDecoration);
            _batched.ButtonPressed = true;
            _instanced.ButtonPressed = true;
            _occluder.ButtonPressed = true;
            _mergeEligible.ButtonPressed = mode.Contains("merge", StringComparison.OrdinalIgnoreCase) ||
                                           mode.Contains("collisionless", StringComparison.OrdinalIgnoreCase);
            _staticCollision.ButtonPressed = !_mergeEligible.ButtonPressed;
            if (mode.Contains("collisionless", StringComparison.OrdinalIgnoreCase))
            {
                _collisionEnabled.ButtonPressed = false;
                _navigationObstacle.ButtonPressed = false;
                UpdateCollisionFields();
            }
            _notes.Text = mode.Contains("preview", StringComparison.OrdinalIgnoreCase)
                ? "Aperçu des économies de rendu avant fusion du décor."
                : "Décor répétable, occludeur et optimisé pour la scène.";
            UpdateRoleFields();
        }
        else if (interactionMode)
        {
            var firstId = _draft.InteractionVerbs.FirstOrDefault()?.Id ?? Guid.NewGuid();
            _verbEditor.Load([
                new(firstId, "Examiner", "placeable-inspected", "player.can.inspect", null, null),
                new(Guid.NewGuid(), "Activer", "placeable-activated", "power.available", null, null),
            ]);
            _role.Select((int)StudioPlaceableRole.Interactable);
            _prompt.Text = "Choisir une action";
            _interactionDistance.Value = mode.Contains("focus", StringComparison.OrdinalIgnoreCase) ? 1.75 : 2.5;
            _interactionAngle.Value = mode.Contains("focus", StringComparison.OrdinalIgnoreCase) ? 75 : 120;
            _interactionDuration.Value = .4;
            _requiresFocus.ButtonPressed = true;
            SelectInteractionAuthority("server");
            _interactionConditions.Text = "player.conscious, line_of_sight";
            _onUsedEnabled.ButtonPressed = true;
            _onUsedEventEditor.SetEvidenceValues("1.0.0", "InteractionExecuted");
            _scripts.Text = "onUse=scripts/placeable_used";
            UpdateRoleFields();
            UpdateOnUsedFields();
        }
        else if (containerMode)
        {
            _container.ButtonPressed = true;
            _containerEditor.SetEvidenceValues(
                36,
                240,
                "party",
                persistent: true,
                replicated: true,
                "inventory:coffre-expedition",
                "loot:coffre-expedition",
                "ui:placeable-chest",
                requiredAtRuntime: true);
            _notes.Text = "Réserve persistante du groupe, partagée par le serveur.";
            UpdateContainerFields();
        }
        else if (mode.Contains("interaction", StringComparison.OrdinalIgnoreCase))
        {
            _role.Select((int)StudioPlaceableRole.Interactable);
            _prompt.Text = "Examiner le décor";
            _interactionDistance.Value = 2.5;
            _interactionAngle.Value = 120;
            _interactionDuration.Value = .4;
            _requiresFocus.ButtonPressed = true;
            _scripts.Text = "onUse=inspecter_decor";
            UpdateRoleFields();
        }
        else if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase))
        {
            _notes.Text = "Repère visuel partagé par toutes les instances de ce blueprint.";
            _occluder.ButtonPressed = true;
        }
        Preview();
        if (lifecycleEventsMode)
        {
            if (mode.Contains("used", StringComparison.OrdinalIgnoreCase))
                _onUsedEventEditor.FocusBindings();
            else if (mode.Contains("disturbed", StringComparison.OrdinalIgnoreCase))
                _contentsChangedEditor.FocusBindings();
            else
                _damageEventsEditor.FocusBindings();
            _evidenceScrollControl = mode.Contains("disturbed", StringComparison.OrdinalIgnoreCase)
                ? _contentsChangedEditor
                : mode.Contains("used", StringComparison.OrdinalIgnoreCase)
                    ? _onUsedEventEditor
                    : _damageEventsEditor;
        }
        else if (lockEventsMode)
        {
            if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("transition", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("veto", StringComparison.OrdinalIgnoreCase))
            {
                _lockEventsEditor.FocusPreview();
            }
            else if (mode.Contains("payload", StringComparison.OrdinalIgnoreCase))
            {
                _lockEventsEditor.FocusPayload();
            }
            else
            {
                _lockEventsEditor.FocusBindings();
            }
            _evidenceScrollControl = _lockEventsEditor;
        }
        else if (containerEventsMode)
        {
            if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("close", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("open", StringComparison.OrdinalIgnoreCase))
            {
                _containerEventsEditor.FocusPreview();
            }
            else if (mode.Contains("payload", StringComparison.OrdinalIgnoreCase))
            {
                _containerEventsEditor.FocusPayload();
            }
            else
            {
                _containerEventsEditor.FocusBindings();
            }
            _evidenceScrollControl = _containerEventsEditor;
        }
        else if (lootMode)
        {
            if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase))
            {
                _lootEditor.FocusPreview();
            }
            else if (mode.Contains("reset", StringComparison.OrdinalIgnoreCase))
            {
                _lootEditor.FocusGeneration();
            }
            else if (mode.Contains("state", StringComparison.OrdinalIgnoreCase))
            {
                _lootEditor.FocusGeneratedState();
            }
            else
            {
                _lootEditor.FocusTable();
            }
            _evidenceScrollControl = _lootEditor;
        }
        else if (trapMode)
        {
            _trapEditor.FocusTrap();
            _evidenceScrollControl = _trapEditor;
        }
        else if (credentialMode)
        {
            var evidenceScroll = mode.Contains("preview", StringComparison.OrdinalIgnoreCase)
                ? 3_900
                : mode.Contains("virtual", StringComparison.OrdinalIgnoreCase)
                    ? 3_650
                    : mode.Contains("charge", StringComparison.OrdinalIgnoreCase)
                        ? 3_500
                        : 3_300;
            if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase))
            {
                _credentialEditor.FocusPreview();
            }
            else if (mode.Contains("virtual", StringComparison.OrdinalIgnoreCase))
            {
                _credentialEditor.FocusVirtualPermission();
            }
            else if (mode.Contains("charge", StringComparison.OrdinalIgnoreCase))
            {
                _credentialEditor.FocusCharges();
            }
            else
            {
                _credentialEditor.FocusPolicy();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (lockMode)
        {
            var evidenceScroll = mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                                 mode.Contains("jam", StringComparison.OrdinalIgnoreCase)
                ? 2_800
                : mode.Contains("lifecycle", StringComparison.OrdinalIgnoreCase)
                    ? 2_600
                    : 2_350;
            if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("jam", StringComparison.OrdinalIgnoreCase))
            {
                _lockEditor.FocusPreview();
            }
            else if (mode.Contains("lifecycle", StringComparison.OrdinalIgnoreCase))
            {
                _lockEditor.FocusLifecycle();
            }
            else
            {
                _lockEditor.FocusMechanisms();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (conversationMode)
        {
            var evidenceScroll = mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                                 mode.Contains("used", StringComparison.OrdinalIgnoreCase)
                ? 6_850
                : mode.Contains("sources", StringComparison.OrdinalIgnoreCase)
                    ? 6_100
                    : 5_350;
            if (mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("used", StringComparison.OrdinalIgnoreCase))
            {
                _conversationEditor.FocusPreview();
            }
            else
            {
                _conversationEditor.FocusBindings();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (factionMode)
        {
            var evidenceScroll = mode.Contains("decision", StringComparison.OrdinalIgnoreCase)
                ? 4_850
                : mode.Contains("policies", StringComparison.OrdinalIgnoreCase)
                    ? 4_550
                    : 4_250;
            if (mode.Contains("decision", StringComparison.OrdinalIgnoreCase))
            {
                _factionEditor.FocusDecision();
            }
            else if (mode.Contains("policies", StringComparison.OrdinalIgnoreCase))
            {
                _factionEditor.FocusPolicies();
            }
            else
            {
                _factionEditor.FocusPrimaryFaction();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (plotProtectionMode)
        {
            var evidenceScroll = mode.Contains("override", StringComparison.OrdinalIgnoreCase)
                ? 3_750
                : mode.Contains("permissions", StringComparison.OrdinalIgnoreCase)
                    ? 3_450
                    : 2_200;
            if (mode.Contains("override", StringComparison.OrdinalIgnoreCase))
            {
                _protectionEditor.FocusOverride();
            }
            else if (mode.Contains("permissions", StringComparison.OrdinalIgnoreCase))
            {
                _protectionEditor.FocusPermissions();
            }
            else
            {
                _protectionEditor.FocusFirstAction();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (destructibilityMode)
        {
            var evidenceScroll = mode.Contains("destruction", StringComparison.OrdinalIgnoreCase)
                ? 2_650
                : mode.Contains("resistances", StringComparison.OrdinalIgnoreCase)
                    ? 2_400
                    : 2_180;
            if (mode.Contains("destruction", StringComparison.OrdinalIgnoreCase))
            {
                _damageableEditor.FocusDestruction();
            }
            else if (mode.Contains("resistances", StringComparison.OrdinalIgnoreCase))
            {
                _damageableEditor.FocusFirstResistance();
            }
            else
            {
                _damageableEditor.FocusHealth();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (initialAnimationMode)
        {
            var evidenceScroll = mode.Contains("transition", StringComparison.OrdinalIgnoreCase)
                ? 2_850
                : mode.Contains("states", StringComparison.OrdinalIgnoreCase)
                    ? 2_300
                    : 2_030;
            if (mode.Contains("transition", StringComparison.OrdinalIgnoreCase))
            {
                _stateMachineEditor.FocusFirstTransition();
            }
            else
            {
                _stateMachineEditor.FocusInitialState();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (transformMode)
        {
            var evidenceScroll = mode.Contains("parent", StringComparison.OrdinalIgnoreCase) ? 1_900 : 1_650;
            if (mode.Contains("animation", StringComparison.OrdinalIgnoreCase))
            {
                _transformAnimation.GrabFocus();
                evidenceScroll = 1_980;
            }
            else if (mode.Contains("pivot", StringComparison.OrdinalIgnoreCase))
            {
                _pivotY.GetLineEdit().GrabFocus();
                evidenceScroll = 1_760;
            }
            else
            {
                _positionX.GetLineEdit().GrabFocus();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (identityMode)
        {
            var evidenceScroll = 0;
            if (mode.Contains("appearance", StringComparison.OrdinalIgnoreCase))
            {
                _appearanceEditor.FocusPortrait();
                evidenceScroll = 560;
            }
            else if (mode.Contains("localization", StringComparison.OrdinalIgnoreCase))
            {
                _localizationEditor.FocusFirstLocalization();
                evidenceScroll = 220;
            }
            else
            {
                _name.GrabFocus();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (containerMode)
        {
            var evidenceScroll = 760;
            if (mode.Contains("references", StringComparison.OrdinalIgnoreCase))
            {
                _containerEditor.FocusReferences();
                evidenceScroll = 1_140;
            }
            else if (mode.Contains("access", StringComparison.OrdinalIgnoreCase))
            {
                _containerEditor.FocusAccess();
                evidenceScroll = 920;
            }
            else
            {
                _containerEditor.FocusCapacity();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        else if (interactionMode)
        {
            var evidenceScroll = 480;
            if (mode.Contains("event", StringComparison.OrdinalIgnoreCase))
            {
                _onUsedEventEditor.FocusBindings();
                evidenceScroll = 1_000;
            }
            else if (mode.Contains("focus", StringComparison.OrdinalIgnoreCase))
            {
                _interactionDistance.GetLineEdit().GrabFocus();
                evidenceScroll = 720;
            }
            else
            {
                _verbEditor.FocusFirstAction();
            }
            _formScroll.ScrollVertical = evidenceScroll;
            _formScroll.SetDeferred("scroll_vertical", evidenceScroll);
            _evidenceScrollTarget = evidenceScroll;
        }
        return Visible && _result?.Succeeded == true && ShowsHumanSurface;
    }

    internal void FinalizeEvidenceLayout()
    {
        if (_evidenceScrollControl is { } control)
        {
            var controlTarget = Math.Max(0, (int)control.Position.Y - 40);
            _formScroll.ScrollVertical = controlTarget;
            _evidenceScrollTarget = controlTarget;
            return;
        }
        if (_evidenceScrollTarget is { } target)
        {
            _formScroll.ScrollVertical = target;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        Open();
        if (_draft is null)
        {
            Close();
            return false;
        }
        _notes.Text = "Preuve transactionnelle UX1-E";
        Preview();
        if (_result?.Succeeded != true)
        {
            Close();
            return false;
        }
        var apply = _session.ApplyApprovedPlaceable();
        var changed = apply.Succeeded && !ReferenceEquals(originalProject, _session.Project);
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        return changed && restored;
    }

    internal bool RunStaticUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenStaticDecoration();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _collisionEnabled.ButtonPressed = false;
        _navigationObstacle.ButtonPressed = false;
        _batched.ButtonPressed = false;
        _instanced.ButtonPressed = true;
        _occluder.ButtonPressed = true;
        _mergeEligible.ButtonPressed = true;
        _staticCollision.ButtonPressed = false;
        _notes.Text = "Preuve transactionnelle UX1-F";
        UpdateCollisionFields();
        UpdateRoleFields();
        Preview();
        var visibleContract = StaticPreviewReady &&
                              _staticHint.Text.Contains("fusion", StringComparison.OrdinalIgnoreCase);
        var apply = _session.ApplyApprovedPlaceable();
        var foundation = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId)?.Foundation;
        var canonical = apply.Succeeded && foundation is
        {
            Interaction: null,
            Collision.Enabled: false,
            StaticProp:
            {
                Batched: false,
                Instanced: true,
                Occluder: true,
                MergeEligible: true,
                CollisionEnabled: false,
            },
        };
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunInteractionUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenInteractable();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        var firstId = _draft.InteractionVerbs.FirstOrDefault()?.Id ?? Guid.NewGuid();
        _verbEditor.Load([
            new(firstId, "Examiner", "placeable-inspected", "player.can.inspect", null, null),
            new(Guid.NewGuid(), "Activer", "placeable-activated", "power.available", null, null),
        ]);
        _interactionDistance.Value = 2.75;
        _interactionAngle.Value = 100;
        _interactionDuration.Value = .5;
        _requiresFocus.ButtonPressed = true;
        SelectInteractionAuthority("server");
        _interactionConditions.Text = "player.conscious, line_of_sight";
        _onUsedEnabled.ButtonPressed = true;
        _onUsedEventEditor.SetEvidenceValues("1.0.0", "InteractionExecuted");
        UpdateRoleFields();
        UpdateOnUsedFields();
        Preview();
        var visibleContract = InteractionPreviewReady && InteractionVerbCount == 2;
        var apply = _session.ApplyApprovedPlaceable();
        var foundation = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId)?.Foundation;
        var canonical = apply.Succeeded && foundation is
        {
            StaticProp: null,
            Interaction:
            {
                MaximumDistance: 2.75,
                MaximumAngleDegrees: 100,
                RequiresFocus: true,
                DurationSeconds: .5,
                Authority: "server",
                Verbs.Count: 2,
            },
            OnUsed:
            {
                EventBinding: "InteractionExecuted",
                BeforeScriptId: "scripts/placeable_before_use",
                AfterScriptId: "scripts/placeable_after_use",
            },
            OnUsedRequiredAtRuntime: true,
        };
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1G_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"role={(foundation?.Interaction is not null && foundation.StaticProp is null).ToString().ToLowerInvariant()} " +
                $"distance={foundation?.Interaction?.MaximumDistance:0.##} " +
                $"angle={foundation?.Interaction?.MaximumAngleDegrees:0.##} " +
                $"focus={foundation?.Interaction?.RequiresFocus.ToString().ToLowerInvariant()} " +
                $"duration={foundation?.Interaction?.DurationSeconds:0.##} " +
                $"authority={foundation?.Interaction?.Authority} verbs={foundation?.Interaction?.Verbs.Count ?? 0} " +
                $"event={foundation?.OnUsed?.EventBinding ?? "absent"} " +
                $"before={foundation?.OnUsed?.BeforeScriptId ?? "absent"} " +
                $"after={foundation?.OnUsed?.AfterScriptId ?? "absent"} " +
                $"required={foundation?.OnUsedRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunContainerUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenContainer();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _container.ButtonPressed = true;
        _containerEditor.SetEvidenceValues(
            32,
            180,
            "party",
            persistent: true,
            replicated: true,
            "inventory:studio-container",
            "loot:studio-container",
            "ui:placeable-chest",
            requiredAtRuntime: true);
        UpdateContainerFields();
        Preview();
        var visibleContract = ContainerPreviewReady;
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var canonical = apply.Succeeded && profile is
        {
            Foundation.Components.HasContainer: true,
            OperationsRequiredAtRuntime: true,
            Operations.Container:
            {
                CapacitySlots: 32,
                CapacityMass: 180,
                OwnershipPolicy: "party",
                Persistent: true,
                Replicated: true,
                InventoryId: "inventory:studio-container",
                LootPolicyId: "loot:studio-container",
                UiProfileId: "ui:placeable-chest",
            },
        } && profile.Operations.Container.AccessPermissions.SequenceEqual(
            ["container.open", "container.deposit", "container.withdraw"]);
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1H_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"component={profile?.Foundation?.Components.HasContainer.ToString().ToLowerInvariant()} " +
                $"slots={profile?.Operations?.Container?.CapacitySlots ?? 0} " +
                $"mass={profile?.Operations?.Container?.CapacityMass ?? 0:0.##} " +
                $"owner={profile?.Operations?.Container?.OwnershipPolicy ?? "absent"} " +
                $"access={profile?.Operations?.Container?.AccessPermissions.Count ?? 0} " +
                $"persistent={profile?.Operations?.Container?.Persistent.ToString().ToLowerInvariant()} " +
                $"replicated={profile?.Operations?.Container?.Replicated.ToString().ToLowerInvariant()} " +
                $"required={profile?.OperationsRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunIdentityAppearanceUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenIdentityAppearance();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _name.Text = "Stèle de preuve Studio";
        _slug.Text = "stele-preuve-studio";
        _prompt.Text = "Examiner la stèle";
        _localizationEditor.SetEvidenceValues([
            new("en-US", "Studio proof stele", "Examine the stele"),
        ]);
        _tags.Text = "preuve, stèle, studio";
        _notes.Text = "Preuve transactionnelle UX1-I";
        _appearanceEditor.SetEvidenceValues(
            "mesh:stele-preuve, mesh:socle-preuve",
            "material:pierre-preuve",
            "asset:stele-preuve-icon",
            "asset:stele-preuve-portrait",
            "vfx:stele-preuve");
        Preview();
        var visibleContract = IdentityAppearancePreviewReady;
        var apply = _session.ApplyApprovedPlaceable();
        var foundation = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId)?.Foundation;
        var canonical = apply.Succeeded && foundation is
        {
            Slug: "stele-preuve-studio",
            AuthorNotes: "Preuve transactionnelle UX1-I",
            Visual:
            {
                IconAssetId: "asset:stele-preuve-icon",
                PortraitAssetId: "asset:stele-preuve-portrait",
            },
        } && foundation.Texts.Any(item => item.Locale == "fr-FR" && item.Name == "Stèle de preuve Studio") &&
                        foundation.Texts.Any(item => item.Locale == "en-US" && item.Name == "Studio proof stele") &&
                        foundation.Tags.SequenceEqual(["preuve", "stèle", "studio"]) &&
                        foundation.Visual.MeshIds.SequenceEqual(["mesh:stele-preuve", "mesh:socle-preuve"]) &&
                        foundation.Visual.MaterialIds.SequenceEqual(["material:pierre-preuve"]) &&
                        foundation.Visual.VisualEffectIds.SequenceEqual(["vfx:stele-preuve"]);
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1I_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"slug={foundation?.Slug ?? "absent"} locales={foundation?.Texts.Count ?? 0} " +
                $"meshes={foundation?.Visual.MeshIds.Count ?? 0} materials={foundation?.Visual.MaterialIds.Count ?? 0} " +
                $"icon={foundation?.Visual.IconAssetId ?? "absent"} " +
                $"portrait={foundation?.Visual.PortraitAssetId ?? "absent"} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunPreciseTransformUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenPreciseTransform();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _positionX.Value = 1.13;
        _positionY.Value = .25;
        _positionZ.Value = -.13;
        _rotationX.Value = 10;
        _rotationY.Value = 37;
        _rotationZ.Value = -5;
        _scaleX.Value = 1.1;
        _scaleY.Value = 1.25;
        _scaleZ.Value = .9;
        _pivotX.Value = 0;
        _pivotY.Value = 1.2;
        _pivotZ.Value = 0;
        _snap.Value = .25;
        _parent.Select(0);
        _alignToSurface.ButtonPressed = true;
        _transformAnimation.Text = "animation:studio-transform-proof";
        _notes.Text = "Preuve transactionnelle UX1-J";
        Preview();
        var visibleContract = PreciseTransformPreviewReady;
        var apply = _session.ApplyApprovedPlaceable();
        var foundation = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId)?.Foundation;
        var canonical = apply.Succeeded && foundation is
        {
            Transform:
            {
                Position: { X: 1.13f, Y: .25f, Z: -.13f },
                RotationDegrees: { X: 10, Y: 37, Z: -5 },
                Scale: { X: 1.1f, Y: 1.25f, Z: .9f },
                Pivot: { X: 0, Y: 1.2f, Z: 0 },
                SnapIncrement: .25,
                ParentId: null,
                AlignToSurface: true,
                TransformAnimationId: "animation:studio-transform-proof",
            },
        } && foundation.Validation?.AnimationIds.Contains(
            "animation:studio-transform-proof", StringComparer.Ordinal) == true;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1J_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"position={foundation?.Transform.Position} rotation={foundation?.Transform.RotationDegrees} " +
                $"scale={foundation?.Transform.Scale} pivot={foundation?.Transform.Pivot} " +
                $"snap={foundation?.Transform.SnapIncrement:0.##} parent={foundation?.Transform.ParentId?.ToString() ?? "root"} " +
                $"surface={foundation?.Transform.AlignToSurface.ToString().ToLowerInvariant()} " +
                $"animation={foundation?.Transform.TransformAnimationId ?? "absent"} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunInitialAnimationUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenInitialAnimation();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _stateMachineEditor.SetEvidenceValues();
        _notes.Text = "Preuve transactionnelle UX1-K";
        Preview();
        var visibleContract = InitialAnimationPreviewReady;
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var machine = profile?.Operations?.StateMachine;
        var canonical = apply.Succeeded && profile?.OperationsRequiredAtRuntime == true && machine is
        {
            InitialStateId: "active",
            States.Count: 6,
            Transitions.Count: 2,
        } && machine.States.All(state =>
            state.AnimationId == $"animation:studio-{state.StateId}" &&
            state.VisualOverrideId == $"visual:studio-{state.StateId}" &&
            state.Interactable == (state.StateId != "broken")) &&
            machine.Transitions.Any(transition =>
                transition is
                {
                    FromStateId: "closed",
                    ToStateId: "active",
                    Trigger: "power-on",
                    DurationSeconds: .65,
                    Condition: "power.available",
                }) &&
            profile.Foundation?.Validation?.AnimationIds.Contains(
                "animation:studio-active", StringComparer.Ordinal) == true;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1K_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"initial={machine?.InitialStateId ?? "absent"} states={machine?.States.Count ?? 0} " +
                $"transitions={machine?.Transitions.Count ?? 0} " +
                $"required={profile?.OperationsRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunDestructibilityUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenDestructibility();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _health.ButtonPressed = true;
        _damageableEditor.SetEvidenceValues(destructionPreview: true);
        _notes.Text = "Preuve transactionnelle UX1-L";
        UpdateDamageFields();
        Preview();
        var visibleContract = DestructibilityPreviewReady;
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var damageable = profile?.Operations?.Damageable;
        var canonical = apply.Succeeded && profile?.OperationsRequiredAtRuntime == true &&
                        profile.Foundation?.Components.HasHealth == true && damageable is
                        {
                            MaximumHealth: 180,
                            CurrentHealth: 140,
                            Armor: 12,
                            Repairable: true,
                            RepairCost: 18,
                            DestructionEffectId: "vfx:studio-balise-destruction",
                            DebrisDefinitionId: "debris:studio-balise",
                        } && damageable.Resistances.Count == 3 &&
                        damageable.Resistances["fire"] == .4 &&
                        damageable.VisualStageThresholds.SequenceEqual([.75, .5, .25]);
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1L_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"health={damageable?.CurrentHealth:0.##}/{damageable?.MaximumHealth:0.##} " +
                $"armor={damageable?.Armor:0.##} resistances={damageable?.Resistances.Count ?? 0} " +
                $"stages={damageable?.VisualStageThresholds.Count ?? 0} " +
                $"repair={damageable?.Repairable.ToString().ToLowerInvariant()} " +
                $"required={profile?.OperationsRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunPlotProtectionUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenPlotProtection();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _protection.ButtonPressed = true;
        _protectionEditor.SetEvidenceValues(gameMasterOverridePreview: true);
        _notes.Text = "Preuve transactionnelle UX1-M";
        UpdateProtectionFields();
        Preview();
        var visibleContract = PlotProtectionPreviewReady;
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var protection = profile?.Operations?.Protection;
        var canonical = apply.Succeeded && profile?.OperationsRequiredAtRuntime == true && protection is
        {
            Feedback: "Cet objet est protégé par le scénario principal.",
            AllowGameMasterOverride: true,
            AuditBinding: "PlaceableProtectionOverride",
        } && protection.ProtectedActions.SequenceEqual(["destroy", "move", "use"]) &&
            protection.RequiredPermissions.SequenceEqual(["placeable.plot.override", "placeable.maintain"]);
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1M_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"actions={protection?.ProtectedActions.Count ?? 0} " +
                $"permissions={protection?.RequiredPermissions.Count ?? 0} " +
                $"gm={protection?.AllowGameMasterOverride.ToString().ToLowerInvariant()} " +
                $"audit={protection?.AuditBinding ?? "absent"} " +
                $"required={profile?.OperationsRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunFactionOwnershipUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        var seeded = _session.EnsureUx1NEvidenceFactions();
        OpenFactionOwnership();
        if (_draft is null)
        {
            if (seeded)
            {
                _session.Undo();
            }
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _factionOwnership.ButtonPressed = true;
        _factionEditor.SetEvidenceValues(hostileActor: true);
        _notes.Text = "Preuve transactionnelle UX1-N";
        UpdateFactionFields();
        Preview();
        var visibleContract = FactionOwnershipPreviewReady;
        var expected = _factionEditor.Capture(_draft.FactionOwnership);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var ownership = profile?.FactionOwnership;
        var canonical = apply.Succeeded && profile?.FactionOwnershipRequiredAtRuntime == true && ownership is not null &&
                        ownership.PrimaryFactionId == expected.PrimaryFactionId &&
                        ownership.MembershipFactionIds.SequenceEqual(expected.MembershipFactionIds) &&
                        ownership.InheritHostility && ownership.UnauthorizedActionsAreCrime &&
                        ownership.MembersOnlyAccess && ownership.BlockFriendlyTargeting;
        var undoPlaceable = _session.Undo();
        var undoSeed = !seeded || _session.Undo().Succeeded;
        var restored = undoPlaceable.Succeeded && undoSeed &&
                       ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1N_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"memberships={ownership?.MembershipFactionIds.Count ?? 0} " +
                $"hostility={ownership?.InheritHostility.ToString().ToLowerInvariant()} " +
                $"crime={ownership?.UnauthorizedActionsAreCrime.ToString().ToLowerInvariant()} " +
                $"access={ownership?.MembersOnlyAccess.ToString().ToLowerInvariant()} " +
                $"targeting={ownership?.BlockFriendlyTargeting.ToString().ToLowerInvariant()} " +
                $"required={profile?.FactionOwnershipRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo_placeable={undoPlaceable.Succeeded.ToString().ToLowerInvariant()} " +
                $"undo_seed={undoSeed.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunConversationUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenConversation();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _conversation.ButtonPressed = true;
        _conversationEditor.SetEvidenceValues(previewUsed: false);
        _notes.Text = "Preuve transactionnelle UX1-O";
        UpdateConversationFields();
        Preview();
        var visibleContract = ConversationPreviewReady &&
                              _conversationEditor.BindingCount == 5 &&
                              _conversationEditor.TriggerCount == 5;
        var expected = _conversationEditor.Capture(_draft.Conversation);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var conversation = profile?.Conversation;
        var canonical = apply.Succeeded && profile?.ConversationRequiredAtRuntime == true &&
                        conversation is not null &&
                        conversation.Bindings.Count == expected.Bindings.Count &&
                        conversation.Bindings.Select(binding => binding.Trigger).Distinct().Count() == 5 &&
                        conversation.Bindings.Select(binding => binding.DialogueId)
                            .SequenceEqual(expected.Bindings.Select(binding => binding.DialogueId)) &&
                        conversation.Bindings[0].OncePerUser;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1O_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"bindings={conversation?.Bindings.Count ?? 0} " +
                $"triggers={conversation?.Bindings.Select(binding => binding.Trigger).Distinct().Count() ?? 0} " +
                $"required={profile?.ConversationRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunLockUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenLock();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _lock.ButtonPressed = true;
        _lockEditor.SetEvidenceValues(jamPreview: false);
        _notes.Text = "Preuve transactionnelle UX1-P";
        UpdateLockFields();
        Preview();
        var visibleContract = LockPreviewReady;
        var expected = _lockEditor.Capture(_draft.Lock);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var lockProfile = profile?.Operations?.Lock;
        var canonical = apply.Succeeded && profile?.OperationsRequiredAtRuntime == true &&
                        profile.Foundation?.Components.HasLock == true && lockProfile is not null &&
                        lockProfile.InitialState == expected.InitialState &&
                        lockProfile.CredentialIds.Count == expected.OtherCredentialIds.Count +
                        (expected.KeyItemId is null ? 0 : 1) &&
                        lockProfile.SkillId == expected.SkillId &&
                        lockProfile.CodeHash is not null &&
                        lockProfile.Hackable && lockProfile.Relockable && lockProfile.CanJam &&
                        lockProfile.RequiredPermissions.Count == expected.RequiredPermissions.Count;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1P_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"credentials={lockProfile?.CredentialIds.Count ?? 0} " +
                $"skill={lockProfile?.SkillId ?? "absent"} " +
                $"code={(lockProfile?.CodeHash is not null).ToString().ToLowerInvariant()} " +
                $"hacking={lockProfile?.Hackable.ToString().ToLowerInvariant()} " +
                $"relock={lockProfile?.Relockable.ToString().ToLowerInvariant()} " +
                $"jam={lockProfile?.CanJam.ToString().ToLowerInvariant()} " +
                $"required={profile?.OperationsRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunCredentialUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenCredentialPolicy();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _credentialPolicy.ButtonPressed = true;
        _credentialEditor.SetEvidenceValues("ux1-q-charge");
        _notes.Text = "Preuve transactionnelle UX1-Q";
        UpdateCredentialFields();
        Preview();
        var visibleContract = CredentialPreviewReady;
        var expected = _credentialEditor.Capture(_draft.Credential);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var credential = profile?.Credential;
        var canonical = apply.Succeeded && profile?.CredentialRequiredAtRuntime == true &&
                        credential is not null &&
                        credential.Consumption == expected.Consumption &&
                        credential.Charges == expected.Charges &&
                        credential.AuditUse == expected.AuditUse &&
                        credential.CredentialId.StartsWith("item:", StringComparison.Ordinal);
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1Q_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"mode={credential?.Consumption.ToString() ?? "absent"} " +
                $"charges={credential?.Charges ?? -1} " +
                $"audit={credential?.AuditUse.ToString().ToLowerInvariant()} " +
                $"required={profile?.CredentialRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunTrapUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenTrap();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _trap.ButtonPressed = true;
        _trapEditor.SetEvidenceValues("ux1-r-trigger");
        _notes.Text = "Preuve transactionnelle UX1-R";
        UpdateTrapFields();
        Preview();
        var visibleContract = TrapPreviewReady;
        var expected = _trapEditor.Capture(_draft.Trap);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var trap = profile?.SecurityEvents?.Trap;
        var canonical = apply.Succeeded && profile?.TrapRequiredAtRuntime == true &&
                        profile.Foundation?.Components.HasTrap == true && trap is not null &&
                        trap.TriggerId == expected.TriggerId && trap.PayloadId == expected.PayloadId &&
                        trap.DetectionDifficulty.Equals(expected.DetectionDifficulty) &&
                        trap.DisarmDifficulty.Equals(expected.DisarmDifficulty) &&
                        trap.ResetDelaySeconds.Equals(expected.ResetDelaySeconds) &&
                        trap.OwnerId == expected.OwnerId && trap.VisualEffectId == expected.VisualEffectId &&
                        trap.FriendlyPolicy == expected.FriendlyPolicy && trap.Armed == expected.Armed &&
                        trap.OneShot == expected.OneShot;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1R_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"trigger={trap?.TriggerId ?? "absent"} payload={trap?.PayloadId ?? "absent"} " +
                $"detect={trap?.DetectionDifficulty ?? -1} disarm={trap?.DisarmDifficulty ?? -1} " +
                $"required={profile?.TrapRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunLootUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenInitialTreasure();
        if (_draft?.Loot is null || _catalog?.Items.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1S_UI_TRANSACTION_FAILED reason=no-placeable-or-item");
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _loot.ButtonPressed = true;
        _lootEditor.SetEvidenceValues("ux1-s-reset-preview");
        _notes.Text = "Preuve transactionnelle UX1-S";
        UpdateLootFields();
        Preview();
        var visibleContract = LootPreviewReady;
        var expected = _lootEditor.Capture(_draft.Loot);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var loot = profile?.SecurityEvents?.Loot;
        var canonical = apply.Succeeded && profile?.LootRequiredAtRuntime == true && loot is not null &&
                        loot.Timing == expected.Timing && loot.Seed == expected.Seed &&
                        loot.PersistGeneratedState == expected.PersistGeneratedState &&
                        loot.LootTable.Entries.Count == expected.Entries.Count &&
                        loot.LootTable.Entries.Select(entry => entry.ItemDefinitionId).SequenceEqual(
                            expected.Entries.Select(entry => entry.ItemBlueprintId.Value)) &&
                        loot.Generation == 0 && loot.SavedResults.Count == 0;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1S_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"timing={loot?.Timing.ToString() ?? "absent"} seed={loot?.Seed ?? -1} " +
                $"entries={loot?.LootTable.Entries.Count ?? -1} " +
                $"required={profile?.LootRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunContainerEventsUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenContainerEvents();
        if (_draft?.ContainerEvents is null)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1T_UI_TRANSACTION_FAILED reason=no-placeable");
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _containerEvents.ButtonPressed = true;
        _containerEventsEditor.SetEvidenceValues("ux1-t-close");
        _notes.Text = "Preuve transactionnelle UX1-T";
        UpdateContainerEventsFields();
        Preview();
        var visibleContract = ContainerEventsPreviewReady;
        var expected = _containerEventsEditor.Capture(_draft.ContainerEvents);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var containerEvents = profile?.SecurityEvents?.ContainerEvents;
        var canonical = apply.Succeeded && profile?.ContainerEventsRequiredAtRuntime == true &&
                        containerEvents is not null &&
                        containerEvents.ForgeScriptApiVersion == expected.ForgeScriptApiVersion &&
                        containerEvents.OpenedBinding == expected.OpenedBinding &&
                        containerEvents.ClosedBinding == expected.ClosedBinding &&
                        containerEvents.IncludeActor == expected.IncludeActor &&
                        containerEvents.IncludeResult == expected.IncludeResult &&
                        containerEvents.IncludePreviousState == expected.IncludePreviousState &&
                        containerEvents.IncludeAuthority == expected.IncludeAuthority;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1T_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"opened={containerEvents?.OpenedBinding ?? "absent"} " +
                $"closed={containerEvents?.ClosedBinding ?? "absent"} " +
                $"required={profile?.ContainerEventsRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    internal bool RunLockEventsUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenLockEvents();
        if (_draft?.LockEvents is null)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1U_UI_TRANSACTION_FAILED reason=no-placeable");
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        _lock.ButtonPressed = true;
        _lockEvents.ButtonPressed = true;
        _lockEditor.SetEvidenceValues(jamPreview: false);
        _lockEventsEditor.SetEvidenceValues("ux1-u-veto");
        _notes.Text = "Preuve transactionnelle UX1-U";
        UpdateLockFields();
        UpdateLockEventsFields();
        Preview();
        var visibleContract = LockEventsPreviewReady;
        var expected = _lockEventsEditor.Capture(_draft.LockEvents);
        var apply = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var lockEvents = profile?.SecurityEvents?.LockEvents;
        var canonical = apply.Succeeded && profile?.LockEventsRequiredAtRuntime == true &&
                        profile.Operations?.Lock is not null &&
                        lockEvents is not null &&
                        lockEvents.ForgeScriptApiVersion == expected.ForgeScriptApiVersion &&
                        lockEvents.ChangingBinding == expected.ChangingBinding &&
                        lockEvents.ChangedBinding == expected.ChangedBinding &&
                        lockEvents.IncludeMethod == expected.IncludeMethod &&
                        lockEvents.IncludeCredential == expected.IncludeCredential &&
                        lockEvents.IncludeActor == expected.IncludeActor &&
                        lockEvents.ControlledVeto == expected.ControlledVeto;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        if (!visibleContract || !canonical || !restored)
        {
            GD.PrintErr(
                "OPENRPG_FORGE_STUDIO_UX1U_UI_TRANSACTION_FAILED " +
                $"visible={visibleContract.ToString().ToLowerInvariant()} " +
                $"apply={apply.Succeeded.ToString().ToLowerInvariant()} " +
                $"changing={lockEvents?.ChangingBinding ?? "absent"} " +
                $"changed={lockEvents?.ChangedBinding ?? "absent"} " +
                $"required={profile?.LockEventsRequiredAtRuntime.ToString().ToLowerInvariant()} " +
                $"diagnostic={_session.LastPlaceableDiagnosticCode} " +
                $"preview_message={_result?.Message ?? "absent"} status={_status.Text} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        Close();
        return visibleContract && canonical && restored;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key ||
            key.Keycode != Key.Escape)
        {
            return;
        }
        Close();
        GetViewport().SetInputAsHandled();
    }

    private void LoadBlueprint(BlueprintId blueprintId)
    {
        _updating = true;
        _session.CancelPlaceablePreview();
        _result = null;
        _draft = _session.CreatePlaceableDraft(blueprintId);
        if (_draft is null || _catalog is null)
        {
            _updating = false;
            SetEditingEnabled(false);
            return;
        }
        var option = _catalog.Blueprints.Single(candidate => candidate.Id == blueprintId);
        _blueprintSummary.Text =
            $"{option.InstanceCount} instance(s) dans le module · " +
            (option.HasProfile ? "fiche canonique existante" : "aucune fiche canonique");
        _empty.Visible = !option.HasProfile;
        _empty.Text = option.HasProfile
            ? string.Empty
            : "État vide · le Studio prépare une fiche sûre à partir du blueprint et du visuel le plus utilisé.";

        PopulateVisuals(_draft.VisualAssetId);
        _slug.Text = _draft.Slug;
        _name.Text = _draft.Name;
        _prompt.Text = _draft.Prompt;
        _localizationEditor.Load(_draft.AdditionalLocalizedTexts);
        _tags.Text = _draft.TagsText;
        _notes.Text = _draft.AuthorNotes;
        _appearanceEditor.Load(_draft.Appearance);
        _overrides.Text = _draft.ExplicitOverridesText;
        _collisionEnabled.ButtonPressed = _draft.CollisionEnabled;
        _navigationObstacle.ButtonPressed = _draft.NavigationObstacle;
        SelectCollisionShape(_draft.CollisionShape);
        SetVector(_collisionX, _collisionY, _collisionZ, _draft.CollisionSize);
        _role.Select((int)_draft.Role);
        _batched.ButtonPressed = _draft.Batched;
        _instanced.ButtonPressed = _draft.Instanced;
        _occluder.ButtonPressed = _draft.Occluder;
        _mergeEligible.ButtonPressed = _draft.MergeEligible;
        _staticCollision.ButtonPressed = _draft.StaticCollisionEnabled;
        _interactionDistance.Value = _draft.InteractionDistance;
        _interactionAngle.Value = _draft.InteractionAngle;
        _interactionDuration.Value = _draft.InteractionDuration;
        _requiresFocus.ButtonPressed = _draft.RequiresFocus;
        _verbEditor.Load(_draft.InteractionVerbs);
        SelectInteractionAuthority(_draft.InteractionAuthority);
        _interactionConditions.Text = _draft.InteractionConditionsText;
        _onUsedEnabled.ButtonPressed = _draft.OnUsedEnabled;
        _onUsedEventEditor.Load(_draft, _draft.OnUsedEnabled);
        _health.ButtonPressed = _draft.HasHealth;
        _health.Disabled = _draft.Damageable.ExistingProfile;
        _damageableEditor.Load(_draft.Damageable, _draft.HasHealth, _draft.RequireOperationsRuntime);
        _protection.ButtonPressed = _draft.HasProtection;
        _protection.Disabled = _draft.Protection.ExistingProfile;
        _protectionEditor.Load(_draft.Protection, _draft.HasProtection, _draft.RequireOperationsRuntime);
        _factionOwnership.ButtonPressed = _draft.HasFactionOwnership;
        _factionOwnership.Disabled = _draft.FactionOwnership.ExistingProfile;
        _factionEditor.Load(
            _draft.FactionOwnership,
            _catalog.Factions,
            _draft.HasFactionOwnership,
            _draft.RequireFactionOwnershipRuntime);
        _conversation.ButtonPressed = _draft.HasConversation;
        _conversation.Disabled = _draft.Conversation.ExistingProfile;
        _conversationEditor.Load(
            _draft.Conversation,
            _catalog.Dialogues,
            _catalog.Items,
            _draft.HasConversation,
            _draft.RequireConversationRuntime);
        _container.ButtonPressed = _draft.HasContainer;
        _containerEditor.Load(_draft.Container, _draft.RequireOperationsRuntime);
        _stateMachineEditor.Load(
            _draft.StateMachine,
            _draft.HasStateMachine,
            _draft.RequireOperationsRuntime);
        _lock.ButtonPressed = _draft.HasLock;
        _lock.Disabled = _draft.Lock.ExistingProfile;
        _lockEditor.Load(
            _draft.Lock,
            _catalog.Items,
            _draft.HasLock,
            _draft.RequireOperationsRuntime);
        _credentialPolicy.ButtonPressed = _draft.HasCredentialPolicy;
        _credentialPolicy.Disabled = _draft.Credential.ExistingPolicy;
        _credentialEditor.Load(
            _draft.Credential,
            _catalog.Items,
            _draft.HasCredentialPolicy,
            _draft.RequireCredentialRuntime);
        _trap.ButtonPressed = _draft.HasTrap;
        _trap.Disabled = _draft.Trap.ExistingProfile;
        _trapEditor.Load(_draft.Trap, _draft.HasTrap, _draft.RequireTrapRuntime);
        _loot.ButtonPressed = _draft.HasLoot;
        _loot.Disabled = _draft.Loot?.ExistingProfile == true;
        _lootEditor.Load(
            _draft.Loot!,
            _catalog.Items,
            _draft.HasLoot,
            _draft.RequireLootRuntime);
        _containerEvents.ButtonPressed = _draft.HasContainerEvents;
        _containerEvents.Disabled = _draft.ContainerEvents?.ExistingProfile == true;
        _containerEventsEditor.Load(
            _draft.ContainerEvents!,
            _draft.HasContainerEvents,
            _draft.RequireContainerEventsRuntime);
        _lockEvents.ButtonPressed = _draft.HasLockEvents;
        _lockEvents.Disabled = _draft.LockEvents?.ExistingProfile == true;
        _lockEventsEditor.Load(
            _draft.LockEvents!,
            _draft.HasLockEvents,
            _draft.RequireLockEventsRuntime);
        _contentsChanged.ButtonPressed = _draft.HasContentsChanged;
        _contentsChanged.Disabled = _draft.ContentsChanged?.ExistingProfile == true;
        _contentsChangedEditor.Load(
            _draft.ContentsChanged!,
            _draft.HasContentsChanged,
            _draft.RequireContentsChangedRuntime);
        _damageEvents.ButtonPressed = _draft.HasDamageEvents;
        _damageEvents.Disabled = _draft.OriginalCompositionEvents is not null;
        _damageEventsEditor.Load(
            _draft.DamageEvents!,
            _draft.HasDamageEvents,
            _draft.RequireDamageEventsRuntime);
        _physics.ButtonPressed = _draft.HasPhysics;
        _scripts.Text = _draft.ScriptBindingsText;
        SetVector(_positionX, _positionY, _positionZ, _draft.Position);
        SetVector(_rotationX, _rotationY, _rotationZ, _draft.RotationDegrees);
        SetVector(_scaleX, _scaleY, _scaleZ, _draft.Scale);
        SetVector(_pivotX, _pivotY, _pivotZ, _draft.Pivot);
        _snap.Value = _draft.SnapIncrement;
        PopulateParents(_draft.BlueprintId, _draft.ParentId);
        _alignToSurface.ButtonPressed = _draft.AlignToSurface;
        _transformAnimation.Text = _draft.TransformAnimationId;
        _requireRuntime.ButtonPressed = _draft.RequireRuntime;
        _previewSummary.Text = "Aucun changement appliqué · prévisualisez le brouillon.";
        _previewState.Text = "Pose, pivot, grille et instances seront calculés ici.";
        _apply.Disabled = true;
        _selectInstances.Disabled = true;
        _updating = false;
        UpdateRoleFields();
        UpdateCollisionFields();
        UpdateOnUsedFields();
        UpdateContainerFields();
        UpdateDamageFields();
        UpdateProtectionFields();
        UpdateFactionFields();
        UpdateConversationFields();
        UpdateLockFields();
        UpdateCredentialFields();
        UpdateTrapFields();
        UpdateLootFields();
        UpdateContainerEventsFields();
        UpdateLockEventsFields();
        UpdateContentsChangedFields();
        UpdateDamageEventsFields();
        SetEditingEnabled(true);
        SetStatus(option.HasProfile ? "Fiche chargée sans modification." : "Brouillon initialisé depuis la scène.",
            error: false);
    }

    private void PopulateVisuals(string selectedAssetId)
    {
        _visualIds.Clear();
        _visual.Clear();
        var selectedIndex = -1;
        if (_catalog is null)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(selectedAssetId) && !_catalog.Visuals.Any(option =>
                string.Equals(option.AssetId, selectedAssetId, StringComparison.Ordinal)))
        {
            _visualIds.Add(selectedAssetId);
            _visual.AddItem("Visuel existant du blueprint");
            selectedIndex = 0;
        }
        foreach (var option in _catalog.Visuals)
        {
            _visualIds.Add(option.AssetId);
            var usage = option.UsageCount > 0 ? $" · utilisé {option.UsageCount} fois" : string.Empty;
            _visual.AddItem(option.DisplayName + usage);
            _visual.SetItemTooltip(_visual.ItemCount - 1, $"Licence {option.License} · asset local vérifié");
            if (string.Equals(option.AssetId, selectedAssetId, StringComparison.Ordinal))
            {
                selectedIndex = _visual.ItemCount - 1;
            }
        }
        if (selectedIndex < 0 && _visual.ItemCount > 0)
        {
            selectedIndex = 0;
        }
        if (selectedIndex >= 0)
        {
            _visual.Select(selectedIndex);
        }
    }

    private void PopulateParents(BlueprintId blueprintId, Guid? selectedParentId)
    {
        _parentIds.Clear();
        _parent.Clear();
        _parentIds.Add(null);
        _parent.AddItem("Aucun parent · racine de la scène");
        var selectedIndex = 0;
        if (_catalog is not null)
        {
            foreach (var option in _catalog.Parents.Where(option => option.BlueprintId != blueprintId))
            {
                _parentIds.Add(option.ProfileId);
                _parent.AddItem(option.DisplayName);
                _parent.SetItemTooltip(_parent.ItemCount - 1,
                    "Parent canonique · sa transformation est résolue avant celle de ce placeable");
                if (option.ProfileId == selectedParentId)
                {
                    selectedIndex = _parent.ItemCount - 1;
                }
            }
        }
        if (selectedParentId is not null && selectedIndex == 0)
        {
            _parentIds.Add(selectedParentId);
            _parent.AddItem("Parent existant indisponible dans ce module");
            _parent.SetItemDisabled(_parent.ItemCount - 1, true);
            selectedIndex = _parent.ItemCount - 1;
        }
        _parent.Select(selectedIndex);
    }

    private StudioPlaceableDraft CaptureDraft()
    {
        var source = _draft ?? throw new InvalidOperationException("Aucun brouillon de placeable.");
        var visualIndex = _visual.Selected;
        var visualId = visualIndex >= 0 && visualIndex < _visualIds.Count ? _visualIds[visualIndex] : string.Empty;
        var parentIndex = _parent.Selected;
        var parentId = parentIndex >= 0 && parentIndex < _parentIds.Count ? _parentIds[parentIndex] : null;
        return source with
        {
            Slug = _slug.Text,
            Name = _name.Text,
            Prompt = _prompt.Text,
            AdditionalLocalizedTexts = _localizationEditor.Capture(),
            TagsText = _tags.Text,
            AuthorNotes = _notes.Text,
            VisualAssetId = visualId,
            Appearance = _appearanceEditor.Capture(),
            CollisionEnabled = _collisionEnabled.ButtonPressed,
            NavigationObstacle = _navigationObstacle.ButtonPressed,
            CollisionShape = CollisionShapeValue(),
            CollisionSize = ReadVector(_collisionX, _collisionY, _collisionZ),
            Role = (StudioPlaceableRole)_role.Selected,
            Batched = _batched.ButtonPressed,
            Instanced = _instanced.ButtonPressed,
            Occluder = _occluder.ButtonPressed,
            MergeEligible = _mergeEligible.ButtonPressed,
            StaticCollisionEnabled = _staticCollision.ButtonPressed,
            InteractionDistance = _interactionDistance.Value,
            InteractionAngle = _interactionAngle.Value,
            RequiresFocus = _requiresFocus.ButtonPressed,
            InteractionDuration = _interactionDuration.Value,
            InteractionVerbs = _verbEditor.Capture(),
            InteractionAuthority = InteractionAuthorityValue(),
            InteractionConditionsText = _interactionConditions.Text,
            OnUsedEnabled = _onUsedEnabled.ButtonPressed,
            OnUsedForgeScriptApiVersion = _onUsedEventEditor.ApiVersion,
            OnUsedEventBinding = _onUsedEventEditor.EventBinding,
            BeforeUseScript = _onUsedEventEditor.BeforeScript,
            AfterUseScript = _onUsedEventEditor.AfterScript,
            RequireOnUsedRuntime = _onUsedEventEditor.RequiredAtRuntime,
            HasHealth = _health.ButtonPressed,
            HasContainer = _container.ButtonPressed,
            HasStateMachine = _stateMachineEditor.Enabled,
            StateMachine = _stateMachineEditor.Capture(source.StateMachine),
            Damageable = _damageableEditor.Capture(source.Damageable),
            HasProtection = _protection.ButtonPressed,
            Protection = _protectionEditor.Capture(source.Protection),
            HasFactionOwnership = _factionOwnership.ButtonPressed,
            FactionOwnership = _factionEditor.Capture(source.FactionOwnership),
            RequireFactionOwnershipRuntime = _factionEditor.RequiredAtRuntime,
            HasConversation = _conversation.ButtonPressed,
            Conversation = _conversationEditor.Capture(source.Conversation),
            RequireConversationRuntime = _conversationEditor.RequiredAtRuntime,
            HasCredentialPolicy = _credentialPolicy.ButtonPressed,
            Credential = _credentialEditor.Capture(source.Credential),
            RequireCredentialRuntime = _credentialEditor.RequiredAtRuntime,
            Lock = _lockEditor.Capture(source.Lock),
            Container = _containerEditor.Capture(source.Container),
            RequireOperationsRuntime = _stateMachineEditor.RequiredAtRuntime ||
                                       _containerEditor.RequiredAtRuntime ||
                                       _damageableEditor.RequiredAtRuntime ||
                                       _protectionEditor.RequiredAtRuntime ||
                                       _lockEditor.RequiredAtRuntime,
            HasLock = _lock.ButtonPressed,
            HasTrap = _trap.ButtonPressed,
            Trap = _trapEditor.Capture(source.Trap),
            RequireTrapRuntime = _trapEditor.RequiredAtRuntime,
            HasLoot = _loot.ButtonPressed,
            Loot = _lootEditor.Capture(source.Loot!),
            RequireLootRuntime = _lootEditor.RequiredAtRuntime,
            HasContainerEvents = _containerEvents.ButtonPressed,
            ContainerEvents = _containerEventsEditor.Capture(source.ContainerEvents!),
            RequireContainerEventsRuntime = _containerEventsEditor.RequiredAtRuntime,
            HasLockEvents = _lockEvents.ButtonPressed,
            LockEvents = _lockEventsEditor.Capture(source.LockEvents!),
            RequireLockEventsRuntime = _lockEventsEditor.RequiredAtRuntime,
            HasContentsChanged = _contentsChanged.ButtonPressed,
            ContentsChanged = _contentsChangedEditor.Capture(source.ContentsChanged!),
            RequireContentsChangedRuntime = _contentsChangedEditor.RequiredAtRuntime,
            HasDamageEvents = _damageEvents.ButtonPressed,
            DamageEvents = _damageEventsEditor.Capture(source.DamageEvents!),
            RequireDamageEventsRuntime = _damageEventsEditor.RequiredAtRuntime,
            HasPhysics = _physics.ButtonPressed,
            ScriptBindingsText = _scripts.Text,
            Position = ReadVector(_positionX, _positionY, _positionZ),
            RotationDegrees = ReadVector(_rotationX, _rotationY, _rotationZ),
            Scale = ReadVector(_scaleX, _scaleY, _scaleZ),
            Pivot = ReadVector(_pivotX, _pivotY, _pivotZ),
            SnapIncrement = _snap.Value,
            ParentId = parentId,
            AlignToSurface = _alignToSurface.ButtonPressed,
            TransformAnimationId = _transformAnimation.Text,
            ExplicitOverridesText = _overrides.Text,
            RequireRuntime = _requireRuntime.ButtonPressed,
        };
    }

    private void Preview()
    {
        if (_draft is null)
        {
            return;
        }
        _draft = CaptureDraft();
        _result = _session.PreviewPlaceable(_draft);
        StudioPlaceableFactionDecisionPreview? factionDecision = null;
        if (_factionOwnership.ButtonPressed)
        {
            factionDecision = _session.PreviewPlaceableFactionDecision(
                _draft,
                _factionEditor.PreviewActorFactionId,
                _factionEditor.PreviewAction);
            _factionEditor.SetDecisionPreview(factionDecision);
        }
        StudioPlaceableConversationPreview? conversationPreview = null;
        if (_conversation.ButtonPressed)
        {
            conversationPreview = _session.PreviewPlaceableConversation(
                _draft,
                _conversationEditor.PreviewBindingId,
                _conversationEditor.PreviewAlreadyUsed);
            _conversationEditor.SetConversationPreview(conversationPreview);
        }
        StudioPlaceableLockPreview? lockPreview = null;
        if (_lock.ButtonPressed)
        {
            lockPreview = _session.PreviewPlaceableLock(
                _draft,
                _lockEditor.PreviewMode,
                _lockEditor.PreviewSkillResult);
            _lockEditor.SetPreviewResult(lockPreview);
        }
        StudioPlaceableCredentialPreview? credentialPreview = null;
        if (_credentialPolicy.ButtonPressed)
        {
            credentialPreview = _session.PreviewPlaceableCredential(
                _draft,
                _credentialEditor.PreviewMode,
                _credentialEditor.PreviewAvailableCharges);
            _credentialEditor.SetPreviewResult(credentialPreview);
        }
        StudioPlaceableTrapPreview? trapPreview = null;
        if (_trap.ButtonPressed)
        {
            trapPreview = _session.PreviewPlaceableTrap(_draft, _trapEditor.PreviewMode);
            _trapEditor.SetPreviewResult(trapPreview);
        }
        StudioPlaceableLootPreview? lootPreview = null;
        if (_loot.ButtonPressed)
        {
            lootPreview = _session.PreviewPlaceableLoot(_draft, _lootEditor.PreviewMode);
            _lootEditor.SetLootPreview(lootPreview);
        }
        StudioPlaceableContainerEventPreview? containerEventPreview = null;
        if (_containerEvents.ButtonPressed)
        {
            containerEventPreview = _session.PreviewPlaceableContainerEvent(
                _draft,
                _containerEventsEditor.PreviewMode);
            _containerEventsEditor.SetPreviewResult(containerEventPreview);
        }
        StudioPlaceableLockEventPreview? lockEventPreview = null;
        if (_lockEvents.ButtonPressed)
        {
            lockEventPreview = _session.PreviewPlaceableLockEvent(
                _draft,
                _lockEventsEditor.PreviewMode);
            _lockEventsEditor.SetPreviewResult(lockEventPreview);
        }
        StudioPlaceableOnUsedEventPreview? onUsedEventPreview = null;
        if (_onUsedEnabled.ButtonPressed)
        {
            onUsedEventPreview = _session.PreviewPlaceableOnUsedEvent(_draft);
            _onUsedEventEditor.SetPreviewResult(onUsedEventPreview);
        }
        StudioPlaceableContentsChangedPreview? contentsChangedPreview = null;
        if (_contentsChanged.ButtonPressed)
        {
            contentsChangedPreview = _session.PreviewPlaceableContentsChanged(_draft);
            _contentsChangedEditor.SetPreviewResult(contentsChangedPreview);
        }
        StudioPlaceableDamageEventPreview? damageEventPreview = null;
        if (_damageEvents.ButtonPressed)
        {
            damageEventPreview = _session.PreviewPlaceableDamageEvent(_draft, _damageEventsEditor.PreviewMode);
            _damageEventsEditor.SetPreviewResult(damageEventPreview);
        }
        _previewSummary.Text = _lifecycleEventsFocus && _result.Succeeded
            ? "Cycle de vie prêt · OnUsed · Contenu modifié · Dégâts et mort · aperçu sans écriture · application annulable"
            : _result.Summary;
        _previewSummary.AddThemeColorOverride("font_color", _result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _previewState.Text = _result.Succeeded
            ? _lifecycleEventsFocus
                ? string.Join("\n", new[]
                {
                    onUsedEventPreview?.Summary,
                    contentsChangedPreview?.Summary,
                    damageEventPreview?.Summary,
                }.Where(value => !string.IsNullOrWhiteSpace(value)))
                : _lockEventsFocus
                ? lockEventPreview?.Summary ??
                  "Événements avant et après, contexte canonique et veto contrôlé validés · aperçu sans écriture"
                : _containerEventsFocus
                ? containerEventPreview?.Summary ??
                  "Ouverture, fermeture et contexte canonique validés · aperçu sans écriture"
                : _lootFocus
                ? lootPreview?.Message ??
                  "Table, moment, graine et état généré validés · aperçu sans écriture"
                : _trapFocus
                ? trapPreview?.Summary ??
                  "Détection, désamorçage, déclenchement et réarmement validés · aperçu sans écriture"
                : _credentialFocus
                ? credentialPreview?.Summary ??
                  "Politique de justificatif validée · aperçu sans écriture"
                : _lockFocus
                ? lockPreview?.Summary ??
                  "État, moyens d’ouverture et cycle du verrou validés · aperçu sans écriture"
                : _conversationFocus
                ? conversationPreview?.Summary ??
                  "Dialogue, point d’entrée, déclencheur et priorité validés · aperçu sans écriture"
                : _factionFocus
                ? factionDecision?.Summary ??
                  "Appartenance, hostilité, crime, accès et ciblage validés · aperçu sans écriture"
                : _plotProtectionFocus
                ? "Actions, droits, feedback et dérogation MJ validés · aperçu sans écriture"
                : _destructibilityFocus
                ? "Santé, impact, destruction et réparation validés · aperçu sans écriture"
                : _initialAnimationFocus
                    ? "État initial et transitions validés · aperçu sans écriture"
                    : $"{_result.RoleLabel} · {_result.VisualLabel} · pose et références valides"
            : "Aperçu refusé · corrigez le champ indiqué.";
        _previewState.AddThemeColorOverride("font_color", _result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        _apply.Disabled = !_result.CanApply;
        _selectInstances.Disabled = !_result.Succeeded || _result.InstanceIds.Count == 0;
        SetStatus(_result.Message, !_result.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedPlaceable();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        ProjectChanged?.Invoke();
        var selected = _draft?.BlueprintId;
        _catalog = _session.CreatePlaceableCatalog();
        if (selected is not null)
        {
            LoadBlueprint(selected);
        }
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ProjectChanged?.Invoke();
            var selected = _draft?.BlueprintId;
            _catalog = _session.CreatePlaceableCatalog();
            if (selected is not null)
            {
                LoadBlueprint(selected);
            }
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ProjectChanged?.Invoke();
            var selected = _draft?.BlueprintId;
            _catalog = _session.CreatePlaceableCatalog();
            if (selected is not null)
            {
                LoadBlueprint(selected);
            }
        }
    }

    private void SelectInstances()
    {
        if (_result?.InstanceIds.Count is not > 0)
        {
            return;
        }
        var ids = _result.InstanceIds;
        Close();
        InstancesRequested?.Invoke(ids, ids[0]);
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _session.CancelPlaceablePreview();
        _result = null;
        _apply.Disabled = true;
        _selectInstances.Disabled = true;
        _previewSummary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _previewState.Text = "Pose, pivot, grille et instances seront recalculés.";
        _previewState.AddThemeColorOverride("font_color", StudioStyle.Warning);
    }

    private void UpdateRoleFields()
    {
        var interaction = _role.Selected == (int)StudioPlaceableRole.Interactable;
        _staticFields.Visible = !interaction;
        _interactionFields.Visible = interaction;
        _onUsedEnabled.Disabled = !interaction;
        if (!interaction)
        {
            _onUsedEnabled.ButtonPressed = false;
        }
        UpdateOnUsedFields();
        InvalidatePreview();
    }

    private void UpdateOnUsedFields()
    {
        var enabled = _interactionFields.Visible && _onUsedEnabled.ButtonPressed;
        _onUsedEventEditor.SetEnabledState(enabled);
        InvalidatePreview();
    }

    private void UpdateContainerFields()
    {
        _containerEditor.Visible = _container.ButtonPressed;
        InvalidatePreview();
    }

    private void UpdateDamageFields()
    {
        _damageableEditor.Visible = _health.ButtonPressed;
        _damageableEditor.SetEnabledState(_health.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateProtectionFields()
    {
        _protectionEditor.Visible = _protection.ButtonPressed;
        _protectionEditor.SetEnabledState(_protection.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateFactionFields()
    {
        _factionEditor.Visible = _factionOwnership.ButtonPressed;
        _factionEditor.SetEnabledState(_factionOwnership.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateConversationFields()
    {
        _conversationEditor.Visible = _conversation.ButtonPressed;
        _conversationEditor.SetEnabledState(_conversation.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateLockFields()
    {
        _lockEditor.Visible = _lock.ButtonPressed;
        _lockEditor.SetEnabledState(_lock.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateCredentialFields()
    {
        _credentialEditor.Visible = _credentialPolicy.ButtonPressed;
        _credentialEditor.SetEnabledState(_credentialPolicy.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateTrapFields()
    {
        _trapEditor.Visible = _trap.ButtonPressed;
        _trapEditor.SetEnabledState(_trap.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateLootFields()
    {
        _lootEditor.Visible = _loot.ButtonPressed;
        _lootEditor.SetEnabledState(_loot.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateContainerEventsFields()
    {
        _containerEventsEditor.Visible = _containerEvents.ButtonPressed;
        _containerEventsEditor.SetEnabledState(_containerEvents.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateLockEventsFields()
    {
        _lockEventsEditor.Visible = _lockEvents.ButtonPressed;
        _lockEventsEditor.SetEnabledState(_lockEvents.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateContentsChangedFields()
    {
        _contentsChangedEditor.Visible = _contentsChanged.ButtonPressed;
        _contentsChangedEditor.SetEnabledState(_contentsChanged.ButtonPressed);
        InvalidatePreview();
    }

    private void UpdateDamageEventsFields()
    {
        var enabled = _damageEvents.ButtonPressed;
        _damageEventsEditor.Visible = enabled;
        _damageEventsEditor.SetEnabledState(enabled);
        InvalidatePreview();
    }

    private void SetPresentation(
        bool staticFocus,
        bool interactionFocus = false,
        bool containerFocus = false,
        bool identityFocus = false,
        bool transformFocus = false,
        bool initialAnimationFocus = false,
        bool destructibilityFocus = false,
        bool plotProtectionFocus = false,
        bool factionFocus = false,
        bool conversationFocus = false,
        bool lockFocus = false,
        bool credentialFocus = false,
        bool trapFocus = false,
        bool lootFocus = false,
        bool containerEventsFocus = false,
        bool lockEventsFocus = false)
    {
        _initialAnimationFocus = initialAnimationFocus;
        _destructibilityFocus = destructibilityFocus;
        _plotProtectionFocus = plotProtectionFocus;
        _factionFocus = factionFocus;
        _conversationFocus = conversationFocus;
        _lockFocus = lockFocus;
        _credentialFocus = credentialFocus;
        _trapFocus = trapFocus;
        _lootFocus = lootFocus;
        _containerEventsFocus = containerEventsFocus;
        _lockEventsFocus = lockEventsFocus;
        _lifecycleEventsFocus = false;
        _scenePreviewTitle.Text = lockEventsFocus
            ? "Aperçu du changement de verrou"
            : containerEventsFocus
            ? "Aperçu d’ouverture ou de fermeture"
            : lootFocus
            ? "Aperçu du trésor initial"
            : trapFocus
            ? "Aperçu du piège"
            : credentialFocus
            ? "Aperçu du justificatif"
            : lockFocus
            ? "Aperçu de tentative sur le verrou"
            : conversationFocus
            ? "Aperçu de résolution de conversation"
            : factionFocus
            ? "Aperçu de relation et d’autorisation"
            : plotProtectionFocus
            ? "Aperçu d’autorisation et d’audit"
            : destructibilityFocus
            ? "Aperçu des dégâts et de la réparation"
            : initialAnimationFocus
                ? "Aperçu des états et transitions"
                : "Gizmo de pivot et pas de grille";
        _scenePreviewDescription.Text = lockEventsFocus
            ? "Les événements avant et après, la méthode, le justificatif, l’acteur et le veto sont produits par le même adaptateur que le runtime."
            : containerEventsFocus
            ? "La liaison ForgeScript et le contexte acteur, résultat, état précédent et autorité sont produits par le même adaptateur que le runtime."
            : lootFocus
            ? "Moment, graine, table pondérée et restauration de l’état généré sont évalués par le même LootSpawner que le runtime."
            : trapFocus
            ? "Détection, désamorçage, déclenchement, réarmement et politique alliée sont évalués par le même TrapComponent que le runtime."
            : credentialFocus
            ? "Conservation, consommation, charges, partage et permission virtuelle sont évalués par le même profil canonique que le runtime."
            : lockFocus
            ? "Clé, compétence, code, piratage, permissions, reverrouillage et brouillage sont résolus par le même composant canonique que le runtime."
            : conversationFocus
            ? "Dialogue, point d’entrée, déclencheur, priorité et usage unique sont résolus par le même profil canonique que le runtime."
            : factionFocus
            ? "Appartenances, relations, hostilité, crime, accès et ciblage sont évalués par le même profil canonique que le runtime."
            : plotProtectionFocus
            ? "Actions protégées, droits, feedback et dérogation MJ sont évalués par la même politique canonique que le runtime."
            : destructibilityFocus
            ? "Santé, armure, résistances, étapes visuelles, débris et réparation sont projetés par le même profil canonique que le runtime."
            : initialAnimationFocus
                ? "L’état chargé, les apparences et la durée de transition sont validés par le même profil canonique que le runtime."
                : "La sélection 3D utilise les instances réelles du blueprint. La position affichée est celle résolue par le runtime canonique.";
        _eyebrow.Text = lockEventsFocus
            ? "VERROUILLAGE ET DÉVERROUILLAGE"
            : containerEventsFocus
            ? "OUVERTURE ET FERMETURE"
            : lootFocus
            ? "TRÉSOR INITIAL"
            : trapFocus
            ? "PIÈGE"
            : credentialFocus
            ? "CLÉ CONSOMMÉE"
            : lockFocus
            ? "VERROUILLAGE"
            : conversationFocus
            ? "CONVERSATION"
            : factionFocus
            ? "FACTION ET PROPRIÉTÉ"
            : plotProtectionFocus
            ? "PROTECTION PLOT"
            : destructibilityFocus
            ? "DESTRUCTIBILITÉ"
            : initialAnimationFocus
            ? "ANIMATION INITIALE"
            : transformFocus
            ? "TRANSFORMATION PRÉCISE"
            : identityFocus
            ? "IDENTITÉ & APPARENCE"
            : staticFocus
            ? "DÉCOR STATIQUE"
            : interactionFocus
                ? "OBJET UTILISABLE"
                : containerFocus
                    ? "CONTENEUR"
                    : "BLUEPRINT DE DÉCOR";
        _heading.Text = lockEventsFocus
            ? "Configurer et prévisualiser les événements du verrou"
            : containerEventsFocus
            ? "Configurer et prévisualiser les événements du conteneur"
            : lootFocus
            ? "Composer et prévisualiser le trésor initial du placeable"
            : trapFocus
            ? "Configurer et prévisualiser le piège du placeable"
            : credentialFocus
            ? "Décider ce que devient la clé après utilisation"
            : lockFocus
            ? "Configurer l’accès et le cycle de vie du verrou"
            : conversationFocus
            ? "Relier le placeable aux dialogues et choisir quand il parle"
            : factionFocus
            ? "Relier le placeable aux factions et projeter leurs effets"
            : plotProtectionFocus
            ? "Protéger les actions narratives et auditer les dérogations"
            : destructibilityFocus
            ? "Configurer les dégâts, la destruction et la réparation"
            : initialAnimationFocus
                ? "Choisir l’état visuel chargé et ses transitions"
            : transformFocus
            ? "Placer et hiérarchiser un placeable avec précision"
            : identityFocus
            ? "Nommer, localiser et habiller un placeable"
            : staticFocus
            ? "Optimiser un décor non utilisable"
            : interactionFocus
                ? "Composer les actions proposées au joueur"
                : containerFocus
                    ? "Configurer l’inventaire d’un placeable"
                    : "Composer un placeable sans quitter la scène";
        _subtitle.Text = lockEventsFocus
            ? "Version ForgeScript · avant · après · méthode · justificatif · acteur · veto contrôlé · aperçu"
            : containerEventsFocus
            ? "Version ForgeScript · ouverture · fermeture · acteur · résultat · état précédent · autorité · aperçu"
            : lootFocus
            ? "Objets · catégories · poids · quantités · qualité · moment · graine · sauvegarde · aperçu"
            : trapFocus
            ? "Déclencheur · effet · détection · désamorçage · réarmement · propriétaire · alliés · VFX · aperçu"
            : credentialFocus
            ? "Conserver · consommer · charges · partage · permission virtuelle · audit · aperçu"
            : lockFocus
            ? "État · clé · justificatif · compétence · difficulté · code · piratage · droits · reverrouillage · brouillage"
            : conversationFocus
            ? "Dialogue · point d’entrée · interaction · état · utilisateur · objet · événement · priorité · aperçu"
            : factionFocus
            ? "Propriétaire · appartenances · hostilité · crime · accès · ciblage · aperçu"
            : plotProtectionFocus
            ? "Actions · droits · feedback joueur · override MJ · motif · audit · aperçu"
            : destructibilityFocus
            ? "Santé · armure · résistances · étapes visuelles · effet · débris · réparation · aperçu"
            : initialAnimationFocus
                ? "Au repos · ouvert · fermé · brisé · actif · personnalisé · transitions · aperçu"
            : transformFocus
            ? "Coordonnées · rotation · échelle · pivot · grille · parent · surface · animation · gizmo 3D"
            : identityFocus
            ? "Nom court · langues · tags · notes · meshes · matériaux · icône · portrait · VFX"
            : staticFocus
            ? "Regroupement · instanciation · occlusion · fusion · collision optionnelle · aperçu avant application"
            : interactionFocus
                ? "Actions · portée · angle · focus · durée · autorité · conditions · OnUsed · aperçu avant application"
                : containerFocus
                    ? "Capacité · masse · propriétaire · accès · sauvegarde · butin · interface · multijoueur"
                    : "Visuel · collision · rôle · composants · scripts · transformation · aperçu avant application";
    }

    private void UpdateCollisionFields()
    {
        var enabled = _collisionEnabled.ButtonPressed;
        _navigationObstacle.Disabled = !enabled;
        if (!enabled)
        {
            _navigationObstacle.ButtonPressed = false;
        }
        _collisionShape.Disabled = !enabled;
        _collisionX.Editable = enabled;
        _collisionY.Editable = enabled;
        _collisionZ.Editable = enabled;
        InvalidatePreview();
    }

    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled;
        _undo.Disabled = !enabled || !_session.CanUndo;
        _redo.Disabled = !enabled || !_session.CanRedo;
        _apply.Disabled = true;
        _selectInstances.Disabled = true;
    }

    private void WireEvents()
    {
        _blueprint.ItemSelected += index =>
        {
            if (!_updating && index >= 0 && index < _blueprintIds.Count)
            {
                LoadBlueprint(_blueprintIds[(int)index]);
            }
        };
        _visual.ItemSelected += _ => InvalidatePreview();
        _parent.ItemSelected += _ => InvalidatePreview();
        foreach (var line in new[]
                 {
                     _slug, _name, _prompt, _tags, _notes, _scripts, _overrides, _interactionConditions,
                     _transformAnimation,
                 })
        {
            line.TextChanged += _ => InvalidatePreview();
        }
        _role.ItemSelected += _ => UpdateRoleFields();
        _interactionAuthority.ItemSelected += _ => InvalidatePreview();
        _collisionShape.ItemSelected += _ => InvalidatePreview();
        _collisionEnabled.Toggled += _ => UpdateCollisionFields();
        foreach (var check in new[]
                 {
                     _navigationObstacle, _batched, _instanced, _occluder, _mergeEligible, _staticCollision,
                     _requiresFocus, _onUsedEnabled, _health, _protection, _factionOwnership,
                     _conversation,
                     _container,
                       _lock, _credentialPolicy, _trap, _loot, _containerEvents, _lockEvents,
                       _contentsChanged, _damageEvents, _physics,
                      _alignToSurface, _requireRuntime,
                 })
        {
            check.Toggled += _ => InvalidatePreview();
        }
        _onUsedEnabled.Toggled += _ => UpdateOnUsedFields();
        _container.Toggled += _ => UpdateContainerFields();
        _health.Toggled += _ => UpdateDamageFields();
        _protection.Toggled += _ => UpdateProtectionFields();
        _factionOwnership.Toggled += _ => UpdateFactionFields();
        _conversation.Toggled += _ => UpdateConversationFields();
        _lock.Toggled += _ => UpdateLockFields();
        _credentialPolicy.Toggled += _ => UpdateCredentialFields();
        _trap.Toggled += _ => UpdateTrapFields();
        _loot.Toggled += _ => UpdateLootFields();
        _containerEvents.Toggled += _ => UpdateContainerEventsFields();
        _lockEvents.Toggled += _ => UpdateLockEventsFields();
        _contentsChanged.Toggled += _ => UpdateContentsChangedFields();
        _damageEvents.Toggled += _ => UpdateDamageEventsFields();
        _onUsedEventEditor.Changed += InvalidatePreview;
        _onUsedEventEditor.PreviewRequested += Preview;
        _verbEditor.Changed += InvalidatePreview;
        _containerEditor.Changed += InvalidatePreview;
        _stateMachineEditor.Changed += InvalidatePreview;
        _damageableEditor.Changed += InvalidatePreview;
        _protectionEditor.Changed += InvalidatePreview;
        _factionEditor.Changed += InvalidatePreview;
        _factionEditor.DecisionPreviewRequested += Preview;
        _factionEditor.RuntimeRequirementChanged += _ => InvalidatePreview();
        _conversationEditor.Changed += InvalidatePreview;
        _conversationEditor.PreviewRequested += Preview;
        _conversationEditor.RuntimeRequirementChanged += _ => InvalidatePreview();
        _lockEditor.Changed += InvalidatePreview;
        _lockEditor.PreviewRequested += Preview;
        _credentialEditor.Changed += InvalidatePreview;
        _credentialEditor.PreviewRequested += Preview;
        _credentialEditor.RuntimeRequirementChanged += _ => InvalidatePreview();
        _trapEditor.Changed += InvalidatePreview;
        _trapEditor.PreviewRequested += Preview;
        _trapEditor.RuntimeRequirementChanged += _ => InvalidatePreview();
        _lootEditor.Changed += InvalidatePreview;
        _lootEditor.PreviewRequested += Preview;
        _lootEditor.RuntimeRequirementChanged += _ => InvalidatePreview();
        _containerEventsEditor.Changed += InvalidatePreview;
        _containerEventsEditor.PreviewRequested += Preview;
        _containerEventsEditor.RuntimeRequirementChanged += _ => InvalidatePreview();
        _lockEventsEditor.Changed += InvalidatePreview;
        _lockEventsEditor.PreviewRequested += Preview;
        _lockEventsEditor.RuntimeRequirementChanged += _ => InvalidatePreview();
        _contentsChangedEditor.Changed += InvalidatePreview;
        _contentsChangedEditor.PreviewRequested += Preview;
        _damageEventsEditor.Changed += InvalidatePreview;
        _damageEventsEditor.PreviewRequested += Preview;
        _lockEditor.RuntimeRequirementChanged += required =>
        {
            _containerEditor.SetRequiredAtRuntime(required);
            _stateMachineEditor.SetRequiredAtRuntime(required);
            _damageableEditor.SetRequiredAtRuntime(required);
            _protectionEditor.SetRequiredAtRuntime(required);
        };
        _containerEditor.RuntimeRequirementChanged += required =>
        {
            _stateMachineEditor.SetRequiredAtRuntime(required);
            _damageableEditor.SetRequiredAtRuntime(required);
            _protectionEditor.SetRequiredAtRuntime(required);
            _lockEditor.SetRequiredAtRuntime(required);
        };
        _stateMachineEditor.RuntimeRequirementChanged += required =>
        {
            _containerEditor.SetRequiredAtRuntime(required);
            _damageableEditor.SetRequiredAtRuntime(required);
            _protectionEditor.SetRequiredAtRuntime(required);
            _lockEditor.SetRequiredAtRuntime(required);
        };
        _damageableEditor.RuntimeRequirementChanged += required =>
        {
            _containerEditor.SetRequiredAtRuntime(required);
            _stateMachineEditor.SetRequiredAtRuntime(required);
            _protectionEditor.SetRequiredAtRuntime(required);
            _lockEditor.SetRequiredAtRuntime(required);
        };
        _protectionEditor.RuntimeRequirementChanged += required =>
        {
            _containerEditor.SetRequiredAtRuntime(required);
            _stateMachineEditor.SetRequiredAtRuntime(required);
            _damageableEditor.SetRequiredAtRuntime(required);
            _lockEditor.SetRequiredAtRuntime(required);
        };
        _localizationEditor.Changed += InvalidatePreview;
        _appearanceEditor.Changed += InvalidatePreview;
        foreach (var spin in new[]
                 {
                     _collisionX, _collisionY, _collisionZ, _interactionDistance, _interactionAngle,
                     _interactionDuration, _positionX, _positionY, _positionZ, _rotationX, _rotationY, _rotationZ,
                     _scaleX, _scaleY, _scaleZ, _pivotX, _pivotY, _pivotZ, _snap,
                 })
        {
            spin.ValueChanged += _ => InvalidatePreview();
        }
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _selectInstances.Pressed += SelectInstances;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.75f : Size.X >= 2300 ? 1.25f : 1f;
        var width = Math.Clamp((Size.X - 54) / scale, 1020, 1260);
        var height = Math.Clamp((Size.Y - 68) / scale, 660, 820);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2((Size.X - renderedWidth) * .5f, Math.Max(22, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static LineEdit AddLine(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    private static OptionButton AddOption(VBoxContainer parent, string label, IReadOnlyList<string> options)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton();
        StudioStyle.ApplyOptionButton(option);
        foreach (var value in options)
        {
            option.AddItem(value);
        }
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

    private static SpinBox AddSpin(VBoxContainer parent, string label, double minimum, double maximum, double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = NewSpin(minimum, maximum, step);
        parent.AddChild(spin);
        return spin;
    }

    private static (SpinBox X, SpinBox Y, SpinBox Z) AddVector(
        VBoxContainer parent,
        string label,
        double minimum,
        double maximum,
        double step)
    {
        parent.AddChild(StudioStyle.Label(label + " · X / Y / Z", 10, StudioStyle.Muted));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        parent.AddChild(row);
        var x = NewSpin(minimum, maximum, step);
        var y = NewSpin(minimum, maximum, step);
        var z = NewSpin(minimum, maximum, step);
        foreach (var spin in new[] { x, y, z })
        {
            spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(spin);
        }
        return (x, y, z);
    }

    private static SpinBox NewSpin(double minimum, double maximum, double step)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static void SetVector(SpinBox x, SpinBox y, SpinBox z, Vector3Value value)
    {
        x.Value = value.X;
        y.Value = value.Y;
        z.Value = value.Z;
    }

    private static Vector3Value ReadVector(SpinBox x, SpinBox y, SpinBox z) =>
        new((float)x.Value, (float)y.Value, (float)z.Value);

    private void SelectCollisionShape(string value)
    {
        var index = value.ToLowerInvariant() switch
        {
            "capsule" => 1,
            "cylinder" => 2,
            "sphere" => 3,
            _ => 0,
        };
        _collisionShape.Select(index);
    }

    private void SelectInteractionAuthority(string value)
    {
        var index = value.ToLowerInvariant() switch
        {
            "owner" => 1,
            "client" => 2,
            _ => 0,
        };
        _interactionAuthority.Select(index);
    }

    private string InteractionAuthorityValue() => _interactionAuthority.Selected switch
    {
        1 => "owner",
        2 => "client",
        _ => "server",
    };

    private string CollisionShapeValue() => _collisionShape.Selected switch
    {
        1 => "capsule",
        2 => "cylinder",
        3 => "sphere",
        _ => "box",
    };
}
