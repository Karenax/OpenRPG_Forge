using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioContentDataWorkbenchUx3H : ColorRect
{
    private static readonly ContentBatchCollisionPolicy[] CollisionPolicies = Enum.GetValues<ContentBatchCollisionPolicy>();
    private static readonly ContentMergePolicy[] MergePolicies = Enum.GetValues<ContentMergePolicy>();
    private static readonly ContentConflictPolicy[] ConflictPolicies = Enum.GetValues<ContentConflictPolicy>();
    private static readonly ContentSourceKind[] SourceKinds = Enum.GetValues<ContentSourceKind>();
    private static readonly DataColumnKind[] ColumnKinds = Enum.GetValues<DataColumnKind>();

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3H? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealEvidence;
    private bool _usesFallbackEvidence;
    private bool _updating;
    private bool _suppressInvalidation;
    private bool _explicitSelections;

    private readonly Label _context;
    private readonly TabContainer _tabs;

    private readonly LineEdit _glob;
    private readonly LineEdit _extensions;
    private readonly OptionButton _collision;
    private readonly LineEdit _batchProvenance;
    private readonly LineEdit _batchLicense;
    private readonly LineEdit _batchSource;
    private readonly LineEdit _batchDestination;
    private readonly Button _batchPreviewButton;
    private readonly Button _batchApplyButton;
    private readonly Button _batchUndoButton;
    private readonly Button _batchRedoButton;
    private readonly Label _batchSummary;
    private readonly VBoxContainer _batchEntries;

    private readonly ItemList _blueprintList;
    private readonly LineEdit _exportOutput;
    private readonly CheckBox _exportPreviews;
    private readonly Button _exportPreviewButton;
    private readonly Button _exportApplyButton;
    private readonly Button _exportUndoButton;
    private readonly Button _exportRedoButton;
    private readonly Label _exportSummary;
    private readonly VBoxContainer _exportEntries;

    private readonly LineEdit _catalogProvenance;
    private readonly OptionButton _conflictPolicy;
    private readonly OptionButton _packageList;
    private readonly Button _packageAdd;
    private readonly Button _packageRemove;
    private readonly Button _packageSave;
    private readonly LineEdit _packageNamespace;
    private readonly LineEdit _packageVersion;
    private readonly LineEdit _packageProvenance;
    private readonly LineEdit _packageLicense;
    private readonly LineEdit _packagePriority;
    private readonly OptionButton _sourceList;
    private readonly Button _sourceAdd;
    private readonly Button _sourceRemove;
    private readonly Button _sourceSave;
    private readonly LineEdit _sourceLogical;
    private readonly LineEdit _sourcePath;
    private readonly OptionButton _sourceKind;
    private readonly CheckBox _sourceRequired;
    private readonly LineEdit _sourceLicense;
    private readonly LineEdit _sourceBudget;
    private readonly LineEdit _sourceProvenance;
    private readonly LineEdit _sourceAliases;
    private readonly LineEdit _sourceReferences;
    private readonly LineEdit _sourceHash;
    private readonly Button _sourceHashButton;

    private readonly LineEdit _workspaceProvenance;
    private readonly OptionButton _mergePolicy;
    private readonly OptionButton _tableList;
    private readonly Button _tableAdd;
    private readonly Button _tableRemove;
    private readonly Button _tableSave;
    private readonly LineEdit _tableSlug;
    private readonly LineEdit _tableName;
    private readonly LineEdit _tableProvenance;
    private readonly LineEdit _tableLicense;
    private readonly OptionButton _tableOverride;
    private readonly OptionButton _columnList;
    private readonly Button _columnAdd;
    private readonly Button _columnRemove;
    private readonly Button _columnSave;
    private readonly LineEdit _columnKey;
    private readonly LineEdit _columnName;
    private readonly OptionButton _columnKind;
    private readonly CheckBox _columnRequired;
    private readonly OptionButton _columnReference;
    private readonly OptionButton _rowList;
    private readonly Button _rowAdd;
    private readonly Button _rowRemove;
    private readonly Button _rowSave;
    private readonly LineEdit _rowProvenance;
    private readonly OptionButton _cellColumn;
    private readonly LineEdit _cellValue;

    private readonly CheckBox _approval;
    private readonly Button _contentPreviewButton;
    private readonly Button _contentApplyButton;
    private readonly Label _contentSummary;
    private readonly VBoxContainer _semanticDiff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _sourcePlan;
    private readonly VBoxContainer _tablePlan;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private readonly Label _status;

    private StudioUx3HContentDraft? _draft;
    private StudioUx3HContentPreview? _contentPreview;
    private StudioUx3HBatchPreview? _batchPreview;
    private StudioUx3HExportPreview? _exportPreview;
    private List<ContentPackageDefinition> _packages = [];
    private List<DataTableDefinition> _tables = [];
    private IReadOnlyList<StudioUx3HBlueprintChoice> _blueprints = [];
    private int _packageIndex = -1;
    private int _sourceIndex = -1;
    private int _tableIndex = -1;
    private int _columnIndex = -1;
    private int _rowIndex = -1;
    private int _cellColumnIndex = -1;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesFallbackEvidence;
    internal bool HasExplicitSelections => _explicitSelections;
    internal bool ContentPreviewReady => _contentPreview is
    {
        Succeeded: true,
        CanApply: true,
        DryRunSucceeded: true,
        SourcesResolved: true,
        TablesValid: true,
        RuntimeClaimsBounded: true,
    };
    internal bool BatchPreviewReady => _batchPreview is { Succeeded: true, CanApply: true };
    internal bool ExportPreviewReady => _exportPreview is { Succeeded: true, CanApply: true };
    internal bool EvidenceSourceUntouched => SourceMatchesUx3H();
    internal bool EvidenceResourcesReleased => _evidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var labels = FindChildren("*", "Label", true, false).Cast<Label>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item));
            var buttons = FindChildren("*", "Button", true, false).Cast<Button>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item));
            var tabs = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var all = labels.Concat(buttons).Concat(tabs).ToArray();
            return all.Any(item => item.Contains("Import de masse", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Export de blueprints", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Sources textuelles", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Tables typées", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase)) &&
                   all.Any(item => item.Contains("ERF propriétaire", StringComparison.OrdinalIgnoreCase)) &&
                   all.All(item => !Regex.IsMatch(item,
                       "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                       RegexOptions.CultureInvariant));
        }
    }

    internal StudioContentDataWorkbenchUx3H(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Visible = false;
        Color = StudioStyle.Background;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 194;

        var margin = StudioStyle.Margin(22, 18, 22, 18);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("UX3-H · Données, import et export", 20, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Transferts bornés · blueprints déterministes · sources texte gouvernées · tables UUID typées.",
            10, StudioStyle.Muted, wrap: true));
        _context = StudioStyle.Label(
            "PROJET COURANT · état vide conservé ; aucune donnée riche n’est synthétisée dans le produit.",
            10, StudioStyle.Accent, wrap: true);
        titles.AddChild(_context);
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var panel = StudioStyle.Panel("Ux3HContent", StudioStyle.Surface, 9, 1);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var panelMargin = StudioStyle.Margin(12, 10, 12, 10);
        panel.AddChild(panelMargin);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panelMargin.AddChild(_tabs);
        root.AddChild(panel);

        var importTab = ScrollTabUx3H("Import de masse", out var importBody);
        importBody.AddChild(StudioStyle.Label(
            "IMPORT GOUVERNÉ · le format ERF propriétaire n’est pas décodé ; les fichiers ouverts sont filtrés et copiés tels quels.",
            10, StudioStyle.Warning, wrap: true));
        _glob = FieldUx3H(importBody, "Filtre glob relatif", "*");
        _extensions = FieldUx3H(importBody, "Extensions autorisées (virgules)", ".json,.txt");
        _collision = EnumChoiceUx3H(importBody, "Collision", CollisionPolicies, StudioSessionController.HumanCollisionUx3H);
        _batchProvenance = FieldUx3H(importBody, "Provenance", "project-author");
        _batchLicense = FieldUx3H(importBody, "Licence SPDX", "UNLICENSED");
        _batchSource = FieldUx3H(importBody, "Dossier source", "Choisissez un dossier local");
        _batchDestination = FieldUx3H(importBody, "Destination relative au workspace", "content/imported");
        var batchActions = new HBoxContainer();
        _batchPreviewButton = StudioStyle.Button("Prévisualiser l’import", compact: true);
        _batchPreviewButton.Pressed += PreviewBatchUx3H;
        batchActions.AddChild(_batchPreviewButton);
        _batchApplyButton = StudioStyle.Button("Importer", compact: true);
        _batchApplyButton.Pressed += () => ApplyBatchUx3H();
        batchActions.AddChild(_batchApplyButton);
        _batchUndoButton = StudioStyle.Button("Annuler import", compact: true);
        _batchUndoButton.Pressed += () => UndoBatchUx3H();
        batchActions.AddChild(_batchUndoButton);
        _batchRedoButton = StudioStyle.Button("Rétablir import", compact: true);
        _batchRedoButton.Pressed += () => RedoBatchUx3H();
        batchActions.AddChild(_batchRedoButton);
        importBody.AddChild(batchActions);
        _batchSummary = StudioStyle.Label("Aucun import prévisualisé.", 9, StudioStyle.Muted, wrap: true);
        importBody.AddChild(_batchSummary);
        _batchEntries = new VBoxContainer();
        importBody.AddChild(_batchEntries);
        _tabs.AddChild(importTab);

        var exportTab = ScrollTabUx3H("Export de blueprints", out var exportBody);
        exportBody.AddChild(StudioStyle.Label(
            "Sélectionnez un ou plusieurs blueprints. Les dépendances transitives, licences, crédits, migrations et aperçus sont fermés automatiquement.",
            10, StudioStyle.Text, wrap: true));
        _blueprintList = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Multi,
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _blueprintList.MultiSelected += (_, _) => { _explicitSelections = true; InvalidateExportUx3H(); };
        exportBody.AddChild(_blueprintList);
        _exportOutput = FieldUx3H(exportBody, "Pack relatif de sortie", "exports/selection.orpblueprints");
        _exportOutput.Text = "exports/selection.orpblueprints";
        _exportPreviews = CheckUx3H(exportBody, "Inclure les aperçus textuels gouvernés");
        _exportPreviews.ButtonPressed = true;
        var exportActions = new HBoxContainer();
        _exportPreviewButton = StudioStyle.Button("Prévisualiser le pack", compact: true);
        _exportPreviewButton.Pressed += PreviewExportUx3H;
        exportActions.AddChild(_exportPreviewButton);
        _exportApplyButton = StudioStyle.Button("Exporter", compact: true);
        _exportApplyButton.Pressed += () => ApplyExportUx3H();
        exportActions.AddChild(_exportApplyButton);
        _exportUndoButton = StudioStyle.Button("Annuler export", compact: true);
        _exportUndoButton.Pressed += () => UndoExportUx3H();
        exportActions.AddChild(_exportUndoButton);
        _exportRedoButton = StudioStyle.Button("Rétablir export", compact: true);
        _exportRedoButton.Pressed += () => RedoExportUx3H();
        exportActions.AddChild(_exportRedoButton);
        exportBody.AddChild(exportActions);
        _exportSummary = StudioStyle.Label("Aucun export prévisualisé.", 9, StudioStyle.Muted, wrap: true);
        exportBody.AddChild(_exportSummary);
        _exportEntries = new VBoxContainer();
        exportBody.AddChild(_exportEntries);
        _tabs.AddChild(exportTab);

        var sourcesTab = ScrollTabUx3H("Sources textuelles", out var sourcesBody);
        sourcesBody.AddChild(StudioStyle.Label(
            "Une ressource logique par fichier JSON, texte, YAML ou ForgeScript ; le fichier réel, son hash et son budget sont vérifiés.",
            10, StudioStyle.Text, wrap: true));
        _catalogProvenance = FieldUx3H(sourcesBody, "Provenance du catalogue", "project-author");
        _conflictPolicy = EnumChoiceUx3H(sourcesBody, "Résolution des conflits", ConflictPolicies, HumanConflictUx3H);
        var packageHeading = HeadingUx3H(sourcesBody, "PACKAGES", out _packageAdd, out _packageRemove, out _packageSave,
            AddPackageUx3H, RemovePackageUx3H, SavePackageUx3H);
        sourcesBody.AddChild(packageHeading);
        _packageList = ChoiceUx3H(sourcesBody, "Package sélectionné", SelectPackageUx3H);
        _packageNamespace = FieldUx3H(sourcesBody, "Namespace", "project.content");
        _packageVersion = FieldUx3H(sourcesBody, "Version", "1.0.0");
        _packageProvenance = FieldUx3H(sourcesBody, "Provenance", "project-author");
        _packageLicense = FieldUx3H(sourcesBody, "Licence SPDX", "CC0-1.0");
        _packagePriority = FieldUx3H(sourcesBody, "Priorité", "0");
        var sourceHeading = HeadingUx3H(sourcesBody, "SOURCES", out _sourceAdd, out _sourceRemove, out _sourceSave,
            AddSourceUx3H, RemoveSourceUx3H, SaveSourceUx3H);
        sourcesBody.AddChild(sourceHeading);
        _sourceList = ChoiceUx3H(sourcesBody, "Source sélectionnée", SelectSourceUx3H);
        _sourceLogical = FieldUx3H(sourcesBody, "Ressource logique", "rules/example");
        _sourcePath = FieldUx3H(sourcesBody, "Fichier relatif", "content/example.json");
        _sourceKind = EnumChoiceUx3H(sourcesBody, "Format", SourceKinds, StudioSessionController.HumanSourceKindUx3H);
        _sourceRequired = CheckUx3H(sourcesBody, "Source requise au build");
        _sourceLicense = FieldUx3H(sourcesBody, "Licence SPDX", "CC0-1.0");
        _sourceBudget = FieldUx3H(sourcesBody, "Budget plateforme (octets)", "1048576");
        _sourceProvenance = FieldUx3H(sourcesBody, "Provenance", "project-author");
        _sourceAliases = FieldUx3H(sourcesBody, "Alias humains (virgules)", string.Empty);
        _sourceReferences = FieldUx3H(sourcesBody, "Ressources référencées (virgules)", string.Empty);
        _sourceHash = FieldUx3H(sourcesBody, "SHA-256 du fichier réel", new string('0', 64));
        _sourceHash.Editable = false;
        _sourceHashButton = StudioStyle.Button("Lire le fichier et recalculer", compact: true);
        _sourceHashButton.Pressed += ReadSourceHashUx3H;
        sourcesBody.AddChild(_sourceHashButton);
        _tabs.AddChild(sourcesTab);

        var tablesTab = ScrollTabUx3H("Tables typées", out var tablesBody);
        tablesBody.AddChild(StudioStyle.Label(
            "Édition cellule par cellule avec colonnes Texte, Entier, Décimal, Booléen ou Référence ; aucune table positionnelle brute.",
            10, StudioStyle.Text, wrap: true));
        _workspaceProvenance = FieldUx3H(tablesBody, "Provenance de l’espace contenu", "project-author");
        _mergePolicy = EnumChoiceUx3H(tablesBody, "Politique de fusion", MergePolicies, StudioSessionController.HumanMergeUx3H);
        tablesBody.AddChild(HeadingUx3H(tablesBody, "TABLES", out _tableAdd, out _tableRemove, out _tableSave,
            AddTableUx3H, RemoveTableUx3H, SaveTableUx3H));
        _tableList = ChoiceUx3H(tablesBody, "Table sélectionnée", SelectTableUx3H);
        _tableSlug = FieldUx3H(tablesBody, "Slug", "table-projet");
        _tableName = FieldUx3H(tablesBody, "Nom", "Table projet");
        _tableProvenance = FieldUx3H(tablesBody, "Provenance", "project-author");
        _tableLicense = FieldUx3H(tablesBody, "Licence SPDX", "CC0-1.0");
        _tableOverride = ChoiceUx3H(tablesBody, "Table de base remplacée (optionnelle)", null);
        tablesBody.AddChild(HeadingUx3H(tablesBody, "COLONNES", out _columnAdd, out _columnRemove, out _columnSave,
            AddColumnUx3H, RemoveColumnUx3H, SaveColumnUx3H));
        _columnList = ChoiceUx3H(tablesBody, "Colonne sélectionnée", SelectColumnUx3H);
        _columnKey = FieldUx3H(tablesBody, "Clé", "value");
        _columnName = FieldUx3H(tablesBody, "Libellé", "Valeur");
        _columnKind = EnumChoiceUx3H(tablesBody, "Type", ColumnKinds, StudioSessionController.HumanColumnKindUx3H);
        _columnRequired = CheckUx3H(tablesBody, "Valeur requise");
        _columnReference = ChoiceUx3H(tablesBody, "Table référencée", null);
        tablesBody.AddChild(HeadingUx3H(tablesBody, "LIGNES ET CELLULES", out _rowAdd, out _rowRemove, out _rowSave,
            AddRowUx3H, RemoveRowUx3H, SaveRowUx3H));
        _rowList = ChoiceUx3H(tablesBody, "Ligne sélectionnée", SelectRowUx3H);
        _rowProvenance = FieldUx3H(tablesBody, "Provenance de la ligne", "project-author");
        _cellColumn = ChoiceUx3H(tablesBody, "Colonne de la cellule", SelectCellColumnUx3H);
        _cellValue = FieldUx3H(tablesBody, "Valeur", string.Empty);
        _tabs.AddChild(tablesTab);

        var reviewTab = ScrollTabUx3H("Aperçu sec", out var reviewBody);
        reviewBody.AddChild(StudioStyle.Label(
            "Le dry-run commande valide configuration, packages, fichiers réels et tables sans écrire ni compiler un package sur disque.",
            10, StudioStyle.Muted, wrap: true));
        _approval = CheckUx3H(reviewBody, "J’approuve cette configuration de contenu.");
        var contentActions = new HBoxContainer();
        _contentPreviewButton = StudioStyle.Button("Prévisualiser", compact: true);
        _contentPreviewButton.Pressed += PreviewContentUx3H;
        contentActions.AddChild(_contentPreviewButton);
        _contentApplyButton = StudioStyle.Button("Appliquer", compact: true);
        _contentApplyButton.Pressed += () => ApplyContentUx3H();
        contentActions.AddChild(_contentApplyButton);
        reviewBody.AddChild(contentActions);
        _contentSummary = StudioStyle.Label("Prévisualisation requise.", 9, StudioStyle.Muted, wrap: true);
        reviewBody.AddChild(_contentSummary);
        reviewBody.AddChild(StudioStyle.Label("DIFF SÉMANTIQUE", 10, StudioStyle.Accent));
        _semanticDiff = new VBoxContainer();
        reviewBody.AddChild(_semanticDiff);
        reviewBody.AddChild(StudioStyle.Label("DIAGNOSTICS CODE + CHEMIN", 10, StudioStyle.Warning));
        _diagnostics = new VBoxContainer();
        reviewBody.AddChild(_diagnostics);
        reviewBody.AddChild(StudioStyle.Label("SOURCES RÉSOLUES", 10, StudioStyle.Accent));
        _sourcePlan = new VBoxContainer();
        reviewBody.AddChild(_sourcePlan);
        reviewBody.AddChild(StudioStyle.Label("TABLES", 10, StudioStyle.Accent));
        _tablePlan = new VBoxContainer();
        reviewBody.AddChild(_tablePlan);
        _tabs.AddChild(reviewTab);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        var footerText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerText.AddChild(StudioStyle.Label(
            "Trois frontières : commande projet, transfert de fichiers réversible, export déterministe réversible.",
            9, StudioStyle.Muted, wrap: true));
        _status = StudioStyle.Label("Ouvrez un module pour commencer.", 9, StudioStyle.Muted, wrap: true);
        footerText.AddChild(_status);
        footer.AddChild(footerText);
        _undo = StudioStyle.Button("Annuler config", compact: true);
        _undo.Pressed += () => UndoContentUx3H();
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir config", compact: true);
        _redo.Pressed += () => RedoContentUx3H();
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += () => SaveContentUx3H();
        footer.AddChild(_save);
        root.AddChild(footer);

        ConnectInvalidationUx3H();
        UpdateActionsUx3H();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidenceUx3H();
        _session = _sourceSession;
        _usesRealEvidence = false;
        _usesFallbackEvidence = false;
        _evidenceReleased = true;
        Visible = true;
        LoadUx3H();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && ReferenceEquals(_session, _sourceSession)) return;
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3H();
        _session = _sourceSession;
        ResetUx3H(keepReleaseState: true);
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3H();
        _session = _sourceSession;
        ResetUx3H(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_evidenceSession is not null) ReleaseEvidenceUx3H();
        _sourceSnapshot = CaptureSourceUx3H();
        var evidence = _sourceSession.CreateContentEvidencePlanUx3H();
        if (evidence is null)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_H_EVIDENCE_FAILED stage=plan");
            return false;
        }
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-h-ui-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var created = ProjectSession.Create(root, evidence.Project);
        if (!created.Succeeded)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_H_EVIDENCE_FAILED stage=create details=" +
                     StudioSessionController.ScrubHumanUx3H(string.Join('|', created.Diagnostics.Select(item =>
                         $"{item.Code}:{item.Path}:{item.Message}"))));
            editing.Close();
            DeleteEvidenceRootUx3H(root);
            return false;
        }
        if (!StudioSessionController.MaterializeContentEvidenceUx3H(root, evidence))
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_H_EVIDENCE_FAILED stage=files");
            editing.Close();
            DeleteEvidenceRootUx3H(root);
            return false;
        }
        var opened = editing.Open(root);
        if (!opened.Succeeded)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_H_EVIDENCE_FAILED stage=open details=" +
                     StudioSessionController.ScrubHumanUx3H(string.Join('|', opened.Diagnostics.Select(item =>
                         $"{item.Code}:{item.Path}:{item.Message}"))));
            editing.Close();
            DeleteEvidenceRootUx3H(root);
            return false;
        }
        _evidenceSession = editing;
        _evidenceRoot = root;
        _evidenceReleased = false;
        _usesRealEvidence = evidence.UsesRealModule;
        _usesFallbackEvidence = evidence.UsesCanonicalFallback;
        _session = editing;
        Visible = true;
        LoadUx3H();
        _batchSource.Text = Path.Combine(root, evidence.BatchSourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        _batchDestination.Text = evidence.BatchDestinationRelativePath;

        if (_packages.Count > 0)
        {
            _packageList.Select(0);
            SelectPackageUx3H(0);
            if (_packages[0].Sources.Count > 0)
            {
                _sourceList.Select(0);
                SelectSourceUx3H(0);
            }
        }
        if (_tables.Count > 0)
        {
            _tableList.Select(0);
            SelectTableUx3H(0);
            if (_tables[0].Columns.Count > 0)
            {
                _columnList.Select(0);
                SelectColumnUx3H(0);
                _cellColumn.Select(0);
                SelectCellColumnUx3H(0);
            }
            if (_tables[0].Rows.Count > 0)
            {
                _rowList.Select(0);
                SelectRowUx3H(0);
            }
        }
        var blueprintIndex = _blueprints.Select((item, index) => (item, index))
            .FirstOrDefault(pair => pair.item.Id == evidence.BlueprintId).index;
        if (_blueprints.Count > 0)
        {
            _blueprintList.Select(blueprintIndex);
            _explicitSelections = true;
        }
        _approval.ButtonPressed = true;
        PreviewContentUx3H();
        PreviewBatchUx3H();
        PreviewExportUx3H();
        _tabs.CurrentTab = mode.Contains("batch", StringComparison.OrdinalIgnoreCase) ||
                           mode.Contains("import", StringComparison.OrdinalIgnoreCase) ? 0 :
                           mode.Contains("export", StringComparison.OrdinalIgnoreCase) ? 1 :
                           mode.Contains("source", StringComparison.OrdinalIgnoreCase) ? 2 :
                           mode.Contains("table", StringComparison.OrdinalIgnoreCase) ? 3 : 4;
        var ready = ContentPreviewReady && BatchPreviewReady && ExportPreviewReady &&
                    HasExplicitSelections && ShowsHumanSurface && SourceMatchesUx3H();
        if (!ready)
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_H_EVIDENCE_FAILED stage=ready " +
                     $"content={ContentPreviewReady.ToString().ToLowerInvariant()} " +
                     $"batch={BatchPreviewReady.ToString().ToLowerInvariant()} " +
                     $"export={ExportPreviewReady.ToString().ToLowerInvariant()} " +
                     $"selection={HasExplicitSelections.ToString().ToLowerInvariant()} " +
                     $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                     $"source={SourceMatchesUx3H().ToString().ToLowerInvariant()} " +
                     $"content_message={StudioSessionController.ScrubHumanUx3H(_contentPreview?.Message ?? "absent")} " +
                     $"content_diagnostics={StudioSessionController.ScrubHumanUx3H(string.Join(',', (_contentPreview?.Diagnostics ?? []).Select(item => $"{item.Code}:{item.Path}")))} " +
                     $"export_message={StudioSessionController.ScrubHumanUx3H(_exportPreview?.Message ?? "absent")}");
        return ready || FailAndReleaseUx3H();
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux3-h-content-data")) return false;
        if (_evidenceSession is null || _evidenceRoot is null || _draft is null ||
            !ReferenceEquals(_session, _evidenceSession) || !ContentPreviewReady ||
            !BatchPreviewReady || !ExportPreviewReady)
        {
            CloseEvidence();
            return false;
        }
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var original = ProjectJsonSerializer.Serialize(isolated.Project!);
        var batch = ApplyBatchUx3H();
        var batchUndo = batch && UndoBatchUx3H();
        var batchRedo = batchUndo && RedoBatchUx3H();
        var export = ApplyExportUx3H();
        var exportUndo = export && UndoExportUx3H();
        var exportRedo = exportUndo && RedoExportUx3H();
        var applied = ApplyContentUx3H();
        var undone = applied && UndoContentUx3H();
        var redone = undone && RedoContentUx3H();
        var saved = redone && SaveContentUx3H();
        var reopened = saved ? ProjectSession.Open(root) : null;
        var persisted = reopened is { Succeeded: true, Session: not null } &&
                        ProjectJsonSerializer.Serialize(reopened.Session.Project) ==
                        ProjectJsonSerializer.Serialize(isolated.Project!);
        var changed = original != ProjectJsonSerializer.Serialize(isolated.Project!);
        CloseEvidence();
        return batch && batchUndo && batchRedo && export && exportUndo && exportRedo &&
               applied && undone && redone && saved && persisted && changed &&
               _evidenceReleased && SourceMatchesUx3H();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = true;
        LoadUx3H(cancelApproval: false);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = false;
        ResetUx3H(keepReleaseState: true);
    }

    private void LoadUx3H(bool cancelApproval = true)
    {
        _updating = true;
        if (cancelApproval) _session.CancelContentApprovalUx3H();
        _draft = _session.CreateContentDraftUx3H();
        _packages = _draft?.Packages.Packages.ToList() ?? [];
        _tables = _draft?.DataTables.ToList() ?? [];
        _blueprints = _session.ListBlueprintsUx3H();
        _packageIndex = _sourceIndex = _tableIndex = _columnIndex = _rowIndex = _cellColumnIndex = -1;
        _explicitSelections = false;
        if (_draft is not null)
        {
            _glob.Text = _draft.BatchGlob;
            _extensions.Text = string.Join(",", _draft.BatchExtensions);
            _collision.Selected = Array.IndexOf(CollisionPolicies, _draft.BatchCollisionPolicy);
            _batchProvenance.Text = _draft.BatchProvenance;
            _batchLicense.Text = _draft.BatchLicenseSpdx;
            _catalogProvenance.Text = _draft.Packages.Provenance;
            _conflictPolicy.Selected = Array.IndexOf(ConflictPolicies, _draft.Packages.ConflictPolicy);
            _workspaceProvenance.Text = _draft.Provenance;
            _mergePolicy.Selected = Array.IndexOf(MergePolicies, _draft.MergePolicy);
        }
        RefreshPackagesUx3H();
        RefreshTablesUx3H();
        _blueprintList.Clear();
        foreach (var blueprint in _blueprints)
        {
            _blueprintList.AddItem($"{blueprint.Label} · {blueprint.Kind}");
            _blueprintList.SetItemTooltip(_blueprintList.ItemCount - 1, blueprint.SourceLabel);
        }
        _context.Text = _usesFallbackEvidence
            ? "SESSION DE PREUVE ISOLÉE · projet réel copié, sources et tables canoniques ajoutées seulement dans l’isolat."
            : "PROJET COURANT · état vide conservé ; sélection et création restent explicites.";
        _updating = false;
        InvalidateAllUx3H();
        UpdateActionsUx3H();
    }

    private void PreviewContentUx3H()
    {
        if (!TryReadDraftUx3H(out var draft, out var error))
        {
            SetStatusUx3H(error, true);
            return;
        }
        draft = draft with { Approved = _approval.ButtonPressed };
        _draft = draft;
        _contentPreview = _session.PreviewContentUx3H(draft);
        _contentSummary.Text = $"{_contentPreview.Message}\n{_contentPreview.PatchSummary} · inverse : {_contentPreview.InverseSummary}";
        FillLinesUx3H(_semanticDiff, _contentPreview.SemanticDiff, StudioStyle.Text);
        FillLinesUx3H(_diagnostics, _contentPreview.Diagnostics.Select(item =>
            $"{item.Code} · {item.Path}\n{item.Message}"), _contentPreview.Diagnostics.Count == 0 ? StudioStyle.Success : StudioStyle.Warning);
        FillLinesUx3H(_sourcePlan, _contentPreview.SourcePlan, StudioStyle.Text);
        FillLinesUx3H(_tablePlan, _contentPreview.TablePlan, StudioStyle.Text);
        SetStatusUx3H(_contentPreview.Message, !_contentPreview.Succeeded);
        UpdateActionsUx3H();
    }

    private bool ApplyContentUx3H()
    {
        if (!TryReadDraftUx3H(out var draft, out var error))
        {
            SetStatusUx3H(error, true);
            return false;
        }
        draft = draft with { Approved = _approval.ButtonPressed };
        var result = _session.ApplyApprovedContentUx3H(draft);
        SetStatusUx3H(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        WorkspaceChanged?.Invoke();
        LoadUx3H();
        return true;
    }

    private bool UndoContentUx3H()
    {
        var result = _session.UndoContentUx3H();
        SetStatusUx3H(result.Message, !result.Succeeded);
        if (result.Succeeded) { WorkspaceChanged?.Invoke(); LoadUx3H(); }
        return result.Succeeded;
    }

    private bool RedoContentUx3H()
    {
        var result = _session.RedoContentUx3H();
        SetStatusUx3H(result.Message, !result.Succeeded);
        if (result.Succeeded) { WorkspaceChanged?.Invoke(); LoadUx3H(); }
        return result.Succeeded;
    }

    private bool SaveContentUx3H()
    {
        var result = _session.SaveContentUx3H();
        SetStatusUx3H(result.Message, !result.Succeeded);
        UpdateActionsUx3H();
        return result.Succeeded;
    }

    private void PreviewBatchUx3H()
    {
        if (!TryReadDraftUx3H(out var draft, out var error))
        {
            SetStatusUx3H(error, true);
            return;
        }
        _draft = draft;
        _batchPreview = _session.PreviewBatchUx3H(draft, _batchSource.Text, _batchDestination.Text);
        _batchSummary.Text = _batchPreview.Message;
        FillLinesUx3H(_batchEntries,
            _batchPreview.Entries.Concat(_batchPreview.Diagnostics.Select(item => $"Diagnostic · {item}")),
            _batchPreview.Succeeded ? StudioStyle.Text : StudioStyle.Warning);
        SetStatusUx3H(_batchPreview.Message, !_batchPreview.Succeeded);
        UpdateActionsUx3H();
    }

    private bool ApplyBatchUx3H()
    {
        if (!TryReadDraftUx3H(out var draft, out var error))
        {
            SetStatusUx3H(error, true);
            return false;
        }
        var result = _session.ApplyBatchUx3H(draft, _batchSource.Text, _batchDestination.Text);
        SetStatusUx3H(result.Message, !result.Succeeded);
        if (result.Succeeded) _batchPreview = null;
        UpdateActionsUx3H();
        return result.Succeeded;
    }

    private bool UndoBatchUx3H()
    {
        var result = _session.UndoBatchUx3H();
        SetStatusUx3H(result.Message, !result.Succeeded);
        UpdateActionsUx3H();
        return result.Succeeded;
    }

    private bool RedoBatchUx3H()
    {
        var result = _session.RedoBatchUx3H();
        SetStatusUx3H(result.Message, !result.Succeeded);
        UpdateActionsUx3H();
        return result.Succeeded;
    }

    private void PreviewExportUx3H()
    {
        var selected = SelectedBlueprintsUx3H();
        _exportPreview = _session.PreviewBlueprintExportUx3H(selected, _exportOutput.Text, _exportPreviews.ButtonPressed);
        _exportSummary.Text = _exportPreview.Message;
        FillLinesUx3H(_exportEntries, _exportPreview.Entries.Take(18),
            _exportPreview.Succeeded ? StudioStyle.Text : StudioStyle.Warning);
        SetStatusUx3H(_exportPreview.Message, !_exportPreview.Succeeded);
        UpdateActionsUx3H();
    }

    private bool ApplyExportUx3H()
    {
        var result = _session.ApplyBlueprintExportUx3H(
            SelectedBlueprintsUx3H(), _exportOutput.Text, _exportPreviews.ButtonPressed);
        SetStatusUx3H(result.Message, !result.Succeeded);
        if (result.Succeeded) _exportPreview = null;
        UpdateActionsUx3H();
        return result.Succeeded;
    }

    private bool UndoExportUx3H()
    {
        var result = _session.UndoBlueprintExportUx3H();
        SetStatusUx3H(result.Message, !result.Succeeded);
        UpdateActionsUx3H();
        return result.Succeeded;
    }

    private bool RedoExportUx3H()
    {
        var result = _session.RedoBlueprintExportUx3H();
        SetStatusUx3H(result.Message, !result.Succeeded);
        UpdateActionsUx3H();
        return result.Succeeded;
    }

    private bool TryReadDraftUx3H(out StudioUx3HContentDraft draft, out string error)
    {
        draft = _draft!;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Aucun projet contenu n’est chargé.";
            return false;
        }
        var previousUpdating = _updating;
        var previousSuppression = _suppressInvalidation;
        try
        {
            _updating = true;
            _suppressInvalidation = true;
            SaveCellCoreUx3H();
            SaveRowCoreUx3H();
            SaveColumnCoreUx3H();
            SaveTableCoreUx3H();
            SaveSourceCoreUx3H();
            SavePackageCoreUx3H();
            var extensions = SplitUx3H(_extensions.Text)
                .Select(item => item.StartsWith(".", StringComparison.Ordinal) ? item : $".{item}")
                .ToArray();
            var catalog = new ContentPackageCatalog(
                _draft.Packages.Id,
                _draft.Packages.SchemaVersion,
                _catalogProvenance.Text,
                ConflictPolicies[Math.Clamp(_conflictPolicy.Selected, 0, ConflictPolicies.Length - 1)],
                _packages.ToArray());
            draft = _draft with
            {
                BatchGlob = _glob.Text,
                BatchExtensions = extensions,
                BatchCollisionPolicy = CollisionPolicies[Math.Clamp(_collision.Selected, 0, CollisionPolicies.Length - 1)],
                BatchProvenance = _batchProvenance.Text,
                BatchLicenseSpdx = _batchLicense.Text,
                MergePolicy = MergePolicies[Math.Clamp(_mergePolicy.Selected, 0, MergePolicies.Length - 1)],
                Provenance = _workspaceProvenance.Text,
                Packages = catalog,
                DataTables = _tables.ToArray(),
                Approved = _approval.ButtonPressed,
            };
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        {
            error = StudioSessionController.ScrubHumanUx3H(exception.Message);
            return false;
        }
        finally
        {
            _updating = previousUpdating;
            _suppressInvalidation = previousSuppression;
        }
    }

    private void SelectPackageUx3H(long index)
    {
        if (_updating || index < 0 || index >= _packages.Count) return;
        _packageIndex = (int)index;
        _sourceIndex = -1;
        _explicitSelections = true;
        var package = _packages[_packageIndex];
        _updating = true;
        _packageNamespace.Text = package.PackageNamespace;
        _packageVersion.Text = package.Version;
        _packageProvenance.Text = package.Provenance;
        _packageLicense.Text = package.LicenseSpdx;
        _packagePriority.Text = package.Priority.ToString(CultureInfo.InvariantCulture);
        RefreshSourcesUx3H();
        _updating = false;
        InvalidateContentUx3H();
    }

    private void AddPackageUx3H()
    {
        var draft = new ContentPackageDefinition(
            Guid.NewGuid(), 1, "project-author", $"project.package-{_packages.Count + 1}", "1.0.0",
            "CC0-1.0", new string('0', 64), 0, [], [], [], []);
        _packages.Add(StudioSessionController.RebuildPackageUx3H(draft));
        RefreshPackagesUx3H();
        _packageList.Select(_packages.Count - 1);
        SelectPackageUx3H(_packages.Count - 1);
    }

    private void RemovePackageUx3H()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count) return;
        _packages.RemoveAt(_packageIndex);
        _packageIndex = _sourceIndex = -1;
        RefreshPackagesUx3H();
        ClearPackageFieldsUx3H();
        InvalidateContentUx3H();
    }

    private void SavePackageUx3H()
    {
        try { SavePackageCoreUx3H(); SetStatusUx3H("Package mis à jour dans le brouillon.", false); }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        { SetStatusUx3H(exception.Message, true); }
    }

    private void SavePackageCoreUx3H()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count) return;
        var existing = _packages[_packageIndex];
        if (!int.TryParse(_packagePriority.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority))
            throw new FormatException("La priorité du package doit être un entier.");
        var updated = new ContentPackageDefinition(
            existing.Id, existing.SchemaVersion, _packageProvenance.Text, _packageNamespace.Text,
            _packageVersion.Text, _packageLicense.Text, new string('0', 64), priority,
            existing.SealedResources, existing.Dependencies, existing.Sources, existing.Assets);
        _packages[_packageIndex] = StudioSessionController.RebuildPackageUx3H(updated);
        RefreshPackagesUx3H(keepSelection: true);
        InvalidateContentUx3H();
    }

    private void SelectSourceUx3H(long index)
    {
        if (_updating || _packageIndex < 0 || _packageIndex >= _packages.Count) return;
        var sources = _packages[_packageIndex].Sources;
        if (index < 0 || index >= sources.Count) return;
        _sourceIndex = (int)index;
        _explicitSelections = true;
        var source = sources[_sourceIndex];
        _updating = true;
        _sourceLogical.Text = source.LogicalResourceId;
        _sourcePath.Text = source.RelativePath;
        _sourceKind.Selected = Array.IndexOf(SourceKinds, source.Kind);
        _sourceRequired.ButtonPressed = source.Required;
        _sourceLicense.Text = source.LicenseSpdx;
        _sourceBudget.Text = source.PlatformByteBudget.ToString(CultureInfo.InvariantCulture);
        _sourceProvenance.Text = source.Provenance;
        _sourceAliases.Text = string.Join(", ", source.Aliases ?? []);
        _sourceReferences.Text = string.Join(", ", source.References ?? []);
        _sourceHash.Text = source.ContentSha256;
        _updating = false;
        InvalidateContentUx3H();
    }

    private void AddSourceUx3H()
    {
        if (_packageIndex < 0 || _packageIndex >= _packages.Count)
        {
            SetStatusUx3H("Sélectionnez ou créez d’abord un package.", true);
            return;
        }
        var package = _packages[_packageIndex];
        var sources = package.Sources.Append(new ContentPackageSource(
            Guid.NewGuid(), 1, "project-author", $"resource/source-{package.Sources.Count + 1}",
            $"content/source-{package.Sources.Count + 1}.json", ContentSourceKind.Json, false,
            "CC0-1.0", new string('0', 64), 1_048_576, [], [])).ToArray();
        _packages[_packageIndex] = StudioSessionController.RebuildPackageUx3H(new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources,
            package.Dependencies, sources, package.Assets));
        RefreshSourcesUx3H();
        _sourceList.Select(sources.Length - 1);
        SelectSourceUx3H(sources.Length - 1);
    }

    private void RemoveSourceUx3H()
    {
        if (_packageIndex < 0 || _sourceIndex < 0 || _packageIndex >= _packages.Count) return;
        var package = _packages[_packageIndex];
        var sources = package.Sources.Where((_, index) => index != _sourceIndex).ToArray();
        _packages[_packageIndex] = StudioSessionController.RebuildPackageUx3H(new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources,
            package.Dependencies, sources, package.Assets));
        _sourceIndex = -1;
        RefreshSourcesUx3H();
        ClearSourceFieldsUx3H();
        InvalidateContentUx3H();
    }

    private void SaveSourceUx3H()
    {
        try { SaveSourceCoreUx3H(); SetStatusUx3H("Source mise à jour dans le brouillon.", false); }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        { SetStatusUx3H(exception.Message, true); }
    }

    private void SaveSourceCoreUx3H()
    {
        if (_packageIndex < 0 || _sourceIndex < 0 || _packageIndex >= _packages.Count) return;
        var package = _packages[_packageIndex];
        if (_sourceIndex >= package.Sources.Count) return;
        if (!long.TryParse(_sourceBudget.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var budget))
            throw new FormatException("Le budget de source doit être un entier d’octets.");
        var existing = package.Sources[_sourceIndex];
        var source = new ContentPackageSource(
            existing.Id, existing.SchemaVersion, _sourceProvenance.Text, _sourceLogical.Text,
            ContentPackageDefinition.NormalizePath(_sourcePath.Text),
            SourceKinds[Math.Clamp(_sourceKind.Selected, 0, SourceKinds.Length - 1)],
            _sourceRequired.ButtonPressed, _sourceLicense.Text, _sourceHash.Text, budget,
            SplitUx3H(_sourceAliases.Text), SplitUx3H(_sourceReferences.Text));
        var sources = package.Sources.Select((item, index) => index == _sourceIndex ? source : item).ToArray();
        _packages[_packageIndex] = StudioSessionController.RebuildPackageUx3H(new ContentPackageDefinition(
            package.Id, package.SchemaVersion, package.Provenance, package.PackageNamespace, package.Version,
            package.LicenseSpdx, package.ContentSha256, package.Priority, package.SealedResources,
            package.Dependencies, sources, package.Assets));
        RefreshSourcesUx3H(keepSelection: true);
        InvalidateContentUx3H();
    }

    private void ReadSourceHashUx3H()
    {
        var result = _session.ReadSourceHashUx3H(_sourcePath.Text, out var hash);
        if (result.Succeeded) _sourceHash.Text = hash;
        SetStatusUx3H(result.Message, !result.Succeeded);
        InvalidateContentUx3H();
    }

    private void SelectTableUx3H(long index)
    {
        if (_updating || index < 0 || index >= _tables.Count) return;
        _tableIndex = (int)index;
        _columnIndex = _rowIndex = _cellColumnIndex = -1;
        _explicitSelections = true;
        var table = _tables[_tableIndex];
        _updating = true;
        _tableSlug.Text = table.Slug;
        _tableName.Text = table.DisplayName;
        _tableProvenance.Text = table.Provenance;
        _tableLicense.Text = table.LicenseSpdx;
        RefreshTableReferencesUx3H();
        _tableOverride.Selected = table.OverridesTableId is null ? 0 :
            Math.Max(0, _tables.FindIndex(item => item.Id == table.OverridesTableId) + 1);
        RefreshColumnsUx3H();
        RefreshRowsUx3H();
        _updating = false;
        InvalidateContentUx3H();
    }

    private void AddTableUx3H()
    {
        var column = new DataColumnDefinition(DataColumnId.From(Guid.NewGuid()), "value", "Valeur", DataColumnKind.Text);
        var row = new DataRowDefinition(DataRowId.From(Guid.NewGuid()), new Dictionary<string, string> { ["value"] = string.Empty });
        var slug = $"table-{_tables.Count + 1}";
        var hash = StudioSessionController.ComputeTableHashUx3H(slug, $"Table {_tables.Count + 1}", [column], [row],
            null, "project-author", "CC0-1.0");
        _tables.Add(new DataTableDefinition(DataTableId.From(Guid.NewGuid()), slug, $"Table {_tables.Count + 1}",
            [column], [row], provenance: "project-author", licenseSpdx: "CC0-1.0", contentHash: hash));
        RefreshTablesUx3H();
        _tableList.Select(_tables.Count - 1);
        SelectTableUx3H(_tables.Count - 1);
    }

    private void RemoveTableUx3H()
    {
        if (_tableIndex < 0 || _tableIndex >= _tables.Count) return;
        var target = _tables[_tableIndex].Id;
        if (_tables.Any(item => item.OverridesTableId == target || item.Columns.Any(column => column.ReferenceTableId == target)))
        {
            SetStatusUx3H("Cette table est référencée ; retirez d’abord les overrides ou colonnes concernées.", true);
            return;
        }
        _tables.RemoveAt(_tableIndex);
        _tableIndex = _columnIndex = _rowIndex = _cellColumnIndex = -1;
        RefreshTablesUx3H();
        ClearTableFieldsUx3H();
        InvalidateContentUx3H();
    }

    private void SaveTableUx3H()
    {
        try { SaveTableCoreUx3H(); SetStatusUx3H("Table mise à jour dans le brouillon.", false); }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        { SetStatusUx3H(exception.Message, true); }
    }

    private void SaveTableCoreUx3H()
    {
        if (_tableIndex < 0 || _tableIndex >= _tables.Count) return;
        var existing = _tables[_tableIndex];
        var overrideId = _tableOverride.Selected <= 0 ? null : _tables[_tableOverride.Selected - 1].Id;
        var hash = StudioSessionController.ComputeTableHashUx3H(
            _tableSlug.Text, _tableName.Text, existing.Columns, existing.Rows, overrideId,
            _tableProvenance.Text, _tableLicense.Text);
        _tables[_tableIndex] = new DataTableDefinition(
            existing.Id, _tableSlug.Text, _tableName.Text, existing.Columns, existing.Rows, overrideId,
            _tableProvenance.Text, _tableLicense.Text, hash);
        RefreshTablesUx3H(keepSelection: true);
        InvalidateContentUx3H();
    }

    private void SelectColumnUx3H(long index)
    {
        if (_updating || _tableIndex < 0 || _tableIndex >= _tables.Count) return;
        var columns = _tables[_tableIndex].Columns;
        if (index < 0 || index >= columns.Count) return;
        _columnIndex = (int)index;
        _explicitSelections = true;
        var column = columns[_columnIndex];
        _updating = true;
        _columnKey.Text = column.Key;
        _columnName.Text = column.DisplayName;
        _columnKind.Selected = Array.IndexOf(ColumnKinds, column.Kind);
        _columnRequired.ButtonPressed = column.Required;
        RefreshTableReferencesUx3H();
        _columnReference.Selected = column.ReferenceTableId is null ? 0 :
            Math.Max(0, _tables.FindIndex(item => item.Id == column.ReferenceTableId) + 1);
        _updating = false;
        InvalidateContentUx3H();
    }

    private void AddColumnUx3H()
    {
        if (_tableIndex < 0 || _tableIndex >= _tables.Count)
        {
            SetStatusUx3H("Sélectionnez ou créez d’abord une table.", true);
            return;
        }
        var table = _tables[_tableIndex];
        var key = $"column_{table.Columns.Count + 1}";
        var columns = table.Columns.Append(new DataColumnDefinition(
            DataColumnId.From(Guid.NewGuid()), key, $"Colonne {table.Columns.Count + 1}", DataColumnKind.Text)).ToArray();
        var rows = table.Rows.Select(row => new DataRowDefinition(row.Id,
            new Dictionary<string, string>(row.Values, StringComparer.Ordinal) { [key] = string.Empty }, row.Provenance)).ToArray();
        ReplaceTableShapeUx3H(table, columns, rows);
        RefreshColumnsUx3H();
        RefreshRowsUx3H();
        _columnList.Select(columns.Length - 1);
        SelectColumnUx3H(columns.Length - 1);
    }

    private void RemoveColumnUx3H()
    {
        if (_tableIndex < 0 || _columnIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        if (table.Columns.Count <= 1)
        {
            SetStatusUx3H("Une table doit conserver au moins une colonne.", true);
            return;
        }
        var key = table.Columns[_columnIndex].Key;
        var columns = table.Columns.Where((_, index) => index != _columnIndex).ToArray();
        var rows = table.Rows.Select(row => new DataRowDefinition(row.Id,
            row.Values.Where(item => item.Key != key).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            row.Provenance)).ToArray();
        ReplaceTableShapeUx3H(table, columns, rows);
        _columnIndex = _cellColumnIndex = -1;
        RefreshColumnsUx3H();
        RefreshRowsUx3H();
        ClearColumnFieldsUx3H();
        InvalidateContentUx3H();
    }

    private void SaveColumnUx3H()
    {
        try { SaveColumnCoreUx3H(); SetStatusUx3H("Colonne mise à jour dans le brouillon.", false); }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        { SetStatusUx3H(exception.Message, true); }
    }

    private void SaveColumnCoreUx3H()
    {
        if (_tableIndex < 0 || _columnIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        if (_columnIndex >= table.Columns.Count) return;
        var existing = table.Columns[_columnIndex];
        var kind = ColumnKinds[Math.Clamp(_columnKind.Selected, 0, ColumnKinds.Length - 1)];
        var reference = kind == DataColumnKind.Reference && _columnReference.Selected > 0
            ? _tables[_columnReference.Selected - 1].Id
            : null;
        var column = new DataColumnDefinition(
            existing.Id, _columnKey.Text, _columnName.Text, kind, _columnRequired.ButtonPressed, reference);
        var columns = table.Columns.Select((item, index) => index == _columnIndex ? column : item).ToArray();
        var rows = table.Rows;
        if (!string.Equals(existing.Key, column.Key, StringComparison.Ordinal))
        {
            rows = table.Rows.Select(row =>
            {
                var values = row.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
                var old = values.GetValueOrDefault(existing.Key, string.Empty);
                values.Remove(existing.Key);
                values[column.Key] = old;
                return new DataRowDefinition(row.Id, values, row.Provenance);
            }).ToArray();
        }
        ReplaceTableShapeUx3H(table, columns, rows);
        RefreshColumnsUx3H(keepSelection: true);
        RefreshRowsUx3H(keepSelection: true);
        InvalidateContentUx3H();
    }

    private void SelectRowUx3H(long index)
    {
        if (_updating || _tableIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        if (index < 0 || index >= table.Rows.Count) return;
        _rowIndex = (int)index;
        _explicitSelections = true;
        _updating = true;
        _rowProvenance.Text = table.Rows[_rowIndex].Provenance;
        RefreshCellColumnsUx3H();
        if (table.Columns.Count > 0)
        {
            var cellIndex = Math.Clamp(_cellColumnIndex, 0, table.Columns.Count - 1);
            _cellColumn.Select(cellIndex);
            _cellColumnIndex = cellIndex;
            _cellValue.Text = table.Rows[_rowIndex].Values.GetValueOrDefault(table.Columns[cellIndex].Key, string.Empty);
        }
        _updating = false;
        InvalidateContentUx3H();
    }

    private void SelectCellColumnUx3H(long index)
    {
        if (_updating || _tableIndex < 0 || _rowIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        if (index < 0 || index >= table.Columns.Count || _rowIndex >= table.Rows.Count) return;
        _cellColumnIndex = (int)index;
        _updating = true;
        _cellValue.Text = table.Rows[_rowIndex].Values.GetValueOrDefault(table.Columns[_cellColumnIndex].Key, string.Empty);
        _updating = false;
        InvalidateContentUx3H();
    }

    private void AddRowUx3H()
    {
        if (_tableIndex < 0 || _tableIndex >= _tables.Count)
        {
            SetStatusUx3H("Sélectionnez ou créez d’abord une table.", true);
            return;
        }
        var table = _tables[_tableIndex];
        var values = table.Columns.ToDictionary(column => column.Key, _ => string.Empty, StringComparer.Ordinal);
        var rows = table.Rows.Append(new DataRowDefinition(DataRowId.From(Guid.NewGuid()), values)).ToArray();
        ReplaceTableShapeUx3H(table, table.Columns, rows);
        RefreshRowsUx3H();
        _rowList.Select(rows.Length - 1);
        SelectRowUx3H(rows.Length - 1);
    }

    private void RemoveRowUx3H()
    {
        if (_tableIndex < 0 || _rowIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        var rows = table.Rows.Where((_, index) => index != _rowIndex).ToArray();
        ReplaceTableShapeUx3H(table, table.Columns, rows);
        _rowIndex = -1;
        RefreshRowsUx3H();
        ClearRowFieldsUx3H();
        InvalidateContentUx3H();
    }

    private void SaveRowUx3H()
    {
        try
        {
            SaveCellCoreUx3H();
            SaveRowCoreUx3H();
            SetStatusUx3H("Ligne et cellule mises à jour dans le brouillon.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        { SetStatusUx3H(exception.Message, true); }
    }

    private void SaveRowCoreUx3H()
    {
        if (_tableIndex < 0 || _rowIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        if (_rowIndex >= table.Rows.Count) return;
        var row = table.Rows[_rowIndex];
        var updated = new DataRowDefinition(row.Id, row.Values, _rowProvenance.Text);
        var rows = table.Rows.Select((item, index) => index == _rowIndex ? updated : item).ToArray();
        ReplaceTableShapeUx3H(table, table.Columns, rows);
        RefreshRowsUx3H(keepSelection: true);
        InvalidateContentUx3H();
    }

    private void SaveCellCoreUx3H()
    {
        if (_tableIndex < 0 || _rowIndex < 0 || _cellColumnIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        if (_rowIndex >= table.Rows.Count || _cellColumnIndex >= table.Columns.Count) return;
        var row = table.Rows[_rowIndex];
        var values = row.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        values[table.Columns[_cellColumnIndex].Key] = _cellValue.Text;
        var updated = new DataRowDefinition(row.Id, values, _rowProvenance.Text);
        var rows = table.Rows.Select((item, index) => index == _rowIndex ? updated : item).ToArray();
        ReplaceTableShapeUx3H(table, table.Columns, rows);
        RefreshRowsUx3H(keepSelection: true);
        InvalidateContentUx3H();
    }

    private void ReplaceTableShapeUx3H(
        DataTableDefinition table,
        IReadOnlyList<DataColumnDefinition> columns,
        IReadOnlyList<DataRowDefinition> rows)
    {
        var hash = StudioSessionController.ComputeTableHashUx3H(
            table.Slug, table.DisplayName, columns, rows, table.OverridesTableId,
            table.Provenance, table.LicenseSpdx);
        _tables[_tableIndex] = new DataTableDefinition(
            table.Id, table.Slug, table.DisplayName, columns, rows, table.OverridesTableId,
            table.Provenance, table.LicenseSpdx, hash);
    }

    private void RefreshPackagesUx3H(bool keepSelection = false)
    {
        _updating = true;
        _packageList.Clear();
        foreach (var package in _packages)
            _packageList.AddItem($"{StudioSessionController.ScrubHumanUx3H(package.PackageNamespace)} · v{package.Version} · " +
                                 $"{package.Sources.Count} source(s)");
        if (keepSelection && _packageIndex >= 0 && _packageIndex < _packages.Count) _packageList.Select(_packageIndex);
        _updating = false;
    }

    private void RefreshSourcesUx3H(bool keepSelection = false)
    {
        _sourceList.Clear();
        if (_packageIndex < 0 || _packageIndex >= _packages.Count) return;
        foreach (var source in _packages[_packageIndex].Sources)
            _sourceList.AddItem($"{StudioSessionController.ScrubHumanUx3H(source.LogicalResourceId)} · " +
                                $"{StudioSessionController.HumanSourceKindUx3H(source.Kind)} · " +
                                StudioSessionController.HumanFileUx3H(source.RelativePath));
        if (keepSelection && _sourceIndex >= 0 && _sourceIndex < _packages[_packageIndex].Sources.Count)
            _sourceList.Select(_sourceIndex);
    }

    private void RefreshTablesUx3H(bool keepSelection = false)
    {
        _updating = true;
        _tableList.Clear();
        foreach (var table in _tables)
            _tableList.AddItem($"{StudioSessionController.ScrubHumanUx3H(table.DisplayName)} · " +
                               $"{table.Columns.Count} colonne(s) · {table.Rows.Count} ligne(s)");
        if (keepSelection && _tableIndex >= 0 && _tableIndex < _tables.Count) _tableList.Select(_tableIndex);
        RefreshTableReferencesUx3H();
        _updating = false;
    }

    private void RefreshTableReferencesUx3H()
    {
        var overrideSelection = _tableOverride.Selected;
        var referenceSelection = _columnReference.Selected;
        _tableOverride.Clear();
        _tableOverride.AddItem("Aucune table de base");
        _columnReference.Clear();
        _columnReference.AddItem("Aucune référence");
        foreach (var table in _tables)
        {
            var label = StudioSessionController.ScrubHumanUx3H(table.DisplayName);
            _tableOverride.AddItem(label);
            _columnReference.AddItem(label);
        }
        _tableOverride.Selected = Math.Clamp(overrideSelection, 0, Math.Max(0, _tableOverride.ItemCount - 1));
        _columnReference.Selected = Math.Clamp(referenceSelection, 0, Math.Max(0, _columnReference.ItemCount - 1));
    }

    private void RefreshColumnsUx3H(bool keepSelection = false)
    {
        _columnList.Clear();
        if (_tableIndex < 0 || _tableIndex >= _tables.Count) return;
        foreach (var column in _tables[_tableIndex].Columns)
            _columnList.AddItem($"{StudioSessionController.ScrubHumanUx3H(column.DisplayName)} · " +
                                StudioSessionController.HumanColumnKindUx3H(column.Kind));
        if (keepSelection && _columnIndex >= 0 && _columnIndex < _tables[_tableIndex].Columns.Count)
            _columnList.Select(_columnIndex);
        RefreshCellColumnsUx3H();
    }

    private void RefreshRowsUx3H(bool keepSelection = false)
    {
        _rowList.Clear();
        if (_tableIndex < 0 || _tableIndex >= _tables.Count) return;
        var table = _tables[_tableIndex];
        for (var index = 0; index < table.Rows.Count; index++)
        {
            var first = table.Columns.Select(column => table.Rows[index].Values.GetValueOrDefault(column.Key))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            _rowList.AddItem($"Ligne {index + 1} · {StudioSessionController.ScrubHumanUx3H(first ?? "vide")}");
        }
        if (keepSelection && _rowIndex >= 0 && _rowIndex < table.Rows.Count) _rowList.Select(_rowIndex);
    }

    private void RefreshCellColumnsUx3H()
    {
        _cellColumn.Clear();
        if (_tableIndex < 0 || _tableIndex >= _tables.Count) return;
        foreach (var column in _tables[_tableIndex].Columns)
            _cellColumn.AddItem(StudioSessionController.ScrubHumanUx3H(column.DisplayName));
    }

    private IReadOnlyList<BlueprintId> SelectedBlueprintsUx3H() => _blueprintList.GetSelectedItems()
        .Select(index => _blueprints[(int)index].Id).ToArray();

    private void ConnectInvalidationUx3H()
    {
        foreach (var field in new[]
                 {
                     _glob, _extensions, _batchProvenance, _batchLicense,
                     _catalogProvenance, _packageNamespace, _packageVersion, _packageProvenance,
                     _packageLicense, _packagePriority, _sourceLogical, _sourcePath, _sourceLicense,
                     _sourceBudget, _sourceProvenance, _sourceAliases, _sourceReferences,
                     _workspaceProvenance, _tableSlug, _tableName, _tableProvenance, _tableLicense,
                     _columnKey, _columnName, _rowProvenance, _cellValue,
                 })
            field.TextChanged += _ => InvalidateContentUx3H();
        _batchSource.TextChanged += _ => InvalidateBatchUx3H();
        _batchDestination.TextChanged += _ => InvalidateBatchUx3H();
        _exportOutput.TextChanged += _ => InvalidateExportUx3H();
        _collision.ItemSelected += _ => { InvalidateContentUx3H(); InvalidateBatchUx3H(); };
        _conflictPolicy.ItemSelected += _ => InvalidateContentUx3H();
        _sourceKind.ItemSelected += _ => InvalidateContentUx3H();
        _sourceRequired.Toggled += _ => InvalidateContentUx3H();
        _mergePolicy.ItemSelected += _ => InvalidateContentUx3H();
        _tableOverride.ItemSelected += _ => InvalidateContentUx3H();
        _columnKind.ItemSelected += _ => InvalidateContentUx3H();
        _columnRequired.Toggled += _ => InvalidateContentUx3H();
        _columnReference.ItemSelected += _ => InvalidateContentUx3H();
        _exportPreviews.Toggled += _ => InvalidateExportUx3H();
        _approval.Toggled += _ => InvalidateContentUx3H();
    }

    private void InvalidateAllUx3H()
    {
        _contentPreview = null;
        _batchPreview = null;
        _exportPreview = null;
        _contentSummary.Text = "Prévisualisation requise.";
        _batchSummary.Text = "Aucun import prévisualisé.";
        _exportSummary.Text = "Aucun export prévisualisé.";
        ClearChildrenUx3H(_semanticDiff);
        ClearChildrenUx3H(_diagnostics);
        ClearChildrenUx3H(_sourcePlan);
        ClearChildrenUx3H(_tablePlan);
        ClearChildrenUx3H(_batchEntries);
        ClearChildrenUx3H(_exportEntries);
        UpdateActionsUx3H();
    }

    private void InvalidateContentUx3H()
    {
        if (_updating || _suppressInvalidation) return;
        _contentPreview = null;
        _contentSummary.Text = "Prévisualisation requise après modification.";
        ClearChildrenUx3H(_semanticDiff);
        ClearChildrenUx3H(_diagnostics);
        ClearChildrenUx3H(_sourcePlan);
        ClearChildrenUx3H(_tablePlan);
        _session.CancelContentApprovalUx3H();
        InvalidateBatchUx3H();
        UpdateActionsUx3H();
    }

    private void InvalidateBatchUx3H()
    {
        if (_updating || _suppressInvalidation) return;
        _batchPreview = null;
        _batchSummary.Text = "Aucun import prévisualisé.";
        ClearChildrenUx3H(_batchEntries);
        UpdateActionsUx3H();
    }

    private void InvalidateExportUx3H()
    {
        if (_updating || _suppressInvalidation) return;
        _exportPreview = null;
        _exportSummary.Text = "Aucun export prévisualisé.";
        ClearChildrenUx3H(_exportEntries);
        UpdateActionsUx3H();
    }

    private void UpdateActionsUx3H()
    {
        _contentApplyButton.Disabled = !ContentPreviewReady;
        _batchApplyButton.Disabled = !BatchPreviewReady;
        _exportApplyButton.Disabled = !ExportPreviewReady;
        _batchUndoButton.Disabled = !_session.CanUndoBatchUx3H;
        _batchRedoButton.Disabled = !_session.CanRedoBatchUx3H;
        _exportUndoButton.Disabled = !_session.CanUndoExportUx3H;
        _exportRedoButton.Disabled = !_session.CanRedoExportUx3H;
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = _session.Project is null;
    }

    private void ResetUx3H(bool keepReleaseState = false)
    {
        _updating = true;
        _draft = null;
        _contentPreview = null;
        _batchPreview = null;
        _exportPreview = null;
        _packages = [];
        _tables = [];
        _blueprints = [];
        _packageIndex = _sourceIndex = _tableIndex = _columnIndex = _rowIndex = _cellColumnIndex = -1;
        _explicitSelections = false;
        _packageList.Clear();
        _sourceList.Clear();
        _tableList.Clear();
        _columnList.Clear();
        _rowList.Clear();
        _cellColumn.Clear();
        _blueprintList.Clear();
        ClearPackageFieldsUx3H();
        ClearSourceFieldsUx3H();
        ClearTableFieldsUx3H();
        ClearChildrenUx3H(_semanticDiff);
        ClearChildrenUx3H(_diagnostics);
        ClearChildrenUx3H(_sourcePlan);
        ClearChildrenUx3H(_tablePlan);
        ClearChildrenUx3H(_batchEntries);
        ClearChildrenUx3H(_exportEntries);
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        UpdateActionsUx3H();
    }

    private void ClearPackageFieldsUx3H()
    {
        _packageNamespace.Text = string.Empty;
        _packageVersion.Text = string.Empty;
        _packageProvenance.Text = string.Empty;
        _packageLicense.Text = string.Empty;
        _packagePriority.Text = string.Empty;
    }

    private void ClearSourceFieldsUx3H()
    {
        _sourceLogical.Text = string.Empty;
        _sourcePath.Text = string.Empty;
        _sourceLicense.Text = string.Empty;
        _sourceBudget.Text = string.Empty;
        _sourceProvenance.Text = string.Empty;
        _sourceAliases.Text = string.Empty;
        _sourceReferences.Text = string.Empty;
        _sourceHash.Text = string.Empty;
    }

    private void ClearTableFieldsUx3H()
    {
        _tableSlug.Text = string.Empty;
        _tableName.Text = string.Empty;
        _tableProvenance.Text = string.Empty;
        _tableLicense.Text = string.Empty;
        ClearColumnFieldsUx3H();
        ClearRowFieldsUx3H();
    }

    private void ClearColumnFieldsUx3H()
    {
        _columnKey.Text = string.Empty;
        _columnName.Text = string.Empty;
        _columnRequired.ButtonPressed = false;
    }

    private void ClearRowFieldsUx3H()
    {
        _rowProvenance.Text = string.Empty;
        _cellValue.Text = string.Empty;
    }

    private SourceSnapshotUx3H CaptureSourceUx3H()
    {
        var history = _sourceSession.History;
        return new(
            _sourceSession.Project,
            history,
            history?.State.Revision,
            history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty,
            history?.CanUndo,
            history?.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile),
            _sourceSession.CaptureContentApprovalUx3H());
    }

    private bool SourceMatchesUx3H()
    {
        if (_sourceSnapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(_sourceSnapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(_sourceSnapshot.History, history) &&
               history?.State.Revision == _sourceSnapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(_sourceSnapshot.Journal) &&
               history?.CanUndo == _sourceSnapshot.CanUndo && history?.CanRedo == _sourceSnapshot.CanRedo &&
               _sourceSession.IsDirty == _sourceSnapshot.IsDirty &&
               _sourceSession.ActiveAreaId == _sourceSnapshot.AreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(_sourceSnapshot.Selection) &&
               _sourceSession.SelectedEntityId == _sourceSnapshot.ActiveEntity &&
               EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile) == _sourceSnapshot.Workspace &&
               _sourceSession.CaptureContentApprovalUx3H() == _sourceSnapshot.Approval;
    }

    private bool FailAndReleaseUx3H()
    {
        ReleaseEvidenceUx3H();
        return false;
    }

    private void ReleaseEvidenceUx3H()
    {
        var root = _evidenceRoot;
        _evidenceSession?.Close();
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        if (!string.IsNullOrWhiteSpace(root)) DeleteEvidenceRootUx3H(root);
        _evidenceReleased = string.IsNullOrWhiteSpace(root) || !Directory.Exists(root);
        _usesRealEvidence = false;
        _usesFallbackEvidence = false;
    }

    private static void DeleteEvidenceRootUx3H(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static ScrollContainer ScrollTabUx3H(string title, out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(body);
        return scroll;
    }

    private static LineEdit FieldUx3H(VBoxContainer body, string label, string placeholder)
    {
        body.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(field);
        return field;
    }

    private static CheckBox CheckUx3H(VBoxContainer body, string label)
    {
        var check = new CheckBox { Text = label };
        body.AddChild(check);
        return check;
    }

    private static OptionButton ChoiceUx3H(VBoxContainer body, string label, Action<long>? selected)
    {
        body.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (selected is not null) choice.ItemSelected += index => selected(index);
        body.AddChild(choice);
        return choice;
    }

    private static OptionButton EnumChoiceUx3H<T>(
        VBoxContainer body,
        string label,
        IReadOnlyList<T> values,
        Func<T, string> human) where T : struct, Enum
    {
        var choice = ChoiceUx3H(body, label, null);
        foreach (var value in values) choice.AddItem(human(value));
        return choice;
    }

    private static HBoxContainer HeadingUx3H(
        VBoxContainer body,
        string title,
        out Button add,
        out Button remove,
        out Button save,
        Action addAction,
        Action removeAction,
        Action saveAction)
    {
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        add = StudioStyle.Button("Ajouter", compact: true);
        add.Pressed += addAction;
        heading.AddChild(add);
        remove = StudioStyle.Button("Retirer", compact: true);
        remove.Pressed += removeAction;
        heading.AddChild(remove);
        save = StudioStyle.Button("Mettre à jour", compact: true);
        save.Pressed += saveAction;
        heading.AddChild(save);
        return heading;
    }

    private static string HumanConflictUx3H(ContentConflictPolicy policy) => policy switch
    {
        ContentConflictPolicy.Error => "Refuser les conflits",
        ContentConflictPolicy.HighestPriority => "Priorité la plus élevée",
        ContentConflictPolicy.NamespaceOrder => "Ordre des namespaces",
        _ => "Politique protégée",
    };

    private static IReadOnlyList<string> SplitUx3H(string value) => (value ?? string.Empty)
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void FillLinesUx3H(VBoxContainer target, IEnumerable<string> lines, Color color)
    {
        ClearChildrenUx3H(target);
        foreach (var line in lines)
            target.AddChild(StudioStyle.Label($"• {StudioSessionController.ScrubHumanUx3H(line)}", 9, color, wrap: true));
    }

    private static void ClearChildrenUx3H(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private void SetStatusUx3H(string message, bool error)
    {
        var human = StudioSessionController.ScrubHumanUx3H(message);
        _status.Text = human;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(human, error);
    }

    private sealed record SourceSnapshotUx3H(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool? CanUndo,
        bool? CanRedo,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? ActiveEntity,
        string Workspace,
        StudioUx3HApprovalSnapshot Approval);
}
