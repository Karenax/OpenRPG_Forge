#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ContentWorkflowWindow : Window
{
    private readonly List<DataTableDefinition> _tables = [];
    private Func<ProjectState?>? _getState;
    private Func<ContentWorkspaceConfiguration, bool>? _apply;
    private Func<string?>? _getWorkspaceRoot;
    private Action<Window>? _brandWindow;
    private ContentWorkspaceConfigurationId? _configurationId;
    private DataTableId? _draftTableId;
    private ContentWorkspaceConfiguration? _approved;
    private IReadOnlyList<Forge.Domain.Blueprints.BlueprintDefinition> _blueprints = [];
    private bool _suppressSignals;

    private LineEdit? _glob;
    private LineEdit? _extensions;
    private OptionButton? _collision;
    private LineEdit? _provenance;
    private LineEdit? _license;
    private LineEdit? _batchSource;
    private LineEdit? _batchDestination;
    private CheckButton? _batchDryRun;
    private Label? _batchStatus;
    private ItemList? _blueprintList;
    private LineEdit? _blueprintOutput;
    private ItemList? _tableList;
    private LineEdit? _tableSlug;
    private LineEdit? _tableName;
    private LineEdit? _tableProvenance;
    private LineEdit? _tableLicense;
    private LineEdit? _tableHash;
    private TextEdit? _columns;
    private TextEdit? _rows;
    private OptionButton? _mergePolicy;
    private LineEdit? _baseWorkspace;
    private LineEdit? _theirsWorkspace;
    private ItemList? _conflicts;
    private Label? _summary;
    private Label? _status;
    private Button? _applyButton;

    public int DataTableCount => _tables.Count;

    public int ConflictCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> getState,
        Func<ContentWorkspaceConfiguration, bool> apply,
        Func<string?> getWorkspaceRoot,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _getWorkspaceRoot = getWorkspaceRoot ?? throw new ArgumentNullException(nameof(getWorkspaceRoot));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Données & contenu en masse · OpenRPG Forge";
        MinSize = new Vector2I(1380, 850);
        Size = new Vector2I(1380, 850);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1380, 850));
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _getState?.Invoke();
        if (state is null)
        {
            SetStatus("Aucun projet ouvert.", true);
            return;
        }

        LoadConfiguration(state.Project.Module.Content);
        LoadBlueprints(state.Project);
        var root = _getWorkspaceRoot?.Invoke();
        if (!string.IsNullOrWhiteSpace(root))
        {
            _batchSource!.PlaceholderText = Path.Combine(root, "content", "incoming");
            _batchDestination!.PlaceholderText = Path.Combine(root, "content", "imported");
            _blueprintOutput!.PlaceholderText = Path.Combine(root, "exports", "selection.orpblueprints");
        }

        PreviewConfiguration();
    }

    public bool ConfigureForSmokeTest(ContentWorkspaceConfiguration configuration)
    {
        LoadConfiguration(configuration);
        PreviewConfiguration();
        return _approved is not null && _applyButton?.Disabled == false;
    }

    public bool ApplyForSmokeTest()
    {
        if (_approved is null)
        {
            return false;
        }

        ApplyConfiguration();
        return _approved is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 20);
        }

        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "DONNÉES & CONTENU EN MASSE" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Importer, exporter et fusionner sans perdre les UUID, licences, sources ni variantes concurrentes.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);
        BuildBatchCard(AddCard(body, new Vector2(360, 0)));
        BuildTableCard(AddCard(body, new Vector2(560, 0), expand: true));
        BuildMergeCard(AddCard(body, new Vector2(390, 0)));

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        var footerText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        footer.AddChild(footerText);
        _summary = new Label { Text = "Prévisualisation requise.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        footerText.AddChild(_summary);
        _status = new Label { Text = "Prêt." };
        footerText.AddChild(_status);
        var preview = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(150, 44) };
        preview.Pressed += PreviewConfiguration;
        footer.AddChild(preview);
        _applyButton = new Button { Text = "Enregistrer dans le module", CustomMinimumSize = new Vector2(250, 44), Disabled = true };
        _applyButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        _applyButton.Pressed += ApplyConfiguration;
        footer.AddChild(_applyButton);
    }

    private void BuildBatchCard(VBoxContainer card)
    {
        card.AddChild(Section("IMPORT / EXPORT EN MASSE"));
        _glob = AddField(card, "Filtre", "*.json");
        _extensions = AddField(card, "Types", ".json, .forge, .glb");
        card.AddChild(new Label { Text = "Collision" });
        _collision = new OptionButton();
        foreach (var policy in Enum.GetValues<ContentBatchCollisionPolicy>()) _collision.AddItem(FormatCollision(policy));
        card.AddChild(_collision);
        _provenance = AddField(card, "Provenance", "project-author");
        _license = AddField(card, "Licence SPDX", "CC0-1.0");
        _batchSource = AddField(card, "Dossier source", "Chemin absolu");
        _batchDestination = AddField(card, "Dossier cible", "Chemin absolu");
        _batchDryRun = new CheckButton { Text = "Prévisualisation sans écriture", ButtonPressed = true };
        card.AddChild(_batchDryRun);
        var batchActions = new HBoxContainer();
        card.AddChild(batchActions);
        var import = new Button { Text = "Importer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        import.Pressed += () => RunBatch(ContentBatchOperation.Import);
        batchActions.AddChild(import);
        var export = new Button { Text = "Exporter", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        export.Pressed += () => RunBatch(ContentBatchOperation.Export);
        batchActions.AddChild(export);
        _batchStatus = new Label { Text = "Aucun transfert exécuté.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _batchStatus.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        card.AddChild(_batchStatus);
        card.AddChild(Section("EXPORT DE BLUEPRINTS"));
        _blueprintList = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Multi,
            CustomMinimumSize = new Vector2(0, 105),
        };
        card.AddChild(_blueprintList);
        _blueprintOutput = AddField(card, "Pack de sortie", "selection.orpblueprints");
        var exportBlueprints = new Button { Text = "Exporter la sélection + dépendances" };
        exportBlueprints.Pressed += ExportBlueprints;
        card.AddChild(exportBlueprints);
    }

    private void BuildTableCard(VBoxContainer card)
    {
        card.AddChild(Section("TABLES DE DONNÉES TYPÉES"));
        var tableBody = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        card.AddChild(tableBody);
        var tableIndex = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
        tableBody.AddChild(tableIndex);
        _tableList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tableIndex.AddChild(_tableList);
        var newTable = new Button { Text = "+ Nouvelle table" };
        newTable.Pressed += ClearTableDraft;
        tableIndex.AddChild(newTable);
        var removeTable = new Button { Text = "Retirer" };
        removeTable.Pressed += RemoveTable;
        tableIndex.AddChild(removeTable);

        var editorScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tableBody.AddChild(editorScroll);
        var editor = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        editorScroll.AddChild(editor);
        _tableSlug = AddField(editor, "Identifiant lisible", "loot-tables");
        _tableName = AddField(editor, "Nom", "Tables de butin");
        _tableProvenance = AddField(editor, "Provenance", "project-author");
        _tableLicense = AddField(editor, "Licence SPDX", "CC0-1.0");
        _tableHash = AddField(editor, "SHA-256", "unverified");
        editor.AddChild(new Label { Text = "Colonnes · clé|libellé|type|required|table-référence" });
        _columns = new TextEdit { CustomMinimumSize = new Vector2(0, 105), PlaceholderText = "name|Nom|text|required\nweight|Poids|decimal|optional" };
        editor.AddChild(_columns);
        editor.AddChild(new Label { Text = "Lignes · uuid|clé=valeur;clé=valeur|provenance" });
        _rows = new TextEdit { CustomMinimumSize = new Vector2(0, 155), PlaceholderText = "new|name=Épée;weight=1.5|project-author" };
        editor.AddChild(_rows);
        var upsert = new Button { Text = "Ajouter / mettre à jour la table", CustomMinimumSize = new Vector2(0, 38) };
        upsert.Pressed += UpsertTable;
        editor.AddChild(upsert);
    }

    private void BuildMergeCard(VBoxContainer card)
    {
        card.AddChild(Section("FUSION ASSISTÉE À TROIS VOIES"));
        card.AddChild(new Label { Text = "Politique de conflit" });
        _mergePolicy = new OptionButton();
        foreach (var policy in Enum.GetValues<ContentMergePolicy>()) _mergePolicy.AddItem(FormatMerge(policy));
        card.AddChild(_mergePolicy);
        var help = new Label
        {
            Text = "La variante courante est « la nôtre ». Choisissez une table puis fournissez les espaces de base et distant.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        help.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        card.AddChild(help);
        _baseWorkspace = AddField(card, "Espace de base", "Dossier du projet ancêtre");
        _theirsWorkspace = AddField(card, "Espace distant", "Dossier de l’autre variante");
        var analyze = new Button { Text = "Analyser les conflits" };
        analyze.Pressed += AnalyzeMerge;
        card.AddChild(analyze);
        _conflicts = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        card.AddChild(_conflicts);
        var conflictHelp = new Label
        {
            Text = "Chaque conflit conserve base, notre variante et la variante distante. « Conserver les deux » crée un nouvel UUID stable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        conflictHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        card.AddChild(conflictHelp);
    }

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        _tableList!.ItemSelected += index => LoadTable((int)index);
        foreach (var field in new[]
                 {
                     _glob, _extensions, _provenance, _license, _tableSlug, _tableName, _tableProvenance,
                     _tableLicense, _tableHash,
                 })
        {
            field!.TextChanged += _ => InvalidatePreview();
        }

        _columns!.TextChanged += InvalidatePreview;
        _rows!.TextChanged += InvalidatePreview;
        _collision!.ItemSelected += _ => InvalidatePreview();
        _mergePolicy!.ItemSelected += _ => InvalidatePreview();
    }

    private void LoadConfiguration(ContentWorkspaceConfiguration configuration)
    {
        _suppressSignals = true;
        _configurationId = configuration.Id;
        _glob!.Text = configuration.Batch.Glob;
        _extensions!.Text = string.Join(", ", configuration.Batch.Extensions);
        _collision!.Selected = (int)configuration.Batch.CollisionPolicy;
        _provenance!.Text = configuration.Batch.Provenance;
        _license!.Text = configuration.Batch.LicenseSpdx;
        _mergePolicy!.Selected = (int)configuration.MergePolicy;
        _tables.Clear();
        _tables.AddRange(configuration.DataTables);
        RefreshTableList();
        if (_tables.Count > 0)
        {
            _tableList!.Select(0);
            LoadTable(0);
        }
        else
        {
            ClearTableDraft();
        }

        _suppressSignals = false;
        InvalidatePreview();
    }

    private void LoadBlueprints(ProjectDefinition project)
    {
        _blueprints = project.Blueprints.OrderBy(blueprint => blueprint.DisplayName, StringComparer.Ordinal).ToArray();
        _blueprintList!.Clear();
        foreach (var blueprint in _blueprints)
        {
            _blueprintList.AddItem($"{blueprint.DisplayName} · {blueprint.Kind}");
            _blueprintList.SetItemTooltip(_blueprintList.ItemCount - 1, $"UUID {blueprint.Id}\n{blueprint.Provenance.Source}");
        }
    }

    private void UpsertTable()
    {
        try
        {
            var existing = _draftTableId is null ? null : _tables.SingleOrDefault(table => table.Id == _draftTableId);
            var columns = ParseColumns(existing);
            var rows = ParseRows(existing);
            var table = new DataTableDefinition(
                _draftTableId ?? DataTableId.From(Guid.NewGuid()),
                _tableSlug!.Text,
                _tableName!.Text,
                columns,
                rows,
                existing?.OverridesTableId,
                _tableProvenance!.Text,
                _tableLicense!.Text,
                _tableHash!.Text);
            var index = _tables.FindIndex(candidate => candidate.Id == table.Id);
            if (index >= 0) _tables[index] = table;
            else _tables.Add(table);
            _tables.Sort((left, right) => StringComparer.Ordinal.Compare(left.Slug, right.Slug));
            _draftTableId = table.Id;
            RefreshTableList();
            var selected = _tables.FindIndex(candidate => candidate.Id == table.Id);
            _tableList!.Select(selected);
            _ = BuildConfiguration();
            InvalidatePreview();
            SetStatus($"Table « {table.DisplayName} » prête dans le brouillon.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private IReadOnlyList<DataColumnDefinition> ParseColumns(DataTableDefinition? existing)
    {
        var byKey = existing?.Columns.ToDictionary(column => column.Key, StringComparer.Ordinal) ??
                    new Dictionary<string, DataColumnDefinition>(StringComparer.Ordinal);
        var result = new List<DataColumnDefinition>();
        foreach (var line in Lines(_columns!.Text))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length is < 3 or > 5) throw new FormatException($"Colonne invalide : '{line}'.");
            var kind = ParseColumnKind(parts[2]);
            var required = parts.Length >= 4 && string.Equals(parts[3], "required", StringComparison.OrdinalIgnoreCase);
            var reference = kind == DataColumnKind.Reference && parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4])
                ? DataTableId.Parse(parts[4])
                : null;
            result.Add(new DataColumnDefinition(
                byKey.GetValueOrDefault(parts[0])?.Id ?? DataColumnId.From(Guid.NewGuid()),
                parts[0], parts[1], kind, required, reference));
        }

        return result;
    }

    private IReadOnlyList<DataRowDefinition> ParseRows(DataTableDefinition? existing)
    {
        var result = new List<DataRowDefinition>();
        foreach (var line in Lines(_rows!.Text))
        {
            var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length < 2) throw new FormatException($"Ligne invalide : '{line}'.");
            var id = string.Equals(parts[0], "new", StringComparison.OrdinalIgnoreCase)
                ? DataRowId.From(Guid.NewGuid())
                : DataRowId.Parse(parts[0]);
            if (existing is not null && string.Equals(parts[0], "new", StringComparison.OrdinalIgnoreCase) &&
                result.Count < existing.Rows.Count)
            {
                id = existing.Rows[result.Count].Id;
            }

            var values = parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Split('=', 2, StringSplitOptions.TrimEntries))
                .ToDictionary(value => value[0], value => value.Length == 2 ? value[1] : string.Empty, StringComparer.Ordinal);
            result.Add(new DataRowDefinition(id, values, parts.Length == 3 ? parts[2] : "project-author"));
        }

        return result;
    }

    private ContentWorkspaceConfiguration BuildConfiguration()
    {
        var state = _getState?.Invoke() ?? throw new InvalidOperationException("Aucun projet ouvert.");
        return new ContentWorkspaceConfiguration(
            _configurationId ?? ContentWorkspaceConfigurationId.From(state.Project.Module.Id.Value),
            new ContentBatchConfiguration(
                _glob!.Text,
                ParseCsv(_extensions!.Text),
                (ContentBatchCollisionPolicy)_collision!.Selected,
                _provenance!.Text,
                _license!.Text),
            _tables,
            (ContentMergePolicy)_mergePolicy!.Selected,
            "creator-command");
    }

    private void PreviewConfiguration()
    {
        try
        {
            _approved = BuildConfiguration();
            _applyButton!.Disabled = false;
            _summary!.Text = $"{_tables.Count} table(s) · filtre {_approved.Batch.Glob} · " +
                             $"collision {FormatCollision(_approved.Batch.CollisionPolicy)} · fusion {FormatMerge(_approved.MergePolicy)}";
            SetStatus("✓ Configuration valide et prête à enregistrer.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            InvalidatePreview();
            SetStatus(exception.Message, true);
        }
    }

    private void ApplyConfiguration()
    {
        var configuration = _approved;
        if (configuration is null || _apply?.Invoke(configuration) != true)
        {
            SetStatus("La commande de projet a refusé la configuration.", true);
            return;
        }

        _approved = null;
        _applyButton!.Disabled = true;
        SetStatus("Configuration enregistrée dans le document canonique.", false);
    }

    private void RunBatch(ContentBatchOperation operation)
    {
        try
        {
            var configuration = BuildConfiguration();
            var request = new ContentBatchTransferRequest(
                operation,
                _batchSource!.Text,
                _batchDestination!.Text,
                configuration.Batch.Glob,
                configuration.Batch.Extensions,
                configuration.Batch.CollisionPolicy,
                configuration.Batch.Provenance,
                configuration.Batch.LicenseSpdx,
                _batchDryRun!.ButtonPressed);
            var report = ContentBatchTransferService.Execute(request);
            _batchStatus!.Text = report.Valid
                ? $"✓ {report.Entries.Count} fichier(s) · {report.TotalBytes / 1024d:0.0} Kio · " +
                  $"ajout {report.Added} · remplacé {report.Replaced} · conservé {report.PreservedBoth}"
                : string.Join("\n", report.Diagnostics);
            _batchStatus.AddThemeColorOverride("font_color", new Color(report.Valid ? "83d6ae" : "ff9f88"));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            _batchStatus!.Text = exception.Message;
            _batchStatus.AddThemeColorOverride("font_color", new Color("ff9f88"));
        }
    }

    private void ExportBlueprints()
    {
        try
        {
            var state = _getState?.Invoke() ?? throw new InvalidOperationException("Aucun projet ouvert.");
            var selected = _blueprintList!.GetSelectedItems().Select(index => _blueprints[(int)index].Id).ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("Sélectionnez au moins un blueprint.");
            var result = BlueprintExportPackageBuilder.Build(state.Project, selected, _blueprintOutput!.Text);
            SetStatus(
                $"✓ Pack créé · {result.BlueprintCount} blueprint(s) · {result.DependencyCount} dépendance(s) · {result.ContentSha256[..12]}…",
                false);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void AnalyzeMerge()
    {
        _conflicts!.Clear();
        ConflictCount = 0;
        try
        {
            var selected = _tableList!.GetSelectedItems().FirstOrDefault(-1);
            if (selected < 0 || selected >= _tables.Count) throw new InvalidOperationException("Sélectionnez une table.");
            var baseOpen = ProjectSession.Open(_baseWorkspace!.Text);
            var theirsOpen = ProjectSession.Open(_theirsWorkspace!.Text);
            if (!baseOpen.Succeeded || baseOpen.Session is null || !theirsOpen.Succeeded || theirsOpen.Session is null)
            {
                throw new InvalidOperationException("Les espaces de base et distant doivent être des projets Forge valides.");
            }

            var ours = _tables[selected];
            var result = DataTableThreeWayMerger.Merge(
                baseOpen.Session.Project.Module.Content.DataTables.Single(table => table.Id == ours.Id),
                ours,
                theirsOpen.Session.Project.Module.Content.DataTables.Single(table => table.Id == ours.Id),
                (ContentMergePolicy)_mergePolicy!.Selected);
            ConflictCount = result.Conflicts.Count;
            foreach (var conflict in result.Conflicts)
            {
                _conflicts.AddItem($"Conflit · {conflict.RowId}\n{conflict.Summary}");
                _conflicts.SetItemTooltip(
                    _conflicts.ItemCount - 1,
                    $"BASE {FormatValues(conflict.Base)}\nNOUS {FormatValues(conflict.Ours)}\nDISTANT {FormatValues(conflict.Theirs)}");
            }

            if (result.Conflicts.Count == 0)
            {
                _conflicts.AddItem($"✓ Fusion automatique · {result.Merged.Rows.Count} ligne(s)");
                _conflicts.SetItemSelectable(0, false);
            }

            SetStatus($"Analyse terminée · {result.Conflicts.Count} conflit(s) · {result.Merged.Rows.Count} ligne(s).", false);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException)
        {
            _conflicts.AddItem(exception.Message);
            SetStatus(exception.Message, true);
        }
    }

    private void LoadTable(int index)
    {
        if (index < 0 || index >= _tables.Count) return;
        var table = _tables[index];
        _draftTableId = table.Id;
        _tableSlug!.Text = table.Slug;
        _tableName!.Text = table.DisplayName;
        _tableProvenance!.Text = table.Provenance;
        _tableLicense!.Text = table.LicenseSpdx;
        _tableHash!.Text = table.ContentHash;
        _columns!.Text = string.Join("\n", table.Columns.Select(column =>
            $"{column.Key}|{column.DisplayName}|{column.Kind.ToString().ToLowerInvariant()}|" +
            $"{(column.Required ? "required" : "optional")}|{column.ReferenceTableId}"));
        _rows!.Text = string.Join("\n", table.Rows.Select(row =>
            $"{row.Id}|{string.Join(';', row.Values.Select(pair => $"{pair.Key}={pair.Value}"))}|{row.Provenance}"));
    }

    private void ClearTableDraft()
    {
        _draftTableId = null;
        _tableSlug!.Text = string.Empty;
        _tableName!.Text = string.Empty;
        _tableProvenance!.Text = "project-author";
        _tableLicense!.Text = "UNLICENSED";
        _tableHash!.Text = "unverified";
        _columns!.Text = string.Empty;
        _rows!.Text = string.Empty;
    }

    private void RemoveTable()
    {
        var selected = _tableList!.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _tables.Count) return;
        _tables.RemoveAt(selected);
        RefreshTableList();
        ClearTableDraft();
        InvalidatePreview();
    }

    private void RefreshTableList()
    {
        _tableList!.Clear();
        foreach (var table in _tables)
        {
            _tableList.AddItem($"{table.DisplayName}\n{table.Rows.Count} ligne(s)");
            _tableList.SetItemTooltip(_tableList.ItemCount - 1, $"UUID {table.Id}\nSchéma v{table.SchemaVersion}\n{table.LicenseSpdx}");
        }
    }

    private void InvalidatePreview()
    {
        if (_suppressSignals) return;
        _approved = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        if (_summary is not null) _summary.Text = "Prévisualisation requise après chaque changement.";
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff9f88" : "83d6ae"));
    }

    private static IReadOnlyList<string> Lines(string value) =>
        value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> ParseCsv(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static DataColumnKind ParseColumnKind(string value) => value.Trim().ToLowerInvariant() switch
    {
        "text" or "texte" => DataColumnKind.Text,
        "integer" or "entier" => DataColumnKind.Integer,
        "decimal" or "décimal" => DataColumnKind.Decimal,
        "boolean" or "booléen" => DataColumnKind.Boolean,
        "reference" or "référence" => DataColumnKind.Reference,
        _ => throw new FormatException($"Type de colonne inconnu : '{value}'."),
    };

    private static string FormatValues(DataRowDefinition? row) => row is null
        ? "<supprimée>"
        : string.Join(", ", row.Values.Select(pair => $"{pair.Key}={pair.Value}"));

    private static string FormatCollision(ContentBatchCollisionPolicy policy) => policy switch
    {
        ContentBatchCollisionPolicy.Fail => "Bloquer",
        ContentBatchCollisionPolicy.Skip => "Ignorer",
        ContentBatchCollisionPolicy.Replace => "Remplacer",
        ContentBatchCollisionPolicy.PreserveBoth => "Conserver les deux",
        _ => policy.ToString(),
    };

    private static string FormatMerge(ContentMergePolicy policy) => policy switch
    {
        ContentMergePolicy.Manual => "Résolution manuelle",
        ContentMergePolicy.PreferOurs => "Préférer notre variante",
        ContentMergePolicy.PreferTheirs => "Préférer la variante distante",
        ContentMergePolicy.PreserveBoth => "Conserver les deux",
        _ => policy.ToString(),
    };

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static LineEdit AddField(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static VBoxContainer AddCard(Container parent, Vector2 minimum, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimum,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 12);
        }

        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);
        return content;
    }

    private static StyleBoxFlat Card(string background, string border, int width, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };
}
#endif
