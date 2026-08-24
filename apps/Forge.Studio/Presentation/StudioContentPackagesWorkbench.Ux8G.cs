using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Content;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioContentPackagesWorkbenchUx8G : ColorRect
{
    private static readonly ContentConflictPolicy[] ConflictPolicies = Enum.GetValues<ContentConflictPolicy>();
    private static readonly ContentDependencyKind[] DependencyKinds = Enum.GetValues<ContentDependencyKind>();

    private readonly StudioSessionController _session;
    private StudioUx8GPackageDraft? _sourceDraft;
    private StudioUx8GPackagePreview? _preview;
    private readonly List<ContentPackageDefinition> _packages = [];
    private int _packageIndex = -1;
    private int _dependencyIndex = -1;
    private bool _updating;
    private bool _explicitSelection;
    private bool _navigationReady;

    private PanelContainer _dialog = null!;
    private LineEdit _catalogProvenance = null!;
    private OptionButton _conflictPolicy = null!;
    private OptionButton _packageChoice = null!;
    private LineEdit _packageNamespace = null!;
    private LineEdit _packageVersion = null!;
    private LineEdit _packageLicense = null!;
    private LineEdit _packagePriority = null!;
    private LineEdit _packageProvenance = null!;
    private LineEdit _sealedResources = null!;
    private Label _packageNeighbors = null!;
    private Button _packageAdd = null!;
    private Button _packageRemove = null!;
    private Button _packageUpdate = null!;
    private OptionButton _dependencyChoice = null!;
    private LineEdit _dependencyNamespace = null!;
    private LineEdit _dependencyConstraint = null!;
    private OptionButton _dependencyKind = null!;
    private LineEdit _dependencyProvenance = null!;
    private Button _dependencyAdd = null!;
    private Button _dependencyRemove = null!;
    private Button _dependencyUpdate = null!;
    private Label _priorityPlan = null!;
    private TextEdit _previewText = null!;
    private TextEdit _diagnostics = null!;
    private CheckBox _approval = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Label _status = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _packageIndex >= 0;
    internal bool PreviewReady => _preview is { Succeeded: true, CompilationReady: true };
    internal bool NavigationReady => _navigationReady;
    internal bool EvidenceResourcesReleased => true;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8G(this));
            return Visible &&
                   text.Contains("Contenu personnalisé ordonné", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Package de contenu versionné", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Ordre de priorité", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Dépendances", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Compilation de package", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune installation", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8G(text);
        }
    }

    internal StudioContentPackagesWorkbenchUx8G(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8G();
    }

    internal void Open()
    {
        _session.CancelPackageApprovalUx8G();
        _sourceDraft = _session.CreatePackageDraftUx8G();
        _preview = null;
        _packages.Clear();
        if (_sourceDraft is not null) _packages.AddRange(_sourceDraft.Packages);
        _packageIndex = -1;
        _dependencyIndex = -1;
        _explicitSelection = false;
        _navigationReady = false;
        Visible = true;
        PopulateCatalogUx8G();
        UpdateBoundsUx8G();
        SetStatusUx8G(_sourceDraft is null
            ? "Ouvrez un workspace pour gérer ses packages."
            : _packages.Count == 0
                ? "Catalogue réel vide · créez explicitement un package pour commencer."
                : "Sélectionnez explicitement un package ; aucun package n’est chargé automatiquement.",
            _sourceDraft is null);
    }

    internal void Close()
    {
        if (!Visible && _sourceDraft is null) return;
        _session.CancelPackageApprovalUx8G();
        Visible = false;
        ResetUx8G();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if (!Visible) Open();
        var source = _session.CreatePackageDraftUx8G();
        if (source is null) return false;
        var fixture = StudioSessionController.CreatePackageFixtureUx8G(source);
        _sourceDraft = source;
        _packages.Clear();
        _packages.AddRange(fixture.Packages);
        _catalogProvenance.Text = fixture.CatalogProvenance;
        _conflictPolicy.Selected = Array.IndexOf(ConflictPolicies, fixture.ConflictPolicy);
        RefreshPackageChoicesUx8G();
        var addonIndex = _packages.FindIndex(item => item.PackageNamespace == "studio.ux8g.addon");
        if (addonIndex < 0) return false;
        _packageChoice.Select(addonIndex + 1);
        SelectPackageUx8G(addonIndex + 1);
        _approval.ButtonPressed = true;
        PreviewUx8G();
        return HasExplicitSelection && PreviewReady && _preview is { CanApply: true } && ShowsHumanSurface;
    }

    internal bool RunUiEvidence()
    {
        if (!PreviewReady && !PrepareEvidence("ux8-g-content-packages")) return false;
        _navigationReady = true;
        for (var index = 0; index < _packages.Count; index++)
        {
            _packageChoice.Select(index + 1);
            SelectPackageUx8G(index + 1);
            _navigationReady &= _packageNamespace.Text.Length > 0 &&
                                _packageNeighbors.Text.Contains("source", StringComparison.OrdinalIgnoreCase);
        }
        var addonIndex = _packages.FindIndex(item => item.PackageNamespace == "studio.ux8g.addon");
        _packageChoice.Select(addonIndex + 1);
        SelectPackageUx8G(addonIndex + 1);
        if (_packages[addonIndex].Dependencies.Count > 0)
        {
            _dependencyChoice.Select(1);
            SelectDependencyUx8G(1);
            _navigationReady &= _dependencyNamespace.Text.Contains("studio.ux8g.core", StringComparison.Ordinal);
        }
        _approval.ButtonPressed = true;
        PreviewUx8G();
        return _navigationReady && PreviewReady && _preview is { CanApply: true } &&
               _previewText.Text.Contains("packages.lock", StringComparison.Ordinal) &&
               _previewText.Text.Contains("studio.ux8g.addon", StringComparison.Ordinal) &&
               ShowsHumanSurface;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        Open();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        Close();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx8G();
    }

    private void BuildUiUx8G()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.96f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux8GContentPackagesWorkbench", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("PACKAGES & DÉPENDANCES · UX8-G", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Catalogue versionné, priorité et compilation en mémoire", 18,
            StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "ORF-PROJ-020 · ORF-CONT-001/002/003/007 · commande canonique et préservation UX3-H",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8G("Fermer", Close, 82));
        root.AddChild(header);

        var catalogRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        catalogRow.AddThemeConstantOverride("separation", 8);
        catalogRow.AddChild(FieldLabelUx8G("Provenance du catalogue", 165));
        _catalogProvenance = FieldUx8G("project-author");
        _catalogProvenance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        catalogRow.AddChild(_catalogProvenance);
        catalogRow.AddChild(FieldLabelUx8G("Conflits", 72));
        _conflictPolicy = new OptionButton { CustomMinimumSize = new Vector2(210, 34) };
        StudioStyle.ApplyOptionButton(_conflictPolicy);
        foreach (var policy in ConflictPolicies) _conflictPolicy.AddItem(HumanConflictUx8G(policy));
        _conflictPolicy.ItemSelected += _ => InvalidateUx8G();
        catalogRow.AddChild(_conflictPolicy);
        root.AddChild(catalogRow);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 680),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [610] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildPackageColumnUx8G(body);
        BuildPreviewColumnUx8G(body);

        var footer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 5);
        _approval = new CheckBox { Text = "J’approuve ce catalogue pour la révision et le brouillon affichés." };
        _approval.Toggled += _ => InvalidateUx8G(cancelApproval: true, preserveApprovalCheck: true);
        footer.AddChild(_approval);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        _previewButton = ActionButtonUx8G("Aperçu sec", PreviewUx8G, 125, primary: true);
        _applyButton = ActionButtonUx8G("Appliquer", ApplyUx8G, 108);
        _undoButton = ActionButtonUx8G("Annuler", UndoUx8G, 96);
        _redoButton = ActionButtonUx8G("Rétablir", RedoUx8G, 96);
        _saveButton = ActionButtonUx8G("Enregistrer", SaveUx8G, 112);
        actions.AddChild(_previewButton);
        actions.AddChild(_applyButton);
        actions.AddChild(_undoButton);
        actions.AddChild(_redoButton);
        actions.AddChild(_saveButton);
        _status = StudioStyle.Label("Ouvrez un workspace pour commencer.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        footer.AddChild(actions);
        footer.AddChild(StudioStyle.Label(
            "Limite honnête · compilation et lock restent en mémoire : aucune installation, signature, publication ou exécution de script.",
            9, StudioStyle.Warning, wrap: true));
        root.AddChild(footer);

        ConnectInvalidationUx8G();
        UpdateActionsUx8G();
    }

    private void BuildPackageColumnUx8G(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8GPackageEditor", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(560, 0);
        body.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(column);

        column.AddChild(RouteCardUx8G("ORF-PROJ-020", "Contenu personnalisé ordonné",
            "Les priorités et conflits décident de la ressource canonique sans déplacer les fichiers."));
        column.AddChild(RouteCardUx8G("ORF-CONT-001", "Package de contenu versionné",
            "Namespace, SemVer, SPDX, provenance, hash canonique et ressources scellées."));
        column.AddChild(RouteCardUx8G("ORF-CONT-002", "Ordre de priorité",
            "La résolution HighestPriority reste déterministe et visible avant application."));

        column.AddChild(StudioStyle.Label("PACKAGE SÉLECTIONNÉ", 10, StudioStyle.Accent));
        _packageChoice = ChoiceUx8G(column, "Package", SelectPackageUx8G);
        var packageActions = new HBoxContainer();
        packageActions.AddThemeConstantOverride("separation", 6);
        _packageAdd = ActionButtonUx8G("Nouveau", AddPackageUx8G, 88);
        _packageRemove = ActionButtonUx8G("Retirer", RemovePackageUx8G, 88);
        _packageUpdate = ActionButtonUx8G("Mettre à jour", SavePackageUx8G, 120);
        packageActions.AddChild(_packageAdd);
        packageActions.AddChild(_packageRemove);
        packageActions.AddChild(_packageUpdate);
        column.AddChild(packageActions);
        _packageNamespace = LabeledFieldUx8G(column, "Namespace", "project.package");
        _packageVersion = LabeledFieldUx8G(column, "Version sémantique", "1.0.0");
        _packageLicense = LabeledFieldUx8G(column, "Licence SPDX", "CC0-1.0");
        _packagePriority = LabeledFieldUx8G(column, "Priorité", "0");
        _packageProvenance = LabeledFieldUx8G(column, "Provenance", "project-author");
        _sealedResources = LabeledFieldUx8G(column, "Ressources scellées (virgules)", string.Empty);
        _packageNeighbors = StudioStyle.Label(
            "Les sources et assets restent gérés par UX3-H et ne sont jamais réécrits ici.",
            9, StudioStyle.Muted, wrap: true);
        column.AddChild(_packageNeighbors);

        column.AddChild(StudioStyle.Label("DÉPENDANCES", 10, StudioStyle.Accent));
        column.AddChild(RouteCardUx8G("ORF-CONT-003", "Dépendances",
            "Required, Optional ou Peer avec contrainte SemVer et diagnostic de cycle."));
        _dependencyChoice = ChoiceUx8G(column, "Dépendance", SelectDependencyUx8G);
        var dependencyActions = new HBoxContainer();
        dependencyActions.AddThemeConstantOverride("separation", 6);
        _dependencyAdd = ActionButtonUx8G("Ajouter", AddDependencyUx8G, 88);
        _dependencyRemove = ActionButtonUx8G("Retirer", RemoveDependencyUx8G, 88);
        _dependencyUpdate = ActionButtonUx8G("Mettre à jour", SaveDependencyUx8G, 120);
        dependencyActions.AddChild(_dependencyAdd);
        dependencyActions.AddChild(_dependencyRemove);
        dependencyActions.AddChild(_dependencyUpdate);
        column.AddChild(dependencyActions);
        _dependencyNamespace = LabeledFieldUx8G(column, "Package cible", "project.core");
        _dependencyConstraint = LabeledFieldUx8G(column, "Contrainte de version", "^1.0.0");
        _dependencyKind = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_dependencyKind);
        foreach (var kind in DependencyKinds) _dependencyKind.AddItem(StudioSessionController.HumanDependencyKindUx8G(kind));
        AddLabeledControlUx8G(column, "Type", _dependencyKind);
        _dependencyProvenance = LabeledFieldUx8G(column, "Provenance", "project-author");
    }

    private void BuildPreviewColumnUx8G(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8GPackagePreview", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(650, 0);
        body.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);
        column.AddChild(RouteCardUx8G("ORF-CONT-007", "Compilation de package",
            "Validation, résolution, lock et manifeste sont produits en mémoire sans package disque."));
        _priorityPlan = StudioStyle.Label("Aucun ordre prévisualisé.", 10, StudioStyle.Muted, wrap: true);
        column.AddChild(_priorityPlan);
        column.AddChild(StudioStyle.Label("APERÇU SEC · RÉSOLUTION ET ARTEFACTS", 10, StudioStyle.Accent));
        _previewText = ReadOnlyTextUx8G("Prévisualisation requise.", 245);
        column.AddChild(_previewText);
        column.AddChild(StudioStyle.Label("DIAGNOSTICS CODE + CHEMIN", 10, StudioStyle.Warning));
        _diagnostics = ReadOnlyTextUx8G("Aucun diagnostic avant l’aperçu.", 115);
        column.AddChild(_diagnostics);
    }

    private void PopulateCatalogUx8G()
    {
        _updating = true;
        _catalogProvenance.Text = _sourceDraft?.CatalogProvenance ?? string.Empty;
        _conflictPolicy.Selected = _sourceDraft is null
            ? 0
            : Math.Max(0, Array.IndexOf(ConflictPolicies, _sourceDraft.ConflictPolicy));
        _approval.ButtonPressed = false;
        _updating = false;
        RefreshPackageChoicesUx8G();
        ClearPackageUx8G();
        ClearDependencyUx8G();
        PopulatePreviewUx8G();
        UpdateActionsUx8G();
    }

    private void RefreshPackageChoicesUx8G(Guid? keepId = null)
    {
        _updating = true;
        _packageChoice.Clear();
        _packageChoice.AddItem("Choisir explicitement un package…");
        foreach (var package in _packages)
            _packageChoice.AddItem($"{HumanTextUx8G(package.PackageNamespace)} · {package.Version} · priorité {package.Priority}");
        _updating = false;
        if (keepId is not null)
        {
            var index = _packages.FindIndex(item => item.Id == keepId.Value);
            if (index >= 0)
            {
                _packageChoice.Select(index + 1);
                SelectPackageUx8G(index + 1);
            }
        }
    }

    private void SelectPackageUx8G(int choiceIndex)
    {
        if (_updating) return;
        _session.CancelPackageApprovalUx8G();
        _preview = null;
        _approval.ButtonPressed = false;
        _packageIndex = choiceIndex - 1;
        _dependencyIndex = -1;
        if (_packageIndex < 0 || _packageIndex >= _packages.Count)
        {
            _packageIndex = -1;
            ClearPackageUx8G();
            ClearDependencyUx8G();
            _explicitSelection = false;
            SetStatusUx8G("Sélectionnez explicitement un package.");
            UpdateActionsUx8G();
            return;
        }
        _explicitSelection = true;
        var package = _packages[_packageIndex];
        _updating = true;
        _packageNamespace.Text = package.PackageNamespace;
        _packageVersion.Text = package.Version;
        _packageLicense.Text = package.LicenseSpdx;
        _packagePriority.Text = package.Priority.ToString(CultureInfo.InvariantCulture);
        _packageProvenance.Text = package.Provenance;
        _sealedResources.Text = string.Join(", ", package.SealedResources);
        _packageNeighbors.Text = $"Voisins protégés · {package.Sources.Count} source(s), {package.Assets.Count} asset(s), " +
                                 $"schéma {package.SchemaVersion}, hash recalculé au brouillon.";
        _updating = false;
        RefreshDependencyChoicesUx8G();
        ClearDependencyUx8G();
        SetStatusUx8G("Package chargé dans le brouillon ; sources et assets restent inchangés.");
        UpdateActionsUx8G();
    }

    private void AddPackageUx8G()
    {
        if (_sourceDraft is null) return;
        var suffix = 1;
        string packageNamespace;
        do packageNamespace = $"project.package-{suffix++}";
        while (_packages.Any(item => item.PackageNamespace == packageNamespace));
        var package = StudioSessionController.RehashPackageUx8G(new ContentPackageDefinition(
            Guid.NewGuid(), ContentPackageDefinition.CurrentSchemaVersion, "project-author", packageNamespace,
            "1.0.0", "CC0-1.0", new string('0', 64), 0, [], [], [], []));
        _packages.Add(package);
        _packages.Sort((left, right) => StringComparer.Ordinal.Compare(left.PackageNamespace, right.PackageNamespace));
        RefreshPackageChoicesUx8G(package.Id);
        InvalidateUx8G();
        SetStatusUx8G("Nouveau package ajouté explicitement au brouillon.");
    }

    private void RemovePackageUx8G()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count) return;
        var package = _packages[_packageIndex];
        if (_packages.Any(item => item.Id != package.Id && item.Dependencies.Any(dependency =>
                dependency.PackageNamespace == package.PackageNamespace)))
        {
            SetStatusUx8G("Retrait refusé · un autre package dépend encore de celui-ci.", true);
            return;
        }
        _packages.RemoveAt(_packageIndex);
        _packageIndex = -1;
        _dependencyIndex = -1;
        RefreshPackageChoicesUx8G();
        ClearPackageUx8G();
        ClearDependencyUx8G();
        InvalidateUx8G();
        SetStatusUx8G("Package retiré du brouillon ; prévisualisez avant toute application.");
    }

    private void SavePackageUx8G()
    {
        try
        {
            SavePackageCoreUx8G();
            var packageId = _packages[_packageIndex].Id;
            RefreshPackageChoicesUx8G(packageId);
            InvalidateUx8G();
            SetStatusUx8G("Package mis à jour dans le brouillon ; hash canonique recalculé.");
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            SetStatusUx8G(HumanTextUx8G(exception.Message), true);
        }
    }

    private void SavePackageCoreUx8G()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count) return;
        if (!int.TryParse(_packagePriority.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority))
            throw new FormatException("La priorité doit être un entier.");
        var existing = _packages[_packageIndex];
        var updated = StudioSessionController.RehashPackageUx8G(new ContentPackageDefinition(
            existing.Id, existing.SchemaVersion, _packageProvenance.Text, _packageNamespace.Text,
            _packageVersion.Text, _packageLicense.Text, existing.ContentSha256, priority,
            SplitUx8G(_sealedResources.Text), existing.Dependencies, existing.Sources, existing.Assets));
        if (_packages.Where(item => item.Id != existing.Id)
            .Any(item => item.PackageNamespace == updated.PackageNamespace))
            throw new ArgumentException("Ce namespace est déjà utilisé par un autre package.");
        _packages[_packageIndex] = updated;
    }

    private void RefreshDependencyChoicesUx8G(Guid? keepId = null)
    {
        _updating = true;
        _dependencyChoice.Clear();
        _dependencyChoice.AddItem("Choisir explicitement une dépendance…");
        if (_packageIndex >= 0 && _packageIndex < _packages.Count)
        {
            foreach (var dependency in _packages[_packageIndex].Dependencies)
                _dependencyChoice.AddItem($"{HumanTextUx8G(dependency.PackageNamespace)} · " +
                                          $"{dependency.VersionConstraint} · " +
                                          StudioSessionController.HumanDependencyKindUx8G(dependency.Kind));
        }
        _updating = false;
        if (keepId is not null && _packageIndex >= 0 && _packageIndex < _packages.Count)
        {
            var index = _packages[_packageIndex].Dependencies.ToList().FindIndex(item => item.Id == keepId.Value);
            if (index >= 0)
            {
                _dependencyChoice.Select(index + 1);
                SelectDependencyUx8G(index + 1);
            }
        }
    }

    private void SelectDependencyUx8G(int choiceIndex)
    {
        if (_updating || _packageIndex < 0 || _packageIndex >= _packages.Count) return;
        _session.CancelPackageApprovalUx8G();
        _preview = null;
        _approval.ButtonPressed = false;
        var dependencies = _packages[_packageIndex].Dependencies;
        _dependencyIndex = choiceIndex - 1;
        if (_dependencyIndex < 0 || _dependencyIndex >= dependencies.Count)
        {
            _dependencyIndex = -1;
            ClearDependencyUx8G();
            UpdateActionsUx8G();
            return;
        }
        var dependency = dependencies[_dependencyIndex];
        _updating = true;
        _dependencyNamespace.Text = dependency.PackageNamespace;
        _dependencyConstraint.Text = dependency.VersionConstraint;
        _dependencyKind.Selected = Math.Max(0, Array.IndexOf(DependencyKinds, dependency.Kind));
        _dependencyProvenance.Text = dependency.Provenance;
        _updating = false;
        SetStatusUx8G("Dépendance chargée ; namespace et contrainte restent humains, aucun UUID n’est affiché.");
        UpdateActionsUx8G();
    }

    private void AddDependencyUx8G()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count) return;
        var package = _packages[_packageIndex];
        var target = _packages.FirstOrDefault(item => item.Id != package.Id)?.PackageNamespace ?? "project.required";
        var dependency = new ContentPackageDependency(
            Guid.NewGuid(), ContentPackageDefinition.CurrentSchemaVersion, "project-author", target,
            "^1.0.0", ContentDependencyKind.Required);
        var dependencies = package.Dependencies.Append(dependency).ToArray();
        _packages[_packageIndex] = StudioSessionController.RehashPackageUx8G(new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources,
            dependencies, package.Sources, package.Assets));
        RefreshDependencyChoicesUx8G(dependency.Id);
        InvalidateUx8G();
        SetStatusUx8G("Dépendance ajoutée au brouillon ; corrigez la cible si nécessaire.");
    }

    private void RemoveDependencyUx8G()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count || _dependencyIndex < 0) return;
        var package = _packages[_packageIndex];
        if (_dependencyIndex >= package.Dependencies.Count) return;
        var dependencies = package.Dependencies.Where((_, index) => index != _dependencyIndex).ToArray();
        _packages[_packageIndex] = StudioSessionController.RehashPackageUx8G(new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources,
            dependencies, package.Sources, package.Assets));
        _dependencyIndex = -1;
        RefreshDependencyChoicesUx8G();
        ClearDependencyUx8G();
        InvalidateUx8G();
        SetStatusUx8G("Dépendance retirée du brouillon.");
    }

    private void SaveDependencyUx8G()
    {
        try
        {
            SaveDependencyCoreUx8G();
            var dependencyId = _packages[_packageIndex].Dependencies[_dependencyIndex].Id;
            RefreshDependencyChoicesUx8G(dependencyId);
            InvalidateUx8G();
            SetStatusUx8G("Dépendance mise à jour dans le brouillon.");
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            SetStatusUx8G(HumanTextUx8G(exception.Message), true);
        }
    }

    private void SaveDependencyCoreUx8G()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count || _dependencyIndex < 0) return;
        var package = _packages[_packageIndex];
        if (_dependencyIndex >= package.Dependencies.Count) return;
        var existing = package.Dependencies[_dependencyIndex];
        var kindIndex = Math.Clamp(_dependencyKind.Selected, 0, DependencyKinds.Length - 1);
        var dependency = new ContentPackageDependency(
            existing.Id, existing.SchemaVersion, _dependencyProvenance.Text,
            _dependencyNamespace.Text.Trim().ToLowerInvariant(), _dependencyConstraint.Text,
            DependencyKinds[kindIndex]);
        var dependencies = package.Dependencies.Select((item, index) =>
            index == _dependencyIndex ? dependency : item).ToArray();
        _packages[_packageIndex] = StudioSessionController.RehashPackageUx8G(new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources,
            dependencies, package.Sources, package.Assets));
    }

    private bool TryReadDraftUx8G(out StudioUx8GPackageDraft draft, out string failure)
    {
        draft = null!;
        failure = string.Empty;
        if (_sourceDraft is null)
        {
            failure = "Aucun workspace n’est chargé.";
            return false;
        }
        try
        {
            SaveDependencyCoreUx8G();
            SavePackageCoreUx8G();
            var conflictIndex = Math.Clamp(_conflictPolicy.Selected, 0, ConflictPolicies.Length - 1);
            draft = _sourceDraft with
            {
                CatalogProvenance = _catalogProvenance.Text,
                ConflictPolicy = ConflictPolicies[conflictIndex],
                Packages = _packages.ToArray(),
                Approved = _approval.ButtonPressed,
            };
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            failure = HumanTextUx8G(exception.Message);
            return false;
        }
    }

    private void PreviewUx8G()
    {
        if (!TryReadDraftUx8G(out var draft, out var failure))
        {
            SetStatusUx8G(failure, true);
            return;
        }
        _preview = _session.PreviewPackagesUx8G(draft);
        PopulatePreviewUx8G();
        SetStatusUx8G(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx8G();
    }

    private void ApplyUx8G()
    {
        if (!TryReadDraftUx8G(out var draft, out var failure))
        {
            SetStatusUx8G(failure, true);
            return;
        }
        var result = _session.ApplyApprovedPackagesUx8G(draft);
        SetStatusUx8G(result.Message, !result.Succeeded);
        if (result.Succeeded) Open();
        else UpdateActionsUx8G();
    }

    private void UndoUx8G()
    {
        var result = _session.UndoPackagesUx8G();
        SetStatusUx8G(result.Message, !result.Succeeded);
        if (result.Succeeded) Open();
    }

    private void RedoUx8G()
    {
        var result = _session.RedoPackagesUx8G();
        SetStatusUx8G(result.Message, !result.Succeeded);
        if (result.Succeeded) Open();
    }

    private void SaveUx8G()
    {
        var result = _session.SavePackagesUx8G();
        SetStatusUx8G(result.Message, !result.Succeeded);
        UpdateActionsUx8G();
    }

    private void PopulatePreviewUx8G()
    {
        if (_preview is null)
        {
            _priorityPlan.Text = "Aucun ordre prévisualisé.";
            _previewText.Text = "Prévisualisation requise.";
            _diagnostics.Text = "Aucun diagnostic avant l’aperçu.";
            return;
        }
        _priorityPlan.Text = _preview.Packages.Count == 0
            ? "Catalogue vide · aucun ordre de priorité."
            : "Ordre résolu · " + string.Join(" › ", _preview.Packages.Select(item =>
                $"{item.PackageLabel} ({item.Priority})"));
        var lines = new List<string>
        {
            $"Dry-run canonique · {_preview.CanonicalDryRun}",
            $"Compilation en mémoire · {_preview.CompilationReady}",
            $"Patch · {_preview.Patch}",
            $"Inverse · {_preview.Inverse}",
            string.Empty,
            "DÉPENDANCES",
        };
        lines.AddRange(_preview.DependencyPlan.Select(item => "• " + item));
        lines.Add(string.Empty);
        lines.Add("LOCK");
        lines.AddRange(_preview.LockPlan.Select(item => "• " + item));
        lines.Add(string.Empty);
        lines.Add("RESSOURCES RÉSOLUES");
        lines.AddRange(_preview.ResourcePlan.Select(item => "• " + item));
        lines.Add(string.Empty);
        lines.Add("ARTEFACTS EN MÉMOIRE");
        lines.AddRange(_preview.ArtifactPlan.Select(item => "• " + item));
        _previewText.Text = string.Join('\n', lines);
        _diagnostics.Text = _preview.Diagnostics.Count == 0
            ? "Aucun diagnostic bloquant."
            : string.Join('\n', _preview.Diagnostics.Select(item =>
                $"{item.Code} · {HumanTextUx8G(item.Path)}\n{HumanTextUx8G(item.Message)}"));
    }

    private void InvalidateUx8G(bool cancelApproval = true, bool preserveApprovalCheck = false)
    {
        if (_updating) return;
        if (cancelApproval) _session.CancelPackageApprovalUx8G();
        if (!preserveApprovalCheck)
        {
            _updating = true;
            _approval.ButtonPressed = false;
            _updating = false;
        }
        _preview = null;
        PopulatePreviewUx8G();
        UpdateActionsUx8G();
    }

    private void ConnectInvalidationUx8G()
    {
        foreach (var field in new[]
                 {
                     _catalogProvenance, _packageNamespace, _packageVersion, _packageLicense,
                     _packagePriority, _packageProvenance, _sealedResources, _dependencyNamespace,
                     _dependencyConstraint, _dependencyProvenance,
                 })
            field.TextChanged += _ => InvalidateUx8G();
        _dependencyKind.ItemSelected += _ => InvalidateUx8G();
    }

    private void UpdateActionsUx8G()
    {
        var loaded = _sourceDraft is not null;
        var package = _packageIndex >= 0 && _packageIndex < _packages.Count;
        var dependency = package && _dependencyIndex >= 0 &&
                         _dependencyIndex < _packages[_packageIndex].Dependencies.Count;
        _packageAdd.Disabled = !loaded;
        _packageRemove.Disabled = !package;
        _packageUpdate.Disabled = !package;
        _dependencyAdd.Disabled = !package;
        _dependencyRemove.Disabled = !dependency;
        _dependencyUpdate.Disabled = !dependency;
        _previewButton.Disabled = !loaded;
        _applyButton.Disabled = _preview is not { CanApply: true };
        _undoButton.Disabled = !_session.CanUndo;
        _redoButton.Disabled = !_session.CanRedo;
        _saveButton.Disabled = !_session.IsDirty;
    }

    private void ResetUx8G()
    {
        _sourceDraft = null;
        _preview = null;
        _packages.Clear();
        _packageIndex = _dependencyIndex = -1;
        _explicitSelection = false;
        _navigationReady = false;
        ClearPackageUx8G();
        ClearDependencyUx8G();
    }

    private void ClearPackageUx8G()
    {
        _updating = true;
        _packageNamespace.Text = string.Empty;
        _packageVersion.Text = string.Empty;
        _packageLicense.Text = string.Empty;
        _packagePriority.Text = string.Empty;
        _packageProvenance.Text = string.Empty;
        _sealedResources.Text = string.Empty;
        _packageNeighbors.Text = "Les sources et assets restent gérés par UX3-H et ne sont jamais réécrits ici.";
        _updating = false;
    }

    private void ClearDependencyUx8G()
    {
        _updating = true;
        _dependencyNamespace.Text = string.Empty;
        _dependencyConstraint.Text = string.Empty;
        _dependencyKind.Selected = 0;
        _dependencyProvenance.Text = string.Empty;
        _updating = false;
        RefreshDependencyChoicesUx8G();
    }

    private void SetStatusUx8G(string message, bool error = false)
    {
        var human = HumanTextUx8G(message);
        _status.Text = human;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(human, error);
    }

    private void UpdateBoundsUx8G()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 54, 1280, 1840);
        var height = Math.Clamp(viewport.Y - 40, 820, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static OptionButton ChoiceUx8G(VBoxContainer parent, string label, Action<int> onSelected)
    {
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(choice);
        choice.ItemSelected += index => onSelected((int)index);
        AddLabeledControlUx8G(parent, label, choice);
        return choice;
    }

    private static LineEdit LabeledFieldUx8G(VBoxContainer parent, string label, string placeholder)
    {
        var field = FieldUx8G(placeholder);
        AddLabeledControlUx8G(parent, label, field);
        return field;
    }

    private static void AddLabeledControlUx8G(VBoxContainer parent, string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(FieldLabelUx8G(label, 190));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(control);
        parent.AddChild(row);
    }

    private static Label FieldLabelUx8G(string text, float width)
    {
        var label = StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

    private static LineEdit FieldUx8G(string placeholder)
    {
        var field = new LineEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, 34) };
        StudioStyle.ApplyLineEdit(field);
        return field;
    }

    private static Button ActionButtonUx8G(string text, Action pressed, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += pressed;
        return button;
    }

    private static TextEdit ReadOnlyTextUx8G(string text, float height) => new()
    {
        Text = text,
        Editable = false,
        WrapMode = TextEdit.LineWrappingMode.Boundary,
        CustomMinimumSize = new Vector2(0, height),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill,
    };

    private static Control RouteCardUx8G(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux8G" + route.Replace("-", string.Empty, StringComparison.Ordinal),
            StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(route + " · " + title, 10, StudioStyle.Text, wrap: true));
        column.AddChild(StudioStyle.Label(detail, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static string HumanConflictUx8G(ContentConflictPolicy policy) => policy switch
    {
        ContentConflictPolicy.Error => "Refuser tout conflit",
        ContentConflictPolicy.HighestPriority => "Priorité la plus haute",
        ContentConflictPolicy.NamespaceOrder => "Ordre des namespaces",
        _ => "Valeur inconnue protégée",
    };

    private static IReadOnlyList<string> SplitUx8G(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string HumanTextUx8G(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return Regex.Replace(text,
            "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "identifiant protégé", RegexOptions.CultureInvariant);
    }

    private static IEnumerable<string> DescendantTextUx8G(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Label label when !string.IsNullOrWhiteSpace(label.Text): yield return label.Text; break;
                case Button button when !string.IsNullOrWhiteSpace(button.Text): yield return button.Text; break;
                case LineEdit line when !string.IsNullOrWhiteSpace(line.Text): yield return line.Text; break;
                case TextEdit text when !string.IsNullOrWhiteSpace(text.Text): yield return text.Text; break;
            }
            foreach (var text in DescendantTextUx8G(child)) yield return text;
        }
    }

    private static bool ContainsUuidUx8G(string text) => Regex.IsMatch(text,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.CultureInvariant);
}
