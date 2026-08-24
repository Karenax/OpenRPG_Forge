using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly CreateAreaHandler _createArea = new(new SystemClock());
    private StudioAreaCreationDraft? _approvedAreaCreation;

    internal IReadOnlyList<StudioAreaSpawnChoice> AreaSpawnChoices => Project?.Blueprints
        .Where(item => item.Kind == BlueprintKind.PlayerSpawn)
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .Select(item => new StudioAreaSpawnChoice(item.Id, item.DisplayName))
        .ToArray() ?? [];

    internal IReadOnlyList<StudioAreaPresetChoice> AreaKitChoices => AreaPresets(
        [("Kit polyvalent Forge", "forge-generic")],
        settings => settings.KitKey);

    internal IReadOnlyList<StudioAreaPresetChoice> AreaTerrainChoices => AreaPresets(
        [("Sol plat", "flat"), ("Relief extérieur", "heightmap")],
        settings => settings.TerrainProfile);

    internal IReadOnlyList<StudioAreaPresetChoice> AreaLightingChoices => AreaPresets(
        [("Intérieur neutre", "interior-neutral"), ("Lumière du jour", "daylight")],
        settings => settings.LightingProfile);

    internal IReadOnlyList<StudioAreaPresetChoice> AreaNavigationChoices => AreaPresets(
        [("Marche standard", "walk-medium")],
        settings => settings.NavigationProfile);

    internal IReadOnlyList<StudioAreaPresetChoice> AreaStreamingChoices => AreaPresets(
        [("Charger cette zone seule", "single-area")],
        settings => settings.StreamingProfile);

    internal IReadOnlyList<StudioAreaPresetChoice> AreaBiomeChoices => AreaPresets(
        [("Intérieur", "interior"), ("Climat tempéré", "temperate")],
        settings => settings.Biome);

    internal StudioAreaCreationDraft CreateAreaCreationDraft()
    {
        var kind = ActiveArea?.Kind ?? AreaKind.Exterior;
        var settings = AreaCreationSettings.CreateDefault(kind);
        var spawn = AreaSpawnChoices.FirstOrDefault();
        return new StudioAreaCreationDraft(
            AreaId.From(Guid.NewGuid()),
            EntityId.From(Guid.NewGuid()),
            NextAreaName(),
            kind,
            64,
            64,
            spawn is null ? AreaTemplateKind.Empty : AreaTemplateKind.Playable,
            spawn?.Id,
            settings.KitKey,
            settings.TerrainProfile,
            settings.LightingProfile,
            settings.NavigationProfile,
            settings.StreamingProfile,
            settings.Biome,
            settings.Seed,
            settings.RuntimeBudgetMegabytes);
    }

    internal StudioAreaCreationPreview PreviewAreaCreation(StudioAreaCreationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _approvedAreaCreation = null;
        if (!TryBuildAreaCommand(draft, dryRun: true, out var command, out var message))
        {
            return new StudioAreaCreationPreview(false, message, string.Empty, false);
        }

        var dryRun = _createArea.Handle(History!.State, command!);
        if (!dryRun.Succeeded)
        {
            var human = HumanizeAreaCreation(dryRun.Diagnostics.FirstOrDefault()?.Code);
            AddOperationalDiagnostic(
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "area.creation_refused",
                human,
                target: null,
                documentPath: "zones",
                Forge.Application.Creator.CreatorDiagnosticSeverity.Error,
                "area:create");
            return new StudioAreaCreationPreview(false, human, string.Empty, false);
        }

        var candidateResult = _createArea.Handle(History.State, command! with { DryRun = false });
        var candidate = candidateResult.Succeeded
            ? candidateResult.State.Project.Areas.FirstOrDefault(item => item.Id == draft.AreaId)
            : null;
        if (candidate is null)
        {
            return new StudioAreaCreationPreview(
                false,
                "L’aperçu de la zone n’a pas pu être construit.",
                string.Empty,
                false);
        }

        _approvedAreaCreation = draft;
        return new StudioAreaCreationPreview(
            true,
            "Aperçu validé · aucun fichier écrit et aucun changement appliqué.",
            BuildAreaCreationSummary(candidate),
            true,
            candidate);
    }

    internal StudioAreaCreationResult ApplyApprovedAreaCreation()
    {
        if (_approvedAreaCreation is null || History is null)
        {
            return new StudioAreaCreationResult(false, "Prévisualisez d’abord la nouvelle zone.");
        }

        var draft = _approvedAreaCreation;
        if (!TryBuildAreaCommand(draft, dryRun: false, out var command, out var message))
        {
            _approvedAreaCreation = null;
            return new StudioAreaCreationResult(false, message);
        }

        var preview = _createArea.Handle(History.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            _approvedAreaCreation = null;
            return new StudioAreaCreationResult(
                false,
                HumanizeAreaCreation(preview.Diagnostics.FirstOrDefault()?.Code));
        }

        var previousAreaId = ActiveAreaId;
        var applied = _createArea.Handle(History.State, command!);
        if (!applied.Succeeded)
        {
            _approvedAreaCreation = null;
            return new StudioAreaCreationResult(
                false,
                HumanizeAreaCreation(applied.Diagnostics.FirstOrDefault()?.Code));
        }

        History.Record(applied);
        InvalidateContentIndexes();
        var opened = OpenAreaTab(draft.AreaId, activate: true, persist: false);
        if (!opened.Succeeded)
        {
            History.Undo();
            ReconcileWorkspaceAreasAfterProjectMutation(previousAreaId);
            InvalidateContentIndexes();
            _approvedAreaCreation = null;
            return new StudioAreaCreationResult(false, "La zone n’a pas pu être ouverte dans l’espace de travail.");
        }

        _approvedAreaCreation = null;
        return new StudioAreaCreationResult(
            true,
            $"Zone « {draft.DisplayName.Trim()} » créée · Ctrl+Z pour annuler · Ctrl+S pour écrire son document.",
            draft.AreaId);
    }

    internal void CancelAreaCreation() => _approvedAreaCreation = null;

    internal StudioUx1ASmokeResult RunUx1AContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1a-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var spawnBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000001")),
                "studio-player-start",
                "Point de départ du joueur",
                BlueprintKind.PlayerSpawn);
            var entryArea = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000002")),
                "zone-d-entree",
                "Zone d’entrée",
                [new EntityInstance(
                    EntityId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000003")),
                    spawnBlueprint.Id,
                    TransformValue.Identity,
                    ["area-anchor"])]);
            var seedProject = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000004")),
                "Studio UX1-A",
                [spawnBlueprint],
                [entryArea]);
            var created = ProjectSession.Create(tempRoot, seedProject);
            var opened = created.Succeeded && editing.Open(tempRoot).Succeeded;
            if (!opened)
            {
                return new StudioUx1ASmokeResult(
                    false, false, false, false, false, false, false, false, false, false, false, false,
                    false);
            }

            var originalProject = editing.Project!;
            var originalAreaDocumentCount = Directory.GetFiles(Path.Combine(tempRoot, "areas"), "*.json").Length;
            var emptyDraft = editing.CreateAreaCreationDraft() with
            {
                AreaId = AreaId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000010")),
                SpawnEntityId = EntityId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000011")),
                DisplayName = "Atelier vide",
                Kind = AreaKind.Interior,
                Width = 32,
                Depth = 24,
                Template = AreaTemplateKind.Empty,
                SpawnBlueprintId = null,
                TerrainProfile = "flat",
                LightingProfile = "interior-neutral",
                Biome = "interior",
            };
            var emptyPreview = editing.PreviewAreaCreation(emptyDraft);
            var emptyTemplate = emptyPreview.Succeeded && emptyPreview.Candidate?.Entities.Count == 0 &&
                                ReferenceEquals(originalProject, editing.Project);

            var draft = editing.CreateAreaCreationDraft() with
            {
                AreaId = AreaId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000020")),
                SpawnEntityId = EntityId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000021")),
                DisplayName = "Bosquet des Brumes",
                Kind = AreaKind.Exterior,
                Width = 96,
                Depth = 72,
                Template = AreaTemplateKind.Playable,
                SpawnBlueprintId = spawnBlueprint.Id,
                KitKey = "forge-generic",
                TerrainProfile = "heightmap",
                LightingProfile = "daylight",
                NavigationProfile = "walk-medium",
                StreamingProfile = "single-area",
                Biome = "temperate",
                Seed = 731,
                RuntimeBudgetMegabytes = 768,
            };
            var preview = editing.PreviewAreaCreation(draft);
            var noAreaDocumentBeforeApply = Directory.GetFiles(
                Path.Combine(tempRoot, "areas"),
                $"{draft.AreaId}-*.json").Length == 0;
            var wizardPreview = preview.Succeeded && preview.CanApply && preview.Candidate is not null;
            var dryRun = wizardPreview && ReferenceEquals(originalProject, editing.Project) &&
                         editing.Project.Areas.Count == 1 && noAreaDocumentBeforeApply;
            var candidate = preview.Candidate;
            var foundationProfile = candidate?.Foundation.Creation is
            {
                KitKey: "forge-generic",
                TerrainProfile: "heightmap",
                LightingProfile: "daylight",
                NavigationProfile: "walk-medium",
                StreamingProfile: "single-area",
                Biome: "temperate",
                Seed: 731,
                RuntimeBudgetMegabytes: 768,
            };

            var duplicate = editing.PreviewAreaCreation(draft with
            {
                AreaId = AreaId.From(Guid.Parse("c1a10000-0000-4000-8000-000000000030")),
                DisplayName = entryArea.DisplayName,
            });
            var invalidBlocked = !duplicate.Succeeded;
            var humanDiagnostics = invalidBlocked &&
                                   !duplicate.Message.Contains("area.", StringComparison.OrdinalIgnoreCase) &&
                                   !duplicate.Message.Contains('/', StringComparison.Ordinal) &&
                                   !Guid.TryParse(duplicate.Message, out _);

            preview = editing.PreviewAreaCreation(draft);
            var applied = editing.ApplyApprovedAreaCreation();
            var appliedProject = editing.Project!;
            var appliedArea = appliedProject.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
            var playableTemplate = appliedArea?.Entities.SingleOrDefault()?.BlueprintId == spawnBlueprint.Id;
            var neighborsPreserved = ReferenceEquals(
                originalProject.Areas.Single(item => item.Id == entryArea.Id),
                appliedProject.Areas.Single(item => item.Id == entryArea.Id));
            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project) && editing.Project!.Areas.Count == 1;
            var redo = editing.Redo();
            var redone = editing.Project!.Areas.Any(item => item.Id == draft.AreaId);
            var atomicUndoRedo = applied.Succeeded && undo.Succeeded && undone && redo.Succeeded && redone;

            var save = editing.Save();
            var independentFiles = Directory.GetFiles(Path.Combine(tempRoot, "areas"), $"{draft.AreaId}-*.json");
            var independentDocument = save.Succeeded && originalAreaDocumentCount == 1 && independentFiles.Length == 1;
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
            var persistenceRoundTrip = reopen.Succeeded && restored is not null &&
                                       restored.DisplayName == draft.DisplayName &&
                                       restored.Kind == draft.Kind &&
                                       restored.Dimensions == new AreaDimensions(draft.Width, draft.Depth) &&
                                       restored.Foundation.Creation == appliedArea?.Foundation.Creation;
            var stableIdentity = restored?.Id == appliedArea?.Id &&
                                 restored?.Foundation.Id == appliedArea?.Foundation.Id &&
                                 restored?.Foundation.TileKit.Id == appliedArea?.Foundation.TileKit.Id;

            var succeeded = wizardPreview && emptyTemplate && playableTemplate && foundationProfile && dryRun &&
                            invalidBlocked && atomicUndoRedo && independentDocument && persistenceRoundTrip &&
                            stableIdentity && neighborsPreserved && humanDiagnostics;
            return new StudioUx1ASmokeResult(
                succeeded,
                wizardPreview,
                emptyTemplate,
                playableTemplate,
                foundationProfile,
                dryRun,
                invalidBlocked,
                atomicUndoRedo,
                independentDocument,
                persistenceRoundTrip,
                stableIdentity,
                neighborsPreserved,
                humanDiagnostics);
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

    private bool TryBuildAreaCommand(
        StudioAreaCreationDraft draft,
        bool dryRun,
        out CreateAreaCommand? command,
        out string message)
    {
        command = null;
        message = string.Empty;
        if (History is null || Project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(draft.DisplayName))
        {
            message = "Donnez un nom visible à la nouvelle zone.";
            return false;
        }

        try
        {
            var dimensions = new AreaDimensions(draft.Width, draft.Depth);
            var settings = new AreaCreationSettings(
                draft.Template == AreaTemplateKind.Playable ? "playable" : "blank",
                draft.KitKey.Trim(),
                draft.TerrainProfile.Trim(),
                draft.LightingProfile.Trim(),
                draft.NavigationProfile.Trim(),
                draft.StreamingProfile.Trim(),
                draft.Biome.Trim(),
                draft.Seed,
                draft.RuntimeBudgetMegabytes);
            var defaults = AreaFoundationProfile.CreateDefault(draft.AreaId, draft.Kind);
            var foundation = new AreaFoundationProfile(
                defaults.Id,
                defaults.SchemaVersion,
                "studio:ux1-a-area-wizard",
                settings,
                new ModularTileKitProfile(
                    defaults.TileKit.Id,
                    defaults.TileKit.SchemaVersion,
                    "studio:ux1-a-area-wizard",
                    defaults.TileKit.GridSizeMeters,
                    defaults.TileKit.Definitions,
                    defaults.TileKit.ConnectionRules,
                    defaults.TileKit.Placements,
                    defaults.TileKit.Groups,
                    defaults.TileKit.Brushes),
                defaults.Lifecycle,
                defaults.Volumes,
                defaults.Dependencies);
            command = new CreateAreaCommand(
                CommandId.From(Guid.NewGuid()),
                History.State.Revision,
                draft.AreaId,
                draft.DisplayName.Trim(),
                draft.Kind,
                dimensions,
                draft.Template,
                draft.Template == AreaTemplateKind.Playable ? draft.SpawnBlueprintId : null,
                draft.Template == AreaTemplateKind.Playable ? draft.SpawnEntityId : null,
                dryRun,
                foundation);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            message = $"Largeur et profondeur doivent rester entre {AreaDimensions.MinimumSize} et {AreaDimensions.MaximumSize}.";
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            message = "La configuration de la zone est incomplète : " + exception.Message;
            return false;
        }
    }

    private IReadOnlyList<StudioAreaPresetChoice> AreaPresets(
        IReadOnlyList<(string Label, string Value)> defaults,
        Func<AreaCreationSettings, string> selector)
    {
        var choices = defaults.Select(item => new StudioAreaPresetChoice(item.Label, item.Value)).ToList();
        if (Project is not null)
        {
            foreach (var value in Project.Areas.Select(area => selector(area.Foundation.Creation))
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                if (choices.All(item => !string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase)))
                {
                    choices.Add(new StudioAreaPresetChoice($"Profil du projet · {HumanizePreset(value)}", value));
                }
            }
        }
        return choices;
    }

    private string NextAreaName()
    {
        const string stem = "Nouvelle zone";
        if (Project?.Areas.All(area => !string.Equals(area.DisplayName, stem, StringComparison.CurrentCultureIgnoreCase)) !=
            false)
        {
            return stem;
        }
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{stem} {index}";
            if (Project!.Areas.All(area =>
                    !string.Equals(area.DisplayName, candidate, StringComparison.CurrentCultureIgnoreCase)))
            {
                return candidate;
            }
        }
        return $"{stem} {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private void ReconcileWorkspaceAreasAfterProjectMutation(AreaId? preferredAreaId = null)
    {
        if (_workspaceProfileHistory is null || Project is null)
        {
            return;
        }

        var available = Project.Areas.Select(area => area.Id).ToHashSet();
        var requested = preferredAreaId is not null && available.Contains(preferredAreaId)
            ? preferredAreaId
            : ActiveAreaId is not null && available.Contains(ActiveAreaId)
                ? ActiveAreaId
                : Project.Areas.FirstOrDefault()?.Id;
        if (_workspaceProfileHistory.Current.OpenAreaTabs.Any(id => !available.Contains(id)) ||
            (_workspaceProfileHistory.Current.ActiveAreaId is not null &&
             !available.Contains(_workspaceProfileHistory.Current.ActiveAreaId)))
        {
            var reconciled = Forge.Application.Creator.EditorWorkspaceProfilePlanner.ReconcileAreas(
                _workspaceProfileHistory.Current,
                available.ToArray(),
                requested);
            _workspaceProfileHistory = new Forge.Application.Creator.EditorWorkspaceProfileCommandHistory(reconciled);
        }
        RestoreAreaContext(requested);
    }

    private static string BuildAreaCreationSummary(AreaDefinition area)
    {
        var creation = area.Foundation.Creation;
        var kind = area.Kind == AreaKind.Exterior ? "Extérieure" : "Intérieure";
        var template = area.Entities.Count > 0 ? "jouable avec point de départ" : "vide, prête à aménager";
        return $"{kind} · {area.Dimensions.Width} × {area.Dimensions.Depth} · {template}\n" +
               $"Kit {HumanizePreset(creation.KitKey)} · {HumanizePreset(creation.TerrainProfile)} · " +
               $"{HumanizePreset(creation.LightingProfile)}\n" +
               "Un document de zone indépendant sera écrit seulement lors de l’enregistrement.";
    }

    private static string HumanizePreset(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Non configuré";
        }
        var text = value.Replace('-', ' ').Replace('_', ' ').Trim();
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string HumanizeAreaCreation(string? code) => code switch
    {
        CreateAreaHandler.NameRequiredCode => "Donnez un nom visible à la nouvelle zone.",
        CreateAreaHandler.DuplicateIdCode => "Cette tentative de création n’est plus actuelle. Fermez puis rouvrez l’assistant.",
        CreateAreaHandler.DuplicateSlugCode => "Une zone porte déjà ce nom. Choisissez un nom différent.",
        CreateAreaHandler.SpawnRequiredCode => "Le gabarit jouable exige un point de départ du joueur.",
        CreateAreaHandler.SpawnBlueprintMissingCode => "Le point de départ choisi n’est plus disponible dans le projet.",
        CreateAreaHandler.FoundationInvalidCode or AreaFoundationValidator.CreationCode =>
            "Vérifiez les profils, la graine et le budget mémoire de la zone.",
        CreateAreaHandler.RevisionConflictCode => "Le module a changé depuis l’aperçu. Prévisualisez à nouveau.",
        _ => "La création de la zone a été refusée. Consultez Messages et état pour la cible concernée.",
    };

    private void CloseUx1A() => _approvedAreaCreation = null;
}
