using System.Net;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private StudioUx1AFContext? _ux1AF;
    private StudioUx1AFApprovedPlan? _approvedUx1AFPlan;
    private StudioUx1AFApprovedBuild? _approvedUx1AFBuild;
    private readonly object _ux1AFPlanPreviewGate = new();
    private long _ux1AFPlanPreviewGeneration;

    internal StudioUx1AFWorkspaceSnapshot CreateUx1AFWorkspaceSnapshot()
    {
        if (!TryGetUx1AFContext(out var context, out var failure))
        {
            return new([], [], [], "Indisponible", "—", "—", AssistantPermissionLevel.ReadOnly,
                false, true, false, 0, false, failure);
        }

        var project = Project!;
        var areas = project.Areas
            .Select(area => new StudioUx1AFChoice(area.Id.Value, area.DisplayName))
            .OrderBy(area => area.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var kits = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Placeable)
            .Select(blueprint => new StudioUx1AFChoice(blueprint.Id.Value, blueprint.DisplayName))
            .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var plans = context.PlanHistory.State.Catalog.Plans
            .Select(plan => new StudioUx1AFPlanChoice(
                plan.Id,
                plan.Title,
                plan.ApprovedForConstruction,
                project.Areas.SelectMany(area => area.Entities).Any(entity =>
                    entity.Tags.Contains($"assistant-plan:{plan.Id:D}", StringComparer.Ordinal)),
                plan.ProviderKey,
                plan.EstimatedCostUsd))
            .OrderBy(plan => plan.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var provider = context.Profile.ActiveProvider;
        var ready = areas.Length > 0 && kits.Length > 0 && provider.Enabled;
        var message = !provider.Enabled
            ? "Le fournisseur Assistant actif est désactivé. Activez-le dans le profil avant de demander un plan."
            : areas.Length == 0
                ? "Créez une zone avant de préparer un plan assisté."
                : kits.Length == 0
                    ? "Ajoutez au moins un blueprint Placeable utilisable comme kit."
                    : "Sélectionnez explicitement une zone et un kit. Le brief et un contexte structurel borné sont transmis; les détails sensibles suivent la politique affichée.";
        return new(
            areas,
            kits,
            plans,
            provider.DisplayName,
            provider.Kind.ToString(),
            provider.Provenance,
            context.Profile.MaximumPermission,
            context.Profile.NetworkAllowed,
            provider.LocalOnly,
            context.Profile.ShareProjectContent,
            context.Lore.Entries.Count,
            ready,
            message);
    }

    internal async Task<StudioUx1AFPlanPreview> PreviewUx1AFPlanAsync(
        StudioUx1AFPlanDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var capturedDraft = SnapshotUx1AFDraft(draft);
        var draftFingerprint = FingerprintUx1AFDraft(capturedDraft);
        if (!TryGetUx1AFContext(out var context, out var failure))
        {
            CancelUx1AFPreview();
            return FailedUx1AFPlan(failure);
        }
        var previewGeneration = BeginUx1AFPlanPreview();

        var project = Project!;
        var area = project.Areas.SingleOrDefault(candidate => candidate.Id.Value == capturedDraft.AreaId);
        if (area is null)
        {
            return FailedUx1AFPlan("Choisissez explicitement une zone existante; aucune zone de secours n’est sélectionnée.");
        }

        var kit = project.Blueprints.SingleOrDefault(candidate =>
            candidate.Id.Value == capturedDraft.KitBlueprintId && candidate.Kind == BlueprintKind.Placeable);
        if (kit is null)
        {
            return FailedUx1AFPlan("Choisissez explicitement un kit Placeable existant; aucun kit de secours n’est sélectionné.");
        }

        if (string.IsNullOrWhiteSpace(capturedDraft.Title) || capturedDraft.Title.Trim().Length > 120)
        {
            return FailedUx1AFPlan("Donnez au plan un titre lisible de 1 à 120 caractères.");
        }

        if (capturedDraft.NodeCount is < 4 or > 24)
        {
            return FailedUx1AFPlan("Le plan doit contenir entre 4 et 24 espaces.");
        }

        if (capturedDraft.WidthMeters is < 8 or > 2048 || capturedDraft.DepthMeters is < 8 or > 2048 ||
            capturedDraft.WidthMeters > area.Dimensions.Width || capturedDraft.DepthMeters > area.Dimensions.Depth)
        {
            return FailedUx1AFPlan(
                $"Les dimensions doivent rester entre 8 m et la taille de la zone ({area.Dimensions.Width} × {area.Dimensions.Depth} m).");
        }

        if (capturedDraft.Constraints.Count == 0 || capturedDraft.Objectives.Count == 0 ||
            capturedDraft.Constraints.Any(string.IsNullOrWhiteSpace) ||
            capturedDraft.Objectives.Any(string.IsNullOrWhiteSpace))
        {
            return FailedUx1AFPlan("Ajoutez au moins une contrainte et un objectif non vides.");
        }

        var requestedCatalogPermission = capturedDraft.ApproveForConstruction ||
                                         context.PlanHistory.State.Catalog.Plans.Any(plan =>
                                             plan.ApprovedForConstruction)
            ? AssistantPermissionLevel.ApplyApprovedPlan
            : AssistantPermissionLevel.Configure;
        if (context.Profile.MaximumPermission < requestedCatalogPermission)
        {
            return FailedUx1AFPlan(
                "Le catalogue final contient ou demande un plan approuvé; élevez explicitement la permission maximale avant tout dry-run.");
        }

        var providerDefinition = context.Profile.ActiveProvider;
        if (!providerDefinition.Enabled)
        {
            return FailedUx1AFPlan("Le fournisseur Assistant sélectionné est désactivé.");
        }

        var brief = new AssistedAreaPlanBrief(
            Guid.NewGuid(),
            area.Id.Value,
            capturedDraft.Title.Trim(),
            capturedDraft.Archetype,
            capturedDraft.Seed,
            capturedDraft.WidthMeters,
            capturedDraft.DepthMeters,
            capturedDraft.NodeCount,
            [kit.Id.Value],
            NormalizeUx1AFLines(capturedDraft.Constraints),
            NormalizeUx1AFLines(capturedDraft.Objectives),
            [$"project:{project.Id}"],
            "studio:ux1-af:author-request");
        var baseProjectRevision = History!.State.Revision;
        var basePlanRevision = context.PlanHistory.State.Revision;
        if (!IsCurrentUx1AFPlanPreview(
                context,
                draft,
                draftFingerprint,
                previewGeneration,
                baseProjectRevision,
                basePlanRevision))
        {
            return StaleUx1AFPlanPreview();
        }
        AssistedAreaPlanProposal proposal;
        try
        {
            var provider = context.ProviderRegistry.Resolve(providerDefinition);
            proposal = await AssistedAreaPlanProposalService.ProposeAsync(
                brief,
                project,
                context.Lore,
                provider,
                providerDefinition,
                context.Profile.NetworkAllowed,
                context.Profile.ShareProjectContent,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!IsCurrentUx1AFPlanPreview(
                    context, draft, draftFingerprint, previewGeneration, baseProjectRevision, basePlanRevision))
            {
                return StaleUx1AFPlanPreview();
            }
            return FailedUx1AFPlan("La demande de plan a été annulée; le module reste inchangé.");
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentUx1AFPlanPreview(
                    context, draft, draftFingerprint, previewGeneration, baseProjectRevision, basePlanRevision))
            {
                return StaleUx1AFPlanPreview();
            }
            return FailedUx1AFPlan(
                "Le fournisseur a interrompu la demande de plan; le module reste inchangé.");
        }
        catch (AssistantProviderTransportException exception)
        {
            if (!IsCurrentUx1AFPlanPreview(
                    context, draft, draftFingerprint, previewGeneration, baseProjectRevision, basePlanRevision))
            {
                return StaleUx1AFPlanPreview();
            }
            return FailedUx1AFPlan(HumanizeUx1AFDiagnostic(exception.Code));
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException or NotSupportedException)
        {
            if (!IsCurrentUx1AFPlanPreview(
                    context, draft, draftFingerprint, previewGeneration, baseProjectRevision, basePlanRevision))
            {
                return StaleUx1AFPlanPreview();
            }
            return FailedUx1AFPlan(
                "Le fournisseur configuré n’a pas pu produire un plan valide. Vérifiez sa disponibilité, sa politique réseau et ses identifiants d’environnement.");
        }

        if (!IsCurrentUx1AFPlanPreview(
                context,
                draft,
                draftFingerprint,
                previewGeneration,
                baseProjectRevision,
                basePlanRevision))
        {
            return StaleUx1AFPlanPreview();
        }

        var proposedPlan = proposal.Plan;
        if (!proposal.Succeeded || proposedPlan is null)
        {
            return FailedUx1AFPlan(HumanizeUx1AFDiagnostic(proposal.Diagnostics.FirstOrDefault()?.Code));
        }

        var plan = proposedPlan.WithApproval(
            capturedDraft.ApproveForConstruction,
            capturedDraft.ApproveForConstruction
                ? $"author-approved:studio:ux1-af:{proposedPlan.Provenance}"
                : proposedPlan.Provenance);
        var catalog = new AssistedAreaPlanCatalog(
            context.PlanHistory.State.Catalog.Id,
            AssistedAreaPlanCatalog.CurrentSchemaVersion,
            context.PlanHistory.State.Catalog.Plans.Where(candidate => candidate.Id != plan.Id).Append(plan).ToArray(),
            "studio:ux1-af:governed-catalog");
        var permission = catalog.Plans.Any(candidate => candidate.ApprovedForConstruction)
            ? AssistantPermissionLevel.ApplyApprovedPlan
            : AssistantPermissionLevel.Configure;
        if (context.Profile.MaximumPermission < permission)
        {
            return FailedUx1AFPlan(
                "Le catalogue final contient déjà un plan approuvé et exige la permission d’application approuvée.");
        }
        var command = new SetAssistedAreaPlansCommand(
            CommandId.From(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            basePlanRevision,
            catalog,
            permission,
            DryRun: true);
        AssistedAreaPlanCommandResult dryRun;
        lock (_ux1AFPlanPreviewGate)
        {
            if (!IsCurrentUx1AFPlanPreview(
                    context,
                    draft,
                    draftFingerprint,
                    previewGeneration,
                    baseProjectRevision,
                    basePlanRevision))
            {
                return FailedUx1AFPlan(
                    "Le brouillon a changé avant le dry-run; aucun token d’application n’a été créé.");
            }

            dryRun = context.SetPlans.Handle(context.PlanHistory.State, command, project);
            if (dryRun.Succeeded && IsCurrentUx1AFPlanPreview(
                    context,
                    draft,
                    draftFingerprint,
                    previewGeneration,
                    baseProjectRevision,
                    basePlanRevision))
            {
                _approvedUx1AFPlan = new(
                    catalog,
                    command,
                    basePlanRevision,
                    baseProjectRevision,
                    plan.Id,
                    previewGeneration,
                    draftFingerprint);
            }
        }

        if (!dryRun.Succeeded)
        {
            return FailedUx1AFPlan(HumanizeUx1AFDiagnostic(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        if (_approvedUx1AFPlan is not { } approved || approved.Generation != previewGeneration ||
            !string.Equals(approved.DraftFingerprint, draftFingerprint, StringComparison.Ordinal))
        {
            return FailedUx1AFPlan(
                "L’aperçu a été invalidé pendant le dry-run; aucun token d’application actif n’est conservé.");
        }
        var summary =
            $"Aperçu sans écriture · {plan.Nodes.Count} espace(s) · {plan.Links.Count} liaison(s) · " +
            $"{plan.Nodes.Sum(node => node.ContentBlueprintIds.Count)} placement(s) envisagé(s) · " +
            $"géométrie appliquée : non · approbation : {(plan.ApprovedForConstruction ? "oui" : "non")}.";
        return new(
            true,
            "Plan prévisualisé; le catalogue et la zone sont inchangés.",
            summary,
            plan,
            proposal.SemanticDiff,
            proposal.PromptSha256,
            proposal.InputTokens,
            proposal.OutputTokens,
            proposal.EstimatedCostUsd,
            proposal.ProviderProvenance,
            GeometryApplied: false,
            NetworkAuthorized: context.Profile.NetworkAllowed &&
                               providerDefinition.Kind != AssistantProviderKind.DeterministicOffline,
            ProjectContentAuthorized: context.Profile.ShareProjectContent,
            CanApply: true);
    }

    internal StudioUx1AFActionResult ApplyApprovedUx1AFPlan()
    {
        if (_approvedUx1AFPlan is not { } approval)
        {
            return new(false, "Prévisualisez de nouveau le plan avant de l’enregistrer.");
        }

        if (!TryGetUx1AFContext(out var context, out var failure))
        {
            return new(false, failure);
        }

        lock (_ux1AFPlanPreviewGate)
        {
            if (approval.Generation != _ux1AFPlanPreviewGeneration)
            {
                _approvedUx1AFPlan = null;
                return new(false, "Le brouillon a été invalidé depuis l’aperçu; recommencez le dry-run.");
            }
        }

        if (context.PlanHistory.State.Revision != approval.PlanRevision)
        {
            _approvedUx1AFPlan = null;
            return new(false, "Le catalogue de plans a changé depuis l’aperçu; recommencez le dry-run.");
        }

        if (History!.State.Revision != approval.ProjectRevision)
        {
            _approvedUx1AFPlan = null;
            return new(false, "Le projet a changé depuis l’aperçu; recommencez le dry-run avant d’enregistrer le plan.");
        }

        var applied = context.SetPlans.Handle(
            context.PlanHistory.State,
            approval.Command with { DryRun = false },
            Project!);
        _approvedUx1AFPlan = null;
        if (!applied.Succeeded)
        {
            return new(false, HumanizeUx1AFDiagnostic(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        var saved = AssistedAreaPlanStore.Save(Session!.RootDirectory, applied.State.Catalog);
        if (!saved.Succeeded)
        {
            return new(false, "Le plan était valide, mais son document n’a pas pu être enregistré atomiquement.");
        }

        context.PlanHistory.Record(applied);
        return new(true, "Plan approuvé et enregistré dans son document gouverné; la géométrie reste inchangée.");
    }

    internal StudioUx1AFActionResult UndoUx1AFPlan()
    {
        _approvedUx1AFPlan = null;
        if (!TryGetUx1AFContext(out var context, out var failure))
        {
            return new(false, failure);
        }

        if (!context.PlanHistory.Undo())
        {
            return new(false, "Aucune modification du catalogue de plans à annuler.");
        }

        var saved = AssistedAreaPlanStore.Save(Session!.RootDirectory, context.PlanHistory.State.Catalog);
        if (!saved.Succeeded)
        {
            context.PlanHistory.Redo();
            return new(false, "L’annulation n’a pas pu être enregistrée; le catalogue précédent a été conservé.");
        }

        return new(true, "Modification du catalogue de plans annulée; le projet géométrique est inchangé.");
    }

    internal StudioUx1AFActionResult RedoUx1AFPlan()
    {
        _approvedUx1AFPlan = null;
        if (!TryGetUx1AFContext(out var context, out var failure))
        {
            return new(false, failure);
        }

        if (!context.PlanHistory.Redo())
        {
            return new(false, "Aucune modification du catalogue de plans à rétablir.");
        }

        var saved = AssistedAreaPlanStore.Save(Session!.RootDirectory, context.PlanHistory.State.Catalog);
        if (!saved.Succeeded)
        {
            context.PlanHistory.Undo();
            return new(false, "Le rétablissement n’a pas pu être enregistré; le catalogue précédent a été conservé.");
        }

        return new(true, "Modification du catalogue de plans rétablie; la géométrie est inchangée.");
    }

    internal StudioUx1AFBuildPreview PreviewUx1AFBuild(Guid planId)
    {
        _approvedUx1AFBuild = null;
        if (planId == Guid.Empty)
        {
            return FailedUx1AFBuild("Choisissez explicitement un plan approuvé; aucun plan de secours n’est sélectionné.");
        }

        if (!TryGetUx1AFContext(out var context, out var failure))
        {
            return FailedUx1AFBuild(failure);
        }

        var plan = context.PlanHistory.State.Catalog.Plans.SingleOrDefault(candidate => candidate.Id == planId);
        if (plan is null)
        {
            return FailedUx1AFBuild("Le plan sélectionné n’existe plus dans le catalogue.");
        }

        if (!plan.ApprovedForConstruction)
        {
            return FailedUx1AFBuild("Ce plan reste un brouillon. Une approbation humaine explicite est requise.");
        }

        if (context.Profile.MaximumPermission < AssistantPermissionLevel.ApplyApprovedPlan)
        {
            return FailedUx1AFBuild("Le profil Assistant n’autorise pas la construction d’un plan approuvé.");
        }

        var command = new BuildAssistedAreaPlanCommand(
            CommandId.From(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            History!.State.Revision,
            plan.Id,
            AssistantPermissionLevel.ApplyApprovedPlan,
            DryRun: true);
        var dryRun = context.BuildPlan.Handle(History.State, command, context.PlanHistory.State.Catalog);
        if (!dryRun.Succeeded)
        {
            return FailedUx1AFBuild(HumanizeUx1AFDiagnostic(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }

        _approvedUx1AFBuild = new(
            command,
            History.State.Revision,
            context.PlanHistory.State.Revision,
            plan.Id);
        return new(
            true,
            "Construction prévisualisée; aucun objet n’a été placé.",
            $"Aperçu sans écriture · {dryRun.EntityIds.Count} placement(s) · navigation avant {dryRun.NavigationDiagnosticsBefore}, " +
            $"après {dryRun.NavigationDiagnosticsAfter} · éclairage {(dryRun.LightingPass ? "valide" : "invalide")} · undo atomique.",
            plan.Id,
            dryRun.EntityIds.Count,
            dryRun.NavigationDiagnosticsBefore,
            dryRun.NavigationDiagnosticsAfter,
            dryRun.LightingPass,
            GeometryApplied: false,
            CanApply: true);
    }

    internal StudioUx1AFActionResult ApplyApprovedUx1AFBuild()
    {
        if (_approvedUx1AFBuild is not { } approval)
        {
            return new(false, "Prévisualisez de nouveau la construction avant de l’appliquer.");
        }

        if (!TryGetUx1AFContext(out var context, out var failure))
        {
            return new(false, failure);
        }

        if (History!.State.Revision != approval.ProjectRevision)
        {
            _approvedUx1AFBuild = null;
            return new(false, "Le projet a changé depuis l’aperçu de construction; recommencez le dry-run.");
        }

        if (context.PlanHistory.State.Revision != approval.PlanRevision)
        {
            _approvedUx1AFBuild = null;
            return new(false, "Le catalogue de plans a changé depuis l’aperçu de construction; recommencez le dry-run.");
        }

        var applied = context.BuildPlan.Handle(
            History.State,
            approval.Command with { DryRun = false },
            context.PlanHistory.State.Catalog);
        _approvedUx1AFBuild = null;
        if (!applied.Succeeded)
        {
            return new(false, HumanizeUx1AFDiagnostic(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        var saved = Session!.Save(applied.Command.State.Project);
        if (!saved.Succeeded)
        {
            return new(false, "La construction était valide, mais le projet n’a pas pu être enregistré atomiquement.");
        }

        History.Record(applied.Command);
        InvalidateContentIndexes();
        return new(true,
            $"Construction appliquée et enregistrée · {applied.EntityIds.Count} objet(s) · annulation projet atomique disponible.");
    }

    internal void CancelUx1AFPreview()
    {
        lock (_ux1AFPlanPreviewGate)
        {
            _ux1AFPlanPreviewGeneration++;
            _approvedUx1AFPlan = null;
            _approvedUx1AFBuild = null;
        }
    }

    internal void CloseUx1AF()
    {
        CancelUx1AFPreview();
        _ux1AF?.Dispose();
        _ux1AF = null;
    }

    internal StudioUx1AFEvidenceWorkspace? CreateUx1AFEvidenceWorkspace()
    {
        var rootPath = StudioUx1AFEvidenceWorkspace.CreateRootPath();
        var evidenceSession = new StudioSessionController();
        var workspace = new StudioUx1AFEvidenceWorkspace(evidenceSession, rootPath);
        try
        {
            var areaId = AreaId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000001"));
            var spawnId = BlueprintId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000002"));
            var kitId = BlueprintId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000003"));
            var spawn = new EntityInstance(
                EntityId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000004")),
                spawnId,
                new TransformValue(new Vector3Value(0, .8f, 0), QuaternionValue.Identity, Vector3Value.One),
                ["player"]);
            var area = new AreaDefinition(
                areaId,
                "atelier-plan-assiste-ux1-af",
                "Atelier de plan assisté",
                [spawn],
                AreaKind.Interior,
                new AreaDimensions(64, 64));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1af0000-0000-4000-8000-000000000005")),
                "Preuve Studio UX1-AF",
                [
                    new BlueprintDefinition(spawnId, "spawn-preuve-ux1-af", "Point de départ", BlueprintKind.PlayerSpawn),
                    new BlueprintDefinition(kitId, "kit-pierre-preuve-ux1-af", "Kit pierre de preuve", BlueprintKind.Placeable),
                ],
                [area]);
            var created = ProjectSession.Create(rootPath, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_CREATE_FAILED diagnostics=" +
                                  string.Join(" | ", created.Diagnostics.Select(diagnostic => diagnostic.Code)));
                workspace.Dispose();
                return null;
            }

            var defaults = AssistantWorkspaceProfile.CreateDefault();
            var evidenceProfile = new AssistantWorkspaceProfile(
                defaults.Id,
                AssistantWorkspaceProfile.CurrentSchemaVersion,
                defaults.ActiveProviderId,
                defaults.Providers,
                defaults.Prompts,
                AssistantPermissionLevel.ApplyApprovedPlan,
                networkAllowed: false,
                shareProjectContent: false,
                provenance: "evidence:studio:ux1-af",
                agentWorkflow: defaults.AgentWorkflow,
                testAutomation: defaults.TestAutomation,
                combatSimulation: defaults.CombatSimulation);
            var profileSaved = AssistantWorkspaceProfileStore.Save(rootPath, evidenceProfile);
            if (!profileSaved.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_PROFILE_FAILED diagnostics=" +
                                  string.Join(" | ", profileSaved.Diagnostics.Select(diagnostic => diagnostic.Code)));
                workspace.Dispose();
                return null;
            }

            var opened = evidenceSession.Open(rootPath, areaId.Value.ToString("D"));
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_OPEN_FAILED diagnostics=" +
                                  string.Join(" | ", opened.Diagnostics.Select(diagnostic => diagnostic.Code)));
                workspace.Dispose();
                return null;
            }

            return workspace;
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_EXCEPTION type={exception.GetType().Name}");
            workspace.Dispose();
            return null;
        }
    }

    internal StudioUx1AFSmokeResult RunUx1AFContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceProject is null || sourceHistory is null)
        {
            return FailedUx1AFSmoke("Aucune session source n’est ouverte pour la preuve d’isolation.");
        }
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceDirty = IsDirty;
        var sourceAreaId = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;

        var workspace = CreateUx1AFEvidenceWorkspace();
        if (workspace is null)
        {
            return FailedUx1AFSmoke("Le workspace de preuve hors ligne n’a pas pu être créé.");
        }

        try
        {
            var editing = workspace.Session;
            var snapshot = editing.CreateUx1AFWorkspaceSnapshot();
            var draft = new StudioUx1AFPlanDraft(
                snapshot.Areas.Single().Id,
                snapshot.Kits.Single().Id,
                "Crypte du témoin hors ligne",
                AssistedAreaArchetype.Crypt,
                20260817,
                48,
                48,
                4,
                ["Conserver un trajet navigable entre l’entrée et la sortie."],
                ["Atteindre l’objectif central puis revenir en sécurité."],
                ApproveForConstruction: true);
            var invalid = editing.PreviewUx1AFPlanAsync(draft with { AreaId = Guid.Empty })
                .GetAwaiter().GetResult();
            var explicitSelection = !invalid.Succeeded;

            var projectBeforePlan = editing.Project;
            var projectHistory = editing.History!;
            var projectRevisionBeforePlan = projectHistory.State.Revision;
            var projectJournalBeforePlan = projectHistory.Journal.ToArray();
            var plansBefore = editing.CreateUx1AFWorkspaceSnapshot().Plans.Count;
            var planPreview = editing.PreviewUx1AFPlanAsync(draft).GetAwaiter().GetResult();
            var planDryRunPure = planPreview.Succeeded && !planPreview.GeometryApplied &&
                                 ReferenceEquals(projectBeforePlan, editing.Project) &&
                                 projectHistory.State.Revision == projectRevisionBeforePlan &&
                                 projectHistory.Journal.SequenceEqual(projectJournalBeforePlan) &&
                                 editing.CreateUx1AFWorkspaceSnapshot().Plans.Count == plansBefore;

            var spawn = editing.ActiveArea!.Entities.Single(entity => entity.Tags.Contains("player"));
            var moved = new TransformValue(
                new Vector3Value(
                    spawn.Transform.Position.X + 1,
                    spawn.Transform.Position.Y,
                    spawn.Transform.Position.Z),
                spawn.Transform.Rotation,
                spawn.Transform.Scale);
            var neighborMutation = editing.TransformEntity(spawn.Id, moved);
            var staleProjectPlan = editing.ApplyApprovedUx1AFPlan();
            var projectRevisionGuard = neighborMutation.Succeeded && !staleProjectPlan.Succeeded &&
                                       staleProjectPlan.Message.Contains("projet", StringComparison.OrdinalIgnoreCase);
            editing.Undo();

            planPreview = editing.PreviewUx1AFPlanAsync(draft).GetAwaiter().GetResult();
            var projectRevisionBeforeCatalogApply = editing.History!.State.Revision;
            var journalBeforeCatalogApply = editing.History.Journal.ToArray();
            var planApplied = editing.ApplyApprovedUx1AFPlan();
            var appliedPlan = editing.CreateUx1AFWorkspaceSnapshot().Plans.SingleOrDefault();
            var planApprovalPersisted = planPreview.Succeeded && planApplied.Succeeded &&
                                        appliedPlan is { Approved: true } &&
                                        AssistedAreaPlanStore.Load(workspace.RootPath).Catalog.Plans.Any(plan =>
                                            plan.Id == appliedPlan.Id && plan.ApprovedForConstruction);
            var planHistorySeparated = editing.History.State.Revision == projectRevisionBeforeCatalogApply &&
                                       editing.History.Journal.SequenceEqual(journalBeforeCatalogApply);
            var planUndo = editing.UndoUx1AFPlan();
            var planRemoved = editing.CreateUx1AFWorkspaceSnapshot().Plans.Count == plansBefore;
            var planRedo = editing.RedoUx1AFPlan();
            appliedPlan = editing.CreateUx1AFWorkspaceSnapshot().Plans.SingleOrDefault();
            var planUndoRedo = planUndo.Succeeded && planRemoved && planRedo.Succeeded && appliedPlan is not null;
            if (appliedPlan is null)
            {
                throw new InvalidOperationException("The approved evidence plan was not restored.");
            }

            var stalePlanPreview = editing.PreviewUx1AFPlanAsync(draft with { Title = "Plan périmé" })
                .GetAwaiter().GetResult();
            var stalePlanContext = editing._ux1AF ??
                                   throw new InvalidOperationException("The stale plan evidence context is unavailable.");
            var stalePlanUndo = stalePlanContext.PlanHistory.Undo();
            var stalePlanRedo = stalePlanContext.PlanHistory.Redo();
            var stalePlanApply = editing.ApplyApprovedUx1AFPlan();
            var planRevisionGuard = stalePlanPreview.Succeeded && stalePlanUndo && stalePlanRedo &&
                                    !stalePlanApply.Succeeded &&
                                    stalePlanApply.Message.Contains("catalogue", StringComparison.OrdinalIgnoreCase);

            var mixedCatalogPreview = editing.PreviewUx1AFPlanAsync(draft with
                {
                    Title = "Brouillon auprès d’un plan approuvé",
                    ApproveForConstruction = false,
                })
                .GetAwaiter().GetResult();
            var mixedApproval = editing._approvedUx1AFPlan;
            var mixedPlan = mixedCatalogPreview.Plan;
            var mixedCatalogPermission = mixedCatalogPreview.Succeeded &&
                                         mixedPlan is { ApprovedForConstruction: false } &&
                                         mixedApproval is not null &&
                                         mixedApproval.Command.Permission == AssistantPermissionLevel.ApplyApprovedPlan &&
                                         mixedApproval.Command.Catalog.Plans.Any(plan => plan.ApprovedForConstruction) &&
                                         mixedApproval.Command.Catalog.Plans.Any(plan =>
                                             !plan.ApprovedForConstruction &&
                                             plan.Id == mixedPlan.Id);
            editing.CancelUx1AFPreview();

            var asyncContext = editing._ux1AF ??
                               throw new InvalidOperationException("The async evidence context is unavailable.");
            var mutableConstraints = new[] { "Conserver le brouillon exact pendant l’attente." };
            var mutableDraft = draft with
            {
                Title = "Aperçu asynchrone périmé",
                Constraints = mutableConstraints,
            };
            var capturedMutableDraft = SnapshotUx1AFDraft(mutableDraft);
            var mutableFingerprint = FingerprintUx1AFDraft(capturedMutableDraft);
            var asyncProjectRevision = editing.History!.State.Revision;
            var asyncPlanRevision = asyncContext.PlanHistory.State.Revision;
            var mutableGeneration = editing.BeginUx1AFPlanPreview();
            mutableConstraints[0] = "Brouillon modifié pendant l’attente.";
            var fingerprintRejected = !editing.IsCurrentUx1AFPlanPreview(
                asyncContext,
                mutableDraft,
                mutableFingerprint,
                mutableGeneration,
                asyncProjectRevision,
                asyncPlanRevision);
            var canceledGeneration = editing.BeginUx1AFPlanPreview();
            editing.CancelUx1AFPreview();
            var cancellationRejected = !editing.IsCurrentUx1AFPlanPreview(
                asyncContext,
                capturedMutableDraft,
                mutableFingerprint,
                canceledGeneration,
                asyncProjectRevision,
                asyncPlanRevision);
            var revisionGeneration = editing.BeginUx1AFPlanPreview();
            var revisionRejected = !editing.IsCurrentUx1AFPlanPreview(
                asyncContext,
                capturedMutableDraft,
                mutableFingerprint,
                revisionGeneration,
                asyncProjectRevision.Next(),
                asyncPlanRevision);
            editing.CancelUx1AFPreview();
            var asyncStaleBlocked = fingerprintRejected && cancellationRejected && revisionRejected &&
                                    editing._approvedUx1AFPlan is null;

            var projectBeforeBuild = editing.Project;
            var projectRevisionBeforeBuild = editing.History!.State.Revision;
            var journalBeforeBuild = editing.History.Journal.ToArray();
            var planContext = editing._ux1AF ??
                              throw new InvalidOperationException("The evidence plan context is unavailable.");
            var planRevisionBeforeBuild = planContext.PlanHistory.State.Revision;
            var buildPreview = editing.PreviewUx1AFBuild(appliedPlan.Id);
            var buildDryRunPure = buildPreview.Succeeded && !buildPreview.GeometryApplied &&
                                  ReferenceEquals(projectBeforeBuild, editing.Project) &&
                                  editing.History.State.Revision == projectRevisionBeforeBuild &&
                                  editing.History.Journal.SequenceEqual(journalBeforeBuild) &&
                                  planContext.PlanHistory.State.Revision == planRevisionBeforeBuild;

            var buildNeighborMutation = editing.TransformEntity(spawn.Id, new TransformValue(
                new Vector3Value(
                    spawn.Transform.Position.X + 2,
                    spawn.Transform.Position.Y,
                    spawn.Transform.Position.Z),
                spawn.Transform.Rotation,
                spawn.Transform.Scale));
            var staleProjectBuildApply = editing.ApplyApprovedUx1AFBuild();
            var buildProjectRevisionGuard = buildNeighborMutation.Succeeded &&
                                            !staleProjectBuildApply.Succeeded &&
                                            staleProjectBuildApply.Message.Contains(
                                                "projet",
                                                StringComparison.OrdinalIgnoreCase);
            projectRevisionGuard &= buildProjectRevisionGuard;
            editing.Undo();

            buildPreview = editing.PreviewUx1AFBuild(appliedPlan.Id);
            editing.UndoUx1AFPlan();
            editing.RedoUx1AFPlan();
            var staleBuildApply = editing.ApplyApprovedUx1AFBuild();
            var buildRevisionGuard = !staleBuildApply.Succeeded &&
                                     staleBuildApply.Message.Contains("catalogue", StringComparison.OrdinalIgnoreCase);

            buildPreview = editing.PreviewUx1AFBuild(appliedPlan.Id);
            planContext = editing._ux1AF ??
                          throw new InvalidOperationException("The evidence plan context is unavailable.");
            var planRevisionBeforeBuildApply = planContext.PlanHistory.State.Revision;
            var planCatalogBeforeBuild = planContext.PlanHistory.State.Catalog;
            var entityCountBeforeBuild = editing.ActiveArea!.Entities.Count;
            var buildApplied = editing.ApplyApprovedUx1AFBuild();
            var builtMarker = $"assistant-plan:{appliedPlan.Id:D}";
            var entityCountAfterBuild = editing.ActiveArea!.Entities.Count;
            var buildAppliedGeometry = buildPreview.Succeeded && buildApplied.Succeeded &&
                                       entityCountAfterBuild == entityCountBeforeBuild + buildPreview.PlacementCount &&
                                       editing.ActiveArea.Entities.Any(entity =>
                                           entity.Tags.Contains(builtMarker, StringComparer.Ordinal));
            var buildHistorySeparated = planContext.PlanHistory.State.Revision == planRevisionBeforeBuildApply &&
                                        ReferenceEquals(planCatalogBeforeBuild, planContext.PlanHistory.State.Catalog);
            var buildUndo = editing.Undo();
            var geometryRemoved = editing.ActiveArea!.Entities.Count == entityCountBeforeBuild;
            var buildRedo = editing.Redo();
            var geometryRestored = editing.ActiveArea!.Entities.Count == entityCountAfterBuild;
            var buildUndoRedo = buildUndo.Succeeded && geometryRemoved && buildRedo.Succeeded && geometryRestored;
            var saved = editing.Save();

            var reopened = new StudioSessionController();
            var reopen = reopened.Open(workspace.RootPath);
            var reopenedSnapshot = reopen.Succeeded
                ? reopened.CreateUx1AFWorkspaceSnapshot()
                : new StudioUx1AFWorkspaceSnapshot([], [], [], "", "", "", AssistantPermissionLevel.ReadOnly,
                    false, true, false, 0, false, "");
            var saveReopen = reopen.Succeeded && saved.Succeeded &&
                             reopenedSnapshot.Plans.Any(plan => plan.Id == appliedPlan.Id && plan.Approved) &&
                             reopened.Project!.Areas.SelectMany(area => area.Entities).Any(entity =>
                                 entity.Tags.Contains(builtMarker, StringComparer.Ordinal));
            reopened.CloseUx1AF();
            reopened.Close();

            var privacyVisible = !snapshot.NetworkAllowed && snapshot.LocalOnly && !snapshot.ShareProjectContent;
            var provenanceVisible = !string.IsNullOrWhiteSpace(snapshot.ProviderProvenance) &&
                                    planPreview.Plan?.Provenance.Contains(
                                        "map-contract-",
                                        StringComparison.Ordinal) == true;
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  sourceHistory.State.Revision == sourceRevision &&
                                  sourceHistory.Journal.SequenceEqual(sourceJournal) &&
                                  sourceHistory.CanUndo == sourceCanUndo &&
                                  sourceHistory.CanRedo == sourceCanRedo &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceAreaId &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected;
            var result = new StudioUx1AFSmokeResult(
                explicitSelection && privacyVisible && provenanceVisible && planDryRunPure &&
                planApprovalPersisted && planHistorySeparated && planUndoRedo && planRevisionGuard &&
                asyncStaleBlocked && mixedCatalogPermission && projectRevisionGuard &&
                buildDryRunPure && buildAppliedGeometry && buildHistorySeparated &&
                buildUndoRedo && buildRevisionGuard && saveReopen && sourceUntouched,
                explicitSelection,
                privacyVisible,
                provenanceVisible,
                planDryRunPure,
                planApprovalPersisted,
                planHistorySeparated,
                planUndoRedo,
                planRevisionGuard,
                asyncStaleBlocked,
                mixedCatalogPermission,
                projectRevisionGuard,
                buildDryRunPure,
                buildAppliedGeometry,
                buildHistorySeparated,
                buildUndoRedo,
                buildRevisionGuard,
                saveReopen,
                sourceUntouched,
                ResourcesReleased: false,
                Message: "Preuve contractuelle UX1-AF exécutée dans un workspace temporaire hors ligne.");
            workspace.Dispose();
            return result with
            {
                Succeeded = result.Succeeded && workspace.ResourcesReleased,
                ResourcesReleased = workspace.ResourcesReleased,
            };
        }
        catch (Exception exception)
        {
            workspace.Dispose();
            return FailedUx1AFSmoke(
                $"La preuve contractuelle isolée a échoué ({exception.GetType().Name}).",
                workspace.ResourcesReleased);
        }
    }

    private bool TryGetUx1AFContext(out StudioUx1AFContext context, out string failure)
    {
        context = null!;
        failure = string.Empty;
        if (Session is null || History is null || Project is null)
        {
            failure = "Aucun module n’est ouvert.";
            return false;
        }

        if (_ux1AF is not null && ReferenceEquals(_ux1AF.SessionIdentity, Session))
        {
            context = _ux1AF;
            return true;
        }

        CloseUx1AF();
        var plans = AssistedAreaPlanStore.Load(Session.RootDirectory);
        var profile = AssistantWorkspaceProfileStore.Load(Session.RootDirectory);
        var lore = LoreIndexStore.Load(Session.RootDirectory);
        var diagnostics = plans.Diagnostics.Concat(profile.Diagnostics).Concat(lore.Diagnostics).ToArray();
        if (diagnostics.Length > 0)
        {
            failure = "Les documents Assistant ne peuvent pas être lus sans ambiguïté; corrigez-les avant toute mutation.";
            return false;
        }

        try
        {
            _ = profile.Profile.ActiveProvider;
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            var client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
            _ux1AF = new StudioUx1AFContext(
                Session,
                profile.Profile,
                lore.Index,
                new AssistedAreaPlanCommandHistory(plans.Catalog),
                new SetAssistedAreaPlansHandler(new SystemClock()),
                new BuildAssistedAreaPlanHandler(new SystemClock()),
                AssistantProviderRegistry.CreateWithLocalTransports(
                    client,
                    requestTimeout: TimeSpan.FromSeconds(30)),
                client);
            context = _ux1AF;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            failure = "Le profil Assistant actif est incohérent; aucune proposition ni mutation n’a été lancée.";
            CloseUx1AF();
            return false;
        }
    }

    private static StudioUx1AFPlanPreview FailedUx1AFPlan(string message) => new(
        false,
        message,
        "Aucun aperçu valide; le catalogue et la géométrie restent inchangés.",
        null,
        "no-change",
        string.Empty,
        0,
        0,
        0,
        string.Empty,
        GeometryApplied: false,
        NetworkAuthorized: false,
        ProjectContentAuthorized: false,
        CanApply: false);

    private static StudioUx1AFPlanPreview StaleUx1AFPlanPreview() => FailedUx1AFPlan(
        "La réponse reçue correspond à un brouillon ou une révision périmée; aucun token d’application n’a été créé.");

    private static StudioUx1AFBuildPreview FailedUx1AFBuild(string message) => new(
        false,
        message,
        "Aucune construction valide; la zone reste inchangée.",
        Guid.Empty,
        0,
        0,
        0,
        LightingPass: false,
        GeometryApplied: false,
        CanApply: false);

    private static StudioUx1AFSmokeResult FailedUx1AFSmoke(string message, bool resourcesReleased = false) => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, resourcesReleased, message);

    private static IReadOnlyList<string> NormalizeUx1AFLines(IEnumerable<string> values) => values
        .Select(value => (value ?? string.Empty).Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private long BeginUx1AFPlanPreview()
    {
        lock (_ux1AFPlanPreviewGate)
        {
            _approvedUx1AFPlan = null;
            _approvedUx1AFBuild = null;
            return ++_ux1AFPlanPreviewGeneration;
        }
    }

    private bool IsCurrentUx1AFPlanPreview(
        StudioUx1AFContext context,
        StudioUx1AFPlanDraft sourceDraft,
        string draftFingerprint,
        long generation,
        ProjectRevision projectRevision,
        AssistedAreaPlanRevision planRevision)
    {
        lock (_ux1AFPlanPreviewGate)
        {
            if (generation != _ux1AFPlanPreviewGeneration)
            {
                return false;
            }
        }

        try
        {
            return ReferenceEquals(context.SessionIdentity, Session) &&
                   History?.State.Revision == projectRevision &&
                   context.PlanHistory.State.Revision == planRevision &&
                   string.Equals(
                       FingerprintUx1AFDraft(sourceDraft),
                       draftFingerprint,
                       StringComparison.Ordinal);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static StudioUx1AFPlanDraft SnapshotUx1AFDraft(StudioUx1AFPlanDraft draft) => new(
        draft.AreaId,
        draft.KitBlueprintId,
        draft.Title ?? string.Empty,
        draft.Archetype,
        draft.Seed,
        draft.WidthMeters,
        draft.DepthMeters,
        draft.NodeCount,
        draft.Constraints.Select(value => value ?? string.Empty).ToArray(),
        draft.Objectives.Select(value => value ?? string.Empty).ToArray(),
        draft.ApproveForConstruction);

    private static string FingerprintUx1AFDraft(StudioUx1AFPlanDraft draft)
    {
        var value = new StringBuilder()
            .Append(draft.AreaId.ToString("D")).Append('|')
            .Append(draft.KitBlueprintId.ToString("D")).Append('|')
            .Append(draft.Archetype).Append('|')
            .Append(draft.Seed).Append('|')
            .Append(BitConverter.SingleToInt32Bits(draft.WidthMeters)).Append('|')
            .Append(BitConverter.SingleToInt32Bits(draft.DepthMeters)).Append('|')
            .Append(draft.NodeCount).Append('|')
            .Append(draft.ApproveForConstruction).Append('|');
        AppendUx1AFFingerprintValue(value, draft.Title);
        foreach (var constraint in draft.Constraints)
        {
            AppendUx1AFFingerprintValue(value, constraint);
        }
        value.Append("|objectives|");
        foreach (var objective in draft.Objectives)
        {
            AppendUx1AFFingerprintValue(value, objective);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendUx1AFFingerprintValue(StringBuilder builder, string? value)
    {
        var normalized = value ?? string.Empty;
        builder.Append(normalized.Length).Append(':').Append(normalized).Append('|');
    }

    private static string HumanizeUx1AFDiagnostic(string? code) => code switch
    {
        null or "" => "Le contrat canonique a refusé cette opération; le module reste inchangé.",
        AssistedAreaPlanGenerator.BriefInvalidCode =>
            "Le titre, les dimensions, les espaces, les contraintes ou les objectifs du plan sont incomplets.",
        AssistedAreaPlanProjectValidator.AreaMissingCode =>
            "La zone sélectionnée n’existe plus; actualisez la liste et sélectionnez-la de nouveau.",
        AssistedAreaPlanProjectValidator.BlueprintMissingCode =>
            "Le kit sélectionné n’existe plus; actualisez la liste et sélectionnez-le de nouveau.",
        AssistedAreaPlanProjectValidator.LoreSourceMissingCode =>
            "Une source de contexte n’est plus résolue; corrigez l’index de lore avant de continuer.",
        SetAssistedAreaPlansHandler.RevisionConflictCode or BuildAssistedAreaPlanHandler.RevisionConflictCode =>
            "Le document a changé depuis l’aperçu; recommencez le dry-run.",
        SetAssistedAreaPlansHandler.PermissionDeniedCode or BuildAssistedAreaPlanHandler.PermissionDeniedCode =>
            "La permission Assistant active ne permet pas cette opération.",
        BuildAssistedAreaPlanHandler.ApprovalRequiredCode =>
            "Le plan requiert une approbation humaine explicite avant construction.",
        BuildAssistedAreaPlanHandler.AlreadyBuiltCode =>
            "Ce plan a déjà été construit dans la zone; aucune seconde application n’est autorisée.",
        BuildAssistedAreaPlanHandler.NavigationRegressionCode =>
            "La construction introduirait une régression de navigation localisée.",
        BuildAssistedAreaPlanHandler.LightingInvalidCode =>
            "La zone ne possède pas un éclairage valide pour cette construction.",
        AssistantProviderRegistry.ProviderUnavailableCode =>
            "Le type de fournisseur configuré n’est pas installé dans cette édition de Studio.",
        _ when code is not null && code.StartsWith("assistant.provider", StringComparison.Ordinal) =>
            "Le fournisseur Assistant a refusé la demande. Vérifiez réseau, boucle locale, délai et identifiant d’environnement.",
        _ => "Le contrat canonique a refusé cette opération; le module reste inchangé.",
    };

    private sealed record StudioUx1AFApprovedPlan(
        AssistedAreaPlanCatalog Catalog,
        SetAssistedAreaPlansCommand Command,
        AssistedAreaPlanRevision PlanRevision,
        ProjectRevision ProjectRevision,
        Guid PlanId,
        long Generation,
        string DraftFingerprint);

    private sealed record StudioUx1AFApprovedBuild(
        BuildAssistedAreaPlanCommand Command,
        ProjectRevision ProjectRevision,
        AssistedAreaPlanRevision PlanRevision,
        Guid PlanId);

    private sealed class StudioUx1AFContext : IDisposable
    {
        internal ProjectSession SessionIdentity { get; }
        internal AssistantWorkspaceProfile Profile { get; }
        internal LoreIndexDefinition Lore { get; }
        internal AssistedAreaPlanCommandHistory PlanHistory { get; }
        internal SetAssistedAreaPlansHandler SetPlans { get; }
        internal BuildAssistedAreaPlanHandler BuildPlan { get; }
        internal AssistantProviderRegistry ProviderRegistry { get; }
        private HttpClient HttpClient { get; }

        internal StudioUx1AFContext(
            ProjectSession sessionIdentity,
            AssistantWorkspaceProfile profile,
            LoreIndexDefinition lore,
            AssistedAreaPlanCommandHistory planHistory,
            SetAssistedAreaPlansHandler setPlans,
            BuildAssistedAreaPlanHandler buildPlan,
            AssistantProviderRegistry providerRegistry,
            HttpClient httpClient)
        {
            SessionIdentity = sessionIdentity;
            Profile = profile;
            Lore = lore;
            PlanHistory = planHistory;
            SetPlans = setPlans;
            BuildPlan = buildPlan;
            ProviderRegistry = providerRegistry;
            HttpClient = httpClient;
        }

        public void Dispose() => HttpClient.Dispose();
    }
}
