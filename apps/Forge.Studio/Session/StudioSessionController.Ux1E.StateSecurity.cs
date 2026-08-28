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
    private static bool TryBuildPlaceableStateMachine(
        StudioPlaceableStateMachineDraft draft,
        out PlaceableVisualStateMachineProfile? stateMachine,
        out string message)
    {
        stateMachine = null;
        message = string.Empty;
        var stateIds = draft.States.Select(state => state.StateId.Trim()).ToArray();
        var required = new[] { "idle", "open", "closed", "broken", "active", "custom" };
        if (draft.Id == Guid.Empty || string.IsNullOrWhiteSpace(draft.InitialStateId) ||
            !stateIds.Contains(draft.InitialStateId.Trim(), StringComparer.Ordinal) ||
            required.Except(stateIds, StringComparer.Ordinal).Any() ||
            stateIds.Any(string.IsNullOrWhiteSpace) ||
            stateIds.Distinct(StringComparer.Ordinal).Count() != stateIds.Length)
        {
            message =
                "La machine visuelle exige un état initial valide et les états Au repos, Ouvert, Fermé, Brisé, Actif et Personnalisé.";
            return false;
        }
        if (draft.States.Any(state => state.Id == Guid.Empty ||
                                      string.IsNullOrWhiteSpace(state.AnimationId) ||
                                      string.IsNullOrWhiteSpace(state.VisualOverrideId) ||
                                      state.AnimationId.Trim().Length > 256 ||
                                      state.VisualOverrideId.Trim().Length > 256))
        {
            message = "Chaque état visuel exige une animation et une apparence valides.";
            return false;
        }
        if (draft.Transitions.Any(transition => transition.Id == Guid.Empty ||
                                               !stateIds.Contains(transition.FromStateId.Trim(), StringComparer.Ordinal) ||
                                               !stateIds.Contains(transition.ToStateId.Trim(), StringComparer.Ordinal) ||
                                               string.IsNullOrWhiteSpace(transition.Trigger) ||
                                               transition.Trigger.Trim().Length > 256 ||
                                               !double.IsFinite(transition.DurationSeconds) ||
                                               transition.DurationSeconds is < 0 or > 3_600 ||
                                               transition.Condition.Trim().Length > 512))
        {
            message =
                "Chaque transition exige un départ, une arrivée, un déclencheur et une durée comprise entre 0 et 3 600 secondes.";
            return false;
        }
        var identities = new[] { draft.Id }
            .Concat(draft.States.Select(state => state.Id))
            .Concat(draft.Transitions.Select(transition => transition.Id))
            .ToArray();
        if (identities.Distinct().Count() != identities.Length)
        {
            message = "Les états et transitions contiennent une identité interne en conflit.";
            return false;
        }

        stateMachine = new(
            draft.Id,
            draft.InitialStateId.Trim(),
            draft.States.Select(state => new PlaceableVisualStateProfile(
                state.Id,
                state.StateId.Trim(),
                state.AnimationId.Trim(),
                state.VisualOverrideId.Trim(),
                state.Interactable)).ToArray(),
            draft.Transitions.Select(transition => new PlaceableVisualTransitionProfile(
                transition.Id,
                transition.FromStateId.Trim(),
                transition.ToStateId.Trim(),
                transition.Trigger.Trim(),
                transition.DurationSeconds,
                NullIfWhiteSpace(transition.Condition))).ToArray());
        return true;
    }

    private static bool TryBuildPlaceableDamageable(
        StudioPlaceableDamageableDraft draft,
        out PlaceableDamageableProfile? damageable,
        out string message)
    {
        damageable = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty || !double.IsFinite(draft.MaximumHealth) ||
            draft.MaximumHealth is <= 0 or > 1_000_000_000 || !double.IsFinite(draft.CurrentHealth) ||
            draft.CurrentHealth < 0 || draft.CurrentHealth > draft.MaximumHealth ||
            !double.IsFinite(draft.Armor) || draft.Armor is < 0 or > 1_000_000_000)
        {
            message = "La destructibilité exige une santé maximale positive, une santé actuelle comprise dans cette limite et une armure positive ou nulle.";
            return false;
        }
        var damageTypes = draft.Resistances.Select(item => item.DamageType.Trim()).ToArray();
        if (draft.Resistances.Count == 0 || damageTypes.Any(string.IsNullOrWhiteSpace) ||
            damageTypes.Any(value => value.Length > 128) ||
            damageTypes.Distinct(StringComparer.Ordinal).Count() != damageTypes.Length ||
            draft.Resistances.Any(item => !double.IsFinite(item.Ratio) || item.Ratio is < 0 or > 1))
        {
            message = "Ajoutez au moins une résistance avec un type unique et une réduction comprise entre 0 et 100 %.";
            return false;
        }
        if (draft.VisualStageThresholds.Count == 0 ||
            draft.VisualStageThresholds.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
            draft.VisualStageThresholds.Distinct().Count() != draft.VisualStageThresholds.Count)
        {
            message = "Ajoutez au moins une étape visuelle unique, comprise entre 0 et 100 % de santé.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.DestructionEffectId) ||
            draft.DestructionEffectId.Trim().Length > 256 ||
            string.IsNullOrWhiteSpace(draft.DebrisDefinitionId) ||
            draft.DebrisDefinitionId.Trim().Length > 256)
        {
            message = "Choisissez un effet de destruction et une définition de débris.";
            return false;
        }
        if (!double.IsFinite(draft.RepairCost) || draft.RepairCost is < 0 or > 1_000_000_000)
        {
            message = "Le coût de réparation doit être un nombre positif ou nul.";
            return false;
        }

        damageable = new(
            draft.Id,
            draft.MaximumHealth,
            draft.CurrentHealth,
            draft.Armor,
            draft.Resistances.ToDictionary(
                item => item.DamageType.Trim(),
                item => item.Ratio,
                StringComparer.Ordinal),
            draft.VisualStageThresholds.ToArray(),
            draft.DestructionEffectId.Trim(),
            draft.DebrisDefinitionId.Trim(),
            draft.Repairable,
            draft.RepairCost);
        return true;
    }

    private static bool DamageableValuesEqual(
        PlaceableDamageableProfile? left,
        PlaceableDamageableProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.MaximumHealth.Equals(right.MaximumHealth) &&
               left.CurrentHealth.Equals(right.CurrentHealth) &&
               left.Armor.Equals(right.Armor) &&
               left.Resistances.OrderBy(item => item.Key, StringComparer.Ordinal)
                   .SequenceEqual(right.Resistances.OrderBy(item => item.Key, StringComparer.Ordinal)) &&
               left.VisualStageThresholds.SequenceEqual(right.VisualStageThresholds) &&
               left.DestructionEffectId == right.DestructionEffectId &&
               left.DebrisDefinitionId == right.DebrisDefinitionId &&
               left.Repairable == right.Repairable &&
               left.RepairCost.Equals(right.RepairCost);
    }

    private static bool TryBuildPlaceableProtection(
        StudioPlaceableProtectionDraft draft,
        out PlaceableProtectionProfile? protection,
        out string message)
    {
        protection = null;
        message = string.Empty;
        var actions = draft.ProtectedActions.Select(value => value.Trim()).ToArray();
        if (draft.Id == Guid.Empty || actions.Length == 0 ||
            actions.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
            actions.Distinct(StringComparer.Ordinal).Count() != actions.Length)
        {
            message = "Choisissez au moins une action protégée unique pour cette politique plot.";
            return false;
        }
        var permissions = draft.RequiredPermissions.Select(value => value.Trim()).ToArray();
        if (permissions.Length == 0 ||
            permissions.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
            permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length)
        {
            message = "Choisissez au moins un droit unique permettant de contourner la protection.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.Feedback) || draft.Feedback.Trim().Length > 512)
        {
            message = "Rédigez le message affiché lorsqu’une action protégée est refusée.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.AuditBinding) || draft.AuditBinding.Trim().Length > 256)
        {
            message = "La protection exige un journal d’audit gouverné pour les dérogations MJ.";
            return false;
        }

        protection = new(
            draft.Id,
            actions,
            permissions,
            draft.Feedback.Trim(),
            draft.AllowGameMasterOverride,
            draft.AuditBinding.Trim());
        return true;
    }

    private static bool ProtectionValuesEqual(
        PlaceableProtectionProfile? left,
        PlaceableProtectionProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.ProtectedActions.SequenceEqual(right.ProtectedActions, StringComparer.Ordinal) &&
               left.RequiredPermissions.SequenceEqual(right.RequiredPermissions, StringComparer.Ordinal) &&
               left.Feedback == right.Feedback &&
               left.AllowGameMasterOverride == right.AllowGameMasterOverride &&
               left.AuditBinding == right.AuditBinding;
    }

    private bool TryBuildPlaceableFactionOwnership(
        StudioPlaceableFactionOwnershipDraft draft,
        out PlaceableFactionOwnershipProfile? ownership,
        out string message)
    {
        ownership = null;
        message = string.Empty;
        var available = Project?.Module.Factions.Factions
            .Where(item => !item.Archived)
            .Select(item => item.Id)
            .ToHashSet() ?? [];
        if (available.Count == 0)
        {
            message = "Ce module ne contient aucune faction active. Créez d’abord une faction dans le catalogue du projet.";
            return false;
        }
        if (draft.Id == Guid.Empty || draft.PrimaryFactionId == Guid.Empty ||
            !available.Contains(draft.PrimaryFactionId))
        {
            message = "Choisissez une faction principale active pour ce placeable.";
            return false;
        }
        var memberships = draft.MembershipFactionIds.ToArray();
        if (memberships.Length == 0 || memberships.Any(id => id == Guid.Empty || !available.Contains(id)) ||
            memberships.Distinct().Count() != memberships.Length || !memberships.Contains(draft.PrimaryFactionId))
        {
            message = "Choisissez des appartenances uniques et conservez la faction principale parmi les membres.";
            return false;
        }

        ownership = new(
            draft.Id,
            PlaceableFactionOwnershipProfile.CurrentSchemaVersion,
            "studio:ux1-n-placeable-faction",
            draft.PrimaryFactionId,
            memberships,
            draft.InheritHostility,
            draft.UnauthorizedActionsAreCrime,
            draft.MembersOnlyAccess,
            draft.BlockFriendlyTargeting);
        return true;
    }

    private static bool FactionOwnershipValuesEqual(
        PlaceableFactionOwnershipProfile? left,
        PlaceableFactionOwnershipProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.PrimaryFactionId == right.PrimaryFactionId &&
               left.MembershipFactionIds.SequenceEqual(right.MembershipFactionIds) &&
               left.InheritHostility == right.InheritHostility &&
               left.UnauthorizedActionsAreCrime == right.UnauthorizedActionsAreCrime &&
               left.MembersOnlyAccess == right.MembersOnlyAccess &&
               left.BlockFriendlyTargeting == right.BlockFriendlyTargeting;
    }

    private bool TryBuildPlaceableConversation(
        StudioPlaceableConversationDraft draft,
        out PlaceableConversationProfile? conversation,
        out string message)
    {
        conversation = null;
        message = string.Empty;
        if (Project is null || draft.Id == Guid.Empty || draft.Bindings.Count == 0)
        {
            message = "Ajoutez au moins une liaison de conversation à ce placeable.";
            return false;
        }

        var bindings = draft.Bindings.Select(binding => new PlaceableConversationBinding(
            binding.Id,
            binding.DialogueId,
            binding.Trigger,
            binding.Trigger == PlaceableConversationTrigger.Item ? string.Empty : binding.Selector.Trim(),
            binding.Trigger == PlaceableConversationTrigger.Item ? binding.ItemBlueprintId : null,
            binding.EntryPointKey.Trim(),
            binding.Priority,
            binding.OncePerUser)).ToArray();
        conversation = new(
            draft.Id,
            PlaceableConversationProfile.CurrentSchemaVersion,
            "studio:ux1-o-placeable-conversation",
            bindings);
        var entryPoints = Project.Dialogues.ToDictionary(
            dialogue => dialogue.Id.Value,
            dialogue => (IReadOnlySet<string>)dialogue.EntryPoints
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal));
        var items = Project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Item)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        var diagnostic = PlaceableConversationProfileValidator.Validate(
                conversation,
                entryPoints,
                items,
                $"module/worldFeatures/placeables/{draft.Id}/conversation")
            .FirstOrDefault();
        if (diagnostic is null)
        {
            return true;
        }

        message = diagnostic.Code switch
        {
            "world.placeable.conversation_dialogue_missing" =>
                "Choisissez un dialogue existant pour chaque liaison.",
            "world.placeable.conversation_entry_point_missing" =>
                "Choisissez un point d’entrée disponible dans le dialogue.",
            "world.placeable.conversation_item_missing" =>
                "Choisissez un objet existant pour le déclencheur Objet.",
            "world.placeable.conversation_priority_invalid" =>
                "La priorité d’une liaison doit rester entre -1000 et 1000.",
            "world.placeable.conversation_selector_invalid" =>
                "Renseignez un déclencheur lisible pour interaction, état, utilisateur ou événement.",
            "world.placeable.conversation_binding_uuid_invalid" =>
                "Les liaisons de conversation doivent être uniques.",
            _ => "La configuration de conversation est incomplète ou invalide.",
        };
        conversation = null;
        return false;
    }

    private static bool ConversationValuesEqual(
        PlaceableConversationProfile? left,
        PlaceableConversationProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Id == right.Id &&
               left.Bindings.SequenceEqual(right.Bindings);
    }

    private static bool TryBuildPlaceableTrap(
        StudioPlaceableTrapDraft draft,
        out PlaceableTrapProfile? trap,
        out string message)
    {
        trap = null;
        message = string.Empty;
        if (draft.Id == Guid.Empty)
        {
            message = "Le piège exige une identité interne stable.";
            return false;
        }
        if (new[]
            {
                draft.TriggerId,
                draft.PayloadId,
                draft.OwnerId,
                draft.VisualEffectId,
                draft.FriendlyPolicy,
            }.Any(string.IsNullOrWhiteSpace))
        {
            message = "Renseignez le déclencheur, l’effet, le propriétaire, le VFX et la politique alliée du piège.";
            return false;
        }
        if (new[]
            {
                draft.TriggerId,
                draft.PayloadId,
                draft.OwnerId,
                draft.VisualEffectId,
                draft.FriendlyPolicy,
            }.Any(value => value.Trim().Length > 256))
        {
            message = "Une identité métier du piège dépasse la longueur autorisée.";
            return false;
        }
        if (!double.IsFinite(draft.DetectionDifficulty) || draft.DetectionDifficulty is < 0 or > 1000 ||
            !double.IsFinite(draft.DisarmDifficulty) || draft.DisarmDifficulty is < 0 or > 1000)
        {
            message = "Les difficultés de détection et de désamorçage doivent rester entre 0 et 1000.";
            return false;
        }
        if (!double.IsFinite(draft.ResetDelaySeconds) || draft.ResetDelaySeconds is < 0 or > 86400)
        {
            message = "Le délai de réarmement doit rester entre 0 seconde et 24 heures.";
            return false;
        }

        trap = new(
            draft.Id,
            draft.TriggerId.Trim(),
            draft.PayloadId.Trim(),
            draft.DetectionDifficulty,
            draft.DisarmDifficulty,
            draft.ResetDelaySeconds,
            draft.OwnerId.Trim(),
            draft.VisualEffectId.Trim(),
            draft.FriendlyPolicy.Trim(),
            draft.Armed,
            draft.OneShot);
        return true;
    }

    private static bool TrapValuesEqual(PlaceableTrapProfile? left, PlaceableTrapProfile? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Id == right.Id &&
               left.TriggerId == right.TriggerId &&
               left.PayloadId == right.PayloadId &&
               left.DetectionDifficulty.Equals(right.DetectionDifficulty) &&
               left.DisarmDifficulty.Equals(right.DisarmDifficulty) &&
               left.ResetDelaySeconds.Equals(right.ResetDelaySeconds) &&
               left.OwnerId == right.OwnerId &&
               left.VisualEffectId == right.VisualEffectId &&
               left.FriendlyPolicy == right.FriendlyPolicy &&
               left.Armed == right.Armed &&
               left.OneShot == right.OneShot;
    }


}
