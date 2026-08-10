#if TOOLS
using System.Security.Cryptography;
using Forge.Application.Projects;
using Forge.Domain.Content;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Content;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ContentPackageWindow : Window
{
    private readonly List<ContentPackageDefinition> _packages = [];
    private Func<ProjectState?>? _getState;
    private Func<ContentWorkspaceConfiguration, bool>? _apply;
    private Func<string?>? _getWorkspaceRoot;
    private Action<Window>? _brandWindow;
    private Guid? _draftId;
    private ContentPackageCatalog? _approved;
    private bool _suppress;
    private ItemList? _packageList;
    private OptionButton? _conflictPolicy;
    private LineEdit? _namespace;
    private LineEdit? _version;
    private SpinBox? _priority;
    private LineEdit? _provenance;
    private LineEdit? _license;
    private LineEdit? _hash;
    private LineEdit? _sealedResources;
    private TextEdit? _dependencies;
    private TextEdit? _sources;
    private TextEdit? _assets;
    private ItemList? _diagnostics;
    private Label? _summary;
    private Label? _status;
    private Button? _applyButton;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;

    public int PackageCount => _packages.Count;

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
        Title = "Packages de contenu · OpenRPG Forge";
        MinSize = new Vector2I(1420, 900);
        Size = new Vector2I(1420, 900);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--content-package-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _capturePath = capture["--content-package-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            ConfigureCaptureDemo();
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_CONTENT_PACKAGE_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1420, 900));
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
        LoadCatalog(state.Project.Module.Content.Packages);
    }

    public bool ConfigureForSmokeTest(ContentPackageCatalog catalog)
    {
        LoadCatalog(catalog);
        PreviewCatalog(runCompiler: false);
        return _approved is not null && _applyButton?.Disabled == false;
    }

    public bool ApplyForSmokeTest()
    {
        if (_approved is null) return false;
        ApplyCatalog();
        return _approved is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("0d0a16", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "PACKAGES DE CONTENU VERSIONNÉS" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Déclarer les namespaces, dépendances et sources texte ; prévisualiser la résolution avant de produire le .orpmod.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);
        BuildCatalogCard(AddCard(body, new Vector2(240, 0)));
        BuildManifestCard(AddCard(body, new Vector2(340, 0)));
        BuildSourcesCard(AddCard(body, new Vector2(0, 0), expand: true));

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        var footerText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        footer.AddChild(footerText);
        _summary = new Label { Text = "État vide · aucun package configuré.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        footerText.AddChild(_summary);
        _status = new Label { Text = "Ajoutez un package ou chargez un module existant." };
        footerText.AddChild(_status);
        var preview = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(150, 44) };
        preview.Pressed += () => PreviewCatalog(runCompiler: false);
        footer.AddChild(preview);
        var compile = new Button { Text = "Vérifier & compiler", CustomMinimumSize = new Vector2(180, 44) };
        compile.Pressed += () => PreviewCatalog(runCompiler: true);
        footer.AddChild(compile);
        _applyButton = new Button { Text = "Enregistrer", CustomMinimumSize = new Vector2(160, 44), Disabled = true };
        _applyButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        _applyButton.Pressed += ApplyCatalog;
        footer.AddChild(_applyButton);
    }

    private void BuildCatalogCard(VBoxContainer card)
    {
        card.AddChild(Section("CATALOGUE CANONIQUE"));
        _packageList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        card.AddChild(_packageList);
        var actions = new HBoxContainer();
        card.AddChild(actions);
        var add = new Button { Text = "+ Nouveau", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        add.Pressed += ClearDraft;
        actions.AddChild(add);
        var remove = new Button { Text = "Retirer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        remove.Pressed += RemovePackage;
        actions.AddChild(remove);
        card.AddChild(Section("RÈGLE DE CONFLIT"));
        _conflictPolicy = new OptionButton();
        foreach (var policy in Enum.GetValues<ContentConflictPolicy>()) _conflictPolicy.AddItem(FormatConflict(policy));
        card.AddChild(_conflictPolicy);
        var help = new Label
        {
            Text = "L’ordre est explicite et déterministe. Une ressource scellée ne peut jamais être remplacée.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        help.AddThemeColorOverride("font_color", new Color("9f92b8"));
        card.AddChild(help);
    }

    private void BuildManifestCard(VBoxContainer card)
    {
        card.AddChild(Section("MANIFESTE DU PACKAGE"));
        _namespace = AddField(card, "Namespace", "crypt.encounters");
        _version = AddField(card, "Version sémantique", "1.0.0");
        card.AddChild(new Label { Text = "Priorité d’override" });
        _priority = new SpinBox { MinValue = -10_000, MaxValue = 10_000, Step = 1, Value = 0 };
        card.AddChild(_priority);
        _provenance = AddField(card, "Provenance", "project-author");
        _license = AddField(card, "Licence SPDX", "CC0-1.0");
        _sealedResources = AddField(card, "Ressources scellées", "rules.balance, ui.theme");
        _hash = AddField(card, "SHA-256 du manifeste", "Calculé automatiquement");
        _hash.Editable = false;
        var upsert = new Button { Text = "Ajouter / mettre à jour", CustomMinimumSize = new Vector2(0, 42) };
        upsert.Pressed += UpsertPackage;
        card.AddChild(upsert);
        var hint = new Label
        {
            Text = "Le hash de package est recalculé depuis le manifeste. Pour une source, utilisez « auto » afin de lire le fichier du workspace.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        card.AddChild(hint);
    }

    private void BuildSourcesCard(VBoxContainer card)
    {
        card.AddChild(Section("DÉPENDANCES · une par ligne"));
        card.AddChild(new Label
        {
            Text = "namespace | contrainte SemVer | required/optional/peer | provenance",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _dependencies = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 70),
            PlaceholderText = "forge.core | ^1.0.0 | required | project-author",
        };
        card.AddChild(_dependencies);
        card.AddChild(Section("SOURCES TEXTE · une ressource logique par fichier"));
        card.AddChild(new Label
        {
            Text = "ressource | chemin | json/text/yaml/forge | required/optional | licence | sha256/auto | budget | provenance | aliases | références",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _sources = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 110),
            PlaceholderText = "creature/warden | data/warden.json | json | required | CC0-1.0 | auto | 1048576 | project-author | legacy.core:text/warden |",
        };
        card.AddChild(_sources);
        card.AddChild(Section("ASSETS GOUVERNÉS · binaires référencés"));
        card.AddChild(new Label
        {
            Text = "ressource | chemin | required/optional | licence | sha256/auto | budget | provenance | aliases | références",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _assets = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 80),
            PlaceholderText = "portrait/warden | assets/portraits/warden.png | required | CC0-1.0 | auto | 4194304 | project-author | legacy.core:texture/warden | crypt.encounters:text/creature/warden",
        };
        card.AddChild(_assets);
        card.AddChild(Section("DIAGNOSTICS & RÉSOLUTION"));
        _diagnostics = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _diagnostics.AddItem("État vide · ajoutez un package pour lancer la validation.");
        _diagnostics.SetItemSelectable(0, false);
        card.AddChild(_diagnostics);
    }

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        _packageList!.ItemSelected += index => LoadPackage((int)index);
        foreach (var field in new[] { _namespace, _version, _provenance, _license, _sealedResources })
            field!.TextChanged += _ => Invalidate();
        _dependencies!.TextChanged += Invalidate;
        _sources!.TextChanged += Invalidate;
        _assets!.TextChanged += Invalidate;
        _priority!.ValueChanged += _ => Invalidate();
        _conflictPolicy!.ItemSelected += _ => Invalidate();
    }

    private void LoadCatalog(ContentPackageCatalog catalog)
    {
        _suppress = true;
        _packages.Clear();
        _packages.AddRange(catalog.Packages);
        _conflictPolicy!.Selected = (int)catalog.ConflictPolicy;
        RefreshList();
        if (_packages.Count == 0) ClearDraft();
        else
        {
            _packageList!.Select(0);
            LoadPackage(0);
        }
        _suppress = false;
        Invalidate();
        if (_packages.Count == 0)
        {
            _summary!.Text = "État vide · aucun package configuré.";
            SetStatus("Ajoutez un package ; aucune extension optionnelle ne sera chargée.", false);
        }
    }

    private void UpsertPackage()
    {
        try
        {
            var existing = _draftId is null ? null : _packages.SingleOrDefault(item => item.Id == _draftId);
            var dependencies = ParseDependencies(existing);
            var sources = ParseSources(existing);
            var assets = ParseAssets(existing);
            var draft = new ContentPackageDefinition(_draftId ?? Guid.NewGuid(), 1, _provenance!.Text,
                _namespace!.Text, _version!.Text, _license!.Text, new string('0', 64), (int)_priority!.Value,
                ParseCsv(_sealedResources!.Text), dependencies, sources, assets);
            var package = new ContentPackageDefinition(draft.Id, draft.SchemaVersion, draft.Provenance,
                draft.PackageNamespace, draft.Version, draft.LicenseSpdx,
                ContentPackageCompiler.ComputePackageHash(draft), draft.Priority, draft.SealedResources,
                draft.Dependencies, draft.Sources, draft.Assets);
            var index = _packages.FindIndex(item => item.Id == package.Id);
            if (index >= 0) _packages[index] = package;
            else _packages.Add(package);
            _packages.Sort((left, right) => StringComparer.Ordinal.Compare(left.PackageNamespace, right.PackageNamespace));
            _draftId = package.Id;
            _hash!.Text = package.ContentSha256;
            RefreshList();
            _packageList!.Select(_packages.FindIndex(item => item.Id == package.Id));
            Invalidate();
            SetStatus($"Package « {package.PackageNamespace} » prêt dans le brouillon.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or IOException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private IReadOnlyList<ContentPackageDependency> ParseDependencies(ContentPackageDefinition? existing)
    {
        var ids = existing?.Dependencies.ToDictionary(item => item.PackageNamespace, item => item.Id, StringComparer.Ordinal)
                  ?? new Dictionary<string, Guid>(StringComparer.Ordinal);
        return Lines(_dependencies!.Text).Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length is < 3 or > 4) throw new FormatException($"Dépendance invalide : '{line}'.");
            var kind = parts[2].ToLowerInvariant() switch
            {
                "required" or "requise" => ContentDependencyKind.Required,
                "optional" or "optionnelle" => ContentDependencyKind.Optional,
                "peer" or "paire" => ContentDependencyKind.Peer,
                _ => throw new FormatException($"Type de dépendance inconnu : '{parts[2]}'."),
            };
            return new ContentPackageDependency(ids.GetValueOrDefault(parts[0], Guid.NewGuid()), 1,
                parts.Length == 4 ? parts[3] : "project-author", parts[0], parts[1], kind);
        }).ToArray();
    }

    private IReadOnlyList<ContentPackageSource> ParseSources(ContentPackageDefinition? existing)
    {
        var ids = existing?.Sources.ToDictionary(item => item.RelativePath, item => item.Id, StringComparer.Ordinal)
                  ?? new Dictionary<string, Guid>(StringComparer.Ordinal);
        return Lines(_sources!.Text).Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length is < 7 or > 10) throw new FormatException($"Source invalide : '{line}'.");
            var kind = parts[2].ToLowerInvariant() switch
            {
                "json" => ContentSourceKind.Json,
                "text" or "texte" => ContentSourceKind.Text,
                "yaml" or "yml" => ContentSourceKind.Yaml,
                "forge" or "forgescript" or "as" => ContentSourceKind.ForgeScript,
                _ => throw new FormatException($"Format source inconnu : '{parts[2]}'."),
            };
            var required = parts[3].Equals("required", StringComparison.OrdinalIgnoreCase) ||
                           parts[3].Equals("requise", StringComparison.OrdinalIgnoreCase);
            var hash = parts[5];
            if (hash.Equals("auto", StringComparison.OrdinalIgnoreCase)) hash = HashWorkspaceFile(parts[1]);
            if (!long.TryParse(parts[6], out var budget)) throw new FormatException($"Budget invalide : '{parts[6]}'.");
            return new ContentPackageSource(ids.GetValueOrDefault(parts[1], Guid.NewGuid()), 1,
                parts.Length >= 8 ? parts[7] : "project-author", parts[0], parts[1], kind, required,
                parts[4], hash, budget, parts.Length >= 9 ? ParseCsv(parts[8]) : [],
                parts.Length >= 10 ? ParseCsv(parts[9]) : []);
        }).ToArray();
    }

    private IReadOnlyList<ContentPackageAsset> ParseAssets(ContentPackageDefinition? existing)
    {
        var ids = existing?.Assets.ToDictionary(item => item.RelativePath, item => item.Id, StringComparer.Ordinal)
                  ?? new Dictionary<string, Guid>(StringComparer.Ordinal);
        return Lines(_assets!.Text).Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length is < 6 or > 9) throw new FormatException($"Asset invalide : '{line}'.");
            var required = parts[2].Equals("required", StringComparison.OrdinalIgnoreCase) ||
                           parts[2].Equals("requis", StringComparison.OrdinalIgnoreCase);
            var hash = parts[4];
            if (hash.Equals("auto", StringComparison.OrdinalIgnoreCase)) hash = HashWorkspaceFile(parts[1]);
            if (!long.TryParse(parts[5], out var budget)) throw new FormatException($"Budget invalide : '{parts[5]}'.");
            return new ContentPackageAsset(ids.GetValueOrDefault(parts[1], Guid.NewGuid()), 1,
                parts.Length >= 7 ? parts[6] : "project-author", parts[0], parts[1], required,
                parts[3], hash, budget, parts.Length >= 8 ? ParseCsv(parts[7]) : [],
                parts.Length >= 9 ? ParseCsv(parts[8]) : []);
        }).ToArray();
    }

    private string HashWorkspaceFile(string relativePath)
    {
        var root = _getWorkspaceRoot?.Invoke() ?? throw new InvalidOperationException("Aucun workspace ouvert pour calculer le hash.");
        var normalized = ContentPackageDefinition.NormalizePath(relativePath);
        var contained = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(contained, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(contained, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new FileNotFoundException($"Source introuvable : {normalized}.", path);
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private void PreviewCatalog(bool runCompiler)
    {
        _diagnostics!.Clear();
        try
        {
            var state = _getState?.Invoke() ?? throw new InvalidOperationException("Aucun projet ouvert.");
            var catalog = new ContentPackageCatalog(state.Project.Module.Id.Value, 1, "creator-command",
                (ContentConflictPolicy)_conflictPolicy!.Selected, _packages);
            var diagnostics = ContentPackageCatalogValidator.Validate(catalog, state.Project.Module.Id.Value);
            if (diagnostics.Count > 0)
            {
                ShowDiagnostics(diagnostics);
                Invalidate();
                SetStatus($"{diagnostics.Count} erreur(s) à corriger avant le build.", true);
                return;
            }
            var resolution = ContentPackageResolver.Resolve(catalog);
            if (runCompiler)
            {
                var root = _getWorkspaceRoot?.Invoke() ?? throw new InvalidOperationException("Aucun workspace ouvert.");
                var compilation = ContentPackageCompiler.Compile(root, state.Project.Module.Id.Value, catalog);
                if (!compilation.Success)
                {
                    ShowDiagnostics(compilation.Diagnostics);
                    Invalidate();
                    SetStatus($"Compilation refusée · {compilation.Diagnostics.Count} diagnostic(s).", true);
                    return;
                }
                _diagnostics.AddItem($"✓ Compilation prête · {compilation.GeneratedArtifacts.Count} artefact(s) généré(s) en mémoire");
            }
            else
            {
                _diagnostics.AddItem($"✓ Résolution déterministe · {resolution.LockEntries.Count} verrou(s) · {resolution.Resources.Count} ressource(s)");
            }
            _approved = catalog;
            _applyButton!.Disabled = false;
            _summary!.Text = $"{catalog.Packages.Count} package(s) · {resolution.Resources.Count} ressource(s) · " +
                             $"conflits : {FormatConflict(catalog.ConflictPolicy)}";
            SetStatus(runCompiler ? "✓ Validation, sources, scripts et lockfile prêts pour forge pack." :
                "✓ Catalogue valide ; utilisez Vérifier & compiler pour contrôler les fichiers.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException)
        {
            Invalidate();
            _diagnostics.AddItem(exception.Message);
            SetStatus(exception.Message, true);
        }
    }

    private void ApplyCatalog()
    {
        var catalog = _approved;
        var state = _getState?.Invoke();
        if (catalog is null || state is null) return;
        var current = state.Project.Module.Content;
        var configuration = new ContentWorkspaceConfiguration(current.Id, current.Batch, current.DataTables,
            current.MergePolicy, "creator-command", current.SchemaVersion, catalog);
        if (_apply?.Invoke(configuration) != true)
        {
            SetStatus("La commande canonique a refusé le catalogue.", true);
            return;
        }
        _approved = null;
        _applyButton!.Disabled = true;
        SetStatus("Catalogue enregistré · undo/redo disponibles dans la barre Forge.", false);
    }

    private void LoadPackage(int index)
    {
        if (index < 0 || index >= _packages.Count) return;
        _suppress = true;
        var package = _packages[index];
        _draftId = package.Id;
        _namespace!.Text = package.PackageNamespace;
        _version!.Text = package.Version;
        _priority!.Value = package.Priority;
        _provenance!.Text = package.Provenance;
        _license!.Text = package.LicenseSpdx;
        _hash!.Text = package.ContentSha256;
        _sealedResources!.Text = string.Join(", ", package.SealedResources);
        _dependencies!.Text = string.Join("\n", package.Dependencies.Select(item =>
            $"{item.PackageNamespace} | {item.VersionConstraint} | {item.Kind.ToString().ToLowerInvariant()} | {item.Provenance}"));
        _sources!.Text = string.Join("\n", package.Sources.Select(item =>
            $"{item.LogicalResourceId} | {item.RelativePath} | {FormatSource(item.Kind)} | " +
            $"{(item.Required ? "required" : "optional")} | {item.LicenseSpdx} | {item.ContentSha256} | " +
            $"{item.PlatformByteBudget} | {item.Provenance} | {string.Join(", ", item.Aliases ?? [])} | " +
            $"{string.Join(", ", item.References ?? [])}"));
        _assets!.Text = string.Join("\n", package.Assets.Select(item =>
            $"{item.LogicalResourceId} | {item.RelativePath} | {(item.Required ? "required" : "optional")} | " +
            $"{item.LicenseSpdx} | {item.ContentSha256} | {item.PlatformByteBudget} | {item.Provenance} | " +
            $"{string.Join(", ", item.Aliases ?? [])} | {string.Join(", ", item.References ?? [])}"));
        _suppress = false;
        Invalidate();
    }

    private void ClearDraft()
    {
        _draftId = null;
        _namespace!.Text = string.Empty;
        _version!.Text = "1.0.0";
        _priority!.Value = 0;
        _provenance!.Text = "project-author";
        _license!.Text = "CC0-1.0";
        _hash!.Text = string.Empty;
        _sealedResources!.Text = string.Empty;
        _dependencies!.Text = string.Empty;
        _sources!.Text = string.Empty;
        _assets!.Text = string.Empty;
        Invalidate();
    }

    private void RemovePackage()
    {
        var selected = _packageList!.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _packages.Count) return;
        _packages.RemoveAt(selected);
        RefreshList();
        ClearDraft();
        Invalidate();
    }

    private void RefreshList()
    {
        _packageList!.Clear();
        foreach (var package in _packages)
        {
            _packageList.AddItem($"{package.PackageNamespace} · v{package.Version} · priorité {package.Priority}");
            _packageList.SetItemTooltip(_packageList.ItemCount - 1,
                $"UUID {package.Id:D}\n{package.Sources.Count} source(s) · {package.Assets.Count} asset(s) · " +
                $"{package.Dependencies.Count} dépendance(s)\n{package.LicenseSpdx}");
        }
    }

    private void ShowDiagnostics(IReadOnlyList<Forge.Domain.Validation.DomainDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            _diagnostics!.AddItem($"{diagnostic.Code}\n{diagnostic.Message}");
            _diagnostics.SetItemTooltip(_diagnostics.ItemCount - 1, diagnostic.Path);
        }
    }

    private void Invalidate()
    {
        if (_suppress) return;
        _approved = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        if (_summary is not null) _summary.Text = "Prévisualisation requise après chaque changement.";
    }

    private void ConfigureCaptureDemo()
    {
        var source = new ContentPackageSource(Guid.Parse("c0160000-0000-4000-8000-000000000103"), 1,
            "creator-demo", "creature/ash-warden", "data/creatures/ash-warden.json", ContentSourceKind.Json,
            true, "CC0-1.0", new string('a', 64), 1_048_576, ["legacy.core:text/ash-warden"], []);
        var dependency = new ContentPackageDependency(Guid.Parse("c0160000-0000-4000-8000-000000000104"), 1,
            "creator-demo", "forge.core", "^1.0.0", ContentDependencyKind.Optional);
        var asset = new ContentPackageAsset(Guid.Parse("c0160000-0000-4000-8000-000000000105"), 1,
            "creator-demo", "portrait/ash-warden", "assets/portraits/ash-warden.png", false,
            "CC0-1.0", new string('b', 64), 4_194_304, [], ["crypt.encounters:text/creature/ash-warden"]);
        var draft = new ContentPackageDefinition(Guid.Parse("c0160000-0000-4000-8000-000000000102"), 1,
            "creator-demo", "crypt.encounters", "1.2.0", "CC0-1.0", new string('0', 64), 80,
            ["creature/ash-warden"], [dependency], [source], [asset]);
        var package = new ContentPackageDefinition(draft.Id, 1, draft.Provenance, draft.PackageNamespace,
            draft.Version, draft.LicenseSpdx, ContentPackageCompiler.ComputePackageHash(draft), draft.Priority,
            draft.SealedResources, draft.Dependencies, draft.Sources, draft.Assets);
        var state = _getState?.Invoke();
        if (state is null) return;
        LoadCatalog(new ContentPackageCatalog(state.Project.Module.Id.Value, 1, "creator-demo",
            ContentConflictPolicy.HighestPriority, [package]));
        PreviewCatalog(runCompiler: false);
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

    private static string FormatConflict(ContentConflictPolicy policy) => policy switch
    {
        ContentConflictPolicy.Error => "Bloquer les conflits",
        ContentConflictPolicy.HighestPriority => "Priorité la plus haute",
        ContentConflictPolicy.NamespaceOrder => "Ordre des namespaces",
        _ => policy.ToString(),
    };

    private static string FormatSource(ContentSourceKind kind) => kind switch
    {
        ContentSourceKind.ForgeScript => "forge",
        _ => kind.ToString().ToLowerInvariant(),
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
            margin.AddThemeConstantOverride(side, 12);
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
