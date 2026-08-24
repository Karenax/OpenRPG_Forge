using System.Globalization;
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
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Encounters;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwFProvenance = "studio:ux2-lw-f";
    private const string Ux2LwFEvidenceProvenance = "studio:ux2-lw-f:evidence";
    private readonly SetWorldFeatureCatalogHandler _setEncounterCatalogUx2LwF = new(new SystemClock());
    private StudioUx2LwFDraft? _approvedEncounterUx2LwF;
    private ProjectRevision? _approvedEncounterRevisionUx2LwF;
    private string? _approvedEncounterSourceFingerprintUx2LwF;
    private string? _approvedEncounterDraftFingerprintUx2LwF;

    internal IReadOnlyList<StudioUx2LwFEncounterChoice> EncounterChoicesUx2LwF
    {
        get
        {
            var project = Project;
            var area = ActiveArea;
            if (project is null || area is null) return [];
            var choices = project.Module.WorldFeatures.Encounters
                .Where(item => item.AreaId == area.Id)
                .Select(item => new StudioUx2LwFEncounterChoice(
                    item.Id,
                    $"{HumanEncounterNameUx2LwF(item)} · {area.DisplayName} · " +
                    (item.Foundation is null || item.Composition is null
                        ? "fiche de base incomplète"
                        : $"{item.Foundation.Candidates.Count} candidat(s), {item.Composition.Groups.Count} groupe(s)"),
                    false,
                    item.Foundation is not null && item.Composition is not null,
                    string.Equals(item.Provenance, Ux2LwFEvidenceProvenance, StringComparison.Ordinal)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            return [new(null, "Créer une nouvelle rencontre…", true, true, false), .. choices];
        }
    }

    internal IReadOnlyList<StudioUx2LwFReferenceChoice<AreaId>> EncounterAreaChoicesUx2LwF()
    {
        var project = Project;
        if (project is null) return [];
        return project.Areas.Select(area => new StudioUx2LwFReferenceChoice<AreaId>(
                area.Id, $"{area.DisplayName} · {area.Slug}", "Zone enregistrée dans le module"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StudioUx2LwFReferenceChoice<BlueprintId>> EncounterCreatureChoicesUx2LwF()
    {
        var project = Project;
        if (project is null) return [];
        return project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .Select(item => new StudioUx2LwFReferenceChoice<BlueprintId>(item.Id,
                $"{item.DisplayName} · {item.Slug}",
                $"Blueprint réel du module · {HumanProvenanceUx2LwF(item.Provenance.Source)}"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StudioUx2LwFReferenceChoice<string>> EncounterScriptChoicesUx2LwF()
    {
        var session = Session;
        if (session is null) return [];
        var root = Path.Combine(session.RootDirectory, "scripts");
        if (!Directory.Exists(root)) return [];
        try
        {
            return Directory.EnumerateFiles(root, "*.forge", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/'))
                .Where(IsSafeRelativeScriptUx2LwF)
                .Distinct(StringComparer.Ordinal)
                .Select(path => new StudioUx2LwFReferenceChoice<string>(path,
                    HumanScriptNameUx2LwF(path), "Source ForgeScript locale réelle · lecture seule dans l’aperçu"))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal IReadOnlyList<StudioUx2LwFReferenceChoice<string>> EncounterAnchorChoicesUx2LwF(AreaId? areaId)
    {
        var project = Project;
        if (project is null || areaId is null) return [];
        return NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.AreaId == areaId &&
                           item.Kind is NavigationDestinationKind.SpawnAnchor or NavigationDestinationKind.Waypoint)
            .Select(item => new StudioUx2LwFReferenceChoice<string>(
                item.ArrivalAnchorKey ?? $"waypoint:{item.Slug}", item.HumanLabel,
                $"Ancre réelle du module · {HumanProvenanceUx2LwF(item.Provenance)}"))
            .DistinctBy(item => item.Id, StringComparer.Ordinal)
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal StudioUx2LwFDraft? CreateEncounterDraftUx2LwF(Guid? profileId)
    {
        var history = History;
        var project = Project;
        var area = ActiveArea;
        if (history is null || project is null || area is null || profileId is null) return null;
        var profile = project.Module.WorldFeatures.Encounters.SingleOrDefault(item =>
            item.Id == profileId.Value && item.AreaId == area.Id);
        if (profile?.Foundation is null || profile.Composition is null) return null;
        return DraftFromEncounterUx2LwF(profile, history.State.Revision, ProjectFingerprintUx2LwF(project));
    }

    internal StudioUx2LwFDraft? CreateNewEncounterDraftUx2LwF(
        AreaId? areaId,
        BlueprintId? blueprintId,
        string? spawnAnchorId)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || areaId is null || blueprintId is null ||
            string.IsNullOrWhiteSpace(spawnAnchorId) || !project.Areas.Any(item => item.Id == areaId) ||
            !project.Blueprints.Any(item => item.Id == blueprintId &&
                item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) ||
            !EncounterAnchorChoicesUx2LwF(areaId).Any(item =>
                string.Equals(item.Id, spawnAnchorId, StringComparison.Ordinal)))
            return null;

        var area = project.Areas.Single(item => item.Id == areaId);
        var blueprint = project.Blueprints.Single(item => item.Id == blueprintId);
        var profileId = Guid.NewGuid();
        var candidateId = StableUx2LwF(profileId, "candidate:primary");
        var groupId = StableUx2LwF(profileId, "group:primary");
        var slug = SafeSlugUx2LwF($"{area.Slug}-{blueprint.Slug}");
        return new(profileId, true, area.Id, history.State.Revision, ProjectFingerprintUx2LwF(project),
            slug, $"Rencontre · {blueprint.DisplayName}", "rencontre", area.Slug,
            EncounterWorldProfile.CandidateRole.Skirmisher,
            EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
            [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)],
            Vector3Value.Zero, 4, 8, ["player"], [], true, 18, null, false,
            [new(candidateId, blueprint.Id, EncounterWorldProfile.CandidateRole.Skirmisher,
                1, 4, [], [], 3)],
            12, [spawnAnchorId.Trim()], [], 8,
            "ruleset:forge", "party:studio-authored", EncounterDifficultyBand.Standard,
            4, 10, 12, 1, 1,
            [new(groupId, "groupe-principal", 1, 3, 12, [candidateId])],
            new(StableUx2LwF(profileId, "selection"), "party:studio-authored", "budget-bounded", 42, true),
            new(StableUx2LwF(profileId, "activation"), EncounterActivationMode.Once, 0, null,
                EncounterSpawnDistribution.Weighted),
            null);
    }

    internal StudioUx2LwFPreview PreviewEncounterUx2LwF(StudioUx2LwFDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelEncounterPreviewUx2LwF();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedEncounterPreviewUx2LwF("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedEncounterPreviewUx2LwF(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez la rencontre.");
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwF(project), StringComparison.Ordinal))
            return FailedEncounterPreviewUx2LwF(
                "Le catalogue a changé. Rechargez la fiche avant l’aperçu.");
        if (!TryBuildEncounterCandidateUx2LwF(draft, true, out var command, out _, out var candidate,
                out var diagnostics))
        {
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "La fiche contient une valeur invalide.", string.Empty,
                BuildEncounterDiffUx2LwF(draft.OriginalProfile, candidate), diagnostics,
                EmptyRuntimePreviewUx2LwF(), true);
        }

        var commandResult = _setEncounterCatalogUx2LwF.Handle(history.State, command!);
        if (!commandResult.Succeeded)
        {
            var commandDiagnostics = commandResult.Diagnostics.Select(item => new StudioUx2LwFDiagnostic(
                item.Code, $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                HumanEncounterDiagnosticUx2LwF(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé la fiche.",
                commandResult.Patch?.Summary ?? string.Empty,
                BuildEncounterDiffUx2LwF(draft.OriginalProfile, candidate), commandDiagnostics,
                EmptyRuntimePreviewUx2LwF(), true);
        }

        var runtime = BuildEncounterRuntimePreviewUx2LwF(candidate!);
        var diff = BuildEncounterDiffUx2LwF(draft.OriginalProfile, candidate);
        var canApply = draft.IsNew || diff.Any(item => item.Changed);
        if (canApply)
        {
            _approvedEncounterUx2LwF = draft;
            _approvedEncounterRevisionUx2LwF = history.State.Revision;
            _approvedEncounterSourceFingerprintUx2LwF = draft.SourceFingerprint;
            _approvedEncounterDraftFingerprintUx2LwF = EncounterDraftFingerprintUx2LwF(draft);
        }
        return new(true, canApply,
            canApply
                ? "Aperçu sec validé · politique et composition déterministes seulement : aucun acteur moteur, script ou transport réseau n’a été exécuté."
                : "La fiche est déjà identique au document canonique.",
            commandResult.Patch?.Summary ?? string.Empty, diff, [], runtime, true);
    }

    internal StudioUx2LwFActionResult ApplyApprovedEncounterUx2LwF()
    {
        var history = History;
        var project = Project;
        var draft = _approvedEncounterUx2LwF;
        if (history is null || project is null || draft is null ||
            _approvedEncounterRevisionUx2LwF != history.State.Revision ||
            !string.Equals(_approvedEncounterSourceFingerprintUx2LwF,
                ProjectFingerprintUx2LwF(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedEncounterDraftFingerprintUx2LwF,
                EncounterDraftFingerprintUx2LwF(draft), StringComparison.Ordinal))
        {
            CancelEncounterPreviewUx2LwF();
            return new(false, "Prévisualisez à nouveau cette fiche avant de l’appliquer.");
        }
        if (!TryBuildEncounterCandidateUx2LwF(draft, false, out var command, out _, out _, out var diagnostics))
        {
            CancelEncounterPreviewUx2LwF();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "La fiche est invalide.");
        }
        var result = _setEncounterCatalogUx2LwF.Handle(history.State, command!);
        CancelEncounterPreviewUx2LwF();
        if (!result.Succeeded)
            return new(false, HumanEncounterDiagnosticUx2LwF(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Rencontre appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal void CancelEncounterPreviewUx2LwF()
    {
        _approvedEncounterUx2LwF = null;
        _approvedEncounterRevisionUx2LwF = null;
        _approvedEncounterSourceFingerprintUx2LwF = null;
        _approvedEncounterDraftFingerprintUx2LwF = null;
    }

    internal StudioUx2LwFApprovalSnapshot CaptureEncounterApprovalUx2LwF() => new(
        _approvedEncounterUx2LwF, _approvedEncounterRevisionUx2LwF,
        _approvedEncounterSourceFingerprintUx2LwF, _approvedEncounterDraftFingerprintUx2LwF);

    internal void RestoreEncounterApprovalUx2LwF(StudioUx2LwFApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEncounterUx2LwF = snapshot.Draft;
        _approvedEncounterRevisionUx2LwF = snapshot.Revision;
        _approvedEncounterSourceFingerprintUx2LwF = snapshot.SourceFingerprint;
        _approvedEncounterDraftFingerprintUx2LwF = snapshot.DraftFingerprint;
    }

    internal void CloseUx2LwF() => CancelEncounterPreviewUx2LwF();

    internal ProjectDefinition? CreateUx2LwFEvidenceProject()
    {
        var project = Project;
        var area = ActiveArea;
        if (project is null || area is null) return null;
        if (project.Module.WorldFeatures.Encounters.Any(item => item.AreaId == area.Id &&
            item.Foundation is not null && item.Composition is not null))
            return project;
        var creature = project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Creature);
        var spawnBlueprint = project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.PlayerSpawn);
        if (creature is null || spawnBlueprint is null) return null;
        var anchor = EncounterAnchorChoicesUx2LwF(area.Id).FirstOrDefault()?.Id;
        IReadOnlyList<AreaDefinition> areas = project.Areas;
        if (anchor is null)
        {
            var definition = new AnchorDefinition(StableUx2LwF(project.Module.Id.Value, "evidence-anchor-definition"),
                1, Ux2LwFEvidenceProvenance, $"{area.Slug}-rencontre",
                new("anchor.encounter.evidence", "Ancre de rencontre isolée"), AnchorKind.Spawn,
                AnchorScope.Area, ["encounter"], spawnBlueprint.Id, null, false);
            var instance = new AnchorInstance(StableUx2LwF(project.Module.Id.Value, "evidence-anchor-instance"),
                1, Ux2LwFEvidenceProvenance, definition.Id, TransformValue.Identity,
                area.Navigation.Anchors.Instances.Count + 1,
                Forge.Domain.Entities.EntityPropertyOverrides.Empty,
                new("encounter", new(1.2f, 2, 1.2f), 100, "neutral", 0, null, true), null);
            var sourceAnchors = area.Navigation.Anchors;
            var isolatedAnchors = new AnchorCatalog(sourceAnchors.Id, sourceAnchors.SchemaVersion,
                sourceAnchors.Provenance, area.Id, sourceAnchors.RequiredAtRuntime,
                [.. sourceAnchors.Definitions, definition], [.. sourceAnchors.Instances, instance]);
            var isolatedArea = area.WithNavigation(area.Navigation.WithAnchors(isolatedAnchors));
            areas = project.Areas.Select(item => item.Id == area.Id ? isolatedArea : item).ToArray();
            anchor = definition.Slug;
        }
        var script = EncounterScriptChoicesUx2LwF().FirstOrDefault()?.Id;
        var evidence = CreateEvidenceEncounterUx2LwF(project.Module.Id.Value, area.Id, creature.Id,
            anchor, script, Ux2LwFEvidenceProvenance, richTails: false);
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, [.. source.Encounters, evidence],
            source.VehicleRoutes, source.Merchants, source.Companions, source.Scripts);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, areas, project.Dialogues, project.Quests);
    }

    private bool TryBuildEncounterCandidateUx2LwF(
        StudioUx2LwFDraft draft,
        bool dryRun,
        out SetWorldFeatureCatalogCommand? command,
        out ProjectDefinition? candidateProject,
        out EncounterWorldProfile? candidate,
        out IReadOnlyList<StudioUx2LwFDiagnostic> diagnostics)
    {
        command = null;
        candidateProject = null;
        candidate = null;
        diagnostics = [];
        var history = History;
        var project = Project;
        if (history is null || project is null || draft.BaseRevision != history.State.Revision)
        {
            diagnostics = [new("command.revision_conflict", "project/revision",
                "Le projet a changé. Rechargez la rencontre.")];
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx2LwF(project), StringComparison.Ordinal))
        {
            diagnostics = [new("command.source_changed", "module/worldFeatures/encounters",
                "Le catalogue de rencontres a changé. Rechargez la fiche.")];
            return false;
        }
        var current = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == draft.ProfileId);
        if (draft.IsNew ? current is not null : current is null)
        {
            diagnostics = [new("world.encounter.selection_stale",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}",
                draft.IsNew ? "Cette rencontre existe déjà." : "La rencontre sélectionnée n’existe plus.")];
            return false;
        }
        if (!project.Areas.Any(item => item.Id == draft.AreaId))
        {
            diagnostics = [new("world.reference.area_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/areaId",
                "Choisissez une zone réelle encore présente dans le module.")];
            return false;
        }
        var availableCreatures = project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .ToDictionary(item => item.Id);
        if (draft.Candidates.Count == 0 || draft.Candidates.Any(item => !availableCreatures.ContainsKey(item.BlueprintId)))
        {
            diagnostics = [new("world.encounter.candidate_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/foundation/candidates",
                "Ajoutez au moins une créature réelle du module à la liste des candidats.")];
            return false;
        }
        var scriptId = NormalizeScriptUx2LwF(draft.ActivationScriptId);
        if (scriptId is not null && !EncounterScriptChoicesUx2LwF().Any(item =>
                string.Equals(item.Id, scriptId, StringComparison.Ordinal)))
        {
            diagnostics = [new("world.encounter.script_missing",
                $"module/worldFeatures/encounters/{draft.ProfileId:D}/foundation/volume/activationScriptId",
                "Choisissez un script ForgeScript réel encore présent dans le module.")];
            return false;
        }

        var candidates = draft.Candidates.Select(item => new EncounterWorldProfile.SpawnCandidate(
            item.Id, item.BlueprintId, item.Role, item.Weight, item.Cost,
            CleanTokensUx2LwF(item.Conditions), CleanTokensUx2LwF(item.Variants), item.MaximumAlive)).ToArray();
        var candidateIds = candidates.Select(item => item.Id).ToHashSet();
        var groups = draft.Groups.Select(item => new EncounterCompositionProfile.GroupPolicy(
            item.Id, item.Slug.Trim(), item.Minimum, item.Maximum, item.Budget,
            item.CandidateIds.Where(candidateIds.Contains).Distinct().ToArray())).ToArray();
        var points = EncounterVolumePointsUx2LwF(draft);
        var volume = new EncounterWorldProfile.ActivationVolume(draft.Shape, points, draft.Center,
            draft.Height, draft.Radius, CleanTokensUx2LwF(draft.RequiredEntityTags),
            CleanTokensUx2LwF(draft.AllowedFactions), draft.RequireLineOfSight, draft.ThreatRadius,
            scriptId, draft.VisualizeInEditor);
        var foundation = new EncounterWorldProfile.FoundationPolicy(volume, candidates, draft.SpawnBudget,
            CleanTokensUx2LwF(draft.SpawnAnchorIds), CleanTokensUx2LwF(draft.RewardIds),
            draft.NetworkActorCap);
        var composition = new EncounterCompositionProfile(
            current?.Composition?.Id ?? StableUx2LwF(draft.ProfileId, "composition"),
            EncounterCompositionProfile.CurrentSchemaVersion, Ux2LwFProvenance,
            draft.Slug.Trim(), draft.DisplayName.Trim(), draft.Category.Trim(), draft.Theme.Trim(),
            draft.PrimaryRole,
            new(current?.Composition?.Difficulty.Id ?? StableUx2LwF(draft.ProfileId, "difficulty"),
                draft.RulesetId.Trim(), draft.PartyAssessmentProviderId.Trim(), draft.DifficultyBand,
                draft.ExpectedPartySize, draft.ExpectedPartyPower, draft.BaseBudget,
                draft.PartySizeMultiplier, draft.DifficultyMultiplier),
            groups,
            draft.Selection with { PartyAssessmentProviderId = draft.PartyAssessmentProviderId.Trim() },
            draft.Activation,
            current?.Composition?.Overrides ?? new Dictionary<string, string>());
        var updated = current is null
            ? new EncounterWorldProfile(draft.ProfileId, 1, Ux2LwFProvenance, draft.AreaId,
                null, null, null, null,
                new(EncounterWorldProfile.VisibilityMode.Hidden, 0, false, string.Empty),
                new(30, 30, 120, true, true), foundation, composition, true)
            : current with
            {
                Provenance = Ux2LwFProvenance,
                AreaId = draft.AreaId,
                Foundation = foundation,
                Composition = composition,
                CompositionRequiredAtRuntime = true,
            };
        candidate = updated;
        var source = project.Module.WorldFeatures;
        var encounters = draft.IsNew
            ? source.Encounters.Append(updated).ToArray()
            : source.Encounters.Select(item => item.Id == updated.Id ? updated : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        candidateProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var targetPath = $"module/worldFeatures/encounters/{updated.Id}";
        var canonicalDiagnostics = WorldFeatureCatalogValidator.Validate(candidateProject)
            .Where(item => item.Path.StartsWith(targetPath, StringComparison.Ordinal))
            .Select(item => new StudioUx2LwFDiagnostic(item.Code, item.Path,
                HumanEncounterDiagnosticUx2LwF(item.Code))).ToList();
        if (canonicalDiagnostics.Count == 0)
        {
            try
            {
                var realAnchors = project.Areas.SelectMany(area => EncounterAnchorChoicesUx2LwF(area.Id))
                    .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                if (current?.Foundation is { } originalFoundation)
                    realAnchors.UnionWith(originalFoundation.SpawnAnchorIds);
                var knownRewards = current?.Foundation?.RewardIds.ToHashSet(StringComparer.Ordinal) ?? [];
                var knownScripts = EncounterScriptChoicesUx2LwF().Select(item => item.Id)
                    .ToHashSet(StringComparer.Ordinal);
                var authoring = EncounterCompositionFoundationService.ValidateAuthoring(updated,
                    new(project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
                            .Select(item => item.Id.Value).ToHashSet(),
                        project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
                            .Select(item => item.Id.ToString()).ToHashSet(StringComparer.Ordinal),
                        project.Areas.Select(item => item.Id.ToString()).ToHashSet(StringComparer.Ordinal),
                        realAnchors, knownScripts, knownRewards, 512, 1_000_000));
                canonicalDiagnostics.AddRange(authoring.Select(item => new StudioUx2LwFDiagnostic(
                    item.Code, item.Path, HumanEncounterDiagnosticUx2LwF(item.Code))));
            }
            catch (InvalidOperationException exception)
            {
                canonicalDiagnostics.Add(new("world.encounter.runtime_mapping_invalid", targetPath,
                    HumanEncounterDiagnosticUx2LwF(exception.Message)));
            }
        }
        if (canonicalDiagnostics.Count > 0)
        {
            diagnostics = canonicalDiagnostics.DistinctBy(item => (item.Code, item.Path)).ToArray();
            return false;
        }
        command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            catalog, dryRun);
        return true;
    }

    private static StudioUx2LwFDraft DraftFromEncounterUx2LwF(
        EncounterWorldProfile profile,
        ProjectRevision revision,
        string sourceFingerprint)
    {
        var foundation = profile.Foundation!;
        var composition = profile.Composition!;
        return new(profile.Id, false, profile.AreaId, revision, sourceFingerprint,
            composition.Slug, composition.DisplayName, composition.Category, composition.Theme,
            composition.PrimaryRole, foundation.Volume.Shape, foundation.Volume.Points,
            foundation.Volume.Center, foundation.Volume.Height, foundation.Volume.Radius,
            foundation.Volume.RequiredEntityTags, foundation.Volume.AllowedFactions,
            foundation.Volume.RequireLineOfSight, foundation.Volume.ThreatRadius,
            foundation.Volume.ActivationScriptId, foundation.Volume.VisualizeInEditor,
            foundation.Candidates.Select(item => new StudioUx2LwFCandidateDraft(item.Id, item.BlueprintId,
                item.Role, item.Weight, item.Cost, item.Conditions, item.Variants, item.MaximumAlive)).ToArray(),
            foundation.SpawnBudget, foundation.SpawnAnchorIds, foundation.RewardIds,
            foundation.NetworkActorCap, composition.Difficulty.RulesetId,
            composition.Difficulty.PartyAssessmentProviderId, composition.Difficulty.Band,
            composition.Difficulty.ExpectedPartySize, composition.Difficulty.ExpectedPartyPower,
            composition.Difficulty.BaseBudget, composition.Difficulty.PartySizeMultiplier,
            composition.Difficulty.DifficultyMultiplier,
            composition.Groups.Select(item => new StudioUx2LwFGroupDraft(item.Id, item.Slug,
                item.Minimum, item.Maximum, item.Budget, item.CandidateIds)).ToArray(),
            composition.Selection, composition.Activation, profile);
    }

    private static IReadOnlyList<Vector3Value> EncounterVolumePointsUx2LwF(StudioUx2LwFDraft draft)
    {
        var expected = draft.Shape == EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D ? 3 :
            draft.Shape == EncounterWorldProfile.VolumeShape.Box3D ? 2 : 0;
        if (draft.Points.Count >= expected) return draft.Points;
        var span = (float)Math.Max(1, double.IsFinite(draft.Radius) ? draft.Radius : 1);
        return draft.Shape switch
        {
            EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D =>
            [
                new(draft.Center.X - span, draft.Center.Y, draft.Center.Z - span),
                new(draft.Center.X + span, draft.Center.Y, draft.Center.Z - span),
                new(draft.Center.X + span, draft.Center.Y, draft.Center.Z + span),
                new(draft.Center.X - span, draft.Center.Y, draft.Center.Z + span),
            ],
            EncounterWorldProfile.VolumeShape.Box3D =>
            [
                new(draft.Center.X - span, draft.Center.Y - (float)Math.Max(1, draft.Height) / 2,
                    draft.Center.Z - span),
                new(draft.Center.X + span, draft.Center.Y + (float)Math.Max(1, draft.Height) / 2,
                    draft.Center.Z + span),
            ],
            _ => draft.Points,
        };
    }

    private static IReadOnlyList<StudioUx2LwFDiff> BuildEncounterDiffUx2LwF(
        EncounterWorldProfile? before,
        EncounterWorldProfile? after)
    {
        if (after is null) return [];
        var rows = new List<StudioUx2LwFDiff>();
        Add("Assistant", before is null ? "Rencontre absente" : HumanEncounterNameUx2LwF(before),
            HumanEncounterNameUx2LwF(after));
        Add("Volume d’activation", VolumeSummaryUx2LwF(before?.Foundation?.Volume),
            VolumeSummaryUx2LwF(after.Foundation?.Volume));
        Add("Créatures candidates", CandidateSummaryUx2LwF(before?.Foundation),
            CandidateSummaryUx2LwF(after.Foundation));
        Add("Difficulté", DifficultySummaryUx2LwF(before?.Composition?.Difficulty),
            DifficultySummaryUx2LwF(after.Composition?.Difficulty));
        Add("Minimum et maximum", CountSummaryUx2LwF(before?.Composition),
            CountSummaryUx2LwF(after.Composition));
        return rows;

        void Add(string section, string oldValue, string newValue) =>
            rows.Add(new(section, oldValue, newValue,
                !string.Equals(oldValue, newValue, StringComparison.Ordinal)));
    }

    private static StudioUx2LwFRuntimePreview BuildEncounterRuntimePreviewUx2LwF(
        EncounterWorldProfile profile)
    {
        var composition = profile.Composition!;
        var first = EncounterCompositionFoundationService.Preview(profile,
            composition.Difficulty.ExpectedPartySize, composition.Difficulty.ExpectedPartyPower,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var second = EncounterCompositionFoundationService.Preview(profile,
            composition.Difficulty.ExpectedPartySize, composition.Difficulty.ExpectedPartyPower,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var deterministic = first.Composition.Summary == second.Composition.Summary &&
                            first.Composition.Candidates.Select(item => item.CreatureId).SequenceEqual(
                                second.Composition.Candidates.Select(item => item.CreatureId));
        return new(VolumeSummaryUx2LwF(profile.Foundation!.Volume),
            $"{profile.Foundation.Candidates.Count} candidat(s) · aperçu {first.Composition.TotalCount} acteur(s), " +
            $"coût {first.Composition.TotalCost:0.##} · {(deterministic ? "graine reproductible" : "écart détecté")}",
            DifficultySummaryUx2LwF(composition.Difficulty), CountSummaryUx2LwF(composition),
            "Projection de politique uniquement : aucun volume moteur, acteur, ForgeScript ou transport réseau n’est exécuté.");
    }

    private static StudioUx2LwFRuntimePreview EmptyRuntimePreviewUx2LwF() => new(
        "Volume non évalué", "Candidats non évalués", "Difficulté non évaluée",
        "Comptages non évalués",
        "Aucune simulation moteur, script ou réseau n’a été lancée.");

    private static StudioUx2LwFPreview FailedEncounterPreviewUx2LwF(string message) => new(
        false, false, message, string.Empty, [], [], EmptyRuntimePreviewUx2LwF(), true);

    private static string VolumeSummaryUx2LwF(EncounterWorldProfile.ActivationVolume? volume) => volume is null
        ? "Volume absent"
        : $"{HumanVolumeShapeUx2LwF(volume.Shape)} · hauteur {volume.Height:0.##} · rayon {volume.Radius:0.##} · " +
          $"menace {volume.ThreatRadius:0.##} · {(volume.RequireLineOfSight ? "ligne de vue requise" : "sans ligne de vue")}";

    private static string CandidateSummaryUx2LwF(EncounterWorldProfile.FoundationPolicy? foundation) =>
        foundation is null ? "Liste absente" :
        $"{foundation.Candidates.Count} candidat(s) · budget {foundation.SpawnBudget:0.##} · " +
        $"plafond autoritaire {foundation.NetworkActorCap}";

    private static string DifficultySummaryUx2LwF(EncounterCompositionProfile.DifficultyPolicy? difficulty) =>
        difficulty is null ? "Difficulté absente" :
        $"{HumanDifficultyUx2LwF(difficulty.Band)} · groupe attendu {difficulty.ExpectedPartySize} · " +
        $"puissance {difficulty.ExpectedPartyPower:0.##} · budget effectif {difficulty.EffectiveBudget:0.##}";

    private static string CountSummaryUx2LwF(EncounterCompositionProfile? composition) => composition is null
        ? "Groupes absents"
        : string.Join(" · ", composition.Groups.Select(item =>
            $"{HumanTokenUx2LwF(item.Slug)} : {item.Minimum} à {item.Maximum}, budget {item.Budget:0.##}"));

    private static string HumanEncounterNameUx2LwF(EncounterWorldProfile profile) =>
        profile.Composition is { } composition && !string.IsNullOrWhiteSpace(composition.DisplayName)
            ? composition.DisplayName
            : "Rencontre sans fiche d’assistant";

    private static string HumanVolumeShapeUx2LwF(EncounterWorldProfile.VolumeShape shape) => shape switch
    {
        EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D => "Polygone extrudé",
        EncounterWorldProfile.VolumeShape.Box3D => "Boîte 3D",
        EncounterWorldProfile.VolumeShape.Sphere3D => "Sphère 3D",
        EncounterWorldProfile.VolumeShape.Capsule3D => "Capsule 3D",
        _ => "Forme inconnue",
    };

    private static string HumanDifficultyUx2LwF(EncounterDifficultyBand band) => band switch
    {
        EncounterDifficultyBand.Story => "Narrative",
        EncounterDifficultyBand.Easy => "Facile",
        EncounterDifficultyBand.Standard => "Standard",
        EncounterDifficultyBand.Hard => "Difficile",
        EncounterDifficultyBand.Extreme => "Extrême",
        EncounterDifficultyBand.Custom => "Personnalisée",
        _ => "Inconnue",
    };

    internal static string HumanEncounterDiagnosticUx2LwF(string? code) => code switch
    {
        "world.encounter.volume_invalid" or "RPG_ENCOUNTER_VOLUME_INVALID" =>
            "Corrigez la forme, les dimensions, les filtres ou le rayon du volume d’activation.",
        "world.encounter.pool_invalid" or "world.encounter.candidate_missing" or
            "world.encounter.candidate_invalid" or "RPG_ENCOUNTER_POOL_EMPTY" or
            "RPG_ENCOUNTER_CANDIDATE_INVALID" or "RPG_ENCOUNTER_CANDIDATE_MISSING" =>
            "La liste doit contenir des créatures réelles avec poids, coût, rôle et plafond valides.",
        "world.encounter.difficulty_invalid" or "world.encounter.composition_budget_invalid" or
            "world.encounter.budget_invalid" or "RPG_ENCOUNTER_BUDGET_INVALID" =>
            "La difficulté, l’estimation de groupe et les budgets doivent rester finis, positifs et cohérents.",
        "world.encounter.count_range_invalid" or "RPG_ENCOUNTER_COUNT_INVALID" =>
            "Chaque groupe exige un minimum, un maximum et un budget cohérents avec le plafond autoritaire.",
        "world.encounter.script_missing" or "RPG_ENCOUNTER_SCRIPT_MISSING" =>
            "Choisissez un script ForgeScript réel encore présent dans le module.",
        "world.encounter.anchors_invalid" or "RPG_ENCOUNTER_ANCHOR_INVALID" =>
            "Choisissez au moins une ancre réelle de la zone.",
        "world.encounter.network_cap_invalid" or "RPG_ENCOUNTER_NETWORK_CAP_INVALID" =>
            "Le plafond autoritaire doit couvrir les maximums des candidats et des groupes.",
        "world.reference.area_missing" or "RPG_ENCOUNTER_ZONE_MISSING" =>
            "Choisissez une zone réelle encore présente dans le module.",
        "world.encounter.composition_identity_invalid" or "RPG_ENCOUNTER_IDENTITY_INVALID" =>
            "Renseignez un nom, un identifiant lisible, une catégorie et un thème.",
        _ when !string.IsNullOrWhiteSpace(code) && code.Contains("finite", StringComparison.OrdinalIgnoreCase) =>
            "Une valeur numérique doit être finie et rester dans les limites du contrat.",
        _ => "La validation canonique a refusé cette fiche. Consultez la section concernée.",
    };

    private static string ProjectFingerprintUx2LwF(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string EncounterDraftFingerprintUx2LwF(StudioUx2LwFDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Write(writer, draft.ProfileId.ToString("D")); Write(writer, draft.IsNew ? "1" : "0");
        Write(writer, draft.AreaId.ToString()); Write(writer, draft.BaseRevision.Value.ToString(CultureInfo.InvariantCulture));
        Write(writer, draft.SourceFingerprint); Write(writer, draft.Slug); Write(writer, draft.DisplayName);
        Write(writer, draft.Category); Write(writer, draft.Theme); Write(writer, ((int)draft.PrimaryRole).ToString());
        Write(writer, ((int)draft.Shape).ToString());
        foreach (var point in draft.Points) WriteVector(writer, point);
        WriteVector(writer, draft.Center); Write(writer, Number(draft.Height)); Write(writer, Number(draft.Radius));
        foreach (var value in draft.RequiredEntityTags) Write(writer, "required:" + value);
        foreach (var value in draft.AllowedFactions) Write(writer, "faction:" + value);
        Write(writer, draft.RequireLineOfSight ? "1" : "0"); Write(writer, Number(draft.ThreatRadius));
        Write(writer, draft.ActivationScriptId); Write(writer, draft.VisualizeInEditor ? "1" : "0");
        foreach (var item in draft.Candidates)
        {
            Write(writer, item.Id.ToString("D")); Write(writer, item.BlueprintId.ToString());
            Write(writer, ((int)item.Role).ToString()); Write(writer, Number(item.Weight));
            Write(writer, Number(item.Cost)); Write(writer, item.MaximumAlive.ToString());
            foreach (var value in item.Conditions) Write(writer, "condition:" + value);
            foreach (var value in item.Variants) Write(writer, "variant:" + value);
        }
        Write(writer, Number(draft.SpawnBudget));
        foreach (var value in draft.SpawnAnchorIds) Write(writer, "anchor:" + value);
        foreach (var value in draft.RewardIds) Write(writer, "reward:" + value);
        Write(writer, draft.NetworkActorCap.ToString()); Write(writer, draft.RulesetId);
        Write(writer, draft.PartyAssessmentProviderId); Write(writer, ((int)draft.DifficultyBand).ToString());
        Write(writer, draft.ExpectedPartySize.ToString()); Write(writer, Number(draft.ExpectedPartyPower));
        Write(writer, Number(draft.BaseBudget)); Write(writer, Number(draft.PartySizeMultiplier));
        Write(writer, Number(draft.DifficultyMultiplier));
        foreach (var item in draft.Groups)
        {
            Write(writer, item.Id.ToString("D")); Write(writer, item.Slug); Write(writer, item.Minimum.ToString());
            Write(writer, item.Maximum.ToString()); Write(writer, Number(item.Budget));
            foreach (var id in item.CandidateIds) Write(writer, id.ToString("D"));
        }
        Write(writer, draft.Selection.Id.ToString("D")); Write(writer, draft.Selection.PartyAssessmentProviderId);
        Write(writer, draft.Selection.Strategy); Write(writer, draft.Selection.DeterministicSeed.ToString());
        Write(writer, draft.Selection.PreviewComposition ? "1" : "0");
        Write(writer, draft.Activation.Id.ToString("D")); Write(writer, ((int)draft.Activation.Mode).ToString());
        Write(writer, Number(draft.Activation.IntervalSeconds)); Write(writer, draft.Activation.ScriptId);
        Write(writer, ((int)draft.Activation.Distribution).ToString());
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));

        static void Write(BinaryWriter writer, string? value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length); writer.Write(bytes);
        }
        static void WriteVector(BinaryWriter writer, Vector3Value value)
        {
            writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z);
        }
        static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<string> CleanTokensUx2LwF(IEnumerable<string> values) => values
        .Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    private static string? NormalizeScriptUx2LwF(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim().Replace('\\', '/');

    private static bool IsSafeRelativeScriptUx2LwF(string path) =>
        path.StartsWith("scripts/", StringComparison.Ordinal) && !path.Contains("..", StringComparison.Ordinal) &&
        !Path.IsPathRooted(path);

    private static string HumanScriptNameUx2LwF(string path) =>
        Path.GetFileNameWithoutExtension(path).Replace('-', ' ').Replace('_', ' ');

    private static string HumanProvenanceUx2LwF(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "provenance enregistrée" :
        value.StartsWith("sample", StringComparison.OrdinalIgnoreCase) ? "exemple libre du module" :
        value.StartsWith("studio", StringComparison.OrdinalIgnoreCase) ? "Studio local" :
        "auteur du module";

    private static string HumanTokenUx2LwF(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "sans nom" : value.Trim();
        return Guid.TryParse(text, out _) ? "élément protégé" : text.Replace('-', ' ').Replace('_', ' ');
    }

    private static string SafeSlugUx2LwF(string value)
    {
        var normalized = new string(value.Trim().ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal)) normalized = normalized.Replace("--", "-");
        return normalized.Trim('-');
    }

    private static Guid StableUx2LwF(Guid seed, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-lw-f:{seed:D}:{purpose}"));
        return new Guid(bytes[..16]);
    }

    private static EncounterWorldProfile CreateEvidenceEncounterUx2LwF(
        Guid seed,
        AreaId areaId,
        BlueprintId blueprintId,
        string anchorId,
        string? scriptId,
        string provenance,
        bool richTails)
    {
        var profileId = StableUx2LwF(seed, $"encounter:{areaId}:{blueprintId}");
        var candidateId = StableUx2LwF(profileId, "candidate");
        var groupId = StableUx2LwF(profileId, "group");
        var foundation = new EncounterWorldProfile.FoundationPolicy(
            new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)],
                Vector3Value.Zero, 4, 8, ["player"], [], true, 18, scriptId, false),
            [new(candidateId, blueprintId, EncounterWorldProfile.CandidateRole.Controller,
                3, 8, ["area.ready"], ["evidence"], 4)],
            40, [anchorId], [], 12);
        var composition = new EncounterCompositionProfile(StableUx2LwF(profileId, "composition"), 1,
            provenance, "veille-des-braises", "Veille des braises", "embuscade", "forêt oubliée",
            EncounterWorldProfile.CandidateRole.Controller,
            new(StableUx2LwF(profileId, "difficulty"), "ruleset:forge", "party:studio-evidence",
                EncounterDifficultyBand.Hard, 4, 12, 20, 1.2, 1.5),
            [new(groupId, "garde-des-braises", 2, 4, 36, [candidateId])],
            new(StableUx2LwF(profileId, "selection"), "party:studio-evidence", "budget-bounded", 42, true),
            new(StableUx2LwF(profileId, "activation"), EncounterActivationMode.Once, 0, null,
                EncounterSpawnDistribution.Weighted), new Dictionary<string, string>());
        EncounterScenarioProfile? scenario = null;
        EncounterStatisticsProfile? statistics = null;
        if (richTails)
        {
            var waveId = StableUx2LwF(profileId, "wave");
            scenario = new(StableUx2LwF(profileId, "scenario"), 1, provenance,
                [new(StableUx2LwF(profileId, "scenario-anchor"), anchorId, TransformValue.Identity,
                    8, true, EncounterWorldProfile.VisibilityMode.BehindCover, true, 1)],
                EncounterSpawnDistribution.Weighted,
                new(StableUx2LwF(profileId, "activator"), true, true, true, true, true,
                    EncounterNetworkAuthority.Server, ["party"]),
                new(StableUx2LwF(profileId, "lifecycle"), "1.0.0",
                    new(StableUx2LwF(profileId, "entered"), "EncounterEntered", null),
                    new(StableUx2LwF(profileId, "exited"), "EncounterExited", null),
                    new(StableUx2LwF(profileId, "exhausted"), "EncounterExhausted", null)),
                [new(StableUx2LwF(profileId, "phase"), 1, "Ouverture", [],
                    [new(waveId, 1, [groupId], 0, EncounterWaveCompletionPolicy.AllDefeated,
                        null, null, null, null, null)], [])], new Dictionary<string, string>());
            statistics = new(StableUx2LwF(profileId, "statistics"), 1, provenance, 8, 42,
                "duration:actors-linear", 5, "reward:cost-linear", .5, [.5, .9], false,
                new Dictionary<string, string>());
        }
        return new(profileId, 1, provenance, areaId, null, null, null, null,
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx:embers"),
            new(30, 30, 120, true, true), foundation, composition, true,
            scenario, richTails, statistics, richTails);
    }

    internal StudioUx2LwFSmokeResult RunUx2LwFContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-f-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var reopenedNew = new StudioSessionController();
        try
        {
            var fixture = CreateUx2LwFSmokeProject();
            var created = ProjectSession.Create(tempRoot, fixture.Project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_F_CONTRACT_CREATE_FAILED " +
                                  string.Join(" | ", created.Diagnostics.Select(item =>
                                      $"{item.Code}:{item.Path}:{item.Message}")));
                return FailedUx2LwFSmoke();
            }
            if (!WriteUx2LwFEvidenceScript(tempRoot) || !editing.Open(tempRoot).Succeeded)
                return FailedUx2LwFSmoke();

            var targetChoice = editing.EncounterChoicesUx2LwF.Single(item => item.ProfileId == fixture.TargetId);
            var explicitSelection = editing.CreateEncounterDraftUx2LwF(null) is null &&
                                    editing.CreateNewEncounterDraftUx2LwF(null, null, null) is null &&
                                    targetChoice.IsComplete;
            var loadedProject = editing.Project!;
            var initialCatalog = loadedProject.Module.WorldFeatures;
            var loadedTarget = initialCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var loadedNeighbor = initialCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var draft = editing.CreateEncounterDraftUx2LwF(fixture.TargetId)!;
            var invalidVolume = editing.PreviewEncounterUx2LwF(draft with { ThreatRadius = double.NaN });
            var invalidCandidate = editing.PreviewEncounterUx2LwF(draft with
            {
                Candidates = [draft.Candidates[0] with { Weight = double.PositiveInfinity }],
            });
            var invalidDifficulty = editing.PreviewEncounterUx2LwF(draft with { BaseBudget = double.NaN });
            var invalidCount = editing.PreviewEncounterUx2LwF(draft with
            {
                Groups = [draft.Groups[0] with { Minimum = 5, Maximum = 2 }],
            });
            var invalidScript = editing.PreviewEncounterUx2LwF(draft with
            {
                ActivationScriptId = "scripts/source-absente.forge",
            });
            var volumeValidation = !invalidVolume.Succeeded && invalidVolume.Diagnostics.Any(item =>
                item.Code is "world.encounter.volume_invalid" or "RPG_ENCOUNTER_VOLUME_INVALID");
            var candidateValidation = !invalidCandidate.Succeeded && invalidCandidate.Diagnostics.Any(item =>
                item.Code is "world.encounter.candidate_invalid" or "RPG_ENCOUNTER_CANDIDATE_INVALID");
            var difficultyValidation = !invalidDifficulty.Succeeded && invalidDifficulty.Diagnostics.Any(item =>
                item.Code is "world.encounter.difficulty_invalid" or "world.encounter.composition_budget_invalid" or
                    "RPG_ENCOUNTER_BUDGET_INVALID");
            var countValidation = !invalidCount.Succeeded && invalidCount.Diagnostics.Any(item =>
                item.Code is "world.encounter.count_range_invalid" or "RPG_ENCOUNTER_COUNT_INVALID");
            var realScriptValidation = !invalidScript.Succeeded && invalidScript.Diagnostics.Any(item =>
                item.Code == "world.encounter.script_missing") &&
                editing.EncounterScriptChoicesUx2LwF().Any(item =>
                    item.Id == "scripts/encounter_activate.forge");

            var changedDraft = draft with
            {
                ThreatRadius = draft.ThreatRadius + 1,
                Candidates = draft.Candidates.Select((item, index) => index == 0
                    ? item with { Weight = item.Weight + 1 }
                    : item).ToArray(),
                BaseBudget = 21,
                Groups = draft.Groups.Select((item, index) => index == 0
                    ? item with { Minimum = 1 }
                    : item).ToArray(),
                ActivationScriptId = "scripts/encounter_activate.forge",
            };
            var staleDraftSource = editing.CreateEncounterDraftUx2LwF(fixture.TargetId)!;
            var changedBeforePreview = ApplyUx2LwFIntermediateMutation(editing, fixture.NeighborId, 11);
            var staleDraftPreview = editing.PreviewEncounterUx2LwF(staleDraftSource);
            var staleDraftBlocked = changedBeforePreview && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("changé", StringComparison.OrdinalIgnoreCase) &&
                                    editing.Undo().Succeeded;

            changedDraft = editing.CreateEncounterDraftUx2LwF(fixture.TargetId)! with
            {
                ThreatRadius = draft.ThreatRadius + 1,
                Candidates = draft.Candidates.Select((item, index) => index == 0
                    ? item with { Weight = item.Weight + 1 }
                    : item).ToArray(),
                BaseBudget = 21,
                Groups = draft.Groups.Select((item, index) => index == 0
                    ? item with { Minimum = 1 }
                    : item).ToArray(),
                ActivationScriptId = "scripts/encounter_activate.forge",
            };
            var approvalPreview = editing.PreviewEncounterUx2LwF(changedDraft);
            var changedAfterPreview = ApplyUx2LwFIntermediateMutation(editing, fixture.NeighborId, 12);
            var staleApply = editing.ApplyApprovedEncounterUx2LwF();
            var staleApprovalBlocked = approvalPreview.Succeeded && changedAfterPreview && !staleApply.Succeeded &&
                                       staleApply.Message.Contains("Prévisualisez", StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            changedDraft = editing.CreateEncounterDraftUx2LwF(fixture.TargetId)! with
            {
                ThreatRadius = draft.ThreatRadius + 1,
                Candidates = draft.Candidates.Select((item, index) => index == 0
                    ? item with { Weight = item.Weight + 1 }
                    : item).ToArray(),
                BaseBudget = 21,
                Groups = draft.Groups.Select((item, index) => index == 0
                    ? item with { Minimum = 1 }
                    : item).ToArray(),
                ActivationScriptId = "scripts/encounter_activate.forge",
            };
            var historyBefore = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = historyBefore.State.Revision;
            var journalBefore = historyBefore.Journal.ToArray();
            var dirtyBefore = editing.IsDirty;
            var preview = editing.PreviewEncounterUx2LwF(changedDraft);
            var repeat = editing.PreviewEncounterUx2LwF(changedDraft);
            var dryRunPure = preview.Succeeded && repeat.Succeeded && preview.DryRunPure &&
                             ReferenceEquals(projectBefore, editing.Project) &&
                             ReferenceEquals(historyBefore, editing.History) &&
                             historyBefore.State.Revision == revisionBefore &&
                             historyBefore.Journal.SequenceEqual(journalBefore) &&
                             editing.IsDirty == dirtyBefore;
            var deterministicPreview = preview.Runtime.Pool == repeat.Runtime.Pool &&
                                       preview.Runtime.Count == repeat.Runtime.Count &&
                                       preview.Runtime.Boundary.Contains("aucun volume moteur", StringComparison.Ordinal);
            var applied = editing.ApplyApprovedEncounterUx2LwF();
            if (!applied.Succeeded) return FailedUx2LwFSmoke();
            var appliedCatalog = editing.Project!.Module.WorldFeatures;
            var appliedTarget = appliedCatalog.Encounters.Single(item => item.Id == fixture.TargetId);
            var appliedNeighbor = appliedCatalog.Encounters.Single(item => item.Id == fixture.NeighborId);
            var targetNeighborsPreserved =
                ReferenceEquals(loadedTarget.Respawn, appliedTarget.Respawn) &&
                loadedTarget.SpawnFactionId == appliedTarget.SpawnFactionId &&
                ReferenceEquals(loadedTarget.Scheduler, appliedTarget.Scheduler) &&
                ReferenceEquals(loadedTarget.Reinforcements, appliedTarget.Reinforcements) &&
                ReferenceEquals(loadedTarget.Visibility, appliedTarget.Visibility) &&
                ReferenceEquals(loadedTarget.Cleanup, appliedTarget.Cleanup) &&
                ReferenceEquals(loadedTarget.Scenario, appliedTarget.Scenario) &&
                loadedTarget.ScenarioRequiredAtRuntime == appliedTarget.ScenarioRequiredAtRuntime &&
                ReferenceEquals(loadedTarget.Statistics, appliedTarget.Statistics) &&
                loadedTarget.StatisticsRequiredAtRuntime == appliedTarget.StatisticsRequiredAtRuntime;
            var projectNeighborsPreserved = ReferenceEquals(loadedNeighbor, appliedNeighbor) &&
                                            loadedProject.Blueprints.SequenceEqual(editing.Project.Blueprints) &&
                                            loadedProject.Areas.SequenceEqual(editing.Project.Areas) &&
                                            loadedProject.Dialogues.SequenceEqual(editing.Project.Dialogues) &&
                                            loadedProject.Quests.SequenceEqual(editing.Project.Quests);
            var collectionPreserved = SameReferencesUx2LwF(initialCatalog.Placeables, appliedCatalog.Placeables) &&
                                      SameReferencesUx2LwF(initialCatalog.Doors, appliedCatalog.Doors) &&
                                      SameReferencesUx2LwF(initialCatalog.Triggers, appliedCatalog.Triggers) &&
                                      SameReferencesUx2LwF(initialCatalog.VehicleRoutes, appliedCatalog.VehicleRoutes) &&
                                      SameReferencesUx2LwF(initialCatalog.Merchants, appliedCatalog.Merchants) &&
                                      SameReferencesUx2LwF(initialCatalog.Companions, appliedCatalog.Companions) &&
                                      ReferenceEquals(initialCatalog.Scripts, appliedCatalog.Scripts) &&
                                      appliedCatalog.Encounters.Select(item => item.Id).SequenceEqual(
                                          initialCatalog.Encounters.Select(item => item.Id));

            var undo = editing.Undo();
            var undone = editing.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var redo = editing.Redo();
            var redone = editing.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == fixture.TargetId);
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                           EncounterProfileJsonUx2LwF(undone, editing.Project!) ==
                           EncounterProfileJsonUx2LwF(loadedTarget, loadedProject) &&
                           EncounterProfileJsonUx2LwF(redone, editing.Project!) ==
                           EncounterProfileJsonUx2LwF(appliedTarget, editing.Project!);
            var saved = editing.Save();
            var opened = saved.Succeeded && reopened.Open(tempRoot).Succeeded;
            var persistedTarget = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == fixture.TargetId) : null;
            var persistence = persistedTarget is not null &&
                              persistedTarget.Foundation?.Candidates.Count == appliedTarget.Foundation?.Candidates.Count &&
                              persistedTarget.Composition?.Groups.Count == appliedTarget.Composition?.Groups.Count &&
                              EncounterProfileJsonUx2LwF(persistedTarget, reopened.Project!) ==
                              EncounterProfileJsonUx2LwF(appliedTarget, editing.Project!) &&
                              ProjectJsonSerializer.Serialize(editing.Project!) ==
                              ProjectJsonSerializer.Serialize(reopened.Project!);
            reopened.Close();

            var areaChoice = editing.EncounterAreaChoicesUx2LwF().Single(item => item.Id == fixture.AreaId);
            var creatureChoice = editing.EncounterCreatureChoicesUx2LwF().First(item =>
                item.Id != fixture.TargetBlueprintId);
            var anchorChoice = editing.EncounterAnchorChoicesUx2LwF(areaChoice.Id).Single();
            var newDraft = editing.CreateNewEncounterDraftUx2LwF(areaChoice.Id, creatureChoice.Id, anchorChoice.Id);
            if (newDraft is null) return FailedUx2LwFSmoke();
            newDraft = newDraft with { ActivationScriptId = "scripts/encounter_activate.forge" };
            var newPreview = editing.PreviewEncounterUx2LwF(newDraft);
            var newApply = editing.ApplyApprovedEncounterUx2LwF();
            var newSaved = editing.Save();
            var newOpened = newSaved.Succeeded && reopenedNew.Open(tempRoot).Succeeded;
            var newAssistant = newPreview.Succeeded && newPreview.CanApply && newApply.Succeeded && newOpened &&
                               reopenedNew.Project!.Module.WorldFeatures.Encounters.Any(item =>
                                   item.Id == newDraft.ProfileId && item.Foundation?.Candidates.Count == 1 &&
                                   item.Composition?.Groups.Single().Minimum == 1);
            reopenedNew.Close();

            var journal = editing.History!.Journal;
            var canonicalCommand = journal.Count >= 3 &&
                                   journal.All(item => item.CommandName == "set_world_feature_catalog");
            var diagnosticSets = new[]
            {
                invalidVolume, invalidCandidate, invalidDifficulty, invalidCount, invalidScript,
            };
            var structuredDiagnostics = diagnosticSets.SelectMany(item => item.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
            var humanText = editing.EncounterChoicesUx2LwF.All(item => IsHumanUx2LwFText(item.Label)) &&
                            editing.EncounterAreaChoicesUx2LwF().All(item => IsHumanUx2LwFText(item.Label)) &&
                            editing.EncounterCreatureChoicesUx2LwF().All(item =>
                                IsHumanUx2LwFText(item.Label) && IsHumanUx2LwFText(item.Provenance));
            var succeeded = explicitSelection && newAssistant && volumeValidation && candidateValidation &&
                            difficultyValidation && countValidation && realScriptValidation &&
                            deterministicPreview && dryRunPure && staleDraftBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && targetNeighborsPreserved &&
                            projectNeighborsPreserved && collectionPreserved && structuredDiagnostics && humanText;
            return new(succeeded, explicitSelection, newAssistant, volumeValidation, candidateValidation,
                difficultyValidation, countValidation, realScriptValidation, deterministicPreview, dryRunPure,
                staleDraftBlocked, staleApprovalBlocked, canonicalCommand, undoRedo, persistence,
                targetNeighborsPreserved, projectNeighborsPreserved, collectionPreserved,
                structuredDiagnostics, humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_F_CONTRACT_FAILED exception={exception.Message}");
            return FailedUx2LwFSmoke();
        }
        finally
        {
            reopenedNew.Close();
            reopened.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of this isolated temporary workspace only.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup of this isolated temporary workspace only.
            }
        }
    }

    private static bool ApplyUx2LwFIntermediateMutation(
        StudioSessionController session,
        Guid profileId,
        double cameraDistance)
    {
        var history = session.History;
        var project = session.Project;
        if (history is null || project is null) return false;
        var source = project.Module.WorldFeatures;
        var encounters = source.Encounters.Select(item => item.Id == profileId
            ? item with { Visibility = item.Visibility with { MinimumCameraDistance = cameraDistance } }
            : item).ToArray();
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var result = new SetWorldFeatureCatalogHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, catalog, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static Ux2LwFSmokeFixture CreateUx2LwFSmokeProject()
    {
        var source = SampleModuleAuthoringService.Create().State.Project;
        var area = source.Areas.Single();
        var spawnBlueprint = source.Blueprints.First(item => item.Kind == BlueprintKind.PlayerSpawn);
        var definition = new AnchorDefinition(StableUx2LwF(area.Id.Value, "spawn-definition"), 1,
            Ux2LwFEvidenceProvenance, "clairiere-rencontre",
            new("anchor.encounter", "Clairière des rencontres"), AnchorKind.Spawn, AnchorScope.Area,
            ["encounter"], spawnBlueprint.Id, null, true);
        var instance = new AnchorInstance(StableUx2LwF(area.Id.Value, "spawn-instance"), 1,
            Ux2LwFEvidenceProvenance, definition.Id, TransformValue.Identity, 1,
            Forge.Domain.Entities.EntityPropertyOverrides.Empty,
            new("encounter", new(1.2f, 2, 1.2f), 100, "neutral", 0, null, true), null);
        var anchors = new AnchorCatalog(StableUx2LwF(area.Id.Value, "anchor-catalog"), 1,
            Ux2LwFEvidenceProvenance, area.Id, true, [definition], [instance]);
        var authoredArea = area.WithNavigation(area.Navigation.WithAnchors(anchors));
        var creatures = source.Blueprints.Where(item => item.Kind == BlueprintKind.Creature).Take(2).ToArray();
        var anchorId = definition.Slug;
        var target = CreateEvidenceEncounterUx2LwF(source.Module.Id.Value, authoredArea.Id, creatures[0].Id,
            anchorId, "scripts/encounter_activate.forge", "test:ux2-lw-f", richTails: true);
        var neighborSeed = StableUx2LwF(source.Module.Id.Value, "neighbor-seed");
        var neighbor = CreateEvidenceEncounterUx2LwF(neighborSeed, authoredArea.Id, creatures[1].Id,
            anchorId, null, "test:ux2-lw-f-neighbor", richTails: false);
        var catalog = new WorldFeatureCatalog(source.Module.Id.Value, 1, "test:ux2-lw-f",
            encounters: [target, neighbor]);
        var project = new ProjectDefinition(source.Id, "Studio UX2-LW-F",
            source.Module.WithWorldFeatures(catalog), source.Blueprints, [authoredArea],
            source.Dialogues, source.Quests);
        return new(project, authoredArea.Id, target.Id, neighbor.Id, creatures[0].Id);
    }

    private static bool WriteUx2LwFEvidenceScript(string root)
    {
        try
        {
            var scripts = Path.Combine(root, "scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "encounter_activate.forge"),
                "void OnEncounterActivate() { }\n", Encoding.UTF8);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string EncounterProfileJsonUx2LwF(EncounterWorldProfile profile, ProjectDefinition project)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, [profile], source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return ProjectJsonSerializer.Serialize(new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests));
    }

    private static bool SameReferencesUx2LwF<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        where T : class => left.Count == right.Count &&
                           left.Zip(right).All(pair => ReferenceEquals(pair.First, pair.Second));

    private static bool IsHumanUx2LwFText(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("module/worldFeatures", StringComparison.Ordinal) &&
        !value.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));

    private static StudioUx2LwFSmokeResult FailedUx2LwFSmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private sealed record Ux2LwFSmokeFixture(
        ProjectDefinition Project,
        AreaId AreaId,
        Guid TargetId,
        Guid NeighborId,
        BlueprintId TargetBlueprintId);
}
