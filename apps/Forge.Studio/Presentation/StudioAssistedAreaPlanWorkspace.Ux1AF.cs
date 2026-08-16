using System.Text;
using Forge.Domain.Assistant;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed class StudioAssistedAreaPlanWorkspace : PanelContainer
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private readonly OptionButton _area;
    private readonly OptionButton _kit;
    private readonly OptionButton _archetype;
    private readonly LineEdit _title;
    private readonly SpinBox _seed;
    private readonly SpinBox _width;
    private readonly SpinBox _depth;
    private readonly SpinBox _nodes;
    private readonly TextEdit _constraints;
    private readonly TextEdit _objectives;
    private readonly CheckBox _approve;
    private readonly Label _provider;
    private readonly Label _privacy;
    private readonly Label _planSummary;
    private readonly Button _applyPlan;
    private readonly OptionButton _savedPlans;
    private readonly Label _buildSummary;
    private readonly Button _applyBuild;
    private readonly Label _status;
    private readonly List<Guid> _areaIds = [];
    private readonly List<Guid> _kitIds = [];
    private readonly List<Guid> _planIds = [];
    private StudioUx1AFWorkspaceSnapshot? _catalog;
    private StudioUx1AFPlanPreview? _planPreview;
    private StudioUx1AFBuildPreview? _buildPreview;
    private StudioUx1AFEvidenceWorkspace? _evidenceWorkspace;
    private StudioUx1AFSourceSnapshot? _evidenceSourceSnapshot;
    private bool _loading;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;

    internal bool UsesIsolatedEvidence => _evidenceWorkspace is not null;

    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;

    internal bool EvidenceResourcesReleased => _lastEvidenceResourcesReleased;

    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal bool HasExplicitSelection => _area.Selected > 0 && _kit.Selected > 0;

    internal bool PlanPreviewReady => _planPreview is { Succeeded: true, CanApply: true };

    internal bool BuildPreviewReady => _buildPreview is { Succeeded: true, CanApply: true };

    internal int SavedPlanCount => _catalog?.Plans.Count ?? 0;

    internal bool ShowsHumanSurface =>
        _privacy.Text.Contains("Contenu projet", StringComparison.Ordinal) &&
        _privacy.Text.Contains("Réseau", StringComparison.Ordinal) &&
        _provider.Text.Contains("Provenance", StringComparison.Ordinal) &&
        _planSummary.Text.Contains("sans écriture", StringComparison.OrdinalIgnoreCase) &&
        !_planSummary.Text.Contains("AreaId", StringComparison.OrdinalIgnoreCase) &&
        !_buildSummary.Text.Contains("PlanId", StringComparison.OrdinalIgnoreCase);

    internal StudioAssistedAreaPlanWorkspace(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioAssistedAreaPlanWorkspaceUx1AF";
        Visible = false;
        ZIndex = 140;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Background, StudioStyle.Border, 0));

        var outer = StudioStyle.Margin(20, 16, 20, 16);
        AddChild(outer);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);
        outer.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("PLANS DE ZONE ASSISTÉS", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Préparer, approuver puis construire", 22));
        heading.AddChild(StudioStyle.Label(
            "Deux transactions distinctes : le document de plan, puis la géométrie. Chaque application exige son propre dry-run à la révision courante.",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        var policy = AddCard(root, "CONFIDENTIALITÉ, PROVENANCE ET AUTORITÉ",
            "La politique active est affichée avant toute proposition. Les secrets restent des variables d’environnement et ne sont jamais rendus ici.");
        _provider = StudioStyle.Label("Fournisseur indisponible.", 10, StudioStyle.Muted, wrap: true);
        policy.AddChild(_provider);
        _privacy = StudioStyle.Label("Réseau : inconnu · Contenu projet : inconnu", 10, StudioStyle.Muted, wrap: true);
        policy.AddChild(_privacy);

        var body = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = [620],
        };
        root.AddChild(body);

        var formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(560, 0),
        };
        body.AddChild(formScroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 10);
        formScroll.AddChild(form);

        var brief = AddCard(form, "1 · CRÉATION DU PLAN",
            "La zone et le kit doivent être choisis explicitement. La proposition ne modifie ni le catalogue ni la géométrie.");
        _area = AddOption(brief, "Zone cible");
        _kit = AddOption(brief, "Kit Placeable");
        _title = AddLine(brief, "Titre du plan", "Crypte du Serment");
        _archetype = AddOption(brief, "Archétype");
        _archetype.AddItem("Crypte");
        _archetype.AddItem("Village");
        var dimensions = new HBoxContainer();
        dimensions.AddThemeConstantOverride("separation", 8);
        brief.AddChild(dimensions);
        _seed = AddSpin(dimensions, "Seed", int.MinValue, int.MaxValue, 1);
        _width = AddSpin(dimensions, "Largeur", 8, 2048, 1, " m");
        _depth = AddSpin(dimensions, "Profondeur", 8, 2048, 1, " m");
        _nodes = AddSpin(dimensions, "Espaces", 4, 24, 1);
        _constraints = AddText(brief, "Contraintes", "Conserver un trajet navigable entre l’entrée et la sortie.");
        _objectives = AddText(brief, "Objectifs", "Atteindre l’objectif central puis revenir en sécurité.");
        _approve = new CheckBox
        {
            Text = "J’approuve explicitement ce plan pour construction",
            FocusMode = FocusModeEnum.All,
        };
        _approve.AddThemeColorOverride("font_color", StudioStyle.Text);
        brief.AddChild(_approve);
        var previewPlan = StudioStyle.Button("Prévisualiser le plan", primary: true);
        previewPlan.Pressed += async () => await PreviewPlanAsync();
        brief.AddChild(previewPlan);

        var planHistory = new HBoxContainer();
        planHistory.AddThemeConstantOverride("separation", 8);
        brief.AddChild(planHistory);
        var undoPlan = StudioStyle.Button("Annuler le document de plan", compact: true);
        undoPlan.Pressed += UndoPlan;
        planHistory.AddChild(undoPlan);
        var redoPlan = StudioStyle.Button("Rétablir le document de plan", compact: true);
        redoPlan.Pressed += RedoPlan;
        planHistory.AddChild(redoPlan);

        var build = AddCard(form, "2 · CONSTRUCTION DE LA ZONE",
            "Seul un plan approuvé, explicitement sélectionné et prévisualisé peut produire une commande géométrique atomique.");
        _savedPlans = AddOption(build, "Plan enregistré");
        var previewBuild = StudioStyle.Button("Prévisualiser la construction", primary: true);
        previewBuild.Pressed += PreviewBuild;
        build.AddChild(previewBuild);
        var projectHistory = new HBoxContainer();
        projectHistory.AddThemeConstantOverride("separation", 8);
        build.AddChild(projectHistory);
        var undoBuild = StudioStyle.Button("Annuler la construction", compact: true);
        undoBuild.Pressed += UndoBuild;
        projectHistory.AddChild(undoBuild);
        var redoBuild = StudioStyle.Button("Rétablir la construction", compact: true);
        redoBuild.Pressed += RedoBuild;
        projectHistory.AddChild(redoBuild);
        var save = StudioStyle.Button("Enregistrer le projet", compact: true);
        save.Pressed += SaveProject;
        projectHistory.AddChild(save);

        var previews = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(430, 0),
        };
        previews.AddThemeConstantOverride("separation", 10);
        body.AddChild(previews);
        var planPreviewCard = AddCard(previews, "APERÇU DU PLAN",
            "Topologie humaine, coût estimé et provenance. Aucun identifiant technique n’est nécessaire pour décider.");
        _planSummary = PreviewLabel("Aucun aperçu · le document de plans et la géométrie sont inchangés.");
        _planSummary.SizeFlagsVertical = SizeFlags.ExpandFill;
        planPreviewCard.AddChild(_planSummary);
        _applyPlan = StudioStyle.Button("Enregistrer le plan prévisualisé", primary: true);
        _applyPlan.Disabled = true;
        _applyPlan.Pressed += ApplyPlan;
        planPreviewCard.AddChild(_applyPlan);

        var buildPreviewCard = AddCard(previews, "APERÇU DE CONSTRUCTION",
            "Placements, navigation et éclairage sont validés avant toute mutation géométrique.");
        _buildSummary = PreviewLabel("Aucun aperçu · aucun objet n’a été placé.");
        _buildSummary.SizeFlagsVertical = SizeFlags.ExpandFill;
        buildPreviewCard.AddChild(_buildSummary);
        _applyBuild = StudioStyle.Button("Construire le plan prévisualisé", primary: true);
        _applyBuild.Disabled = true;
        _applyBuild.Pressed += ApplyBuild;
        buildPreviewCard.AddChild(_applyBuild);

        _status = StudioStyle.Label("Surface prête · aucune écriture effectuée.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        _area.ItemSelected += _ => OnDraftChanged();
        _kit.ItemSelected += _ => OnDraftChanged();
        _archetype.ItemSelected += _ => OnDraftChanged();
        _title.TextChanged += _ => OnDraftChanged();
        _seed.ValueChanged += _ => OnDraftChanged();
        _width.ValueChanged += _ => OnDraftChanged();
        _depth.ValueChanged += _ => OnDraftChanged();
        _nodes.ValueChanged += _ => OnDraftChanged();
        _constraints.TextChanged += OnDraftChanged;
        _objectives.TextChanged += OnDraftChanged;
        _approve.Toggled += _ => OnDraftChanged();
        _savedPlans.ItemSelected += _ => OnPlanSelectionChanged();
    }

    public override void _ExitTree()
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
        _session = _sourceSession;
        OpenActive();
    }

    private void OpenActive()
    {
        _session.CancelUx1AFPreview();
        Visible = true;
        _loading = true;
        var catalog = _session.CreateUx1AFWorkspaceSnapshot();
        _catalog = catalog;
        PopulateChoices();
        ApplyPolicyText(catalog);
        _title.Text = "Crypte du Serment";
        _seed.Value = 20260817;
        _nodes.Value = 8;
        SetDimensionDefaults();
        _constraints.Text = "Conserver un trajet navigable entre l’entrée et la sortie.";
        _objectives.Text = "Atteindre l’objectif central puis revenir en sécurité.";
        _approve.ButtonPressed = false;
        _loading = false;
        ClearPlanPreview("Aucun aperçu · sélectionnez explicitement une zone et un kit.");
        ClearBuildPreview("Aucun aperçu · sélectionnez explicitement un plan approuvé.");
        SetNeutralStatus(catalog.Message);
    }

    internal void Close()
    {
        _session.CloseUx1AF();
        Visible = false;
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
    }

    internal bool PrepareEvidence(string mode)
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }

        _evidenceSourceSnapshot = sourceSnapshot;
        _lastEvidenceSourceUntouched = false;
        if (!ActivateEvidenceWorkspace())
        {
            Visible = false;
            ReleaseEvidenceWorkspace();
            CompleteEvidenceSourceAudit();
            return false;
        }

        SelectEvidenceInputs();
        var preview = PreviewPlanAsync().GetAwaiter().GetResult();
        if (!preview || !ApplyPlanInternal())
        {
            Close();
            return false;
        }

        if (!SelectEvidencePlan() || !PreviewBuildInternal())
        {
            Close();
            return false;
        }

        var ready = UsesIsolatedEvidence && HasExplicitSelection && PlanPreviewReady && BuildPreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched &&
                    mode.StartsWith("ux1-af", StringComparison.OrdinalIgnoreCase);
        if (!ready)
        {
            Close();
        }
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }

        _evidenceSourceSnapshot = sourceSnapshot;
        _lastEvidenceSourceUntouched = false;
        if (!ActivateEvidenceWorkspace())
        {
            Visible = false;
            ReleaseEvidenceWorkspace();
            CompleteEvidenceSourceAudit();
            return false;
        }

        SelectEvidenceInputs();
        var planHistory = _session.History!;
        var planProject = _session.Project;
        var planRevision = planHistory.State.Revision;
        var planJournal = planHistory.Journal.ToArray();
        var planPreviewed = PreviewPlanAsync().GetAwaiter().GetResult();
        var planDryRunPure = planPreviewed && ReferenceEquals(planProject, _session.Project) &&
                             planHistory.State.Revision == planRevision &&
                             planHistory.Journal.SequenceEqual(planJournal);
        var planApplied = ApplyPlanInternal();
        var approvedPlan = _catalog?.Plans.SingleOrDefault(plan => plan.Approved);
        var planUndo = _session.UndoUx1AFPlan();
        RefreshCatalog();
        var planRemoved = _catalog?.Plans.Count == 0;
        var planRedo = _session.RedoUx1AFPlan();
        RefreshCatalog();
        var planRestored = approvedPlan is not null && _catalog?.Plans.Any(plan => plan.Id == approvedPlan.Id) == true;
        var selected = SelectEvidencePlan();

        var buildProject = _session.Project;
        var buildRevision = _session.History!.State.Revision;
        var buildJournal = _session.History.Journal.ToArray();
        var buildPreviewed = selected && PreviewBuildInternal();
        var buildDryRunPure = buildPreviewed && ReferenceEquals(buildProject, _session.Project) &&
                              _session.History.State.Revision == buildRevision &&
                              _session.History.Journal.SequenceEqual(buildJournal);
        var planId = _buildPreview?.PlanId ?? Guid.Empty;
        var beforeCount = _session.ActiveArea!.Entities.Count;
        var buildApplied = ApplyBuildInternal();
        var afterCount = _session.ActiveArea!.Entities.Count;
        var marker = $"assistant-plan:{planId:D}";
        var geometryApplied = buildApplied && afterCount > beforeCount &&
                              _session.ActiveArea.Entities.Any(entity =>
                                  entity.Tags.Contains(marker, StringComparer.Ordinal));
        var undoBuild = _session.Undo();
        var geometryUndone = _session.ActiveArea!.Entities.Count == beforeCount;
        var redoBuild = _session.Redo();
        var geometryRedone = _session.ActiveArea!.Entities.Count == afterCount;
        var saved = _session.Save();

        var reopened = new StudioSessionController();
        var reopen = reopened.Open(_evidenceWorkspace!.RootPath);
        var reopenedCatalog = reopen.Succeeded ? reopened.CreateUx1AFWorkspaceSnapshot() : null;
        var persisted = reopen.Succeeded && reopenedCatalog?.Plans.Any(plan => plan.Id == planId && plan.Approved) == true &&
                        reopened.Project!.Areas.SelectMany(area => area.Entities).Any(entity =>
                            entity.Tags.Contains(marker, StringComparer.Ordinal));
        reopened.CloseUx1AF();
        reopened.Close();

        var visible = HasExplicitSelection && ShowsHumanSurface;
        Close();
        var sourceUntouched = EvidenceSourceUntouched;
        var resourcesReleased = EvidenceResourcesReleased;
        var valid = visible && planDryRunPure && planApplied && planUndo.Succeeded && planRemoved &&
                    planRedo.Succeeded && planRestored && buildDryRunPure && geometryApplied &&
                    undoBuild.Succeeded && geometryUndone && redoBuild.Succeeded && geometryRedone &&
                    saved.Succeeded && persisted && sourceUntouched && resourcesReleased && SourceSessionRestored;
        if (!valid)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AF_UI_TRANSACTION_FAILED visible={visible.ToString().ToLowerInvariant()} " +
                $"plan_dry={planDryRunPure.ToString().ToLowerInvariant()} plan_apply={planApplied.ToString().ToLowerInvariant()} " +
                $"plan_undo_redo={(planUndo.Succeeded && planRedo.Succeeded).ToString().ToLowerInvariant()} " +
                $"build_dry={buildDryRunPure.ToString().ToLowerInvariant()} geometry={geometryApplied.ToString().ToLowerInvariant()} " +
                $"build_undo_redo={(undoBuild.Succeeded && redoBuild.Succeeded).ToString().ToLowerInvariant()} " +
                $"persisted={persisted.ToString().ToLowerInvariant()} source_untouched={sourceUntouched.ToString().ToLowerInvariant()} " +
                $"released={resourcesReleased.ToString().ToLowerInvariant()}");
        }
        return valid;
    }

    private async Task<bool> PreviewPlanAsync()
    {
        var draft = CreateDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement une zone et un kit avant l’aperçu.", true);
            return false;
        }

        _applyPlan.Disabled = true;
        var preview = await _session.PreviewUx1AFPlanAsync(draft);
        _planPreview = preview;
        _planSummary.Text = RenderPlanPreview(preview);
        _planSummary.AddThemeColorOverride("font_color",
            preview.Succeeded ? StudioStyle.Text : StudioStyle.Warning);
        _applyPlan.Disabled = !preview.CanApply;
        SetStatus(preview.Message, !preview.Succeeded);
        return preview.Succeeded;
    }

    private void ApplyPlan()
    {
        ApplyPlanInternal();
    }

    private bool ApplyPlanInternal()
    {
        var result = _session.ApplyApprovedUx1AFPlan();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _applyPlan.Disabled = true;
            return false;
        }

        _applyPlan.Disabled = true;
        RefreshCatalog();
        WorkspaceChanged?.Invoke();
        return true;
    }

    private void UndoPlan()
    {
        var result = _session.UndoUx1AFPlan();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshCatalog();
            ClearBuildPreview("Le catalogue a changé · prévisualisez de nouveau la construction.");
            WorkspaceChanged?.Invoke();
        }
    }

    private void RedoPlan()
    {
        var result = _session.RedoUx1AFPlan();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshCatalog();
            ClearBuildPreview("Le catalogue a changé · prévisualisez de nouveau la construction.");
            WorkspaceChanged?.Invoke();
        }
    }

    private void PreviewBuild()
    {
        PreviewBuildInternal();
    }

    private bool PreviewBuildInternal()
    {
        if (_savedPlans.Selected <= 0 || _savedPlans.Selected > _planIds.Count)
        {
            SetStatus("Choisissez explicitement un plan enregistré avant l’aperçu de construction.", true);
            return false;
        }

        var preview = _session.PreviewUx1AFBuild(_planIds[_savedPlans.Selected - 1]);
        _buildPreview = preview;
        _buildSummary.Text = RenderBuildPreview(preview);
        _buildSummary.AddThemeColorOverride("font_color",
            preview.Succeeded ? StudioStyle.Text : StudioStyle.Warning);
        _applyBuild.Disabled = !preview.CanApply;
        SetStatus(preview.Message, !preview.Succeeded);
        return preview.Succeeded;
    }

    private void ApplyBuild()
    {
        ApplyBuildInternal();
    }

    private bool ApplyBuildInternal()
    {
        var result = _session.ApplyApprovedUx1AFBuild();
        SetStatus(result.Message, !result.Succeeded);
        _applyBuild.Disabled = true;
        if (!result.Succeeded)
        {
            return false;
        }

        RefreshCatalog();
        WorkspaceChanged?.Invoke();
        return true;
    }

    private void UndoBuild()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshCatalog();
            ClearBuildPreview("Le projet a changé · prévisualisez de nouveau la construction.");
            WorkspaceChanged?.Invoke();
        }
    }

    private void RedoBuild()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshCatalog();
            ClearBuildPreview("Le projet a changé · prévisualisez de nouveau la construction.");
            WorkspaceChanged?.Invoke();
        }
    }

    private void SaveProject()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private StudioUx1AFPlanDraft? CreateDraft()
    {
        if (_area.Selected <= 0 || _area.Selected > _areaIds.Count ||
            _kit.Selected <= 0 || _kit.Selected > _kitIds.Count)
        {
            return null;
        }

        return new(
            _areaIds[_area.Selected - 1],
            _kitIds[_kit.Selected - 1],
            _title.Text,
            _archetype.Selected == 0 ? AssistedAreaArchetype.Crypt : AssistedAreaArchetype.Village,
            checked((int)_seed.Value),
            (float)_width.Value,
            (float)_depth.Value,
            checked((int)_nodes.Value),
            Lines(_constraints.Text),
            Lines(_objectives.Text),
            _approve.ButtonPressed);
    }

    private void PopulateChoices()
    {
        _area.Clear();
        _areaIds.Clear();
        _area.AddItem("Choisir une zone…");
        foreach (var choice in _catalog?.Areas ?? [])
        {
            _area.AddItem(choice.DisplayName);
            _areaIds.Add(choice.Id);
        }
        _area.Select(0);

        _kit.Clear();
        _kitIds.Clear();
        _kit.AddItem("Choisir un kit Placeable…");
        foreach (var choice in _catalog?.Kits ?? [])
        {
            _kit.AddItem(choice.DisplayName);
            _kitIds.Add(choice.Id);
        }
        _kit.Select(0);
        RefreshPlanOptions();
    }

    private void RefreshCatalog()
    {
        var catalog = _session.CreateUx1AFWorkspaceSnapshot();
        _catalog = catalog;
        ApplyPolicyText(catalog);
        RefreshPlanOptions();
    }

    private void RefreshPlanOptions()
    {
        _loading = true;
        _savedPlans.Clear();
        _planIds.Clear();
        _savedPlans.AddItem("Choisir un plan enregistré…");
        foreach (var plan in _catalog?.Plans ?? [])
        {
            var state = plan.AlreadyBuilt ? "construit" : plan.Approved ? "approuvé" : "brouillon";
            _savedPlans.AddItem($"{plan.DisplayName} · {state}");
            _planIds.Add(plan.Id);
        }
        _savedPlans.Select(0);
        _savedPlans.Disabled = _planIds.Count == 0;
        _loading = false;
    }

    private void ApplyPolicyText(StudioUx1AFWorkspaceSnapshot snapshot)
    {
        _provider.Text =
            $"Fournisseur : {snapshot.ProviderName} · moteur {snapshot.ProviderKind} · " +
            $"permission maximale {HumanPermission(snapshot.MaximumPermission)} · Provenance : {snapshot.ProviderProvenance}";
        _privacy.Text =
            $"Réseau : {(snapshot.NetworkAllowed ? "autorisé par le profil" : "interdit par le profil")} · " +
            $"Portée : {(snapshot.LocalOnly ? "boucle locale uniquement" : "destination configurée")} · " +
            $"Contenu projet : {(snapshot.ShareProjectContent ? "partage explicitement autorisé" : "masqué au fournisseur")} · " +
            $"Index de lore : {snapshot.LoreEntryCount} entrée(s).";
    }

    private void OnDraftChanged()
    {
        if (_loading)
        {
            return;
        }
        _session.CancelUx1AFPreview();
        ClearPlanPreview("Brouillon modifié · prévisualisez de nouveau avant toute écriture.");
        ClearBuildPreview("Le brouillon du plan a changé · prévisualisez de nouveau la construction.");
    }

    private void OnPlanSelectionChanged()
    {
        if (_loading)
        {
            return;
        }
        _session.CancelUx1AFPreview();
        ClearBuildPreview("Plan sélectionné · prévisualisez sa construction avant application.");
    }

    private void SetDimensionDefaults()
    {
        _width.Value = 16;
        _depth.Value = 16;
    }

    private void SelectEvidenceInputs()
    {
        _loading = true;
        _area.Select(1);
        _kit.Select(1);
        _archetype.Select(0);
        _title.Text = "Crypte du témoin hors ligne";
        _seed.Value = 20260817;
        _width.Value = 48;
        _depth.Value = 48;
        _nodes.Value = 4;
        _constraints.Text = "Conserver un trajet navigable entre l’entrée et la sortie.";
        _objectives.Text = "Atteindre l’objectif central puis revenir en sécurité.";
        _approve.ButtonPressed = true;
        _loading = false;
        OnDraftChanged();
    }

    private bool SelectEvidencePlan()
    {
        var index = _catalog?.Plans.ToList().FindIndex(plan => plan.Approved && !plan.AlreadyBuilt) ?? -1;
        if (index < 0)
        {
            return false;
        }
        _savedPlans.Select(index + 1);
        OnPlanSelectionChanged();
        return true;
    }

    private bool ActivateEvidenceWorkspace()
    {
        ReleaseEvidenceWorkspace();
        var workspace = _sourceSession.CreateUx1AFEvidenceWorkspace();
        if (workspace is null)
        {
            return false;
        }
        _evidenceWorkspace = workspace;
        _lastEvidenceResourcesReleased = false;
        _session = workspace.Session;
        OpenActive();
        return _catalog is { Ready: true } && _areaIds.Count > 0 && _kitIds.Count > 0;
    }

    private void ReleaseEvidenceWorkspace()
    {
        if (_evidenceWorkspace is null)
        {
            _session = _sourceSession;
            return;
        }

        var workspace = _evidenceWorkspace;
        _evidenceWorkspace = null;
        _session = _sourceSession;
        workspace.Dispose();
        _lastEvidenceResourcesReleased = workspace.ResourcesReleased;
        if (!_lastEvidenceResourcesReleased)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_RELEASE_FAILED resources_released=false");
        }
    }

    private StudioUx1AFSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId);
    }

    private bool SourceMatchesSnapshot(StudioUx1AFSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId;
    }

    private void CompleteEvidenceSourceAudit()
    {
        if (_evidenceSourceSnapshot is not { } snapshot)
        {
            return;
        }
        _lastEvidenceSourceUntouched = SourceMatchesSnapshot(snapshot);
        _evidenceSourceSnapshot = null;
        if (!_lastEvidenceSourceUntouched)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_SOURCE_CHANGED source_untouched=false");
        }
    }

    private static string RenderPlanPreview(StudioUx1AFPlanPreview preview)
    {
        var proposedPlan = preview.Plan;
        if (!preview.Succeeded || proposedPlan is null)
        {
            return $"Aperçu refusé sans écriture.\n\n{preview.Message}";
        }

        var plan = proposedPlan;
        var builder = new StringBuilder()
            .AppendLine(preview.Summary)
            .AppendLine()
            .AppendLine($"Fournisseur : {plan.ProviderKey} · coût estimé {preview.EstimatedCostUsd:0.0000} USD")
            .AppendLine($"Provenance : {plan.Provenance}")
            .AppendLine($"Empreinte du prompt : {ShortHash(preview.PromptSha256)} · " +
                        $"{preview.InputTokens + preview.OutputTokens} jeton(s)")
            .AppendLine($"Transport réseau utilisé : {(preview.NetworkAuthorized ? "autorisé" : "non")} · " +
                        $"contenu projet : {(preview.ProjectContentAuthorized ? "partagé" : "masqué")}")
            .AppendLine()
            .AppendLine("Topologie proposée :");
        foreach (var node in plan.Nodes)
        {
            builder.AppendLine(
                $"• {node.Kind} · {node.Label} · position {node.X:0.#}, {node.Z:0.#} · {node.ContentBlueprintIds.Count} contenu(s)");
        }
        builder.AppendLine().Append($"Connexions : {plan.Links.Count} · géométrie appliquée : non.");
        return builder.ToString();
    }

    private static string RenderBuildPreview(StudioUx1AFBuildPreview preview) => preview.Succeeded
        ? preview.Summary + "\n\nLa construction reste une politique validée; elle ne prétend pas exécuter un moteur ou un réseau."
        : $"Aperçu refusé sans écriture.\n\n{preview.Message}";

    private void ClearPlanPreview(string message)
    {
        _planPreview = null;
        _applyPlan.Disabled = true;
        _planSummary.Text = message;
        _planSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void ClearBuildPreview(string message)
    {
        _buildPreview = null;
        _applyBuild.Disabled = true;
        _buildSummary.Text = message;
        _buildSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetNeutralStatus(string message)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        StatusChanged?.Invoke(message, false);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Warning : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private static VBoxContainer AddCard(Container parent, string title, string description)
    {
        var panel = StudioStyle.Panel($"Ux1AF{title.Replace(" ", string.Empty)}", StudioStyle.Surface, 8);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        column.AddChild(StudioStyle.Label(description, 10, StudioStyle.Muted, wrap: true));
        return column;
    }

    private static OptionButton AddOption(Container parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        parent.AddChild(option);
        return option;
    }

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    private static TextEdit AddText(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var edit = new TextEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new Vector2(0, 68),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            ScrollFitContentHeight = true,
        };
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        edit.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 5));
        parent.AddChild(edit);
        return edit;
    }

    private static SpinBox AddSpin(
        Container parent,
        string label,
        double minimum,
        double maximum,
        double step,
        string suffix = "")
    {
        var group = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        group.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Suffix = suffix,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        group.AddChild(spin);
        parent.AddChild(group);
        return spin;
    }

    private static Label PreviewLabel(string text)
    {
        var label = StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);
        label.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        return label;
    }

    private static IReadOnlyList<string> Lines(string text) => text
        .Split(['\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string HumanPermission(AssistantPermissionLevel permission) => permission switch
    {
        AssistantPermissionLevel.ReadOnly => "lecture seule",
        AssistantPermissionLevel.Propose => "proposition",
        AssistantPermissionLevel.Configure => "configuration",
        AssistantPermissionLevel.ApplyApprovedPlan => "application approuvée",
        AssistantPermissionLevel.BoundedAutonomous => "autonomie bornée",
        _ => "inconnue",
    };

    private static string ShortHash(string value) => value.Length <= 12 ? value : value[..12] + "…";
}
