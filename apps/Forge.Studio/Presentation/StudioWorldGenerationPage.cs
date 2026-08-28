using Forge.Application.WorldGeneration;
using Forge.Domain.WorldGeneration;
using Forge.Mcp.Projects;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldGenerationPage : Control
{
    private readonly StudioSessionController _session;
    private readonly Label _area;
    private readonly Label _revisions;
    private readonly LineEdit _title;
    private readonly SpinBox _seed;
    private readonly SpinBox _width;
    private readonly SpinBox _depth;
    private readonly SpinBox _elevation;
    private readonly SpinBox _roughness;
    private readonly CheckBox _nativeOnly;
    private readonly Button _applyRecipe;
    private readonly Button _previewExecution;
    private readonly Button _queueExecution;
    private readonly Button _runWorker;
    private readonly OptionButton _provider;
    private readonly Button _previewPromotion;
    private readonly Button _applyPromotion;
    private readonly ItemList _jobs;
    private readonly RichTextLabel _proof;
    private readonly Label _status;
    private string? _recipePlanId;
    private Guid? _recipeId;
    private Guid? _executionPlanId;
    private Guid? _selectedJobPlanId;
    private string? _promotionPlanId;
    private Task<WorldGenerationWorkerResult>? _workerTask;
    private IReadOnlyList<WorldGenerationJobResource> _jobResources = [];
    private Guid? _contextAreaId;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ProjectChanged;

    internal StudioWorldGenerationPage(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioWorldGenerationPage";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = StudioStyle.Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margin = StudioStyle.Margin(10, 9, 10, 9);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var banner = StudioStyle.Panel("WorldGenerationBanner", StudioStyle.AccentDark, 8);
        root.AddChild(banner);
        var bannerMargin = StudioStyle.Margin(10, 7, 10, 7);
        banner.AddChild(bannerMargin);
        var bannerRow = new HBoxContainer();
        bannerMargin.AddChild(bannerRow);
        var bannerText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bannerText.AddChild(StudioStyle.Label("Pipeline gouverné et réversible", 15));
        _area = StudioStyle.Label(string.Empty, 10, StudioStyle.Success, wrap: true);
        bannerText.AddChild(_area);
        bannerRow.AddChild(bannerText);
        _revisions = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted);
        bannerRow.AddChild(_revisions);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        root.AddChild(body);

        var recipePanel = StudioStyle.Panel("WorldGenerationRecipe", StudioStyle.Chrome, 8);
        recipePanel.CustomMinimumSize = new Vector2(330, 0);
        body.AddChild(recipePanel);
        var recipeMargin = StudioStyle.Margin(10, 10, 10, 10);
        recipePanel.AddChild(recipeMargin);
        var recipeColumn = new VBoxContainer();
        recipeColumn.AddThemeConstantOverride("separation", 7);
        recipeMargin.AddChild(recipeColumn);
        recipeColumn.AddChild(StudioStyle.SectionLabel("RECETTE DE LA ZONE ACTIVE"));
        recipeColumn.AddChild(StudioStyle.Label(
            "La recette reste dérivée jusqu’à une promotion explicite. Chaque application vérifie les révisions.",
            10, StudioStyle.Muted, wrap: true));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 6);
        recipeColumn.AddChild(grid);
        _title = AddField(grid, "Nom", Edit("Vallée gouvernée"));
        _seed = AddField(grid, "Graine", Number(1, int.MaxValue, 1, 2026));
        _width = AddField(grid, "Largeur", Number(32, 32768, 1, 512, " m"));
        _depth = AddField(grid, "Profondeur", Number(32, 32768, 1, 512, " m"));
        _elevation = AddField(grid, "Relief max.", Number(1, 8192, 1, 96, " m"));
        _roughness = AddField(grid, "Rugosité", Number(0, 1, .01, .42));
        _nativeOnly = new CheckBox
        {
            Text = "Repli Godot natif uniquement",
            TooltipText = "Utile pour vérifier le contrat sans addons. Une promotion artistique complète exige les fournisseurs qualifiés.",
        };
        recipeColumn.AddChild(_nativeOnly);
        var recipeActions = new HBoxContainer();
        recipeActions.AddThemeConstantOverride("separation", 6);
        var previewRecipe = StudioStyle.Button("1 · Prévisualiser", primary: true, compact: true);
        previewRecipe.Pressed += PreviewRecipe;
        recipeActions.AddChild(previewRecipe);
        _applyRecipe = StudioStyle.Button("2 · Appliquer", compact: true);
        _applyRecipe.Disabled = true;
        _applyRecipe.Pressed += ApplyRecipe;
        recipeActions.AddChild(_applyRecipe);
        recipeColumn.AddChild(recipeActions);

        var executionActions = new HBoxContainer();
        executionActions.AddThemeConstantOverride("separation", 6);
        _previewExecution = StudioStyle.Button("3 · Prévisualiser le plan", compact: true);
        _previewExecution.Pressed += PreviewExecution;
        executionActions.AddChild(_previewExecution);
        _queueExecution = StudioStyle.Button("4 · Mettre en file", compact: true);
        _queueExecution.Disabled = true;
        _queueExecution.Pressed += QueueExecution;
        executionActions.AddChild(_queueExecution);
        recipeColumn.AddChild(executionActions);
        recipeColumn.AddChild(StudioStyle.Label(
            "Le worker Godot consomme le job durable. Aucun script libre ne provient de la recette.",
            9, StudioStyle.Muted, wrap: true));

        var jobsPanel = StudioStyle.Panel("WorldGenerationJobs", StudioStyle.Chrome, 8);
        jobsPanel.CustomMinimumSize = new Vector2(260, 0);
        body.AddChild(jobsPanel);
        var jobsMargin = StudioStyle.Margin(9, 9, 9, 9);
        jobsPanel.AddChild(jobsMargin);
        var jobsColumn = new VBoxContainer();
        jobsColumn.AddThemeConstantOverride("separation", 6);
        jobsMargin.AddChild(jobsColumn);
        jobsColumn.AddChild(StudioStyle.SectionLabel("JOBS PERSISTANTS · ZONE ACTIVE"));
        var refresh = StudioStyle.Button("Actualiser", compact: true);
        refresh.Pressed += RefreshJobs;
        jobsColumn.AddChild(refresh);
        _jobs = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 180),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _jobs.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        _jobs.ItemSelected += SelectJob;
        jobsColumn.AddChild(_jobs);
        _provider = new OptionButton();
        _provider.AddItem("Godot Flow · qualifié", 0);
        _provider.SetItemMetadata(0, "godot-flow");
        _provider.AddItem("GF-PCGODOT · qualifié", 1);
        _provider.SetItemMetadata(1, "gf-pcgodot");
        StudioStyle.ApplyOptionButton(_provider);
        jobsColumn.AddChild(_provider);
        _runWorker = StudioStyle.Button("5 · Lancer le worker local", primary: true, compact: true);
        _runWorker.Disabled = true;
        _runWorker.Pressed += RunWorker;
        jobsColumn.AddChild(_runWorker);
        _previewPromotion = StudioStyle.Button("6 · Prévisualiser la promotion", compact: true);
        _previewPromotion.Disabled = true;
        _previewPromotion.Pressed += PreviewPromotion;
        jobsColumn.AddChild(_previewPromotion);
        _applyPromotion = StudioStyle.Button("7 · Promouvoir vers la zone", primary: true, compact: true);
        _applyPromotion.Disabled = true;
        _applyPromotion.Pressed += ApplyPromotion;
        jobsColumn.AddChild(_applyPromotion);

        var proofPanel = StudioStyle.Panel("WorldGenerationProof", StudioStyle.Surface, 8);
        proofPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        proofPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(proofPanel);
        var proofMargin = StudioStyle.Margin(12, 10, 12, 10);
        proofPanel.AddChild(proofMargin);
        var proofColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        proofColumn.AddThemeConstantOverride("separation", 6);
        proofMargin.AddChild(proofColumn);
        proofColumn.AddChild(StudioStyle.SectionLabel("DIFF, EMPREINTES ET PREUVES"));
        _proof = new RichTextLabel
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = true,
            ContextMenuEnabled = true,
            BbcodeEnabled = false,
        };
        _proof.AddThemeColorOverride("default_color", StudioStyle.Text);
        _proof.AddThemeFontSizeOverride("normal_font_size", 11);
        proofColumn.AddChild(_proof);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        proofColumn.AddChild(_status);
        SetProcess(false);
    }

    internal void Activate()
    {
        var active = _session.ActiveArea;
        if (active is null)
        {
            _area.Text = "Créez d’abord une zone.";
            SetControlsEnabled(false);
            return;
        }
        var areaChanged = _contextAreaId != active.Id.Value;
        _contextAreaId = active.Id.Value;
        if (areaChanged) ResetTransientPlans();
        SetControlsEnabled(true);
        _area.Text = $"Zone cible · {active.DisplayName} · {active.Kind}";
        RefreshState();
        RefreshJobs();
    }

    private void PreviewRecipe()
    {
        Run("Prévisualisation de la recette", () =>
        {
            var area = _session.ActiveArea ?? throw new InvalidOperationException("Aucune zone active.");
            var result = _session.PreviewWorldGenerationRecipe(
                area.Id.Value, _title.Text, (int)_seed.Value, _width.Value, _depth.Value,
                _elevation.Value, _roughness.Value, _nativeOnly.ButtonPressed);
            if (!result.Succeeded || result.PlanId is null)
                throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
            _recipePlanId = result.PlanId;
            _applyRecipe.Disabled = false;
            _proof.Text = $"RECETTE PRÉVISUALISÉE\n\nPlan : {result.PlanId}\nTransaction : {result.TransactionId}\n" +
                          $"Révision recette : {result.BaseRecipeRevision}\nRévision projet : {result.ProjectRevision}\n\n" +
                          $"Diff : {result.Diff?.Operation} · {result.Diff?.Path}\n{result.Diff?.Summary}\n\n" +
                          $"Inverse : {result.Inverse?.Summary}";
        });
    }

    private void ApplyRecipe()
    {
        Run("Application de la recette", () =>
        {
            if (_recipePlanId is null) throw new InvalidOperationException("Prévisualisez d’abord la recette.");
            var result = _session.ApplyWorldGenerationRecipe(_recipePlanId);
            if (!result.Succeeded || !result.Applied)
                throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
            var state = _session.GetWorldGenerationState();
            _recipeId = state.Catalog.Recipes.Single(item => item.AreaId == _session.ActiveArea!.Id.Value).Id;
            _recipePlanId = null;
            _applyRecipe.Disabled = true;
            _previewExecution.Disabled = false;
            _proof.Text += $"\n\nRECETTE APPLIQUÉE\nNouvelle révision : {result.NewRecipeRevision}\n" +
                           $"Empreinte : {state.Catalog.Find(_recipeId.Value)!.Fingerprint()}";
            RefreshState();
        });
    }

    private void PreviewExecution()
    {
        Run("Prévisualisation du plan d’exécution", () =>
        {
            ResolveRecipeId();
            var result = _session.PreviewWorldGenerationExecution(_recipeId!.Value);
            if (!result.Succeeded || result.Plan is null)
                throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
            _executionPlanId = result.Plan.PlanId;
            _queueExecution.Disabled = !result.Plan.ReadyToApply;
            _proof.Text = $"PLAN D’EXÉCUTION\n\nPlan : {result.Plan.PlanId:D}\n" +
                          $"Empreinte recette : {result.Plan.RecipeFingerprint}\n" +
                          $"Empreinte plan : {result.Plan.PlanFingerprint}\n" +
                          $"Prêt : {result.Plan.ReadyToApply}\n\n" +
                          string.Join('\n', result.Plan.Steps.Select(step =>
                              $"{step.Order + 1}. {step.Stage} · {step.Backend} · {step.Operation}"));
        });
    }

    private void QueueExecution()
    {
        Run("Mise en file du plan", () =>
        {
            if (_executionPlanId is null) throw new InvalidOperationException("Prévisualisez d’abord le plan.");
            var result = _session.QueueWorldGenerationExecution(_executionPlanId.Value);
            if (!result.Succeeded || !result.Queued)
                throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
            _proof.Text += $"\n\nJOB PERSISTÉ\nÉtat : {result.JobStatus}\n" +
                           "Le worker Godot peut maintenant reprendre ce plan, même après redémarrage.";
            _queueExecution.Disabled = true;
            RefreshJobs();
        });
    }

    private void RefreshJobs()
    {
        Run("Actualisation des jobs", () =>
        {
            var active = _session.ActiveArea ?? throw new InvalidOperationException("Aucune zone active.");
            if (_contextAreaId != active.Id.Value)
            {
                _contextAreaId = active.Id.Value;
                ResetTransientPlans();
                _area.Text = $"Zone cible · {active.DisplayName} · {active.Kind}";
                RefreshState();
            }
            _jobResources = _session.GetWorldGenerationJobs(active.Id.Value);
            _jobs.Clear();
            _selectedJobPlanId = null;
            _promotionPlanId = null;
            _previewPromotion.Disabled = true;
            _applyPromotion.Disabled = true;
            foreach (var job in _jobResources)
                _jobs.AddItem($"{job.Status} · {job.PlanId[..8]} · {job.UpdatedAtUtc:dd/MM HH:mm:ss}");
            if (_jobResources.Count == 0)
            {
                _runWorker.Disabled = true;
                _proof.Text = $"AUCUN JOB POUR LA ZONE ACTIVE\n\nZone : {active.DisplayName}\n" +
                              $"Identifiant : {active.Id.Value:D}\n\n" +
                              "Appliquez une recette, prévisualisez son plan puis mettez-le en file.";
                return;
            }
            _runWorker.Disabled = _workerTask is not null || !_jobResources.Any(job =>
                job.Status is WorldGenerationJobStatus.Queued or WorldGenerationJobStatus.Running);
            _jobs.Select(0);
            SelectJob(0);
        }, quiet: true);
    }

    private void RunWorker()
    {
        if (_workerTask is not null) return;
        var provider = _provider.GetSelectedMetadata().AsString();
        _workerTask = _session.RunWorldGenerationWorkerAsync(provider);
        _runWorker.Disabled = true;
        _provider.Disabled = true;
        _status.Text = "Worker local en cours · fournisseurs vérifiés et génération isolée…";
        _status.AddThemeColorOverride("font_color", StudioStyle.Warning);
        StatusChanged?.Invoke(_status.Text, false);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_workerTask is not { IsCompleted: true } completed) return;
        WorldGenerationWorkerResult result;
        if (completed.IsCompletedSuccessfully)
        {
            result = completed.Result;
        }
        else
        {
            var message = completed.Exception?.GetBaseException().Message ?? "Le worker a été annulé.";
            result = new(false, -1, string.Empty, message, string.Empty);
        }
        _workerTask = null;
        SetProcess(false);
        CompleteWorker(result);
    }

    private void CompleteWorker(WorldGenerationWorkerResult result)
    {
        _provider.Disabled = false;
        RefreshJobs();
        var output = string.Join('\n', result.StandardOutput.Split(['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries).TakeLast(12));
        var error = string.Join('\n', result.StandardError.Split(['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries).TakeLast(8));
        _proof.Text = $"WORKER LOCAL\n\nScript : {result.ScriptPath}\nCode : {result.ExitCode}\n\n{output}" +
                      (error.Length == 0 ? string.Empty : $"\n\nERREUR\n{error}");
        _status.Text = result.Succeeded
            ? "Worker local terminé · candidat vérifié disponible pour revue et promotion."
            : "Worker local en échec · consultez les preuves détaillées.";
        _status.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        StatusChanged?.Invoke(_status.Text, !result.Succeeded);
    }

    private void SelectJob(long index)
    {
        if (index < 0 || index >= _jobResources.Count) return;
        var job = _jobResources[(int)index];
        _selectedJobPlanId = Guid.Parse(job.PlanId);
        _promotionPlanId = null;
        _previewPromotion.Disabled = job.Status != WorldGenerationJobStatus.Succeeded;
        _applyPromotion.Disabled = true;
        _proof.Text = $"JOB {job.Status}\n\nPlan : {job.PlanId}\nRecette : {job.RecipeId}\nZone : {job.AreaId}\n" +
                      $"Empreinte plan : {job.PlanFingerprint}\nMis à jour : {job.UpdatedAtUtc:O}\n\n" +
                      string.Join('\n', job.Audit.Select(item =>
                          $"{item.OccurredAtUtc:HH:mm:ss} · {item.Action} · {item.Detail}"));
    }

    private void PreviewPromotion()
    {
        Run("Prévisualisation de la promotion", () =>
        {
            if (_selectedJobPlanId is null) throw new InvalidOperationException("Sélectionnez un job réussi.");
            var activeAreaId = RequireSelectedJobForActiveArea();
            var result = _session.PreviewWorldGenerationPromotion(_selectedJobPlanId.Value, activeAreaId);
            if (!result.Succeeded || result.PromotionPlanId is null)
                throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
            _promotionPlanId = result.PromotionPlanId;
            _applyPromotion.Disabled = false;
            _proof.Text = $"PROMOTION PRÉVISUALISÉE\n\nPlan : {result.PromotionPlanId}\n" +
                          $"Scène canonique : {result.CanonicalSceneRelativePath}\n" +
                          $"SHA-256 : {result.CanonicalSceneSha256}\n\n" +
                          $"Diff : {result.Diff?.Summary}\nInverse : {result.Inverse?.Summary}";
        });
    }

    private void ApplyPromotion()
    {
        Run("Promotion canonique", () =>
        {
            if (_promotionPlanId is null) throw new InvalidOperationException("Prévisualisez d’abord la promotion.");
            RequireSelectedJobForActiveArea();
            var result = _session.ApplyWorldGenerationPromotion(_promotionPlanId);
            if (!result.Succeeded || !result.Applied)
                throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
            _applyPromotion.Disabled = true;
            _proof.Text += $"\n\nPROMOTION APPLIQUÉE\nNouvelle révision projet : {result.NewProjectRevision}\n" +
                           "La scène vérifiée est maintenant une ressource canonique de la zone et sera instanciée par le runtime.";
            ProjectChanged?.Invoke();
            RefreshState();
        });
    }

    private void RefreshState()
    {
        var state = _session.GetWorldGenerationState();
        _revisions.Text = $"Projet r{state.ProjectRevision} · recettes r{state.RecipeRevision}";
        var areaId = _session.ActiveArea?.Id.Value;
        var recipe = areaId is null ? null : state.Catalog.Recipes.SingleOrDefault(item => item.AreaId == areaId);
        _recipeId = recipe?.Id;
        _previewExecution.Disabled = recipe is null;
        if (recipe is null)
        {
            if (_session.ActiveArea is { } active)
            {
                var baseline = WorldGenerationRecipe.CreateDefault(
                    active.Id.Value,
                    $"{active.DisplayName} · génération",
                    2026);
                PopulateRecipeFields(baseline);
            }
            return;
        }
        PopulateRecipeFields(recipe);
    }

    private void PopulateRecipeFields(WorldGenerationRecipe recipe)
    {
        _title.Text = recipe.Title;
        _seed.Value = recipe.Seed;
        _width.Value = recipe.Bounds.WidthMeters;
        _depth.Value = recipe.Bounds.DepthMeters;
        _elevation.Value = recipe.Bounds.MaxElevationMeters;
        _roughness.Value = recipe.Terrain.Roughness;
        _nativeOnly.ButtonPressed = recipe.Backends.Values.All(value => value == WorldGenerationBackend.GodotNative);
    }

    private Guid RequireSelectedJobForActiveArea()
    {
        if (_selectedJobPlanId is null) throw new InvalidOperationException("Sélectionnez un job réussi.");
        var active = _session.ActiveArea ?? throw new InvalidOperationException("Aucune zone active.");
        var selected = _jobResources.FirstOrDefault(job =>
            string.Equals(job.PlanId, _selectedJobPlanId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase));
        if (selected is null || !Guid.TryParse(selected.AreaId, out var selectedAreaId) ||
            selectedAreaId != active.Id.Value)
            throw new InvalidOperationException("Le job sélectionné ne cible plus la zone active. Actualisez la liste.");
        return active.Id.Value;
    }

    private void ResetTransientPlans()
    {
        _recipePlanId = null;
        _recipeId = null;
        _executionPlanId = null;
        _selectedJobPlanId = null;
        _promotionPlanId = null;
        _applyRecipe.Disabled = true;
        _queueExecution.Disabled = true;
        _previewPromotion.Disabled = true;
        _applyPromotion.Disabled = true;
    }

    private void ResolveRecipeId()
    {
        if (_recipeId is not null) return;
        RefreshState();
        if (_recipeId is null) throw new InvalidOperationException("Appliquez d’abord une recette à la zone active.");
    }

    private void Run(string action, Action operation, bool quiet = false)
    {
        try
        {
            operation();
            if (!quiet)
            {
                _status.Text = $"{action} réussie.";
                _status.AddThemeColorOverride("font_color", StudioStyle.Success);
                StatusChanged?.Invoke(_status.Text, false);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            _status.Text = $"{action} refusée · {exception.Message}";
            _status.AddThemeColorOverride("font_color", StudioStyle.Danger);
            StatusChanged?.Invoke(_status.Text, true);
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        _title.Editable = enabled;
        _seed.Editable = enabled;
        _width.Editable = enabled;
        _depth.Editable = enabled;
        _elevation.Editable = enabled;
        _roughness.Editable = enabled;
    }

    private static string FormatDiagnostics(IEnumerable<Forge.Mcp.AgentDiagnostic> diagnostics) =>
        string.Join(" · ", diagnostics.Select(item => $"{item.Code} : {item.Message}"));

    private static LineEdit Edit(string placeholder)
    {
        var edit = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static SpinBox Number(double minimum, double maximum, double step, double value, string suffix = "")
    {
        var number = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Value = value,
            Suffix = suffix,
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(number);
        return number;
    }

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        grid.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }
}
