using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1ADProvenance = "studio:ux1-ad:door-reactive-complex";
    private const string AbilityTargetedEventUx1AD = "ability-targeted";
    private const string AbilityImpactedEventUx1AD = "ability-impacted";
    private const string HeartbeatEventUx1AD = "heartbeat";
    private const string CustomPrefixUx1AD = "custom:";

    private static readonly string[] ManagedExactDoorEventsUx1AD =
    [
        AbilityTargetedEventUx1AD,
        AbilityImpactedEventUx1AD,
        HeartbeatEventUx1AD,
    ];

    private readonly SetWorldFeatureCatalogHandler _setDoorCatalogUx1AD = new(new SystemClock());
    private StudioDoorReactiveComplexDraft? _approvedDoorReactiveComplexDraftUx1AD;
    private ProjectRevision? _approvedDoorReactiveComplexRevisionUx1AD;

    internal IReadOnlyList<StudioDoorReactiveComplexChoice> DoorReactiveComplexChoicesUx1AD
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }

            var configured = project.Module.WorldFeatures.Doors
                .Where(profile => profile.DialogueId is not null ||
                                  !string.IsNullOrWhiteSpace(profile.FactionId) ||
                                  profile.Assembly is not null ||
                                  profile.Events.Any(binding => IsManagedExactDoorEventUx1AD(binding.Event) ||
                                                                IsCustomDoorEventUx1AD(binding.Event)))
                .Select(profile => profile.BlueprintId)
                .ToHashSet();
            var doors = project.Blueprints
                .Where(blueprint => blueprint.Kind == BlueprintKind.Door)
                .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(blueprint => blueprint.Slug, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            var duplicateNames = doors
                .GroupBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return doors
                .Select(blueprint => new StudioDoorReactiveComplexChoice(
                    blueprint.Id,
                    duplicateNames.Contains(blueprint.DisplayName)
                        ? $"{blueprint.DisplayName} · {blueprint.Slug}"
                        : blueprint.DisplayName,
                    configured.Contains(blueprint.Id)))
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioDoorDialogueChoice> DoorDialogueChoicesUx1AD
    {
        get
        {
            var dialogues = Project?.Dialogues
                .OrderBy(dialogue => dialogue.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(dialogue => dialogue.Slug, StringComparer.CurrentCultureIgnoreCase)
                .ToArray() ?? [];
            var duplicateNames = dialogues
                .GroupBy(dialogue => dialogue.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return dialogues.Select(dialogue => new StudioDoorDialogueChoice(
                dialogue.Id.Value,
                duplicateNames.Contains(dialogue.DisplayName)
                    ? $"{dialogue.DisplayName} · {dialogue.Slug}"
                    : dialogue.DisplayName)).ToArray();
        }
    }

    internal IReadOnlyList<StudioDoorFactionChoice> DoorFactionChoicesUx1AD
    {
        get
        {
            var factions = Project?.Module.Factions.Factions
                .OrderBy(faction => faction.Archived)
                .ThenBy(faction => faction.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(faction => faction.Slug, StringComparer.CurrentCultureIgnoreCase)
                .ToArray() ?? [];
            var duplicateNames = factions
                .GroupBy(faction => faction.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return factions.Select(faction =>
            {
                var label = duplicateNames.Contains(faction.DisplayName)
                    ? $"{faction.DisplayName} · {faction.Slug}"
                    : faction.DisplayName;
                return new StudioDoorFactionChoice(
                    faction.Slug,
                    faction.Archived ? $"{label} (archivée)" : label);
            }).ToArray();
        }
    }

    internal IReadOnlyList<StudioDoorCustomEventChoice> DoorCustomEventChoicesUx1AD(BlueprintId blueprintId)
    {
        var profile = Project?.Module.WorldFeatures.Doors.SingleOrDefault(candidate =>
            candidate.BlueprintId == blueprintId);
        if (profile is null)
        {
            return [];
        }

        var choices = new List<StudioDoorCustomEventChoice>();
        foreach (var group in profile.Events
                     .Where(binding => IsCustomDoorEventUx1AD(binding.Event))
                     .GroupBy(binding => binding.Event, StringComparer.Ordinal)
                     .Where(group => group.Count() == 1)
                     .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var binding = group.Single();
            if (!TryParseCustomDoorEventUx1AD(binding.Event, out var eventNamespace, out var eventName))
            {
                continue;
            }
            choices.Add(new(
                binding.Event,
                $"{eventNamespace} · {eventName}",
                eventNamespace,
                eventName,
                binding.ScriptId,
                binding.Before,
                binding.Authoritative));
        }
        return choices;
    }

    internal StudioDoorReactiveComplexDraft? CreateDoorReactiveComplexDraftUx1AD(BlueprintId blueprintId) =>
        TryCreateDoorReactiveComplexDraftUx1AD(blueprintId, out var draft, out _) ? draft : null;

    internal bool TryCreateDoorReactiveComplexDraftUx1AD(
        BlueprintId blueprintId,
        out StudioDoorReactiveComplexDraft? draft,
        out string message)
    {
        draft = null;
        message = string.Empty;
        var project = Project;
        if (project is null)
        {
            message = "Ouvrez un module avant de configurer une porte.";
            return false;
        }
        if (project.Blueprints.All(blueprint =>
                blueprint.Id != blueprintId || blueprint.Kind != BlueprintKind.Door))
        {
            message = "La porte sélectionnée n’est plus disponible dans le module.";
            return false;
        }

        var matches = project.Module.WorldFeatures.Doors
            .Where(profile => profile.BlueprintId == blueprintId)
            .ToArray();
        if (matches.Length > 1)
        {
            message = "Plusieurs fiches ciblent cette porte · corrigez le module avant l’édition.";
            return false;
        }

        var profile = matches.SingleOrDefault();
        if (!TryValidateDoorAssemblyForEditorUx1AD(profile, out message))
        {
            return false;
        }
        var abilityTargeted = FindDoorEventUx1AD(profile, AbilityTargetedEventUx1AD);
        var abilityImpacted = FindDoorEventUx1AD(profile, AbilityImpactedEventUx1AD);
        var heartbeat = FindDoorEventUx1AD(profile, HeartbeatEventUx1AD);
        var assembly = profile?.Assembly;
        var panels = assembly?.Panels.Select(panel => new StudioDoorAssemblyPanelDraft(
                panel.Id,
                panel.Role,
                panel.OpenOffset,
                panel.DelaySeconds,
                panel.Reversed))
            .ToArray() ??
            [
                new(StableDoorIdUx1AD(blueprintId, "panel:left"), "vantail gauche", -1, 0, false),
                new(StableDoorIdUx1AD(blueprintId, "panel:right"), "vantail droit", 1, .15, true),
            ];
        draft = new StudioDoorReactiveComplexDraft(
            blueprintId,
            abilityTargeted is not null,
            abilityTargeted?.ScriptId ?? "scripts/door_ability_targeted",
            abilityImpacted is not null,
            abilityImpacted?.ScriptId ?? "scripts/door_ability_impacted",
            heartbeat is not null,
            heartbeat?.ScriptId ?? "scripts/door_heartbeat",
            new(false, null, false, "door", "signal", "scripts/door_custom", false, true),
            profile?.DialogueId,
            profile?.FactionId,
            assembly is not null,
            assembly?.Kind ?? DoorWorldProfile.AssemblyKind.Double,
            panels,
            assembly?.Sequence ?? "ouvrir les panneaux dans l’ordre",
            assembly?.Interlocked ?? false,
            assembly?.FailureRecovery ?? "refermer les panneaux engagés");
        return true;
    }

    internal StudioDoorReactiveComplexPreview PreviewDoorReactiveComplexUx1AD(
        StudioDoorReactiveComplexDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDoorReactiveComplexPreviewUx1AD();
        var history = History;
        if (!TryBuildDoorReactiveComplexUx1AD(draft, dryRun: true, out var command, out var profile,
                out var message) || command is null || profile is null || history is null)
        {
            return FailedDoorReactiveComplexPreviewUx1AD(message);
        }

        var originalProject = history.State.Project;
        var originalRevision = history.State.Revision;
        var preview = _setDoorCatalogUx1AD.Handle(history.State, command);
        if (!preview.Succeeded)
        {
            return FailedDoorReactiveComplexPreviewUx1AD(
                HumanizeDoorReactiveComplexDiagnosticUx1AD(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        if (!ReferenceEquals(originalProject, history.State.Project) ||
            history.State.Revision != originalRevision || preview.NewRevision is not null)
        {
            return FailedDoorReactiveComplexPreviewUx1AD(
                "L’aperçu n’a pas pu garantir l’absence d’écriture.");
        }

        var eventsAccepted = PreviewDoorReactiveEventsUx1AD(profile, draft);
        var narrativeAccepted = profile.DialogueId == draft.DialogueId &&
                                string.Equals(profile.FactionId, NormalizeOptionalUx1AD(draft.FactionId),
                                    StringComparison.Ordinal);
        var assemblyAccepted = DoorAssemblyMatchesDraftUx1AD(profile.Assembly, draft);
        if (!eventsAccepted || !narrativeAccepted || !assemblyAccepted)
        {
            return FailedDoorReactiveComplexPreviewUx1AD(
                "La simulation des réactions, références ou panneaux ne correspond pas aux réglages choisis.");
        }

        var eventLabels = EnabledDoorReactiveEventLabelsUx1AD(draft).ToArray();
        var eventSummary = eventLabels.Length == 0
            ? "Réactions · aucune réaction gérée activée"
            : $"Réactions · {string.Join(" · ", eventLabels)}";
        var dialogueSummary = draft.DialogueId is null ? "aucune conversation" : "conversation liée";
        var factionSummary = string.IsNullOrWhiteSpace(draft.FactionId) ? "aucune faction" : "faction liée";
        var assemblySummary = draft.AssemblyEnabled
            ? $"Assemblage · {HumanizeAssemblyKindUx1AD(draft.AssemblyKind)} · {draft.Panels.Count} panneau(x) · " +
              (draft.Interlocked ? "verrouillage coordonné" : "mouvement indépendant")
            : "Assemblage · porte simple sans orchestration";
        _approvedDoorReactiveComplexDraftUx1AD = draft;
        _approvedDoorReactiveComplexRevisionUx1AD = originalRevision;
        return new(
            true,
            "Aperçu validé · aucune porte ni aucun fichier n’a été modifié.",
            $"{eventSummary}\nRéférences · {dialogueSummary} · {factionSummary}\n{assemblySummary}",
            true,
            eventsAccepted,
            narrativeAccepted,
            assemblyAccepted);
    }

    internal StudioDoorReactiveComplexActionResult ApplyApprovedDoorReactiveComplexUx1AD()
    {
        var draft = _approvedDoorReactiveComplexDraftUx1AD;
        var approvedRevision = _approvedDoorReactiveComplexRevisionUx1AD;
        var history = History;
        if (draft is null || approvedRevision is null || history is null)
        {
            CancelDoorReactiveComplexPreviewUx1AD();
            return new(false, "Relancez l’aperçu avant d’appliquer.");
        }
        if (history.State.Revision != approvedRevision.Value)
        {
            CancelDoorReactiveComplexPreviewUx1AD();
            return new(false, "Le module a changé depuis l’aperçu · relancez-le avant d’appliquer.", true);
        }
        if (!TryBuildDoorReactiveComplexUx1AD(draft, dryRun: true, out var command, out _, out var message) ||
            command is null)
        {
            CancelDoorReactiveComplexPreviewUx1AD();
            return new(false, message);
        }

        var confirmation = _setDoorCatalogUx1AD.Handle(history.State, command);
        if (!confirmation.Succeeded)
        {
            CancelDoorReactiveComplexPreviewUx1AD();
            return new(false,
                HumanizeDoorReactiveComplexDiagnosticUx1AD(confirmation.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setDoorCatalogUx1AD.Handle(history.State, command with
        {
            CommandId = CommandId.From(Guid.NewGuid()),
            DryRun = false,
        });
        if (!applied.Succeeded)
        {
            CancelDoorReactiveComplexPreviewUx1AD();
            return new(false,
                HumanizeDoorReactiveComplexDiagnosticUx1AD(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        CancelDoorReactiveComplexPreviewUx1AD();
        return new(true, "Réactions, références et assemblage de porte appliqués · enregistrez le module.");
    }

    internal void CancelDoorReactiveComplexPreviewUx1AD()
    {
        _approvedDoorReactiveComplexDraftUx1AD = null;
        _approvedDoorReactiveComplexRevisionUx1AD = null;
    }

    private void CloseUx1AD() => CancelDoorReactiveComplexPreviewUx1AD();

    internal StudioUx1ADSmokeResult RunUx1ADContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ad-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var authored = OriginalModuleAuthoringService.CreateVeilleeDesEclats().State.Project;
            var targetBlueprint = authored.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.Door);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000002")),
                "porte-voisine-ad",
                targetBlueprint.DisplayName,
                BlueprintKind.Door);
            var freshBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000003")),
                "porte-neuve-ad",
                targetBlueprint.DisplayName,
                BlueprintKind.Door);
            var unsafeBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000005")),
                "porte-hors-bornes-ad",
                targetBlueprint.DisplayName,
                BlueprintKind.Door);
            var tooManyBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000006")),
                "porte-neuf-panneaux-ad",
                targetBlueprint.DisplayName,
                BlueprintKind.Door);
            var faction = new FactionDefinition(
                Guid.Parse("c1ad0000-0000-4000-8000-000000000004"),
                1,
                "test:ux1-ad-faction",
                "ash-watch",
                "Veilleurs de cendre",
                false,
                [],
                []);
            var duplicateFaction = new FactionDefinition(
                Guid.Parse("c1ad0000-0000-4000-8000-000000000007"),
                1,
                "test:ux1-ad-faction-duplicate",
                "ash-watch-auxiliary",
                faction.DisplayName,
                false,
                [],
                []);
            var duplicateDialogueNodeId = DialogueNodeId.From(
                Guid.Parse("c1ad0000-0000-4000-8000-000000000008"));
            var duplicateDialogue = new DialogueDefinition(
                DialogueId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000009")),
                "lyra-veillee-auxiliaire",
                authored.Dialogues.Single().DisplayName,
                duplicateDialogueNodeId,
                [new DialogueNode(
                    duplicateDialogueNodeId,
                    null,
                    new LocalizedText("dialogue.ux1ad.duplicate", "Une seconde conversation homonyme."))],
                provenance: "test:ux1-ad-dialogue-duplicate");
            var targetProfile = CreateCompleteDoorProfileUx1X(
                targetBlueprint.Id,
                Guid.Parse("c1ad0000-0000-4000-8000-000000000010"),
                authored.Dialogues.Single().Id.Value) with
            {
                Events =
                [
                    new("damage-applied", "scripts/door_damage_neighbor", false, true),
                    new(AbilityTargetedEventUx1AD, "scripts/door_targeted_old", true, true),
                    new("physical-attack-targeted", "scripts/door_attack_target_neighbor", true, true),
                    new(AbilityImpactedEventUx1AD, "scripts/door_impacted_old", false, true),
                    new("custom:ash:seal", "scripts/door_ash_old", true, false),
                    new(HeartbeatEventUx1AD, "scripts/door_heartbeat_old", false, true),
                    new("entity-destroyed", "scripts/door_destroy_neighbor", false, true),
                    new("custom:ember:ward", "scripts/door_ember_neighbor", false, true),
                    new("physical-attack-resolved", "scripts/door_attack_neighbor", false, true),
                    new("unknown:door-audit", "scripts/door_unknown_neighbor", true, false),
                ],
                DialogueId = null,
                FactionId = null,
            };
            var neighborProfile = CreateNeighborDoorProfileUx1X(
                neighborBlueprint.Id,
                Guid.Parse("c1ad0000-0000-4000-8000-000000000011"));
            var unsafeProfile = CreateNeighborDoorProfileUx1X(
                unsafeBlueprint.Id,
                Guid.Parse("c1ad0000-0000-4000-8000-000000000012")) with
            {
                Assembly = new(
                    DoorWorldProfile.AssemblyKind.Double,
                    [new(
                        Guid.Parse("c1ad0000-0000-4000-8000-000000000030"),
                        "panneau hors bornes",
                        150,
                        0,
                        false)],
                    "ouvrir",
                    false,
                    "arrêter"),
            };
            var tooManyProfile = CreateNeighborDoorProfileUx1X(
                tooManyBlueprint.Id,
                Guid.Parse("c1ad0000-0000-4000-8000-000000000013")) with
            {
                Assembly = new(
                    DoorWorldProfile.AssemblyKind.Airlock,
                    Enumerable.Range(0, 9).Select(index => new DoorWorldProfile.Panel(
                        StableDoorIdUx1AD(tooManyBlueprint.Id, $"panel:{index}"),
                        $"panneau {index + 1}",
                        index,
                        index * .1,
                        false)).ToArray(),
                    "ouvrir en série",
                    true,
                    "refermer"),
            };
            var catalog = new WorldFeatureCatalog(
                authored.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ad-catalog",
                authored.Module.WorldFeatures.Placeables,
                [targetProfile, neighborProfile, unsafeProfile, tooManyProfile],
                authored.Module.WorldFeatures.Triggers,
                authored.Module.WorldFeatures.Encounters,
                authored.Module.WorldFeatures.VehicleRoutes,
                authored.Module.WorldFeatures.Merchants,
                authored.Module.WorldFeatures.Companions,
                authored.Module.WorldFeatures.Scripts);
            var module = authored.Module
                .WithFactions(new FactionCatalog([faction, duplicateFaction], [], [], []))
                .WithWorldFeatures(catalog);
            var project = new ProjectDefinition(
                authored.Id,
                authored.Title,
                module,
                authored.Blueprints
                    .Append(neighborBlueprint)
                    .Append(freshBlueprint)
                    .Append(unsafeBlueprint)
                    .Append(tooManyBlueprint)
                    .ToArray(),
                authored.Areas,
                authored.Dialogues.Append(duplicateDialogue).ToArray(),
                authored.Quests);
            if (!ProjectSession.Create(tempRoot, project).Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1ADSmoke();
            }

            var currentProject = editing.Project;
            var history = editing.History;
            if (currentProject is null || history is null)
            {
                return FailedUx1ADSmoke();
            }
            var loadedOriginal = currentProject.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var loadedNeighbor = currentProject.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var loadedDoorOrder = currentProject.Module.WorldFeatures.Doors.Select(profile => profile.Id).ToArray();
            var preservedEventNames = new[]
            {
                "damage-applied",
                "physical-attack-targeted",
                "entity-destroyed",
                "custom:ember:ward",
                "physical-attack-resolved",
                "unknown:door-audit",
            };
            var preservedEvents = loadedOriginal.Events
                .Where(binding => preservedEventNames.Contains(binding.Event, StringComparer.Ordinal))
                .ToArray();
            var choices = editing.DoorReactiveComplexChoicesUx1AD;
            var dialogueChoices = editing.DoorDialogueChoicesUx1AD;
            var factionChoices = editing.DoorFactionChoicesUx1AD;
            var customChoices = editing.DoorCustomEventChoicesUx1AD(targetBlueprint.Id);
            var source = editing.CreateDoorReactiveComplexDraftUx1AD(targetBlueprint.Id);
            var fresh = editing.CreateDoorReactiveComplexDraftUx1AD(freshBlueprint.Id);
            if (source is null || fresh is null)
            {
                return FailedUx1ADSmoke();
            }

            var selectedCustom = customChoices.Single(choice => choice.EventName == "custom:ash:seal");
            var explicitSelection = choices.Count == 5 &&
                                    choices.Single(choice => choice.BlueprintId == targetBlueprint.Id).Configured &&
                                    !choices.Single(choice => choice.BlueprintId == freshBlueprint.Id).Configured &&
                                    !source.CustomEvent.Touched &&
                                    customChoices.Select(choice => choice.EventName).SequenceEqual(
                                        ["custom:ash:seal", "custom:ember:ward"]);
            var duplicateHumanLabels = choices.All(choice =>
                                           {
                                               var blueprint = currentProject.Blueprints.Single(item =>
                                                   item.Id == choice.BlueprintId);
                                               return choice.Label == $"{blueprint.DisplayName} · {blueprint.Slug}";
                                           }) &&
                                       dialogueChoices.Count == 2 && dialogueChoices.All(choice =>
                                       {
                                           var dialogue = currentProject.Dialogues.Single(item =>
                                               item.Id.Value == choice.Id);
                                           return choice.Label == $"{dialogue.DisplayName} · {dialogue.Slug}";
                                       }) &&
                                       factionChoices.Count == 2 && factionChoices.All(choice =>
                                       {
                                           var option = currentProject.Module.Factions.Factions.Single(item =>
                                               item.Slug == choice.Slug);
                                           return choice.Label == $"{option.DisplayName} · {option.Slug}";
                                       });
            var unsafeProject = editing.Project;
            var unsafeRevision = history.State.Revision;
            var unsafeAvailable = editing.TryCreateDoorReactiveComplexDraftUx1AD(
                unsafeBlueprint.Id,
                out var unsafeDraft,
                out var unsafeMessage);
            var tooManyAvailable = editing.TryCreateDoorReactiveComplexDraftUx1AD(
                tooManyBlueprint.Id,
                out var tooManyDraft,
                out var tooManyMessage);
            var unsafeAssembly = unsafeProfile.Assembly!;
            var delayProfile = unsafeProfile with
            {
                Assembly = unsafeAssembly with
                {
                    Panels = [unsafeAssembly.Panels[0] with { OpenOffset = 0, DelaySeconds = 301 }],
                },
            };
            var delayAvailable = TryValidateDoorAssemblyForEditorUx1AD(delayProfile, out var delayMessage);
            var unsafeAssemblyBlocked = !unsafeAvailable && unsafeDraft is null &&
                                        unsafeMessage.Contains("-100 à 100", StringComparison.Ordinal) &&
                                        !tooManyAvailable && tooManyDraft is null &&
                                        tooManyMessage.Contains("plus de 8 panneaux", StringComparison.Ordinal) &&
                                        !delayAvailable && delayMessage.Contains("0 à 300", StringComparison.Ordinal) &&
                                        ReferenceEquals(unsafeProject, editing.Project) &&
                                        history.State.Revision == unsafeRevision && !editing.IsDirty;
            var dialogueId = authored.Dialogues.Single().Id.Value;
            var panels = new[]
            {
                new StudioDoorAssemblyPanelDraft(
                    Guid.Parse("c1ad0000-0000-4000-8000-000000000020"), "vantail extérieur", -1.4, 0, false),
                new StudioDoorAssemblyPanelDraft(
                    Guid.Parse("c1ad0000-0000-4000-8000-000000000021"), "vantail intérieur", 1.4, .2, true),
                new StudioDoorAssemblyPanelDraft(
                    Guid.Parse("c1ad0000-0000-4000-8000-000000000022"), "verrou central", 0, .4, false),
            };
            var draft = source with
            {
                AbilityTargetedEnabled = true,
                AbilityTargetedScriptId = "scripts/door_ad_ability_targeted",
                AbilityImpactedEnabled = true,
                AbilityImpactedScriptId = "scripts/door_ad_ability_impacted",
                HeartbeatEnabled = true,
                HeartbeatScriptId = "scripts/door_ad_heartbeat",
                CustomEvent = new(
                    true,
                    selectedCustom.EventName,
                    true,
                    "moon",
                    "signal",
                    "scripts/door_ad_moon_signal",
                    false,
                    true),
                DialogueId = dialogueId,
                FactionId = faction.Slug,
                AssemblyEnabled = true,
                AssemblyKind = DoorWorldProfile.AssemblyKind.Airlock,
                Panels = panels,
                Sequence = "extérieur puis verrou puis intérieur",
                Interlocked = true,
                FailureRecovery = "refermer le sas et libérer le verrou",
            };

            var beforePreview = editing.Project;
            var beforeRevision = history.State.Revision;
            var preview = editing.PreviewDoorReactiveComplexUx1AD(draft);
            var dryRun = preview.Succeeded && preview.CanApply && preview.EventSimulationAccepted &&
                         preview.NarrativeReferencesAccepted && preview.AssemblyAccepted &&
                         ReferenceEquals(beforePreview, editing.Project) &&
                         history.State.Revision == beforeRevision && !editing.IsDirty;

            var commandBuilt = editing.TryBuildDoorReactiveComplexUx1AD(
                draft,
                dryRun: true,
                out var dryRunCommand,
                out var builtProfile,
                out _);
            var commandPreview = dryRunCommand is null
                ? null
                : editing._setDoorCatalogUx1AD.Handle(history.State, dryRunCommand);
            var freshDraft = fresh with
            {
                AbilityImpactedEnabled = true,
                AbilityImpactedScriptId = "scripts/door_ad_fresh_impact",
                CustomEvent = new(true, null, true, "fresh", "signal", "scripts/door_ad_fresh_custom", false, true),
            };
            var freshBuilt = editing.TryBuildDoorReactiveComplexUx1AD(
                freshDraft,
                dryRun: true,
                out var freshCommand,
                out var freshProfile,
                out _);
            var expectedFreshOrder = loadedDoorOrder.Append(freshProfile?.Id ?? Guid.Empty).OrderBy(id => id);
            var canonicalCommand = commandBuilt && commandPreview?.Succeeded == true && builtProfile is not null &&
                                   dryRunCommand is { DryRun: true } &&
                                   dryRunCommand.Catalog.Doors.Select(profile => profile.Id)
                                       .SequenceEqual(loadedDoorOrder) &&
                                   freshBuilt && freshCommand is not null && freshProfile is not null &&
                                   freshCommand.Catalog.Doors.Select(profile => profile.Id)
                                       .SequenceEqual(expectedFreshOrder) &&
                                   freshProfile.Id == StableDoorIdUx1AD(freshBlueprint.Id, "profile") &&
                                   ReferenceEquals(beforePreview, editing.Project) &&
                                   history.State.Revision == beforeRevision;

            var expectedEventOrder = new[]
            {
                "damage-applied",
                AbilityTargetedEventUx1AD,
                "physical-attack-targeted",
                AbilityImpactedEventUx1AD,
                "custom:moon:signal",
                HeartbeatEventUx1AD,
                "entity-destroyed",
                "custom:ember:ward",
                "physical-attack-resolved",
                "unknown:door-audit",
            };
            var abilityEvents = builtProfile is { } abilityProfile &&
                                ResolveDoorEventUx1AD(
                                    abilityProfile,
                                    AbilityTargetedEventUx1AD,
                                    "scripts/door_ad_ability_targeted") &&
                                ResolveDoorEventUx1AD(
                                    abilityProfile,
                                    AbilityImpactedEventUx1AD,
                                    "scripts/door_ad_ability_impacted") &&
                                abilityProfile.Events.Single(binding =>
                                    binding.Event == AbilityTargetedEventUx1AD).Before &&
                                !abilityProfile.Events.Single(binding =>
                                    binding.Event == AbilityImpactedEventUx1AD).Before;
            var heartbeatAndCustom = builtProfile is { } reactiveProfile &&
                                     ResolveDoorEventUx1AD(
                                         reactiveProfile,
                                         HeartbeatEventUx1AD,
                                         "scripts/door_ad_heartbeat",
                                         elapsedSeconds: 1) &&
                                     !WorldFeatureRuntimeResolver.ResolveDoorEvent(
                                         reactiveProfile, HeartbeatEventUx1AD, 0, .5).Dispatched &&
                                     WorldFeatureRuntimeResolver.ResolveDoorEvent(
                                         reactiveProfile, HeartbeatEventUx1AD, 60, 2).Reason == "budget" &&
                                     ResolveDoorEventUx1AD(
                                         reactiveProfile,
                                         "custom:moon:signal",
                                         "scripts/door_ad_moon_signal");
            var narrativeReferences = builtProfile is
            {
                DialogueId: { } builtDialogue,
                FactionId: "ash-watch",
            } && builtDialogue == dialogueId;
            var complexAssembly = builtProfile?.Assembly is
            {
                Kind: DoorWorldProfile.AssemblyKind.Airlock,
                Interlocked: true,
                Sequence: "extérieur puis verrou puis intérieur",
                FailureRecovery: "refermer le sas et libérer le verrou",
            } builtAssembly && builtAssembly.Panels.Count == 3 &&
                                      builtAssembly.Panels[1] is
                                      {
                                          Role: "vantail intérieur",
                                          OpenOffset: 1.4,
                                          DelaySeconds: .2,
                                          Reversed: true,
                                      };
            var neighborEventMerge = builtProfile is { } mergedProfile &&
                                     mergedProfile.Events.Select(binding => binding.Event)
                                         .SequenceEqual(expectedEventOrder) &&
                                     preservedEvents.All(original => ReferenceEquals(
                                         original,
                                         mergedProfile.Events.Single(binding => binding.Event == original.Event))) &&
                                     mergedProfile.Events.Single(binding => binding.Event == "custom:ember:ward") is
                                     {
                                         ScriptId: "scripts/door_ember_neighbor",
                                         Before: false,
                                         Authoritative: true,
                                     };

            var staleBase = editing.Project;
            var approvalPreview = editing.PreviewDoorReactiveComplexUx1AD(draft);
            var staleCommandBuilt = editing.TryBuildDoorReactiveComplexUx1AD(
                draft,
                dryRun: true,
                out var staleCommand,
                out _,
                out _);
            var interveningBuilt = editing.TryBuildDoorReactiveComplexUx1AD(
                draft with { HeartbeatScriptId = "scripts/door_ad_intervening" },
                dryRun: false,
                out var interveningCommand,
                out _,
                out _);
            var intervening = interveningCommand is null
                ? null
                : editing._setDoorCatalogUx1AD.Handle(history.State, interveningCommand);
            if (intervening?.Succeeded == true)
            {
                history.Record(intervening);
            }
            var staleApply = editing.ApplyApprovedDoorReactiveComplexUx1AD();
            var staleCommandResult = staleCommand is null
                ? null
                : editing._setDoorCatalogUx1AD.Handle(history.State, staleCommand);
            var staleUndo = editing.Undo();
            var approvalRevisionGuard = approvalPreview.Succeeded && interveningBuilt &&
                                        intervening?.Succeeded == true && !staleApply.Succeeded &&
                                        staleApply.StaleRevision && staleUndo.Succeeded &&
                                        ReferenceEquals(staleBase, editing.Project) && !editing.IsDirty;
            var commandRevisionGuard = staleCommandBuilt && staleCommandResult is
            {
                Succeeded: false,
            } && staleCommandResult.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "command.revision_conflict");

            var invalidPreviews = new[]
            {
                editing.PreviewDoorReactiveComplexUx1AD(draft with { AbilityTargetedScriptId = string.Empty }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with
                {
                    CustomEvent = draft.CustomEvent with { Namespace = "moon:bad" },
                }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with
                {
                    CustomEvent = draft.CustomEvent with { Namespace = "ember", Name = "ward" },
                }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with { DialogueId = Guid.NewGuid() }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with { FactionId = "faction-absente" }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with { Panels = [] }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with
                {
                    Panels = [panels[0], panels[1] with { Id = panels[0].Id }],
                }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with
                {
                    Panels = [panels[0] with { DelaySeconds = -1 }],
                }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with
                {
                    Panels = [panels[0] with { OpenOffset = 101 }],
                }),
                editing.PreviewDoorReactiveComplexUx1AD(draft with { Sequence = string.Empty }),
            };
            var invalidBlocked = invalidPreviews.All(result => !result.Succeeded && !result.CanApply) &&
                                 ReferenceEquals(staleBase, editing.Project) && !editing.IsDirty;
            var humanDiagnostics = invalidPreviews.All(result =>
                IsHumanDoorReactiveComplexMessageUx1AD(result.Message));

            var approved = editing.PreviewDoorReactiveComplexUx1AD(draft);
            var apply = editing.ApplyApprovedDoorReactiveComplexUx1AD();
            var appliedProject = editing.Project;
            var appliedProfile = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var appliedNeighbor = appliedProject?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == neighborBlueprint.Id);
            var preservation = approved.Succeeded && apply.Succeeded && appliedProject is { } preservedProject &&
                               appliedProfile is { } preservedProfile &&
                               preservedProfile.Id == loadedOriginal.Id &&
                               preservedProfile.SchemaVersion == loadedOriginal.SchemaVersion &&
                               preservedProfile.Provenance == loadedOriginal.Provenance &&
                               ReferenceEquals(preservedProfile.Damage, loadedOriginal.Damage) &&
                               ReferenceEquals(preservedProfile.Loading, loadedOriginal.Loading) &&
                               ReferenceEquals(preservedProfile.Foundation, loadedOriginal.Foundation) &&
                               ReferenceEquals(preservedProfile.AccessEvents, loadedOriginal.AccessEvents) &&
                               preservedProfile.AccessEventsRequiredAtRuntime ==
                               loadedOriginal.AccessEventsRequiredAtRuntime &&
                               ReferenceEquals(appliedNeighbor, loadedNeighbor) &&
                               preservedProject.Module.WorldFeatures.Doors.Select(profile => profile.Id)
                                   .SequenceEqual(loadedDoorOrder) &&
                               preservedEvents.All(original => ReferenceEquals(
                                   original,
                                   preservedProfile.Events.Single(binding => binding.Event == original.Event)));

            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(staleBase, editing.Project) && !editing.IsDirty;
            var redo = editing.Redo();
            var redoneProfile = editing.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var redone = redo.Succeeded && redoneProfile?.Assembly?.Kind == DoorWorldProfile.AssemblyKind.Airlock &&
                         redoneProfile.Events.Any(binding => binding.Event == "custom:moon:signal");
            var undoRedo = apply.Succeeded && undone && redone;

            var saved = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var persisted = reopened.Project?.Module.WorldFeatures.Doors.Single(profile =>
                profile.BlueprintId == targetBlueprint.Id);
            var persistence = saved.Succeeded && reopen.Succeeded && persisted is
            {
                DialogueId: { } persistedDialogue,
                FactionId: "ash-watch",
                Assembly:
                {
                    Kind: DoorWorldProfile.AssemblyKind.Airlock,
                    Interlocked: true,
                    Sequence: "extérieur puis verrou puis intérieur",
                    FailureRecovery: "refermer le sas et libérer le verrou",
                },
            } persistedProfile && persistedDialogue == dialogueId &&
                              persistedProfile.Assembly is { Panels.Count: 3 } &&
                              persistedProfile.Events.Select(binding => binding.Event)
                                  .SequenceEqual(expectedEventOrder) &&
                              persistedProfile.Events.Single(binding =>
                                  binding.Event == "custom:moon:signal") is
                              {
                                  ScriptId: "scripts/door_ad_moon_signal",
                                  Before: false,
                                  Authoritative: true,
                              } &&
                              persistedProfile.Events.Single(binding =>
                                  binding.Event == "custom:ember:ward") is
                              {
                                  ScriptId: "scripts/door_ember_neighbor",
                                  Before: false,
                                  Authoritative: true,
                              } &&
                              persistedProfile.Damage == loadedOriginal.Damage &&
                              persistedProfile.Loading == loadedOriginal.Loading &&
                              DoorPortalMatchesUx1AD(
                                  persistedProfile.Foundation?.Portal,
                                  loadedOriginal.Foundation?.Portal) &&
                              persistedProfile.Foundation?.Links.SequenceEqual(
                                  loadedOriginal.Foundation?.Links ?? []) == true &&
                              DoorNavigationMatchesUx1AD(
                                  persistedProfile.Foundation?.Navigation,
                                  loadedOriginal.Foundation?.Navigation) &&
                              persistedProfile.AccessEvents?.Id == loadedOriginal.AccessEvents?.Id;

            var succeeded = explicitSelection && duplicateHumanLabels && unsafeAssemblyBlocked &&
                            abilityEvents && heartbeatAndCustom && narrativeReferences && complexAssembly &&
                            neighborEventMerge && dryRun && approvalRevisionGuard && commandRevisionGuard &&
                            canonicalCommand && preservation && undoRedo && persistence && invalidBlocked &&
                            humanDiagnostics;
            return new(
                succeeded,
                explicitSelection,
                duplicateHumanLabels,
                unsafeAssemblyBlocked,
                abilityEvents,
                heartbeatAndCustom,
                narrativeReferences,
                complexAssembly,
                neighborEventMerge,
                dryRun,
                approvalRevisionGuard,
                commandRevisionGuard,
                canonicalCommand,
                preservation,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AD_CONTRACT_FAILED " + exception);
            return FailedUx1ADSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private bool TryBuildDoorReactiveComplexUx1AD(
        StudioDoorReactiveComplexDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out DoorWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Ouvrez un module avant de configurer une porte.";
            return false;
        }
        if (project.Blueprints.All(blueprint =>
                blueprint.Id != draft.BlueprintId || blueprint.Kind != BlueprintKind.Door))
        {
            message = "La porte sélectionnée n’est plus disponible dans le module.";
            return false;
        }

        var catalog = project.Module.WorldFeatures;
        var matches = catalog.Doors.Where(candidate => candidate.BlueprintId == draft.BlueprintId).ToArray();
        if (matches.Length > 1)
        {
            message = "Plusieurs fiches ciblent cette porte · corrigez le module avant l’édition.";
            return false;
        }
        var existing = matches.SingleOrDefault();
        if (!ValidateDoorReactiveComplexDraftUx1AD(project, existing, draft, out message))
        {
            return false;
        }

        try
        {
            var events = MergeDoorEventsUx1AD(existing?.Events ?? [], draft);
            var assembly = draft.AssemblyEnabled
                ? new DoorWorldProfile.AssemblyPolicy(
                    draft.AssemblyKind,
                    draft.Panels.Select(panel => new DoorWorldProfile.Panel(
                        panel.Id,
                        panel.Role.Trim(),
                        panel.OpenOffset,
                        panel.DelaySeconds,
                        panel.Reversed)).ToArray(),
                    draft.Sequence.Trim(),
                    draft.Interlocked,
                    draft.FailureRecovery.Trim())
                : null;
            var builtProfile = existing is null
                ? new DoorWorldProfile(
                    StableDoorIdUx1AD(draft.BlueprintId, "profile"),
                    WorldFeatureCatalog.CurrentSchemaVersion,
                    Ux1ADProvenance,
                    draft.BlueprintId,
                    null,
                    null,
                    events,
                    draft.DialogueId,
                    NormalizeOptionalUx1AD(draft.FactionId),
                    assembly)
                : existing with
                {
                    Events = events,
                    DialogueId = draft.DialogueId,
                    FactionId = NormalizeOptionalUx1AD(draft.FactionId),
                    Assembly = assembly,
                };
            profile = builtProfile;
            var doors = existing is null
                ? catalog.Doors.Append(builtProfile).ToArray()
                : catalog.Doors.Select(candidate => candidate.Id == existing.Id ? builtProfile : candidate).ToArray();
            var candidateCatalog = new WorldFeatureCatalog(
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
                catalog.Scripts);
            command = new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                candidateCatalog,
                dryRun);
            return true;
        }
        catch (ArgumentException)
        {
            message = "Les réactions, références ou panneaux sont incomplets · vérifiez les champs signalés.";
            return false;
        }
    }

    private static bool ValidateDoorReactiveComplexDraftUx1AD(
        ProjectDefinition project,
        DoorWorldProfile? existing,
        StudioDoorReactiveComplexDraft draft,
        out string message)
    {
        message = string.Empty;
        foreach (var (enabled, script, label) in new[]
                 {
                     (draft.AbilityTargetedEnabled, draft.AbilityTargetedScriptId, "capacité ciblée"),
                     (draft.AbilityImpactedEnabled, draft.AbilityImpactedScriptId, "capacité reçue"),
                     (draft.HeartbeatEnabled, draft.HeartbeatScriptId, "mise à jour périodique"),
                 })
        {
            if (enabled && string.IsNullOrWhiteSpace(script))
            {
                message = $"Choisissez le script de la réaction {label}.";
                return false;
            }
        }

        var custom = draft.CustomEvent;
        if (custom.Touched)
        {
            if (!custom.Enabled && custom.OriginalEvent is null)
            {
                message = "Choisissez un événement personnalisé existant avant de le désactiver.";
                return false;
            }
            if (custom.OriginalEvent is { } original)
            {
                var occurrences = existing?.Events.Count(binding =>
                    string.Equals(binding.Event, original, StringComparison.Ordinal)) ?? 0;
                if (!IsCustomDoorEventUx1AD(original) || occurrences != 1)
                {
                    message = "L’événement personnalisé choisi n’est plus disponible sans ambiguïté.";
                    return false;
                }
            }
            if (custom.Enabled)
            {
                if (!IsSafeCustomSegmentUx1AD(custom.Namespace) || !IsSafeCustomSegmentUx1AD(custom.Name))
                {
                    message = "Le domaine et le nom personnalisés acceptent lettres, chiffres, point, tiret et soulignement.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(custom.ScriptId))
                {
                    message = "Choisissez le script de l’événement personnalisé.";
                    return false;
                }
                var targetName = custom.EventName;
                var collision = existing?.Events.Any(binding =>
                    string.Equals(binding.Event, targetName, StringComparison.Ordinal) &&
                    !string.Equals(binding.Event, custom.OriginalEvent, StringComparison.Ordinal)) == true;
                if (collision)
                {
                    message = "Un autre événement personnalisé utilise déjà ce domaine et ce nom.";
                    return false;
                }
            }
        }

        if (draft.DialogueId is { } dialogueId && project.Dialogues.All(dialogue =>
                dialogue.Id.Value != dialogueId))
        {
            message = "Choisissez une conversation disponible dans le module.";
            return false;
        }
        var factionId = NormalizeOptionalUx1AD(draft.FactionId);
        if (factionId is not null && project.Module.Factions.Factions.All(faction =>
                !string.Equals(faction.Slug, factionId, StringComparison.Ordinal)) &&
            !string.Equals(existing?.FactionId, factionId, StringComparison.Ordinal))
        {
            message = "Choisissez une faction disponible dans le module.";
            return false;
        }

        if (draft.AssemblyEnabled)
        {
            if (!Enum.IsDefined(draft.AssemblyKind))
            {
                message = "Choisissez un type d’assemblage disponible.";
                return false;
            }
            if (draft.Panels.Count is < 1 or > 8)
            {
                message = "Un assemblage doit comporter entre un et huit panneaux.";
                return false;
            }
            if (draft.Panels.Any(panel => panel.Id == Guid.Empty || string.IsNullOrWhiteSpace(panel.Role)) ||
                draft.Panels.Select(panel => panel.Id).Distinct().Count() != draft.Panels.Count)
            {
                message = "Chaque panneau doit avoir un rôle et une identité distincts.";
                return false;
            }
            if (draft.Panels.Any(panel => !double.IsFinite(panel.OpenOffset) ||
                                          panel.OpenOffset is < -100 or > 100 ||
                                          !double.IsFinite(panel.DelaySeconds) ||
                                          panel.DelaySeconds is < 0 or > 300))
            {
                message = "Les décalages doivent rester entre -100 et 100 m et les délais entre 0 et 300 secondes.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.Sequence))
            {
                message = "Décrivez l’ordre de mouvement des panneaux.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(draft.FailureRecovery))
            {
                message = "Décrivez la récupération prévue si un panneau échoue.";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateDoorAssemblyForEditorUx1AD(
        DoorWorldProfile? profile,
        out string message)
    {
        message = string.Empty;
        var assembly = profile?.Assembly;
        if (assembly is null)
        {
            return true;
        }
        if (assembly.Panels.Count > 8)
        {
            message = "Cet assemblage comporte plus de 8 panneaux et n’est pas éditable dans cette surface.";
            return false;
        }
        if (assembly.Panels.Any(panel => !double.IsFinite(panel.OpenOffset) ||
                                         panel.OpenOffset is < -100 or > 100))
        {
            message = "Cet assemblage contient un décalage hors des bornes de -100 à 100 m et n’est pas éditable.";
            return false;
        }
        if (assembly.Panels.Any(panel => !double.IsFinite(panel.DelaySeconds) ||
                                         panel.DelaySeconds is < 0 or > 300))
        {
            message = "Cet assemblage contient un délai hors des bornes de 0 à 300 s et n’est pas éditable.";
            return false;
        }
        return true;
    }

    private static IReadOnlyList<DoorWorldProfile.EventBinding> MergeDoorEventsUx1AD(
        IReadOnlyList<DoorWorldProfile.EventBinding> existing,
        StudioDoorReactiveComplexDraft draft)
    {
        var desired = new Dictionary<string, DoorWorldProfile.EventBinding>(StringComparer.Ordinal);
        if (draft.AbilityTargetedEnabled)
            desired[AbilityTargetedEventUx1AD] = new(
                AbilityTargetedEventUx1AD, draft.AbilityTargetedScriptId.Trim(), true, true);
        if (draft.AbilityImpactedEnabled)
            desired[AbilityImpactedEventUx1AD] = new(
                AbilityImpactedEventUx1AD, draft.AbilityImpactedScriptId.Trim(), false, true);
        if (draft.HeartbeatEnabled)
            desired[HeartbeatEventUx1AD] = new(
                HeartbeatEventUx1AD, draft.HeartbeatScriptId.Trim(), false, true);

        DoorWorldProfile.EventBinding? customReplacement = null;
        if (draft.CustomEvent is { Touched: true, Enabled: true } custom)
        {
            customReplacement = new(
                custom.EventName,
                custom.ScriptId.Trim(),
                custom.Before,
                custom.Authoritative);
        }

        var merged = new List<DoorWorldProfile.EventBinding>();
        var inserted = new HashSet<string>(StringComparer.Ordinal);
        var customInserted = false;
        foreach (var binding in existing)
        {
            if (IsManagedExactDoorEventUx1AD(binding.Event))
            {
                if (desired.TryGetValue(binding.Event, out var replacement) && inserted.Add(binding.Event))
                {
                    merged.Add(replacement);
                }
                continue;
            }
            if (draft.CustomEvent.Touched && draft.CustomEvent.OriginalEvent is { } original &&
                string.Equals(binding.Event, original, StringComparison.Ordinal))
            {
                if (customReplacement is not null && !customInserted)
                {
                    merged.Add(customReplacement);
                    customInserted = true;
                }
                continue;
            }
            merged.Add(binding);
        }
        foreach (var eventName in ManagedExactDoorEventsUx1AD)
        {
            if (desired.TryGetValue(eventName, out var binding) && inserted.Add(eventName))
            {
                merged.Add(binding);
            }
        }
        if (draft.CustomEvent is { Touched: true, OriginalEvent: null, Enabled: true } &&
            customReplacement is not null && !customInserted)
        {
            merged.Add(customReplacement);
        }
        return merged.ToArray();
    }

    private static bool PreviewDoorReactiveEventsUx1AD(
        DoorWorldProfile profile,
        StudioDoorReactiveComplexDraft draft)
    {
        var exactEvents = new[]
        {
            (draft.AbilityTargetedEnabled, AbilityTargetedEventUx1AD, draft.AbilityTargetedScriptId, 0d),
            (draft.AbilityImpactedEnabled, AbilityImpactedEventUx1AD, draft.AbilityImpactedScriptId, 0d),
            (draft.HeartbeatEnabled, HeartbeatEventUx1AD, draft.HeartbeatScriptId, 1d),
        };
        foreach (var (enabled, eventName, scriptId, elapsed) in exactEvents)
        {
            var matches = profile.Events.Where(binding => binding.Event == eventName).ToArray();
            if (!enabled)
            {
                if (matches.Length != 0) return false;
                continue;
            }
            if (matches.Length != 1 || !ResolveDoorEventUx1AD(profile, eventName, scriptId.Trim(), elapsed))
            {
                return false;
            }
        }
        if (draft.HeartbeatEnabled &&
            (WorldFeatureRuntimeResolver.ResolveDoorEvent(profile, HeartbeatEventUx1AD, 0, .5).Dispatched ||
             WorldFeatureRuntimeResolver.ResolveDoorEvent(profile, HeartbeatEventUx1AD, 60, 2).Dispatched))
        {
            return false;
        }

        var custom = draft.CustomEvent;
        if (!custom.Touched)
        {
            return true;
        }
        if (!custom.Enabled)
        {
            return custom.OriginalEvent is not null && profile.Events.All(binding =>
                !string.Equals(binding.Event, custom.OriginalEvent, StringComparison.Ordinal));
        }
        return profile.Events.Count(binding => binding.Event == custom.EventName) == 1 &&
               ResolveDoorEventUx1AD(profile, custom.EventName, custom.ScriptId.Trim());
    }

    private static bool ResolveDoorEventUx1AD(
        DoorWorldProfile profile,
        string eventName,
        string expectedScript,
        double elapsedSeconds = 0)
    {
        var result = WorldFeatureRuntimeResolver.ResolveDoorEvent(profile, eventName, 0, elapsedSeconds);
        var binding = profile.Events.SingleOrDefault(item => item.Event == eventName);
        return binding is not null && result.Dispatched && result.ScriptId == expectedScript &&
               result.Reason == (binding.Authoritative ? "server" : "local");
    }

    private static bool DoorAssemblyMatchesDraftUx1AD(
        DoorWorldProfile.AssemblyPolicy? assembly,
        StudioDoorReactiveComplexDraft draft)
    {
        if (!draft.AssemblyEnabled)
        {
            return assembly is null;
        }
        return assembly is not null && assembly.Kind == draft.AssemblyKind &&
               assembly.Sequence == draft.Sequence.Trim() && assembly.Interlocked == draft.Interlocked &&
               assembly.FailureRecovery == draft.FailureRecovery.Trim() &&
               assembly.Panels.Count == draft.Panels.Count &&
               assembly.Panels.Zip(draft.Panels).All(pair =>
                   pair.First.Id == pair.Second.Id && pair.First.Role == pair.Second.Role.Trim() &&
                   pair.First.OpenOffset == pair.Second.OpenOffset &&
                   pair.First.DelaySeconds == pair.Second.DelaySeconds &&
                   pair.First.Reversed == pair.Second.Reversed);
    }

    private static bool DoorPortalMatchesUx1AD(
        DoorWorldProfile.PortalPolicy? left,
        DoorWorldProfile.PortalPolicy? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.DestinationKind == right.DestinationKind && left.DestinationId == right.DestinationId &&
               left.SpawnAnchorId == right.SpawnAnchorId && left.ExitYawDegrees == right.ExitYawDegrees &&
               left.Preload == right.Preload && left.Permissions.SequenceEqual(right.Permissions);
    }

    private static bool DoorNavigationMatchesUx1AD(
        DoorWorldProfile.NavigationPolicy? left,
        DoorWorldProfile.NavigationPolicy? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.ObstacleId == right.ObstacleId && left.LinkId == right.LinkId &&
               left.BlockingStateIds.SequenceEqual(right.BlockingStateIds) &&
               left.TraversableStateIds.SequenceEqual(right.TraversableStateIds) &&
               left.OpeningPassableAt == right.OpeningPassableAt &&
               left.ClosingBlockedAt == right.ClosingBlockedAt &&
               left.VisualizeInEditor == right.VisualizeInEditor;
    }

    private static IEnumerable<string> EnabledDoorReactiveEventLabelsUx1AD(
        StudioDoorReactiveComplexDraft draft)
    {
        if (draft.AbilityTargetedEnabled) yield return "capacité ciblée";
        if (draft.AbilityImpactedEnabled) yield return "capacité reçue";
        if (draft.HeartbeatEnabled) yield return "mise à jour périodique limitée";
        if (draft.CustomEvent is { Touched: true, Enabled: true }) yield return "événement personnalisé choisi";
    }

    private static DoorWorldProfile.EventBinding? FindDoorEventUx1AD(
        DoorWorldProfile? profile,
        string eventName) => profile?.Events.FirstOrDefault(binding =>
        string.Equals(binding.Event, eventName, StringComparison.Ordinal));

    private static bool IsManagedExactDoorEventUx1AD(string eventName) =>
        ManagedExactDoorEventsUx1AD.Contains(eventName, StringComparer.Ordinal);

    private static bool IsCustomDoorEventUx1AD(string eventName) =>
        eventName.StartsWith(CustomPrefixUx1AD, StringComparison.Ordinal);

    private static bool TryParseCustomDoorEventUx1AD(
        string eventName,
        out string eventNamespace,
        out string name)
    {
        eventNamespace = string.Empty;
        name = string.Empty;
        if (!IsCustomDoorEventUx1AD(eventName))
        {
            return false;
        }
        var segments = eventName[CustomPrefixUx1AD.Length..].Split(':');
        if (segments.Length != 2 || !IsSafeCustomSegmentUx1AD(segments[0]) ||
            !IsSafeCustomSegmentUx1AD(segments[1]))
        {
            return false;
        }
        eventNamespace = segments[0];
        name = segments[1];
        return true;
    }

    private static bool IsSafeCustomSegmentUx1AD(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 64 &&
        value.Trim().All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');

    private static string? NormalizeOptionalUx1AD(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string HumanizeAssemblyKindUx1AD(DoorWorldProfile.AssemblyKind kind) => kind switch
    {
        DoorWorldProfile.AssemblyKind.Single => "simple",
        DoorWorldProfile.AssemblyKind.Double => "double",
        DoorWorldProfile.AssemblyKind.Portcullis => "herse",
        DoorWorldProfile.AssemblyKind.Elevator => "élévateur",
        DoorWorldProfile.AssemblyKind.Airlock => "sas",
        _ => "complexe",
    };

    private static Guid StableDoorIdUx1AD(BlueprintId blueprintId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{blueprintId.Value:D}:ux1-ad:{purpose}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static StudioDoorReactiveComplexPreview FailedDoorReactiveComplexPreviewUx1AD(string message) =>
        new(false, message, string.Empty, false);

    private static StudioUx1ADSmokeResult FailedUx1ADSmoke() => new(
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

    private static string HumanizeDoorReactiveComplexDiagnosticUx1AD(string? code) => code switch
    {
        "command.revision_conflict" => "Le module a changé · relancez l’aperçu.",
        "world.event.binding_invalid" => "Chaque réaction activée exige un script nommé.",
        "world.reference.dialogue_missing" => "La conversation choisie n’est plus disponible.",
        "world.door.assembly_invalid" => "L’assemblage exige des panneaux distincts et un ordre de mouvement.",
        "world.reference.blueprint_missing" => "La porte sélectionnée n’est plus disponible.",
        _ => "Les réactions, références ou panneaux ne respectent pas le module.",
    };

    private static bool IsHumanDoorReactiveComplexMessageUx1AD(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("world.door", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/worldFeatures", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
        !Guid.TryParse(message, out _);
}
