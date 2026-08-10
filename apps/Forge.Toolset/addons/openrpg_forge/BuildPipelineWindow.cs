#if TOOLS
using Forge.Application.Build;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Build;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class BuildPipelineWindow : Window
{
    private readonly SetWorkspaceBuildProfileHandler _profileHandler = new(new SystemClock());
    private readonly Dictionary<string, LineEdit> _layoutEdits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LineEdit> _governanceEdits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _governanceChecks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _collaborationChecks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LineEdit> _collaborationEdits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpinBox> _collaborationLimits = new(StringComparer.Ordinal);
    private Func<ProjectSession?>? _getSession;
    private Action<Window>? _brandWindow;
    private Label? _status;
    private Label? _graphSummary;
    private Label? _impactSummary;
    private Label? _cacheSummary;
    private ItemList? _resources;
    private ItemList? _dependencies;
    private WorkspaceBuildProfileCommandHistory? _profileHistory;
    private string? _profileWorkspace;
    private Label? _profileSummary;
    private Button? _applyProfile;
    private Button? _undoProfile;
    private Button? _redoProfile;
    private readonly Dictionary<string, CheckBox> _policyChecks = new(StringComparer.Ordinal);
    private TabContainer? _profileTabs;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private bool _captureR023;
    private bool _captureR026;
    private bool _captureR036;

    public int ResourceCount { get; private set; }

    public int ScriptCount { get; private set; }

    public int AutomaticDependencyCount { get; private set; }

    public int BuildTaskCount { get; private set; }

    public int BuildMaximumParallelism { get; private set; }

    public void Initialize(Func<ProjectSession?> getSession, Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Scripts et build incrémental · OpenRPG Forge";
        MinSize = new Vector2I(1440, 980);
        Size = new Vector2I(1440, 980);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--build-profile-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r023-build-collaboration-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r026-project-compatibility-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r036-ee-build-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            _capturePath = captureArgument[(captureArgument.IndexOf('=') + 1)..];
            _captureR023 = captureArgument.StartsWith("--r023-build-collaboration-ui-capture=", StringComparison.Ordinal);
            _captureR026 = captureArgument.StartsWith("--r026-project-compatibility-ui-capture=", StringComparison.Ordinal);
            _captureR036 = captureArgument.StartsWith("--r036-ee-build-ui-capture=", StringComparison.Ordinal);
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1.0)
        {
            Open();
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        var marker = _captureR036 ? "OPENRPG_FORGE_R036_EE_BUILD_UI_CAPTURE" :
            _captureR026 ? "OPENRPG_FORGE_R026_PROJECT_COMPATIBILITY_UI_CAPTURE" :
            _captureR023 ? "OPENRPG_FORGE_R023_BUILD_COLLABORATION_UI_CAPTURE" :
                "OPENRPG_FORGE_BUILD_PROFILE_UI_CAPTURE";
        GD.Print($"{marker}_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        LoadProfile();
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1440, 980));
        if (_capturePath is not null && _profileTabs is not null)
            _profileTabs.CurrentTab = _captureR036 ? 0 : _captureR023 || _captureR026 ? 2 : 1;
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var session = _getSession?.Invoke();
        _resources?.Clear();
        _dependencies?.Clear();
        ResourceCount = 0;
        ScriptCount = 0;
        AutomaticDependencyCount = 0;
        if (session is null)
        {
            SetStatus("Aucun projet ouvert.", true);
            return;
        }

        if (_profileHistory is null || !string.Equals(_profileWorkspace, session.RootDirectory, StringComparison.OrdinalIgnoreCase))
            LoadProfile();
        PopulateProfileControls();

        try
        {
            var build = WorkspaceBuildGraphBuilder.Build(
                session.RootDirectory,
                session.Project,
                session.ProjectFilePath);
            var previous = WorkspaceBuildStateStore.TryLoad(session.RootDirectory);
            var plan = WorkspaceIncrementalBuildPlanner.Plan(previous, build.Graph);
            var collaboration = _profileHistory!.State.Profile.Collaboration;
            var references = WorkspaceBuildCollaborationService.BuildReferenceIndex(
                session.RootDirectory, session.Project, session.ProjectFilePath, collaboration);
            var semanticBefore = WorkspaceBuildCollaborationService.LoadSemanticSnapshot(session.RootDirectory);
            var semanticAfter = WorkspaceBuildCollaborationService.CreateSemanticSnapshot(session.Project, build.Graph);
            var semanticDiff = WorkspaceBuildCollaborationService.Diff(semanticBefore, semanticAfter, collaboration);
            ResourceCount = build.Graph.Nodes.Count;
            ScriptCount = build.Graph.Nodes.Count(node => node.Kind == BuildResourceKind.Script);
            AutomaticDependencyCount = build.AutomaticDependencies.Count;
            BuildTaskCount = build.Materialization.TaskCount;
            BuildMaximumParallelism = build.Materialization.MaximumParallelism;
            foreach (var node in build.Graph.Nodes)
            {
                var state = plan.RebuiltResources.Any(candidate => candidate.Id == node.Id)
                    ? "REBUILD"
                    : node.Kind == BuildResourceKind.ExternalPackage ? "EXTERNE" : "CACHE";
                _resources!.AddItem(
                    $"{state,-7}  {FormatKind(node.Kind),-10}  {node.Path} · {node.ContentSha256[..12]} · {node.LicenseSpdx}");
                _resources.SetItemTooltip(
                    _resources.ItemCount - 1,
                    $"UUID {node.Id}\nProvenance {node.Provenance}\n{node.Dependencies.Count} dépendance(s)");
            }

            foreach (var dependency in build.AutomaticDependencies)
            {
                _dependencies!.AddItem($"PACK  {dependency}");
            }

            if (build.AutomaticDependencies.Count == 0)
            {
                _dependencies!.AddItem("Aucun pack externe requis par le contenu utilisé.");
                _dependencies.SetItemSelectable(0, false);
            }

            _graphSummary!.Text =
                $"Graphe v{build.Graph.SchemaVersion} · {build.Graph.Nodes.Count} ressources · " +
                $"{build.Graph.Nodes.Sum(node => node.Dependencies.Count)} liens · " +
                $"{references.Nodes.Sum(node => node.Dependents.Count)} références inverses";
            _impactSummary!.Text =
                $"Prochain export · {plan.RebuiltResources.Count} à recalculer · " +
                $"{plan.ReusedResources.Count} réutilisées · {plan.RemovedPaths.Count} retirées · " +
                $"{semanticDiff.Changes.Count} changement(s) sémantique(s)";
            _cacheSummary!.Text =
                $"ForgeScript · {ScriptCount} source(s) · build {BuildTaskCount} tâche(s), " +
                $"jusqu'à {BuildMaximumParallelism} en parallèle · sortie ordonnée " +
                $"{build.Materialization.OrderFingerprint[..8]}";
            SetStatus("✓ Analyse déterministe prête · le cache reste jetable et hors du module", false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            SetStatus($"Analyse impossible : {exception.Message}", true);
        }
    }

    public bool AnalyzeForSmokeTest()
    {
        Refresh();
        return ResourceCount >= 5 && ScriptCount == 2 && _status?.Text.StartsWith("✓", StringComparison.Ordinal) == true;
    }

    public bool PreviewProfileForSmokeTest()
    {
        PreviewProfile();
        return _applyProfile?.Disabled == false &&
               _profileHistory?.State.Profile.SchemaVersion == WorkspaceBuildProfile.CurrentSchemaVersion &&
               _profileHistory.State.Profile.Governance.SchemaVersion == WorkspaceBuildGovernanceProfile.CurrentSchemaVersion &&
               ProjectCompatibilityProfileValidator.Validate(
                   _profileHistory.State.Profile.Compatibility).Count == 0 &&
               WorkspaceBuildCollaborationProfileValidator.Validate(
                   _profileHistory.State.Profile.Collaboration).Count == 0;
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
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);
        var title = new Label { Text = "SCRIPTS & BUILD INCRÉMENTAL", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Comprendre l’impact avant l’export, sans éditer les fichiers générés.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var metrics = new HBoxContainer();
        metrics.AddThemeConstantOverride("separation", 12);
        root.AddChild(metrics);
        _graphSummary = AddMetric(metrics, "GRAPHE", "Analyse en attente");
        _impactSummary = AddMetric(metrics, "IMPACT", "Analyse en attente");
        _cacheSummary = AddMetric(metrics, "CACHE FORGESCRIPT", "Analyse en attente");

        var profileCard = AddCard(root, expand: true);
        profileCard.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        profileCard.AddChild(Section("PROFIL CANONIQUE DE BUILD"));
        _profileTabs = new TabContainer { CustomMinimumSize = new Vector2(0, 235) };
        profileCard.AddChild(_profileTabs);
        var buildTab = new VBoxContainer { Name = "Build" };
        _profileTabs.AddChild(buildTab);
        var profileBody = new HBoxContainer();
        profileBody.AddThemeConstantOverride("separation", 16);
        buildTab.AddChild(profileBody);
        var layout = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        profileBody.AddChild(layout);
        foreach (var key in new[] { "data", "scripts", "assets", "localization", "tests", "packages", "docs", "cache" })
        {
            layout.AddChild(new Label { Text = key });
            var edit = new LineEdit { CustomMinimumSize = new Vector2(145, 30) };
            edit.TextChanged += _ => InvalidateProfilePreview();
            layout.AddChild(edit);
            _layoutEdits[key] = edit;
        }
        var policies = new GridContainer { Columns = 2, CustomMinimumSize = new Vector2(390, 0) };
        profileBody.AddChild(policies);
        AddPolicy(policies, "git", "Dépôt Git requis");
        AddPolicy(policies, "text", "Documents texte");
        AddPolicy(policies, "generated", "Fichiers générés explicites");
        AddPolicy(policies, "assets", "Assets reproductibles");
        AddPolicy(policies, "ordering", "Ordre stable");
        AddPolicy(policies, "timestamps", "Horodatages normalisés");
        AddPolicy(policies, "locked", "Dépendances verrouillées");
        AddPolicy(policies, "hashes", "Hashes de contenu");
        AddPolicy(policies, "verify", "Vérifier la reproductibilité");
        AddPolicy(policies, "incremental", "Build incrémental");
        AddPolicy(policies, "affected", "Reconstruire l’impact seul");

        var governanceTab = new VBoxContainer { Name = "Gouvernance" };
        governanceTab.AddThemeConstantOverride("separation", 8);
        _profileTabs.AddChild(governanceTab);
        governanceTab.AddChild(new Label
        {
            Text = "Worktrees, CI, cinq fixtures, SemVer Core/Kits/packages et ADRs partagent ce contrat versionné.",
        });
        var governanceBody = new HBoxContainer();
        governanceBody.AddThemeConstantOverride("separation", 16);
        governanceTab.AddChild(governanceBody);
        var governanceFields = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        governanceBody.AddChild(governanceFields);
        AddGovernanceEdit(governanceFields, "branch", "Préfixe branche");
        AddGovernanceEdit(governanceFields, "ownership", "Ownership");
        AddGovernanceEdit(governanceFields, "ci", "Workflow CI");
        AddGovernanceEdit(governanceFields, "fixtures", "Catalogue fixtures");
        AddGovernanceEdit(governanceFields, "core", "Core SemVer");
        AddGovernanceEdit(governanceFields, "rpg", "RPG Kit");
        AddGovernanceEdit(governanceFields, "network", "Network Kit");
        AddGovernanceEdit(governanceFields, "live", "Live Director");
        AddGovernanceEdit(governanceFields, "package", "Package SemVer");
        AddGovernanceEdit(governanceFields, "changelog", "Changelog");
        AddGovernanceEdit(governanceFields, "adr-dir", "Dossier ADR");
        AddGovernanceEdit(governanceFields, "adr-template", "Template ADR");
        var governancePolicies = new GridContainer { Columns = 2, CustomMinimumSize = new Vector2(425, 0) };
        governanceBody.AddChild(governancePolicies);
        AddGovernancePolicy(governancePolicies, "worktree", "Branche/worktree dédié");
        AddGovernancePolicy(governancePolicies, "exclusive", "Ownership exclusif");
        AddGovernancePolicy(governancePolicies, "scopes", "Périmètres déclarés");
        AddGovernancePolicy(governancePolicies, "schemas", "CI valide les schémas");
        AddGovernancePolicy(governancePolicies, "managed", "CI compile C#");
        AddGovernancePolicy(governancePolicies, "native", "CI compile C++");
        AddGovernancePolicy(governancePolicies, "scripts", "CI compile ForgeScript");
        AddGovernancePolicy(governancePolicies, "tests", "CI exécute les tests");
        AddGovernancePolicy(governancePolicies, "pack", "CI package un exemple");
        AddGovernancePolicy(governancePolicies, "licenses", "CI vérifie les licences");
        AddGovernancePolicy(governancePolicies, "adr-tests", "ADRs lient les tests");
        AddGovernancePolicy(governancePolicies, "adr-bench", "ADRs lient les benchmarks");
        AddGovernancePolicy(governancePolicies, "adr-replace", "Remplacement par nouvel ADR");
        var collaborationTab = new VBoxContainer { Name = "Collaboration & compatibilité" };
        collaborationTab.AddThemeConstantOverride("separation", 8);
        _profileTabs.AddChild(collaborationTab);
        collaborationTab.AddChild(new Label
        {
            Text = "Compatibilité runtime, migrations sûres, cache jetable, références et diff partagent un contrat reconstructible.",
        });
        var collaborationBody = new HBoxContainer();
        collaborationBody.AddThemeConstantOverride("separation", 16);
        collaborationTab.AddChild(collaborationBody);
        var collaborationFields = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        collaborationBody.AddChild(collaborationFields);
        AddCollaborationEdit(collaborationFields, "cache-index", "Index cache");
        AddCollaborationLimit(collaborationFields, "cache-max", "Entrées cache", 1, 1_000_000, 1_000);
        AddCollaborationLimit(collaborationFields, "document-max", "Document max (octets)", 1_024, 67_108_864, 1_024);
        AddCollaborationEdit(collaborationFields, "backup-dir", "Sauvegardes migration");
        AddCollaborationLimit(collaborationFields, "backup-max", "Sauvegardes max", 1, 100, 1);
        AddCollaborationEdit(collaborationFields, "runtime-min", "Runtime minimum");
        AddCollaborationEdit(collaborationFields, "project-format", "Format projet");
        AddCollaborationLimit(collaborationFields, "reference-max", "Nœuds références", 10, 1_000_000, 1_000);
        AddCollaborationLimit(collaborationFields, "diff-max", "Entrées diff", 1, 1_000_000, 1_000);
        var collaborationPolicies = new GridContainer { Columns = 1, CustomMinimumSize = new Vector2(410, 0) };
        collaborationBody.AddChild(collaborationPolicies);
        AddCollaborationPolicy(collaborationPolicies, "cache", "Cache adressé · jetable · reconstructible");
        AddCollaborationPolicy(collaborationPolicies, "normalize", "JSON canonique UTF-8/LF · écriture atomique");
        AddCollaborationPolicy(collaborationPolicies, "migrate", "Migration dry-run · sauvegarde · idempotence");
        AddCollaborationPolicy(collaborationPolicies, "automatic", "Migrations automatiques versionnées");
        AddCollaborationPolicy(collaborationPolicies, "required", "Compatibilité requise au runtime");
        AddCollaborationPolicy(collaborationPolicies, "references", "Références avant/arrière · éditeur · CLI · MCP");
        AddCollaborationPolicy(collaborationPolicies, "diff", "Diff zones · graphes · entités · packages · scripts");
        var profileFooter = new HBoxContainer();
        profileFooter.AddThemeConstantOverride("separation", 8);
        profileCard.AddChild(profileFooter);
        _profileSummary = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        profileFooter.AddChild(_profileSummary);
        var previewProfile = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(120, 34) };
        previewProfile.Pressed += PreviewProfile;
        profileFooter.AddChild(previewProfile);
        _applyProfile = new Button { Text = "Appliquer", Disabled = true, CustomMinimumSize = new Vector2(110, 34) };
        _applyProfile.Pressed += ApplyProfile;
        profileFooter.AddChild(_applyProfile);
        _undoProfile = new Button { Text = "Annuler", CustomMinimumSize = new Vector2(95, 34) };
        _undoProfile.Pressed += UndoProfile;
        profileFooter.AddChild(_undoProfile);
        _redoProfile = new Button { Text = "Rétablir", CustomMinimumSize = new Vector2(95, 34) };
        _redoProfile.Pressed += RedoProfile;
        profileFooter.AddChild(_redoProfile);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);
        var resourcesCard = AddCard(body, expand: true);
        resourcesCard.AddChild(Section("RESSOURCES ET IMPACT"));
        _resources = new ItemList
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FixedColumnWidth = 760,
            MaxColumns = 1,
            SameColumnWidth = true,
        };
        resourcesCard.AddChild(_resources);
        var dependenciesCard = AddCard(body, new Vector2(330, 0));
        dependenciesCard.AddChild(Section("DÉPENDANCES AUTOMATIQUES"));
        var dependencyHelp = new Label
        {
            Text = "Seuls les packs réellement référencés rejoignent le manifeste `.orpmod`.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        dependencyHelp.AddThemeColorOverride("font_color", new Color("aa9bc7"));
        dependenciesCard.AddChild(dependencyHelp);
        _dependencies = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        dependenciesCard.AddChild(_dependencies);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        _status = new Label
        {
            Text = "Analyse en attente",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        footer.AddChild(_status);
        var refresh = new Button { Text = "Actualiser", CustomMinimumSize = new Vector2(130, 38) };
        refresh.Pressed += Refresh;
        footer.AddChild(refresh);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 38) };
        close.Pressed += Hide;
        footer.AddChild(close);
        CloseRequested += Hide;
    }

    private static Label AddMetric(Container parent, string title, string value)
    {
        var card = AddCard(parent, expand: true);
        card.AddChild(Section(title));
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        card.AddChild(label);
        return label;
    }

    private void AddPolicy(Container parent, string key, string label)
    {
        var check = new CheckBox { Text = label };
        check.Toggled += _ => InvalidateProfilePreview();
        parent.AddChild(check);
        _policyChecks[key] = check;
    }

    private void AddGovernanceEdit(Container parent, string key, string label)
    {
        parent.AddChild(new Label { Text = label });
        var edit = new LineEdit { CustomMinimumSize = new Vector2(170, 30) };
        edit.TextChanged += value =>
        {
            edit.TooltipText = value;
            InvalidateProfilePreview();
        };
        parent.AddChild(edit);
        _governanceEdits[key] = edit;
    }

    private void AddGovernancePolicy(Container parent, string key, string label)
    {
        var check = new CheckBox { Text = label };
        check.Toggled += _ => InvalidateProfilePreview();
        parent.AddChild(check);
        _governanceChecks[key] = check;
    }

    private void AddCollaborationPolicy(Container parent, string key, string label)
    {
        var check = new CheckBox { Text = label };
        check.Toggled += _ => InvalidateProfilePreview();
        parent.AddChild(check);
        _collaborationChecks[key] = check;
    }

    private void AddCollaborationEdit(Container parent, string key, string label)
    {
        parent.AddChild(new Label { Text = label });
        var edit = new LineEdit { CustomMinimumSize = new Vector2(190, 30) };
        edit.TextChanged += _ => InvalidateProfilePreview();
        parent.AddChild(edit);
        _collaborationEdits[key] = edit;
    }

    private void AddCollaborationLimit(
        Container parent,
        string key,
        string label,
        double minimum,
        double maximum,
        double step)
    {
        parent.AddChild(new Label { Text = label });
        var limit = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Rounded = true,
            CustomMinimumSize = new Vector2(190, 30),
        };
        limit.ValueChanged += _ => InvalidateProfilePreview();
        parent.AddChild(limit);
        _collaborationLimits[key] = limit;
    }

    private void LoadProfile()
    {
        var session = _getSession?.Invoke();
        if (session is null) return;
        var loaded = WorkspaceBuildProfileStore.Load(session.RootDirectory);
        _profileWorkspace = session.RootDirectory;
        _profileHistory = new WorkspaceBuildProfileCommandHistory(loaded.Profile);
        if (!loaded.Succeeded)
            SetStatus($"Profil de build invalide : {loaded.Diagnostics[0].Code} · {loaded.Diagnostics[0].Path}", true);
    }

    private void PopulateProfileControls()
    {
        var profile = _profileHistory?.State.Profile;
        if (profile is null) return;
        foreach (var item in _layoutEdits)
            item.Value.Text = profile.Layout.GetValueOrDefault(item.Key, string.Empty);
        _policyChecks["git"].ButtonPressed = profile.RequireGitRepository;
        _policyChecks["text"].ButtonPressed = profile.TextDocuments;
        _policyChecks["generated"].ButtonPressed = profile.ExplicitGeneratedFiles;
        _policyChecks["assets"].ButtonPressed = profile.ReproducibleAssets;
        _policyChecks["ordering"].ButtonPressed = profile.StableOrdering;
        _policyChecks["timestamps"].ButtonPressed = profile.NormalizedTimestamps;
        _policyChecks["locked"].ButtonPressed = profile.LockedDependencies;
        _policyChecks["hashes"].ButtonPressed = profile.ContentHashes;
        _policyChecks["verify"].ButtonPressed = profile.VerifyReproducibility;
        _policyChecks["incremental"].ButtonPressed = profile.IncrementalBuild;
        _policyChecks["affected"].ButtonPressed = profile.RebuildAffectedOnly;
        var governance = profile.Governance;
        _governanceEdits["branch"].Text = governance.Worktrees.BranchPrefix;
        _governanceEdits["ownership"].Text = governance.Worktrees.OwnershipManifestPath;
        _governanceEdits["ci"].Text = governance.ContinuousIntegration.WorkflowPath;
        _governanceEdits["fixtures"].Text = governance.ReferenceFixtures.CatalogPath;
        _governanceEdits["core"].Text = governance.Versioning.CoreVersion;
        _governanceEdits["rpg"].Text = governance.Versioning.KitVersions.GetValueOrDefault("rpg", string.Empty);
        _governanceEdits["network"].Text = governance.Versioning.KitVersions.GetValueOrDefault("network", string.Empty);
        _governanceEdits["live"].Text = governance.Versioning.KitVersions.GetValueOrDefault("live-director", string.Empty);
        _governanceEdits["package"].Text = governance.Versioning.PackageVersion;
        _governanceEdits["changelog"].Text = governance.Versioning.ChangelogPath;
        _governanceEdits["adr-dir"].Text = governance.ArchitectureDecisions.DirectoryPath;
        _governanceEdits["adr-template"].Text = governance.ArchitectureDecisions.TemplatePath;
        _governanceChecks["worktree"].ButtonPressed = governance.Worktrees.RequireDedicatedBranchOrWorktree;
        _governanceChecks["exclusive"].ButtonPressed = governance.Worktrees.RequireExclusiveOwnership;
        _governanceChecks["scopes"].ButtonPressed = governance.Worktrees.RequireDeclaredFileScopes;
        _governanceChecks["schemas"].ButtonPressed = governance.ContinuousIntegration.ValidateSchemas;
        _governanceChecks["managed"].ButtonPressed = governance.ContinuousIntegration.BuildManagedCode;
        _governanceChecks["native"].ButtonPressed = governance.ContinuousIntegration.BuildNativeCode;
        _governanceChecks["scripts"].ButtonPressed = governance.ContinuousIntegration.CompileForgeScript;
        _governanceChecks["tests"].ButtonPressed = governance.ContinuousIntegration.RunTests;
        _governanceChecks["pack"].ButtonPressed = governance.ContinuousIntegration.PackReferenceModule;
        _governanceChecks["licenses"].ButtonPressed = governance.ContinuousIntegration.VerifyLicenses;
        _governanceChecks["adr-tests"].ButtonPressed = governance.ArchitectureDecisions.RequireTestLinks;
        _governanceChecks["adr-bench"].ButtonPressed = governance.ArchitectureDecisions.RequireBenchmarksForPerformanceDecisions;
        _governanceChecks["adr-replace"].ButtonPressed = governance.ArchitectureDecisions.ReplaceBySupersedingDecision;
        var collaboration = profile.Collaboration;
        var compatibility = profile.Compatibility;
        _collaborationEdits["cache-index"].Text = collaboration.Cache.IndexFileName;
        _collaborationEdits["backup-dir"].Text = collaboration.Migrations.BackupDirectory;
        _collaborationEdits["runtime-min"].Text = compatibility.MinimumRuntimeVersion;
        _collaborationEdits["project-format"].Text = compatibility.ProjectFormatVersion;
        _collaborationLimits["cache-max"].Value = collaboration.Cache.MaximumEntries;
        _collaborationLimits["document-max"].Value = collaboration.Normalization.MaximumDocumentBytes;
        _collaborationLimits["backup-max"].Value = collaboration.Migrations.MaximumBackups;
        _collaborationLimits["reference-max"].Value = collaboration.References.MaximumNodes;
        _collaborationLimits["diff-max"].Value = collaboration.SemanticDiff.MaximumEntries;
        _collaborationChecks["cache"].ButtonPressed = collaboration.Cache.ExplicitDirectory &&
            collaboration.Cache.ContentAddressed && collaboration.Cache.Invalidatable &&
            collaboration.Cache.RebuildableFromSource && collaboration.Cache.NeverCanonical;
        _collaborationChecks["normalize"].ButtonPressed = collaboration.Normalization.CanonicalJson &&
            collaboration.Normalization.Utf8WithoutBom && collaboration.Normalization.LineFeedEndings &&
            collaboration.Normalization.StablePropertyOrder && collaboration.Normalization.StableNumericFormatting &&
            collaboration.Normalization.AtomicWrites;
        _collaborationChecks["migrate"].ButtonPressed = collaboration.Migrations.VersionedSteps &&
            collaboration.Migrations.RequireDryRun && collaboration.Migrations.BackupBeforeApply &&
            collaboration.Migrations.RequireIdempotence && collaboration.Migrations.DowngradeWhenSupported;
        _collaborationChecks["references"].ButtonPressed = collaboration.References.IndexedGraph &&
            collaboration.References.IncludeForwardReferences && collaboration.References.IncludeReverseReferences &&
            collaboration.References.RebuildableFromSource && collaboration.References.EditorAccess &&
            collaboration.References.CliAccess && collaboration.References.McpAccess;
        _collaborationChecks["diff"].ButtonPressed = collaboration.SemanticDiff.Areas &&
            collaboration.SemanticDiff.Graphs && collaboration.SemanticDiff.Entities &&
            collaboration.SemanticDiff.Packages && collaboration.SemanticDiff.Scripts &&
            collaboration.SemanticDiff.TextOutput && collaboration.SemanticDiff.JsonOutput &&
            collaboration.SemanticDiff.StableOrdering;
        _collaborationChecks["automatic"].ButtonPressed = compatibility.AutomaticMigrations;
        _collaborationChecks["required"].ButtonPressed = compatibility.RequiredAtRuntime;
        _profileSummary!.Text = $"UUID {profile.Id:D} · gouvernance {governance.Id:D} · " +
                                $"runtime ≥ {compatibility.MinimumRuntimeVersion} · format {compatibility.ProjectFormatVersion} · " +
                                $"{governance.ReferenceFixtures.Fixtures.Count} fixtures";
        _applyProfile!.Disabled = true;
        UpdateHistoryButtons();
    }

    private WorkspaceBuildProfile DraftProfile()
    {
        var source = _profileHistory?.State.Profile ?? WorkspaceBuildProfile.CreateDefault();
        var paths = _layoutEdits.ToDictionary(item => item.Key, item => item.Value.Text, StringComparer.Ordinal);
        var governanceSource = source.Governance;
        var governance = new WorkspaceBuildGovernanceProfile(
            governanceSource.Id,
            governanceSource.SchemaVersion,
            "openrpg-forge:toolset:p0-r002",
            new(_governanceChecks["worktree"].ButtonPressed, _governanceChecks["exclusive"].ButtonPressed,
                _governanceChecks["scopes"].ButtonPressed, _governanceEdits["branch"].Text,
                _governanceEdits["ownership"].Text, governanceSource.Worktrees.IntegrationChecks),
            new(_governanceEdits["ci"].Text, _governanceChecks["schemas"].ButtonPressed,
                _governanceChecks["managed"].ButtonPressed, _governanceChecks["native"].ButtonPressed,
                _governanceChecks["scripts"].ButtonPressed, _governanceChecks["tests"].ButtonPressed,
                _governanceChecks["pack"].ButtonPressed, _governanceChecks["licenses"].ButtonPressed),
            governanceSource.ReferenceFixtures with { CatalogPath = _governanceEdits["fixtures"].Text },
            governanceSource.Versioning with
            {
                CoreVersion = _governanceEdits["core"].Text,
                KitVersions = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["live-director"] = _governanceEdits["live"].Text,
                    ["network"] = _governanceEdits["network"].Text,
                    ["rpg"] = _governanceEdits["rpg"].Text,
                },
                PackageVersion = _governanceEdits["package"].Text,
                ChangelogPath = _governanceEdits["changelog"].Text,
            },
            governanceSource.ArchitectureDecisions with
            {
                DirectoryPath = _governanceEdits["adr-dir"].Text,
                TemplatePath = _governanceEdits["adr-template"].Text,
                RequireTestLinks = _governanceChecks["adr-tests"].ButtonPressed,
                RequireBenchmarksForPerformanceDecisions = _governanceChecks["adr-bench"].ButtonPressed,
                ReplaceBySupersedingDecision = _governanceChecks["adr-replace"].ButtonPressed,
            });
        var collaborationSource = source.Collaboration;
        var cacheEnabled = _collaborationChecks["cache"].ButtonPressed;
        var normalizeEnabled = _collaborationChecks["normalize"].ButtonPressed;
        var migrationEnabled = _collaborationChecks["migrate"].ButtonPressed;
        var referencesEnabled = _collaborationChecks["references"].ButtonPressed;
        var diffEnabled = _collaborationChecks["diff"].ButtonPressed;
        var collaboration = new WorkspaceBuildCollaborationProfile(
            collaborationSource.Id,
            collaborationSource.SchemaVersion,
            "openrpg-forge:toolset:r023",
            new(cacheEnabled, cacheEnabled, cacheEnabled, cacheEnabled, cacheEnabled,
                _collaborationEdits["cache-index"].Text, (int)_collaborationLimits["cache-max"].Value),
            new(normalizeEnabled, normalizeEnabled, normalizeEnabled, normalizeEnabled, normalizeEnabled,
                normalizeEnabled, (int)_collaborationLimits["document-max"].Value),
            new(migrationEnabled, migrationEnabled, migrationEnabled, migrationEnabled, migrationEnabled,
                _collaborationEdits["backup-dir"].Text, (int)_collaborationLimits["backup-max"].Value),
            new(referencesEnabled, referencesEnabled, referencesEnabled, referencesEnabled, referencesEnabled,
                referencesEnabled, referencesEnabled, (int)_collaborationLimits["reference-max"].Value),
            new(diffEnabled, diffEnabled, diffEnabled, diffEnabled, diffEnabled, diffEnabled, diffEnabled,
                diffEnabled, (int)_collaborationLimits["diff-max"].Value));
        var compatibilitySource = source.Compatibility;
        var compatibility = new ProjectCompatibilityProfile(
            compatibilitySource.Id,
            compatibilitySource.SchemaVersion,
            "openrpg-forge:toolset:r026",
            _collaborationEdits["runtime-min"].Text,
            _collaborationEdits["project-format"].Text,
            _collaborationChecks["automatic"].ButtonPressed,
            _collaborationChecks["required"].ButtonPressed);
        return new WorkspaceBuildProfile(source.Id, source.SchemaVersion, "openrpg-forge:toolset",
            _policyChecks["git"].ButtonPressed, _policyChecks["text"].ButtonPressed,
            _policyChecks["generated"].ButtonPressed, _policyChecks["assets"].ButtonPressed,
            paths, WorkspaceBuildProfile.RequiredCliOperations,
            _policyChecks["ordering"].ButtonPressed, _policyChecks["timestamps"].ButtonPressed,
            _policyChecks["locked"].ButtonPressed, _policyChecks["hashes"].ButtonPressed,
            _policyChecks["verify"].ButtonPressed, _policyChecks["incremental"].ButtonPressed,
            paths.GetValueOrDefault("cache", string.Empty), _policyChecks["affected"].ButtonPressed,
            governance, collaboration, compatibility);
    }

    private void PreviewProfile()
    {
        if (_profileHistory is null) return;
        var result = _profileHandler.Handle(_profileHistory.State, new SetWorkspaceBuildProfileCommand(
            CommandId.From(Guid.NewGuid()), _profileHistory.State.Revision, DraftProfile(), true));
        _applyProfile!.Disabled = !result.Succeeded;
        _profileSummary!.Text = result.Succeeded
            ? "Configuration valide · la même commande canonique sera appliquée et sauvegardée atomiquement."
            : $"{result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
    }

    private void ApplyProfile()
    {
        var session = _getSession?.Invoke();
        if (_profileHistory is null || session is null) return;
        var result = _profileHandler.Handle(_profileHistory.State, new SetWorkspaceBuildProfileCommand(
            CommandId.From(Guid.NewGuid()), _profileHistory.State.Revision, DraftProfile(), false));
        if (!result.Succeeded) { PreviewProfile(); return; }
        var saved = WorkspaceBuildProfileStore.Save(session.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
        {
            _profileSummary!.Text = $"{saved.Diagnostics[0].Code} · {saved.Diagnostics[0].Path}";
            return;
        }
        _profileHistory.Record(result);
        PopulateProfileControls();
        Refresh();
    }

    private void UndoProfile() => RestoreProfile(redo: false);
    private void RedoProfile() => RestoreProfile(redo: true);

    private void RestoreProfile(bool redo)
    {
        var session = _getSession?.Invoke();
        if (_profileHistory is null || session is null || !(redo ? _profileHistory.Redo() : _profileHistory.Undo())) return;
        var saved = WorkspaceBuildProfileStore.Save(session.RootDirectory, _profileHistory.State.Profile);
        if (!saved.Succeeded)
        {
            if (redo) _profileHistory.Undo(); else _profileHistory.Redo();
            _profileSummary!.Text = $"{saved.Diagnostics[0].Code} · {saved.Diagnostics[0].Path}";
            return;
        }
        PopulateProfileControls();
        Refresh();
    }

    private void InvalidateProfilePreview()
    {
        if (_applyProfile is not null) _applyProfile.Disabled = true;
        if (_profileSummary is not null) _profileSummary.Text = "Modifications non prévisualisées.";
    }

    private void UpdateHistoryButtons()
    {
        if (_undoProfile is not null) _undoProfile.Disabled = _profileHistory?.CanUndo != true;
        if (_redoProfile is not null) _redoProfile.Disabled = _profileHistory?.CanRedo != true;
    }

    private static VBoxContainer AddCard(Container parent, Vector2? minimum = null, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimum ?? new Vector2(0, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 14);
        }

        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 9);
        margin.AddChild(content);
        return content;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static StyleBoxFlat Card(string background, string border, int width, int radius)
    {
        var style = new StyleBoxFlat
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
        return style;
    }

    private static string FormatKind(BuildResourceKind kind) => kind switch
    {
        BuildResourceKind.ProjectDocument => "PROJET",
        BuildResourceKind.AreaDocument => "ZONE",
        BuildResourceKind.Script => "SCRIPT",
        BuildResourceKind.Asset => "ASSET",
        BuildResourceKind.ExternalPackage => "PACK",
        BuildResourceKind.ModulePackage => "MODULE",
        _ => kind.ToString().ToUpperInvariant(),
    };

    private void SetStatus(string text, bool error)
    {
        if (_status is null)
        {
            return;
        }

        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "83d6ae"));
    }
}
#endif
