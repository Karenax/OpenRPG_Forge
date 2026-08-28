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
    private bool TryBuildPlaceableLoot(
        StudioPlaceableLootDraft draft,
        out PlaceableLootSpawnerProfile? loot,
        out string message)
    {
        loot = null;
        message = string.Empty;
        if (Project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.Id == Guid.Empty || draft.LootTableId == Guid.Empty || draft.Id == draft.LootTableId)
        {
            message = "Le trésor et sa table exigent deux identités internes stables et distinctes.";
            return false;
        }
        if (draft.Generation < 0)
        {
            message = "La génération enregistrée du trésor ne peut pas être négative.";
            return false;
        }
        if (draft.Entries.Count == 0)
        {
            message = "Ajoutez au moins un objet à la table du trésor initial.";
            return false;
        }
        if (draft.Entries.Any(entry => entry.Id == Guid.Empty) ||
            draft.Entries.Select(entry => entry.Id).Distinct().Count() != draft.Entries.Count)
        {
            message = "Chaque ligne de trésor doit posséder une identité unique.";
            return false;
        }
        var itemIds = Project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .Select(item => item.Id).ToHashSet();
        if (draft.Entries.Any(entry => !itemIds.Contains(entry.ItemBlueprintId)))
        {
            message = "Un objet choisi pour le trésor initial n’existe plus dans le projet.";
            return false;
        }
        if (draft.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.Group) || entry.Group.Trim().Length > 128))
        {
            message = "Chaque objet du trésor exige une catégorie lisible de 128 caractères maximum.";
            return false;
        }
        if (draft.Entries.Any(entry => !double.IsFinite(entry.Weight) || entry.Weight <= 0 ||
                                       entry.Weight > 1_000_000))
        {
            message = "Le poids de chaque objet du trésor doit être strictement positif.";
            return false;
        }
        if (draft.Entries.Any(entry => entry.MinimumQuantity < 1 ||
                                       entry.MaximumQuantity < entry.MinimumQuantity ||
                                       entry.MaximumQuantity > 1_000_000 ||
                                       entry.MinimumQuality is < 0 or > 1_000_000))
        {
            message = "Les quantités du trésor doivent commencer à 1, rester ordonnées, et la qualité doit être positive ou nulle.";
            return false;
        }

        var reset = draft.ResetGeneratedState;
        loot = new(
            draft.Id,
            draft.LootTableId,
            draft.Timing,
            draft.Seed,
            draft.PersistGeneratedState,
            reset ? [] : draft.SavedResults,
            reset ? 0 : draft.Generation,
            new ItemLootTablePolicy(
                draft.LootTableId,
                1,
                "studio:ux1-s-placeable-initial-treasure",
                draft.Entries.Select(entry => new ItemLootEntryPolicy(
                    entry.ItemBlueprintId.Value,
                    entry.Group.Trim(),
                    entry.Weight,
                    entry.MinimumQuantity,
                    entry.MaximumQuantity,
                    entry.MinimumQuality,
                    entry.Condition.Trim(),
                    entry.Unique)).ToArray()));
        return true;
    }

    private static bool LootConfigurationValuesEqual(
        PlaceableLootSpawnerProfile? left,
        PlaceableLootSpawnerProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.LootTableId == right.LootTableId &&
               left.Timing == right.Timing &&
               left.Seed == right.Seed &&
               left.PersistGeneratedState == right.PersistGeneratedState &&
               left.LootTable.Id == right.LootTable.Id &&
               left.LootTable.SchemaVersion == right.LootTable.SchemaVersion &&
               left.LootTable.Entries.SequenceEqual(right.LootTable.Entries);
    }

    private static bool TryBuildPlaceableContainerEvents(
        StudioPlaceableContainerEventsDraft draft,
        out PlaceableContainerStateEventsProfile? containerEvents,
        out string message)
    {
        containerEvents = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty)
        {
            message = "Les événements d’ouverture et de fermeture exigent une identité interne stable.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.OpenedBinding, draft.ClosedBinding }
            .Any(string.IsNullOrWhiteSpace))
        {
            message = "Renseignez la version ForgeScript et les deux événements d’ouverture et de fermeture.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.OpenedBinding, draft.ClosedBinding }
            .Any(value => value.Trim().Length > 256))
        {
            message = "La version ForgeScript et les noms d’événements doivent rester sous 256 caractères.";
            return false;
        }
        if (!draft.IncludeActor || !draft.IncludeResult || !draft.IncludePreviousState ||
            !draft.IncludeAuthority)
        {
            message = "Le contexte des événements doit conserver l’acteur, le résultat, l’état précédent et l’autorité.";
            return false;
        }

        containerEvents = new(
            draft.Id,
            draft.ForgeScriptApiVersion.Trim(),
            draft.OpenedBinding.Trim(),
            draft.ClosedBinding.Trim(),
            draft.IncludeActor,
            draft.IncludeResult,
            draft.IncludePreviousState,
            draft.IncludeAuthority);
        return true;
    }

    private static bool ContainerEventValuesEqual(
        PlaceableContainerStateEventsProfile? left,
        PlaceableContainerStateEventsProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.ForgeScriptApiVersion == right.ForgeScriptApiVersion &&
               left.OpenedBinding == right.OpenedBinding &&
               left.ClosedBinding == right.ClosedBinding &&
               left.IncludeActor == right.IncludeActor &&
               left.IncludeResult == right.IncludeResult &&
               left.IncludePreviousState == right.IncludePreviousState &&
               left.IncludeAuthority == right.IncludeAuthority;
    }

    private static bool TryBuildPlaceableLockEvents(
        StudioPlaceableLockEventsDraft draft,
        out PlaceableLockStateEventsProfile? lockEvents,
        out string message)
    {
        lockEvents = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty)
        {
            message = "Les événements de verrouillage et de déverrouillage exigent une identité interne stable.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.ChangingBinding, draft.ChangedBinding }
            .Any(string.IsNullOrWhiteSpace))
        {
            message = "Renseignez la version ForgeScript et les événements avant et après le changement de verrou.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.ChangingBinding, draft.ChangedBinding }
            .Any(value => value.Trim().Length > 256))
        {
            message = "La version ForgeScript et les noms d’événements doivent rester sous 256 caractères.";
            return false;
        }
        if (!draft.IncludeMethod || !draft.IncludeCredential || !draft.IncludeActor || !draft.ControlledVeto)
        {
            message = "Le contexte doit conserver la méthode, le justificatif, l’acteur et le veto contrôlé.";
            return false;
        }

        lockEvents = new(
            draft.Id,
            draft.ForgeScriptApiVersion.Trim(),
            draft.ChangingBinding.Trim(),
            draft.ChangedBinding.Trim(),
            draft.IncludeMethod,
            draft.IncludeCredential,
            draft.IncludeActor,
            draft.ControlledVeto);
        return true;
    }

    private static bool LockEventValuesEqual(
        PlaceableLockStateEventsProfile? left,
        PlaceableLockStateEventsProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.ForgeScriptApiVersion == right.ForgeScriptApiVersion &&
               left.ChangingBinding == right.ChangingBinding &&
               left.ChangedBinding == right.ChangedBinding &&
               left.IncludeMethod == right.IncludeMethod &&
               left.IncludeCredential == right.IncludeCredential &&
               left.IncludeActor == right.IncludeActor &&
               left.ControlledVeto == right.ControlledVeto;
    }

    private static bool TryBuildPlaceableContentsChanged(
        StudioPlaceableContentsChangedDraft draft,
        out PlaceableContentsChangedEventProfile? contentsChanged,
        out string message)
    {
        contentsChanged = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty ||
            new[] { draft.ForgeScriptApiVersion, draft.EventBinding }.Any(string.IsNullOrWhiteSpace))
        {
            message = "L’événement de contenu modifié exige une identité stable, une version ForgeScript et un nom d’événement.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.EventBinding }
            .Any(value => value.Trim().Length > 256))
        {
            message = "La version ForgeScript et le nom de l’événement doivent rester sous 256 caractères.";
            return false;
        }
        if (!draft.IncludeTransactionalDelta || !draft.IncludeCause || !draft.IncludeRevision || !draft.IncludeActor)
        {
            message = "Le contenu modifié doit conserver le delta transactionnel, la cause, les révisions et l’acteur.";
            return false;
        }

        contentsChanged = new(
            draft.Id,
            draft.ForgeScriptApiVersion.Trim(),
            draft.EventBinding.Trim(),
            draft.IncludeTransactionalDelta,
            draft.IncludeCause,
            draft.IncludeRevision,
            draft.IncludeActor);
        return true;
    }

    private static bool ContentsChangedValuesEqual(
        PlaceableContentsChangedEventProfile? left,
        PlaceableContentsChangedEventProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.ForgeScriptApiVersion == right.ForgeScriptApiVersion &&
               left.EventBinding == right.EventBinding &&
               left.IncludeTransactionalDelta == right.IncludeTransactionalDelta &&
               left.IncludeCause == right.IncludeCause &&
               left.IncludeRevision == right.IncludeRevision &&
               left.IncludeActor == right.IncludeActor;
    }

    private static bool TryBuildPlaceableDamageEvents(
        StudioPlaceableDamageEventsDraft draft,
        out PlaceableDamageEventsProfile? damageEvents,
        out string message)
    {
        damageEvents = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty ||
            new[] { draft.ForgeScriptApiVersion, draft.DamagedBinding, draft.DestroyedBinding }
                .Any(string.IsNullOrWhiteSpace))
        {
            message = "Les événements de dégâts et de mort exigent une identité stable, une version ForgeScript et deux noms d’événement.";
            return false;
        }
        if (new[] { draft.ForgeScriptApiVersion, draft.DamagedBinding, draft.DestroyedBinding }
            .Any(value => value.Trim().Length > 256))
        {
            message = "La version ForgeScript et les noms d’événements doivent rester sous 256 caractères.";
            return false;
        }
        if (!draft.IncludeSource || !draft.IncludeAmount || !draft.IncludeDamageType ||
            !draft.IncludePreviousHealth || !draft.IncludeCurrentHealth || !draft.IncludeContext)
        {
            message = "Les événements de dégâts et de mort doivent conserver la source, la quantité, le type, la santé avant/après et le contexte.";
            return false;
        }

        damageEvents = new(
            draft.Id,
            draft.ForgeScriptApiVersion.Trim(),
            draft.DamagedBinding.Trim(),
            draft.DestroyedBinding.Trim(),
            draft.IncludeSource,
            draft.IncludeAmount,
            draft.IncludeDamageType,
            draft.IncludePreviousHealth,
            draft.IncludeCurrentHealth,
            draft.IncludeContext);
        return true;
    }

    private static bool DamageEventValuesEqual(
        PlaceableDamageEventsProfile? left,
        PlaceableDamageEventsProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.ForgeScriptApiVersion == right.ForgeScriptApiVersion &&
               left.DamagedBinding == right.DamagedBinding &&
               left.DestroyedBinding == right.DestroyedBinding &&
               left.IncludeSource == right.IncludeSource &&
               left.IncludeAmount == right.IncludeAmount &&
               left.IncludeDamageType == right.IncludeDamageType &&
               left.IncludePreviousHealth == right.IncludePreviousHealth &&
               left.IncludeCurrentHealth == right.IncludeCurrentHealth &&
               left.IncludeContext == right.IncludeContext;
    }

    private bool TryBuildPlaceableCredential(
        StudioPlaceableCredentialDraft draft,
        out PlaceableWorldProfile.CredentialPolicy? credential,
        out string message)
    {
        credential = null;
        message = string.Empty;
        if (Project is null)
        {
            message = "Ouvrez un module avant de configurer un justificatif.";
            return false;
        }

        if (draft.ItemBlueprintId is { } itemId && Project.Blueprints.All(blueprint =>
                blueprint.Id != itemId || blueprint.Kind != BlueprintKind.Item))
        {
            message = "Choisissez un objet clé encore présent dans le projet.";
            return false;
        }

        var virtualPermission = draft.VirtualPermission.Trim();
        if (draft.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission &&
            string.IsNullOrWhiteSpace(virtualPermission))
        {
            message = "Renseignez la permission virtuelle qui autorise cet accès.";
            return false;
        }
        if (virtualPermission.Length > 128)
        {
            message = "Le nom de la permission virtuelle est trop long.";
            return false;
        }

        var credentialId = draft.ItemBlueprintId is { } selectedItem
            ? LockItemCredential(selectedItem)
            : draft.OtherCredentialId.Trim();
        if (draft.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission &&
            string.IsNullOrWhiteSpace(credentialId))
        {
            credentialId = "permission:" + virtualPermission;
        }
        if (string.IsNullOrWhiteSpace(credentialId) || credentialId.Length > 256)
        {
            message = "Choisissez un objet clé ou nommez un justificatif métier.";
            return false;
        }
        if (credentialId.StartsWith("item:", StringComparison.OrdinalIgnoreCase) &&
            (TryParseLockItemCredential(credentialId) is not { } referencedItem ||
             Project.Blueprints.All(blueprint =>
                 blueprint.Id != referencedItem || blueprint.Kind != BlueprintKind.Item)))
        {
            message = "Le justificatif doit référencer un objet clé existant dans le projet.";
            return false;
        }

        if (draft.Charges < 0 ||
            draft.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge && draft.Charges < 1)
        {
            message = "Une politique à charges exige au moins une charge disponible.";
            return false;
        }

        credential = new(
            credentialId,
            draft.Consumption,
            draft.Charges,
            draft.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                ? virtualPermission
                : null,
            draft.AuditUse);
        return true;
    }

    private static bool CredentialValuesEqual(
        PlaceableWorldProfile.CredentialPolicy? left,
        PlaceableWorldProfile.CredentialPolicy? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.CredentialId == right.CredentialId &&
               left.Consumption == right.Consumption &&
               left.Charges == right.Charges &&
               left.VirtualPermission == right.VirtualPermission &&
               left.AuditUse == right.AuditUse;
    }

    private bool TryBuildPlaceableLock(
        StudioPlaceableLockDraft draft,
        out PlaceableLockProfile? lockProfile,
        out string message)
    {
        lockProfile = null;
        message = string.Empty;
        if (Project is null || draft.Id == Guid.Empty)
        {
            message = "Le verrou exige une identité interne stable.";
            return false;
        }

        if (!double.IsFinite(draft.Difficulty) || draft.Difficulty is < 0 or > 1000)
        {
            message = "La difficulté du verrou doit rester entre 0 et 1000.";
            return false;
        }

        if (draft.KeyItemId is { } keyItemId && Project.Blueprints.All(blueprint =>
                blueprint.Id != keyItemId || blueprint.Kind != BlueprintKind.Item))
        {
            message = "Choisissez une clé encore présente dans les objets du projet.";
            return false;
        }

        var credentials = (draft.KeyItemId is null
                ? Enumerable.Empty<string>()
                : [LockItemCredential(draft.KeyItemId)])
            .Concat(draft.OtherCredentialIds.Select(value => value.Trim()))
            .ToArray();
        if (credentials.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256) ||
            credentials.Distinct(StringComparer.Ordinal).Count() != credentials.Length ||
            credentials.Where(value => value.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                .Any(value => TryParseLockItemCredential(value) is not { } itemId ||
                              Project.Blueprints.All(blueprint =>
                                  blueprint.Id != itemId || blueprint.Kind != BlueprintKind.Item)))
        {
            message = "Les clés et justificatifs doivent être uniques et référencer des objets existants.";
            return false;
        }

        var permissions = draft.RequiredPermissions.Select(value => value.Trim()).ToArray();
        if (permissions.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
            permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length)
        {
            message = "Les droits de déverrouillage doivent être uniques et nommés.";
            return false;
        }

        var skillId = draft.SkillId.Trim();
        if (draft.Hackable && string.IsNullOrWhiteSpace(skillId))
        {
            message = "Le piratage exige une compétence ou une spécialité.";
            return false;
        }
        if (skillId.Length > 128)
        {
            message = "Le nom de la compétence est trop long.";
            return false;
        }

        string? codeHash = null;
        if (draft.CodeEnabled)
        {
            codeHash = !string.IsNullOrWhiteSpace(draft.AccessCode)
                ? HashLockCode(draft.AccessCode)
                : draft.ExistingCodeHash;
            if (string.IsNullOrWhiteSpace(codeHash))
            {
                message = "Saisissez un code d’accès avant de l’activer.";
                return false;
            }
        }

        var mechanismCount = credentials.Length + permissions.Length +
                             (string.IsNullOrWhiteSpace(skillId) ? 0 : 1) +
                             (codeHash is null ? 0 : 1) + (draft.Hackable ? 1 : 0);
        if (mechanismCount == 0)
        {
            message = "Ajoutez au moins une clé, une compétence, un code, un piratage ou un droit.";
            return false;
        }

        lockProfile = new(
            draft.Id,
            draft.InitialState,
            credentials,
            string.IsNullOrWhiteSpace(skillId) ? null : skillId,
            draft.Difficulty,
            codeHash,
            draft.Hackable,
            draft.Relockable,
            draft.CanJam,
            permissions);
        return true;
    }

    private static bool LockValuesEqual(PlaceableLockProfile? left, PlaceableLockProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.InitialState == right.InitialState &&
               left.CredentialIds.SequenceEqual(right.CredentialIds, StringComparer.Ordinal) &&
               left.SkillId == right.SkillId &&
               left.Difficulty.Equals(right.Difficulty) &&
               left.CodeHash == right.CodeHash &&
               left.Hackable == right.Hackable &&
               left.Relockable == right.Relockable &&
               left.CanJam == right.CanJam &&
               left.RequiredPermissions.SequenceEqual(right.RequiredPermissions, StringComparer.Ordinal);
    }

    private static string LockItemCredential(BlueprintId itemId) => $"item:{itemId.Value:D}";

    private static BlueprintId? TryParseLockItemCredential(string value) =>
        value.StartsWith("item:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(value[5..], out var itemId)
            ? BlueprintId.From(itemId)
            : null;

    private static string HashLockCode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
            .ToLowerInvariant();
    }

    private static string HumanizeLockState(PlaceableAuthoredLockState state) => state switch
    {
        PlaceableAuthoredLockState.Unlocked => "Déverrouillé au chargement",
        PlaceableAuthoredLockState.Locked => "Verrouillé au chargement",
        PlaceableAuthoredLockState.Jammed => "Brouillé au chargement",
        PlaceableAuthoredLockState.Broken => "Brisé au chargement",
        _ => "État de verrou",
    };

    private static string HumanizeCredentialConsumption(
        PlaceableWorldProfile.CredentialConsumption consumption) => consumption switch
        {
            PlaceableWorldProfile.CredentialConsumption.Retain => "conserver après usage",
            PlaceableWorldProfile.CredentialConsumption.Consume => "consommer après usage",
            PlaceableWorldProfile.CredentialConsumption.Charge => "dépenser une charge",
            PlaceableWorldProfile.CredentialConsumption.Shared => "partager sans consommer",
            PlaceableWorldProfile.CredentialConsumption.VirtualPermission => "vérifier une permission virtuelle",
            _ => "politique d’accès",
        };

    private string FactionDisplayName(Guid factionId) =>
        Project?.Module.Factions.Factions.FirstOrDefault(item => item.Id == factionId)?.DisplayName ??
        "Faction indisponible";


}
