using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetCreatureEventsHandler _setCreatureEventsUx4N = new(new SystemClock());
    private StudioCreatureEventDraftUx4N? _approvedCreatureEventDraftUx4N;
    private StudioCreatureEventCandidateUx4N? _approvedCreatureEventCandidateUx4N;
    private ProjectRevision? _approvedCreatureEventRevisionUx4N;
    private string? _approvedCreatureEventProjectFingerprintUx4N;
    private string? _approvedCreatureEventDraftFingerprintUx4N;
    private string? _approvedCreatureEventCandidateFingerprintUx4N;

    internal IReadOnlyList<StudioCreatureEventChoiceUx4N> CreatureEventChoicesUx4N =>
        Project?.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioCreatureEventChoiceUx4N(item.Id,
                $"{item.DisplayName} · {(item.CreatureEvents is null ? "profil absent" :
                    $"{CreatureEventValidator.EnumerateBindings(item.CreatureEvents).Count} binding(s)")}",
                item.CreatureEvents is not null, item.IsReadOnly())).ToArray() ?? [];

    internal StudioCreatureEventDraftUx4N? CreateCreatureEventDraftUx4N(BlueprintId blueprintId)
    {
        var project = Project;
        var history = History;
        var blueprint = project?.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (project is null || history is null || blueprint is null || blueprint.IsReadOnly() ||
            blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return null;
        var source = blueprint.CreatureEvents;
        var working = source ?? CanonicalCreatureEventsUx4N(blueprint.Id);
        if (!Enum.IsDefined(working.CombatBoundary?.Mode ?? CreatureCombatBoundaryMode.CombatTickCompleted))
            return null;
        return new(history.State.Revision, FingerprintProjectUx4N(project), blueprint.Id, blueprint, source,
            working.RequiredAtRuntime,
            working.CombatBoundary is not null,
            working.CombatBoundary?.Mode ?? CreatureCombatBoundaryMode.CombatTickCompleted,
            working.CombatBoundary?.Binding.Handler ?? "OnCombatTickCompleted",
            working.Inventory is not null,
            working.Inventory?.Accessed.Handler ?? "OnInventoryAccessed",
            working.Inventory?.Transferred.Handler ?? "OnItemTransferred",
            working.Rest is not null,
            working.Rest?.Completed.Handler ?? "OnRestCompleted",
            working.Heartbeat?.Enabled == true,
            (working.Heartbeat?.IntervalMilliseconds ?? 2_000).ToString(CultureInfo.InvariantCulture),
            (working.Heartbeat?.BudgetMilliseconds ?? 8).ToString(CultureInfo.InvariantCulture),
            working.Heartbeat?.SuspendOutsideInterest != false,
            working.Heartbeat?.Binding.Handler ?? "OnHeartbeat",
            string.Join('\n', working.CustomEvents.Select(item =>
                $"{item.Namespace}|{item.Name}|{item.PayloadVersion}|" +
                $"{item.Required.ToString().ToLowerInvariant()}|" +
                $"{string.Join(',', item.Fields.Select(field => $"{field.Key}:{field.Value}"))}|" +
                item.Binding.Handler)));
    }

    internal StudioCreatureEventPreviewUx4N PreviewCreatureEventsUx4N(StudioCreatureEventDraftUx4N draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatureEventsUx4N();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedCreatureEventsUx4N("Aucun module n’est ouvert.", "studio.creature.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintProjectUx4N(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedCreatureEventsUx4N("Le projet a changé ; rechargez la fiche.",
                SetCreatureEventsHandler.RevisionConflictCode);
        var source = project.Blueprints.SingleOrDefault(item => item.Id == draft.BlueprintId);
        if (source is null)
            return FailedCreatureEventsUx4N("La créature n’existe plus.",
                SetCreatureEventsHandler.BlueprintMissingCode);
        if (!TryBuildCreatureEventCandidateUx4N(draft, out var candidate, out var diagnostics))
            return new(false, false, "Les événements avancés sont invalides.", string.Empty, string.Empty,
                diagnostics, "Cycle non évalué.", "Heartbeat non évalué.", "Événements non évalués.",
                "Runtime non évalué.");
        var dryRun = _setCreatureEventsUx4N.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, draft.BlueprintId, candidate.Profile,
                DryRun: true));
        if (!dryRun.Succeeded)
            return new(false, false, "La commande canonique refuse le profil.", string.Empty, string.Empty,
                CreatureEventDiagnosticsUx4N(dryRun.Diagnostics), LifecycleSummaryUx4N(candidate.Profile),
                HeartbeatSummaryUx4N(candidate.Profile), CustomSummaryUx4N(candidate.Profile),
                "Runtime non évalué.");

        try
        {
            var profile = candidate.Profile;
            var combat = profile.CombatBoundary is null ? null : CreatureEventRuntimeResolver.CombatBoundary(
                profile, profile.CombatBoundary.Mode, source.Slug, 7, "studio-preview");
            var inventory = CreatureEventRuntimeResolver.InventoryAccessed(profile, source.Slug, "preview-actor",
                "inspection", "accepted");
            var rest = CreatureEventRuntimeResolver.RestCompleted(profile, source.Slug, 28_800,
                new Dictionary<string, double> { ["health"] = 12, ["focus"] = 4 }, ["none"]);
            var heartbeat = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile, CreatureEventRuntimeState.Empty,
                profile.Heartbeat?.IntervalMilliseconds ?? 0, true, source.Slug);
            CreatureEventDispatch? custom = null;
            if (profile.CustomEvents.FirstOrDefault() is { } first)
                custom = CreatureEventRuntimeResolver.Custom(profile, first.SchemaId,
                    SampleCustomPayloadUx4N(first.Fields));
            var script = new CreatureEventScriptService(profile).Read(new(profile.Id, profile.SchemaVersion));
            var dispatched = new[] { combat, inventory, rest, custom }.Count(item => item is not null) +
                             heartbeat.Dispatches.Count;
            var runtime = $"Projection pure : {dispatched} dispatch(es), {script.BindingCount} binding(s), " +
                          $"autorité serveur, backlog heartbeat borné à {CreatureEventRuntimeResolver.MaximumCatchUpDispatches}. " +
                          "Aucun script, acteur, inventaire, repos ou réseau n’est exécuté dans Studio.";
            var frozen = draft with { };
            _approvedCreatureEventDraftUx4N = frozen;
            _approvedCreatureEventCandidateUx4N = candidate;
            _approvedCreatureEventRevisionUx4N = history.State.Revision;
            _approvedCreatureEventProjectFingerprintUx4N = FingerprintProjectUx4N(project);
            _approvedCreatureEventDraftFingerprintUx4N = FingerprintCreatureEventDraftUx4N(frozen);
            _approvedCreatureEventCandidateFingerprintUx4N =
                FingerprintCreatureEventCandidateUx4N(project, draft.BlueprintId, candidate);
            return new(true, true, "Bindings et budgets valides · prêts à être appliqués.",
                dryRun.Patch?.Summary ?? "Définir les événements avancés.",
                dryRun.Inverse?.Summary ?? "Restaurer les événements précédents.", [],
                LifecycleSummaryUx4N(profile), HeartbeatSummaryUx4N(profile), CustomSummaryUx4N(profile), runtime);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return FailedCreatureEventsUx4N("La projection runtime refuse le profil.",
                "studio.creature.events.runtime_invalid", exception.Message);
        }
    }

    internal StudioCreatureEventActionResultUx4N ApplyApprovedCreatureEventsUx4N(
        StudioCreatureEventDraftUx4N currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var project = Project;
        var history = History;
        var approved = _approvedCreatureEventDraftUx4N;
        var candidate = _approvedCreatureEventCandidateUx4N;
        if (project is null || history is null || approved is null || candidate is null ||
            _approvedCreatureEventRevisionUx4N is null ||
            _approvedCreatureEventProjectFingerprintUx4N is null ||
            _approvedCreatureEventDraftFingerprintUx4N is null ||
            _approvedCreatureEventCandidateFingerprintUx4N is null)
            return new(false, "Prévisualisez les événements avant de les appliquer.");
        var stale = history.State.Revision != _approvedCreatureEventRevisionUx4N ||
                    !string.Equals(FingerprintProjectUx4N(project),
                        _approvedCreatureEventProjectFingerprintUx4N, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureEventDraftUx4N(currentDraft),
                        _approvedCreatureEventDraftFingerprintUx4N, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureEventDraftUx4N(approved),
                        _approvedCreatureEventDraftFingerprintUx4N, StringComparison.Ordinal) ||
                    !string.Equals(FingerprintCreatureEventCandidateUx4N(project, currentDraft.BlueprintId,
                            candidate), _approvedCreatureEventCandidateFingerprintUx4N, StringComparison.Ordinal);
        if (stale)
        {
            CancelCreatureEventsUx4N();
            return new(false, "Le brouillon, le projet ou l’approbation a changé ; prévisualisez à nouveau.");
        }
        var validation = _setCreatureEventsUx4N.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Profile, DryRun: true));
        if (!validation.Succeeded)
        {
            CancelCreatureEventsUx4N();
            return new(false, "La revalidation canonique refuse le profil.");
        }
        var result = _setCreatureEventsUx4N.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, currentDraft.BlueprintId,
                candidate.Profile, DryRun: false));
        CancelCreatureEventsUx4N();
        if (!result.Succeeded) return new(false, "La commande n’a pas appliqué le profil.");
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, result.Patch?.Summary ?? "Événements avancés appliqués.");
    }

    internal StudioCreatureEventActionResultUx4N UndoCreatureEventsUx4N()
    {
        CancelCreatureEventsUx4N();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioCreatureEventActionResultUx4N RedoCreatureEventsUx4N()
    {
        CancelCreatureEventsUx4N();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelCreatureEventsUx4N()
    {
        _approvedCreatureEventDraftUx4N = null;
        _approvedCreatureEventCandidateUx4N = null;
        _approvedCreatureEventRevisionUx4N = null;
        _approvedCreatureEventProjectFingerprintUx4N = null;
        _approvedCreatureEventDraftFingerprintUx4N = null;
        _approvedCreatureEventCandidateFingerprintUx4N = null;
    }

    internal void CloseUx4N() => CancelCreatureEventsUx4N();

    internal StudioCreatureEventApprovalSnapshotUx4N CaptureCreatureEventApprovalUx4N() => new(
        _approvedCreatureEventDraftUx4N, _approvedCreatureEventCandidateUx4N,
        _approvedCreatureEventRevisionUx4N, _approvedCreatureEventProjectFingerprintUx4N,
        _approvedCreatureEventDraftFingerprintUx4N, _approvedCreatureEventCandidateFingerprintUx4N);

    internal void RestoreCreatureEventApprovalUx4N(StudioCreatureEventApprovalSnapshotUx4N snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatureEventDraftUx4N = snapshot.Draft;
        _approvedCreatureEventCandidateUx4N = snapshot.Candidate;
        _approvedCreatureEventRevisionUx4N = snapshot.Revision;
        _approvedCreatureEventProjectFingerprintUx4N = snapshot.ProjectFingerprint;
        _approvedCreatureEventDraftFingerprintUx4N = snapshot.DraftFingerprint;
        _approvedCreatureEventCandidateFingerprintUx4N = snapshot.CandidateFingerprint;
    }

    internal StudioCreatureEventEvidenceProjectUx4N? CreateUx4NEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var real = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly() &&
                item.CreatureEvents is { CombatBoundary: not null, Inventory: not null, Rest: not null,
                    Heartbeat: not null, CustomEvents.Count: > 0 })
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault(item => CreatureEventValidator.Validate(item.CreatureEvents!).Count == 0);
        if (real is not null) return new(project, real.Id, true, false);
        var editable = project.Blueprints.Where(item =>
                (item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn) && !item.IsReadOnly())
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
        if (editable is null) return null;
        var profile = CanonicalCreatureEventsUx4N(editable.Id);
        return new(WithCreatureEventsUx4N(project, editable.Id, profile), editable.Id, false, true);
    }

    internal StudioUx4NSmokeResult RunUx4NContractSmoke()
    {
        var empty = new StudioUx4NSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4NEvidenceProject();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4n-smoke-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx4NSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                    return empty;
                var explicitSelection = editing.CreatureEventChoicesUx4N.Count > 0 &&
                    editing.CreatureEventChoicesUx4N.All(item =>
                        !item.Label.Contains(item.BlueprintId.ToString(), StringComparison.OrdinalIgnoreCase));
                var draft = editing.CreateCreatureEventDraftUx4N(evidence.BlueprintId);
                if (draft is null) return empty with { ExplicitSelection = explicitSelection };
                var changed = ChangedCreatureEventDraftUx4N(draft);
                var sourceProject = editing.Project!;
                var sourceHistory = editing.History!;
                var preview = editing.PreviewCreatureEventsUx4N(changed);
                var dryRun = preview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             ReferenceEquals(sourceHistory, editing.History) &&
                             sourceHistory.State.Revision == draft.BaseRevision;
                var runtimeProjection = preview.Succeeded &&
                                        preview.RuntimeSummary.Contains("dispatch", StringComparison.OrdinalIgnoreCase);
                var invalid = editing.PreviewCreatureEventsUx4N(changed with
                {
                    HeartbeatIntervalMilliseconds = "100"
                });
                var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                    item.Code == "creature.events.heartbeat_interval_invalid");
                var duplicate = editing.PreviewCreatureEventsUx4N(changed with
                {
                    CustomEvents = changed.CustomEvents + "\ncrypt|braziers_lit|2|true|count:Integer|OnDuplicate"
                });
                var duplicateBlocked = !duplicate.Succeeded && duplicate.Diagnostics.Any(item =>
                    item.Code == "creature.events.custom_duplicate");

                var approvedBeforeRevision = editing.PreviewCreatureEventsUx4N(changed);
                var neighborChanged = ToggleCreatureEventNeighborUx4N(editing, evidence.BlueprintId);
                var staleDraftBlocked = approvedBeforeRevision.Succeeded && neighborChanged &&
                    !editing.ApplyApprovedCreatureEventsUx4N(changed).Succeeded;
                if (neighborChanged) editing.UndoCreatureEventsUx4N();

                var fresh = editing.CreateCreatureEventDraftUx4N(evidence.BlueprintId)!;
                var freshChanged = ChangedCreatureEventDraftUx4N(fresh);
                var approvedBeforeTamper = editing.PreviewCreatureEventsUx4N(freshChanged);
                var staleApprovalBlocked = approvedBeforeTamper.Succeeded &&
                    !editing.ApplyApprovedCreatureEventsUx4N(freshChanged with
                    {
                        CombatHandler = freshChanged.CombatHandler + "Tampered"
                    }).Succeeded;

                fresh = editing.CreateCreatureEventDraftUx4N(evidence.BlueprintId)!;
                freshChanged = ChangedCreatureEventDraftUx4N(fresh);
                var beforeApply = editing.Project!;
                var beforeProfile = beforeApply.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureEvents;
                var finalPreview = editing.PreviewCreatureEventsUx4N(freshChanged);
                var apply = finalPreview.Succeeded
                    ? editing.ApplyApprovedCreatureEventsUx4N(freshChanged)
                    : new StudioCreatureEventActionResultUx4N(false, "preview failed");
                var appliedProject = editing.Project;
                var applied = appliedProject?.Blueprints.Single(item => item.Id == evidence.BlueprintId)
                    .CreatureEvents;
                var combat = applied?.CombatBoundary?.Binding.Handler == "OnCombatRoundBoundary" &&
                             applied.CombatBoundary.Mode == CreatureCombatBoundaryMode.TurnEnded;
                var inventory = applied?.Inventory?.Accessed.Handler == "OnInventoryInspected" &&
                                applied.Inventory.Transferred.Handler == "OnItemMoved";
                var rest = applied?.Rest?.Completed.Handler == "OnRestResolved";
                var heartbeat = applied?.Heartbeat is { Enabled: true, IntervalMilliseconds: 1_500,
                    BudgetMilliseconds: 8, SuspendOutsideInterest: true };
                var custom = applied?.CustomEvents.Count == 1 &&
                             applied.CustomEvents[0].QualifiedName == "crypt.braziers_lit" &&
                             applied.CustomEvents[0].Fields.Count == 2;
                var damageDeath = Equals(beforeProfile?.DamageDeath, applied?.DamageDeath);
                var canonical = apply.Succeeded && combat && inventory && rest && heartbeat && custom;
                var expected = applied is null ? null : WithCreatureEventsUx4N(beforeApply,
                    evidence.BlueprintId, applied);
                var neighbors = canonical && expected is not null && appliedProject is not null &&
                                ProjectJsonSerializer.Serialize(expected) ==
                                ProjectJsonSerializer.Serialize(appliedProject);
                var appliedJson = appliedProject is null ? string.Empty :
                    ProjectJsonSerializer.Serialize(appliedProject);
                var beforeJson = ProjectJsonSerializer.Serialize(beforeApply);
                var undo = canonical && editing.UndoCreatureEventsUx4N().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == beforeJson;
                var redo = undo && editing.RedoCreatureEventsUx4N().Succeeded &&
                           ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = redo && editing.Save().Succeeded;
                var persisted = saved && reopened.Open(root).Succeeded &&
                                ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                return new(canonical && explicitSelection && combat && inventory && rest && heartbeat && custom &&
                        invalidBlocked && duplicateBlocked && dryRun && runtimeProjection && staleDraftBlocked &&
                        staleApprovalBlocked && undo && redo && persisted && neighbors && damageDeath,
                    explicitSelection, combat, inventory, rest, heartbeat, custom, invalidBlocked,
                    duplicateBlocked, dryRun, runtimeProjection, staleDraftBlocked, staleApprovalBlocked,
                    canonical, undo && redo, persisted, neighbors, damageDeath, false);
            }
            catch
            {
                return empty;
            }
        }

        var result = Execute();
        reopened.Close();
        editing.Close();
        var released = DeleteUx4NSmokeRoot(root);
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    internal static StudioCreatureEventDraftUx4N ChangedCreatureEventDraftUx4N(
        StudioCreatureEventDraftUx4N draft) => draft with
    {
        RequiredAtRuntime = true,
        CombatEnabled = true,
        CombatMode = CreatureCombatBoundaryMode.TurnEnded,
        CombatHandler = "OnCombatRoundBoundary",
        InventoryEnabled = true,
        InventoryAccessedHandler = "OnInventoryInspected",
        ItemTransferredHandler = "OnItemMoved",
        RestEnabled = true,
        RestHandler = "OnRestResolved",
        HeartbeatEnabled = true,
        HeartbeatIntervalMilliseconds = "1500",
        HeartbeatBudgetMilliseconds = "8",
        SuspendOutsideInterest = true,
        HeartbeatHandler = "OnHeartbeat",
        CustomEvents = "crypt|braziers_lit|2|true|count:Integer,source:EntityId|OnBraziersLit",
    };

    private bool TryBuildCreatureEventCandidateUx4N(
        StudioCreatureEventDraftUx4N draft,
        out StudioCreatureEventCandidateUx4N candidate,
        out IReadOnlyList<StudioCreatureEventDiagnosticUx4N> diagnostics)
    {
        var errors = new List<StudioCreatureEventDiagnosticUx4N>();
        try
        {
            if (!Enum.IsDefined(draft.CombatMode))
                errors.Add(new("creature.events.combat_mode_invalid", "creatureEvents/combatBoundary",
                    "La frontière de combat sélectionnée est inconnue."));
            var source = draft.SourceProfile ?? CanonicalCreatureEventsUx4N(draft.BlueprintId);
            var combatEvent = draft.CombatMode == CreatureCombatBoundaryMode.TurnEnded
                ? "creature.turn_ended"
                : "creature.combat_tick_completed";
            var combat = draft.CombatEnabled
                ? new CreatureEventProfile.CreatureCombatBoundaryBinding(draft.CombatMode,
                    UpdateBindingUx4N(source.CombatBoundary?.Binding,
                        StableCreatureEventIdUx4N(source.Id, "combat"), combatEvent, draft.CombatHandler,
                        source.Provenance))
                : null;
            var inventory = draft.InventoryEnabled
                ? new CreatureEventProfile.CreatureInventoryEventBindings(
                    UpdateBindingUx4N(source.Inventory?.Accessed,
                        StableCreatureEventIdUx4N(source.Id, "inventory-accessed"),
                        "creature.inventory_accessed", draft.InventoryAccessedHandler, source.Provenance),
                    UpdateBindingUx4N(source.Inventory?.Transferred,
                        StableCreatureEventIdUx4N(source.Id, "item-transferred"),
                        "creature.item_transferred", draft.ItemTransferredHandler, source.Provenance))
                : null;
            var rest = draft.RestEnabled
                ? new CreatureEventProfile.CreatureRestEventBinding(UpdateBindingUx4N(source.Rest?.Completed,
                    StableCreatureEventIdUx4N(source.Id, "rest-completed"), "creature.rest_completed",
                    draft.RestHandler, source.Provenance))
                : null;
            var heartbeat = new CreatureEventProfile.CreatureHeartbeatBinding(draft.HeartbeatEnabled,
                ParseIntUx4N(draft.HeartbeatIntervalMilliseconds, "cadence heartbeat"),
                ParseIntUx4N(draft.HeartbeatBudgetMilliseconds, "budget heartbeat"),
                draft.SuspendOutsideInterest,
                UpdateBindingUx4N(source.Heartbeat?.Binding,
                    StableCreatureEventIdUx4N(source.Id, "heartbeat"), "creature.heartbeat",
                    draft.HeartbeatHandler, source.Provenance));
            var rows = ParseRowsUx4N(draft.CustomEvents, 6, "événement utilisateur");
            var custom = rows.Select((row, index) =>
            {
                var qualified = $"{row[0]}.{row[1]}";
                var existing = source.CustomEvents.FirstOrDefault(item =>
                    string.Equals(item.QualifiedName, qualified, StringComparison.Ordinal));
                var fields = ParseCustomFieldsUx4N(row[4]);
                return new CreatureEventProfile.CreatureCustomEventBinding(
                    existing?.SchemaId ?? StableCreatureEventIdUx4N(source.Id, $"custom-schema:{qualified}:{index}"),
                    row[0], row[1], ParseIntUx4N(row[2], "version de payload"), fields,
                    ParseBoolUx4N(row[3], "required"),
                    UpdateBindingUx4N(existing?.Binding,
                        StableCreatureEventIdUx4N(source.Id, $"custom-binding:{qualified}:{index}"),
                        $"custom.{qualified}", row[5], source.Provenance));
            }).ToArray();
            var profile = new CreatureEventProfile(source.Id, CreatureEventProfile.CurrentSchemaVersion,
                source.Provenance, draft.RequiredAtRuntime, combat, inventory, rest, heartbeat, custom,
                source.Overrides, source.DamageDeath);
            errors.AddRange(CreatureEventValidator.Validate(profile,
                    $"blueprints/{draft.BlueprintId}/creatureEvents")
                .Select(CreatureEventDiagnosticUx4N));
            candidate = new(profile);
        }
        catch (FormatException exception)
        {
            candidate = new(CanonicalCreatureEventsUx4N(draft.BlueprintId));
            errors.Add(new("studio.creature.events.form_invalid", "creatureEvents", exception.Message));
        }
        diagnostics = errors.GroupBy(item => (item.Code, item.Path)).Select(item => item.First()).ToArray();
        return diagnostics.Count == 0;
    }

    private static CreatureEventProfile CanonicalCreatureEventsUx4N(BlueprintId blueprintId)
    {
        var profileId = StableCreatureEventIdUx4N(blueprintId.Value, "profile");
        const string provenance = "studio:ux4-n";
        return new(profileId, CreatureEventProfile.CurrentSchemaVersion, provenance, false,
            new(CreatureCombatBoundaryMode.CombatTickCompleted,
                UpdateBindingUx4N(null, StableCreatureEventIdUx4N(profileId, "combat"),
                    "creature.combat_tick_completed", "OnCombatTickCompleted", provenance)),
            new(UpdateBindingUx4N(null, StableCreatureEventIdUx4N(profileId, "inventory-accessed"),
                    "creature.inventory_accessed", "OnInventoryAccessed", provenance),
                UpdateBindingUx4N(null, StableCreatureEventIdUx4N(profileId, "item-transferred"),
                    "creature.item_transferred", "OnItemTransferred", provenance)),
            new(UpdateBindingUx4N(null, StableCreatureEventIdUx4N(profileId, "rest-completed"),
                "creature.rest_completed", "OnRestCompleted", provenance)),
            new(true, 2_000, 8, true,
                UpdateBindingUx4N(null, StableCreatureEventIdUx4N(profileId, "heartbeat"),
                    "creature.heartbeat", "OnHeartbeat", provenance)),
            [], new Dictionary<string, string>(), damageDeath: null);
    }

    private static EventBinding UpdateBindingUx4N(
        EventBinding? source, Guid fallbackId, string eventName, string handler, string provenance) =>
        source is null
            ? new(fallbackId, 1, provenance, "1.0", eventName, handler, EventAuthorityScope.Server)
            : source with { EventName = eventName, Handler = handler };

    private static IReadOnlyDictionary<string, CreatureCustomFieldType> ParseCustomFieldsUx4N(string text)
    {
        var result = new Dictionary<string, CreatureCustomFieldType>(StringComparer.Ordinal);
        foreach (var field in SplitCsvUx4N(text))
        {
            var pair = field.Split(':', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !Enum.TryParse<CreatureCustomFieldType>(pair[1], true, out var type) ||
                !Enum.IsDefined(type) || !result.TryAdd(pair[0], type))
                throw new FormatException($"Champ typé invalide : '{field}'.");
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> SampleCustomPayloadUx4N(
        IReadOnlyDictionary<string, CreatureCustomFieldType> fields) =>
        fields.ToDictionary(item => item.Key, item => item.Value switch
        {
            CreatureCustomFieldType.Integer => "3",
            CreatureCustomFieldType.Number => "1.5",
            CreatureCustomFieldType.Boolean => "true",
            CreatureCustomFieldType.EntityId => "preview-creature",
            CreatureCustomFieldType.ItemId => "preview-item",
            _ => "preview",
        }, StringComparer.Ordinal);

    private static bool ToggleCreatureEventNeighborUx4N(StudioSessionController editing, BlueprintId excluded)
    {
        var history = editing.History;
        var project = editing.Project;
        var neighbor = project?.Blueprints.FirstOrDefault(item => item.Id != excluded && !item.IsReadOnly() &&
            item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn);
        if (history is null || neighbor is null) return false;
        var profile = neighbor.CreatureEvents ?? CanonicalCreatureEventsUx4N(neighbor.Id);
        var changed = new CreatureEventProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
            !profile.RequiredAtRuntime, profile.CombatBoundary, profile.Inventory, profile.Rest, profile.Heartbeat,
            profile.CustomEvents, profile.Overrides, profile.DamageDeath);
        var result = editing._setCreatureEventsUx4N.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, neighbor.Id, changed, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        editing.InvalidateContentIndexes();
        return true;
    }

    private static ProjectDefinition WithCreatureEventsUx4N(
        ProjectDefinition project, BlueprintId blueprintId, CreatureEventProfile profile)
    {
        var source = project.Blueprints.Single(item => item.Id == blueprintId);
        var updated = new BlueprintDefinition(source.Id, source.Slug, source.DisplayName, source.Kind,
            source.Combat, source.PaletteOrigin, source.IsFavorite, source.Provenance,
            source.CreaturePresentation, source.CreatureProgression, source.CreatureLoot,
            source.CreatureRoleBehavior, profile, source.ItemAdvanced, source.Authoring,
            source.CreatureFoundation, source.ItemFoundation);
        return new(project.Id, project.Title, project.Module,
            project.Blueprints.Select(item => item.Id == blueprintId ? updated : item).ToArray(),
            project.Areas, project.Dialogues, project.Quests);
    }

    private static StudioCreatureEventPreviewUx4N FailedCreatureEventsUx4N(
        string message, string code, string? detail = null) =>
        new(false, false, message, string.Empty, string.Empty,
            [new(code, "creatureEvents", detail ?? message)], "Cycle non évalué.",
            "Heartbeat non évalué.", "Événements non évalués.", "Runtime non évalué.");

    private static StudioCreatureEventDiagnosticUx4N CreatureEventDiagnosticUx4N(
        CreatureEventDiagnostic diagnostic) => new(diagnostic.Code, diagnostic.Path, HumanCreatureEventUx4N(diagnostic.Code));

    private static IReadOnlyList<StudioCreatureEventDiagnosticUx4N> CreatureEventDiagnosticsUx4N(
        IReadOnlyList<CommandDiagnostic> diagnostics) => diagnostics.Select(item =>
        {
            var separator = item.Message.IndexOf(':');
            var path = separator > 0 ? item.Message[..separator] : "creatureEvents";
            return new StudioCreatureEventDiagnosticUx4N(item.Code, path, HumanCreatureEventUx4N(item.Code));
        }).ToArray();

    private static string HumanCreatureEventUx4N(string code) => code switch
    {
        "creature.events.binding_invalid" => "Un handler doit être un identifiant ForgeScript valide, API 1.0 et autorité serveur.",
        "creature.events.heartbeat_interval_invalid" => "La cadence heartbeat doit rester entre 250 ms et 24 heures.",
        "creature.events.heartbeat_budget_invalid" => "Le budget heartbeat doit rester positif, borné et inférieur à la cadence.",
        "creature.events.custom_duplicate" => "Chaque événement utilisateur doit avoir un nom qualifié unique.",
        "creature.events.custom_schema_invalid" => "Namespace, nom et version de payload doivent être canoniques.",
        "creature.events.custom_fields_invalid" => "Les champs doivent avoir des noms et types pris en charge.",
        _ => code,
    };

    private static string LifecycleSummaryUx4N(CreatureEventProfile profile) =>
        $"Combat : {(profile.CombatBoundary is null ? "désactivé" : HumanCombatUx4N(profile.CombatBoundary.Mode))} · " +
        $"inventaire : {(profile.Inventory is null ? "désactivé" : "2 handlers serveur")} · " +
        $"repos : {(profile.Rest is null ? "désactivé" : "handler serveur")}.";

    private static string HeartbeatSummaryUx4N(CreatureEventProfile profile) => profile.Heartbeat is null
        ? "Heartbeat absent."
        : $"Heartbeat {(profile.Heartbeat.Enabled ? "actif" : "inactif")} · " +
          $"{profile.Heartbeat.IntervalMilliseconds} ms · budget {profile.Heartbeat.BudgetMilliseconds} ms · " +
          $"suspension hors intérêt {(profile.Heartbeat.SuspendOutsideInterest ? "active" : "inactive")}.";

    private static string CustomSummaryUx4N(CreatureEventProfile profile) =>
        profile.CustomEvents.Count == 0 ? "Aucun événement utilisateur." :
        $"{profile.CustomEvents.Count} événement(s) typé(s) : " +
        string.Join(", ", profile.CustomEvents.Select(item => $"{item.QualifiedName}@{item.PayloadVersion}"));

    private static string HumanCombatUx4N(CreatureCombatBoundaryMode mode) => mode switch
    {
        CreatureCombatBoundaryMode.CombatTickCompleted => "fin de tick de combat",
        CreatureCombatBoundaryMode.TurnEnded => "fin de tour",
        _ => mode.ToString(),
    };

    private static IReadOnlyList<string[]> ParseRowsUx4N(string text, int columns, string label)
    {
        var result = new List<string[]>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries |
                                              StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != columns)
                throw new FormatException($"Ligne {label} invalide : {columns} colonnes attendues.");
            result.Add(parts);
        }
        return result;
    }

    private static string[] SplitCsvUx4N(string value) => value.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int ParseIntUx4N(string value, string label) => int.TryParse(value,
        NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        ? parsed : throw new FormatException($"{label} invalide : '{value}'.");

    private static bool ParseBoolUx4N(string value, string label) => bool.TryParse(value, out var parsed)
        ? parsed : throw new FormatException($"{label} invalide : '{value}'.");

    private static Guid StableCreatureEventIdUx4N(Guid seed, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed:D}|ux4-n|{key}"));
        return new Guid(bytes[..16]);
    }

    private static string FingerprintProjectUx4N(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string FingerprintCreatureEventCandidateUx4N(
        ProjectDefinition project, BlueprintId blueprintId, StudioCreatureEventCandidateUx4N candidate) =>
        FingerprintProjectUx4N(WithCreatureEventsUx4N(project, blueprintId, candidate.Profile));

    private static string FingerprintCreatureEventDraftUx4N(StudioCreatureEventDraftUx4N draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteUx4N(writer, draft.BlueprintId.ToString());
        writer.Write(draft.RequiredAtRuntime);
        writer.Write(draft.CombatEnabled); writer.Write((int)draft.CombatMode); WriteUx4N(writer, draft.CombatHandler);
        writer.Write(draft.InventoryEnabled); WriteUx4N(writer, draft.InventoryAccessedHandler);
        WriteUx4N(writer, draft.ItemTransferredHandler);
        writer.Write(draft.RestEnabled); WriteUx4N(writer, draft.RestHandler);
        writer.Write(draft.HeartbeatEnabled); WriteUx4N(writer, draft.HeartbeatIntervalMilliseconds);
        WriteUx4N(writer, draft.HeartbeatBudgetMilliseconds); writer.Write(draft.SuspendOutsideInterest);
        WriteUx4N(writer, draft.HeartbeatHandler); WriteUx4N(writer, draft.CustomEvents);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx4N(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool DeleteUx4NSmokeRoot(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4n-smoke-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }
}
