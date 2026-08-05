#if TOOLS
using Forge.Application.Quality;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Quality;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class QualityLabWindow : Window
{
    private readonly WorkspaceQualityLabService _service = new(new NativeForgeScriptRuntime());
    private readonly WorkspacePerformanceLabService _performanceService = new();
    private readonly WorkspaceGovernanceLabService _governanceService = new(new NativeForgeScriptRuntime());
    private Func<ProjectDefinition?>? _project;
    private Func<string?>? _workspaceRoot;
    private Action<Window>? _brand;
    private Label? _summary;
    private Label? _status;
    private RichTextLabel? _localization;
    private RichTextLabel? _scripts;
    private RichTextLabel? _dialogues;
    private RichTextLabel? _quests;
    private RichTextLabel? _encounters;
    private RichTextLabel? _pseudoLocalization;
    private RichTextLabel? _network;
    private RichTextLabel? _visual;
    private RichTextLabel? _benchmarks;
    private RichTextLabel? _budgets;
    private RichTextLabel? _profiler;
    private RichTextLabel? _editorAccessibility;
    private RichTextLabel? _runtimeAccessibility;
    private RichTextLabel? _securityFuzz;
    private RichTextLabel? _dependencies;
    private WorkspaceQualityLabReport? _report;
    private WorkspacePerformanceLabReport? _performanceReport;
    private WorkspaceGovernanceLabReport? _governanceReport;

    internal WorkspaceQualityLabReport? Report => _report;
    internal WorkspacePerformanceLabReport? PerformanceReport => _performanceReport;
    internal WorkspaceGovernanceLabReport? GovernanceReport => _governanceReport;

    public void Initialize(
        Func<ProjectDefinition?> project,
        Func<string?> workspaceRoot,
        Action<Window> brand)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Laboratoire qualité · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = new Vector2I(1380, 860);
        Transient = true;
        Visible = false;
        Build();
    }

    public void Open()
    {
        Run(writePseudoLocalizationSnapshot: true);
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool RunForSmokeTest()
    {
        Run(writePseudoLocalizationSnapshot: false);
        return _report?.Passed == true && _performanceReport?.Passed == true && _governanceReport?.Passed == true;
    }

    private void Build()
    {
        var background = new PanelContainer();
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0d0918"),
            BorderColor = new Color("553a76"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        AddChild(background);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var title = new Label
        {
            Text = "LABORATOIRE QUALITÉ DU MODULE",
            ThemeTypeVariation = "HeaderSmall",
        };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Une même passe reproductible vérifie les textes, les scripts, les récits et les rencontres avant le playtest ou l’export.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new Label { Text = "Aucune analyse exécutée.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        root.AddChild(_summary);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);
        var grid = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(grid);

        _localization = Card(grid, "LOCALISATION", "Couverture, placeholders, pluriels et débordements");
        _scripts = Card(grid, "FORGESCRIPT", "Compilation, quotas, monde simulé et événements");
        _dialogues = Card(grid, "DIALOGUES", "Branches, conditions et actions");
        _quests = Card(grid, "QUÊTES", "Événements, transitions et reprise de sauvegarde");
        _encounters = Card(grid, "RENCONTRES", "Profils, seeds, navigation et nettoyage");
        _pseudoLocalization = Card(grid, "PSEUDO-LOCALISATION", "Aperçu de l’expansion et preuve visuelle SVG");
        _network = Card(grid, "RÉSEAU HOSTILE", "Latence, perte, reconnexion, contenu et autorité");
        _visual = Card(grid, "RÉGRESSION VISUELLE", "Résolutions, caméras, niveaux qualité et tolérance pixel");
        _benchmarks = Card(grid, "BENCHMARKS 3D", "Intérieur, extérieur et stress · charge, frame, mémoire, nav");
        _budgets = Card(grid, "BUDGETS PLATEFORMES", "Frame, mémoire, textures, draws, lumières, scripts et réseau");
        _profiler = Card(grid, "PROFILAGE INTÉGRÉ", "Éditeur, runtime et serveur · script, données et réseau");
        _editorAccessibility = Card(grid, "ACCESSIBILITÉ ÉDITEUR", "Clavier, libellés, texte, contraste et raccourcis");
        _runtimeAccessibility = Card(grid, "ACCESSIBILITÉ RUNTIME", "Sous-titres, contraste, daltonisme, entrées et difficulté");
        _securityFuzz = Card(grid, "FUZZING SÉCURITÉ", "JSON, archives, scripts, réseau, images, modèles et chemins");
        _dependencies = Card(grid, "GOUVERNANCE", "Versions, licences, responsables, risques, revues et retrait");

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        _status = new Label
        {
            Text = "Prêt · aucune mutation du module.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        footer.AddChild(_status);
        var run = new Button { Text = "Relancer les tests", CustomMinimumSize = new Vector2(180, 40) };
        run.Pressed += () => Run(writePseudoLocalizationSnapshot: true);
        footer.AddChild(run);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide;
        footer.AddChild(close);
        root.AddChild(footer);
    }

    private static RichTextLabel Card(Control parent, string title, string subtitle)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(360, 245),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("171123"),
            BorderColor = new Color("4c3467"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 14);
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        margin.AddChild(content);
        var heading = new Label { Text = title, ThemeTypeVariation = "HeaderSmall" };
        heading.AddThemeColorOverride("font_color", new Color("ffad73"));
        content.AddChild(heading);
        var hint = new Label { Text = subtitle, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        hint.AddThemeColorOverride("font_color", new Color("a996c2"));
        content.AddChild(hint);
        var details = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(320, 155),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Text = "En attente d’analyse.",
        };
        content.AddChild(details);
        return details;
    }

    private void Run(bool writePseudoLocalizationSnapshot)
    {
        var project = _project?.Invoke();
        var workspaceRoot = _workspaceRoot?.Invoke();
        if (project is null || string.IsNullOrWhiteSpace(workspaceRoot))
        {
            _report = null;
            _performanceReport = null;
            _governanceReport = null;
            _summary!.Text = "Aucun projet Forge ouvert.";
            SetStatus("⚠ Ouvrez un projet avant de lancer le laboratoire.", new Color("e8c36a"));
            ClearCards();
            return;
        }

        try
        {
            SetStatus("Analyse en cours…", new Color("cbb9e5"));
            var defaults = WorkspaceQualityLabOptions.Default;
            var options = defaults with { WritePseudoLocalizationSnapshot = writePseudoLocalizationSnapshot };
            _report = _service.Run(workspaceRoot, project, options);
            _performanceReport = _performanceService.Run(workspaceRoot, project,
                new WorkspacePerformanceLabOptions(writePseudoLocalizationSnapshot));
            _governanceReport = _governanceService.Run(workspaceRoot, project,
                new WorkspaceGovernanceLabOptions(writePseudoLocalizationSnapshot));
            Render(_report, _performanceReport, _governanceReport);
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or
            FileNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _report = null;
            _performanceReport = null;
            _governanceReport = null;
            _summary!.Text = "Le laboratoire n’a pas pu produire de rapport.";
            SetStatus($"✕ {exception.Message}", new Color("ff756d"));
            ClearCards();
            GD.PrintErr($"OPENRPG_FORGE_QUALITY_LAB_FAILED {exception.Message}");
        }
    }

    private void Render(WorkspaceQualityLabReport report, WorkspacePerformanceLabReport performance,
        WorkspaceGovernanceLabReport governance)
    {
        var warnings = report.Localization.Issues.Count(issue => issue.Severity == LocalizationQualitySeverity.Warning);
        var errors = report.Localization.Issues.Count(issue => issue.Severity == LocalizationQualitySeverity.Error);
        var allPassed = report.Passed && performance.Passed && governance.Passed;
        _summary!.Text = $"{Symbol(allPassed)} Rapports {Short(report.Fingerprint)}/{Short(performance.Fingerprint)}/{Short(governance.Fingerprint)} · " +
            $"{report.Localization.Coverage.Count} langue(s) · {report.Scripts.Tests.Count} script(s) · " +
            $"{report.Dialogues.Count} dialogue(s) · {report.Quests.Count} quête(s) · {report.Encounters.TotalRuns} simulation(s) · " +
            $"{performance.Network.Scenarios.Count} réseau · {performance.VisualReviews.Cases.Count} visuel · " +
            $"{performance.Benchmarks.Measurements.Count} benchmark(s)";

        _localization!.Text = string.Join("\n", report.Localization.Coverage.Select(locale =>
            $"{Symbol(locale.Ratio >= 0.999)} [b]{Escape(locale.Locale)}[/b] · {locale.Covered}/{locale.Total} ({locale.Ratio:P0})")) +
            $"\n\n{Symbol(errors == 0)} {errors} erreur(s) structurelle(s)\n{Symbol(warnings == 0)} {warnings} avertissement(s)";
        _scripts!.Text = $"{Symbol(report.Scripts.Passed)} [b]{report.Scripts.Compiled}/{report.Scripts.Tests.Count}[/b] compilé(s)\n" +
            $"{Symbol(report.Scripts.Executed == report.Scripts.Tests.Count)} {report.Scripts.Executed} exécuté(s) dans le sandbox\n" +
            $"{Symbol(report.Scripts.Coverage >= 0.999)} couverture événements {report.Scripts.Coverage:P0}\n\n" +
            "Quotas CPU, événements autorisés et appels au monde simulé contrôlés.";

        var coveredDialogueNodes = report.Dialogues.Sum(item => item.CoveredNodes);
        var totalDialogueNodes = report.Dialogues.Sum(item => item.TotalNodes);
        var coveredDialogueChoices = report.Dialogues.Sum(item => item.CoveredChoices);
        var totalDialogueChoices = report.Dialogues.Sum(item => item.TotalChoices);
        _dialogues!.Text = $"{Symbol(report.Dialogues.All(item => item.Passed))} [b]{report.Dialogues.Count}[/b] graphe(s)\n" +
            $"{Symbol(coveredDialogueNodes == totalDialogueNodes)} nœuds {coveredDialogueNodes}/{totalDialogueNodes}\n" +
            $"{Symbol(coveredDialogueChoices == totalDialogueChoices)} choix {coveredDialogueChoices}/{totalDialogueChoices}\n\n" +
            "Conditions, actions et chemins terminaux énumérés avec fixtures déterministes.";

        var coveredQuestStates = report.Quests.Sum(item => item.CoveredStates);
        var totalQuestStates = report.Quests.Sum(item => item.TotalStates);
        var coveredTransitions = report.Quests.Sum(item => item.CoveredTransitions);
        var totalTransitions = report.Quests.Sum(item => item.TotalTransitions);
        var saveResume = report.Quests.SelectMany(item => item.Paths).All(path => path.SaveResumeEqual);
        _quests!.Text = $"{Symbol(report.Quests.All(item => item.Passed))} [b]{report.Quests.Count}[/b] machine(s) d’état\n" +
            $"{Symbol(coveredQuestStates == totalQuestStates)} états {coveredQuestStates}/{totalQuestStates}\n" +
            $"{Symbol(coveredTransitions == totalTransitions)} transitions {coveredTransitions}/{totalTransitions}\n" +
            $"{Symbol(saveResume)} sauvegarde/reprise identique\n\nPortées joueur, groupe et monde exercées.";

        var creatureCount = report.Encounters.Areas.Sum(area => area.CreatureCount);
        var reachableCount = report.Encounters.Areas.Sum(area => area.ReachableCreatures);
        var navPassed = report.Encounters.Areas.SelectMany(area => area.Runs).All(run => run.NavigationPassed);
        var cleanupPassed = report.Encounters.Areas.SelectMany(area => area.Runs).All(run => run.CleanupPassed);
        _encounters!.Text = $"{Symbol(report.Encounters.Passed)} [b]{report.Encounters.TotalRuns}[/b] simulation(s)\n" +
            $"{Symbol(reachableCount == creatureCount)} créatures atteignables {reachableCount}/{creatureCount}\n" +
            $"{Symbol(navPassed)} navigation validée\n{Symbol(cleanupPassed)} nettoyage sans résidu\n\n" +
            "Profils solo, groupe et vétéran · seeds 17, 42 et 2026.";

        var pseudoRows = report.Localization.PseudoSnapshots.Take(5).Select(row =>
            $"{Symbol(!row.OverflowRisk)} [b]{Escape(row.MessageKey)}[/b]\n   {Escape(Trim(row.Text, 58))}");
        _pseudoLocalization!.Text = string.Join("\n", pseudoRows) +
            $"\n\nPreuve : {Escape(report.PseudoLocalizationSnapshotPath ?? "aperçu mémoire (smoke test)")}";

        _network!.Text = $"{Symbol(performance.Network.Passed)} [b]{performance.Network.Scenarios.Count}[/b] combinaison(s) latence/perte\n" +
            $"{Symbol(performance.Network.Scenarios.All(item => item.Converged))} convergence multi-client\n" +
            $"{Symbol(performance.Network.ReconnectPassed)} reconnexion contrôlée\n" +
            $"{Symbol(performance.Network.ContentMismatchPassed)} contenu incompatible refusé\n" +
            $"{Symbol(performance.Network.AuthorityAttackPassed)} attaques d’autorité refusées";

        _visual!.Text = $"{Symbol(performance.VisualReviews.Cases.Count > 0)} [b]{performance.VisualReviews.Cases.Count}[/b] fixture(s)\n" +
            $"Résolutions : {Escape(string.Join(", ", projectQuality().Visual.Resolutions))}\n" +
            $"Caméras : {Escape(string.Join(", ", projectQuality().Visual.CameraFixtures))}\n" +
            $"Tolérance pixel : {projectQuality().Visual.PixelTolerance:P1}\n\n" +
            $"Manifeste : {Escape(performance.VisualManifestPath ?? "plan mémoire (smoke test)")}";

        var kinds = performance.Benchmarks.Measurements.Select(item => item.Kind).Distinct().Order().ToArray();
        _benchmarks!.Text = $"{Symbol(performance.Benchmarks.Passed)} [b]{performance.Benchmarks.Measurements.Count}[/b] mesure(s)\n" +
            $"Scénarios : {Escape(string.Join(", ", kinds))}\n" +
            $"Frame max. : {performance.Benchmarks.Measurements.Max(item => item.EstimatedFrameMilliseconds):F2} ms\n" +
            $"Mémoire max. : {performance.Benchmarks.Measurements.Max(item => item.EstimatedMemoryMegabytes):F1} Mo\n" +
            $"Navigation mesurée : {performance.Benchmarks.Measurements.Max(item => item.NavigationRebuildMilliseconds):F2} ms";

        var failedBudgets = performance.Benchmarks.Budgets.Count(item => item.Status == PerformanceBudgetStatus.Failed);
        var warningBudgets = performance.Benchmarks.Budgets.Count(item => item.Status == PerformanceBudgetStatus.Warning);
        _budgets!.Text = $"{Symbol(failedBudgets == 0)} [b]{performance.Benchmarks.Budgets.Count}[/b] évaluation(s)\n" +
            $"{Symbol(failedBudgets == 0)} {failedBudgets} blocage(s)\n" +
            $"{Symbol(warningBudgets == 0)} {warningBudgets} avertissement(s)\n\n" +
            string.Join("\n", performance.Benchmarks.Budgets.GroupBy(item => item.Platform)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{Escape(group.Key)} · {group.Max(item => item.Status)}"));

        var surfaces = performance.TraceSpans.Select(item => item.Surface).Distinct().Order().ToArray();
        var categories = performance.TraceSpans.Select(item => item.Category).Distinct().Order(StringComparer.Ordinal).ToArray();
        _profiler!.Text = $"{Symbol(performance.TraceSpans.All(item => !item.Failed))} [b]{performance.TraceSpans.Count}[/b] span(s)\n" +
            $"Surfaces : {Escape(string.Join(", ", surfaces))}\n" +
            $"Catégories : {Escape(string.Join(", ", categories))}\n" +
            $"Capture : {Escape(performance.TracePath ?? "mémoire (smoke test)")}";

        _editorAccessibility!.Text = string.Join("\n", governance.Accessibility.EditorChecks.Select(check =>
            $"{Symbol(check.Passed)} [b]{Escape(check.Code)}[/b]\n   {Escape(check.Message)}"));
        _runtimeAccessibility!.Text = string.Join("\n", governance.Accessibility.RuntimeChecks.Select(check =>
            $"{Symbol(check.Passed)} [b]{Escape(check.Code)}[/b]\n   {Escape(check.Message)}"));
        _securityFuzz!.Text = $"{Symbol(governance.SecurityFuzz.Passed)} [b]{governance.SecurityFuzz.TotalCases}[/b] cas\n" +
            $"{Symbol(governance.SecurityFuzz.Boundaries.All(item => item.UnexpectedFailures == 0))} zéro crash inattendu\n" +
            $"{Symbol(governance.SecurityFuzz.Boundaries.All(item => item.PathEscapes == 0))} zéro évasion de chemin\n\n" +
            string.Join("\n", governance.SecurityFuzz.Boundaries.Select(item =>
                $"{Escape(item.Boundary)} · {item.Accepted} accepté / {item.Rejected} refusé"));
        _dependencies!.Text = $"{Symbol(governance.Dependencies.Passed)} [b]{governance.Dependencies.DependencyCount}[/b] dépendance(s)\n" +
            $"{Symbol(governance.Dependencies.Checks.All(item => item.Passed))} registre et revues conformes\n\n" +
            (governance.Dependencies.Checks.Count == 0 ? "Aucune dépendance tierce déclarée." :
                string.Join("\n", governance.Dependencies.Checks.Select(item =>
                    $"{Symbol(item.Passed)} {Escape(item.Code)}")));

        SetStatus(allPassed
                ? "✓ Module prêt pour le playtest · qualité, performance, accessibilité, sécurité et gouvernance validées."
                : "✕ Des erreurs bloquantes doivent être corrigées avant l’export.",
            allPassed ? new Color("79d5ad") : new Color("ff756d"));

        QualityAssuranceProfile projectQuality() => _project!.Invoke()!.Module.RuntimePolicies.Extensions
            .ModernTooling.QualityAssurance;
    }

    private void ClearCards()
    {
        foreach (var card in new[] { _localization, _scripts, _dialogues, _quests, _encounters,
                     _pseudoLocalization, _network, _visual, _benchmarks, _budgets, _profiler,
                     _editorAccessibility, _runtimeAccessibility, _securityFuzz, _dependencies })
            if (card is not null) card.Text = "Indisponible.";
    }

    private void SetStatus(string text, Color color)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", color);
    }

    private static string Symbol(bool passed) => passed ? "✓" : "⚠";
    private static string Short(string fingerprint) => fingerprint.Length <= 12 ? fingerprint : fingerprint[..12];
    private static string Trim(string value, int maximum) => value.Length <= maximum ? value : value[..(maximum - 1)] + "…";
    private static string Escape(string value) => value.Replace("[", "﹝", StringComparison.Ordinal)
        .Replace("]", "﹞", StringComparison.Ordinal);
}
#endif
