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
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1AEProvenance = "studio:ux1-ae";
    private readonly SetWorldFeatureCatalogHandler _setTriggerDiscoveryAccessCatalogUx1AE =
        new(new SystemClock());
    private StudioTriggerDiscoveryAccessDraft? _approvedTriggerDiscoveryAccessUx1AE;
    private ProjectRevision? _approvedTriggerDiscoveryAccessRevisionUx1AE;

    internal IReadOnlyList<StudioTriggerDiscoveryAccessChoice> TriggerDiscoveryAccessChoicesUx1AE
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
                        : item.Discovery is null && item.Interaction is null && item.Access is null
                            ? "comportements à configurer"
                            : "comportements configurés";
                    return new StudioTriggerDiscoveryAccessChoice(
                        item.Id,
                        item.BlueprintId,
                        $"{name} · {instances} instance(s) · {state}",
                        item.Foundation is not null,
                        item.Discovery is not null,
                        item.Interaction is not null,
                        item.Access is not null,
                        string.Equals(item.Provenance, Ux1AEProvenance, StringComparison.Ordinal));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioTriggerRevealTargetChoice> TriggerRevealTargetChoicesUx1AE(Guid profileId)
    {
        var project = Project;
        var profile = project?.Module.WorldFeatures.Triggers.SingleOrDefault(item => item.Id == profileId);
        var area = profile is null ? null : project!.Areas.SingleOrDefault(item => item.Id == profile.AreaId);
        if (project is null || profile is null || area is null)
        {
            return [];
        }

        var choices = new List<StudioTriggerRevealTargetChoice>();
        foreach (var pair in area.Entities.Select((entity, index) => (entity, index)))
        {
            var blueprint = project.Blueprints.FirstOrDefault(item => item.Id == pair.entity.BlueprintId);
            var name = blueprint?.DisplayName ?? "Objet de zone";
            choices.Add(new StudioTriggerRevealTargetChoice(
                pair.entity.Id.Value,
                $"{name} · objet {pair.index + 1}",
                true));
        }

        foreach (var trigger in project.Module.WorldFeatures.Triggers.Where(item =>
                     item.AreaId == profile.AreaId && item.Id != profile.Id))
        {
            var blueprint = project.Blueprints.FirstOrDefault(item => item.Id == trigger.BlueprintId);
            choices.Add(new StudioTriggerRevealTargetChoice(
                trigger.Id,
                $"Volume · {blueprint?.DisplayName ?? "déclencheur de la zone"}",
                true));
        }

        var currentTarget = profile.Discovery?.RevealTargetId;
        if (currentTarget is { } unresolved && unresolved != Guid.Empty &&
            choices.All(item => item.TargetId != unresolved))
        {
            choices.Add(new StudioTriggerRevealTargetChoice(
                unresolved,
                "Cible existante indisponible · conservée tant qu’elle n’est pas remplacée",
                false));
        }

        return choices
            .GroupBy(item => item.TargetId)
            .Select(group => group.First())
            .OrderByDescending(item => item.Resolved)
            .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioTriggerScriptChoice> TriggerScriptChoicesUx1AE(Guid profileId)
    {
        var profile = Project?.Module.WorldFeatures.Triggers.SingleOrDefault(item => item.Id == profileId);
        if (profile is null)
        {
            return [];
        }

        var validation = profile.Foundation?.Validation;
        var validationScripts = validation?.KnownScriptIds.ToHashSet(StringComparer.Ordinal);
        var currentScript = string.IsNullOrWhiteSpace(profile.Interaction?.ScriptId)
            ? Array.Empty<(string RelativePath, string DisplayName)>()
            : new[]
            {
                (RelativePath: profile.Interaction!.ScriptId,
                    DisplayName: HumanScriptName(profile.Interaction.ScriptId)),
            };
        var values = FindForgeScripts()
            .Select(item => (item.RelativePath, item.DisplayName))
            .Concat((validation?.KnownScriptIds ?? []).Select(item => (item, HumanScriptName(item))))
            .Concat(currentScript)
            .GroupBy(item => item.Item1, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(item => new StudioTriggerScriptChoice(
                item.Item1,
                item.Item2,
                validationScripts is not null && validationScripts.Contains(item.Item1)))
            .OrderByDescending(item => item.AllowedByValidation)
            .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return values;
    }

    internal IReadOnlyList<StudioTriggerCredentialChoice> TriggerCredentialChoicesUx1AE(Guid profileId)
    {
        var project = Project;
        var profile = project?.Module.WorldFeatures.Triggers.SingleOrDefault(item => item.Id == profileId);
        if (project is null || profile is null)
        {
            return [];
        }

        var values = project.Blueprints
            .Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioTriggerCredentialChoice(
                ItemCredentialUx1AE(item.Id),
                $"Objet clé · {item.DisplayName}"))
            .ToList();
        var existing = profile.Access?.CredentialId;
        if (!string.IsNullOrWhiteSpace(existing) && values.All(item => item.CredentialId != existing))
        {
            var label = TryParseItemCredentialUx1AE(existing) is not null
                ? "Objet clé indisponible · conservé tant qu’il n’est pas remplacé"
                : $"Justificatif existant · {HumanizeCredentialUx1AE(existing)}";
            values.Add(new StudioTriggerCredentialChoice(existing, label));
        }
        return values;
    }

    internal ProjectDefinition? CreateUx1AEEvidenceProject()
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
                .Select(index => StableTriggerIdUx1AE(
                    project.Id.Value,
                    $"evidence-{area.Id.Value:N}-{salt}-{index}"))
                .ToArray();
            advancedIds = TriggerAdvancedComponentIdsUx1AE(ids[2]);
            salt++;
        } while (ids.Concat(advancedIds).Distinct().Count() != ids.Length + advancedIds.Length ||
                 ids.Concat(advancedIds).Any(occupied.Contains));

        var triggerSlug = UniqueBlueprintSlugUx1AE(project, "ux1-ae-secret-trigger");
        var targetSlug = UniqueBlueprintSlugUx1AE(project, "ux1-ae-reveal-target", triggerSlug);
        var itemSlug = UniqueBlueprintSlugUx1AE(project, "ux1-ae-key", triggerSlug, targetSlug);
        var triggerBlueprint = new BlueprintDefinition(
            BlueprintId.From(ids[0]),
            triggerSlug,
            "Secret du sanctuaire",
            BlueprintKind.Trigger);
        var targetBlueprint = new BlueprintDefinition(
            BlueprintId.From(ids[8]),
            targetSlug,
            "Reliquaire à révéler",
            BlueprintKind.Trigger);
        var itemBlueprint = new BlueprintDefinition(
            BlueprintId.From(ids[1]),
            itemSlug,
            "Sceau du sanctuaire",
            BlueprintKind.Item);
        var dialogueId = DialogueId.From(ids[10]);
        var nodeId = DialogueNodeId.From(ids[11]);
        var dialogue = new DialogueDefinition(
            dialogueId,
            "ux1-ae-evidence-dialogue",
            "Murmure du sanctuaire",
            nodeId,
            [new(nodeId, null, new("dialogue.ux1ae", "Le passage secret répond."))],
            provenance: Ux1AEProvenance,
            entryPoints:
            [new(ids[12], WorldFeatureCatalog.CurrentSchemaVersion, "default", nodeId, 0, Ux1AEProvenance)]);
        var neighbor = new TriggerWorldProfile(
            ids[9],
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1AEProvenance,
            targetBlueprint.Id,
            area.Id,
            null,
            null,
            null,
            "trigger.ux1-ae-reveal-target",
            null,
            null,
            null,
            null,
            null);
        var profile = CreateConfiguredTriggerUx1AE(
            ids[2],
            triggerBlueprint.Id,
            area.Id,
            neighbor.Id,
            itemBlueprint.Id,
            dialogueId.Value,
            "evidence");
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
            project.Blueprints.Append(triggerBlueprint).Append(targetBlueprint).Append(itemBlueprint).ToArray(),
            project.Areas,
            project.Dialogues.Append(dialogue).ToArray(),
            project.Quests);
    }

    internal StudioTriggerDiscoveryAccessDraft? CreateTriggerDiscoveryAccessDraftUx1AE(
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

        var target = TriggerRevealTargetChoicesUx1AE(profile.Id).FirstOrDefault(item => item.Resolved)?.TargetId ??
                     Guid.Empty;
        var script = TriggerScriptChoicesUx1AE(profile.Id).FirstOrDefault(item => item.AllowedByValidation)?.ScriptId ??
                     string.Empty;
        var credential = TriggerCredentialChoicesUx1AE(profile.Id).FirstOrDefault()?.CredentialId ?? string.Empty;
        return new StudioTriggerDiscoveryAccessDraft(
            profile.Id,
            profile.BlueprintId,
            profile.AreaId,
            history.State.Revision,
            profile.Discovery is not null,
            profile.Discovery?.Senses.ToArray() ?? ["sight", "search"],
            profile.Discovery?.Difficulty ?? 10,
            profile.Discovery?.RevealTargetId ?? target,
            profile.Discovery?.FeedbackId ?? "trigger.secret.revealed",
            profile.Discovery?.Permanent ?? true,
            profile.Discovery?.Scope ?? TriggerWorldProfile.DiscoveryScope.Party,
            profile.Interaction is not null,
            profile.Interaction?.Verb ?? "inspect",
            profile.Interaction?.Icon ?? "cursor.inspect",
            profile.Interaction?.Outline ?? "outline.interactive",
            profile.Interaction?.PromptKey ?? "trigger.inspect",
            profile.Interaction?.ScriptId ?? script,
            profile.Interaction?.Visible ?? true,
            profile.Interaction?.Permission ?? "trigger.use",
            profile.Access is not null,
            profile.Access?.CredentialId ?? credential,
            profile.Access?.Consume ?? false,
            profile.Access?.DeniedFeedbackKey ?? "trigger.access.denied",
            profile);
    }

    internal StudioTriggerDiscoveryAccessPreview PreviewTriggerDiscoveryAccessUx1AE(
        StudioTriggerDiscoveryAccessDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTriggerDiscoveryAccessUx1AE();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedTriggerDiscoveryAccessPreviewUx1AE("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedTriggerDiscoveryAccessPreviewUx1AE(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le déclencheur avant de prévisualiser.");
        }
        if (!TryBuildTriggerDiscoveryAccessUx1AE(draft, true, out var command, out var profile, out var message))
        {
            return FailedTriggerDiscoveryAccessPreviewUx1AE(message);
        }

        var result = _setTriggerDiscoveryAccessCatalogUx1AE.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            return FailedTriggerDiscoveryAccessPreviewUx1AE(
                HumanizeTriggerDiscoveryAccessUx1AE(result.Diagnostics.FirstOrDefault()?.Code));
        }

        var configured = profile!;
        var highPerception = Math.Min(1_000_001, (configured.Discovery?.Difficulty ?? 0) + 1);
        var allowedCredentials = configured.Access is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>([configured.Access.CredentialId], StringComparer.Ordinal);
        var allowedPermissions = configured.Interaction is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>([configured.Interaction.Permission], StringComparer.Ordinal);
        var hidden = configured.Discovery is null
            ? null
            : WorldFeatureRuntimeResolver.EvaluateTrigger(
                configured,
                configured.Discovery.Difficulty - 1,
                allowedCredentials,
                allowedPermissions,
                false,
                1,
                0,
                false);
        var deniedCredential = configured.Access is null
            ? null
            : WorldFeatureRuntimeResolver.EvaluateTrigger(
                configured,
                highPerception,
                new HashSet<string>(StringComparer.Ordinal),
                allowedPermissions,
                false,
                1,
                0,
                false);
        var deniedPermission = configured.Interaction is null
            ? null
            : WorldFeatureRuntimeResolver.EvaluateTrigger(
                configured,
                highPerception,
                allowedCredentials,
                new HashSet<string>(StringComparer.Ordinal),
                false,
                1,
                0,
                false);
        var allowed = WorldFeatureRuntimeResolver.EvaluateTrigger(
            configured,
            highPerception,
            allowedCredentials,
            allowedPermissions,
            false,
            1,
            0,
            false);
        var hiddenBlocked = configured.Discovery is null || hidden is { Fired: false, Revealed: false, Reason: "hidden" };
        var credentialBlocked = configured.Access is null ||
                                deniedCredential is { Fired: false, Reason: "credential-missing" };
        var permissionBlocked = configured.Interaction is null ||
                                deniedPermission is { Fired: false, Reason: "permission-denied" };
        if (!hiddenBlocked || !credentialBlocked || !permissionBlocked || !allowed.Fired)
        {
            return FailedTriggerDiscoveryAccessPreviewUx1AE(
                "La politique ne produit pas les états de découverte et d’accès attendus.");
        }

        var summary =
            $"Secret : {(configured.Discovery is null ? "désactivé" : $"{configured.Discovery.Senses.Count} sens · difficulté {configured.Discovery.Difficulty} · portée {HumanizeDiscoveryScopeUx1AE(configured.Discovery.Scope)}")}.\n" +
            $"Interaction : {(configured.Interaction is null ? "désactivée" : $"{configured.Interaction.Verb} · présentation {(configured.Interaction.Visible ? "visible" : "masquée")} · permission contrôlée")}.\n" +
            $"Accès : {(configured.Access is null ? "libre" : $"justificatif requis · {(configured.Access.Consume ? "consommation explicitement demandée" : "justificatif conservé")}")}.\n" +
            "Simulation locale : caché, justificatif manquant, permission manquante et autorisé · aucun document modifié.\n" +
            "Fondation, portail, validation, automatisation, narration, profil avancé et déclencheurs voisins préservés.";
        _approvedTriggerDiscoveryAccessUx1AE = draft;
        _approvedTriggerDiscoveryAccessRevisionUx1AE = history.State.Revision;
        return new StudioTriggerDiscoveryAccessPreview(
            true,
            "Aperçu validé · aucun document n’a été modifié.",
            summary,
            hiddenBlocked,
            credentialBlocked,
            permissionBlocked,
            allowed.Fired,
            true);
    }

    internal StudioUx1AEActionResult ApplyApprovedTriggerDiscoveryAccessUx1AE()
    {
        var draft = _approvedTriggerDiscoveryAccessUx1AE;
        var history = History;
        if (draft is null || history is null ||
            draft.BaseRevision != history.State.Revision ||
            _approvedTriggerDiscoveryAccessRevisionUx1AE != history.State.Revision)
        {
            CancelTriggerDiscoveryAccessUx1AE();
            return new StudioUx1AEActionResult(
                false,
                "Prévisualisez de nouveau la découverte et l’accès avant de les appliquer.");
        }
        if (!TryBuildTriggerDiscoveryAccessUx1AE(draft, false, out var command, out _, out var message))
        {
            CancelTriggerDiscoveryAccessUx1AE();
            return new StudioUx1AEActionResult(false, message);
        }

        var result = _setTriggerDiscoveryAccessCatalogUx1AE.Handle(history.State, command!);
        CancelTriggerDiscoveryAccessUx1AE();
        if (!result.Succeeded)
        {
            return new StudioUx1AEActionResult(
                false,
                HumanizeTriggerDiscoveryAccessUx1AE(result.Diagnostics.FirstOrDefault()?.Code));
        }
        history.Record(result);
        InvalidateContentIndexes();
        return new StudioUx1AEActionResult(
            true,
            "Découverte et accès appliqués · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelTriggerDiscoveryAccessUx1AE()
    {
        _approvedTriggerDiscoveryAccessUx1AE = null;
        _approvedTriggerDiscoveryAccessRevisionUx1AE = null;
    }

    private bool TryBuildTriggerDiscoveryAccessUx1AE(
        StudioTriggerDiscoveryAccessDraft draft,
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
                "Configurez d’abord le volume de base avec l’outil Déclencheur avant la découverte et l’accès.";
            return false;
        }
        if (!ValidateTriggerDiscoveryAccessDraftUx1AE(current, draft, out message))
        {
            return false;
        }

        var discovery = draft.DiscoveryEnabled
            ? new TriggerWorldProfile.DiscoveryPolicy(
                draft.Senses.Select(item => item.Trim()).ToArray(),
                draft.Difficulty,
                draft.RevealTargetId,
                draft.FeedbackId.Trim(),
                draft.Permanent,
                draft.Scope)
            : null;
        var interaction = draft.InteractionEnabled
            ? new TriggerWorldProfile.InteractionPolicy(
                draft.Verb.Trim(),
                draft.Icon.Trim(),
                draft.Outline.Trim(),
                draft.PromptKey.Trim(),
                draft.ScriptId.Trim(),
                draft.Visible,
                draft.Permission.Trim())
            : null;
        var access = draft.AccessEnabled
            ? new TriggerWorldProfile.AccessPolicy(
                draft.CredentialId.Trim(),
                draft.ConsumeCredential,
                draft.DeniedFeedbackKey.Trim())
            : null;
        var built = current with
        {
            Discovery = discovery,
            Interaction = interaction,
            Access = access,
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

    private bool ValidateTriggerDiscoveryAccessDraftUx1AE(
        TriggerWorldProfile current,
        StudioTriggerDiscoveryAccessDraft draft,
        out string message)
    {
        message = string.Empty;
        if (draft.DiscoveryEnabled)
        {
            var senses = draft.Senses.Select(item => item.Trim()).ToArray();
            var target = TriggerRevealTargetChoicesUx1AE(current.Id)
                .SingleOrDefault(item => item.TargetId == draft.RevealTargetId);
            if (senses.Length == 0 || senses.Any(string.IsNullOrWhiteSpace) ||
                senses.Distinct(StringComparer.OrdinalIgnoreCase).Count() != senses.Length)
            {
                message = "Ajoutez au moins un sens de découverte, sans doublon ni valeur vide.";
                return false;
            }
            if (draft.Difficulty is < 0 or > 1_000_000)
            {
                message = "La difficulté de découverte doit rester comprise entre 0 et 1 000 000.";
                return false;
            }
            if (draft.RevealTargetId == Guid.Empty || target is not { Resolved: true })
            {
                message = "Choisissez une cible de révélation encore présente dans la zone.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.FeedbackId) || draft.FeedbackId.Trim().Length > 256)
            {
                message = "Indiquez un feedback de révélation court et lisible.";
                return false;
            }
        }

        if (draft.InteractionEnabled)
        {
            var required = new[]
            {
                draft.Verb,
                draft.Icon,
                draft.Outline,
                draft.PromptKey,
                draft.ScriptId,
                draft.Permission,
            };
            if (required.Any(string.IsNullOrWhiteSpace) || required.Any(item => item.Trim().Length > 256))
            {
                message =
                    "Le clic exige un verbe, une icône, une surbrillance, un texte, un script et une permission.";
                return false;
            }
            var validation = current.Foundation?.Validation;
            if (validation is null)
            {
                message =
                    "Le script OnClick exige une liste Validation sur le volume de base. Configurez d’abord Validation de trigger.";
                return false;
            }
            if (!validation.KnownScriptIds.Contains(draft.ScriptId.Trim(), StringComparer.Ordinal))
            {
                message =
                    "Le script OnClick n’est pas déclaré dans la validation du déclencheur. Choisissez un script autorisé ou ouvrez Validation de trigger.";
                return false;
            }
        }

        if (draft.AccessEnabled)
        {
            var credential = draft.CredentialId.Trim();
            if (string.IsNullOrWhiteSpace(credential) || credential.Length > 256)
            {
                message = "Choisissez un objet clé ou indiquez un justificatif métier.";
                return false;
            }
            if (TryParseItemCredentialUx1AE(credential) is { } itemId &&
                Project!.Blueprints.All(item => item.Id != itemId || item.Kind != BlueprintKind.Item))
            {
                message = "L’objet clé choisi n’existe plus dans ce projet.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.DeniedFeedbackKey) || draft.DeniedFeedbackKey.Trim().Length > 256)
            {
                message = "Indiquez le feedback affiché lorsque le justificatif manque.";
                return false;
            }
        }
        return true;
    }

    private static TriggerWorldProfile CreateConfiguredTriggerUx1AE(
        Guid profileId,
        BlueprintId blueprintId,
        AreaId areaId,
        Guid revealTargetId,
        BlueprintId itemId,
        Guid dialogueId,
        string segment,
        bool discoveryAccess = true,
        bool foundation = true)
    {
        var clickScript = "scripts/ux1-ae-click.forge";
        var foundationPolicy = foundation
            ? CreateTriggerFoundationUx1AE(profileId, segment, clickScript)
            : null;
        return new TriggerWorldProfile(
            profileId,
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1AEProvenance,
            blueprintId,
            areaId,
            discoveryAccess
                ? new TriggerWorldProfile.DiscoveryPolicy(
                    ["sight", "search"],
                    12,
                    revealTargetId,
                    "trigger.secret.revealed",
                    true,
                    TriggerWorldProfile.DiscoveryScope.Party)
                : null,
            discoveryAccess
                ? new TriggerWorldProfile.InteractionPolicy(
                    "inspect",
                    "cursor.secret",
                    "outline.gold",
                    "trigger.secret.prompt",
                    clickScript,
                    true,
                    "trigger.use")
                : null,
            new TriggerWorldProfile.SchedulerPolicy(3, 20, true, true),
            $"trigger.ux1-ae.{segment}",
            discoveryAccess
                ? new TriggerWorldProfile.AccessPolicy(
                    ItemCredentialUx1AE(itemId),
                    false,
                    "trigger.access.denied")
                : null,
            new TriggerWorldProfile.RestPolicy(
                true,
                "danger.nearby",
                true,
                true,
                "rest.allowed",
                "rest.denied"),
            new TriggerWorldProfile.CluePolicy(
                "tracking",
                8,
                "secret-trace",
                45,
                2,
                "false-trail",
                null),
            new TriggerWorldProfile.NarrativeCuePolicy(
                [],
                4,
                true,
                60,
                "party-vote",
                dialogueId),
            new TriggerWorldProfile.DirectorPolicy(true, true, true, true, true),
            foundationPolicy,
            foundation,
            foundation ? CreateTriggerAdvancedUx1AE(profileId) : null,
            foundation);
    }

    private static TriggerWorldProfile.FoundationPolicy CreateTriggerFoundationUx1AE(
        Guid profileId,
        string segment,
        string clickScript)
    {
        var portalId = StableTriggerIdUx1AE(profileId, $"{segment}-portal");
        var actionTarget = $"trigger:ux1-ae-{segment}";
        var actionScript = "scripts/ux1-ae-action.forge";
        var enterScript = "scripts/ux1-ae-enter.forge";
        var exitScript = "scripts/ux1-ae-exit.forge";
        return new TriggerWorldProfile.FoundationPolicy(
            StableTriggerIdUx1AE(profileId, $"{segment}-foundation"),
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1AEProvenance,
            $"ux1-ae-{segment}",
            new TriggerWorldProfile.ShapePolicy(
                TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                [],
                new Vector3Value(4, 3, 4),
                3,
                .25,
                true),
            new TriggerWorldProfile.FilterPolicy(["creature"], ["player"], ["undead"], ["characters"]),
            ["area.ready"],
            5,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new TriggerWorldProfile.ActionPolicy(
                StableTriggerIdUx1AE(profileId, $"{segment}-action"),
                "script",
                actionTarget,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["script"] = actionScript })],
            new TriggerWorldProfile.PortalPolicy(
                TriggerWorldProfile.DestinationKind.Waypoint,
                portalId,
                "anchor:ux1-ae-exit",
                new Vector3Value(0, 180, 0),
                true,
                ["portal.travel"]),
            new TriggerWorldProfile.TransformPolicy(
                Vector3Value.Zero,
                Vector3Value.Zero,
                Vector3Value.One,
                null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["editor.overlay"] = "volume",
                ["ux1-ae.preserved"] = segment,
            },
            new TriggerWorldProfile.EventPolicy(
                StableTriggerIdUx1AE(profileId, $"{segment}-enter"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1AEProvenance,
                "1.0.0",
                "VolumeEntered",
                null,
                enterScript),
            new TriggerWorldProfile.EventPolicy(
                StableTriggerIdUx1AE(profileId, $"{segment}-exit"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1AEProvenance,
                "1.0.0",
                "VolumeExited",
                null,
                exitScript),
            new TriggerWorldProfile.RuntimeValidationPolicy(
                [portalId],
                [actionTarget],
                [actionScript, enterScript, exitScript, clickScript],
                ["characters"]),
            true);
    }

    private static TriggerAdvancedProfile CreateTriggerAdvancedUx1AE(Guid profileId)
    {
        var volumes = Enum.GetValues<TriggerGameplayVolumeKind>()
            .Select(kind =>
            {
                var parameter = PrimaryTriggerVolumeParameterUx1AC(kind);
                return new TriggerGameplayVolumeProfile(
                    StableTriggerIdUx1AE(profileId, $"advanced-volume-{kind}"),
                    kind,
                    new Dictionary<string, double>(StringComparer.Ordinal) { [parameter.Key] = parameter.Value },
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    $"{kind}VolumeEntered",
                    $"{kind}VolumeExited",
                    true);
            })
            .ToArray();
        return new TriggerAdvancedProfile(
            StableTriggerIdUx1AE(profileId, "advanced"),
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1AEProvenance,
            "1.0.0",
            new TriggerTrapProfile(
                StableTriggerIdUx1AE(profileId, "advanced-trap"),
                "enter",
                "damage:ux1-ae",
                10,
                12,
                30,
                "module",
                "vfx:ux1-ae",
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
                StableTriggerIdUx1AE(profileId, "advanced-query"),
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
                StableTriggerIdUx1AE(profileId, "advanced-debug"),
                true,
                true,
                true,
                true,
                true,
                true,
                64));
    }

    private static Guid[] TriggerAdvancedComponentIdsUx1AE(Guid profileId) =>
    [
        StableTriggerIdUx1AE(profileId, "advanced"),
        StableTriggerIdUx1AE(profileId, "advanced-trap"),
        StableTriggerIdUx1AE(profileId, "advanced-query"),
        StableTriggerIdUx1AE(profileId, "advanced-debug"),
        .. Enum.GetValues<TriggerGameplayVolumeKind>()
            .Select(kind => StableTriggerIdUx1AE(profileId, $"advanced-volume-{kind}")),
    ];

    private static Guid StableTriggerIdUx1AE(Guid seed, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux1-ae:{seed:N}:{segment}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }

    private static string UniqueBlueprintSlugUx1AE(
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

    private static string ItemCredentialUx1AE(BlueprintId itemId) => $"item:{itemId.Value:D}";

    private static BlueprintId? TryParseItemCredentialUx1AE(string value) =>
        value.StartsWith("item:", StringComparison.OrdinalIgnoreCase) &&
        Guid.TryParse(value[5..], out var itemId)
            ? BlueprintId.From(itemId)
            : null;

    private static string HumanizeCredentialUx1AE(string value)
    {
        var separator = value.IndexOf(':');
        var name = separator >= 0 && separator < value.Length - 1 ? value[(separator + 1)..] : value;
        return string.Join(' ', name.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static string HumanizeDiscoveryScopeUx1AE(TriggerWorldProfile.DiscoveryScope value) => value switch
    {
        TriggerWorldProfile.DiscoveryScope.Player => "joueur",
        TriggerWorldProfile.DiscoveryScope.Party => "groupe",
        TriggerWorldProfile.DiscoveryScope.World => "monde",
        _ => "groupe",
    };

    private static StudioTriggerDiscoveryAccessPreview FailedTriggerDiscoveryAccessPreviewUx1AE(
        string message) => new(false, message, string.Empty, false, false, false, false, false);

    private static string HumanizeTriggerDiscoveryAccessUx1AE(string? code) => code switch
    {
        "world.trigger.discovery_invalid" =>
            "La découverte exige au moins un sens, une cible présente et un feedback lisible.",
        "world.trigger.interaction_invalid" =>
            "Le clic exige un verbe, un script et une permission valides.",
        "world.trigger.access_invalid" =>
            "L’accès exige un objet clé ou un justificatif métier.",
        "world.trigger.script_reference_missing" =>
            "Le script OnClick doit être déclaré dans la validation du déclencheur.",
        "world.trigger.validation_required_missing" =>
            "Le contexte de validation requis manque. Configurez d’abord Validation de trigger.",
        "world.trigger.foundation_required_missing" =>
            "Configurez d’abord le volume de base avec l’outil Déclencheur.",
        _ => "La découverte et l’accès ne peuvent pas être prévisualisés. Vérifiez les champs nommés.",
    };

    private void CloseUx1AE() => CancelTriggerDiscoveryAccessUx1AE();
}

internal sealed partial class StudioSessionController
{
    internal StudioUx1AESmokeResult RunUx1AEContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ae-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var targetBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("ae100000-0000-4000-8000-000000000001")),
                "secret-du-sanctuaire",
                "Secret du sanctuaire",
                BlueprintKind.Trigger);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("ae100000-0000-4000-8000-000000000002")),
                "reliquaire-cache",
                "Reliquaire caché",
                BlueprintKind.Trigger);
            var foundationlessBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("ae100000-0000-4000-8000-000000000003")),
                "seuil-inacheve",
                "Seuil inachevé",
                BlueprintKind.Trigger);
            var itemBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("ae100000-0000-4000-8000-000000000004")),
                "sceau-du-sanctuaire",
                "Sceau du sanctuaire",
                BlueprintKind.Item);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("ae100000-0000-4000-8000-000000000005")),
                "sanctuaire-des-brumes",
                "Sanctuaire des brumes",
                [],
                AreaKind.Interior,
                new AreaDimensions(48, 48));
            var projectId = ProjectId.From(Guid.Parse("ae100000-0000-4000-8000-000000000006"));
            var dialogueId = DialogueId.From(Guid.Parse("ae100000-0000-4000-8000-000000000007"));
            var nodeId = DialogueNodeId.From(Guid.Parse("ae100000-0000-4000-8000-000000000008"));
            var dialogue = new DialogueDefinition(
                dialogueId,
                "murmure-du-sanctuaire",
                "Murmure du sanctuaire",
                nodeId,
                [new(nodeId, null, new("dialogue.ux1ae.smoke", "Le reliquaire se dévoile."))],
                provenance: "test:ux1-ae",
                entryPoints:
                [new(Guid.Parse("ae100000-0000-4000-8000-000000000009"), 1, "default", nodeId, 0,
                    "test:ux1-ae")]);
            var shell = new ProjectDefinition(
                projectId,
                "Studio UX1-AE",
                [targetBlueprint, neighborBlueprint, foundationlessBlueprint, itemBlueprint],
                [area]);
            var targetProfileId = StableTriggerIdUx1AE(projectId.Value, "smoke-target-profile");
            var neighborProfileId = StableTriggerIdUx1AE(projectId.Value, "smoke-neighbor-profile");
            var foundationlessProfileId = StableTriggerIdUx1AE(projectId.Value, "smoke-foundationless-profile");
            var targetProfile = CreateConfiguredTriggerUx1AE(
                targetProfileId,
                targetBlueprint.Id,
                area.Id,
                neighborProfileId,
                itemBlueprint.Id,
                dialogueId.Value,
                "target",
                discoveryAccess: false);
            var neighborProfile = CreateConfiguredTriggerUx1AE(
                neighborProfileId,
                neighborBlueprint.Id,
                area.Id,
                targetProfileId,
                itemBlueprint.Id,
                dialogueId.Value,
                "neighbor");
            var foundationlessProfile = CreateConfiguredTriggerUx1AE(
                foundationlessProfileId,
                foundationlessBlueprint.Id,
                area.Id,
                targetProfileId,
                itemBlueprint.Id,
                dialogueId.Value,
                "foundationless",
                discoveryAccess: false,
                foundation: false);
            var catalog = new WorldFeatureCatalog(
                shell.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ae",
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
                return FailedUx1AESmoke();
            }

            var initialProject = editing.Project!;
            var initialCatalog = initialProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var loadedNeighbor = initialCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            var selectionExplicit = editing.CreateTriggerDiscoveryAccessDraftUx1AE() is null &&
                                    editing.TriggerDiscoveryAccessChoicesUx1AE.Count == 3;
            var foundationRequired =
                editing.CreateTriggerDiscoveryAccessDraftUx1AE(foundationlessProfile.Id) is null &&
                editing.TriggerDiscoveryAccessChoicesUx1AE.Single(item =>
                    item.ProfileId == foundationlessProfile.Id) is { HasFoundation: false };

            var staleDraft = editing.CreateTriggerDiscoveryAccessDraftUx1AE(targetProfile.Id)!;
            var firstIntermediate = ApplyUx1AEIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-before-preview");
            var staleDraftPreview = editing.PreviewTriggerDiscoveryAccessUx1AE(staleDraft);
            var staleDraftBlocked = firstIntermediate && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé", StringComparison.Ordinal) &&
                                    editing.Undo().Succeeded;

            var draft = CustomizeUx1AESmokeDraft(
                editing.CreateTriggerDiscoveryAccessDraftUx1AE(targetProfile.Id)!,
                neighborProfile.Id,
                itemBlueprint.Id);
            var invalidDiscovery = editing.PreviewTriggerDiscoveryAccessUx1AE(draft with { Senses = [] });
            var invalidInteraction = editing.PreviewTriggerDiscoveryAccessUx1AE(draft with
            {
                PromptKey = string.Empty,
            });
            var invalidAccess = editing.PreviewTriggerDiscoveryAccessUx1AE(draft with
            {
                CredentialId = string.Empty,
            });
            var invalidScript = editing.PreviewTriggerDiscoveryAccessUx1AE(draft with
            {
                ScriptId = "scripts/not-declared.forge",
            });
            var beforePreviewProject = editing.Project;
            var beforePreviewRevision = editing.History!.State.Revision;
            var beforePreviewJournal = editing.History.Journal.ToArray();
            var beforePreviewDirty = editing.IsDirty;
            var beforePreviewCanUndo = editing.History.CanUndo;
            var beforePreviewCanRedo = editing.History.CanRedo;
            var preview = editing.PreviewTriggerDiscoveryAccessUx1AE(draft);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(beforePreviewProject, editing.Project) &&
                         editing.History.State.Revision == beforePreviewRevision &&
                         editing.History.Journal.SequenceEqual(beforePreviewJournal) &&
                         editing.IsDirty == beforePreviewDirty &&
                         editing.History.CanUndo == beforePreviewCanUndo &&
                         editing.History.CanRedo == beforePreviewCanRedo;

            var secondIntermediate = ApplyUx1AEIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-after-preview");
            var staleApply = editing.ApplyApprovedTriggerDiscoveryAccessUx1AE();
            var staleApprovalBlocked = secondIntermediate && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var freshDraft = CustomizeUx1AESmokeDraft(
                editing.CreateTriggerDiscoveryAccessDraftUx1AE(targetProfile.Id)!,
                neighborProfile.Id,
                itemBlueprint.Id);
            var freshPreview = editing.PreviewTriggerDiscoveryAccessUx1AE(freshDraft);
            var apply = editing.ApplyApprovedTriggerDiscoveryAccessUx1AE();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var appliedNeighbor = appliedCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            if (!freshPreview.Succeeded || !apply.Succeeded || appliedTarget.Discovery is null ||
                appliedTarget.Interaction is null || appliedTarget.Access is null)
            {
                return FailedUx1AESmoke();
            }

            var discovery = appliedTarget.Discovery.Senses.SequenceEqual(["sight", "search", "magic"]) &&
                            appliedTarget.Discovery.Difficulty == 18 &&
                            appliedTarget.Discovery.RevealTargetId == neighborProfile.Id &&
                            appliedTarget.Discovery.FeedbackId == "trigger.secret.discovered" &&
                            !appliedTarget.Discovery.Permanent &&
                            appliedTarget.Discovery.Scope == TriggerWorldProfile.DiscoveryScope.World;
            var interaction = appliedTarget.Interaction.Verb == "inspect" &&
                              appliedTarget.Interaction.Icon == "cursor.secret" &&
                              appliedTarget.Interaction.Outline == "outline.azure" &&
                              appliedTarget.Interaction.PromptKey == "trigger.secret.inspect" &&
                              appliedTarget.Interaction.ScriptId == "scripts/ux1-ae-click.forge" &&
                              appliedTarget.Interaction.Visible &&
                              appliedTarget.Interaction.Permission == "trigger.use.secret";
            var access = appliedTarget.Access.CredentialId == ItemCredentialUx1AE(itemBlueprint.Id) &&
                         appliedTarget.Access.Consume &&
                         appliedTarget.Access.DeniedFeedbackKey == "trigger.secret.key-required";
            var runtimePreview = freshPreview.HiddenBlocked && freshPreview.CredentialBlocked &&
                                 freshPreview.PermissionBlocked && freshPreview.Allowed;
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
                                        ReferenceEquals(appliedTarget.Narrative, loadedTarget.Narrative) &&
                                        ReferenceEquals(appliedTarget.Scheduler, loadedTarget.Scheduler) &&
                                        appliedTarget.CustomEventNamespace == loadedTarget.CustomEventNamespace &&
                                        ReferenceEquals(appliedTarget.Director, loadedTarget.Director);
            var otherTriggersPreserved = initialCatalog.Triggers.Count == appliedCatalog.Triggers.Count &&
                                         initialCatalog.Triggers
                                             .Where(item => item.Id != targetProfile.Id)
                                             .Zip(appliedCatalog.Triggers.Where(item => item.Id != targetProfile.Id))
                                             .All(pair => ReferenceEquals(pair.First, pair.Second));
            var neighborPreserved = ReferenceEquals(appliedNeighbor, loadedNeighbor) &&
                                    otherTriggersPreserved &&
                                    SameReferencesUx1AE(appliedCatalog.Placeables, initialCatalog.Placeables) &&
                                    SameReferencesUx1AE(appliedCatalog.Doors, initialCatalog.Doors) &&
                                    SameReferencesUx1AE(appliedCatalog.Encounters, initialCatalog.Encounters) &&
                                    SameReferencesUx1AE(
                                        appliedCatalog.VehicleRoutes,
                                        initialCatalog.VehicleRoutes) &&
                                    SameReferencesUx1AE(appliedCatalog.Merchants, initialCatalog.Merchants) &&
                                    SameReferencesUx1AE(appliedCatalog.Companions, initialCatalog.Companions) &&
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
            var undoRedo = undo.Succeeded && removed.Discovery is null && removed.Interaction is null &&
                           removed.Access is null && redo.Succeeded &&
                           SameDiscoveryUx1AE(appliedTarget.Discovery, restored.Discovery) &&
                           SameInteractionUx1AE(appliedTarget.Interaction, restored.Interaction) &&
                           SameAccessUx1AE(appliedTarget.Access, restored.Access);

            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistedTarget = opened
                ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item => item.Id == targetProfile.Id)
                : null;
            var persistence = persistedTarget is not null &&
                              SameDiscoveryUx1AE(appliedTarget.Discovery, persistedTarget.Discovery) &&
                              SameInteractionUx1AE(appliedTarget.Interaction, persistedTarget.Interaction) &&
                              SameAccessUx1AE(appliedTarget.Access, persistedTarget.Access) &&
                              SameFoundationUx1AE(appliedTarget.Foundation!, persistedTarget.Foundation!) &&
                              SameAdvancedUx1AE(appliedTarget.Advanced!, persistedTarget.Advanced!) &&
                              SameUx1AENeighbors(appliedTarget, persistedTarget);
            var invalidDiscoveryBlocked = !invalidDiscovery.Succeeded;
            var invalidInteractionBlocked = !invalidInteraction.Succeeded;
            var invalidAccessBlocked = !invalidAccess.Succeeded;
            var validationScriptBlocked = !invalidScript.Succeeded &&
                                          invalidScript.Message.Contains(
                                              "validation",
                                              StringComparison.OrdinalIgnoreCase);
            var humanDiagnostics = IsHumanUx1AEMessage(invalidDiscovery.Message) &&
                                   IsHumanUx1AEMessage(invalidInteraction.Message) &&
                                   IsHumanUx1AEMessage(invalidAccess.Message) &&
                                   IsHumanUx1AEMessage(invalidScript.Message) &&
                                   IsHumanUx1AEMessage(preview.Message);
            var succeeded = selectionExplicit && foundationRequired && discovery && interaction && access &&
                            runtimePreview && dryRun && invalidDiscoveryBlocked && invalidInteractionBlocked &&
                            invalidAccessBlocked && validationScriptBlocked && staleDraftBlocked &&
                            staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            foundationPreserved && portalPreserved && validationPreserved && advancedPreserved &&
                            ux2NeighborsPreserved && neighborPreserved && humanDiagnostics;
            return new StudioUx1AESmokeResult(
                succeeded,
                selectionExplicit,
                foundationRequired,
                discovery,
                interaction,
                access,
                runtimePreview,
                dryRun,
                invalidDiscoveryBlocked,
                invalidInteractionBlocked,
                invalidAccessBlocked,
                validationScriptBlocked,
                staleDraftBlocked,
                staleApprovalBlocked,
                canonicalCommand,
                undoRedo,
                persistence,
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
            return FailedUx1AESmoke();
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

    private static StudioTriggerDiscoveryAccessDraft CustomizeUx1AESmokeDraft(
        StudioTriggerDiscoveryAccessDraft draft,
        Guid revealTargetId,
        BlueprintId itemId) => draft with
    {
        DiscoveryEnabled = true,
        Senses = ["sight", "search", "magic"],
        Difficulty = 18,
        RevealTargetId = revealTargetId,
        FeedbackId = "trigger.secret.discovered",
        Permanent = false,
        Scope = TriggerWorldProfile.DiscoveryScope.World,
        InteractionEnabled = true,
        Verb = "inspect",
        Icon = "cursor.secret",
        Outline = "outline.azure",
        PromptKey = "trigger.secret.inspect",
        ScriptId = "scripts/ux1-ae-click.forge",
        Visible = true,
        Permission = "trigger.use.secret",
        AccessEnabled = true,
        CredentialId = ItemCredentialUx1AE(itemId),
        ConsumeCredential = true,
        DeniedFeedbackKey = "trigger.secret.key-required",
    };

    private static bool ApplyUx1AEIntermediateMutation(
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
        var result = editing._setTriggerDiscoveryAccessCatalogUx1AE.Handle(history.State, command);
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    private static bool SameDiscoveryUx1AE(
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

    private static bool SameInteractionUx1AE(
        TriggerWorldProfile.InteractionPolicy? expected,
        TriggerWorldProfile.InteractionPolicy? actual) =>
        expected is null && actual is null || expected == actual;

    private static bool SameAccessUx1AE(
        TriggerWorldProfile.AccessPolicy? expected,
        TriggerWorldProfile.AccessPolicy? actual) =>
        expected is null && actual is null || expected == actual;

    private static bool SameUx1AENeighbors(TriggerWorldProfile expected, TriggerWorldProfile actual) =>
        expected.Id == actual.Id &&
        expected.SchemaVersion == actual.SchemaVersion &&
        expected.Provenance == actual.Provenance &&
        expected.BlueprintId == actual.BlueprintId &&
        expected.AreaId == actual.AreaId &&
        expected.Scheduler == actual.Scheduler &&
        expected.CustomEventNamespace == actual.CustomEventNamespace &&
        expected.Rest == actual.Rest &&
        expected.Clue == actual.Clue &&
        expected.Narrative == actual.Narrative &&
        expected.Director == actual.Director &&
        expected.FoundationRequiredAtRuntime == actual.FoundationRequiredAtRuntime &&
        expected.AdvancedRequiredAtRuntime == actual.AdvancedRequiredAtRuntime;

    private static bool SameFoundationUx1AE(
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

    private static bool SameAdvancedUx1AE(TriggerAdvancedProfile expected, TriggerAdvancedProfile actual) =>
        expected.Id == actual.Id &&
        expected.SchemaVersion == actual.SchemaVersion &&
        expected.Provenance == actual.Provenance &&
        expected.ForgeScriptApiVersion == actual.ForgeScriptApiVersion &&
        expected.Trap == actual.Trap &&
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

    private static bool SameReferencesUx1AE<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class =>
        left.Count == right.Count && left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx1AEMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains('{') &&
        !message.Contains("Guid", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !System.Text.RegularExpressions.Regex.IsMatch(
            message,
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    private static StudioUx1AESmokeResult FailedUx1AESmoke() => new(
        Succeeded: false,
        SelectionExplicit: false,
        FoundationRequired: false,
        Discovery: false,
        Interaction: false,
        Access: false,
        RuntimePreview: false,
        DryRun: false,
        InvalidDiscoveryBlocked: false,
        InvalidInteractionBlocked: false,
        InvalidAccessBlocked: false,
        ValidationScriptBlocked: false,
        StaleDraftBlocked: false,
        StaleApprovalBlocked: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        FoundationPreserved: false,
        PortalPreserved: false,
        ValidationPreserved: false,
        AdvancedPreserved: false,
        Ux2NeighborsPreserved: false,
        NeighborPreserved: false,
        HumanDiagnostics: false);
}
