using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux1ACProvenance = "studio:ux1-ac";
    private readonly SetWorldFeatureCatalogHandler _setTriggerAdvancedCatalogUx1AC = new(new SystemClock());
    private StudioTriggerAdvancedDraft? _approvedTriggerAdvancedUx1AC;
    private ProjectRevision? _approvedTriggerAdvancedRevisionUx1AC;

    internal IReadOnlyList<StudioTriggerAdvancedChoice> TriggerAdvancedChoicesUx1AC
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
                        : item.Advanced is null ? "profil avancé à créer" : "profil avancé configuré";
                    return new StudioTriggerAdvancedChoice(
                        item.Id,
                        item.BlueprintId,
                        $"{name} · {instances} instance(s) · {state}",
                        item.Foundation is not null,
                        item.Advanced is not null);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioTriggerFactionChoice> TriggerFactionChoicesUx1AC =>
        Project?.Module.Factions.Factions
            .Where(item => !item.Archived)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioTriggerFactionChoice(item.Id, item.DisplayName))
            .ToArray() ?? [];

    internal ProjectDefinition? CreateUx1ACEvidenceProject()
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
            ids = Enumerable.Range(0, 6)
                .Select(index => StableTriggerAdvancedIdUx1AC(
                    project.Id.Value,
                    $"evidence-{area.Id.Value:N}-{salt}-{index}"))
                .ToArray();
            salt++;
        } while (ids.Distinct().Count() != ids.Length || ids.Any(occupied.Contains));

        var slugBase = "ux1-ac-evidence-trigger";
        var slug = slugBase;
        var slugIndex = 1;
        var existingSlugs = project.Blueprints.Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        while (existingSlugs.Contains(slug))
        {
            slug = $"{slugBase}-{slugIndex++}";
        }

        var blueprint = new BlueprintDefinition(
            BlueprintId.From(ids[0]),
            slug,
            "Déclencheur de preuve UX1-AC",
            BlueprintKind.Trigger);
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            ids[2],
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1ACProvenance,
            slug,
            new TriggerWorldProfile.ShapePolicy(
                TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                [],
                new Vector3Value(4, 3, 4),
                3,
                .25,
                true),
            new TriggerWorldProfile.FilterPolicy(
                ["creature"],
                ["player"],
                [],
                ["characters"]),
            [],
            0,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new TriggerWorldProfile.ActionPolicy(
                ids[3],
                "script",
                "trigger:ux1-ac-evidence",
                new Dictionary<string, string>(StringComparer.Ordinal))],
            null,
            new TriggerWorldProfile.TransformPolicy(
                Vector3Value.Zero,
                Vector3Value.Zero,
                Vector3Value.One,
                null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["editor.overlay"] = "volume",
                ["evidence.scope"] = "isolated",
            },
            new TriggerWorldProfile.EventPolicy(
                ids[4],
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1ACProvenance,
                "1.0.0",
                "VolumeEntered",
                null,
                null),
            new TriggerWorldProfile.EventPolicy(
                ids[5],
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1ACProvenance,
                "1.0.0",
                "VolumeExited",
                null,
                null));
        var profile = new TriggerWorldProfile(
            ids[1],
            WorldFeatureCatalog.CurrentSchemaVersion,
            Ux1ACProvenance,
            blueprint.Id,
            area.Id,
            null,
            null,
            null,
            "trigger.ux1-ac-evidence",
            null,
            null,
            null,
            null,
            null,
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

    internal StudioTriggerAdvancedDraft? CreateTriggerAdvancedDraftUx1AC(Guid? requestedProfileId = null)
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
        if (matches.Length != 1 || matches[0].Foundation is null)
        {
            return null;
        }

        var profile = matches[0];
        var advanced = profile.Advanced;
        return new StudioTriggerAdvancedDraft(
            profile.Id,
            profile.BlueprintId,
            profile.AreaId,
            history.State.Revision,
            advanced?.Id ?? StableTriggerAdvancedIdUx1AC(profile.Id, "advanced"),
            advanced?.ForgeScriptApiVersion ?? "1.0.0",
            profile.AdvancedRequiredAtRuntime || advanced is null,
            advanced?.Trap ?? DefaultTriggerTrapUx1AC(profile.Id),
            advanced?.TargetFilter ?? DefaultTriggerQueryUx1AC(profile.Id),
            advanced?.GameplayVolumes.Select(CopyTriggerVolumeUx1AC).ToArray() ??
                DefaultTriggerVolumesUx1AC(profile.Id),
            advanced?.OccupancyDebug ?? new TriggerOccupancyDebugProfile(
                StableTriggerAdvancedIdUx1AC(profile.Id, "occupancy"),
                true,
                true,
                true,
                true,
                true,
                true,
                100),
            profile,
            advanced);
    }

    internal StudioTriggerAdvancedPreview PreviewTriggerAdvancedUx1AC(StudioTriggerAdvancedDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTriggerAdvancedUx1AC();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedTriggerAdvancedPreviewUx1AC("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedTriggerAdvancedPreviewUx1AC(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez le déclencheur avant de prévisualiser.");
        }
        if (!TryBuildTriggerAdvancedUx1AC(draft, dryRun: true, out var command, out var profile,
                out var message))
        {
            return FailedTriggerAdvancedPreviewUx1AC(message);
        }

        var result = _setTriggerAdvancedCatalogUx1AC.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            return FailedTriggerAdvancedPreviewUx1AC(
                HumanizeTriggerAdvancedUx1AC(result.Diagnostics.FirstOrDefault()?.Code));
        }

        var adapter = new TriggerAdvancedRuntimeAdapter();
        var resolution = adapter.Resolve(profile!);
        if (!resolution.Available || resolution.Handle is null)
        {
            return FailedTriggerAdvancedPreviewUx1AC(
                "Le profil avancé ne peut pas être préparé pour le runtime.");
        }
        var runtime = adapter.Build(resolution.Handle, profile!);
        var trap = runtime.Profile.Trap;
        var query = runtime.Profile.TargetFilter;
        var debug = runtime.Profile.OccupancyDebug;
        var summary =
            $"Piège : détection {trap.DetectionDifficulty:0.##} · désamorçage {trap.DisarmDifficulty:0.##} · réarmement {trap.ResetDelaySeconds:0.##} s · aperçu MJ {(trap.GameMasterVisualization ? "visible" : "masqué")}\n" +
            $"Filtre : {(query.PlayersOnly ? "joueurs uniquement" : "toutes les entités")} · {query.RequiredTags.Count} tag(s) requis · {query.FactionIds.Count} faction(s) · {query.RequiredComponents.Count} composant(s)\n" +
            $"Volumes : {runtime.GameplayVolumes.Count}/10 actifs · liaisons d’entrée et de sortie typées\n" +
            $"Occupation : occupants, déclenchements, délais, filtres et trace · maximum {debug.MaximumTraceEntries} entrée(s)\n" +
            "Aperçu uniquement : volume de base, portail, validation et déclencheurs voisins préservés.";

        _approvedTriggerAdvancedUx1AC = draft;
        _approvedTriggerAdvancedRevisionUx1AC = history.State.Revision;
        return new StudioTriggerAdvancedPreview(
            true,
            "Aperçu validé · aucun document n’a été modifié.",
            summary,
            true);
    }

    internal StudioUx1ACActionResult ApplyApprovedTriggerAdvancedUx1AC()
    {
        var draft = _approvedTriggerAdvancedUx1AC;
        var history = History;
        if (draft is null || history is null || _approvedTriggerAdvancedRevisionUx1AC != history.State.Revision)
        {
            CancelTriggerAdvancedUx1AC();
            return new StudioUx1ACActionResult(
                false,
                "Prévisualisez de nouveau le profil avancé avant de l’appliquer.");
        }
        if (!TryBuildTriggerAdvancedUx1AC(draft, dryRun: false, out var command, out _, out var message))
        {
            CancelTriggerAdvancedUx1AC();
            return new StudioUx1ACActionResult(false, message);
        }

        var result = _setTriggerAdvancedCatalogUx1AC.Handle(history.State, command!);
        CancelTriggerAdvancedUx1AC();
        if (!result.Succeeded)
        {
            return new StudioUx1ACActionResult(
                false,
                HumanizeTriggerAdvancedUx1AC(result.Diagnostics.FirstOrDefault()?.Code));
        }

        history.Record(result);
        InvalidateContentIndexes();
        return new StudioUx1ACActionResult(
            true,
            "Profil avancé appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelTriggerAdvancedUx1AC()
    {
        _approvedTriggerAdvancedUx1AC = null;
        _approvedTriggerAdvancedRevisionUx1AC = null;
    }

    private bool TryBuildTriggerAdvancedUx1AC(
        StudioTriggerAdvancedDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out TriggerWorldProfile? profile,
        out string message)
    {
        command = null;
        profile = null;
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
        if (!ValidateTriggerAdvancedDraftUx1AC(draft, out message))
        {
            return false;
        }

        var catalog = project.Module.WorldFeatures;
        var currentProfile = catalog.Triggers.SingleOrDefault(item =>
            item.Id == draft.TriggerProfileId &&
            item.BlueprintId == draft.BlueprintId &&
            item.AreaId == draft.AreaId);
        if (currentProfile is null)
        {
            message = "Le déclencheur a changé depuis son ouverture. Rechargez sa fiche.";
            return false;
        }
        if (currentProfile.Foundation is null)
        {
            message =
                "Configurez d’abord le volume de base avec l’outil Déclencheur avant d’ajouter piège et volumes.";
            return false;
        }

        var trap = draft.Trap;
        var query = draft.TargetFilter with
        {
            RequiredTags = draft.TargetFilter.RequiredTags.ToArray(),
            ExcludedTags = draft.TargetFilter.ExcludedTags.ToArray(),
            FactionIds = draft.TargetFilter.FactionIds.ToArray(),
            RequiredComponents = draft.TargetFilter.RequiredComponents.ToArray(),
            OwnerId = NullIfWhiteSpaceUx1AC(draft.TargetFilter.OwnerId),
            TeamId = NullIfWhiteSpaceUx1AC(draft.TargetFilter.TeamId),
            SandboxPredicate = NullIfWhiteSpaceUx1AC(draft.TargetFilter.SandboxPredicate),
        };
        var volumes = draft.GameplayVolumes.Select(item => item with
        {
            NumericParameters = item.NumericParameters.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            TextParameters = item.TextParameters.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            EnterBinding = item.EnterBinding,
            ExitBinding = item.ExitBinding,
        }).ToArray();
        var original = currentProfile.Advanced;
        var advanced = original is null
            ? new TriggerAdvancedProfile(
                draft.AdvancedId,
                WorldFeatureCatalog.CurrentSchemaVersion,
                Ux1ACProvenance,
                draft.ForgeScriptApiVersion,
                trap,
                query,
                volumes,
                draft.OccupancyDebug)
            : original with
            {
                ForgeScriptApiVersion = draft.ForgeScriptApiVersion,
                Trap = trap,
                TargetFilter = query,
                GameplayVolumes = volumes,
                OccupancyDebug = draft.OccupancyDebug,
            };
        var builtProfile = currentProfile with
        {
            Advanced = advanced,
            AdvancedRequiredAtRuntime = draft.RequireRuntime,
        };
        profile = builtProfile;
        var candidateCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            catalog.Placeables,
            catalog.Doors,
            catalog.Triggers.Select(item => item.Id == builtProfile.Id ? builtProfile : item).ToArray(),
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

    private static bool ValidateTriggerAdvancedDraftUx1AC(
        StudioTriggerAdvancedDraft draft,
        out string message)
    {
        message = string.Empty;
        if (draft.AdvancedId == Guid.Empty || string.IsNullOrWhiteSpace(draft.ForgeScriptApiVersion))
        {
            message = "La version ForgeScript du profil avancé doit être une version lisible.";
            return false;
        }

        var trap = draft.Trap;
        var bindings = new[]
        {
            trap.DetectedBinding,
            trap.DisarmedBinding,
            trap.TriggeredBinding,
            trap.ResetBinding,
        };
        if (trap.Id == Guid.Empty || string.IsNullOrWhiteSpace(trap.TriggerId) ||
            string.IsNullOrWhiteSpace(trap.PayloadId) || !FiniteNonNegativeUx1AC(trap.DetectionDifficulty) ||
            !FiniteNonNegativeUx1AC(trap.DisarmDifficulty) ||
            !FiniteNonNegativeUx1AC(trap.ResetDelaySeconds) || string.IsNullOrWhiteSpace(trap.OwnerId) ||
            string.IsNullOrWhiteSpace(trap.VisualEffectId) || string.IsNullOrWhiteSpace(trap.FriendlyPolicy) ||
            !ValidColorUx1AC(trap.VisualizationColor) || bindings.Any(string.IsNullOrWhiteSpace) ||
            bindings.Distinct(StringComparer.Ordinal).Count() != bindings.Length)
        {
            message =
                "Le piège exige une charge, des seuils positifs, un propriétaire, un effet, une couleur et quatre événements distincts.";
            return false;
        }

        var query = draft.TargetFilter;
        var queryLists = new IReadOnlyList<string>[]
        {
            query.RequiredTags,
            query.ExcludedTags,
            query.RequiredComponents,
        };
        if (query.Id == Guid.Empty || queryLists.Any(InvalidNamedListUx1AC) ||
            query.RequiredTags.Intersect(query.ExcludedTags, StringComparer.Ordinal).Any() ||
            query.FactionIds.Any(item => item == Guid.Empty) ||
            query.FactionIds.Distinct().Count() != query.FactionIds.Count ||
            query.OwnerId is { Length: 0 } || query.TeamId is { Length: 0 } ||
            !ValidSandboxPredicateUx1AC(query.SandboxPredicate))
        {
            message =
                "Le filtre de cible contient une valeur vide, dupliquée ou contradictoire. Le prédicat attendu suit la forme clé==valeur.";
            return false;
        }

        var kinds = Enum.GetValues<TriggerGameplayVolumeKind>();
        var volumes = draft.GameplayVolumes;
        if (volumes.Count != kinds.Length || volumes.Select(item => item.Id).Distinct().Count() != volumes.Count ||
            volumes.Select(item => item.Kind).Distinct().Count() != kinds.Length ||
            kinds.Any(kind => volumes.All(item => item.Kind != kind)) ||
            volumes.Any(item => item.Id == Guid.Empty || !item.Enabled ||
                item.NumericParameters.Count + item.TextParameters.Count == 0 ||
                item.NumericParameters.Any(pair => string.IsNullOrWhiteSpace(pair.Key) ||
                                                   !double.IsFinite(pair.Value)) ||
                item.TextParameters.Any(pair => string.IsNullOrWhiteSpace(pair.Key) ||
                                                string.IsNullOrWhiteSpace(pair.Value)) ||
                !string.Equals(item.EnterBinding, $"{item.Kind}VolumeEntered", StringComparison.Ordinal) ||
                !string.Equals(item.ExitBinding, $"{item.Kind}VolumeExited", StringComparison.Ordinal)))
        {
            message =
                "La bibliothèque doit conserver les dix volumes actifs, leurs paramètres et leurs événements typés.";
            return false;
        }

        var debug = draft.OccupancyDebug;
        if (debug.Id == Guid.Empty || !debug.Enabled || !debug.ShowOccupants || !debug.ShowLastFires ||
            !debug.ShowCooldowns || !debug.ShowFilters || !debug.ShowEventTrace ||
            debug.MaximumTraceEntries is < 1 or > 1000)
        {
            message =
                "La visualisation d’occupation doit garder ses cinq informations actives et une trace limitée de 1 à 1000 entrées.";
            return false;
        }

        var componentIds = new[] { draft.AdvancedId, trap.Id, query.Id, debug.Id }
            .Concat(volumes.Select(item => item.Id))
            .ToArray();
        if (componentIds.Any(item => item == Guid.Empty) ||
            componentIds.Distinct().Count() != componentIds.Length ||
            draft.OriginalProfile.Foundation is { } originalFoundation &&
            componentIds.Contains(originalFoundation.Id))
        {
            message = "Les composants avancés doivent conserver des identités distinctes.";
            return false;
        }
        if (draft.OriginalAdvanced is { } original &&
            (draft.AdvancedId != original.Id || trap.Id != original.Trap.Id ||
             query.Id != original.TargetFilter.Id || debug.Id != original.OccupancyDebug.Id ||
             original.GameplayVolumes.Any(item =>
                 volumes.SingleOrDefault(candidate => candidate.Kind == item.Kind)?.Id != item.Id)))
        {
            message = "Les identités internes du profil avancé ne peuvent pas être remplacées depuis cette fiche.";
            return false;
        }
        return true;
    }

    private static TriggerTrapProfile DefaultTriggerTrapUx1AC(Guid profileId) => new(
        StableTriggerAdvancedIdUx1AC(profileId, "trap"),
        "enter",
        "damage:trigger",
        10,
        12,
        30,
        "module",
        "vfx:trap",
        "owner-safe",
        true,
        false,
        true,
        "#ff7a45",
        "TrapDetected",
        "TrapDisarmed",
        "TrapTriggered",
        "TrapReset");

    private static TriggerEntityQueryProfile DefaultTriggerQueryUx1AC(Guid profileId) => new(
        StableTriggerAdvancedIdUx1AC(profileId, "query"),
        true,
        ["player"],
        [],
        [],
        [],
        null,
        null,
        null);

    private static IReadOnlyList<TriggerGameplayVolumeProfile> DefaultTriggerVolumesUx1AC(Guid profileId) =>
        Enum.GetValues<TriggerGameplayVolumeKind>()
            .Select(kind =>
            {
                var parameter = PrimaryTriggerVolumeParameterUx1AC(kind);
                return new TriggerGameplayVolumeProfile(
                    StableTriggerAdvancedIdUx1AC(profileId, $"volume-{kind}"),
                    kind,
                    new Dictionary<string, double>(StringComparer.Ordinal) { [parameter.Key] = parameter.Value },
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    $"{kind}VolumeEntered",
                    $"{kind}VolumeExited",
                    true);
            })
            .ToArray();

    internal static KeyValuePair<string, double> PrimaryTriggerVolumeParameterUx1AC(
        TriggerGameplayVolumeKind kind) => kind switch
        {
            TriggerGameplayVolumeKind.Damage => new("health", -10),
            TriggerGameplayVolumeKind.Heal => new("health", 10),
            TriggerGameplayVolumeKind.Gravity => new("gravity", .5),
            TriggerGameplayVolumeKind.Water => new("movement", .6),
            TriggerGameplayVolumeKind.Climb => new("climb", 1),
            TriggerGameplayVolumeKind.NoBuild => new("build", 0),
            TriggerGameplayVolumeKind.Camera => new("camera", 1),
            TriggerGameplayVolumeKind.Audio => new("audio", 1),
            TriggerGameplayVolumeKind.Weather => new("weather", 1),
            TriggerGameplayVolumeKind.Checkpoint => new("checkpoint", 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static TriggerGameplayVolumeProfile CopyTriggerVolumeUx1AC(
        TriggerGameplayVolumeProfile item) => item with
        {
            NumericParameters = item.NumericParameters.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            TextParameters = item.TextParameters.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
        };

    private static Guid StableTriggerAdvancedIdUx1AC(Guid profileId, string segment)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux1-ac:{profileId:N}:{segment}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }

    private static bool FiniteNonNegativeUx1AC(double value) => double.IsFinite(value) && value >= 0;

    private static bool ValidColorUx1AC(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith('#') && value.Length is 4 or 7 or 9;

    private static bool InvalidNamedListUx1AC(IReadOnlyList<string> values) =>
        values.Any(string.IsNullOrWhiteSpace) ||
        values.Distinct(StringComparer.Ordinal).Count() != values.Count;

    private static bool ValidSandboxPredicateUx1AC(string? value)
    {
        if (value is null)
        {
            return true;
        }
        var parts = value.Split("==", StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts.All(item => !string.IsNullOrWhiteSpace(item));
    }

    private static string? NullIfWhiteSpaceUx1AC(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static StudioTriggerAdvancedPreview FailedTriggerAdvancedPreviewUx1AC(string message) =>
        new(false, message, string.Empty, false);

    private static string HumanizeTriggerAdvancedUx1AC(string? code) => code switch
    {
        "command.revision_conflict" =>
            "Le projet a changé. Rechargez puis prévisualisez de nouveau le profil avancé.",
        "world.trigger.advanced_foundation_missing" or "world.trigger.foundation_required_missing" =>
            "Configurez d’abord le volume de base avec l’outil Déclencheur.",
        "world.trigger.trap_invalid" =>
            "Le piège contient une charge, un seuil, une couleur ou une liaison invalide.",
        "world.trigger.target_filter_invalid" =>
            "Le filtre de cible contient une valeur vide, dupliquée ou contradictoire.",
        "world.trigger.gameplay_volumes_invalid" =>
            "Les dix volumes de gameplay doivent rester actifs, paramétrés et correctement liés.",
        "world.trigger.occupancy_debug_invalid" =>
            "La visualisation d’occupation doit rester complète et sa trace doit être bornée.",
        "world.trigger.advanced_uuid_duplicate" =>
            "Deux composants avancés partagent la même identité.",
        _ =>
            "Le profil avancé ne peut pas être prévisualisé. Vérifiez les champs nommés.",
    };

    private void CloseUx1AC() => CancelTriggerAdvancedUx1AC();
}

internal sealed partial class StudioSessionController
{
    internal StudioUx1ACSmokeResult RunUx1ACContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ac-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var targetBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("ac100000-0000-4000-8000-000000000001")),
                "piege-des-cendres",
                "Piège des cendres",
                BlueprintKind.Trigger);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("ac100000-0000-4000-8000-000000000002")),
                "brume-du-sanctuaire",
                "Brume du sanctuaire",
                BlueprintKind.Trigger);
            var foundationlessBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("ac100000-0000-4000-8000-000000000003")),
                "seuil-inacheve",
                "Seuil inachevé",
                BlueprintKind.Trigger);
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("ac100000-0000-4000-8000-000000000004")),
                "sanctuaire-des-cendres",
                "Sanctuaire des cendres",
                [],
                AreaKind.Interior,
                new AreaDimensions(48, 48));
            var shell = new ProjectDefinition(
                ProjectId.From(Guid.Parse("ac100000-0000-4000-8000-000000000005")),
                "Studio UX1-AC",
                [targetBlueprint, neighborBlueprint, foundationlessBlueprint],
                [area]);
            var targetProfile = CreateUx1ACSmokeTrigger(
                targetBlueprint.Id,
                area.Id,
                "ac100000-0000-4000-8100",
                advanced: false,
                foundation: true);
            var neighborProfile = CreateUx1ACSmokeTrigger(
                neighborBlueprint.Id,
                area.Id,
                "ac100000-0000-4000-8200",
                advanced: true,
                foundation: true);
            var foundationlessProfile = CreateUx1ACSmokeTrigger(
                foundationlessBlueprint.Id,
                area.Id,
                "ac100000-0000-4000-8300",
                advanced: false,
                foundation: false);
            var catalog = new WorldFeatureCatalog(
                shell.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-ac",
                triggers: [targetProfile, neighborProfile, foundationlessProfile]);
            var project = new ProjectDefinition(
                shell.Id,
                shell.Title,
                shell.Module.WithWorldFeatures(catalog),
                shell.Blueprints,
                shell.Areas,
                shell.Dialogues,
                shell.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1ACSmoke();
            }

            var initialProject = editing.Project!;
            var initialCatalog = initialProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var loadedNeighbor = initialCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            var selectionExplicit = editing.CreateTriggerAdvancedDraftUx1AC() is null &&
                                    editing.TriggerAdvancedChoicesUx1AC.Count == 3;
            var foundationRequired = editing.CreateTriggerAdvancedDraftUx1AC(foundationlessProfile.Id) is null &&
                                     editing.TriggerAdvancedChoicesUx1AC.Single(item =>
                                         item.ProfileId == foundationlessProfile.Id) is { HasFoundation: false };

            var staleDraft = editing.CreateTriggerAdvancedDraftUx1AC(targetProfile.Id)!;
            var firstIntermediate = ApplyUx1ACIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-before-preview");
            var staleDraftPreview = editing.PreviewTriggerAdvancedUx1AC(staleDraft);
            var staleDraftBlocked = firstIntermediate && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé", StringComparison.Ordinal) &&
                                    editing.Undo().Succeeded;

            var factionId = Guid.Parse("ac100000-0000-4000-8400-000000000001");
            var draft = CustomizeUx1ACSmokeDraft(
                editing.CreateTriggerAdvancedDraftUx1AC(targetProfile.Id)!,
                factionId);
            var invalidTrap = editing.PreviewTriggerAdvancedUx1AC(draft with
            {
                Trap = draft.Trap with { DetectionDifficulty = -1 },
            });
            var invalidFilter = editing.PreviewTriggerAdvancedUx1AC(draft with
            {
                TargetFilter = draft.TargetFilter with { ExcludedTags = ["player", "undead"] },
            });
            var invalidVolume = editing.PreviewTriggerAdvancedUx1AC(draft with
            {
                GameplayVolumes = draft.GameplayVolumes
                    .Where(item => item.Kind != TriggerGameplayVolumeKind.Checkpoint)
                    .ToArray(),
            });
            var invalidDebug = editing.PreviewTriggerAdvancedUx1AC(draft with
            {
                OccupancyDebug = draft.OccupancyDebug with { ShowFilters = false },
            });
            var beforePreviewProject = editing.Project;
            var beforePreviewRevision = editing.History!.State.Revision;
            var preview = editing.PreviewTriggerAdvancedUx1AC(draft);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         ReferenceEquals(beforePreviewProject, editing.Project) &&
                         editing.History.State.Revision == beforePreviewRevision;

            var secondIntermediate = ApplyUx1ACIntermediateMutation(
                editing,
                neighborProfile.Id,
                "trigger.changed-after-preview");
            var staleApply = editing.ApplyApprovedTriggerAdvancedUx1AC();
            var staleApprovalBlocked = secondIntermediate && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var freshDraft = CustomizeUx1ACSmokeDraft(
                editing.CreateTriggerAdvancedDraftUx1AC(targetProfile.Id)!,
                factionId);
            var freshPreview = editing.PreviewTriggerAdvancedUx1AC(freshDraft);
            var apply = editing.ApplyApprovedTriggerAdvancedUx1AC();
            var appliedProject = editing.Project!;
            var appliedCatalog = appliedProject.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Triggers.Single(item => item.Id == targetProfile.Id);
            var appliedNeighbor = appliedCatalog.Triggers.Single(item => item.Id == neighborProfile.Id);
            var appliedAdvanced = appliedTarget.Advanced;
            if (!freshPreview.Succeeded || !apply.Succeeded || appliedAdvanced is null)
            {
                return FailedUx1ACSmoke();
            }

            var confirmedAdvanced = appliedAdvanced!;
            var advancedCreation = loadedTarget.Advanced is null && appliedTarget.AdvancedRequiredAtRuntime;
            var trap = confirmedAdvanced.Trap.DetectionDifficulty == 18 &&
                       confirmedAdvanced.Trap.DisarmDifficulty == 22 &&
                       confirmedAdvanced.Trap.ResetDelaySeconds == 45 &&
                       confirmedAdvanced.Trap.PayloadId == "damage:ember-burst" &&
                       confirmedAdvanced.Trap.GameMasterVisualization;
            var filter = confirmedAdvanced.TargetFilter.PlayersOnly &&
                         confirmedAdvanced.TargetFilter.RequiredTags.SequenceEqual(["player", "hero"]) &&
                         confirmedAdvanced.TargetFilter.FactionIds.SequenceEqual([factionId]) &&
                         confirmedAdvanced.TargetFilter.RequiredComponents.SequenceEqual(["health", "inventory"]) &&
                         confirmedAdvanced.TargetFilter.SandboxPredicate == "quest==active";
            var gameplayVolumes = confirmedAdvanced.GameplayVolumes.Count == 10 &&
                                  confirmedAdvanced.GameplayVolumes.Select(item => item.Kind).Distinct().Count() == 10 &&
                                  confirmedAdvanced.GameplayVolumes.Single(item =>
                                      item.Kind == TriggerGameplayVolumeKind.Damage)
                                      .NumericParameters["health"] == -17 &&
                                  confirmedAdvanced.GameplayVolumes.Single(item =>
                                      item.Kind == TriggerGameplayVolumeKind.Damage)
                                      .TextParameters["authored.note"] == "braises";
            var occupancy = confirmedAdvanced.OccupancyDebug is
            {
                Enabled: true,
                ShowOccupants: true,
                ShowLastFires: true,
                ShowCooldowns: true,
                ShowFilters: true,
                ShowEventTrace: true,
                MaximumTraceEntries: 3,
            };
            var foundationPreserved = ReferenceEquals(appliedTarget.Foundation, loadedTarget.Foundation);
            var portalPreserved = ReferenceEquals(
                appliedTarget.Foundation!.Portal,
                loadedTarget.Foundation!.Portal);
            var validationPreserved = ReferenceEquals(
                appliedTarget.Foundation.Validation,
                loadedTarget.Foundation.Validation);
            var neighborPreserved = ReferenceEquals(appliedNeighbor, loadedNeighbor) &&
                                    SameReferencesUx1AC(appliedCatalog.Placeables, initialCatalog.Placeables) &&
                                    SameReferencesUx1AC(appliedCatalog.Doors, initialCatalog.Doors) &&
                                    SameReferencesUx1AC(appliedCatalog.Encounters, initialCatalog.Encounters) &&
                                    SameReferencesUx1AC(
                                        appliedCatalog.VehicleRoutes,
                                        initialCatalog.VehicleRoutes) &&
                                    SameReferencesUx1AC(appliedCatalog.Merchants, initialCatalog.Merchants) &&
                                    SameReferencesUx1AC(appliedCatalog.Companions, initialCatalog.Companions) &&
                                    ReferenceEquals(appliedCatalog.Scripts, initialCatalog.Scripts);

            var adapter = new TriggerAdvancedRuntimeAdapter();
            var resolution = adapter.Resolve(appliedTarget);
            var handle = resolution.Handle;
            var runtimeAdapter = false;
            if (resolution.Available && handle is not null)
            {
                var runtime = adapter.Build(handle, appliedTarget);
                var detected = adapter.Detect(handle, appliedTarget, 100, "outsider");
                var disarmed = adapter.Disarm(handle, appliedTarget, 100);
                var reset = adapter.Reset(handle, appliedTarget, disarmed.Trap);
                var entity = new QueryableEntity(
                    "hero",
                    true,
                    SetUx1AC("player", "hero"),
                    new HashSet<Guid> { factionId },
                    SetUx1AC("health", "inventory"),
                    "party",
                    "heroes",
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["quest"] = "active" });
                var effect = adapter.Apply(
                    handle,
                    appliedTarget,
                    TriggerGameplayVolumeKind.Damage,
                    "hero");
                var now = DateTimeOffset.UnixEpoch.AddMinutes(1);
                var state = new TriggerRuntimeState(
                    new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
                    {
                        ["hero"] = now.AddSeconds(-2),
                    },
                    new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
                    {
                        ["hero"] = now.AddSeconds(-1),
                    },
                    SetUx1AC("hero"),
                    false);
                var trace = Enumerable.Range(0, 5)
                    .Select(index => new TriggerEventTrace(
                        now.AddSeconds(index),
                        "DamageVolumeEntered",
                        "hero",
                        "fired"))
                    .ToArray();
                var debug = adapter.Debug(handle, appliedTarget, state, now, trace);
                runtimeAdapter = runtime.GameplayVolumes.Count == 10 && detected.Succeeded &&
                                 disarmed.Succeeded && !disarmed.Trap.Armed && reset.Trap.Armed &&
                                 adapter.Matches(handle, appliedTarget, entity) &&
                                 effect.Changes["health"] == "-17" &&
                                 effect.EventName == "DamageVolumeEntered" &&
                                 debug.EventTrace.Count == 3 && debug.Occupants.SequenceEqual(["hero"]);
            }

            var journal = editing.History.Journal;
            var canonicalCommand = journal.Count == 3 &&
                                   journal.All(item => item.CommandName == "set_world_feature_catalog") &&
                                   journal.Count(item => item.IsApplied) == 1;
            var undo = editing.Undo();
            var removed = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id).Advanced is null;
            var redo = editing.Redo();
            var restored = editing.Project!.Module.WorldFeatures.Triggers
                .Single(item => item.Id == targetProfile.Id).Advanced;
            var undoRedo = undo.Succeeded && removed && redo.Succeeded && restored is not null &&
                           restored.Trap.PayloadId == appliedAdvanced.Trap.PayloadId;

            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistedTarget = opened
                ? reopened.Project!.Module.WorldFeatures.Triggers.Single(item => item.Id == targetProfile.Id)
                : null;
            var persistence = persistedTarget?.Advanced is { } persistedAdvanced &&
                              SameAdvancedUx1AC(appliedAdvanced, persistedAdvanced) &&
                              SameFoundationUx1AC(appliedTarget.Foundation!, persistedTarget.Foundation!) &&
                              appliedTarget.FoundationRequiredAtRuntime ==
                              persistedTarget.FoundationRequiredAtRuntime &&
                              appliedTarget.AdvancedRequiredAtRuntime == persistedTarget.AdvancedRequiredAtRuntime;
            var humanDiagnostics = IsHumanUx1ACMessage(invalidTrap.Message) &&
                                   IsHumanUx1ACMessage(invalidFilter.Message) &&
                                   IsHumanUx1ACMessage(invalidVolume.Message) &&
                                   IsHumanUx1ACMessage(invalidDebug.Message) &&
                                   IsHumanUx1ACMessage(preview.Message);
            var invalidTrapBlocked = !invalidTrap.Succeeded;
            var invalidFilterBlocked = !invalidFilter.Succeeded;
            var invalidVolumeBlocked = !invalidVolume.Succeeded;
            var invalidDebugBlocked = !invalidDebug.Succeeded;
            var succeeded = selectionExplicit && foundationRequired && advancedCreation && trap && filter &&
                            gameplayVolumes && occupancy && dryRun && invalidTrapBlocked &&
                            invalidFilterBlocked && invalidVolumeBlocked && invalidDebugBlocked &&
                            staleDraftBlocked && staleApprovalBlocked && runtimeAdapter && canonicalCommand &&
                            undoRedo && persistence && foundationPreserved && portalPreserved &&
                            validationPreserved && neighborPreserved && humanDiagnostics;
            return new StudioUx1ACSmokeResult(
                succeeded,
                selectionExplicit,
                foundationRequired,
                advancedCreation,
                trap,
                filter,
                gameplayVolumes,
                occupancy,
                dryRun,
                invalidTrapBlocked,
                invalidFilterBlocked,
                invalidVolumeBlocked,
                invalidDebugBlocked,
                staleDraftBlocked,
                staleApprovalBlocked,
                runtimeAdapter,
                canonicalCommand,
                undoRedo,
                persistence,
                foundationPreserved,
                portalPreserved,
                validationPreserved,
                neighborPreserved,
                humanDiagnostics);
        }
        catch
        {
            return FailedUx1ACSmoke();
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

    private static StudioTriggerAdvancedDraft CustomizeUx1ACSmokeDraft(
        StudioTriggerAdvancedDraft draft,
        Guid factionId)
    {
        var volumes = draft.GameplayVolumes.Select(CopyTriggerVolumeUx1AC).ToArray();
        var damageIndex = Array.FindIndex(volumes, item => item.Kind == TriggerGameplayVolumeKind.Damage);
        var damage = volumes[damageIndex];
        var numeric = damage.NumericParameters.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        numeric["health"] = -17;
        numeric["radius.scale"] = 1.25;
        var text = damage.TextParameters.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        text["authored.note"] = "braises";
        volumes[damageIndex] = damage with { NumericParameters = numeric, TextParameters = text };
        return draft with
        {
            Trap = draft.Trap with
            {
                TriggerId = "enter",
                PayloadId = "damage:ember-burst",
                DetectionDifficulty = 18,
                DisarmDifficulty = 22,
                ResetDelaySeconds = 45,
                OwnerId = "ash-watch",
                VisualEffectId = "vfx:ember-burst",
                FriendlyPolicy = "ash-watch",
                Armed = true,
                OneShot = false,
                GameMasterVisualization = true,
                VisualizationColor = "#ff7a45",
            },
            TargetFilter = draft.TargetFilter with
            {
                PlayersOnly = true,
                RequiredTags = ["player", "hero"],
                ExcludedTags = ["undead"],
                FactionIds = [factionId],
                RequiredComponents = ["health", "inventory"],
                OwnerId = "party",
                TeamId = "heroes",
                SandboxPredicate = "quest==active",
            },
            GameplayVolumes = volumes,
            OccupancyDebug = draft.OccupancyDebug with { MaximumTraceEntries = 3 },
            RequireRuntime = true,
        };
    }

    private static bool ApplyUx1ACIntermediateMutation(
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
        var result = editing._setTriggerAdvancedCatalogUx1AC.Handle(history.State, command);
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    private static TriggerWorldProfile CreateUx1ACSmokeTrigger(
        BlueprintId blueprintId,
        AreaId areaId,
        string prefix,
        bool advanced,
        bool foundation)
    {
        Guid Id(int suffix) => Guid.Parse($"{prefix}-{suffix:000000000000}");
        var destination = Id(30);
        var foundationProfile = foundation
            ? new TriggerWorldProfile.FoundationPolicy(
                Id(10),
                1,
                "test:ux1-ac",
                "trigger-advanced",
                new TriggerWorldProfile.ShapePolicy(
                    TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                    [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                    [],
                    new Vector3Value(4, 3, 4),
                    3,
                    .25,
                    true),
                new TriggerWorldProfile.FilterPolicy(
                    ["creature"],
                    ["player"],
                    ["undead"],
                    ["characters"]),
                ["area.ready"],
                5,
                TriggerWorldProfile.FirePolicy.OncePerEntity,
                [new TriggerWorldProfile.ActionPolicy(
                    Id(11),
                    "start-encounter",
                    "encounter:guardian",
                    new Dictionary<string, string>(StringComparer.Ordinal))],
                new TriggerWorldProfile.PortalPolicy(
                    TriggerWorldProfile.DestinationKind.Waypoint,
                    destination,
                    "anchor:exit",
                    new Vector3Value(0, 180, 0),
                    true,
                    ["portal.travel"]),
                new TriggerWorldProfile.TransformPolicy(
                    Vector3Value.Zero,
                    Vector3Value.Zero,
                    Vector3Value.One,
                    Id(31)),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["editor.overlay"] = "volume",
                    ["authored.hidden"] = "keep",
                },
                new TriggerWorldProfile.EventPolicy(
                    Id(12), 1, "test:ux1-ac", "1.0.0", "VolumeEntered", "scripts/enter", null),
                new TriggerWorldProfile.EventPolicy(
                    Id(13), 1, "test:ux1-ac", "1.0.0", "VolumeExited", null, "scripts/exit"),
                new TriggerWorldProfile.RuntimeValidationPolicy(
                    [destination],
                    ["encounter:guardian"],
                    ["scripts/enter", "scripts/exit"],
                    ["characters"]),
                true)
            : null;
        var advancedProfile = advanced ? CreateUx1ACAdvancedProfile(prefix) : null;
        return new TriggerWorldProfile(
            Id(100),
            1,
            "test:ux1-ac",
            blueprintId,
            areaId,
            null,
            null,
            null,
            "trigger.advanced",
            null,
            null,
            null,
            null,
            null,
            foundationProfile,
            foundation,
            advancedProfile,
            advanced);
    }

    private static TriggerAdvancedProfile CreateUx1ACAdvancedProfile(string prefix)
    {
        Guid Id(int suffix) => Guid.Parse($"{prefix}-{suffix:000000000000}");
        var volumes = Enum.GetValues<TriggerGameplayVolumeKind>()
            .Select((kind, index) =>
            {
                var parameter = PrimaryTriggerVolumeParameterUx1AC(kind);
                return new TriggerGameplayVolumeProfile(
                    Id(50 + index),
                    kind,
                    new Dictionary<string, double>(StringComparer.Ordinal)
                    {
                        [parameter.Key] = parameter.Value,
                    },
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    $"{kind}VolumeEntered",
                    $"{kind}VolumeExited",
                    true);
            })
            .ToArray();
        return new TriggerAdvancedProfile(
            Id(40),
            1,
            "test:ux1-ac",
            "1.0.0",
            new TriggerTrapProfile(
                Id(41),
                "enter",
                "damage:mist",
                8,
                10,
                20,
                "garden",
                "vfx:mist",
                "allies",
                true,
                false,
                true,
                "#55aaff",
                "TrapDetected",
                "TrapDisarmed",
                "TrapTriggered",
                "TrapReset"),
            new TriggerEntityQueryProfile(
                Id(42),
                true,
                ["player"],
                ["undead"],
                [],
                ["health"],
                "party",
                "heroes",
                "quest==active"),
            volumes,
            new TriggerOccupancyDebugProfile(Id(43), true, true, true, true, true, true, 64));
    }

    private static bool SameAdvancedUx1AC(TriggerAdvancedProfile expected, TriggerAdvancedProfile actual) =>
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
            pair.First.Id == pair.Second.Id && pair.First.Kind == pair.Second.Kind &&
            pair.First.NumericParameters.OrderBy(item => item.Key).SequenceEqual(
                pair.Second.NumericParameters.OrderBy(item => item.Key)) &&
            pair.First.TextParameters.OrderBy(item => item.Key).SequenceEqual(
                pair.Second.TextParameters.OrderBy(item => item.Key)) &&
            pair.First.EnterBinding == pair.Second.EnterBinding &&
            pair.First.ExitBinding == pair.Second.ExitBinding &&
            pair.First.Enabled == pair.Second.Enabled) &&
        expected.OccupancyDebug == actual.OccupancyDebug;

    private static bool SameFoundationUx1AC(
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
                          pair.First.Id == pair.Second.Id && pair.First.Kind == pair.Second.Kind &&
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
        return expected.Id == actual.Id && expected.SchemaVersion == actual.SchemaVersion &&
               expected.Provenance == actual.Provenance && expected.Slug == actual.Slug && shape && filter &&
               expected.Conditions.SequenceEqual(actual.Conditions) &&
               expected.CooldownSeconds == actual.CooldownSeconds && expected.Fire == actual.Fire && actions &&
               portal && expected.Transform == actual.Transform &&
               expected.Overrides.OrderBy(item => item.Key).SequenceEqual(
                   actual.Overrides.OrderBy(item => item.Key)) &&
               expected.OnEnter == actual.OnEnter && expected.OnExit == actual.OnExit && validation &&
               expected.ValidationRequiredAtRuntime == actual.ValidationRequiredAtRuntime;
    }

    private static IReadOnlySet<string> SetUx1AC(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);

    private static bool SameReferencesUx1AC<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class =>
        left.Count == right.Count && left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx1ACMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Contains('{') &&
        !message.Contains("Guid", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !System.Text.RegularExpressions.Regex.IsMatch(
            message,
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    private static StudioUx1ACSmokeResult FailedUx1ACSmoke() => new(
        Succeeded: false,
        SelectionExplicit: false,
        FoundationRequired: false,
        AdvancedCreation: false,
        Trap: false,
        TargetFilter: false,
        GameplayVolumes: false,
        OccupancyDebug: false,
        DryRun: false,
        InvalidTrapBlocked: false,
        InvalidFilterBlocked: false,
        InvalidVolumeBlocked: false,
        InvalidDebugBlocked: false,
        StaleDraftBlocked: false,
        StaleApprovalBlocked: false,
        RuntimeAdapter: false,
        CanonicalCommand: false,
        UndoRedo: false,
        Persistence: false,
        FoundationPreserved: false,
        PortalPreserved: false,
        ValidationPreserved: false,
        NeighborPreserved: false,
        HumanDiagnostics: false);
}
