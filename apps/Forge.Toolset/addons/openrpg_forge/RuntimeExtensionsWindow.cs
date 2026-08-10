#if TOOLS
using Forge.Application.Projects;
using Forge.Application.Validation;
using Forge.Domain.RuntimeExtensions;
using Godot;

namespace Forge.Toolset.Editor;

public partial class RuntimeExtensionsWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<ModuleRuntimeExtensionProfile, bool>? _apply;
    private Action<Window>? _brand;
    private ModuleRuntimeExtensionProfile? _profile;
    private TextEdit? _rules;
    private TextEdit? _uiDocuments;
    private TextEdit? _widgets;
    private TextEdit? _events;
    private TextEdit? _json;
    private LineEdit? _components;
    private SpinBox? _maxReferences;
    private SpinBox? _maxInventory;
    private SpinBox? _maxBlackboard;
    private CheckBox? _restoreReferences;
    private TextEdit? _modernTooling;
    private TextEdit? _qualityAcceptance;
    private TextEdit? _operationalReadiness;
    private TabContainer? _sections;
    private Label? _healthSummary;
    private Label? _summary;
    private Label? _status;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;

    internal string Summary => _profile is null ? "unavailable" :
        $"rules={_profile.RuleSchemas.Count};ui={_profile.UiDocuments.Count};json={_profile.JsonDocuments.Count};" +
        $"components={_profile.SnapshotPolicy.Components.Count};toolkit={_profile.ModernTooling.UiToolkit.ToolkitVersion};" +
        $"tasks={_profile.OperationalReadiness.TaskManifests.Count};sync={_profile.OperationalReadiness.ContentSynchronization.Resumable};" +
        $"qa-budgets={_profile.ModernTooling.QualityAssurance.PlatformBudgets.Count};qa-profiler={_profile.ModernTooling.QualityAssurance.Profiler.OneClickCapture};" +
        $"qa-acceptance={_profile.ModernTooling.QualityAssurance.AcceptanceGates.RequiredGates.Count};" +
        $"post-launch={_profile.PostLaunch.SchemaVersion};shards={_profile.PostLaunch.Network.Shards.Count};soak={_profile.PostLaunch.Production.SoakTests.Enabled}";

    public void Initialize(Func<ProjectState?> state, Func<ModuleRuntimeExtensionProfile, bool> apply,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Extensions runtime & UI · OpenRPG Forge";
        MinSize = new Vector2I(1280, 820);
        Size = new Vector2I(1480, 900);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        Build();
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--qa-acceptance-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            _capturePath = captureArgument["--qa-acceptance-ui-capture=".Length..];
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
            _sections!.CurrentTab = 3;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_QA_ACCEPTANCE_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        var state = _state?.Invoke();
        if (state is null) { _status!.Text = "Aucun projet ouvert."; return; }
        _profile = state.Project.Module.RuntimePolicies.Extensions;
        Populate();
        Exclusive = true;
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool ConfigureForSmokeTest(ModuleRuntimeExtensionProfile profile)
    {
        _profile = profile;
        Populate();
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 9); margin.AddChild(root);
        var title = new Label { Text = "RÈGLES DATA-DRIVEN, UI SCRIPTABLE & ÉTAT", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Éditez les documents canoniques par lignes tabulées. Prévisualisation, validation, sauvegarde et undo utilisent la même commande de domaine.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new Label(); _summary.AddThemeColorOverride("font_color", new Color("c7b3dc")); root.AddChild(_summary);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(scroll);
        _sections = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 650),
        };
        scroll.AddChild(_sections);

        var data = Card(_sections, "RÈGLES & JSON");
        data.AddChild(Hint("Règles : type ⇥ clé ⇥ requis ⇥ schéma JSON ⇥ valeurs JSON"));
        _rules = Editor(210); data.AddChild(_rules);
        data.AddChild(Hint("JSON runtime : clé ⇥ mutable ⇥ octets ⇥ profondeur ⇥ API ⇥ schéma ⇥ valeur"));
        _json = Editor(210); data.AddChild(_json);

        var ui = Card(_sections, "UI DÉCLARATIVE & ÉVÉNEMENTS");
        ui.AddChild(Hint("Documents : clé ⇥ titre ⇥ thème ⇥ requis"));
        _uiDocuments = Editor(120); ui.AddChild(_uiDocuments);
        ui.AddChild(Hint("Widgets : document ⇥ clé ⇥ type ⇥ parent ⇥ texte ⇥ binding ⇥ interactif ⇥ focus ⇥ permission"));
        _widgets = Editor(190); ui.AddChild(_widgets);
        ui.AddChild(Hint("Événements : document ⇥ widget ⇥ type ⇥ commande ⇥ contexte ⇥ permission"));
        _events = Editor(150); ui.AddChild(_events);

        var state = Card(_sections, "SNAPSHOTS & QUALITÉ");
        state.AddChild(Hint("Composants conservés (séparés par des virgules)"));
        _components = new LineEdit(); state.AddChild(_components);
        _maxReferences = Spin(0, 4096, 256, 1); state.AddChild(Row("Références max.", _maxReferences));
        _maxInventory = Spin(0, 4096, 512, 1); state.AddChild(Row("Inventaire max.", _maxInventory));
        _maxBlackboard = Spin(64, 1_048_576, 65_536, 64); state.AddChild(Row("Blackboard octets", _maxBlackboard));
        _restoreReferences = new CheckBox { Text = "Restaurer les références résolues", ButtonPressed = true };
        state.AddChild(_restoreReferences);
        state.AddChild(Hint("Politiques modernes : runtime, QA, diagnostics, fixtures hostiles, couverture Aurora et gates (lignes tabulées)"));
        _modernTooling = Editor(360); state.AddChild(_modernTooling);
        state.AddChild(Hint("Les chemins hôte restent inaccessibles. Les JSON sont validés et bornés avant le runtime."));

        var acceptance = Card(_sections, "QA ACCEPTATION");
        acceptance.AddChild(Hint("Une ligne par politique. Les listes utilisent des virgules ; chaque valeur est validée avant l’application."));
        _qualityAcceptance = Editor(430); acceptance.AddChild(_qualityAcceptance);
        acceptance.AddChild(Hint("Diagnostics : profil, catégories, sévérités, correctifs guidés."));
        acceptance.AddChild(Hint("Fixtures : catalogue versionné, sept familles hostiles et limites bornées."));
        acceptance.AddChild(Hint("Couverture : tracker, statuts, 717 fonctions et preuve obligatoire pour Tested."));
        acceptance.AddChild(Hint("Gates : matrice G0–G13, tests, documentation et preuve par fonction."));

        var readiness = Card(_sections, "SANTÉ, REPRISE & SYNCHRONISATION");
        _healthSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _healthSummary.AddThemeColorOverride("font_color", new Color("79d5ad")); readiness.AddChild(_healthSummary);
        readiness.AddChild(Hint("Lignes : task, crash, health, recovery et sync. Les listes utilisent des virgules et '-' signifie vide."));
        _operationalReadiness = Editor(520); readiness.AddChild(_operationalReadiness);
        readiness.AddChild(Hint("Les tâches bornent les agents ; les secrets sont assainis ; les fichiers corrompus sont mis en quarantaine ; le cache reste sous contrôle utilisateur."));

        var footer = new HBoxContainer();
        _status = new Label { Text = "Prêt · aucune mutation.", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd")); footer.AddChild(_status);
        var apply = new Button { Text = "Prévisualiser et appliquer", CustomMinimumSize = new Vector2(230, 40) };
        apply.Pressed += () => Save(); footer.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide; footer.AddChild(close); root.AddChild(footer);
    }

    private void Populate()
    {
        if (_profile is null) return;
        _rules!.Text = string.Join("\n", _profile.RuleSchemas.Select(item =>
            Join(item.Kind, item.Key, item.Required, item.JsonSchema, item.DefaultsJson)));
        _json!.Text = string.Join("\n", _profile.JsonDocuments.Select(item =>
            Join(item.Key, item.Mutable, item.MaxBytes, item.MaxDepth, item.ForgeScriptApiVersion,
                item.JsonSchema, item.InitialValue)));
        _uiDocuments!.Text = string.Join("\n", _profile.UiDocuments.Select(item =>
            Join(item.Key, item.Title, item.ThemeKey, item.Required)));
        _widgets!.Text = string.Join("\n", _profile.UiDocuments.SelectMany(document => document.Widgets.Select(item =>
            Join(document.Key, item.Key, item.Kind, item.ParentKey ?? "-", item.Text, item.BindingKey ?? "-",
                item.Interactive, item.FocusOrder, item.Permission))));
        _events!.Text = string.Join("\n", _profile.UiDocuments.SelectMany(document => document.EventBindings.Select(item =>
            Join(document.Key, item.WidgetKey, item.Event, item.Command, item.InputContext, item.Permission))));
        _components!.Text = string.Join(", ", _profile.SnapshotPolicy.Components);
        _maxReferences!.Value = _profile.SnapshotPolicy.MaxReferences;
        _maxInventory!.Value = _profile.SnapshotPolicy.MaxInventoryItems;
        _maxBlackboard!.Value = _profile.SnapshotPolicy.MaxBlackboardBytes;
        _restoreReferences!.ButtonPressed = _profile.SnapshotPolicy.RestoreReferences;
        var modern = _profile.ModernTooling;
        var toolingRows = new List<string>
        {
            Join("copy", modern.RuntimeCopy.MaxCopiesPerTransaction, modern.RuntimeCopy.RequireServerAuthority,
                modern.RuntimeCopy.CopyOwnership, modern.RuntimeCopy.ReferenceMode, string.Join(',', modern.RuntimeCopy.StateComponents)),
            Join("aoe", string.Join(',', modern.AreaEffectIndicator.Shapes), modern.AreaEffectIndicator.MaxRange,
                modern.AreaEffectIndicator.MaxHeightDelta, modern.AreaEffectIndicator.EvaluateCover,
                modern.AreaEffectIndicator.RequireServerConfirmation, modern.AreaEffectIndicator.ColorMode,
                modern.AreaEffectIndicator.ValidColor, modern.AreaEffectIndicator.InvalidColor),
            Join("nui", modern.UiToolkit.ToolkitVersion, modern.UiToolkit.ResponsiveLayout, modern.UiToolkit.Forms,
                modern.UiToolkit.Lists, modern.UiToolkit.Canvas, modern.UiToolkit.Accessibility,
                modern.UiToolkit.Localization, modern.UiToolkit.DeveloperInspector),
            Join("compiler", modern.Compiler.Incremental, modern.Compiler.StableDiagnostics,
                modern.Compiler.PerformanceTelemetry, modern.Compiler.MaxParallelism, modern.Compiler.MaxSourceBytes,
                string.Join(',', modern.Compiler.ConformanceSuites)),
            Join("docs", modern.Documentation.OutputDirectory, string.Join(',', modern.Documentation.Catalogs),
                modern.Documentation.IncludeMcpResources),
            Join("network", modern.QualityAssurance.Network.ClientCount,
                string.Join(',', modern.QualityAssurance.Network.LatencyTicks),
                string.Join(',', modern.QualityAssurance.Network.LossPercentages),
                modern.QualityAssurance.Network.TestReconnect, modern.QualityAssurance.Network.TestContentMismatch,
                modern.QualityAssurance.Network.TestAuthorityAttacks),
            Join("visual", string.Join(',', modern.QualityAssurance.Visual.Resolutions),
                string.Join(',', modern.QualityAssurance.Visual.CameraFixtures),
                string.Join(',', modern.QualityAssurance.Visual.QualityTiers),
                modern.QualityAssurance.Visual.PixelTolerance.ToString(System.Globalization.CultureInfo.InvariantCulture),
                modern.QualityAssurance.Visual.ReviewDirectory),
            Join("benchmark", string.Join(',', modern.QualityAssurance.Benchmarks.Scenarios),
                modern.QualityAssurance.Benchmarks.Iterations, modern.QualityAssurance.Benchmarks.SampleFrames),
        };
        toolingRows.AddRange(modern.QualityAssurance.PlatformBudgets.Select(budget => Join("budget", budget.Platform,
            budget.MaximumFrameMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            budget.MaximumMemoryMegabytes, budget.MaximumTextureMegabytes, budget.MaximumDrawCalls,
            budget.MaximumDynamicLights,
            budget.MaximumScriptMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            budget.MaximumNetworkKilobitsPerSecond,
            budget.WarningThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture), budget.GateBuild)));
        toolingRows.Add(Join("profiler", string.Join(',', modern.QualityAssurance.Profiler.Surfaces),
            modern.QualityAssurance.Profiler.MaximumCaptureSeconds,
            modern.QualityAssurance.Profiler.IncludeScriptSpans,
            modern.QualityAssurance.Profiler.IncludeDatabaseSpans,
            modern.QualityAssurance.Profiler.IncludeNetworkSpans,
            modern.QualityAssurance.Profiler.OneClickCapture,
            modern.QualityAssurance.Profiler.OutputDirectory));
        var accessibility = modern.QualityAssurance.Accessibility;
        toolingRows.Add(Join("accessibility", accessibility.EditorKeyboardOnly,
            accessibility.EditorScreenReaderLabels, accessibility.EditorColorIndependentStatus,
            accessibility.EditorShortcutReference,
            accessibility.MinimumContrastRatio.ToString(System.Globalization.CultureInfo.InvariantCulture),
            accessibility.EditorTextScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
            accessibility.RuntimeSubtitles, accessibility.RuntimeCaptions,
            accessibility.RuntimeTextScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
            accessibility.RuntimeHighContrast, accessibility.RuntimeColorVisionMode,
            accessibility.RuntimeInputAssists, accessibility.RuntimeReducedMotion,
            accessibility.RuntimeDifficultyAssists));
        var fuzz = modern.QualityAssurance.SecurityFuzz;
        toolingRows.Add(Join("fuzz", fuzz.Seed, fuzz.CasesPerBoundary, fuzz.MaximumInputBytes,
            fuzz.TimeBudgetMilliseconds, string.Join(',', fuzz.Boundaries), fuzz.RequireNoCrash,
            fuzz.RequireNoPathEscape));
        var diagnostics = modern.QualityAssurance.ClickableDiagnostics;
        var malicious = modern.QualityAssurance.MaliciousFixtures;
        var coverage = modern.QualityAssurance.AuroraParityCoverage;
        var gates = modern.QualityAssurance.AcceptanceGates;
        _modernTooling!.Text = string.Join("\n", toolingRows);
        _qualityAcceptance!.Text = string.Join("\n", new[]
        {
            Join("diagnostics", diagnostics.ActiveProfile, string.Join(',', diagnostics.Categories),
                string.Join(',', diagnostics.Severities), diagnostics.EnableQuickFixes),
            Join("malicious", malicious.CatalogPath, string.Join(',', malicious.Families),
                malicious.MaximumArrayEntries, malicious.RejectInjectionText, malicious.RejectInvalidAssets),
            Join("coverage", coverage.TrackerPath, string.Join(',', coverage.AllowedStatuses),
                coverage.ExpectedFeatureCount, coverage.RequireEvidenceForTested),
            Join("gates", gates.GateMatrixPath, string.Join(',', gates.RequiredGates),
                gates.RequireLinkedTests, gates.RequireLinkedDocumentation, gates.RequireFeatureEvidence),
        });
        var readiness = _profile.OperationalReadiness;
        _operationalReadiness!.Text = string.Join("\n", readiness.TaskManifests.Select(task =>
                Join("task", task.Key, task.Goal, List(task.ScopePaths), List(task.ForbiddenPaths),
                    List(task.AcceptanceTests), task.Owner,
                    List(task.Dependencies.Select(dependency => readiness.TaskManifests
                        .FirstOrDefault(candidate => candidate.Id == dependency)?.Key ?? dependency.ToString("D")))))
            .Concat(new[]
            {
                Join("crash", readiness.CrashArtifacts.CaptureSanitizedLogs, readiness.CrashArtifacts.DumpAdapter,
                    readiness.CrashArtifacts.MaxLogLines, readiness.CrashArtifacts.MaxArtifactBytes,
                    readiness.CrashArtifacts.IncludeVersion, readiness.CrashArtifacts.IncludePackageFingerprint,
                    readiness.CrashArtifacts.IncludeReproductionId, List(readiness.CrashArtifacts.SensitiveKeys)),
                Join("health", List(readiness.ProjectHealth.Domains), readiness.ProjectHealth.IncludeTestStatus,
                    readiness.ProjectHealth.IncludePerformance, readiness.ProjectHealth.IncludeLicenses,
                    readiness.ProjectHealth.IncludeMigrations, readiness.ProjectHealth.IncludeCoverage,
                    readiness.ProjectHealth.FailBuildWhenUnhealthy),
                Join("recovery", readiness.Recovery.AutosaveIntervalSeconds, readiness.Recovery.RetainedSnapshots,
                    readiness.Recovery.OfferRecoveryOnOpen, readiness.Recovery.QuarantineCorruptFiles,
                    readiness.Recovery.CompareBeforeRestore),
                Join("sync", readiness.ContentSynchronization.Resumable,
                    readiness.ContentSynchronization.DeduplicateByHash,
                    readiness.ContentSynchronization.VerifyContent,
                    readiness.ContentSynchronization.RequireTrustedSigner,
                    readiness.ContentSynchronization.StorageDirectory,
                    readiness.ContentSynchronization.MaximumCacheBytes,
                    List(readiness.ContentSynchronization.AllowedLicenses),
                    List(readiness.ContentSynchronization.TrustedSignerFingerprints)),
            }));
        var projectState = _state?.Invoke();
        if (projectState is not null)
        {
            var report = ProjectHealthReportService.Evaluate(projectState.Project, DateTimeOffset.UtcNow);
            _healthSummary!.Text = $"Santé {report.Status} · " + string.Join(" · ", report.Domains.Select(domain =>
                $"{domain.Domain}:{domain.Status} ({domain.Passed}/{domain.Warning}/{domain.Failed})"));
        }
        else _healthSummary!.Text = "Santé indisponible · aucun projet ouvert.";
        _summary!.Text = $"Schéma v{_profile.SchemaVersion} · {Summary} · API ForgeScript 1.0";
        _status!.Text = "Prêt · documents canoniques chargés.";
    }

    private bool Save()
    {
        if (_profile is null) return false;
        try
        {
            var rules = ParseRules();
            var json = ParseJson();
            var ui = ParseUi();
            var components = _components!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var source = _profile.SnapshotPolicy;
            var snapshot = new EntityStateSnapshotPolicy(source.Id, 1, components,
                (int)_maxReferences!.Value, (int)_maxInventory!.Value, (int)_maxBlackboard!.Value,
                _restoreReferences!.ButtonPressed, "forge-ui");
            var profile = new ModuleRuntimeExtensionProfile(_profile.Id, 1, rules, ui, json, snapshot, "forge-ui",
                ParseModernTooling(), ParseOperationalReadiness(), _profile.PostLaunch);
            var succeeded = _apply?.Invoke(profile) == true;
            _status!.Text = succeeded ? "✓ Extensions validées, persistantes et annulables." :
                "✕ Application refusée · consultez les diagnostics.";
            if (succeeded) { _profile = profile; Populate(); }
            return succeeded;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or System.Text.Json.JsonException)
        {
            _status!.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    private ModernRuntimeToolingProfile ParseModernTooling()
    {
        var lines = Lines(_modernTooling!).Concat(Lines(_qualityAcceptance!)).ToArray();
        var rows = lines.Where(parts => Key(parts[0]) != "budget")
            .ToDictionary(parts => Key(parts[0]), StringComparer.Ordinal);
        var source = _profile!.ModernTooling;
        string[] Row(string key, int count)
        {
            if (!rows.TryGetValue(key, out var row)) throw new FormatException($"Politique '{key}' absente.");
            Require(row, count, key); return row;
        }
        var copy = Row("copy", 6); var aoe = Row("aoe", 9); var nui = Row("nui", 9);
        var compiler = Row("compiler", 7); var docs = Row("docs", 4);
        var network = Row("network", 7); var visual = Row("visual", 6);
        var benchmark = Row("benchmark", 4); var profiler = Row("profiler", 8);
        var accessibility = Row("accessibility", 15); var fuzz = Row("fuzz", 8);
        var diagnostics = Row("diagnostics", 5); var malicious = Row("malicious", 6);
        var coverage = Row("coverage", 5); var gates = Row("gates", 6);
        var budgetRows = lines.Where(parts => Key(parts[0]) == "budget").ToArray();
        if (budgetRows.Length == 0) throw new FormatException("Au moins un budget de plateforme est requis.");
        foreach (var budget in budgetRows) Require(budget, 11, "budget");
        var qualitySource = source.QualityAssurance;
        var budgetIds = qualitySource.PlatformBudgets.ToDictionary(item => item.Platform, item => item.Id,
            StringComparer.Ordinal);
        var quality = new QualityAssuranceProfile(qualitySource.Id, 1,
            new NetworkTestSuiteProfile(qualitySource.Network.Id, 1, Int(network[1]),
                Csv(network[2]).Select(Int).ToArray(), Csv(network[3]).Select(Int).ToArray(),
                Bool(network[4]), Bool(network[5]), Bool(network[6]), "forge-ui"),
            new VisualRegressionProfile(qualitySource.Visual.Id, 1, Csv(visual[1]), Csv(visual[2]),
                Csv(visual[3]).Select(EnumValue<Forge.Domain.Areas.RenderQualityTier>).ToArray(),
                Double(visual[4]), visual[5], "forge-ui"),
            new AreaBenchmarkSuiteProfile(qualitySource.Benchmarks.Id, 1,
                Csv(benchmark[1]).Select(EnumValue<AreaBenchmarkKind>).ToArray(), Int(benchmark[2]),
                Int(benchmark[3]), "forge-ui"),
            budgetRows.Select(budget => new PlatformPerformanceBudget(
                budgetIds.GetValueOrDefault(Key(budget[1]), Guid.NewGuid()), 1, budget[1], Double(budget[2]),
                Int(budget[3]), Int(budget[4]), Int(budget[5]), Int(budget[6]), Double(budget[7]),
                Int(budget[8]), Double(budget[9]), Bool(budget[10]), "forge-ui")).ToArray(),
            new IntegratedProfilerProfile(qualitySource.Profiler.Id, 1,
                Csv(profiler[1]).Select(EnumValue<QualityTraceSurface>).ToArray(), Int(profiler[2]),
                Bool(profiler[3]), Bool(profiler[4]), Bool(profiler[5]), Bool(profiler[6]), profiler[7],
                "forge-ui"), "forge-ui",
            new AccessibilityQualityProfile(qualitySource.Accessibility.Id, 1,
                Bool(accessibility[1]), Bool(accessibility[2]), Bool(accessibility[3]), Bool(accessibility[4]),
                Double(accessibility[5]), Double(accessibility[6]), Bool(accessibility[7]),
                Bool(accessibility[8]), Double(accessibility[9]), Bool(accessibility[10]),
                EnumValue<RuntimeColorVisionMode>(accessibility[11]), Bool(accessibility[12]),
                Bool(accessibility[13]), Bool(accessibility[14]), "forge-ui"),
            new SecurityFuzzProfile(qualitySource.SecurityFuzz.Id, 1, Int(fuzz[1]), Int(fuzz[2]),
                Int(fuzz[3]), Int(fuzz[4]), Csv(fuzz[5]), Bool(fuzz[6]), Bool(fuzz[7]), "forge-ui"),
            new ClickableDiagnosticPolicy(qualitySource.ClickableDiagnostics.Id, 1, diagnostics[1],
                Csv(diagnostics[2]), Csv(diagnostics[3]), Bool(diagnostics[4]), "forge-ui"),
            new MaliciousModuleFixturePolicy(qualitySource.MaliciousFixtures.Id, 1, malicious[1],
                Csv(malicious[2]), Int(malicious[3]), Bool(malicious[4]), Bool(malicious[5]), "forge-ui"),
            new AuroraParityCoveragePolicy(qualitySource.AuroraParityCoverage.Id, 1, coverage[1],
                Csv(coverage[2]), Int(coverage[3]), Bool(coverage[4]), "forge-ui"),
            new AcceptanceGatePolicy(qualitySource.AcceptanceGates.Id, 1, gates[1], Csv(gates[2]),
                Bool(gates[3]), Bool(gates[4]), Bool(gates[5]), "forge-ui"));
        return new ModernRuntimeToolingProfile(source.Id, 1,
            new RuntimeObjectCopyPolicy(source.RuntimeCopy.Id, 1, Int(copy[1]), Bool(copy[2]), Bool(copy[3]),
                EnumValue<RuntimeReferenceCopyMode>(copy[4]), Csv(copy[5]), "forge-ui"),
            new AreaEffectIndicatorProfile(source.AreaEffectIndicator.Id, 1,
                Csv(aoe[1]).Select(EnumValue<AreaEffectShape>).ToArray(), Float(aoe[2]), Float(aoe[3]),
                Bool(aoe[4]), Bool(aoe[5]), EnumValue<TargetIndicatorColorMode>(aoe[6]), aoe[7], aoe[8], "forge-ui"),
            new ScriptableUiToolkitProfile(source.UiToolkit.Id, 1, nui[1], Bool(nui[2]), Bool(nui[3]),
                Bool(nui[4]), Bool(nui[5]), Bool(nui[6]), Bool(nui[7]), Bool(nui[8]), "forge-ui"),
            new ForgeScriptCompilerProfile(source.Compiler.Id, 1, Bool(compiler[1]), Bool(compiler[2]),
                Bool(compiler[3]), Int(compiler[4]), Int(compiler[5]), Csv(compiler[6]), "forge-ui"),
            new GeneratedDocumentationProfile(source.Documentation.Id, 1, docs[1], Csv(docs[2]),
                Bool(docs[3]), "forge-ui"), "forge-ui", quality);
    }

    private OperationalReadinessProfile ParseOperationalReadiness()
    {
        var rows = Lines(_operationalReadiness!).ToArray();
        var source = _profile!.OperationalReadiness;
        var taskRows = rows.Where(parts => Key(parts[0]) == "task").ToArray();
        foreach (var parts in taskRows) Require(parts, 8, "task");
        var existingIds = source.TaskManifests.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        var taskIds = taskRows.ToDictionary(parts => Key(parts[1]), parts =>
            existingIds.GetValueOrDefault(Key(parts[1]), Guid.NewGuid()), StringComparer.Ordinal);
        var tasks = taskRows.Select(parts =>
        {
            var dependencies = CsvOptional(parts[7]).Select(value =>
                taskIds.TryGetValue(Key(value), out var id) ? id : Guid.Parse(value)).ToArray();
            return new AgentTaskManifest(taskIds[Key(parts[1])], 1, parts[1], parts[2],
                CsvOptional(parts[3]), CsvOptional(parts[4]), CsvOptional(parts[5]), parts[6], dependencies,
                "forge-ui");
        }).ToArray();
        string[] Row(string key, int count)
        {
            var matches = rows.Where(parts => Key(parts[0]) == key).ToArray();
            if (matches.Length != 1) throw new FormatException($"Politique '{key}' absente ou dupliquée.");
            Require(matches[0], count, key); return matches[0];
        }
        var crash = Row("crash", 9); var health = Row("health", 8);
        var recovery = Row("recovery", 6); var sync = Row("sync", 9);
        return new OperationalReadinessProfile(source.Id, 1, tasks,
            new CrashArtifactPolicy(source.CrashArtifacts.Id, 1, Bool(crash[1]),
                EnumValue<CrashDumpAdapterKind>(crash[2]), Int(crash[3]), Long(crash[4]), Bool(crash[5]),
                Bool(crash[6]), Bool(crash[7]), CsvOptional(crash[8]), "forge-ui"),
            new ProjectHealthPolicy(source.ProjectHealth.Id, 1, Csv(health[1]), Bool(health[2]),
                Bool(health[3]), Bool(health[4]), Bool(health[5]), Bool(health[6]), Bool(health[7]), "forge-ui"),
            new CrashRecoveryPolicy(source.Recovery.Id, 1, Int(recovery[1]), Int(recovery[2]),
                Bool(recovery[3]), Bool(recovery[4]), Bool(recovery[5]), "forge-ui"),
            new ContentSynchronizationPolicy(source.ContentSynchronization.Id, 1, Bool(sync[1]),
                Bool(sync[2]), Bool(sync[3]), Bool(sync[4]), sync[5], Long(sync[6]), Csv(sync[7]),
                CsvOptional(sync[8]), "forge-ui"), "forge-ui");
    }

    private IReadOnlyList<DataDrivenRuleSchema> ParseRules()
    {
        var ids = _profile!.RuleSchemas.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        return Lines(_rules!).Select(parts =>
        {
            Require(parts, 5, "règle");
            return new DataDrivenRuleSchema(ids.GetValueOrDefault(Key(parts[1]), Guid.NewGuid()), 1, parts[1],
                EnumValue<RuleSchemaKind>(parts[0]), parts[3], parts[4], Bool(parts[2]), "forge-ui");
        }).ToArray();
    }

    private IReadOnlyList<RuntimeJsonDocumentDefinition> ParseJson()
    {
        var ids = _profile!.JsonDocuments.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        return Lines(_json!).Select(parts =>
        {
            Require(parts, 7, "JSON runtime");
            return new RuntimeJsonDocumentDefinition(ids.GetValueOrDefault(Key(parts[0]), Guid.NewGuid()), 1,
                parts[0], parts[5], parts[6], Int(parts[2]), Int(parts[3]), Bool(parts[1]), parts[4], "forge-ui");
        }).ToArray();
    }

    private IReadOnlyList<ScriptableUiDocument> ParseUi()
    {
        var documentIds = _profile!.UiDocuments.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        var widgetIds = _profile.UiDocuments.SelectMany(document => document.Widgets.Select(widget =>
            (Key: $"{document.Key}:{widget.Key}", widget.Id))).ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        var eventIds = _profile.UiDocuments.SelectMany(document => document.EventBindings.Select(binding =>
            (Key: $"{document.Key}:{binding.WidgetKey}:{binding.Event}", binding.Id)))
            .ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var widgets = Lines(_widgets!).Select(parts =>
        {
            Require(parts, 9, "widget");
            var documentKey = Key(parts[0]); var widgetKey = Key(parts[1]);
            return (Document: documentKey, Widget: new ScriptableUiWidget(
                widgetIds.GetValueOrDefault($"{documentKey}:{widgetKey}", Guid.NewGuid()), widgetKey,
                EnumValue<ScriptableUiWidgetKind>(parts[2]), Optional(parts[3]), parts[4], Optional(parts[5]),
                Bool(parts[6]), Int(parts[7]), parts[8]));
        }).ToArray();
        var events = Lines(_events!).Select(parts =>
        {
            Require(parts, 6, "événement UI");
            var documentKey = Key(parts[0]); var widgetKey = Key(parts[1]);
            var kind = EnumValue<ScriptableUiEventKind>(parts[2]);
            return (Document: documentKey, Binding: new ScriptableUiEventBinding(
                eventIds.GetValueOrDefault($"{documentKey}:{widgetKey}:{kind}", Guid.NewGuid()), widgetKey,
                kind, parts[3], "1.0", parts[4], parts[5]));
        }).ToArray();
        return Lines(_uiDocuments!).Select(parts =>
        {
            Require(parts, 4, "document UI");
            var key = Key(parts[0]);
            return new ScriptableUiDocument(documentIds.GetValueOrDefault(key, Guid.NewGuid()), 1, key,
                parts[1], parts[2], Bool(parts[3]), widgets.Where(item => item.Document == key).Select(item => item.Widget).ToArray(),
                events.Where(item => item.Document == key).Select(item => item.Binding).ToArray(), "forge-ui");
        }).ToArray();
    }

    private static IEnumerable<string[]> Lines(TextEdit editor) => editor.Text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => !line.StartsWith('#'))
        .Select(line => line.Split('\t').Select(item => item.Trim()).ToArray());
    private static void Require(string[] parts, int count, string kind)
    { if (parts.Length != count) throw new FormatException($"Ligne {kind} invalide : {parts.Length}/{count} colonnes."); }
    private static T EnumValue<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : throw new FormatException($"Valeur {typeof(T).Name} invalide : {value}");
    private static bool Bool(string value) => value.Trim().ToLowerInvariant() switch
    { "true" or "oui" or "yes" or "1" => true, "false" or "non" or "no" or "0" => false, _ => throw new FormatException($"Booléen invalide : {value}") };
    private static int Int(string value) => int.TryParse(value, out var parsed) ? parsed : throw new FormatException($"Entier invalide : {value}");
    private static long Long(string value) => long.TryParse(value, out var parsed) ? parsed : throw new FormatException($"Entier long invalide : {value}");
    private static float Float(string value) => float.TryParse(value, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : throw new FormatException($"Nombre invalide : {value}");
    private static double Double(string value) => double.TryParse(value, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : throw new FormatException($"Nombre invalide : {value}");
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static IReadOnlyList<string> CsvOptional(string value) => value is "-" or "" ? [] : Csv(value);
    private static string Key(string value) => value.Trim().ToLowerInvariant();
    private static string? Optional(string value) => value is "-" or "" ? null : value;
    private static string Join(params object?[] values) => string.Join('\t', values.Select(value => value?.ToString() ?? "-"));
    private static string List(IEnumerable<string> values)
    { var materialized = values.ToArray(); return materialized.Length == 0 ? "-" : string.Join(',', materialized); }
    private static TextEdit Editor(float height) => new() { CustomMinimumSize = new Vector2(420, height), WrapMode = TextEdit.LineWrappingMode.None };
    private static Label Hint(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("bcaacb") };
    private static SpinBox Spin(double min, double max, double value, double step) => new() { MinValue = min, MaxValue = max, Value = value, Step = step };
    private static HBoxContainer Row(string label, Control control)
    { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) }); control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(control); return row; }
    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer
        {
            Name = title,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Style()); parent.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) }; box.AddThemeConstantOverride("separation", 6); panel.AddChild(box);
        var heading = new Label { Text = title }; heading.AddThemeColorOverride("font_color", new Color("ffb06f")); box.AddChild(heading); return box;
    }
    private static StyleBoxFlat Style()
    { var style = new StyleBoxFlat { BgColor = new Color("171221"), BorderColor = new Color("513a6d") }; style.SetBorderWidthAll(1); style.SetCornerRadiusAll(10); style.ContentMarginLeft = style.ContentMarginRight = 12; style.ContentMarginTop = style.ContentMarginBottom = 10; return style; }
}
#endif
