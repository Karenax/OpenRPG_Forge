using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1AIEvidenceProvenance = "studio:ux1-ai:evidence";
    private static readonly System.Text.RegularExpressions.Regex Ux1AIGuidPattern = new(
        @"(?<![0-9a-fA-F])(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})(?![0-9a-fA-F])",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private readonly SetWorldFeatureCatalogHandler _setTriggerValidationCatalogUx1AI = new(new SystemClock());
    private StudioTriggerValidationDraft? _approvedTriggerValidationUx1AI;
    private ProjectRevision? _approvedTriggerValidationRevisionUx1AI;
    private string? _approvedTriggerValidationFingerprintUx1AI;

    internal IReadOnlyList<StudioTriggerValidationChoice> TriggerValidationChoicesUx1AI
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null)
            {
                return [];
            }

            return project.Module.WorldFeatures.Triggers
                .Where(item => item.AreaId == area.Id)
                .Select(item =>
                {
                    var blueprint = project.Blueprints.FirstOrDefault(candidate => candidate.Id == item.BlueprintId);
                    var name = blueprint?.DisplayName ?? "Déclencheur sans blueprint";
                    var instances = area.Entities.Count(entity => entity.BlueprintId == item.BlueprintId);
                    var foundationLabel = item.Foundation is { } foundation
                        ? $"fondation {foundation.Slug}"
                        : "fondation absente";
                    var state = item.Foundation is null
                        ? "volume de base requis"
                        : item.Foundation.Validation is null
                            ? item.Foundation.ValidationRequiredAtRuntime
                                ? "contexte requis manquant"
                                : "fallback optionnel"
                            : item.Foundation.ValidationRequiredAtRuntime
                                ? "contexte requis configuré"
                                : "contexte optionnel configuré";
                    return new StudioTriggerValidationChoice(
                        item.Id,
                        item.BlueprintId,
                        $"{name} · {foundationLabel} · {instances} instance(s) · {state}",
                        item.Foundation is not null,
                        item.Foundation?.Validation is not null,
                        item.Foundation?.ValidationRequiredAtRuntime == true,
                        string.Equals(item.Provenance, Ux1AIEvidenceProvenance, StringComparison.Ordinal));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioTriggerPortalDestinationChoice> TriggerValidationPortalDestinationChoicesUx1AI()
    {
        var project = Project;
        if (project is null)
        {
            return [];
        }

        var resolvedLabels = new Dictionary<Guid, string>();
        var destinationFallbacks = new Dictionary<Guid, string>();
        void Resolve(Guid id, string label)
        {
            if (id != Guid.Empty && !string.IsNullOrWhiteSpace(label))
            {
                resolvedLabels.TryAdd(id, RedactPortalDestinationLabelUx1AI(label));
            }
        }
        void AddDestination(Guid id, string fallbackLabel)
        {
            if (id != Guid.Empty && !string.IsNullOrWhiteSpace(fallbackLabel))
            {
                destinationFallbacks.TryAdd(id, RedactPortalDestinationLabelUx1AI(fallbackLabel));
            }
        }

        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        foreach (var blueprint in project.Blueprints.Where(item =>
                     item.Kind is BlueprintKind.Door or BlueprintKind.Trigger or BlueprintKind.PlayerSpawn))
        {
            Resolve(
                blueprint.Id.Value,
                $"{HumanizePortalBlueprintKindUx1AI(blueprint.Kind)} · {blueprint.DisplayName} · {blueprint.Slug}");
        }

        foreach (var area in project.Areas)
        {
            foreach (var entity in area.Entities)
            {
                if (!blueprints.TryGetValue(entity.BlueprintId, out var blueprint) ||
                    blueprint.Kind is not (BlueprintKind.Door or BlueprintKind.Trigger or BlueprintKind.PlayerSpawn))
                {
                    continue;
                }
                Resolve(
                    entity.Id.Value,
                    $"{HumanizePortalBlueprintKindUx1AI(blueprint.Kind)} placé · {blueprint.DisplayName} · " +
                    $"{area.DisplayName} · {blueprint.Slug}");
            }

            var anchorDefinitions = area.Navigation.Anchors.Definitions.ToDictionary(item => item.Id);
            foreach (var definition in anchorDefinitions.Values)
            {
                Resolve(
                    definition.Id,
                    $"{HumanizeAnchorKindUx1AI(definition.Kind)} · {definition.Slug} · {area.DisplayName}");
            }
            foreach (var instance in area.Navigation.Anchors.Instances)
            {
                var definition = anchorDefinitions.GetValueOrDefault(instance.DefinitionId);
                Resolve(
                    instance.Id,
                    $"{HumanizeAnchorKindUx1AI(definition?.Kind)} placé · " +
                    $"{definition?.Slug ?? "ancre héritée"} · {area.DisplayName} · position {instance.DisplayOrder + 1}");
            }
            foreach (var waypoint in area.Navigation.Waypoints.Waypoints)
            {
                Resolve(
                    waypoint.Id,
                    $"Point de passage · {waypoint.Name} · {area.DisplayName}");
            }

            foreach (var portal in area.Portals)
            {
                var destinationArea = project.Areas.SingleOrDefault(item => item.Id == portal.DestinationAreaId);
                Resolve(
                    portal.DestinationEntityId.Value,
                    $"Transition · {portal.DisplayName} · vers {destinationArea?.DisplayName ?? "zone externe"}");
            }
        }

        var catalog = project.Module.WorldFeatures;
        foreach (var profile in catalog.Doors)
        {
            var blueprint = blueprints.GetValueOrDefault(profile.BlueprintId);
            var source = blueprint is null
                ? "porte héritée"
                : $"{blueprint.DisplayName} · {blueprint.Slug}";
            Resolve(profile.Id, $"Porte · {source} · profil de monde");
            if (profile.Foundation?.Portal is { } portal)
            {
                AddDestination(
                    portal.DestinationId,
                    $"{HumanizeDoorDestinationKindUx1AI(portal.DestinationKind)} · destination de {source}");
            }
        }
        foreach (var profile in catalog.Triggers)
        {
            var blueprint = blueprints.GetValueOrDefault(profile.BlueprintId);
            var area = project.Areas.SingleOrDefault(item => item.Id == profile.AreaId);
            var source = blueprint is null
                ? profile.Foundation?.Slug ?? "déclencheur hérité"
                : $"{blueprint.DisplayName} · {blueprint.Slug}";
            Resolve(
                profile.Id,
                $"Déclencheur · {source} · {area?.DisplayName ?? "zone externe"} · profil de monde");
            if (profile.Foundation?.Portal is { } portal)
            {
                AddDestination(
                    portal.DestinationId,
                    $"{HumanizeTriggerDestinationKindUx1AI(portal.Destination)} · destination de {source}");
            }
        }

        var choices = new List<StudioTriggerPortalDestinationChoice>();
        foreach (var group in destinationFallbacks
                     .Select(item => new StudioTriggerPortalDestinationChoice(
                         item.Key,
                         resolvedLabels.GetValueOrDefault(item.Key) ?? item.Value))
                     .GroupBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase))
        {
            var ordered = group.OrderBy(item => item.Id).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                choices.Add(ordered.Length == 1
                    ? ordered[index]
                    : ordered[index] with { Label = $"{ordered[index].Label} · variante {index + 1}" });
            }
        }
        return choices.OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal ProjectDefinition? CreateUx1AIEvidenceProject()
    {
        var project = Project;
        var area = ActiveArea;
        if (project is null || area is null)
        {
            return null;
        }

        var catalog = project.Module.WorldFeatures;
        var occupied = new[] { project.Id.Value, project.Module.Id.Value }
            .Concat(project.Blueprints.Select(item => item.Id.Value))
            .Concat(project.Areas.Select(item => item.Id.Value))
            .Concat(catalog.Placeables.Select(item => item.Id))
            .Concat(catalog.Doors.Select(item => item.Id))
            .Concat(catalog.Triggers.Select(item => item.Id))
            .Concat(catalog.Encounters.Select(item => item.Id))
            .Concat(catalog.VehicleRoutes.Select(item => item.Id))
            .Concat(catalog.Merchants.Select(item => item.Id))
            .Concat(catalog.Companions.Select(item => item.Id))
            .ToHashSet();
        var salt = 0;
        Guid[] ids;
        do
        {
            ids = Enumerable.Range(0, 8)
                .Select(index => StableTriggerValidationIdUx1AI(
                    project.Id.Value,
                    $"evidence-{area.Id.Value:N}-{salt}-{index}"))
                .ToArray();
            salt++;
        } while (ids.Distinct().Count() != ids.Length || ids.Any(occupied.Contains));

        var slugBase = "ux1-ai-validation-trigger";
        var slug = slugBase;
        var slugIndex = 1;
        var existingSlugs = project.Blueprints.Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        while (existingSlugs.Contains(slug))
        {
            slug = $"{slugBase}-{slugIndex++}";
        }

        const string actionTarget = "encounter:ux1-ai-validation";
        const string actionScript = "script:ux1-ai-validation-action";
        const string enterScript = "script:ux1-ai-validation-enter";
        const string exitScript = "script:ux1-ai-validation-exit";
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(ids[0]),
            slug,
            "Validation canonique du passage",
            BlueprintKind.Trigger);
        var validation = new TriggerWorldProfile.RuntimeValidationPolicy(
            [ids[7], ids[6]],
            [actionTarget],
            [actionScript, enterScript, exitScript],
            ["characters"]);
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            ids[2],
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1AIEvidenceProvenance,
            slug,
            new TriggerWorldProfile.ShapePolicy(
                TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                [],
                new Vector3Value(4, 3, 4),
                3,
                .25,
                true),
            new TriggerWorldProfile.FilterPolicy(["creature"], ["player"], [], ["characters"]),
            ["quest.active"],
            .5,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new TriggerWorldProfile.ActionPolicy(
                ids[3],
                "script",
                actionTarget,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["script"] = actionScript })],
            new TriggerWorldProfile.PortalPolicy(
                TriggerWorldProfile.DestinationKind.Trigger,
                ids[7],
                "spawn:ux1-ai-validation",
                Vector3Value.Zero,
                true,
                ["party"]),
            new TriggerWorldProfile.TransformPolicy(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["editor.overlay"] = "validation",
                ["evidence.scope"] = "isolated",
            },
            new TriggerWorldProfile.EventPolicy(
                ids[4],
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1AIEvidenceProvenance,
                "1.0.0",
                "VolumeEntered",
                enterScript,
                null),
            new TriggerWorldProfile.EventPolicy(
                ids[5],
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1AIEvidenceProvenance,
                "1.0.0",
                "VolumeExited",
                null,
                exitScript),
            validation,
            true);
        var profile = new TriggerWorldProfile(
            ids[1],
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1AIEvidenceProvenance,
            blueprint.Id,
            area.Id,
            null,
            null,
            null,
            "trigger.ux1-ai-validation",
            null,
            null,
            null,
            null,
            new TriggerWorldProfile.DirectorPolicy(true, true, true, false, true),
            foundation,
            true,
            null,
            false);
        var evidenceCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.Placeables,
            catalog.Doors,
            catalog.Triggers.Append(profile).ToArray(),
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        return new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(evidenceCatalog),
            project.Blueprints.Append(blueprint).ToArray(),
            project.Areas,
            project.Dialogues,
            project.Quests);
    }

    internal StudioTriggerValidationDraft? CreateTriggerValidationDraftUx1AI(Guid? requestedProfileId = null)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || requestedProfileId is null)
        {
            return null;
        }

        var matches = project.Module.WorldFeatures.Triggers
            .Where(item => item.Id == requestedProfileId && item.AreaId == area.Id)
            .ToArray();
        if (matches.Length != 1)
        {
            return null;
        }

        var profile = matches[0];
        if (profile.Foundation is not { } foundation)
        {
            return null;
        }
        var validation = foundation.Validation;
        return new StudioTriggerValidationDraft(
            profile.Id,
            profile.BlueprintId,
            profile.AreaId,
            history.State.Revision,
            validation is not null,
            foundation.ValidationRequiredAtRuntime,
            validation?.KnownPortalDestinationIds.ToArray() ?? [],
            validation?.KnownTargetIds.ToArray() ?? [],
            validation?.KnownScriptIds.ToArray() ?? [],
            validation?.AccessibleLayers.ToArray() ?? [],
            profile);
    }

    internal StudioTriggerValidationPreview PreviewTriggerValidationUx1AI(StudioTriggerValidationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTriggerValidationUx1AI();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedTriggerValidationPreviewUx1AI("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedTriggerValidationPreviewUx1AI(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le déclencheur avant de prévisualiser.");
        }
        if (!TryBuildTriggerValidationUx1AI(
                draft,
                dryRun: true,
                out var command,
                out var candidateProject,
                out var candidateProfile,
                out var message))
        {
            return FailedTriggerValidationPreviewUx1AI(message);
        }

        var resolution = WorldFeatureRuntimeResolver.ResolveTriggerValidation(candidateProfile!);
        var groups = BuildTriggerValidationGroupsUx1AI(candidateProject!, candidateProfile!.Id, resolution);
        var blocking = groups.FirstOrDefault(item => item.Blocking);
        if (blocking is not null)
        {
            return new StudioTriggerValidationPreview(
                false,
                blocking.Details.FirstOrDefault() ?? "Le contexte de validation doit être corrigé.",
                BuildTriggerValidationSummaryUx1AI(groups, resolution),
                false,
                resolution.Available,
                !resolution.Available && string.IsNullOrEmpty(resolution.DiagnosticCode),
                groups);
        }

        var result = _setTriggerValidationCatalogUx1AI.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            return new StudioTriggerValidationPreview(
                false,
                HumanizeTriggerValidationCodeUx1AI(result.Diagnostics.FirstOrDefault()?.Code),
                BuildTriggerValidationSummaryUx1AI(groups, resolution),
                false,
                resolution.Available,
                !resolution.Available && string.IsNullOrEmpty(resolution.DiagnosticCode),
                groups);
        }

        _approvedTriggerValidationUx1AI = draft;
        _approvedTriggerValidationRevisionUx1AI = history.State.Revision;
        _approvedTriggerValidationFingerprintUx1AI = TriggerValidationFingerprintUx1AI(draft);
        return new StudioTriggerValidationPreview(
            true,
            "Aperçu validé · aucun document n’a été modifié.",
            BuildTriggerValidationSummaryUx1AI(groups, resolution),
            true,
            resolution.Available,
            !resolution.Available && string.IsNullOrEmpty(resolution.DiagnosticCode),
            groups);
    }

    internal StudioUx1AIActionResult ApplyApprovedTriggerValidationUx1AI()
    {
        var draft = _approvedTriggerValidationUx1AI;
        var history = History;
        if (draft is null || history is null ||
            _approvedTriggerValidationRevisionUx1AI != history.State.Revision ||
            !string.Equals(
                _approvedTriggerValidationFingerprintUx1AI,
                TriggerValidationFingerprintUx1AI(draft),
                StringComparison.Ordinal))
        {
            CancelTriggerValidationUx1AI();
            return new StudioUx1AIActionResult(
                false,
                "Prévisualisez de nouveau la validation canonique avant de l’appliquer.");
        }
        if (!TryBuildTriggerValidationUx1AI(
                draft,
                dryRun: false,
                out var command,
                out _,
                out _,
                out var message))
        {
            CancelTriggerValidationUx1AI();
            return new StudioUx1AIActionResult(false, message);
        }

        var result = _setTriggerValidationCatalogUx1AI.Handle(history.State, command!);
        CancelTriggerValidationUx1AI();
        if (!result.Succeeded)
        {
            return new StudioUx1AIActionResult(
                false,
                HumanizeTriggerValidationCodeUx1AI(result.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(result);
        InvalidateContentIndexes();
        return new StudioUx1AIActionResult(
            true,
            "Validation appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelTriggerValidationUx1AI()
    {
        _approvedTriggerValidationUx1AI = null;
        _approvedTriggerValidationRevisionUx1AI = null;
        _approvedTriggerValidationFingerprintUx1AI = null;
    }

    internal StudioUx1AIApprovalSnapshot CaptureTriggerValidationApprovalUx1AI() => new(
        _approvedTriggerValidationUx1AI,
        _approvedTriggerValidationRevisionUx1AI,
        _approvedTriggerValidationFingerprintUx1AI);

    private bool TryBuildTriggerValidationUx1AI(
        StudioTriggerValidationDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out ProjectDefinition? candidateProject,
        out TriggerWorldProfile? candidateProfile,
        out string message)
    {
        command = null;
        candidateProject = null;
        candidateProfile = null;
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez le déclencheur avant de prévisualiser.";
            return false;
        }

        var catalog = project.Module.WorldFeatures;
        var matches = catalog.Triggers.Where(item =>
            item.Id == draft.TriggerProfileId &&
            item.BlueprintId == draft.BlueprintId &&
            item.AreaId == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "Le déclencheur a changé depuis son ouverture. Rechargez sa fiche.";
            return false;
        }
        var currentProfile = matches[0];
        if (currentProfile.Foundation is null)
        {
            message = "Configurez d’abord le volume de base avec l’outil Déclencheur.";
            return false;
        }

        var validation = draft.ValidationEnabled
            ? new TriggerWorldProfile.RuntimeValidationPolicy(
                draft.KnownPortalDestinationIds.ToArray(),
                draft.KnownTargetIds.ToArray(),
                draft.KnownScriptIds.ToArray(),
                draft.AccessibleLayers.ToArray())
            : null;
        var foundation = currentProfile.Foundation with
        {
            Validation = validation,
            ValidationRequiredAtRuntime = draft.ValidationRequiredAtRuntime,
        };
        var updatedProfile = currentProfile with { Foundation = foundation };
        candidateProfile = updatedProfile;
        var candidateCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.Placeables,
            catalog.Doors,
            catalog.Triggers.Select(item => item.Id == updatedProfile.Id ? updatedProfile : item).ToArray(),
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        candidateProject = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(candidateCatalog),
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);
        command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            candidateCatalog,
            dryRun);
        return true;
    }

    private static IReadOnlyList<StudioTriggerValidationDiagnosticGroup> BuildTriggerValidationGroupsUx1AI(
        ProjectDefinition project,
        Guid profileId,
        WorldFeatureRuntimeResolver.TriggerValidationResolution resolution)
    {
        var targetPath = $"module/worldFeatures/triggers/{profileId}";
        var diagnostics = WorldFeatureCatalogValidator.Validate(project)
            .Where(item => item.Path.StartsWith(targetPath, StringComparison.Ordinal))
            .ToArray();
        var contextUnavailable = !resolution.Available && resolution.Validation is null;
        var definitions = new (string Key, string Title)[]
        {
            ("foundation", "Fondation & politique"),
            ("geometry", "Géométrie"),
            ("filters", "Filtres & accessibilité"),
            ("actions", "Actions & cibles"),
            ("portal", "Portail & destination"),
            ("scripts", "Scripts & événements"),
            ("context", "Contexte runtime"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics
                .Where(item => TriggerValidationGroupKeyUx1AI(item) == definition.Key)
                .ToArray();
            if (items.Length > 0)
            {
                var structured = items.Select(item => new StudioTriggerValidationDiagnostic(
                    item.Code,
                    item.Path,
                    HumanizeTriggerValidationDiagnosticUx1AI(item))).ToArray();
                return new StudioTriggerValidationDiagnosticGroup(
                    definition.Key,
                    definition.Title,
                    items.Length == 1 ? "1 correction requise" : $"{items.Length} corrections requises",
                    structured.Select(item => item.Message).ToArray(),
                    structured,
                    true);
            }
            if (definition.Key == "context" && !resolution.Available)
            {
                var required = !string.IsNullOrEmpty(resolution.DiagnosticCode);
                return new StudioTriggerValidationDiagnosticGroup(
                    definition.Key,
                    definition.Title,
                    required ? "Requis" : "Fallback optionnel",
                    [required
                        ? "Le runtime exige un contexte de validation configuré."
                        : "Le contexte est désactivé : le runtime conserve son fallback optionnel explicite."],
                    [],
                    required);
            }
            if (contextUnavailable)
            {
                return new StudioTriggerValidationDiagnosticGroup(
                    definition.Key,
                    definition.Title,
                    "Non évalué",
                    ["Le contexte est absent : ce contrôle dépendant n’a pas été exécuté."],
                    [],
                    false);
            }
            return new StudioTriggerValidationDiagnosticGroup(
                definition.Key,
                definition.Title,
                "Conforme",
                ["Aucune anomalie signalée par le validateur canonique."],
                [],
                false);
        }).ToArray();
    }

    private static string TriggerValidationGroupKeyUx1AI(DomainDiagnostic diagnostic)
    {
        var code = diagnostic.Code;
        if (code.Contains("validation", StringComparison.Ordinal) ||
            diagnostic.Path.EndsWith("/validation", StringComparison.Ordinal))
        {
            return "context";
        }
        if (code.Contains("geometry", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/shape", StringComparison.Ordinal))
        {
            return "geometry";
        }
        if (code.Contains("filter", StringComparison.Ordinal) ||
            code.Contains("inaccessible", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/layers", StringComparison.Ordinal))
        {
            return "filters";
        }
        if (code.Contains("action", StringComparison.Ordinal))
        {
            return "actions";
        }
        if (code.Contains("portal", StringComparison.Ordinal))
        {
            return "portal";
        }
        if (code.Contains("script", StringComparison.Ordinal) ||
            code.Contains("event", StringComparison.Ordinal))
        {
            return "scripts";
        }
        return "foundation";
    }

    private static string HumanizePortalBlueprintKindUx1AI(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Door => "Porte",
        BlueprintKind.Trigger => "Déclencheur",
        BlueprintKind.PlayerSpawn => "Point d’apparition",
        _ => "Destination",
    };

    private static string RedactPortalDestinationLabelUx1AI(string label) =>
        Ux1AIGuidPattern.Replace(label.Trim(), "référence masquée");

    private static string HumanizeAnchorKindUx1AI(AnchorKind? kind) => kind switch
    {
        AnchorKind.Waypoint => "Point de passage",
        AnchorKind.Spawn => "Point d’apparition",
        AnchorKind.TransitionDestination => "Ancre de transition",
        _ => "Ancre",
    };

    private static string HumanizeDoorDestinationKindUx1AI(DoorWorldProfile.DestinationKind kind) => kind switch
    {
        DoorWorldProfile.DestinationKind.Door => "Porte",
        DoorWorldProfile.DestinationKind.Trigger => "Déclencheur",
        DoorWorldProfile.DestinationKind.Waypoint => "Point de passage",
        DoorWorldProfile.DestinationKind.SpawnAnchor => "Point d’apparition",
        _ => "Destination",
    };

    private static string HumanizeTriggerDestinationKindUx1AI(TriggerWorldProfile.DestinationKind kind) => kind switch
    {
        TriggerWorldProfile.DestinationKind.Door => "Porte",
        TriggerWorldProfile.DestinationKind.Trigger => "Déclencheur",
        TriggerWorldProfile.DestinationKind.Waypoint => "Point de passage",
        TriggerWorldProfile.DestinationKind.SpawnAnchor => "Point d’apparition",
        _ => "Destination",
    };

    private static string HumanizeTriggerValidationDiagnosticUx1AI(DomainDiagnostic diagnostic) =>
        diagnostic.Code switch
        {
            "world.trigger.foundation_required_missing" =>
                "Le volume de base requis n’est pas configuré.",
            "world.trigger.validation_required_missing" =>
                "Le contexte de validation est requis mais absent.",
            "world.trigger.validation_context_invalid" =>
                "Le contexte contient une référence vide ou dupliquée.",
            "world.trigger.geometry_self_intersection" =>
                "Le contour ou un trou se croise lui-même.",
            "world.trigger.geometry_invalid" =>
                "La géométrie du volume de base doit être corrigée.",
            "world.trigger.filter_contradiction" =>
                "Un même tag est à la fois requis et exclu.",
            "world.trigger.inaccessible" =>
                "Aucune couche du déclencheur n’est déclarée accessible.",
            "world.trigger.action_target_missing" =>
                "Une action cible une ressource absente du contexte connu.",
            "world.trigger.actions_invalid" =>
                "Une action du volume de base est incomplète.",
            "world.trigger.portal_destination_missing" =>
                "La destination du portail n’est pas déclarée dans le contexte connu.",
            "world.trigger.portal_invalid" =>
                "Le portail du volume de base est incomplet.",
            "world.trigger.script_reference_missing" =>
                "Un script utilisé par le déclencheur n’est pas déclaré dans le contexte connu.",
            "world.trigger.event_invalid" =>
                "Une liaison d’entrée ou de sortie est incomplète.",
            "world.trigger.policy_invalid" =>
                "Les filtres, conditions ou le délai du volume de base doivent être corrigés.",
            _ => "Le catalogue canonique signale un point à corriger dans cette section.",
        };

    private static string HumanizeTriggerValidationCodeUx1AI(string? code) => code switch
    {
        "command.revision_conflict" =>
            "Le projet a changé. Rechargez puis prévisualisez de nouveau la validation.",
        "world.trigger.foundation_required_missing" =>
            "Configurez d’abord le volume de base avec l’outil Déclencheur.",
        "world.trigger.validation_required_missing" =>
            "Le contexte de validation est requis mais absent.",
        "world.trigger.validation_context_invalid" =>
            "Le contexte contient une référence vide ou dupliquée.",
        "world.trigger.action_target_missing" =>
            "Une action cible une ressource absente du contexte connu.",
        "world.trigger.portal_destination_missing" =>
            "La destination du portail n’est pas déclarée dans le contexte connu.",
        "world.trigger.script_reference_missing" =>
            "Un script utilisé n’est pas déclaré dans le contexte connu.",
        "world.trigger.inaccessible" =>
            "Aucune couche du déclencheur n’est déclarée accessible.",
        _ =>
            "La validation canonique ne peut pas être appliquée. Consultez les sections signalées.",
    };

    private static string BuildTriggerValidationSummaryUx1AI(
        IReadOnlyList<StudioTriggerValidationDiagnosticGroup> groups,
        WorldFeatureRuntimeResolver.TriggerValidationResolution resolution)
    {
        var runtime = resolution.Available && resolution.Validation is { } validation
            ? $"Runtime prêt · {validation.KnownTargetIds.Count} cible(s) · " +
              $"{validation.KnownScriptIds.Count} script(s) · " +
              $"{validation.KnownPortalDestinationIds.Count} destination(s) · " +
              $"{validation.AccessibleLayers.Count} couche(s)"
            : string.IsNullOrEmpty(resolution.DiagnosticCode)
                ? "Runtime · fallback optionnel explicite"
                : "Runtime · contexte requis manquant";
        return runtime + "\n" + string.Join("\n", groups.Select(item => $"{item.Title} · {item.State}"));
    }

    private static StudioTriggerValidationPreview FailedTriggerValidationPreviewUx1AI(string message) =>
        new(false, message, string.Empty, false, false, false, []);

    private static string TriggerValidationFingerprintUx1AI(StudioTriggerValidationDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)1);
            WriteGuidUx1AI(writer, draft.TriggerProfileId);
            WriteGuidUx1AI(writer, draft.BlueprintId.Value);
            WriteGuidUx1AI(writer, draft.AreaId.Value);
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.ValidationEnabled);
            writer.Write(draft.ValidationRequiredAtRuntime);
            WriteGuidListUx1AI(writer, draft.KnownPortalDestinationIds);
            WriteStringListUx1AI(writer, draft.KnownTargetIds);
            WriteStringListUx1AI(writer, draft.KnownScriptIds);
            WriteStringListUx1AI(writer, draft.AccessibleLayers);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteGuidUx1AI(BinaryWriter writer, Guid value)
    {
        var bytes = value.ToByteArray();
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteGuidListUx1AI(BinaryWriter writer, IReadOnlyList<Guid> values)
    {
        writer.Write(values.Count);
        foreach (var value in values)
        {
            WriteGuidUx1AI(writer, value);
        }
    }

    private static void WriteStringListUx1AI(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    private static Guid StableTriggerValidationIdUx1AI(Guid seed, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux1-ai:{seed:N}:{segment}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }

    private void CloseUx1AI() => CancelTriggerValidationUx1AI();
}

internal sealed partial class StudioSessionController
{
    internal StudioUx1AISmokeResult RunUx1AIContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ai-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var targetBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("a1100000-0000-4000-8000-000000000001")),
                "passage-des-veilleurs",
                "Passage des veilleurs",
                BlueprintKind.Trigger);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("a1100000-0000-4000-8000-000000000002")),
                "cloche-du-sanctuaire",
                "Cloche du sanctuaire",
                BlueprintKind.Trigger);
            var foundationlessBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("a1100000-0000-4000-8000-000000000003")),
                "seuil-inacheve",
                "Seuil inachevé",
                BlueprintKind.Trigger);
            var narrativeProject = SampleModuleAuthoringService.Create().State.Project;
            var narrativeDialogue = narrativeProject.Dialogues.Single();
            var narrativeParticipant = narrativeProject.Blueprints.First(item =>
                item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("a1100000-0000-4000-8000-000000000004")),
                "sanctuaire-des-veilleurs",
                "Sanctuaire des veilleurs",
                [],
                AreaKind.Interior,
                new AreaDimensions(48, 48));
            var shell = new ProjectDefinition(
                ProjectId.From(Guid.Parse("a1100000-0000-4000-8000-000000000005")),
                "Studio UX1-AI",
                [targetBlueprint, neighborBlueprint, foundationlessBlueprint, .. narrativeProject.Blueprints],
                [area]);
            var targetProfile = CreateUx1AISmokeTrigger(
                targetBlueprint.Id,
                area.Id,
                Guid.Parse("a1100000-0000-4000-8100-000000000001"),
                foundation: true,
                validation: false,
                narrativeDialogueId: narrativeDialogue.Id.Value,
                narrativeParticipantId: narrativeParticipant.Id,
                foundationSlug: "passage-des-veilleurs-volume");
            var neighborProfile = CreateUx1AISmokeTrigger(
                neighborBlueprint.Id,
                area.Id,
                Guid.Parse("a1100000-0000-4000-8200-000000000001"),
                foundation: true,
                validation: true,
                foundationSlug: "cloche-du-sanctuaire-volume");
            var foundationlessProfile = CreateUx1AISmokeTrigger(
                foundationlessBlueprint.Id,
                area.Id,
                Guid.Parse("a1100000-0000-4000-8300-000000000001"),
                foundation: false,
                validation: false);
            var catalog = new WorldFeatureCatalog(
                shell.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ai",
                triggers: [targetProfile, neighborProfile, foundationlessProfile]);
            var project = new ProjectDefinition(
                shell.Id,
                shell.Title,
                shell.Module.WithWorldFeatures(catalog),
                shell.Blueprints,
                shell.Areas,
                [narrativeDialogue],
                shell.Quests);
            var initialDiagnostics = WorldFeatureCatalogValidator.Validate(project);
            if (initialDiagnostics.Count != 0)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AI_CONTRACT_FAILED stage=initial-validation diagnostics=" +
                                  string.Join("|", initialDiagnostics.Select(item =>
                                      $"{item.Code}@{item.Path}:{item.Message}")));
                return FailedUx1AISmoke();
            }
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AI_CONTRACT_FAILED stage=create-open diagnostics=" +
                                  string.Join("|", created.Diagnostics.Select(item =>
                                      $"{item.Code}@{item.Path}:{item.Message}")));
                return FailedUx1AISmoke();
            }

            var initialProject = editing.Project!;
            var initialCatalog = initialProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var loadedNeighbor = initialCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            var destinationChoices = editing.TriggerValidationPortalDestinationChoicesUx1AI();
            var selectionExplicit = editing.CreateTriggerValidationDraftUx1AI() is null &&
                                    editing.TriggerValidationChoicesUx1AI.Count == 3 &&
                                    editing.TriggerValidationChoicesUx1AI.All(item =>
                                        IsHumanUx1AIMessage(item.Label)) &&
                                    destinationChoices.Any(item =>
                                        item.Id == targetProfile.Foundation!.Portal!.DestinationId) &&
                                    destinationChoices.All(item => IsHumanUx1AIMessage(item.Label)) &&
                                    editing.TriggerValidationChoicesUx1AI.Single(item =>
                                        item.ProfileId == targetProfile.Id).Label.Contains(
                                        targetProfile.Foundation!.Slug,
                                        StringComparison.Ordinal);
            var foundationRequired =
                editing.CreateTriggerValidationDraftUx1AI(foundationlessProfile.Id) is null &&
                editing.TriggerValidationChoicesUx1AI.Single(item =>
                    item.ProfileId == foundationlessProfile.Id) is { HasFoundation: false };

            var staleDraft = editing.CreateTriggerValidationDraftUx1AI(targetProfile.Id)!;
            var firstIntermediate = ApplyUx1AIIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-before-preview");
            var staleDraftPreview = editing.PreviewTriggerValidationUx1AI(staleDraft);
            var staleDraftBlocked = firstIntermediate && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé", StringComparison.Ordinal) &&
                                    editing.Undo().Succeeded;

            var draft = ConfigureUx1AISmokeDraft(
                editing.CreateTriggerValidationDraftUx1AI(targetProfile.Id)!);
            var invalidContext = editing.PreviewTriggerValidationUx1AI(draft with
            {
                KnownTargetIds = draft.KnownTargetIds.Append(draft.KnownTargetIds[0]).ToArray(),
            });
            var missingTarget = editing.PreviewTriggerValidationUx1AI(draft with { KnownTargetIds = [] });
            var missingScript = editing.PreviewTriggerValidationUx1AI(draft with { KnownScriptIds = [] });
            var missingPortal = editing.PreviewTriggerValidationUx1AI(draft with
            {
                KnownPortalDestinationIds = [],
            });
            var inaccessible = editing.PreviewTriggerValidationUx1AI(draft with
            {
                AccessibleLayers = ["spectators"],
            });

            var historyBeforePreview = editing.History!;
            var projectBeforePreview = editing.Project;
            var revisionBeforePreview = historyBeforePreview.State.Revision;
            var journalBeforePreview = historyBeforePreview.Journal.ToArray();
            var canUndoBeforePreview = historyBeforePreview.CanUndo;
            var canRedoBeforePreview = historyBeforePreview.CanRedo;
            var dirtyBeforePreview = editing.IsDirty;
            var preview = editing.PreviewTriggerValidationUx1AI(draft);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(projectBeforePreview, editing.Project) &&
                         ReferenceEquals(historyBeforePreview, editing.History) &&
                         historyBeforePreview.State.Revision == revisionBeforePreview &&
                         historyBeforePreview.Journal.SequenceEqual(journalBeforePreview) &&
                         historyBeforePreview.CanUndo == canUndoBeforePreview &&
                         historyBeforePreview.CanRedo == canRedoBeforePreview &&
                         editing.IsDirty == dirtyBeforePreview;

            var secondIntermediate = ApplyUx1AIIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-after-preview");
            var staleApply = editing.ApplyApprovedTriggerValidationUx1AI();
            var staleApprovalBlocked = secondIntermediate && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var freshDraft = ConfigureUx1AISmokeDraft(
                editing.CreateTriggerValidationDraftUx1AI(targetProfile.Id)!);
            var freshPreview = editing.PreviewTriggerValidationUx1AI(freshDraft);
            var apply = editing.ApplyApprovedTriggerValidationUx1AI();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var appliedNeighbor = appliedCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            var appliedFoundation = appliedTarget.Foundation;
            if (!freshPreview.Succeeded || !apply.Succeeded ||
                appliedFoundation?.Validation is not { } appliedValidation)
            {
                Godot.GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX1AI_CONTRACT_FAILED stage=fresh-apply preview={freshPreview.Succeeded} apply={apply.Succeeded} validation={appliedFoundation?.Validation is not null}");
                return FailedUx1AISmoke();
            }

            var contextCreation = loadedTarget.Foundation!.Validation is null &&
                                  SameValidationUx1AI(freshDraft, appliedValidation);
            var requiredFlag = appliedFoundation.ValidationRequiredAtRuntime;
            var resolution = WorldFeatureRuntimeResolver.ResolveTriggerValidation(appliedTarget);
            var runtimeResolution = resolution.Available && resolution.Validation is not null &&
                                    SameValidationUx1AI(freshDraft, resolution.Validation);
            var foundationNeighborsPreserved = SameFoundationNeighborsByReferenceUx1AI(
                loadedTarget.Foundation,
                appliedFoundation);
            var triggerNeighborsPreserved = SameTriggerNeighborsByReferenceUx1AI(loadedTarget, appliedTarget) &&
                                            ReferenceEquals(appliedNeighbor, loadedNeighbor) &&
                                            SameReferencesUx1AI(appliedCatalog.Placeables, initialCatalog.Placeables) &&
                                            SameReferencesUx1AI(appliedCatalog.Doors, initialCatalog.Doors) &&
                                            SameReferencesUx1AI(
                                                appliedCatalog.Encounters,
                                                initialCatalog.Encounters) &&
                                            SameReferencesUx1AI(
                                                appliedCatalog.VehicleRoutes,
                                                initialCatalog.VehicleRoutes) &&
                                            SameReferencesUx1AI(
                                                appliedCatalog.Merchants,
                                                initialCatalog.Merchants) &&
                                            SameReferencesUx1AI(
                                                appliedCatalog.Companions,
                                                initialCatalog.Companions) &&
                                            ReferenceEquals(appliedCatalog.Scripts, initialCatalog.Scripts);

            var undo = editing.Undo();
            var removed = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id).Foundation!.Validation is null;
            var redo = editing.Redo();
            var restored = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id).Foundation!;
            var undoRedo = undo.Succeeded && removed && redo.Succeeded &&
                           restored.Validation is not null &&
                           SameValidationUx1AI(freshDraft, restored.Validation) &&
                           restored.ValidationRequiredAtRuntime;

            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistedTarget = opened
                ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item => item.Id == targetProfile.Id)
                : null;
            var persistedNeighbor = opened
                ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item => item.Id == neighborProfile.Id)
                : null;
            var persistence = persistedTarget is
            { Foundation: { Validation: { } persistedValidation } persistedFoundation } &&
                              SameValidationUx1AI(freshDraft, persistedValidation) &&
                              persistedFoundation.ValidationRequiredAtRuntime &&
                              SameTriggerUx1AI(appliedTarget, persistedTarget) &&
                              persistedNeighbor is not null && SameTriggerUx1AI(appliedNeighbor, persistedNeighbor);

            reopened.Close();
            var optionalSourceDraft = editing.CreateTriggerValidationDraftUx1AI(targetProfile.Id);
            if (optionalSourceDraft is null)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AI_CONTRACT_FAILED stage=optional-draft");
                return FailedUx1AISmoke();
            }
            var optionalDraft = optionalSourceDraft with
            {
                ValidationEnabled = false,
                ValidationRequiredAtRuntime = false,
            };
            var optionalPreview = editing.PreviewTriggerValidationUx1AI(optionalDraft);
            var optionalGroups = optionalPreview.Groups;
            var optionalApply = editing.ApplyApprovedTriggerValidationUx1AI();
            var optionalAppliedTarget = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id);
            var optionalRemoved = optionalAppliedTarget.Foundation is
            {
                Validation: null,
                ValidationRequiredAtRuntime: false,
            };
            var optionalUndo = editing.Undo();
            var optionalUndoTarget = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id);
            var optionalRedo = editing.Redo();
            var optionalRedoTarget = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id);
            var optionalSaved = editing.Save();
            var optionalOpened = optionalSaved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var optionalPersistedTarget = optionalOpened
                ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item => item.Id == targetProfile.Id)
                : null;
            var optionalSuppression = optionalPreview.Succeeded && optionalPreview.CanApply &&
                                      optionalPreview.OptionalFallback && optionalApply.Succeeded && optionalRemoved &&
                                      optionalGroups.Count == 7 &&
                                      optionalGroups.Where(item => item.Key != "context").All(item =>
                                          item.State == "Non évalué" && !item.Blocking) &&
                                      optionalGroups.Single(item => item.Key == "context") is
                                      { State: "Fallback optionnel", Blocking: false } &&
                                      optionalUndo.Succeeded && SameTriggerUx1AI(appliedTarget, optionalUndoTarget) &&
                                      optionalRedo.Succeeded &&
                                      SameTriggerUx1AI(optionalAppliedTarget, optionalRedoTarget) &&
                                      optionalPersistedTarget is not null &&
                                      SameTriggerUx1AI(optionalAppliedTarget, optionalPersistedTarget);

            var journal = editing.History!.Journal;
            var canonicalCommand = journal.Count == 4 &&
                                   journal.All(item => item.CommandName == "set_world_feature_catalog") &&
                                   journal.Count(item => item.IsApplied) == 2;

            var invalidContextBlocked = HasBlockingGroupUx1AI(invalidContext, "context");
            var missingTargetBlocked = HasBlockingGroupUx1AI(missingTarget, "actions");
            var missingScriptBlocked = HasBlockingGroupUx1AI(missingScript, "scripts");
            var missingPortalBlocked = HasBlockingGroupUx1AI(missingPortal, "portal");
            var inaccessibleBlocked = HasBlockingGroupUx1AI(inaccessible, "filters");
            var structuredDiagnostics = new[]
                {
                    invalidContext,
                    missingTarget,
                    missingScript,
                    missingPortal,
                    inaccessible,
                }
                .SelectMany(item => item.Groups.Where(group => group.Blocking))
                .All(group => group.Diagnostics.Count > 0 &&
                              group.Diagnostics.Count == group.Details.Count &&
                              group.Diagnostics.All(item =>
                                  !string.IsNullOrWhiteSpace(item.Code) &&
                                  !string.IsNullOrWhiteSpace(item.Path) &&
                                  !string.IsNullOrWhiteSpace(item.Message)));
            var groupedDiagnostics = preview.Groups.Count == 7 &&
                                     preview.Groups.Select(item => item.Key).Distinct().Count() == 7 &&
                                     preview.Groups.All(item => !string.IsNullOrWhiteSpace(item.Title)) &&
                                     structuredDiagnostics;
            var humanDiagnostics = new[]
            {
                invalidContext.Message,
                missingTarget.Message,
                missingScript.Message,
                missingPortal.Message,
                inaccessible.Message,
                preview.Message,
                optionalPreview.Message,
            }.All(IsHumanUx1AIMessage) &&
                new[] { invalidContext, missingTarget, missingScript, missingPortal, inaccessible, optionalPreview }
                    .SelectMany(item => item.Groups)
                    .SelectMany(item => item.Details)
                    .All(IsHumanUx1AIMessage);
            var succeeded = selectionExplicit && foundationRequired && contextCreation && requiredFlag &&
                            runtimeResolution && dryRun && invalidContextBlocked && missingTargetBlocked &&
                            missingScriptBlocked && missingPortalBlocked && inaccessibleBlocked &&
                            optionalSuppression && groupedDiagnostics && staleDraftBlocked &&
                            staleApprovalBlocked && canonicalCommand &&
                            undoRedo && persistence && foundationNeighborsPreserved &&
                            triggerNeighborsPreserved && humanDiagnostics;
            return new StudioUx1AISmokeResult(
                succeeded,
                selectionExplicit,
                foundationRequired,
                contextCreation,
                requiredFlag,
                runtimeResolution,
                dryRun,
                invalidContextBlocked,
                missingTargetBlocked,
                missingScriptBlocked,
                missingPortalBlocked,
                inaccessibleBlocked,
                optionalSuppression,
                groupedDiagnostics,
                staleDraftBlocked,
                staleApprovalBlocked,
                canonicalCommand,
                undoRedo,
                persistence,
                foundationNeighborsPreserved,
                triggerNeighborsPreserved,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AI_CONTRACT_FAILED stage=exception " + exception);
            return FailedUx1AISmoke();
        }
        finally
        {
            reopened.Close();
            editing.Close();
            DeleteUx1AITemporaryRoot(tempRoot, "openrpg-forge-studio-ux1ai-");
        }
    }

    private static StudioTriggerValidationDraft ConfigureUx1AISmokeDraft(StudioTriggerValidationDraft draft)
    {
        var profile = draft.OriginalProfile;
        var foundation = profile.Foundation!;
        var scripts = foundation.Actions
            .SelectMany(item => item.Parameters
                .Where(pair => string.Equals(pair.Key, "script", StringComparison.Ordinal))
                .Select(pair => pair.Value))
            .Concat(new[]
            {
                foundation.OnEnter.BeforeScriptId,
                foundation.OnEnter.AfterScriptId,
                foundation.OnExit.BeforeScriptId,
                foundation.OnExit.AfterScriptId,
                profile.Interaction?.ScriptId,
            }.OfType<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return draft with
        {
            ValidationEnabled = true,
            ValidationRequiredAtRuntime = true,
            KnownPortalDestinationIds = foundation.Portal is { } portal ? [portal.DestinationId] : [],
            KnownTargetIds = foundation.Actions.Select(item => item.TargetId)
                .Distinct(StringComparer.Ordinal).ToArray(),
            KnownScriptIds = scripts,
            AccessibleLayers = foundation.Filter.Layers.Distinct(StringComparer.Ordinal).ToArray(),
        };
    }

    private static bool ApplyUx1AIIntermediateMutation(
        StudioSessionController session,
        Guid profileId,
        string customNamespace)
    {
        var history = session.History;
        var project = session.Project;
        if (history is null || project is null)
        {
            return false;
        }
        var catalog = project.Module.WorldFeatures;
        var candidate = catalog.Triggers.Select(item => item.Id == profileId
            ? item with { CustomEventNamespace = customNamespace }
            : item).ToArray();
        var command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            new WorldFeatureCatalog(
                catalog.Id,
                catalog.SchemaVersion,
                catalog.Provenance,
                catalog.Placeables,
                catalog.Doors,
                candidate,
                catalog.Encounters,
                catalog.VehicleRoutes,
                catalog.Merchants,
                catalog.Companions,
                catalog.Scripts),
            false);
        var result = new SetWorldFeatureCatalogHandler(new SystemClock()).Handle(history.State, command);
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    private static TriggerWorldProfile CreateUx1AISmokeTrigger(
        BlueprintId blueprintId,
        AreaId areaId,
        Guid seed,
        bool foundation,
        bool validation,
        Guid? narrativeDialogueId = null,
        BlueprintId? narrativeParticipantId = null,
        string? foundationSlug = null)
    {
        var profileId = StableTriggerValidationIdUx1AI(seed, "profile");
        if (!foundation)
        {
            return new TriggerWorldProfile(
                profileId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ai",
                blueprintId,
                areaId,
                null,
                null,
                null,
                $"trigger.test.{seed:N}",
                null,
                null,
                null,
                null,
                null);
        }

        var destinationId = StableTriggerValidationIdUx1AI(seed, "destination");
        var actionTarget = $"encounter:{seed:N}";
        var actionScript = $"script:action:{seed:N}";
        var enterScript = $"script:enter:{seed:N}";
        var exitScript = $"script:exit:{seed:N}";
        var interactionScript = $"script:interaction:{seed:N}";
        var runtimeValidation = validation
            ? new TriggerWorldProfile.RuntimeValidationPolicy(
                [destinationId],
                [actionTarget],
                [actionScript, enterScript, exitScript, interactionScript],
                ["characters"])
            : null;
        var foundationPolicy = new TriggerWorldProfile.FoundationPolicy(
            StableTriggerValidationIdUx1AI(seed, "foundation"),
            WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux1-ai",
            foundationSlug ?? $"trigger-{seed:N}",
            new TriggerWorldProfile.ShapePolicy(
                TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                [],
                new Vector3Value(4, 3, 4),
                3,
                .25,
                true),
            new TriggerWorldProfile.FilterPolicy(["creature"], ["player"], ["spectator"], ["characters"]),
            ["quest.active"],
            1.5,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new TriggerWorldProfile.ActionPolicy(
                StableTriggerValidationIdUx1AI(seed, "action"),
                "script",
                actionTarget,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["script"] = actionScript,
                    ["authored.note"] = "preserve",
                })],
            new TriggerWorldProfile.PortalPolicy(
                TriggerWorldProfile.DestinationKind.Trigger,
                destinationId,
                "spawn:sanctuary",
                new Vector3Value(0, 90, 0),
                true,
                ["party"]),
            new TriggerWorldProfile.TransformPolicy(
                new Vector3Value(3, 0, 5),
                new Vector3Value(0, 15, 0),
                Vector3Value.One,
                null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["editor.overlay"] = "volume",
                ["authored.neighbor"] = "preserve",
            },
            new TriggerWorldProfile.EventPolicy(
                StableTriggerValidationIdUx1AI(seed, "enter"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ai",
                "1.0.0",
                "VolumeEntered",
                enterScript,
                null),
            new TriggerWorldProfile.EventPolicy(
                StableTriggerValidationIdUx1AI(seed, "exit"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ai",
                "1.0.0",
                "VolumeExited",
                null,
                exitScript),
            runtimeValidation,
            validation);
        return new TriggerWorldProfile(
            profileId,
            WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux1-ai",
            blueprintId,
            areaId,
            new TriggerWorldProfile.DiscoveryPolicy(
                ["vision"],
                12,
                StableTriggerValidationIdUx1AI(seed, "reveal"),
                "feedback:revealed",
                true,
                TriggerWorldProfile.DiscoveryScope.Party),
            new TriggerWorldProfile.InteractionPolicy(
                "Inspecter",
                "icon:inspect",
                "outline:gold",
                "trigger.inspect",
                interactionScript,
                true,
                "party"),
            new TriggerWorldProfile.SchedulerPolicy(2, 30, true, true),
            $"trigger.test.{seed:N}",
            new TriggerWorldProfile.AccessPolicy("credential:warden", false, "trigger.denied"),
            new TriggerWorldProfile.RestPolicy(true, "hostile.nearby", true, true, "rest.allowed", "rest.denied"),
            new TriggerWorldProfile.CluePolicy("perception", 10, "trace:ash", 45, 2, null, null),
            narrativeDialogueId is { } dialogueId
                ? new TriggerWorldProfile.NarrativeCuePolicy(
                    [narrativeParticipantId ?? blueprintId],
                    4,
                    true,
                    60,
                    "party-vote",
                    dialogueId)
                : null,
            new TriggerWorldProfile.DirectorPolicy(true, true, true, true, true),
            foundationPolicy,
            true,
            narrativeDialogueId.HasValue ? CreateUx1AISmokeAdvanced(seed) : null,
            narrativeDialogueId.HasValue);
    }

    private static TriggerAdvancedProfile CreateUx1AISmokeAdvanced(Guid seed)
    {
        var volumes = Enum.GetValues<TriggerGameplayVolumeKind>()
            .Select((kind, index) => new TriggerGameplayVolumeProfile(
                StableTriggerValidationIdUx1AI(seed, $"advanced-volume-{kind}"),
                kind,
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    [kind.ToString().ToLowerInvariant()] = index + 1,
                },
                new Dictionary<string, string>(StringComparer.Ordinal),
                $"{kind}VolumeEntered",
                $"{kind}VolumeExited",
                true))
            .ToArray();
        return new TriggerAdvancedProfile(
            StableTriggerValidationIdUx1AI(seed, "advanced"),
            WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux1-ai-advanced",
            "1.0.0",
            new TriggerTrapProfile(
                StableTriggerValidationIdUx1AI(seed, "advanced-trap"),
                "enter",
                "damage:ux1-ai",
                10,
                12,
                30,
                "module",
                "vfx:ux1-ai",
                "owner-safe",
                true,
                false,
                true,
                "#ff7a45",
                "TrapDetected",
                "TrapDisarmed",
                "TrapTriggered",
                "TrapReset"),
            new TriggerEntityQueryProfile(
                StableTriggerValidationIdUx1AI(seed, "advanced-query"),
                true,
                ["player"],
                ["undead"],
                [],
                ["health"],
                "party",
                "heroes",
                "quest==active"),
            volumes,
            new TriggerOccupancyDebugProfile(
                StableTriggerValidationIdUx1AI(seed, "advanced-debug"),
                true,
                true,
                true,
                true,
                true,
                true,
                64));
    }

    private static bool HasBlockingGroupUx1AI(StudioTriggerValidationPreview preview, string key) =>
        !preview.Succeeded && preview.Groups.SingleOrDefault(item => item.Key == key)?.Blocking == true;

    private static bool SameValidationUx1AI(
        StudioTriggerValidationDraft expected,
        TriggerWorldProfile.RuntimeValidationPolicy actual) =>
        expected.KnownPortalDestinationIds.SequenceEqual(actual.KnownPortalDestinationIds) &&
        expected.KnownTargetIds.SequenceEqual(actual.KnownTargetIds) &&
        expected.KnownScriptIds.SequenceEqual(actual.KnownScriptIds) &&
        expected.AccessibleLayers.SequenceEqual(actual.AccessibleLayers);

    private static bool SameFoundationNeighborsByReferenceUx1AI(
        TriggerWorldProfile.FoundationPolicy expected,
        TriggerWorldProfile.FoundationPolicy? actual) =>
        actual is not null &&
        expected.Id == actual.Id &&
        expected.SchemaVersion == actual.SchemaVersion &&
        expected.Provenance == actual.Provenance &&
        expected.Slug == actual.Slug &&
        ReferenceEquals(expected.Shape, actual.Shape) &&
        ReferenceEquals(expected.Filter, actual.Filter) &&
        ReferenceEquals(expected.Conditions, actual.Conditions) &&
        expected.CooldownSeconds == actual.CooldownSeconds &&
        expected.Fire == actual.Fire &&
        ReferenceEquals(expected.Actions, actual.Actions) &&
        ReferenceEquals(expected.Portal, actual.Portal) &&
        ReferenceEquals(expected.Transform, actual.Transform) &&
        ReferenceEquals(expected.Overrides, actual.Overrides) &&
        ReferenceEquals(expected.OnEnter, actual.OnEnter) &&
        ReferenceEquals(expected.OnExit, actual.OnExit);

    private static bool SameTriggerNeighborsByReferenceUx1AI(
        TriggerWorldProfile expected,
        TriggerWorldProfile actual) =>
        expected.Id == actual.Id &&
        expected.SchemaVersion == actual.SchemaVersion &&
        expected.Provenance == actual.Provenance &&
        expected.BlueprintId == actual.BlueprintId &&
        expected.AreaId == actual.AreaId &&
        ReferenceEquals(expected.Discovery, actual.Discovery) &&
        ReferenceEquals(expected.Interaction, actual.Interaction) &&
        ReferenceEquals(expected.Scheduler, actual.Scheduler) &&
        expected.CustomEventNamespace == actual.CustomEventNamespace &&
        ReferenceEquals(expected.Access, actual.Access) &&
        ReferenceEquals(expected.Rest, actual.Rest) &&
        ReferenceEquals(expected.Clue, actual.Clue) &&
        ReferenceEquals(expected.Narrative, actual.Narrative) &&
        ReferenceEquals(expected.Director, actual.Director) &&
        expected.FoundationRequiredAtRuntime == actual.FoundationRequiredAtRuntime &&
        ReferenceEquals(expected.Advanced, actual.Advanced) &&
        expected.AdvancedRequiredAtRuntime == actual.AdvancedRequiredAtRuntime;

    private static bool SameTriggerUx1AI(TriggerWorldProfile expected, TriggerWorldProfile actual) =>
        expected.Id == actual.Id &&
        expected.SchemaVersion == actual.SchemaVersion &&
        expected.Provenance == actual.Provenance &&
        expected.BlueprintId == actual.BlueprintId &&
        expected.AreaId == actual.AreaId &&
        SameDiscoveryUx1AI(expected.Discovery, actual.Discovery) &&
        expected.Interaction == actual.Interaction &&
        expected.Scheduler == actual.Scheduler &&
        expected.CustomEventNamespace == actual.CustomEventNamespace &&
        expected.Access == actual.Access &&
        expected.Rest == actual.Rest &&
        expected.Clue == actual.Clue &&
        SameNarrativeUx1AI(expected.Narrative, actual.Narrative) &&
        expected.Director == actual.Director &&
        expected.Foundation is not null && actual.Foundation is not null &&
        SameFoundationUx1AI(expected.Foundation, actual.Foundation) &&
        expected.FoundationRequiredAtRuntime == actual.FoundationRequiredAtRuntime &&
        (expected.Advanced is null && actual.Advanced is null ||
         expected.Advanced is { } expectedAdvanced && actual.Advanced is { } actualAdvanced &&
         SameAdvancedUx1AI(expectedAdvanced, actualAdvanced)) &&
        expected.AdvancedRequiredAtRuntime == actual.AdvancedRequiredAtRuntime;

    private static bool SameNarrativeUx1AI(
        TriggerWorldProfile.NarrativeCuePolicy? expected,
        TriggerWorldProfile.NarrativeCuePolicy? actual) =>
        expected is null && actual is null ||
        expected is { } left && actual is { } right &&
        left.Participants.SequenceEqual(right.Participants) &&
        left.Priority == right.Priority &&
        left.Once == right.Once &&
        left.CooldownSeconds == right.CooldownSeconds &&
        left.MultiplayerPolicy == right.MultiplayerPolicy &&
        left.DialogueId == right.DialogueId;

    private static bool SameDiscoveryUx1AI(
        TriggerWorldProfile.DiscoveryPolicy? expected,
        TriggerWorldProfile.DiscoveryPolicy? actual) =>
        expected is null && actual is null ||
        expected is { } left && actual is { } right &&
        left.Senses.SequenceEqual(right.Senses) &&
        left.Difficulty == right.Difficulty &&
        left.RevealTargetId == right.RevealTargetId &&
        left.FeedbackId == right.FeedbackId &&
        left.Permanent == right.Permanent &&
        left.Scope == right.Scope;

    private static bool SameFoundationUx1AI(
        TriggerWorldProfile.FoundationPolicy expected,
        TriggerWorldProfile.FoundationPolicy actual)
    {
        var shape = expected.Shape.Kind == actual.Shape.Kind &&
                    expected.Shape.Vertices.SequenceEqual(actual.Shape.Vertices) &&
                    expected.Shape.Holes.Count == actual.Shape.Holes.Count &&
                    expected.Shape.Holes.Zip(actual.Shape.Holes).All(pair =>
                        pair.First.SequenceEqual(pair.Second)) &&
                    expected.Shape.Size == actual.Shape.Size &&
                    expected.Shape.Height == actual.Shape.Height &&
                    expected.Shape.SnapIncrement == actual.Shape.SnapIncrement &&
                    expected.Shape.VisualizeInEditor == actual.Shape.VisualizeInEditor;
        var filter = expected.Filter.EntityKinds.SequenceEqual(actual.Filter.EntityKinds) &&
                     expected.Filter.RequiredTags.SequenceEqual(actual.Filter.RequiredTags) &&
                     expected.Filter.ExcludedTags.SequenceEqual(actual.Filter.ExcludedTags) &&
                     expected.Filter.Layers.SequenceEqual(actual.Filter.Layers);
        var actions = expected.Actions.Count == actual.Actions.Count &&
                      expected.Actions.Zip(actual.Actions).All(pair =>
                          pair.First.Id == pair.Second.Id &&
                          pair.First.Kind == pair.Second.Kind &&
                          pair.First.TargetId == pair.Second.TargetId &&
                          pair.First.Parameters.OrderBy(item => item.Key).SequenceEqual(
                              pair.Second.Parameters.OrderBy(item => item.Key)));
        var portal = expected.Portal is null && actual.Portal is null ||
                     expected.Portal is { } expectedPortal && actual.Portal is { } actualPortal &&
                     expectedPortal.Destination == actualPortal.Destination &&
                     expectedPortal.DestinationId == actualPortal.DestinationId &&
                     expectedPortal.SpawnAnchorId == actualPortal.SpawnAnchorId &&
                     expectedPortal.ExitOrientationDegrees == actualPortal.ExitOrientationDegrees &&
                     expectedPortal.Preload == actualPortal.Preload &&
                     expectedPortal.Permissions.SequenceEqual(actualPortal.Permissions);
        var validation = expected.Validation is null && actual.Validation is null ||
                         expected.Validation is { } expectedValidation &&
                         actual.Validation is { } actualValidation &&
                         expectedValidation.KnownPortalDestinationIds.SequenceEqual(
                             actualValidation.KnownPortalDestinationIds) &&
                         expectedValidation.KnownTargetIds.SequenceEqual(actualValidation.KnownTargetIds) &&
                         expectedValidation.KnownScriptIds.SequenceEqual(actualValidation.KnownScriptIds) &&
                         expectedValidation.AccessibleLayers.SequenceEqual(actualValidation.AccessibleLayers);
        return expected.Id == actual.Id &&
               expected.SchemaVersion == actual.SchemaVersion &&
               expected.Provenance == actual.Provenance &&
               expected.Slug == actual.Slug &&
               shape &&
               filter &&
               expected.Conditions.SequenceEqual(actual.Conditions) &&
               expected.CooldownSeconds == actual.CooldownSeconds &&
               expected.Fire == actual.Fire &&
               actions &&
               portal &&
               expected.Transform == actual.Transform &&
               expected.Overrides.OrderBy(item => item.Key).SequenceEqual(
                   actual.Overrides.OrderBy(item => item.Key)) &&
               expected.OnEnter == actual.OnEnter &&
               expected.OnExit == actual.OnExit &&
               validation &&
               expected.ValidationRequiredAtRuntime == actual.ValidationRequiredAtRuntime;
    }

    private static bool SameAdvancedUx1AI(TriggerAdvancedProfile expected, TriggerAdvancedProfile actual) =>
        expected.Id == actual.Id &&
        expected.SchemaVersion == actual.SchemaVersion &&
        expected.Provenance == actual.Provenance &&
        expected.ForgeScriptApiVersion == actual.ForgeScriptApiVersion &&
        expected.Trap == actual.Trap &&
        expected.TargetFilter.Id == actual.TargetFilter.Id &&
        expected.TargetFilter.PlayersOnly == actual.TargetFilter.PlayersOnly &&
        expected.TargetFilter.RequiredTags.SequenceEqual(actual.TargetFilter.RequiredTags) &&
        expected.TargetFilter.ExcludedTags.SequenceEqual(actual.TargetFilter.ExcludedTags) &&
        expected.TargetFilter.FactionIds.SequenceEqual(actual.TargetFilter.FactionIds) &&
        expected.TargetFilter.RequiredComponents.SequenceEqual(actual.TargetFilter.RequiredComponents) &&
        expected.TargetFilter.OwnerId == actual.TargetFilter.OwnerId &&
        expected.TargetFilter.TeamId == actual.TargetFilter.TeamId &&
        expected.TargetFilter.SandboxPredicate == actual.TargetFilter.SandboxPredicate &&
        expected.GameplayVolumes.Count == actual.GameplayVolumes.Count &&
        expected.GameplayVolumes.Zip(actual.GameplayVolumes).All(pair =>
            pair.First.Id == pair.Second.Id &&
            pair.First.Kind == pair.Second.Kind &&
            pair.First.NumericParameters.OrderBy(item => item.Key).SequenceEqual(
                pair.Second.NumericParameters.OrderBy(item => item.Key)) &&
            pair.First.TextParameters.OrderBy(item => item.Key).SequenceEqual(
                pair.Second.TextParameters.OrderBy(item => item.Key)) &&
            pair.First.EnterBinding == pair.Second.EnterBinding &&
            pair.First.ExitBinding == pair.Second.ExitBinding &&
            pair.First.Enabled == pair.Second.Enabled) &&
        expected.OccupancyDebug == actual.OccupancyDebug;

    private static bool SameReferencesUx1AI<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class =>
        left.Count == right.Count && left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx1AIMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains('{') &&
        !message.Contains("Guid", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !Ux1AIGuidPattern.IsMatch(message);

    private static bool DeleteUx1AITemporaryRoot(string? tempRoot, string requiredPrefix)
    {
        if (tempRoot is null)
        {
            return true;
        }
        try
        {
            var fullRoot = Path.GetFullPath(tempRoot);
            var tempBase = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(fullRoot);
            if (!fullRoot.StartsWith(tempBase, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, recursive: true);
            }
            return !Directory.Exists(fullRoot);
        }
        catch
        {
            return false;
        }
    }

    private static StudioUx1AISmokeResult FailedUx1AISmoke() => new(
        Succeeded: false,
        SelectionExplicit: false,
        FoundationRequired: false,
        ContextCreation: false,
        RequiredFlag: false,
        RuntimeResolution: false,
        DryRun: false,
        InvalidContextBlocked: false,
        MissingTargetBlocked: false,
        MissingScriptBlocked: false,
        MissingPortalBlocked: false,
        InaccessibleBlocked: false,
        OptionalSuppression: false,
        GroupedDiagnostics: false,
        StaleDraftBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        FoundationNeighborsPreserved: false,
        TriggerNeighborsPreserved: false,
        HumanDiagnostics: false);
}
