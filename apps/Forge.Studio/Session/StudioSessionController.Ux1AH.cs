using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1AHProvenance = "studio:ux1-ah";
    private readonly SetWorldFeatureCatalogHandler _setTriggerAutomationDirectorCatalogUx1AH =
        new(new SystemClock());
    private StudioTriggerAutomationDirectorDraft? _approvedTriggerAutomationDirectorUx1AH;
    private ProjectRevision? _approvedTriggerAutomationDirectorRevisionUx1AH;

    internal IReadOnlyList<StudioTriggerAutomationDirectorChoice> TriggerAutomationDirectorChoicesUx1AH
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
                    var state = item.Foundation is null
                        ? "volume de base requis"
                        : item.Scheduler is null && item.Director is null &&
                          string.IsNullOrWhiteSpace(item.CustomEventNamespace)
                            ? "automatisation à configurer"
                            : "automatisation configurée";
                    return new StudioTriggerAutomationDirectorChoice(
                        item.Id,
                        item.BlueprintId,
                        $"{name} · {instances} instance(s) · {state}",
                        item.Foundation is not null,
                        item.Scheduler is not null,
                        !string.IsNullOrWhiteSpace(item.CustomEventNamespace),
                        item.Director is not null,
                        string.Equals(item.Provenance, Ux1AHProvenance, StringComparison.Ordinal));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal ProjectDefinition? CreateUx1AHEvidenceProject()
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
            .Concat(project.Dialogues.Select(item => item.Id.Value))
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
        Guid[] advancedIds;
        do
        {
            ids = Enumerable.Range(0, 13)
                .Select(index => StableTriggerIdUx1AH(
                    project.Id.Value,
                    $"evidence-{area.Id.Value:N}-{salt}-{index}"))
                .ToArray();
            advancedIds = TriggerAdvancedComponentIdsUx1AE(ids[6])
                .Concat(TriggerAdvancedComponentIdsUx1AE(ids[7]))
                .ToArray();
            salt++;
        } while (ids.Concat(advancedIds).Distinct().Count() != ids.Length + advancedIds.Length ||
                 ids.Concat(advancedIds).Any(occupied.Contains));

        var triggerSlug = UniqueBlueprintSlugUx1AH(project, "ux1-ah-automation-trigger");
        var neighborSlug = UniqueBlueprintSlugUx1AH(project, "ux1-ah-neighbor-trigger", triggerSlug);
        var itemSlug = UniqueBlueprintSlugUx1AH(project, "ux1-ah-preserved-key", triggerSlug, neighborSlug);
        var triggerBlueprint = new BlueprintDefinition(
            BlueprintId.From(ids[0]),
            triggerSlug,
            "Ronde du sanctuaire",
            BlueprintKind.Trigger);
        var neighborBlueprint = new BlueprintDefinition(
            BlueprintId.From(ids[1]),
            neighborSlug,
            "Seuil voisin préservé",
            BlueprintKind.Trigger);
        var itemBlueprint = new BlueprintDefinition(
            BlueprintId.From(ids[2]),
            itemSlug,
            "Sceau témoin",
            BlueprintKind.Item);
        var dialogueId = DialogueId.From(ids[3]);
        var nodeId = DialogueNodeId.From(ids[4]);
        var dialogue = new DialogueDefinition(
            dialogueId,
            "ux1-ah-evidence-dialogue",
            "Annonce de la ronde",
            nodeId,
            [new(nodeId, null, new("dialogue.ux1ah", "La ronde du sanctuaire se poursuit."))],
            provenance: Ux1AHProvenance,
            entryPoints:
            [new(ids[5], WorldFeatureCatalog.CurrentSchemaVersion, "default", nodeId, 0, Ux1AHProvenance)]);
        var profile = CreateConfiguredTriggerUx1AE(
            ids[6],
            triggerBlueprint.Id,
            area.Id,
            ids[7],
            itemBlueprint.Id,
            dialogueId.Value,
            "ah-evidence") with
        {
            Provenance = Ux1AHProvenance,
            Scheduler = new TriggerWorldProfile.SchedulerPolicy(2.5, 24, true, true),
            CustomEventNamespace = "trigger.sanctuary.round",
            Director = new TriggerWorldProfile.DirectorPolicy(true, true, true, true, true),
        };
        var neighbor = CreateConfiguredTriggerUx1AE(
            ids[7],
            neighborBlueprint.Id,
            area.Id,
            profile.Id,
            itemBlueprint.Id,
            dialogueId.Value,
            "ah-neighbor");
        var evidenceCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.Placeables,
            catalog.Doors,
            catalog.Triggers.Append(profile).Append(neighbor).ToArray(),
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        return new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(evidenceCatalog),
            project.Blueprints.Append(triggerBlueprint).Append(neighborBlueprint).Append(itemBlueprint).ToArray(),
            project.Areas,
            project.Dialogues.Append(dialogue).ToArray(),
            project.Quests);
    }

    internal StudioTriggerAutomationDirectorDraft? CreateTriggerAutomationDirectorDraftUx1AH(
        Guid? requestedProfileId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedProfileId is null)
        {
            return null;
        }

        var profile = project.Module.WorldFeatures.Triggers.SingleOrDefault(item =>
            item.Id == requestedProfileId.Value && item.AreaId == ActiveAreaId);
        if (profile?.Foundation is null)
        {
            return null;
        }
        var blueprint = project.Blueprints.FirstOrDefault(item => item.Id == profile.BlueprintId);
        var scheduler = profile.Scheduler;
        var director = profile.Director;
        return new StudioTriggerAutomationDirectorDraft(
            profile.Id,
            profile.BlueprintId,
            profile.AreaId,
            history.State.Revision,
            scheduler is not null,
            scheduler?.IntervalSeconds ?? 2,
            scheduler?.BudgetPerMinute ?? 30,
            scheduler?.WhileOccupiedOnly ?? true,
            scheduler?.AggregateOccupants ?? true,
            string.IsNullOrWhiteSpace(profile.CustomEventNamespace)
                ? DefaultTriggerNamespaceUx1AH(blueprint?.Slug)
                : profile.CustomEventNamespace,
            director is not null,
            director?.AllowManualEnter ?? false,
            director?.AllowManualExit ?? false,
            director?.AllowManualFire ?? false,
            director?.RequireTarget ?? true,
            director?.Audit ?? true,
            profile);
    }

    internal StudioTriggerAutomationDirectorPreview PreviewTriggerAutomationDirectorUx1AH(
        StudioTriggerAutomationDirectorDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTriggerAutomationDirectorUx1AH();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedTriggerAutomationDirectorPreviewUx1AH("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedTriggerAutomationDirectorPreviewUx1AH(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le déclencheur avant de prévisualiser.");
        }
        if (!TryBuildTriggerAutomationDirectorUx1AH(draft, true, out var command, out var profile, out var message))
        {
            return FailedTriggerAutomationDirectorPreviewUx1AH(message);
        }

        var result = _setTriggerAutomationDirectorCatalogUx1AH.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            return FailedTriggerAutomationDirectorPreviewUx1AH(
                HumanizeTriggerAutomationDirectorUx1AH(result.Diagnostics.FirstOrDefault()?.Code));
        }

        var configured = profile!;
        var scheduler = configured.Scheduler;
        var inputs = TriggerEvaluationInputsUx1AH(configured);
        var unoccupied = WorldFeatureRuntimeResolver.EvaluateTrigger(
            configured,
            inputs.Perception,
            inputs.Credentials,
            inputs.Permissions,
            false,
            0,
            0,
            false);
        var occupancyPolicyHonored = scheduler is null ||
                                     (scheduler.WhileOccupiedOnly &&
                                      unoccupied is { Fired: false, Reason: "unoccupied" }) ||
                                     (!scheduler.WhileOccupiedOnly && unoccupied.Fired);
        var budget = scheduler is null
            ? null
            : WorldFeatureRuntimeResolver.EvaluateTrigger(
                configured,
                inputs.Perception,
                inputs.Credentials,
                inputs.Permissions,
                false,
                2,
                scheduler.BudgetPerMinute,
                false);
        var scheduled = scheduler is null
            ? null
            : WorldFeatureRuntimeResolver.EvaluateTrigger(
                configured,
                inputs.Perception,
                inputs.Credentials,
                inputs.Permissions,
                false,
                2,
                Math.Max(0, scheduler.BudgetPerMinute - 1),
                false);
        var budgetBlocked = scheduler is null || budget is { Fired: false, Reason: "budget" };
        var scheduledAllowed = scheduler is null || scheduled is { Fired: true, Reason: "activated" };
        var manual = WorldFeatureRuntimeResolver.EvaluateTrigger(
            configured,
            inputs.Perception,
            inputs.Credentials,
            inputs.Permissions,
            false,
            1,
            0,
            true);
        var manualFireAllowed = configured.Director is { AllowManualFire: true };
        var manualFirePolicyHonored = manualFireAllowed
            ? manual is { Fired: true, Reason: "director" }
            : manual is { Fired: false, Reason: "director-forbidden" };
        var auditRecorded = !manualFireAllowed || manual.Effects.Contains("director:audit", StringComparer.Ordinal);
        var namespaceValid = IsNamespacedTriggerEventUx1AH(configured.CustomEventNamespace);
        if (!occupancyPolicyHonored || !budgetBlocked || !scheduledAllowed || !manualFirePolicyHonored ||
            !auditRecorded || !namespaceValid)
        {
            return FailedTriggerAutomationDirectorPreviewUx1AH(
                "La politique ne produit pas les décisions d’occupation, budget ou contrôle MJ attendues.");
        }

        var summary =
            $"Heartbeat : {(scheduler is null ? "désactivé" : $"toutes les {scheduler.IntervalSeconds:0.###} s · budget {scheduler.BudgetPerMinute}/min · {(scheduler.WhileOccupiedOnly ? "seulement si occupé" : "même sans occupant")} · {(scheduler.AggregateOccupants ? "occupants agrégés" : "occupants séparés")}")}.\n" +
            $"Événement personnalisé : espace nommé {HumanizeNamespaceUx1AH(configured.CustomEventNamespace)}.\n" +
            $"Contrôle MJ : {HumanizeDirectorUx1AH(configured.Director)}.\n" +
            "Simulation locale pure : occupation, budget et autorisation de tir manuel évalués ; aucun timer démarré, aucune cible contactée et aucune commande Live Director envoyée.\n" +
            "Secret, clic, accès, Foundation, Portal, Validation, profil avancé et politiques UX2 restent préservés.";
        _approvedTriggerAutomationDirectorUx1AH = draft;
        _approvedTriggerAutomationDirectorRevisionUx1AH = history.State.Revision;
        return new StudioTriggerAutomationDirectorPreview(
            true,
            "Aperçu validé · aucun document ni session live n’a été modifié.",
            summary,
            occupancyPolicyHonored,
            budgetBlocked,
            scheduledAllowed,
            namespaceValid,
            manualFirePolicyHonored,
            auditRecorded,
            LiveDispatchAttempted: false,
            CanApply: true);
    }

    internal StudioUx1AHActionResult ApplyApprovedTriggerAutomationDirectorUx1AH()
    {
        var draft = _approvedTriggerAutomationDirectorUx1AH;
        var history = History;
        if (draft is null || history is null ||
            draft.BaseRevision != history.State.Revision ||
            _approvedTriggerAutomationDirectorRevisionUx1AH != history.State.Revision)
        {
            CancelTriggerAutomationDirectorUx1AH();
            return new StudioUx1AHActionResult(
                false,
                "Prévisualisez de nouveau l’automatisation et le contrôle MJ avant de les appliquer.");
        }
        if (!TryBuildTriggerAutomationDirectorUx1AH(draft, false, out var command, out _, out var message))
        {
            CancelTriggerAutomationDirectorUx1AH();
            return new StudioUx1AHActionResult(false, message);
        }

        var result = _setTriggerAutomationDirectorCatalogUx1AH.Handle(history.State, command!);
        CancelTriggerAutomationDirectorUx1AH();
        if (!result.Succeeded)
        {
            return new StudioUx1AHActionResult(
                false,
                HumanizeTriggerAutomationDirectorUx1AH(result.Diagnostics.FirstOrDefault()?.Code));
        }
        history.Record(result);
        InvalidateContentIndexes();
        return new StudioUx1AHActionResult(
            true,
            "Automatisation et contrôle MJ appliqués · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelTriggerAutomationDirectorUx1AH()
    {
        _approvedTriggerAutomationDirectorUx1AH = null;
        _approvedTriggerAutomationDirectorRevisionUx1AH = null;
    }

    private bool TryBuildTriggerAutomationDirectorUx1AH(
        StudioTriggerAutomationDirectorDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out TriggerWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
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
        var current = catalog.Triggers.SingleOrDefault(item =>
            item.Id == draft.TriggerProfileId &&
            item.BlueprintId == draft.BlueprintId &&
            item.AreaId == draft.AreaId);
        if (current is null || current.Id != draft.OriginalProfile.Id)
        {
            message = "Le déclencheur a changé depuis son ouverture. Rechargez sa fiche.";
            return false;
        }
        if (current.Foundation is null)
        {
            message =
                "Configurez d’abord le volume de base avec l’outil Déclencheur avant l’automatisation et le contrôle MJ.";
            return false;
        }
        if (!ValidateTriggerAutomationDirectorDraftUx1AH(draft, out message))
        {
            return false;
        }

        var scheduler = draft.SchedulerEnabled
            ? new TriggerWorldProfile.SchedulerPolicy(
                draft.IntervalSeconds,
                draft.BudgetPerMinute,
                draft.WhileOccupiedOnly,
                draft.AggregateOccupants)
            : null;
        var director = draft.DirectorEnabled
            ? new TriggerWorldProfile.DirectorPolicy(
                draft.AllowManualEnter,
                draft.AllowManualExit,
                draft.AllowManualFire,
                draft.RequireTarget,
                draft.Audit)
            : null;
        var built = current with
        {
            Scheduler = scheduler,
            CustomEventNamespace = draft.CustomEventNamespace.Trim(),
            Director = director,
        };
        var candidateCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.Placeables,
            catalog.Doors,
            catalog.Triggers.Select(item => item.Id == built.Id ? built : item).ToArray(),
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            candidateCatalog,
            dryRun);
        profile = built;
        return true;
    }

    private static bool ValidateTriggerAutomationDirectorDraftUx1AH(
        StudioTriggerAutomationDirectorDraft draft,
        out string message)
    {
        message = string.Empty;
        if (draft.SchedulerEnabled)
        {
            if (!double.IsFinite(draft.IntervalSeconds) || draft.IntervalSeconds is <= 0 or > 86_400)
            {
                message = "La cadence Heartbeat doit être positive et rester au plus à 86 400 secondes.";
                return false;
            }
            if (draft.BudgetPerMinute is < 1 or > 100_000)
            {
                message = "Le budget Heartbeat doit rester compris entre 1 et 100 000 appels par minute.";
                return false;
            }
        }
        if (!IsNamespacedTriggerEventUx1AH(draft.CustomEventNamespace))
        {
            message =
                "L’événement personnalisé doit utiliser au moins deux mots minuscules séparés par un point, par exemple trigger.sanctuaire.";
            return false;
        }
        if (draft.DirectorEnabled)
        {
            if (!draft.AllowManualEnter && !draft.AllowManualExit && !draft.AllowManualFire)
            {
                message = "Autorisez au moins une action manuelle : entrée, sortie ou déclenchement.";
                return false;
            }
            if (!draft.RequireTarget)
            {
                message = "Le contrôle MJ exige une cible explicite ; aucune action globale implicite n’est autorisée.";
                return false;
            }
            if (!draft.Audit)
            {
                message = "Le contrôle MJ doit rester audité par l’autorité serveur.";
                return false;
            }
        }
        return true;
    }

    private static (
        int Perception,
        IReadOnlySet<string> Credentials,
        IReadOnlySet<string> Permissions) TriggerEvaluationInputsUx1AH(TriggerWorldProfile profile)
    {
        var perception = Math.Min(1_000_001, (profile.Discovery?.Difficulty ?? 0) + 1);
        IReadOnlySet<string> credentials = profile.Access is { } access
            ? new HashSet<string>([access.CredentialId], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        IReadOnlySet<string> permissions = profile.Interaction is { } interaction
            ? new HashSet<string>([interaction.Permission], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        return (perception, credentials, permissions);
    }

    private static bool IsNamespacedTriggerEventUx1AH(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 3 or > 128)
        {
            return false;
        }
        var segments = normalized.Split('.', StringSplitOptions.None);
        return segments.Length >= 2 && segments.All(segment =>
            segment.Length > 0 &&
            char.IsAsciiLetterLower(segment[0]) &&
            segment.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) ||
                                     character == '-'));
    }

    private static string DefaultTriggerNamespaceUx1AH(string? slug)
    {
        var source = string.IsNullOrWhiteSpace(slug) ? "event" : slug.Trim().ToLowerInvariant();
        var normalized = new string(source.Select(character =>
                char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-'
                    ? character
                    : '-')
            .ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(normalized) || !char.IsAsciiLetterLower(normalized[0]))
        {
            normalized = "event";
        }
        return $"trigger.{normalized}";
    }

    private static string HumanizeNamespaceUx1AH(string value) =>
        string.Join(" › ", value.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => string.Join(' ', segment.Split('-', StringSplitOptions.RemoveEmptyEntries))));

    private static string HumanizeDirectorUx1AH(TriggerWorldProfile.DirectorPolicy? director)
    {
        if (director is null)
        {
            return "désactivé · tout déclenchement manuel est refusé";
        }
        var allowed = new List<string>();
        if (director.AllowManualEnter) allowed.Add("entrée");
        if (director.AllowManualExit) allowed.Add("sortie");
        if (director.AllowManualFire) allowed.Add("déclenchement");
        return $"{string.Join(", ", allowed)} · cible explicite · audit serveur obligatoire";
    }

    private static string HumanizeTriggerAutomationDirectorUx1AH(string? code) => code switch
    {
        "world.trigger.scheduler_invalid" =>
            "Le Heartbeat exige une cadence positive et un budget d’au moins un appel par minute.",
        "world.trigger.foundation_required_missing" =>
            "Configurez d’abord le volume de base avec l’outil Déclencheur.",
        _ => "L’automatisation ne peut pas être prévisualisée. Vérifiez les champs nommés.",
    };

    private static StudioTriggerAutomationDirectorPreview FailedTriggerAutomationDirectorPreviewUx1AH(
        string message) => new(
        false,
        message,
        string.Empty,
        false,
        false,
        false,
        false,
        false,
        false,
        LiveDispatchAttempted: false,
        CanApply: false);

    private static Guid StableTriggerIdUx1AH(Guid seed, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux1-ah:{seed:N}:{segment}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }

    private static string UniqueBlueprintSlugUx1AH(
        ProjectDefinition project,
        string baseSlug,
        params string[] reserved)
    {
        var occupied = project.Blueprints.Select(item => item.Slug)
            .Concat(reserved)
            .ToHashSet(StringComparer.Ordinal);
        var slug = baseSlug;
        var suffix = 1;
        while (occupied.Contains(slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }
        return slug;
    }
}

internal sealed partial class StudioSessionController
{
    internal StudioUx1AHSmokeResult RunUx1AHContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ah-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var targetBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("a8100000-0000-4000-8000-000000000001")),
                "ronde-du-sanctuaire",
                "Ronde du sanctuaire",
                BlueprintKind.Trigger);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("a8100000-0000-4000-8000-000000000002")),
                "seuil-temoin",
                "Seuil témoin",
                BlueprintKind.Trigger);
            var foundationlessBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("a8100000-0000-4000-8000-000000000003")),
                "ronde-inachevee",
                "Ronde inachevée",
                BlueprintKind.Trigger);
            var itemBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("a8100000-0000-4000-8000-000000000004")),
                "sceau-temoin",
                "Sceau témoin",
                BlueprintKind.Item);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("a8100000-0000-4000-8000-000000000005")),
                "sanctuaire-des-rondes",
                "Sanctuaire des rondes",
                [],
                AreaKind.Interior,
                new AreaDimensions(48, 48));
            var projectId = ProjectId.From(Guid.Parse("a8100000-0000-4000-8000-000000000006"));
            var dialogueId = DialogueId.From(Guid.Parse("a8100000-0000-4000-8000-000000000007"));
            var nodeId = DialogueNodeId.From(Guid.Parse("a8100000-0000-4000-8000-000000000008"));
            var dialogue = new DialogueDefinition(
                dialogueId,
                "annonce-de-ronde",
                "Annonce de ronde",
                nodeId,
                [new(nodeId, null, new("dialogue.ux1ah.smoke", "La ronde est auditée."))],
                provenance: "test:ux1-ah",
                entryPoints:
                [new(Guid.Parse("a8100000-0000-4000-8000-000000000009"), 1, "default", nodeId, 0,
                    "test:ux1-ah")]);
            var shell = new ProjectDefinition(
                projectId,
                "Studio UX1-AH",
                [targetBlueprint, neighborBlueprint, foundationlessBlueprint, itemBlueprint],
                [area]);
            var targetProfileId = StableTriggerIdUx1AH(projectId.Value, "smoke-target-profile");
            var neighborProfileId = StableTriggerIdUx1AH(projectId.Value, "smoke-neighbor-profile");
            var foundationlessProfileId = StableTriggerIdUx1AH(projectId.Value, "smoke-foundationless-profile");
            var targetProfile = CreateConfiguredTriggerUx1AE(
                targetProfileId,
                targetBlueprint.Id,
                area.Id,
                neighborProfileId,
                itemBlueprint.Id,
                dialogueId.Value,
                "ah-target") with
            {
                Provenance = "test:ux1-ah",
                Scheduler = null,
                CustomEventNamespace = "trigger.before",
                Director = null,
            };
            var neighborProfile = CreateConfiguredTriggerUx1AE(
                neighborProfileId,
                neighborBlueprint.Id,
                area.Id,
                targetProfileId,
                itemBlueprint.Id,
                dialogueId.Value,
                "ah-neighbor");
            var foundationlessProfile = CreateConfiguredTriggerUx1AE(
                foundationlessProfileId,
                foundationlessBlueprint.Id,
                area.Id,
                targetProfileId,
                itemBlueprint.Id,
                dialogueId.Value,
                "ah-foundationless",
                discoveryAccess: false,
                foundation: false);
            var catalog = new WorldFeatureCatalog(
                shell.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ah",
                triggers: [targetProfile, neighborProfile, foundationlessProfile]);
            var project = new ProjectDefinition(
                shell.Id,
                shell.Title,
                shell.Module.WithWorldFeatures(catalog),
                shell.Blueprints,
                shell.Areas,
                [dialogue],
                shell.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1AHSmoke();
            }

            var initialProject = editing.Project!;
            var initialCatalog = initialProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var loadedNeighbor = initialCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            var selectionExplicit = editing.CreateTriggerAutomationDirectorDraftUx1AH() is null &&
                                    editing.TriggerAutomationDirectorChoicesUx1AH.Count == 3;
            var foundationRequired =
                editing.CreateTriggerAutomationDirectorDraftUx1AH(foundationlessProfile.Id) is null &&
                editing.TriggerAutomationDirectorChoicesUx1AH.Single(item =>
                    item.ProfileId == foundationlessProfile.Id) is { HasFoundation: false };

            var staleDraft = editing.CreateTriggerAutomationDirectorDraftUx1AH(targetProfile.Id)!;
            var firstIntermediate = ApplyUx1AHIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-before-preview");
            var staleDraftPreview = editing.PreviewTriggerAutomationDirectorUx1AH(staleDraft);
            var staleDraftBlocked = firstIntermediate && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé", StringComparison.Ordinal) &&
                                    editing.Undo().Succeeded;

            var draft = CustomizeUx1AHSmokeDraft(
                editing.CreateTriggerAutomationDirectorDraftUx1AH(targetProfile.Id)!);
            var invalidInterval = editing.PreviewTriggerAutomationDirectorUx1AH(draft with
            {
                IntervalSeconds = 0,
            });
            var invalidBudget = editing.PreviewTriggerAutomationDirectorUx1AH(draft with
            {
                BudgetPerMinute = 0,
            });
            var invalidNamespace = editing.PreviewTriggerAutomationDirectorUx1AH(draft with
            {
                CustomEventNamespace = "sans-espace-nomme",
            });
            var noDirectorAction = editing.PreviewTriggerAutomationDirectorUx1AH(draft with
            {
                AllowManualEnter = false,
                AllowManualExit = false,
                AllowManualFire = false,
            });
            var noDirectorTarget = editing.PreviewTriggerAutomationDirectorUx1AH(draft with
            {
                RequireTarget = false,
            });
            var noDirectorAudit = editing.PreviewTriggerAutomationDirectorUx1AH(draft with
            {
                Audit = false,
            });
            var beforePreviewProject = editing.Project;
            var beforePreviewRevision = editing.History!.State.Revision;
            var beforePreviewJournal = editing.History.Journal.ToArray();
            var beforePreviewDirty = editing.IsDirty;
            var beforePreviewCanUndo = editing.History.CanUndo;
            var beforePreviewCanRedo = editing.History.CanRedo;
            var preview = editing.PreviewTriggerAutomationDirectorUx1AH(draft);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(beforePreviewProject, editing.Project) &&
                         editing.History.State.Revision == beforePreviewRevision &&
                         editing.History.Journal.SequenceEqual(beforePreviewJournal) &&
                         editing.IsDirty == beforePreviewDirty &&
                         editing.History.CanUndo == beforePreviewCanUndo &&
                         editing.History.CanRedo == beforePreviewCanRedo;

            var secondIntermediate = ApplyUx1AHIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-after-preview");
            var staleApply = editing.ApplyApprovedTriggerAutomationDirectorUx1AH();
            var staleApprovalBlocked = secondIntermediate && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var freshDraft = CustomizeUx1AHSmokeDraft(
                editing.CreateTriggerAutomationDirectorDraftUx1AH(targetProfile.Id)!);
            var freshPreview = editing.PreviewTriggerAutomationDirectorUx1AH(freshDraft);
            var apply = editing.ApplyApprovedTriggerAutomationDirectorUx1AH();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var appliedNeighbor = appliedCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            if (!freshPreview.Succeeded || !apply.Succeeded || appliedTarget.Scheduler is null ||
                appliedTarget.Director is null)
            {
                return FailedUx1AHSmoke();
            }

            var scheduler = appliedTarget.Scheduler.IntervalSeconds == 1.75 &&
                            appliedTarget.Scheduler.BudgetPerMinute == 17 &&
                            appliedTarget.Scheduler.WhileOccupiedOnly &&
                            appliedTarget.Scheduler.AggregateOccupants;
            var customEvent = appliedTarget.CustomEventNamespace == "trigger.sanctuary.round";
            var director = appliedTarget.Director.AllowManualEnter &&
                           !appliedTarget.Director.AllowManualExit &&
                           appliedTarget.Director.AllowManualFire &&
                           appliedTarget.Director.RequireTarget &&
                           appliedTarget.Director.Audit;
            var runtimePolicy = freshPreview.OccupancyPolicyHonored && freshPreview.BudgetBlocked &&
                                freshPreview.ScheduledAllowed && freshPreview.NamespaceValid &&
                                freshPreview.ManualFirePolicyHonored && freshPreview.AuditRecorded;
            var noLiveDispatch = !freshPreview.LiveDispatchAttempted &&
                                 freshPreview.Summary.Contains("aucune commande Live Director", StringComparison.Ordinal);
            var discoveryPreserved = ReferenceEquals(appliedTarget.Discovery, loadedTarget.Discovery);
            var interactionPreserved = ReferenceEquals(appliedTarget.Interaction, loadedTarget.Interaction);
            var accessPreserved = ReferenceEquals(appliedTarget.Access, loadedTarget.Access);
            var foundationPreserved = ReferenceEquals(appliedTarget.Foundation, loadedTarget.Foundation);
            var portalPreserved = ReferenceEquals(
                appliedTarget.Foundation!.Portal,
                loadedTarget.Foundation!.Portal);
            var validationPreserved = ReferenceEquals(
                appliedTarget.Foundation.Validation,
                loadedTarget.Foundation.Validation);
            var advancedPreserved = ReferenceEquals(appliedTarget.Advanced, loadedTarget.Advanced) &&
                                    appliedTarget.AdvancedRequiredAtRuntime ==
                                    loadedTarget.AdvancedRequiredAtRuntime;
            var ux2NeighborsPreserved = ReferenceEquals(appliedTarget.Rest, loadedTarget.Rest) &&
                                        ReferenceEquals(appliedTarget.Clue, loadedTarget.Clue) &&
                                        ReferenceEquals(appliedTarget.Narrative, loadedTarget.Narrative);
            var otherTriggersPreserved = initialCatalog.Triggers.Count == appliedCatalog.Triggers.Count &&
                                         initialCatalog.Triggers
                                             .Where(item => item.Id != targetProfile.Id)
                                             .Zip(appliedCatalog.Triggers.Where(item => item.Id != targetProfile.Id))
                                             .All(pair => ReferenceEquals(pair.First, pair.Second));
            var neighborPreservedInMemory = ReferenceEquals(appliedNeighbor, loadedNeighbor) &&
                                            otherTriggersPreserved &&
                                            SameReferencesUx1AH(
                                                appliedCatalog.Placeables,
                                                initialCatalog.Placeables) &&
                                            SameReferencesUx1AH(appliedCatalog.Doors, initialCatalog.Doors) &&
                                            SameReferencesUx1AH(
                                                appliedCatalog.Encounters,
                                                initialCatalog.Encounters) &&
                                            SameReferencesUx1AH(
                                                appliedCatalog.VehicleRoutes,
                                                initialCatalog.VehicleRoutes) &&
                                            SameReferencesUx1AH(
                                                appliedCatalog.Merchants,
                                                initialCatalog.Merchants) &&
                                            SameReferencesUx1AH(
                                                appliedCatalog.Companions,
                                                initialCatalog.Companions) &&
                                            ReferenceEquals(appliedCatalog.Scripts, initialCatalog.Scripts);

            var journal = editing.History.Journal;
            var canonicalCommand = journal.Count == 3 &&
                                   journal.All(item => item.CommandName == "set_world_feature_catalog") &&
                                   journal.Count(item => item.IsApplied) == 1;
            var undo = editing.Undo();
            var removed = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id);
            var redo = editing.Redo();
            var restored = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id);
            var undoRedo = undo.Succeeded && removed.Scheduler is null && removed.Director is null &&
                           removed.CustomEventNamespace == "trigger.before" && redo.Succeeded &&
                           SameTriggerAutomationUx1AH(appliedTarget, restored);

            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistedCatalog = opened ? reopened.Project!.Module.WorldFeatures : null;
            var persistedTarget = persistedCatalog?.Triggers.SingleOrDefault(item => item.Id == targetProfile.Id);
            var allTriggersPersisted = persistedCatalog is { } roundTripCatalog &&
                                       roundTripCatalog.Triggers.Count == appliedCatalog.Triggers.Count &&
                                       appliedCatalog.Triggers.All(expected =>
                                           roundTripCatalog.Triggers.Any(actual =>
                                               actual.Id == expected.Id && SameFullTriggerUx1AH(expected, actual)));
            var persistence = persistedTarget is not null &&
                              SameFullTriggerUx1AH(appliedTarget, persistedTarget) &&
                              allTriggersPersisted;
            var neighborPreserved = neighborPreservedInMemory &&
                                    persistedCatalog is { } neighborRoundTripCatalog &&
                                    appliedCatalog.Triggers
                                        .Where(item => item.Id != targetProfile.Id)
                                        .All(expected => neighborRoundTripCatalog.Triggers.Any(actual =>
                                            actual.Id == expected.Id && SameFullTriggerUx1AH(expected, actual)));
            var invalidIntervalBlocked = !invalidInterval.Succeeded;
            var invalidBudgetBlocked = !invalidBudget.Succeeded;
            var invalidNamespaceBlocked = !invalidNamespace.Succeeded;
            var unsafeDirectorBlocked = !noDirectorAction.Succeeded && !noDirectorTarget.Succeeded &&
                                        !noDirectorAudit.Succeeded;
            var humanDiagnostics = IsHumanUx1AHMessage(invalidInterval.Message) &&
                                   IsHumanUx1AHMessage(invalidBudget.Message) &&
                                   IsHumanUx1AHMessage(invalidNamespace.Message) &&
                                   IsHumanUx1AHMessage(noDirectorAction.Message) &&
                                   IsHumanUx1AHMessage(noDirectorTarget.Message) &&
                                   IsHumanUx1AHMessage(noDirectorAudit.Message) &&
                                   IsHumanUx1AHMessage(preview.Message);
            var succeeded = selectionExplicit && foundationRequired && scheduler && customEvent && director &&
                            runtimePolicy && noLiveDispatch && dryRun && invalidIntervalBlocked &&
                            invalidBudgetBlocked && invalidNamespaceBlocked && unsafeDirectorBlocked &&
                            staleDraftBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && discoveryPreserved && interactionPreserved && accessPreserved &&
                            foundationPreserved && portalPreserved && validationPreserved && advancedPreserved &&
                            ux2NeighborsPreserved && neighborPreserved && humanDiagnostics;
            return new StudioUx1AHSmokeResult(
                succeeded,
                selectionExplicit,
                foundationRequired,
                scheduler,
                customEvent,
                director,
                runtimePolicy,
                noLiveDispatch,
                dryRun,
                invalidIntervalBlocked,
                invalidBudgetBlocked,
                invalidNamespaceBlocked,
                unsafeDirectorBlocked,
                staleDraftBlocked,
                staleApprovalBlocked,
                canonicalCommand,
                undoRedo,
                persistence,
                discoveryPreserved,
                interactionPreserved,
                accessPreserved,
                foundationPreserved,
                portalPreserved,
                validationPreserved,
                advancedPreserved,
                ux2NeighborsPreserved,
                neighborPreserved,
                humanDiagnostics);
        }
        catch
        {
            return FailedUx1AHSmoke();
        }
        finally
        {
            reopened.Close();
            editing.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static StudioTriggerAutomationDirectorDraft CustomizeUx1AHSmokeDraft(
        StudioTriggerAutomationDirectorDraft draft) => draft with
    {
        SchedulerEnabled = true,
        IntervalSeconds = 1.75,
        BudgetPerMinute = 17,
        WhileOccupiedOnly = true,
        AggregateOccupants = true,
        CustomEventNamespace = "trigger.sanctuary.round",
        DirectorEnabled = true,
        AllowManualEnter = true,
        AllowManualExit = false,
        AllowManualFire = true,
        RequireTarget = true,
        Audit = true,
    };

    private static bool ApplyUx1AHIntermediateMutation(
        StudioSessionController editing,
        Guid profileId,
        string customEventNamespace)
    {
        var history = editing.History;
        var project = editing.Project;
        if (history is null || project is null)
        {
            return false;
        }
        var catalog = project.Module.WorldFeatures;
        var profile = catalog.Triggers.SingleOrDefault(item => item.Id == profileId);
        if (profile is null)
        {
            return false;
        }
        var changed = profile with { CustomEventNamespace = customEventNamespace };
        var changedCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.Placeables,
            catalog.Doors,
            catalog.Triggers.Select(item => item.Id == changed.Id ? changed : item).ToArray(),
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        var command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            changedCatalog,
            DryRun: false);
        var result = editing._setTriggerAutomationDirectorCatalogUx1AH.Handle(history.State, command);
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    private static bool SameTriggerAutomationUx1AH(TriggerWorldProfile expected, TriggerWorldProfile actual) =>
        expected.Scheduler == actual.Scheduler &&
        expected.CustomEventNamespace == actual.CustomEventNamespace &&
        expected.Director == actual.Director;

    private static bool SameFullTriggerUx1AH(TriggerWorldProfile expected, TriggerWorldProfile actual) =>
        SameDiscoveryUx1AE(expected.Discovery, actual.Discovery) &&
        SameInteractionUx1AE(expected.Interaction, actual.Interaction) &&
        SameAccessUx1AE(expected.Access, actual.Access) &&
        (expected.Foundation is null && actual.Foundation is null ||
         expected.Foundation is { } expectedFoundation && actual.Foundation is { } actualFoundation &&
         SameFoundationUx1AE(expectedFoundation, actualFoundation)) &&
        (expected.Advanced is null && actual.Advanced is null ||
         expected.Advanced is { } expectedAdvanced && actual.Advanced is { } actualAdvanced &&
         SameAdvancedUx1AE(expectedAdvanced, actualAdvanced)) &&
        SameUx1AENeighbors(expected, actual);

    private static bool SameReferencesUx1AH<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class =>
        left.Count == right.Count && left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx1AHMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains('{') &&
        !message.Contains("Guid", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !System.Text.RegularExpressions.Regex.IsMatch(
            message,
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    private static StudioUx1AHSmokeResult FailedUx1AHSmoke() => new(
        Succeeded: false,
        SelectionExplicit: false,
        FoundationRequired: false,
        Scheduler: false,
        CustomEvent: false,
        Director: false,
        RuntimePolicy: false,
        NoLiveDispatch: false,
        DryRun: false,
        InvalidIntervalBlocked: false,
        InvalidBudgetBlocked: false,
        InvalidNamespaceBlocked: false,
        UnsafeDirectorBlocked: false,
        StaleDraftBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        DiscoveryPreserved: false,
        InteractionPreserved: false,
        AccessPreserved: false,
        FoundationPreserved: false,
        PortalPreserved: false,
        ValidationPreserved: false,
        AdvancedPreserved: false,
        Ux2NeighborsPreserved: false,
        NeighborPreserved: false,
        HumanDiagnostics: false);
}
