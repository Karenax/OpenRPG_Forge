using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Domain.Doors;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1AJEvidenceProvenance = "studio:ux1-aj:evidence";
    private readonly SetWorldFeatureCatalogHandler _setDoorValidationCatalogUx1AJ = new(new SystemClock());
    private StudioDoorValidationDraft? _approvedDoorValidationUx1AJ;
    private ProjectRevision? _approvedDoorValidationRevisionUx1AJ;
    private string? _approvedDoorValidationFingerprintUx1AJ;

    internal IReadOnlyList<StudioDoorValidationChoice> DoorValidationChoicesUx1AJ
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            return project.Module.WorldFeatures.Doors.Select(profile =>
                {
                    var blueprint = project.Blueprints.FirstOrDefault(item => item.Id == profile.BlueprintId);
                    var name = blueprint?.DisplayName ?? "Porte sans blueprint";
                    var instances = project.Areas.Sum(area =>
                        area.Entities.Count(entity => entity.BlueprintId == profile.BlueprintId));
                    var prerequisites = profile.Foundation is null
                        ? "fondation absente"
                        : profile.Foundation.Navigation is null
                            ? "navigation absente"
                            : "fondation et navigation prêtes";
                    var state = profile.Validation is null
                        ? profile.ValidationRequiredAtRuntime
                            ? "contexte requis manquant"
                            : "non évalué"
                        : profile.ValidationRequiredAtRuntime
                            ? "contexte requis configuré"
                            : "contexte optionnel configuré";
                    return new StudioDoorValidationChoice(
                        profile.Id,
                        profile.BlueprintId,
                        $"{name} · {prerequisites} · {instances} instance(s) · {state}",
                        profile.Foundation is not null,
                        profile.Foundation?.Navigation is not null,
                        profile.Validation is not null,
                        profile.ValidationRequiredAtRuntime,
                        string.Equals(profile.Provenance, Ux1AJEvidenceProvenance, StringComparison.Ordinal));
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal StudioDoorValidationDraft? CreateDoorValidationDraftUx1AJ(Guid? requestedProfileId = null)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || requestedProfileId is null)
        {
            return null;
        }

        var matches = project.Module.WorldFeatures.Doors.Where(item => item.Id == requestedProfileId).ToArray();
        if (matches.Length != 1 || !HasDoorValidationPrerequisitesUx1AJ(matches[0]))
        {
            return null;
        }

        var profile = matches[0];
        var validation = profile.Validation;
        return new StudioDoorValidationDraft(
            profile.Id,
            profile.BlueprintId,
            history.State.Revision,
            validation is not null,
            profile.ValidationRequiredAtRuntime,
            validation?.KnownDestinationIds.ToArray() ?? [],
            validation?.KnownAnimationIds.ToArray() ?? [],
            validation?.KnownCollisionProfileIds.ToArray() ?? [],
            validation?.KnownCredentialIds.ToArray() ?? [],
            validation?.KnownScriptIds.ToArray() ?? [],
            profile);
    }

    internal StudioDoorValidationSuggestions GetDoorValidationSuggestionsUx1AJ(Guid profileId)
    {
        var project = Project;
        if (project is null)
        {
            return new([], [], [], [], [], null);
        }
        var catalog = project.Module.WorldFeatures;
        var profile = catalog.Doors.SingleOrDefault(item => item.Id == profileId);
        if (profile is null)
        {
            return new([], [], [], [], [], null);
        }

        var destinations = new List<StudioDoorDestinationSuggestion>();
        if (profile.Foundation?.Portal is { } portal)
        {
            destinations.Add(new(
                portal.DestinationId,
                HumanDoorDestinationUx1AJ(project, portal.DestinationKind, portal.DestinationId),
                $"Fondation canonique · {profile.Provenance}",
                true));
        }
        if (profile.Validation is { } existingValidation)
        {
            var position = 1;
            foreach (var id in existingValidation.KnownDestinationIds)
            {
                destinations.Add(new(id, $"Destination qualifiée existante {position++}",
                    $"Politique enregistrée · {profile.Provenance}",
                    profile.Foundation?.Portal?.DestinationId == id));
            }
        }
        foreach (var door in catalog.Doors.Where(item => item.Id != profile.Id))
        {
            var label = project.Blueprints.FirstOrDefault(item => item.Id == door.BlueprintId)?.DisplayName ??
                        "Porte du catalogue";
            destinations.Add(new(door.Id, $"Porte · {label}", $"Catalogue Monde vivant · {door.Provenance}", false));
        }
        foreach (var trigger in catalog.Triggers)
        {
            var label = project.Blueprints.FirstOrDefault(item => item.Id == trigger.BlueprintId)?.DisplayName ??
                        "Déclencheur du catalogue";
            destinations.Add(new(trigger.Id, $"Déclencheur · {label}",
                $"Catalogue Monde vivant · {trigger.Provenance}", false));
        }

        var animations = new List<StudioDoorValidationSuggestion>();
        if (profile.Foundation is { } foundation)
        {
            animations.AddRange(foundation.States.Select(state => new StudioDoorValidationSuggestion(
                state.AnimationId,
                $"Animation de l’état « {state.Id} »",
                $"Fondation de la porte · {profile.Provenance}")));
        }
        if (profile.AccessEvents is { } accessEvents)
        {
            animations.Add(new(
                accessEvents.Rejection.AnimationId,
                "Animation authorée du refus d’ouverture",
                $"Politique d’accès · {accessEvents.Provenance}"));
        }
        animations.AddRange(AssetManifests.Where(item => item.Animation is not null)
            .SelectMany(manifest => manifest.Animation!.Clips.Select(clip => new StudioDoorValidationSuggestion(
                clip.Name,
                $"Clip gouverné · {clip.Name}",
                $"Manifeste asset · {manifest.ModificationProvenance}"))));

        var collisions = AssetManifests.Where(item => item.Collision is not null)
            .SelectMany(manifest => manifest.Collision!.MaterialTags.Select(tag => new StudioDoorValidationSuggestion(
                $"collision:{tag}",
                $"Profil collision · {tag}",
                $"Manifeste asset · {manifest.ModificationProvenance}")))
            .ToList();
        StudioDoorAuthoredCollision? authoredCollision = null;
        if (profile.Foundation?.Overrides.TryGetValue(
                DoorWorldProfile.CollisionProfileOverrideKey,
                out var authoredCollisionId) == true &&
            !string.IsNullOrWhiteSpace(authoredCollisionId))
        {
            authoredCollision = new(
                authoredCollisionId,
                $"Collision authorée par la fondation · {authoredCollisionId}",
                $"Override canonique {DoorWorldProfile.CollisionProfileOverrideKey} · {profile.Provenance}");
        }
        var credentials = catalog.Doors.Where(item => item.AccessEvents is not null)
            .SelectMany(door => door.AccessEvents!.Lock.CredentialIds.Select(value =>
                new StudioDoorValidationSuggestion(value, $"Accès porte · {value}",
                    $"Politique d’accès · {door.AccessEvents.Provenance}")))
            .Concat(catalog.Placeables.Where(item => item.Credential is not null)
                .Select(item => new StudioDoorValidationSuggestion(
                    item.Credential!.CredentialId,
                    $"Accès placeable · {item.Credential.CredentialId}",
                    $"Catalogue Monde vivant · {item.Provenance}")))
            .ToList();
        var scripts = catalog.Doors.SelectMany(door => door.Events.Select(binding =>
                new StudioDoorValidationSuggestion(binding.ScriptId, $"Événement porte · {binding.Event}",
                    $"Profil de porte · {door.Provenance}")))
            .Concat(catalog.Doors.Where(item => item.AccessEvents is not null).Select(door =>
                new StudioDoorValidationSuggestion(
                    door.AccessEvents!.Rejection.ScriptId,
                    "Refus d’ouverture",
                    $"Accès porte · {door.AccessEvents.Provenance}")))
            .Concat(catalog.Scripts.TagRouting.Routes.Select(route => new StudioDoorValidationSuggestion(
                route.ScriptId,
                $"Routage {route.BlueprintTag} · {route.Event}",
                $"Routage de scripts · {catalog.Scripts.Provenance}")))
            .Concat(FindForgeScripts().Select(script => new StudioDoorValidationSuggestion(
                script.RelativePath,
                script.DisplayName,
                "Fichier ForgeScript du workspace")))
            .ToList();

        if (profile.Validation is { } validation)
        {
            animations.AddRange(validation.KnownAnimationIds.Select(value =>
                ExistingDoorSuggestionUx1AJ(value, profile.Provenance)));
            collisions.AddRange(validation.KnownCollisionProfileIds.Select(value =>
                ExistingDoorSuggestionUx1AJ(value, profile.Provenance)));
            credentials.AddRange(validation.KnownCredentialIds.Select(value =>
                ExistingDoorSuggestionUx1AJ(value, profile.Provenance)));
            scripts.AddRange(validation.KnownScriptIds.Select(value =>
                ExistingDoorSuggestionUx1AJ(value, profile.Provenance)));
        }

        return new(
            destinations.GroupBy(item => item.Id).Select(group => group.First()).ToArray(),
            DistinctDoorSuggestionsUx1AJ(animations),
            DistinctDoorSuggestionsUx1AJ(collisions),
            DistinctDoorSuggestionsUx1AJ(credentials),
            DistinctDoorSuggestionsUx1AJ(scripts),
            authoredCollision);
    }

    internal StudioDoorValidationPreview PreviewDoorValidationUx1AJ(StudioDoorValidationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDoorValidationUx1AJ();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedDoorValidationPreviewUx1AJ("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedDoorValidationPreviewUx1AJ(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la porte avant de prévisualiser.");
        }
        if (!TryBuildDoorValidationUx1AJ(draft, true, out var command, out var candidateProject,
                out var candidateProfile, out var message))
        {
            return FailedDoorValidationPreviewUx1AJ(message);
        }

        var canonical = DoorWorldValidationService.Validate(candidateProject!, candidateProfile!.Id);
        var groups = BuildDoorValidationGroupsUx1AJ(canonical, draft.ValidationEnabled);
        var blocking = groups.FirstOrDefault(item => item.Blocking);
        if (blocking is not null)
        {
            return new(false, blocking.Details.FirstOrDefault() ?? "Le contexte doit être corrigé.",
                BuildDoorValidationSummaryUx1AJ(groups, canonical), false, canonical.Available,
                canonical.OptionalFallback, groups);
        }

        var result = _setDoorValidationCatalogUx1AJ.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            return new(false, HumanizeDoorValidationCodeUx1AJ(result.Diagnostics.FirstOrDefault()?.Code),
                BuildDoorValidationSummaryUx1AJ(groups, canonical), false, canonical.Available,
                canonical.OptionalFallback, groups);
        }

        _approvedDoorValidationUx1AJ = draft;
        _approvedDoorValidationRevisionUx1AJ = history.State.Revision;
        _approvedDoorValidationFingerprintUx1AJ = DoorValidationFingerprintUx1AJ(draft);
        return new(true, "Aperçu validé · aucun document n’a été modifié.",
            BuildDoorValidationSummaryUx1AJ(groups, canonical), true, canonical.Available,
            canonical.OptionalFallback, groups);
    }

    internal StudioUx1AJActionResult ApplyApprovedDoorValidationUx1AJ()
    {
        var draft = _approvedDoorValidationUx1AJ;
        var history = History;
        if (draft is null || history is null ||
            _approvedDoorValidationRevisionUx1AJ != history.State.Revision ||
            !string.Equals(_approvedDoorValidationFingerprintUx1AJ, DoorValidationFingerprintUx1AJ(draft),
                StringComparison.Ordinal))
        {
            CancelDoorValidationUx1AJ();
            return new(false, "Prévisualisez de nouveau la validation canonique avant de l’appliquer.");
        }
        if (!TryBuildDoorValidationUx1AJ(draft, false, out var command, out _, out _, out var message))
        {
            CancelDoorValidationUx1AJ();
            return new(false, message);
        }

        var result = _setDoorValidationCatalogUx1AJ.Handle(history.State, command!);
        CancelDoorValidationUx1AJ();
        if (!result.Succeeded)
        {
            return new(false, HumanizeDoorValidationCodeUx1AJ(result.Diagnostics.FirstOrDefault()?.Code));
        }
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Validation appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelDoorValidationUx1AJ()
    {
        _approvedDoorValidationUx1AJ = null;
        _approvedDoorValidationRevisionUx1AJ = null;
        _approvedDoorValidationFingerprintUx1AJ = null;
    }

    internal void CloseUx1AJ() => CancelDoorValidationUx1AJ();

    internal StudioUx1AJApprovalSnapshot CaptureDoorValidationApprovalUx1AJ() => new(
        _approvedDoorValidationUx1AJ,
        _approvedDoorValidationRevisionUx1AJ,
        _approvedDoorValidationFingerprintUx1AJ);

    private static bool HasDoorValidationPrerequisitesUx1AJ(DoorWorldProfile profile) =>
        profile.Foundation?.Navigation is not null;

    internal ProjectDefinition? CreateUx1AJEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx1AJFixtureProject(source.Id.Value, Ux1AJEvidenceProvenance);
    }

    internal AssetPackageManifest? CreateUx1AJEvidenceManifest()
    {
        var source = Project;
        return source is null ? null : CreateUx1AJFixtureManifest(source.Id.Value, Ux1AJEvidenceProvenance);
    }

    private bool TryBuildDoorValidationUx1AJ(
        StudioDoorValidationDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out ProjectDefinition? candidateProject,
        out DoorWorldProfile? candidateProfile,
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
            message = "Le projet a changé. Rechargez la porte avant de prévisualiser.";
            return false;
        }

        var catalog = project.Module.WorldFeatures;
        var matches = catalog.Doors.Where(item => item.Id == draft.DoorProfileId &&
                                                   item.BlueprintId == draft.BlueprintId).ToArray();
        if (matches.Length != 1)
        {
            message = "La porte a changé depuis son ouverture. Rechargez sa fiche.";
            return false;
        }
        var currentProfile = matches[0];
        var currentFoundation = currentProfile.Foundation;
        if (currentFoundation is null)
        {
            message = "Configurez d’abord la fondation de la porte.";
            return false;
        }
        if (currentFoundation.Navigation is null)
        {
            message = "Configurez d’abord la navigation dynamique de la porte.";
            return false;
        }

        var validation = draft.ValidationEnabled
            ? new DoorWorldProfile.RuntimeValidationPolicy(
                draft.KnownDestinationIds.ToArray(),
                draft.KnownAnimationIds.ToArray(),
                draft.KnownCollisionProfileIds.ToArray(),
                draft.KnownCredentialIds.ToArray(),
                draft.KnownScriptIds.ToArray())
            : null;
        var updatedProfile = currentProfile with
        {
            Validation = validation,
            ValidationRequiredAtRuntime = draft.ValidationRequiredAtRuntime,
        };
        candidateProfile = updatedProfile;
        var candidateCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.Placeables,
            catalog.Doors.Select(item => item.Id == updatedProfile.Id ? updatedProfile : item).ToArray(),
            catalog.Triggers,
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(candidateCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidateCatalog, dryRun);
        return true;
    }

    private static IReadOnlyList<StudioDoorValidationDiagnosticGroup> BuildDoorValidationGroupsUx1AJ(
        DoorWorldValidationResult result,
        bool validationEnabled)
    {
        var definitions = new (string Key, string Title)[]
        {
            ("prerequisites", "Fondation & navigation"),
            ("destinations", "Destinations"),
            ("animations", "Animations"),
            ("collisions", "Collisions"),
            ("credentials", "Identifiants d’accès"),
            ("scripts", "Scripts"),
            ("context", "Contexte runtime"),
        };
        return definitions.Select(definition =>
        {
            var items = result.Diagnostics.Where(item =>
                DoorValidationGroupKeyUx1AJ(item) == definition.Key).ToArray();
            if (items.Length > 0)
            {
                var structured = items.Select(item => new StudioDoorValidationDiagnostic(
                    item.Code, item.Path, HumanizeDoorValidationDiagnosticUx1AJ(item.Code))).ToArray();
                return new StudioDoorValidationDiagnosticGroup(
                    definition.Key,
                    definition.Title,
                    items.Length == 1 ? "1 correction requise" : $"{items.Length} corrections requises",
                    structured.Select(item => item.Message).ToArray(),
                    structured,
                    true);
            }
            if (!validationEnabled && definition.Key != "prerequisites")
            {
                return new StudioDoorValidationDiagnosticGroup(
                    definition.Key,
                    definition.Title,
                    "Non évalué",
                    ["Le contexte optionnel est absent : cette catégorie n’a pas été qualifiée."],
                    [],
                    false);
            }
            return new StudioDoorValidationDiagnosticGroup(
                definition.Key,
                definition.Title,
                result.Available || definition.Key == "prerequisites" ? "Conforme" : "Non évalué",
                [result.Available || definition.Key == "prerequisites"
                    ? "Aucune anomalie signalée par le validateur canonique."
                    : "Cette catégorie ne possède pas de verdict canonique."],
                [],
                false);
        }).ToArray();
    }

    private static string DoorValidationGroupKeyUx1AJ(Forge.RpgKit.Domain.Doors.DoorDiagnostic diagnostic)
    {
        var code = diagnostic.Code;
        if (code.Contains("DESTINATION", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/portal", StringComparison.Ordinal))
            return "destinations";
        if (code.Contains("ANIMATION", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/states", StringComparison.Ordinal))
            return "animations";
        if (code.Contains("COLLISION", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/collision", StringComparison.Ordinal))
            return "collisions";
        if (code.Contains("CREDENTIAL", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/lock", StringComparison.Ordinal))
            return "credentials";
        if (code.Contains("SCRIPT", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/scripts", StringComparison.Ordinal))
            return "scripts";
        if (code.Contains("LINK", StringComparison.Ordinal))
            return "context";
        if (code.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Path.EndsWith("/validation", StringComparison.Ordinal))
            return "context";
        return "prerequisites";
    }

    private static string HumanizeDoorValidationDiagnosticUx1AJ(string code) => code switch
    {
        "RPG_DOOR_DESTINATION_MISSING" =>
            "La destination utilisée par la porte n’est pas déclarée parmi les destinations connues.",
        "RPG_DOOR_ANIMATION_MISSING" =>
            "Au moins une animation d’état manque dans le contexte connu.",
        "RPG_DOOR_COLLISION_PROFILE_MISSING" =>
            "La fondation ne possède pas de profil de collision authoré séparément.",
        "RPG_DOOR_COLLISION_PROFILE_MISMATCH" =>
            "Le profil collision authoré par la fondation n’appartient pas au catalogue connu.",
        "RPG_DOOR_CREDENTIAL_MISSING" =>
            "Un identifiant d’accès du verrou manque dans le contexte connu.",
        "RPG_DOOR_SCRIPT_MISSING" =>
            "Un script utilisé par la porte manque dans le contexte connu.",
        "RPG_DOOR_SCRIPT_INVALID" =>
            "Une liaison de script de la porte est vide.",
        "RPG_DOOR_NAVIGATION_INVALID" =>
            "La navigation dynamique de la porte est incomplète.",
        "RPG_DOOR_LINK_ORPHAN" =>
            "Un lien de porte pointe vers une porte absente. Corrigez-le dans l’outil Fondation ; cette fiche ne modifie pas les liens.",
        "RPG_DOOR_LINK_REVERSE_MISSING" =>
            "Un lien bidirectionnel n’a pas son retour. Corrigez-le dans l’outil Fondation ; cette fiche ne modifie pas les liens.",
        "world.door.validation_required_missing" =>
            "Le contexte de validation est requis mais absent.",
        "world.door.validation_context_invalid" =>
            "Le contexte contient une référence vide ou dupliquée.",
        "world.door.validation_foundation_missing" =>
            "La fondation canonique de la porte est requise.",
        "world.door.validation_navigation_missing" or "world.door.navigation_invalid" =>
            "La navigation canonique de la porte est requise.",
        "world.door.validation_blueprint_missing" =>
            "Le blueprint canonique de la porte est introuvable.",
        _ => "Le validateur canonique signale un point à corriger dans cette catégorie.",
    };

    private static string HumanizeDoorValidationCodeUx1AJ(string? code) => code switch
    {
        "command.revision_conflict" =>
            "Le projet a changé. Rechargez puis prévisualisez de nouveau la validation.",
        "world.door.validation_required_missing" =>
            "Le contexte de validation est requis mais absent.",
        "world.door.validation_context_invalid" =>
            "Le contexte contient une référence vide ou dupliquée.",
        _ => "La validation canonique ne peut pas être appliquée. Consultez les catégories signalées.",
    };

    private static string BuildDoorValidationSummaryUx1AJ(
        IReadOnlyList<StudioDoorValidationDiagnosticGroup> groups,
        DoorWorldValidationResult result)
    {
        var runtime = result.Available
            ? "Runtime prêt · politique validée, sans simulation moteur ni réseau"
            : result.OptionalFallback
                ? "Runtime non qualifié · contexte optionnel absent · Non évalué"
                : "Runtime non qualifié · corrections requises";
        return runtime + "\n" + string.Join("\n", groups.Select(item => $"{item.Title} · {item.State}"));
    }

    private static StudioDoorValidationPreview FailedDoorValidationPreviewUx1AJ(string message) =>
        new(false, message, string.Empty, false, false, false, []);

    private static string DoorValidationFingerprintUx1AJ(StudioDoorValidationDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)1);
            WriteGuidUx1AJ(writer, draft.DoorProfileId);
            WriteGuidUx1AJ(writer, draft.BlueprintId.Value);
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.ValidationEnabled);
            writer.Write(draft.ValidationRequiredAtRuntime);
            WriteGuidListUx1AJ(writer, draft.KnownDestinationIds);
            WriteStringListUx1AJ(writer, draft.KnownAnimationIds);
            WriteStringListUx1AJ(writer, draft.KnownCollisionProfileIds);
            WriteStringListUx1AJ(writer, draft.KnownCredentialIds);
            WriteStringListUx1AJ(writer, draft.KnownScriptIds);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteGuidUx1AJ(BinaryWriter writer, Guid value)
    {
        var bytes = value.ToByteArray();
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteGuidListUx1AJ(BinaryWriter writer, IReadOnlyList<Guid> values)
    {
        writer.Write(values.Count);
        foreach (var value in values)
            WriteGuidUx1AJ(writer, value);
    }

    private static void WriteStringListUx1AJ(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    private static string HumanDoorDestinationUx1AJ(ProjectDefinition project,
        DoorWorldProfile.DestinationKind kind, Guid id)
    {
        var catalog = project.Module.WorldFeatures;
        if (kind == DoorWorldProfile.DestinationKind.Door)
        {
            var door = catalog.Doors.FirstOrDefault(item => item.Id == id);
            var name = door is null
                ? null
                : project.Blueprints.FirstOrDefault(item => item.Id == door.BlueprintId)?.DisplayName;
            return name is null ? "Porte de destination héritée" : $"Porte · {name}";
        }
        if (kind == DoorWorldProfile.DestinationKind.Trigger)
        {
            var trigger = catalog.Triggers.FirstOrDefault(item => item.Id == id);
            var name = trigger is null
                ? null
                : project.Blueprints.FirstOrDefault(item => item.Id == trigger.BlueprintId)?.DisplayName;
            return name is null ? "Déclencheur de destination hérité" : $"Déclencheur · {name}";
        }
        return kind == DoorWorldProfile.DestinationKind.Waypoint
            ? "Point d’arrivée hérité de la porte"
            : "Ancre d’apparition héritée de la porte";
    }

    private static StudioDoorValidationSuggestion ExistingDoorSuggestionUx1AJ(string value, string provenance) =>
        new(value, $"Référence déjà qualifiée · {value}", $"Politique enregistrée · {provenance}");

    private static IReadOnlyList<StudioDoorValidationSuggestion> DistinctDoorSuggestionsUx1AJ(
        IEnumerable<StudioDoorValidationSuggestion> suggestions) => suggestions
        .Where(item => !string.IsNullOrWhiteSpace(item.Value))
        .GroupBy(item => item.Value, StringComparer.Ordinal)
        .Select(group => group.First())
        .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    private static Guid StableDoorValidationIdUx1AJ(Guid seed, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux1-aj:{seed:N}:{segment}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}

internal sealed partial class StudioSessionController
{
    private static AssetPackageManifest CreateUx1AJFixtureManifest(Guid seed, string provenance)
    {
        var assetId = AssetCatalogItemId.ForExternalId($"ux1-aj-door-collision:{seed:N}");
        var collisionChunkId = StableDoorValidationIdUx1AJ(seed, "collision-chunk");
        return new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(assetId),
            assetId,
            provenance,
            "CC0-1.0",
            new string('a', 64),
            new Dictionary<AssetTargetPlatform, long>
            {
                [AssetTargetPlatform.Windows] = 4096,
            },
            [new AssetManifestChunk(
                collisionChunkId,
                AssetChunkRole.Collision,
                true,
                "assets/imported/ux1-aj/collision.mesh",
                new string('b', 64),
                256,
                256,
                AssetChunkCompression.None,
                [AssetTargetPlatform.Windows])],
            new AssetCollisionManifest(
                true,
                collisionChunkId,
                AssetCollisionGeneration.Embedded,
                ["ux1-aj-gate"],
                [AssetTargetPlatform.Windows]),
            null,
            null,
            null);
    }

    private static ProjectDefinition CreateUx1AJFixtureProject(Guid seed, string provenance)
    {
        var projectId = ProjectId.From(StableDoorValidationIdUx1AJ(seed, "project"));
        var area = new AreaDefinition(
            AreaId.From(StableDoorValidationIdUx1AJ(seed, "area")),
            "galerie-des-portes",
            "Galerie des portes",
            []);
        var targetBlueprint = new BlueprintDefinition(
            BlueprintId.From(StableDoorValidationIdUx1AJ(seed, "target-blueprint")),
            "porte-validation-canonique",
            "Porte de validation canonique",
            BlueprintKind.Door);
        var neighborBlueprint = new BlueprintDefinition(
            BlueprintId.From(StableDoorValidationIdUx1AJ(seed, "neighbor-blueprint")),
            "porte-voisine-preservee",
            "Porte voisine préservée",
            BlueprintKind.Door);
        var foundationlessBlueprint = new BlueprintDefinition(
            BlueprintId.From(StableDoorValidationIdUx1AJ(seed, "foundationless-blueprint")),
            "porte-sans-fondation",
            "Porte sans fondation",
            BlueprintKind.Door);
        var shell = new ProjectDefinition(
            projectId,
            "Studio UX1-AJ",
            [targetBlueprint, neighborBlueprint, foundationlessBlueprint],
            [area]);
        var targetId = StableDoorValidationIdUx1AJ(seed, "target-profile");
        var neighborId = StableDoorValidationIdUx1AJ(seed, "neighbor-profile");
        var target = CreateUx1AJFixtureDoor(
            targetBlueprint.Id,
            targetId,
            neighborId,
            provenance,
            foundation: true,
            navigation: true,
            rich: true);
        var neighbor = CreateUx1AJFixtureDoor(
            neighborBlueprint.Id,
            neighborId,
            targetId,
            provenance,
            foundation: true,
            navigation: true,
            rich: false);
        var foundationless = CreateUx1AJFixtureDoor(
            foundationlessBlueprint.Id,
            StableDoorValidationIdUx1AJ(seed, "foundationless-profile"),
            neighborId,
            provenance,
            foundation: false,
            navigation: false,
            rich: false);
        var catalog = new WorldFeatureCatalog(
            shell.Module.Id.Value,
            WorldFeatureCatalog.CurrentSchemaVersion,
            provenance,
            doors: [target, neighbor, foundationless]);
        return new ProjectDefinition(
            shell.Id,
            shell.Title,
            shell.Module.WithWorldFeatures(catalog),
            shell.Blueprints,
            shell.Areas,
            shell.Dialogues,
            shell.Quests);
    }

    private static DoorWorldProfile CreateUx1AJFixtureDoor(
        BlueprintId blueprintId,
        Guid profileId,
        Guid destinationId,
        string provenance,
        bool foundation,
        bool navigation,
        bool rich)
    {
        if (!foundation)
        {
            return new DoorWorldProfile(
                profileId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                provenance,
                blueprintId,
                null,
                null,
                [],
                null,
                null,
                null);
        }

        var states = new DoorWorldProfile.State[]
        {
            new("open", DoorWorldProfile.StateKind.Open, "animation:ux1-aj-open", false, true),
            new("closed", DoorWorldProfile.StateKind.Closed, "animation:ux1-aj-closed", true, true),
            new("opening", DoorWorldProfile.StateKind.Opening, "animation:ux1-aj-opening", false, false),
        };
        var foundationPolicy = new DoorWorldProfile.FoundationPolicy(
            DoorWorldProfile.PlacementMode.FreePlacement,
            null,
            null,
            "closed",
            states,
            [new("closed", "opening", "interact", 1, null)],
            new(2.5, "both", 1.2, 2.3, .8, "server", 3, "door.unavailable"),
            rich
                ? new DoorWorldProfile.PortalPolicy(
                    DoorWorldProfile.DestinationKind.Door,
                    destinationId,
                    "anchor:ux1-aj-exit",
                    180,
                    true,
                    ["portal.travel"])
                : null,
            [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DoorWorldProfile.CollisionProfileOverrideKey] = "collision:ux1-aj-gate",
                ["material.preserve"] = "bronze",
            },
            navigation
                ? new DoorWorldProfile.NavigationPolicy(
                    $"nav-obstacle:{profileId:N}",
                    $"nav-link:{profileId:N}",
                    ["closed"],
                    ["open"],
                    .75,
                    .25,
                    true)
                : null);
        if (!rich)
        {
            return new DoorWorldProfile(
                profileId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                provenance,
                blueprintId,
                null,
                null,
                [],
                null,
                null,
                null,
                foundationPolicy);
        }

        var access = new DoorAccessEventProfile(
            StableDoorValidationIdUx1AJ(profileId, "access"),
            DoorAccessEventProfile.CurrentSchemaVersion,
            provenance,
            "assets/doors/ux1-aj-gate.glb",
            new TransformValue(
                new Vector3Value(3, 0, 5),
                QuaternionValue.Identity,
                Vector3Value.One),
            new DoorAccessEventProfile.DoorLockPolicy(
                StableDoorValidationIdUx1AJ(profileId, "lock"),
                DoorAccessLockState.Locked,
                ["key:ux1-aj-gate"],
                "mechanics",
                12,
                null,
                true,
                true,
                true,
                false,
                ["door.unlock"]),
            new DoorAccessEventProfile.DoorOpenRejectedPolicy(
                StableDoorValidationIdUx1AJ(profileId, "rejection"),
                "1.0.0",
                "DoorOpenRejected",
                [new("fr-FR", "La porte résiste.")],
                "audio:ux1-aj-locked",
                "animation:ux1-aj-rattle",
                "scripts/ux1-aj-rejected"),
            new DoorAccessEventProfile.DoorTrapPolicy(
                StableDoorValidationIdUx1AJ(profileId, "trap"),
                "door:open",
                "damage:ux1-aj",
                10,
                12,
                30,
                "guild:watch",
                "vfx:ux1-aj",
                "party",
                true,
                false,
                ["front"],
                ["interact"]),
            new DoorAccessEventProfile.DoorPortalUsePolicy(
                StableDoorValidationIdUx1AJ(profileId, "portal-use"),
                "1.0.0",
                "PortalUseRequested",
                "PortalUseCompleted"),
            new DoorAccessEventProfile.DoorStateEventPolicy(
                StableDoorValidationIdUx1AJ(profileId, "state-events"),
                "1.0.0",
                "DoorOpened",
                "DoorClosed"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["authoring.preserve"] = "true",
            });
        return new DoorWorldProfile(
            profileId,
            WorldFeatureCatalog.CurrentSchemaVersion,
            provenance,
            blueprintId,
            new(125, 8, false, "visual:damaged", "visual:breached", true, "asset:debris", "service:repair"),
            new("image:ux1-aj", "door.loading.title", "door.loading.tip", true, .5),
            [new("interact", "scripts/ux1-aj-door", false, true)],
            null,
            null,
            new DoorWorldProfile.AssemblyPolicy(
                DoorWorldProfile.AssemblyKind.Single,
                [new(StableDoorValidationIdUx1AJ(profileId, "panel"), "leaf", 1, 0, false)],
                "leaf",
                false,
                "return-closed"),
            foundationPolicy,
            access,
            true,
            null,
            false);
    }

    private static StudioDoorValidationDraft ConfigureUx1AJSmokeDraft(StudioDoorValidationDraft draft)
    {
        var profile = draft.OriginalProfile;
        var foundation = profile.Foundation!;
        var access = profile.AccessEvents;
        foundation.Overrides.TryGetValue(
            DoorWorldProfile.CollisionProfileOverrideKey,
            out var collisionProfileId);
        return draft with
        {
            ValidationEnabled = true,
            ValidationRequiredAtRuntime = true,
            KnownDestinationIds = foundation.Portal is { } portal ? [portal.DestinationId] : [],
            KnownAnimationIds = foundation.States.Select(item => item.AnimationId)
                .Concat(access is null ? [] : new[] { access.Rejection.AnimationId })
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            KnownCollisionProfileIds = string.IsNullOrWhiteSpace(collisionProfileId)
                ? []
                : [collisionProfileId],
            KnownCredentialIds = access?.Lock.CredentialIds.Distinct(StringComparer.Ordinal).ToArray() ?? [],
            KnownScriptIds = profile.Events.Select(item => item.ScriptId)
                .Concat(access is null ? [] : new[] { access.Rejection.ScriptId })
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static bool ApplyUx1AJProfileMutation(
        StudioSessionController session,
        Guid profileId,
        Func<DoorWorldProfile, DoorWorldProfile> mutate)
    {
        var history = session.History;
        var project = session.Project;
        if (history is null || project is null)
        {
            return false;
        }
        var catalog = project.Module.WorldFeatures;
        if (catalog.Doors.Count(item => item.Id == profileId) != 1)
        {
            return false;
        }
        var doors = catalog.Doors.Select(item => item.Id == profileId ? mutate(item) : item).ToArray();
        var command = new SetWorldFeatureCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            history.State.Revision,
            new WorldFeatureCatalog(
                catalog.Id,
                catalog.SchemaVersion,
                catalog.Provenance,
                catalog.Placeables,
                doors,
                catalog.Triggers,
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

    private static bool HasDoorValidationCodeUx1AJ(StudioDoorValidationPreview preview, string code) =>
        !preview.Succeeded && preview.Groups
            .SelectMany(item => item.Diagnostics)
            .Any(item => string.Equals(item.Code, code, StringComparison.Ordinal));

    private static bool SameDoorValidationUx1AJ(
        StudioDoorValidationDraft expected,
        DoorWorldProfile.RuntimeValidationPolicy actual) =>
        expected.KnownDestinationIds.SequenceEqual(actual.KnownDestinationIds) &&
        expected.KnownAnimationIds.SequenceEqual(actual.KnownAnimationIds) &&
        expected.KnownCollisionProfileIds.SequenceEqual(actual.KnownCollisionProfileIds) &&
        expected.KnownCredentialIds.SequenceEqual(actual.KnownCredentialIds) &&
        expected.KnownScriptIds.SequenceEqual(actual.KnownScriptIds);

    private static bool SameDoorCatalogReferencesUx1AJ<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual) where T : class =>
        expected.Count == actual.Count && expected.Zip(actual).All(pair =>
            ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanDoorValidationTextUx1AJ(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/worldFeatures", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase);

    private static void DeleteUx1AJTemporaryRoot(string root, string requiredPrefix)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var tempBase = Path.GetFullPath(Path.GetTempPath());
            if (fullRoot.StartsWith(tempBase, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(fullRoot).StartsWith(requiredPrefix, StringComparison.Ordinal) &&
                Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, recursive: true);
            }
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AJ_CLEANUP_FAILED " + exception.Message);
        }
    }

    private static StudioUx1AJSmokeResult FailedUx1AJSmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);

    internal StudioUx1AJSmokeResult RunUx1AJContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1aj-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var project = CreateUx1AJFixtureProject(
                Guid.Parse("a1200000-0000-4000-8000-000000000001"),
                Ux1AJEvidenceProvenance);
            var created = ProjectSession.Create(tempRoot, project);
            var manifestSaved = created.Succeeded && AssetPackageManifestStore.Save(
                tempRoot,
                [CreateUx1AJFixtureManifest(project.Id.Value, Ux1AJEvidenceProvenance)]).Succeeded;
            if (!manifestSaved || !editing.Open(tempRoot).Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AJ_CONTRACT_FAILED stage=create-open");
                return FailedUx1AJSmoke();
            }

            var initialProject = editing.Project!;
            var initialCatalog = initialProject.Module.WorldFeatures;
            var target = initialCatalog.Doors.Single(item =>
                initialProject.Blueprints.Single(blueprint => blueprint.Id == item.BlueprintId).Slug ==
                "porte-validation-canonique");
            var neighbor = initialCatalog.Doors.Single(item =>
                initialProject.Blueprints.Single(blueprint => blueprint.Id == item.BlueprintId).Slug ==
                "porte-voisine-preservee");
            var foundationless = initialCatalog.Doors.Single(item => item.Foundation is null);
            var navigationlessCandidate = target with
            {
                Foundation = target.Foundation! with { Navigation = null },
            };

            var explicitSelection = editing.CreateDoorValidationDraftUx1AJ() is null &&
                                    editing.DoorValidationChoicesUx1AJ.Count == 3 &&
                                    editing.DoorValidationChoicesUx1AJ.All(item =>
                                        IsHumanDoorValidationTextUx1AJ(item.Label));
            var prerequisitesRequired =
                editing.CreateDoorValidationDraftUx1AJ(foundationless.Id) is null &&
                !HasDoorValidationPrerequisitesUx1AJ(navigationlessCandidate) &&
                editing.DoorValidationChoicesUx1AJ.Single(item => item.ProfileId == foundationless.Id) is
                    { HasFoundation: false };

            var suggestions = editing.GetDoorValidationSuggestionsUx1AJ(target.Id);
            var authoredSuggestion = suggestions.AuthoredCollision;
            var targetIdText = target.Id.ToString("D");
            var neighborIdText = neighbor.Id.ToString("D");
            var allSuggestionTexts = suggestions.Destinations.SelectMany(item => new[] { item.Label, item.Provenance })
                .Concat(suggestions.Animations.SelectMany(item => new[] { item.Label, item.Provenance }))
                .Concat(suggestions.CollisionProfiles.SelectMany(item => new[] { item.Label, item.Provenance }))
                .Concat(suggestions.Credentials.SelectMany(item => new[] { item.Label, item.Provenance }))
                .Concat(suggestions.Scripts.SelectMany(item => new[] { item.Label, item.Provenance }))
                .Concat(authoredSuggestion is null
                    ? []
                    : new[] { authoredSuggestion.Label, authoredSuggestion.Provenance })
                .ToArray();
            var humanSuggestions = suggestions.Destinations.Count > 0 &&
                                   suggestions.Animations.Count > 0 &&
                                   suggestions.CollisionProfiles.Count > 0 &&
                                   suggestions.Credentials.Count > 0 &&
                                   suggestions.Scripts.Count > 0 &&
                                   authoredSuggestion is not null &&
                                   suggestions.CollisionProfiles.Any(item =>
                                       string.Equals(item.Value, authoredSuggestion.ProfileId,
                                           StringComparison.Ordinal) &&
                                       item.Provenance.Contains("Manifeste asset", StringComparison.Ordinal)) &&
                                   authoredSuggestion.Provenance.Contains(
                                       "Override canonique",
                                       StringComparison.Ordinal) &&
                                   allSuggestionTexts.All(IsHumanDoorValidationTextUx1AJ) &&
                                   allSuggestionTexts.All(item =>
                                       !item.Contains(targetIdText, StringComparison.OrdinalIgnoreCase) &&
                                       !item.Contains(neighborIdText, StringComparison.OrdinalIgnoreCase));
            var protectedDestinations = suggestions.Destinations.Any(item => item.Protected &&
                item.Id == target.Foundation!.Portal!.DestinationId &&
                item.Label.Contains("Porte", StringComparison.OrdinalIgnoreCase));

            var draft = ConfigureUx1AJSmokeDraft(editing.CreateDoorValidationDraftUx1AJ(target.Id)!);
            var requiredAbsent = editing.PreviewDoorValidationUx1AJ(draft with
            {
                ValidationEnabled = false,
                ValidationRequiredAtRuntime = true,
            });
            var destinationMissing = editing.PreviewDoorValidationUx1AJ(draft with
                { KnownDestinationIds = [] });
            var animationMissing = editing.PreviewDoorValidationUx1AJ(draft with
                { KnownAnimationIds = [] });
            var rejectionAnimation = target.AccessEvents!.Rejection.AnimationId;
            var rejectionAnimationMissing = editing.PreviewDoorValidationUx1AJ(draft with
            {
                KnownAnimationIds = draft.KnownAnimationIds
                    .Where(item => !string.Equals(item, rejectionAnimation, StringComparison.Ordinal)).ToArray(),
            });
            var collisionMismatch = editing.PreviewDoorValidationUx1AJ(draft with
                { KnownCollisionProfileIds = ["collision:autre-profil"] });
            var collisionMissing = editing.PreviewDoorValidationUx1AJ(draft with
                { KnownCollisionProfileIds = [] });
            var credentialMissing = editing.PreviewDoorValidationUx1AJ(draft with
                { KnownCredentialIds = [] });
            var scriptMissing = editing.PreviewDoorValidationUx1AJ(draft with
                { KnownScriptIds = [] });

            var authoredCollisionRemoved = ApplyUx1AJProfileMutation(
                editing,
                target.Id,
                profile => profile with
                {
                    Foundation = profile.Foundation! with
                    {
                        Overrides = profile.Foundation!.Overrides
                            .Where(item => !string.Equals(
                                item.Key,
                                DoorWorldProfile.CollisionProfileOverrideKey,
                                StringComparison.Ordinal))
                            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
                    },
                });
            var collisionAuthoredMissing = authoredCollisionRemoved
                ? editing.PreviewDoorValidationUx1AJ(ConfigureUx1AJSmokeDraft(
                    editing.CreateDoorValidationDraftUx1AJ(target.Id)!))
                : FailedDoorValidationPreviewUx1AJ("Mutation de preuve impossible.");
            var collisionAuthoredMissingBlocked = authoredCollisionRemoved &&
                                                   HasDoorValidationCodeUx1AJ(
                                                       collisionAuthoredMissing,
                                                       "RPG_DOOR_COLLISION_PROFILE_MISSING") &&
                                                   editing.Undo().Succeeded;

            var orphanApplied = ApplyUx1AJProfileMutation(
                editing,
                target.Id,
                profile => profile with
                {
                    Foundation = profile.Foundation! with
                    {
                        Links = [new(
                            StableDoorValidationIdUx1AJ(profile.Id, "orphan-link"),
                            StableDoorValidationIdUx1AJ(profile.Id, "unknown-door"),
                            true,
                            string.Empty)],
                    },
                });
            var orphanPreview = orphanApplied
                ? editing.PreviewDoorValidationUx1AJ(ConfigureUx1AJSmokeDraft(
                    editing.CreateDoorValidationDraftUx1AJ(target.Id)!))
                : FailedDoorValidationPreviewUx1AJ("Mutation de preuve impossible.");
            var orphanLinkBlocked = orphanApplied &&
                                    HasDoorValidationCodeUx1AJ(orphanPreview, "RPG_DOOR_LINK_ORPHAN") &&
                                    editing.Undo().Succeeded;

            var reverseApplied = ApplyUx1AJProfileMutation(
                editing,
                target.Id,
                profile => profile with
                {
                    Foundation = profile.Foundation! with
                    {
                        Links = [new(
                            StableDoorValidationIdUx1AJ(profile.Id, "two-way-link"),
                            neighbor.Id,
                            false,
                            "Retour vers la porte validée")],
                    },
                });
            var reversePreview = reverseApplied
                ? editing.PreviewDoorValidationUx1AJ(ConfigureUx1AJSmokeDraft(
                    editing.CreateDoorValidationDraftUx1AJ(target.Id)!))
                : FailedDoorValidationPreviewUx1AJ("Mutation de preuve impossible.");
            var reverseLinkBlocked = reverseApplied &&
                                     HasDoorValidationCodeUx1AJ(
                                         reversePreview,
                                         "RPG_DOOR_LINK_REVERSE_MISSING") &&
                                     editing.Undo().Succeeded;

            var optionalDraft = editing.CreateDoorValidationDraftUx1AJ(target.Id)! with
            {
                ValidationEnabled = false,
                ValidationRequiredAtRuntime = false,
            };
            var optionalPreview = editing.PreviewDoorValidationUx1AJ(optionalDraft);
            var optionalNotEvaluated = optionalPreview.Succeeded && optionalPreview.CanApply &&
                                       optionalPreview.OptionalFallback && optionalPreview.Groups.Count == 7 &&
                                       optionalPreview.Groups.Where(item => item.Key != "prerequisites")
                                           .All(item => item.State == "Non évalué" && !item.Blocking);

            var staleDraft = ConfigureUx1AJSmokeDraft(editing.CreateDoorValidationDraftUx1AJ(target.Id)!);
            var staleDraftMutation = ApplyUx1AJProfileMutation(
                editing,
                neighbor.Id,
                profile => profile with { Provenance = "studio:ux1-aj:neighbor-stale-draft" });
            var staleDraftPreview = editing.PreviewDoorValidationUx1AJ(staleDraft);
            var staleDraftBlocked = staleDraftMutation && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé", StringComparison.Ordinal) &&
                                    editing.Undo().Succeeded;

            draft = ConfigureUx1AJSmokeDraft(editing.CreateDoorValidationDraftUx1AJ(target.Id)!);
            var historyBeforePreview = editing.History!;
            var projectBeforePreview = editing.Project;
            var revisionBeforePreview = historyBeforePreview.State.Revision;
            var journalBeforePreview = historyBeforePreview.Journal.ToArray();
            var undoBeforePreview = historyBeforePreview.CanUndo;
            var redoBeforePreview = historyBeforePreview.CanRedo;
            var dirtyBeforePreview = editing.IsDirty;
            var dryPreview = editing.PreviewDoorValidationUx1AJ(draft);
            var dryRun = dryPreview.Succeeded && dryPreview.CanApply &&
                         ReferenceEquals(projectBeforePreview, editing.Project) &&
                         ReferenceEquals(historyBeforePreview, editing.History) &&
                         historyBeforePreview.State.Revision == revisionBeforePreview &&
                         historyBeforePreview.Journal.SequenceEqual(journalBeforePreview) &&
                         historyBeforePreview.CanUndo == undoBeforePreview &&
                         historyBeforePreview.CanRedo == redoBeforePreview &&
                         editing.IsDirty == dirtyBeforePreview;

            var mutableScripts = draft.KnownScriptIds as string[];
            var originalScript = mutableScripts is { Length: > 0 } ? mutableScripts[0] : null;
            if (mutableScripts is { Length: > 0 })
            {
                mutableScripts[0] += ":stale-fingerprint";
            }
            var staleFingerprintApply = editing.ApplyApprovedDoorValidationUx1AJ();
            if (mutableScripts is { Length: > 0 } && originalScript is not null)
            {
                mutableScripts[0] = originalScript;
            }
            var staleFingerprintBlocked = mutableScripts is { Length: > 0 } &&
                                          !staleFingerprintApply.Succeeded &&
                                          staleFingerprintApply.Message.Contains(
                                              "Prévisualisez",
                                              StringComparison.Ordinal);
            var approvalPreview = editing.PreviewDoorValidationUx1AJ(draft);
            var staleApprovalMutation = ApplyUx1AJProfileMutation(
                editing,
                neighbor.Id,
                profile => profile with { Provenance = "studio:ux1-aj:neighbor-stale-approval" });
            var staleApply = editing.ApplyApprovedDoorValidationUx1AJ();
            var staleApprovalBlocked = staleFingerprintBlocked && approvalPreview.Succeeded &&
                                       staleApprovalMutation && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var freshDraft = ConfigureUx1AJSmokeDraft(editing.CreateDoorValidationDraftUx1AJ(target.Id)!);
            var freshPreview = editing.PreviewDoorValidationUx1AJ(freshDraft);
            var applied = editing.ApplyApprovedDoorValidationUx1AJ();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Doors.Single(item => item.Id == target.Id);
            var appliedNeighbor = appliedCatalog.Doors.Single(item => item.Id == neighbor.Id);
            if (!freshPreview.Succeeded || !applied.Succeeded || appliedTarget.Validation is null)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AJ_CONTRACT_FAILED stage=fresh-apply");
                return FailedUx1AJSmoke();
            }

            var doorNeighborsPreserved = target.Id == appliedTarget.Id &&
                                         target.SchemaVersion == appliedTarget.SchemaVersion &&
                                         string.Equals(
                                             target.Provenance,
                                             appliedTarget.Provenance,
                                             StringComparison.Ordinal) &&
                                         target.BlueprintId == appliedTarget.BlueprintId &&
                                         ReferenceEquals(target.Damage, appliedTarget.Damage) &&
                                         ReferenceEquals(target.Loading, appliedTarget.Loading) &&
                                         ReferenceEquals(target.Events, appliedTarget.Events) &&
                                         target.DialogueId == appliedTarget.DialogueId &&
                                         target.FactionId == appliedTarget.FactionId &&
                                         ReferenceEquals(target.Assembly, appliedTarget.Assembly) &&
                                         ReferenceEquals(target.Foundation, appliedTarget.Foundation) &&
                                         ReferenceEquals(target.AccessEvents, appliedTarget.AccessEvents) &&
                                         target.AccessEventsRequiredAtRuntime ==
                                         appliedTarget.AccessEventsRequiredAtRuntime;
            var catalogNeighborsPreserved = ReferenceEquals(neighbor, appliedNeighbor) &&
                                            initialCatalog.Doors.Select(item => item.Id)
                                                .SequenceEqual(appliedCatalog.Doors.Select(item => item.Id)) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialCatalog.Placeables,
                                                appliedCatalog.Placeables) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialCatalog.Triggers,
                                                appliedCatalog.Triggers) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialCatalog.Encounters,
                                                appliedCatalog.Encounters) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialCatalog.VehicleRoutes,
                                                appliedCatalog.VehicleRoutes) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialCatalog.Merchants,
                                                appliedCatalog.Merchants) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialCatalog.Companions,
                                                appliedCatalog.Companions) &&
                                            ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialProject.Blueprints,
                                                appliedProject.Blueprints) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialProject.Areas,
                                                appliedProject.Areas) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialProject.Dialogues,
                                                appliedProject.Dialogues) &&
                                            SameDoorCatalogReferencesUx1AJ(
                                                initialProject.Quests,
                                                appliedProject.Quests);

            var undo = editing.Undo();
            var undoneTarget = editing.Project!.Module.WorldFeatures.Doors.Single(item => item.Id == target.Id);
            var redo = editing.Redo();
            var redoneTarget = editing.Project!.Module.WorldFeatures.Doors.Single(item => item.Id == target.Id);
            var undoRedo = undo.Succeeded && undoneTarget.Validation is null &&
                           !undoneTarget.ValidationRequiredAtRuntime && redo.Succeeded &&
                           redoneTarget.Validation is not null &&
                           SameDoorValidationUx1AJ(freshDraft, redoneTarget.Validation) &&
                           redoneTarget.ValidationRequiredAtRuntime;

            var serializedApplied = ProjectJsonSerializer.Serialize(editing.Project!);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistence = opened &&
                              string.Equals(
                                  serializedApplied,
                                  ProjectJsonSerializer.Serialize(reopened.Project!),
                                  StringComparison.Ordinal) &&
                              reopened.Project!.Module.WorldFeatures.Doors.Single(item => item.Id == target.Id)
                                  .Validation is { } persistedValidation &&
                              SameDoorValidationUx1AJ(freshDraft, persistedValidation);
            reopened.Close();

            var suppressionDraft = editing.CreateDoorValidationDraftUx1AJ(target.Id)! with
            {
                ValidationEnabled = false,
                ValidationRequiredAtRuntime = false,
            };
            var suppressionPreview = editing.PreviewDoorValidationUx1AJ(suppressionDraft);
            var suppressionApply = editing.ApplyApprovedDoorValidationUx1AJ();
            var suppressed = editing.Project!.Module.WorldFeatures.Doors.Single(item => item.Id == target.Id);
            var suppressionUndo = editing.Undo();
            var restored = editing.Project!.Module.WorldFeatures.Doors.Single(item => item.Id == target.Id);
            var suppressionRedo = editing.Redo();
            var suppressedAgain = editing.Project!.Module.WorldFeatures.Doors.Single(item => item.Id == target.Id);
            var suppressionSaved = editing.Save();
            var suppressionOpened = suppressionSaved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var suppressedPersisted = suppressionOpened &&
                                      reopened.Project!.Module.WorldFeatures.Doors.Single(item =>
                                          item.Id == target.Id) is
                                          { Validation: null, ValidationRequiredAtRuntime: false };
            reopened.Close();
            var optionalSuppression = suppressionPreview.Succeeded && suppressionApply.Succeeded &&
                                      suppressed is { Validation: null, ValidationRequiredAtRuntime: false } &&
                                      suppressionUndo.Succeeded && restored.Validation is not null &&
                                      suppressionRedo.Succeeded &&
                                      suppressedAgain is { Validation: null, ValidationRequiredAtRuntime: false } &&
                                      suppressedPersisted;

            var canonicalCommand = editing.History!.Journal.Count > 0 &&
                                   editing.History.Journal.All(item =>
                                       item.CommandName == "set_world_feature_catalog");
            var requiredAbsentBlocked = HasDoorValidationCodeUx1AJ(
                requiredAbsent,
                "world.door.validation_required_missing");
            var destinationMissingBlocked = HasDoorValidationCodeUx1AJ(
                destinationMissing,
                "RPG_DOOR_DESTINATION_MISSING");
            var animationMissingBlocked = HasDoorValidationCodeUx1AJ(
                animationMissing,
                "RPG_DOOR_ANIMATION_MISSING");
            var rejectionAnimationBlocked = HasDoorValidationCodeUx1AJ(
                rejectionAnimationMissing,
                "RPG_DOOR_ANIMATION_MISSING");
            var collisionMismatchBlocked = HasDoorValidationCodeUx1AJ(
                collisionMismatch,
                "RPG_DOOR_COLLISION_PROFILE_MISMATCH");
            var collisionMissingBlocked = HasDoorValidationCodeUx1AJ(
                collisionMissing,
                "RPG_DOOR_COLLISION_PROFILE_MISMATCH");
            var credentialMissingBlocked = HasDoorValidationCodeUx1AJ(
                credentialMissing,
                "RPG_DOOR_CREDENTIAL_MISSING");
            var scriptMissingBlocked = HasDoorValidationCodeUx1AJ(
                scriptMissing,
                "RPG_DOOR_SCRIPT_MISSING");
            var diagnosticPreviews = new[]
            {
                requiredAbsent,
                destinationMissing,
                animationMissing,
                rejectionAnimationMissing,
                collisionAuthoredMissing,
                collisionMismatch,
                credentialMissing,
                scriptMissing,
                orphanPreview,
                reversePreview,
            };
            var structuredHumanDiagnostics = diagnosticPreviews
                .SelectMany(item => item.Groups)
                .Where(item => item.Blocking)
                .All(group => group.Diagnostics.Count == group.Details.Count &&
                              group.Diagnostics.Count > 0 &&
                              group.Diagnostics.All(item =>
                                  !string.IsNullOrWhiteSpace(item.Code) &&
                                  !string.IsNullOrWhiteSpace(item.Path) &&
                                  IsHumanDoorValidationTextUx1AJ(item.Message))) &&
                diagnosticPreviews.Select(item => item.Message).All(IsHumanDoorValidationTextUx1AJ);

            var succeeded = explicitSelection && prerequisitesRequired && humanSuggestions &&
                            protectedDestinations && optionalNotEvaluated && requiredAbsentBlocked &&
                            destinationMissingBlocked && animationMissingBlocked &&
                            rejectionAnimationBlocked && collisionAuthoredMissingBlocked &&
                            collisionMismatchBlocked && collisionMissingBlocked &&
                            credentialMissingBlocked && scriptMissingBlocked && orphanLinkBlocked &&
                            reverseLinkBlocked && dryRun && staleDraftBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && optionalSuppression && persistence &&
                            doorNeighborsPreserved && catalogNeighborsPreserved &&
                            structuredHumanDiagnostics;
            return new StudioUx1AJSmokeResult(
                succeeded,
                explicitSelection,
                prerequisitesRequired,
                humanSuggestions,
                protectedDestinations,
                optionalNotEvaluated,
                requiredAbsentBlocked,
                destinationMissingBlocked,
                animationMissingBlocked,
                rejectionAnimationBlocked,
                collisionAuthoredMissingBlocked,
                collisionMismatchBlocked,
                collisionMissingBlocked,
                credentialMissingBlocked,
                scriptMissingBlocked,
                orphanLinkBlocked,
                reverseLinkBlocked,
                dryRun,
                staleDraftBlocked,
                staleApprovalBlocked,
                canonicalCommand,
                undoRedo,
                optionalSuppression,
                persistence,
                doorNeighborsPreserved,
                catalogNeighborsPreserved,
                structuredHumanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AJ_CONTRACT_FAILED stage=exception " + exception);
            return FailedUx1AJSmoke();
        }
        finally
        {
            reopened.Close();
            editing.Close();
            DeleteUx1AJTemporaryRoot(tempRoot, "openrpg-forge-studio-ux1aj-");
        }
    }
}
