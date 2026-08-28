using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Items;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static bool ValidPlaceableTransformDraft(StudioPlaceableDraft draft)
    {
        var bounded = new[]
        {
            draft.Position.X, draft.Position.Y, draft.Position.Z,
            draft.RotationDegrees.X, draft.RotationDegrees.Y, draft.RotationDegrees.Z,
            draft.Pivot.X, draft.Pivot.Y, draft.Pivot.Z,
        };
        var scales = new[] { draft.Scale.X, draft.Scale.Y, draft.Scale.Z };
        return bounded.All(value => float.IsFinite(value) && Math.Abs(value) <= 1_000_000) &&
               scales.All(value => float.IsFinite(value) && value is >= .01f and <= 100) &&
               double.IsFinite(draft.SnapIncrement) && draft.SnapIncrement is >= 0 and <= 10_000 &&
               (string.IsNullOrWhiteSpace(draft.TransformAnimationId) ||
                draft.TransformAnimationId.Trim().Length <= 256);
    }

    private static bool WouldCreatePlaceableParentCycle(
        IReadOnlyList<PlaceableWorldProfile> placeables,
        Guid profileId,
        Guid? parentId)
    {
        var parents = placeables.ToDictionary(
            candidate => candidate.Id,
            candidate => candidate.Id == profileId
                ? parentId
                : candidate.Foundation?.Transform.ParentId);
        var visited = new HashSet<Guid> { profileId };
        var current = parentId;
        while (current is { } candidateId)
        {
            if (!visited.Add(candidateId))
            {
                return true;
            }
            current = parents.GetValueOrDefault(candidateId);
        }
        return false;
    }

    private static PlaceableVisualStateMachineProfile CreateContainerCompatibilityStateMachine(
        BlueprintId blueprintId)
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" }
            .Select(state => new PlaceableVisualStateProfile(
                StablePlaceableId(blueprintId, $"container-state-{state}"),
                state,
                $"animation:{state}",
                $"visual:{state}",
                state != "broken"))
            .ToArray();
        return new(
            StablePlaceableId(blueprintId, "container-state-machine"),
            "closed",
            states,
            []);
    }

    private static StudioPlaceableStateMachineDraft CreatePlaceableStateMachineDraft(
        BlueprintId blueprintId,
        string initialStateId)
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" }
            .Select(state => new StudioPlaceableVisualStateDraft(
                StablePlaceableId(blueprintId, $"visual-state-{state}"),
                state,
                $"animation:{state}",
                $"visual:{state}",
                state != "broken"))
            .ToArray();
        return new(
            StablePlaceableId(blueprintId, "visual-state-machine"),
            initialStateId,
            states,
            [],
            false);
    }

    private static PlaceableProtectionProfile CreateContainerCompatibilityProtection(BlueprintId blueprintId) =>
        new(
            StablePlaceableId(blueprintId, "container-protection"),
            ["container.access"],
            ["placeable.operations.override"],
            "Accès autorisé par la politique du conteneur.",
            true,
            "PlaceableProtectionOverride");

    private static PlaceableWorldProfile CreatePlaceableTransformParent(
        BlueprintDefinition blueprint,
        Guid profileId,
        string provenance)
    {
        var assetId = $"asset:{blueprint.Slug}";
        var foundation = new PlaceableWorldProfile.FoundationPolicy(
            StablePlaceableId(blueprint.Id, "transform-parent-foundation"),
            WorldFeatureCatalog.CurrentSchemaVersion,
            provenance,
            blueprint.Slug,
            [new("fr-FR", blueprint.DisplayName, "Observer")],
            ["ancrage", "parent"],
            "Ancrage canonique de transformation.",
            new(assetId, [$"mesh:{blueprint.Slug}"], [], assetId, assetId, []),
            new("box", Vector3Value.One, true, true, "world"),
            null,
            new(true, true, false, false, true),
            new(false, false, false, false, false, new Dictionary<string, string>(StringComparer.Ordinal)),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, 0, null, true, null),
            new Dictionary<string, string>(StringComparer.Ordinal));
        return new(
            profileId,
            WorldFeatureCatalog.CurrentSchemaVersion,
            provenance,
            blueprint.Id,
            null,
            null,
            null,
            new([], new(false, 1, 1, true, "placeable-parent")),
            null,
            foundation,
            true);
    }

    private static IReadOnlyList<string> SplitValues(string text) => text
        .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static bool TryParsePairs(
        string text,
        string noun,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in text.Split([';', '\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                values = parsed;
                message = $"Chaque {noun} doit suivre la forme nom=valeur.";
                return false;
            }
            var key = token[..separator].Trim();
            var value = token[(separator + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || !parsed.TryAdd(key, value))
            {
                values = parsed;
                message = $"Chaque {noun} doit avoir un nom unique et une valeur.";
                return false;
            }
        }
        values = parsed;
        message = string.Empty;
        return true;
    }

    private static string FormatPairs(IReadOnlyDictionary<string, string>? values) => values is null
        ? string.Empty
        : string.Join("; ", values.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value}"));

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string PlaceableVerbLabelKey(string slug, StudioPlaceableVerbDraft verb)
    {
        if (!string.IsNullOrWhiteSpace(verb.OriginalLabelKey) &&
            string.Equals(verb.Label.Trim(), verb.OriginalLabel?.Trim(), StringComparison.Ordinal))
        {
            return verb.OriginalLabelKey;
        }
        var decomposed = verb.Label.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var separator = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                if (separator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(character));
                separator = false;
            }
            else
            {
                separator = true;
            }
        }
        var key = builder.Length == 0 ? "action" : builder.ToString();
        return $"{slug}.verb.{key}";
    }

    private static string HumanizePlaceableVerb(string labelKey)
    {
        var token = labelKey.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "action";
        var words = token.Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(words);
    }

    private static string HumanizeContainerOwnership(string value) => value.Trim().ToLowerInvariant() switch
    {
        "party" => "groupe",
        "owner" => "propriétaire de l’objet",
        "world" => "monde partagé",
        "gm" => "maître de jeu",
        _ => value.Trim(),
    };

    private static string HumanizeContainerAccess(IReadOnlyList<string> permissions)
    {
        var labels = permissions.Select(permission => permission.Trim() switch
        {
            "container.open" => "ouvrir",
            "container.deposit" => "déposer",
            "container.withdraw" => "retirer",
            var other => other,
        });
        return string.Join(" · ", labels);
    }

    private static string HumanizePlaceableVisualState(string stateId) => stateId.Trim() switch
    {
        "idle" => "Au repos",
        "open" => "Ouvert",
        "closed" => "Fermé",
        "broken" => "Brisé",
        "active" => "Actif",
        "custom" => "Personnalisé",
        var other => HumanizePlaceableVerb(other),
    };

    private static Guid StablePlaceableId(BlueprintId blueprintId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:studio:ux1-e:{blueprintId.Value:D}:{purpose}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string HumanizeAssetManifest(AssetPackageManifest manifest)
    {
        var variant = manifest.ModelMetadata?.Variants.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(candidate.DisplayName))?.DisplayName;
        if (!string.IsNullOrWhiteSpace(variant))
        {
            return variant.Trim();
        }
        var modelPath = manifest.Chunks.First(chunk => chunk.Role == AssetChunkRole.Model).RelativePath;
        var folder = Path.GetFileName(Path.GetDirectoryName(modelPath)) ?? "modèle 3D";
        var prefixes = new[]
        {
            "kenney-fantasy-town-kit-", "kenney-castle-kit-", "kenney-nature-kit-",
            "kenney-mini-forest-", "kaykit-dungeon-remastered-1-0-",
            "kaykit-character-pack-adventures-1-0-", "polyhaven-",
        };
        foreach (var prefix in prefixes)
        {
            if (folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                folder = folder[prefix.Length..];
                break;
            }
        }
        var words = folder.Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(words);
    }

    private static string HumanizePlaceable(string? code) => code switch
    {
        "world.placeable.identity_invalid" => "Le nom court et le nom visible du blueprint sont requis.",
        "world.placeable.visual_invalid" => "Choisissez un visuel 3D complet pour ce blueprint.",
        "world.placeable.metadata_invalid" => "Les tags et surcharges doivent être uniques et non vides.",
        "world.placeable.collision_invalid" => "La collision active exige une taille positive et cohérente avec la navigation.",
        "world.placeable.role_invalid" => "Choisissez exactement un rôle : décor statique ou objet utilisable.",
        "world.placeable.static_invalid" => "Un décor doit être regroupé ou instancié ; la fusion exclut une collision par instance.",
        "world.placeable.interaction_invalid" => "L’interaction exige une portée, un angle, une durée et une action valides.",
        "world.placeable.on_used_invalid" => "L’événement d’utilisation exige une action utilisable et un contrat de script valide.",
        "world.placeable.on_used_required_missing" => "L’événement d’utilisation est requis au runtime mais il est désactivé.",
        "world.placeable.container_invalid" =>
            "Le conteneur exige des limites, un propriétaire, des accès et trois références complètes.",
        "world.placeable.container_required_missing" =>
            "Le composant conteneur est actif mais son profil canonique est absent.",
        "world.placeable.operations_required_missing" or "WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING" =>
            "Le profil d’opérations est requis au runtime mais il est absent.",
        "world.placeable.operations_identity_conflict" =>
            "Les identités internes du conteneur entrent en conflit avec une autre capacité.",
        "world.placeable.lock_invalid" =>
            "Le verrou exige un état et au moins une clé, compétence, code, méthode de piratage ou permission valide.",
        "world.placeable.lock_required_missing" or "WORLD_PLACEABLE_LOCK_REQUIRED_MISSING" =>
            "Le verrou est requis au runtime mais son profil canonique est absent.",
        "world.placeable.credential_invalid" or "WORLD_PLACEABLE_CREDENTIAL_INVALID" =>
            "Le justificatif exige une identité et une politique de charges valides.",
        "world.placeable.credential_item_missing" =>
            "L’objet clé choisi pour le justificatif n’existe plus dans le projet.",
        "world.placeable.credential_virtual_permission_missing" =>
            "La politique virtuelle exige une permission nommée.",
        "world.placeable.credential_required_missing" or
            "WORLD_PLACEABLE_CREDENTIAL_REQUIRED_MISSING" =>
            "La politique de justificatif est requise au runtime mais elle est absente.",
        "world.placeable.trap_invalid" or "WORLD_PLACEABLE_TRAP_MISSING" =>
            "Le piège exige un déclencheur, un effet, deux difficultés, un délai, un propriétaire, un VFX et une politique alliée valides.",
        "world.placeable.trap_component_missing" =>
            "Le profil du piège exige que le composant Piège soit actif.",
        "world.placeable.trap_required_missing" or "WORLD_PLACEABLE_TRAP_REQUIRED_MISSING" =>
            "Le piège est requis au runtime mais son profil canonique est absent.",
        "world.placeable.loot_spawner_invalid" or "WORLD_PLACEABLE_LOOT_MISSING" =>
            "Le trésor initial exige une table, un moment, une graine, des objets et un état généré valides.",
        "world.placeable.loot_item_reference_missing" =>
            "Un objet choisi pour le trésor initial n’existe plus dans le projet.",
        "world.placeable.loot_required_missing" or "WORLD_PLACEABLE_LOOT_REQUIRED_MISSING" =>
            "Le trésor initial est requis au runtime mais son profil canonique est absent.",
        "world.placeable.container_events_invalid" or "WORLD_PLACEABLE_CONTAINER_EVENTS_MISSING" =>
            "Les événements d’ouverture et de fermeture exigent deux liaisons et leur contexte complet.",
        "world.placeable.container_events_required_missing" or
            "WORLD_PLACEABLE_CONTAINER_EVENTS_REQUIRED_MISSING" =>
            "Les événements d’ouverture et de fermeture sont requis au runtime mais leur profil canonique est absent.",
        "world.placeable.contents_changed_invalid" or "WORLD_PLACEABLE_CONTENTS_CHANGED_MISSING" =>
            "L’événement de contenu modifié exige un delta transactionnel, une cause, des révisions et un acteur.",
        "world.placeable.contents_changed_required_missing" or
            "WORLD_PLACEABLE_CONTENTS_CHANGED_REQUIRED_MISSING" =>
            "L’événement de contenu modifié est requis au runtime mais son profil canonique est absent.",
        "world.placeable.damage_events_invalid" or "WORLD_PLACEABLE_DAMAGE_EVENTS_MISSING" =>
            "Les événements de dégâts et de mort exigent deux liaisons et un contexte uniforme.",
        "world.placeable.damage_events_damageable_missing" =>
            "Les événements de dégâts et de mort exigent d’activer la santé du placeable.",
        "world.placeable.damage_events_authority_diverged" or
            "WORLD_PLACEABLE_DAMAGE_EVENTS_AUTHORITY_DIVERGED" =>
            "Les définitions directe et composée des dégâts divergent : alignez leur identité et leurs valeurs.",
        "world.placeable.damage_events_required_missing" or
            "WORLD_PLACEABLE_DAMAGE_EVENTS_REQUIRED_MISSING" =>
            "Les événements de dégâts et de mort sont requis au runtime mais leur profil canonique est absent.",
        "world.placeable.faction_ownership_identity_invalid" =>
            "Le rattachement de faction exige une identité interne stable.",
        "world.placeable.faction_memberships_invalid" =>
            "La faction principale doit appartenir à une liste de membres unique et non vide.",
        "world.placeable.faction_reference_missing" =>
            "Une faction choisie n’existe plus dans le catalogue du projet.",
        "world.placeable.faction_ownership_required_missing" or
            "WORLD_PLACEABLE_FACTION_OWNERSHIP_REQUIRED_MISSING" =>
            "Le rattachement de faction est requis au runtime mais il est absent.",
        "world.placeable.faction_ownership_identity_conflict" =>
            "L’identité interne du rattachement de faction entre en conflit avec une autre capacité.",
        "world.placeable.faction_ownership_uuid_duplicate" =>
            "Deux placeables partagent la même identité de rattachement de faction.",
        "world.placeable.conversation_identity_invalid" =>
            "La conversation exige une identité interne stable.",
        "world.placeable.conversation_bindings_empty" =>
            "Ajoutez au moins une liaison vers un dialogue.",
        "world.placeable.conversation_binding_uuid_invalid" or
            "world.placeable.conversation_binding_uuid_duplicate" =>
            "Les liaisons de conversation doivent être uniques.",
        "world.placeable.conversation_dialogue_missing" =>
            "Un dialogue choisi n’existe plus dans le projet.",
        "world.placeable.conversation_entry_point_missing" =>
            "Un point d’entrée choisi n’existe plus dans le dialogue.",
        "world.placeable.conversation_item_missing" =>
            "L’objet choisi pour déclencher la conversation n’existe plus.",
        "world.placeable.conversation_selector_invalid" =>
            "Renseignez un déclencheur lisible pour cette liaison de conversation.",
        "world.placeable.conversation_priority_invalid" =>
            "La priorité de conversation doit rester entre -1000 et 1000.",
        "world.placeable.conversation_required_missing" or
            "WORLD_PLACEABLE_CONVERSATION_REQUIRED_MISSING" =>
            "La conversation est requise au runtime mais son profil est absent.",
        "world.placeable.conversation_identity_conflict" or
            "world.placeable.conversation_uuid_duplicate" =>
            "L’identité interne de conversation entre en conflit avec une autre capacité.",
        "world.placeable.state_machine_invalid" =>
            "Les états visuels exigent un état initial, six apparences complètes et des transitions valides.",
        "world.placeable.state_machine_required_missing" =>
            "Le profil d’opérations exige une machine d’états visuels.",
        "world.placeable.inventory_reference_missing" =>
            "Le conteneur doit référencer un inventaire unique.",
        "world.placeable.transform_invalid" => "La position, l’échelle, le pivot ou le pas de grille sort des limites autorisées.",
        "world.placeable.parent_missing" => "Le parent choisi n’existe plus ou ne possède pas de transformation canonique.",
        "world.placeable.parent_cycle" => "Le parent choisi crée une boucle de transformation.",
        "world.placeable.animation_invalid" or "world.placeable.animation_reference_missing" =>
            "L’animation de transformation doit référencer une animation gouvernée du placeable.",
        "world.placeable.scripts_invalid" => "Chaque événement de script doit pointer vers une route non vide.",
        "world.placeable.foundation_required_missing" or "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING" =>
            "La fiche est requise au runtime mais elle est absente.",
        "world.reference.blueprint_missing" or "world.reference.blueprint_kind_mismatch" =>
            "Le blueprint de décor ciblé n’existe plus ou n’est pas du bon type.",
        "command.revision_conflict" => "Le module a changé depuis l’aperçu. Prévisualisez à nouveau.",
        _ => "La fiche du blueprint a été refusée. Vérifiez les champs signalés avant de réessayer.",
    };

    private void RecordPlaceableDiagnostic(string code, string message) => AddOperationalDiagnostic(
        code,
        message,
        target: null,
        documentPath: "placement",
        Forge.Application.Creator.CreatorDiagnosticSeverity.Error,
        "placeable:blueprint");

    private static bool IsHumanPlaceableMessage(string message) =>
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("world.placeable", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("\\", StringComparison.OrdinalIgnoreCase);

    private static StudioPlaceablePreview FailedPlaceablePreview(StudioPlaceableDraft draft, string message) => new(
        false,
        message,
        "Aucun changement appliqué.",
        false,
        draft.BlueprintId,
        [],
        Vector3Value.Zero,
        string.Empty,
        string.Empty);

    private static StudioUx1ESmokeResult FailedUx1ESmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false);

    private void CloseUx1E() => _approvedPlaceable = null;

}
