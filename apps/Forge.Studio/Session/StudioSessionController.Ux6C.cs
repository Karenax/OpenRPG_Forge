using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6CSelectionCode = "studio.gameplay.capabilities.selection_required";
    private const string Ux6CSourceCode = "studio.gameplay.capabilities.source_changed";
    private const string Ux6CProfileCode = "studio.gameplay.capabilities.invalid";
    private const string Ux6CProvenance = "studio:ux6-c:gameplay-capabilities";

    private readonly SetGameplayControlProfileHandler _setGameplayUx6C = new(new SystemClock());
    private StudioUx6CDraft? _approvedGameplayUx6C;
    private ProjectRevision? _approvedGameplayRevisionUx6C;
    private string? _approvedGameplayFingerprintUx6C;

    internal IReadOnlyList<StudioUx6CProfileChoice> GameplayCapabilityChoicesUx6C
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            return project.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(area => new StudioUx6CProfileChoice(
                    $"{HumanUx6C(project.Module.DisplayName)} · contexte {HumanUx6C(area.DisplayName)}", area.Id))
                .ToArray();
        }
    }

    internal StudioUx6CDraft? CreateGameplayCapabilityDraftUx6C(
        AreaId? contextAreaId,
        bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null || contextAreaId is null ||
            project.Areas.All(item => item.Id != contextAreaId)) return null;

        var source = project.Module.RuntimePolicies.Gameplay;
        return new StudioUx6CDraft(
            history.State.Revision,
            SourceFingerprintUx6C(project),
            contextAreaId,
            source,
            new StudioUx6CSwimmingDraft(source.Swimming.Enabled, source.Swimming.Required,
                source.Swimming.Buoyancy, source.Swimming.SurfaceSnapDistance,
                source.Swimming.SurfaceSpeed, source.Swimming.DiveSpeed,
                source.Swimming.CurrentInfluence, source.Swimming.BreathSeconds,
                source.Swimming.WaterVolumeTags.ToArray(), source.Swimming.SurfaceAnimation,
                source.Swimming.DiveAnimation, source.Swimming.IdleAnimation),
            new StudioUx6CClimbingDraft(source.Climbing.Enabled, source.Climbing.Required,
                source.Climbing.ClimbSpeed, source.Climbing.StaminaCapacity,
                source.Climbing.StaminaDrainPerSecond, source.Climbing.LedgeReach,
                source.Climbing.InverseKinematics, source.Climbing.RequireAuthoredLinks,
                source.Climbing.PreventUnsafeDrop, source.Climbing.SurfaceTags.ToArray(),
                source.Climbing.ClimbAnimation, source.Climbing.LedgeAnimation),
            new StudioUx6CRadialMenuDraft(source.RadialMenu.Enabled, source.RadialMenu.InputAction,
                source.RadialMenu.DeadZone, source.RadialMenu.MaximumDepth,
                source.RadialMenu.GamepadEnabled, source.RadialMenu.KeyboardNavigation,
                source.RadialMenu.ListFallback, source.RadialMenu.ScreenReaderLabels,
                source.RadialMenu.Actions.Select(item => new StudioUx6CRadialActionDraft(item.Id,
                    item.Key, item.Label, item.AccessibleLabel, item.Kind, item.Command,
                    item.ParentKey, item.Order, item.RequiresTarget, item.Tags.ToArray())).ToArray()),
            new StudioUx6CTimeControlDraft(source.TimeControl.PauseEnabled,
                source.TimeControl.SlowMotionEnabled, source.TimeControl.SlowMotionScale,
                source.TimeControl.PlanningEnabled, source.TimeControl.MaximumPlannedActions,
                source.TimeControl.TurnModeEnabled, source.TimeControl.MultiplayerPolicy,
                source.TimeControl.RequiredVoteFraction, source.TimeControl.PausePermissions.ToArray()),
            new StudioUx6CCombatDraft(source.CombatModes.DefaultProviderId,
                source.CombatModes.Providers.Select(item => new StudioUx6CCombatProviderDraft(item.Id,
                    item.ProviderId, item.DisplayName, item.Kind, item.CustomProvider,
                    item.EventContracts.ToArray(), item.ServerAuthoritative)).ToArray()));
    }

    internal StudioUx6CPreview PreviewGameplayCapabilitiesUx6C(StudioUx6CDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx6CApproval();
        if (!TryGetCurrentUx6C(draft, out var history, out var project, out var failure)) return failure!;
        if (!TryBuildGameplayCandidateUx6C(draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6C(diagnostic?.Message ?? "Le profil gameplay est invalide.", diagnostic!);

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history!.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6C.Handle(history.State, command);
        if (!dryRun.Succeeded) return FailedCommandUx6C(dryRun);

        var source = project!.Module.RuntimePolicies.Gameplay;
        var diff = BuildDiffUx6C(source, candidate!);
        var runtime = BuildRuntimePreviewUx6C(candidate!);
        if (diff.Count > 0)
        {
            _approvedGameplayUx6C = draft;
            _approvedGameplayRevisionUx6C = history.State.Revision;
            _approvedGameplayFingerprintUx6C = CandidateFingerprintUx6C(project, draft);
        }
        return new(true, diff.Count > 0,
            diff.Count > 0
                ? "Aperçu sec des capacités gameplay validé · aucune mutation appliquée."
                : "Aperçu sec validé · le profil est déjà identique.",
            "Remplacement canonique des cinq profils; fondation UX6-B, sauvegarde et autres queues strictement conservées.",
            diff, [], runtime, true, true);
    }

    internal StudioUx6CActionResult ApplyApprovedGameplayCapabilitiesUx6C()
    {
        var draft = _approvedGameplayUx6C;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedGameplayRevisionUx6C != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6C(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedGameplayFingerprintUx6C, CandidateFingerprintUx6C(project, draft),
                StringComparison.Ordinal) ||
            !TryBuildGameplayCandidateUx6C(draft, out var candidate, out _))
        {
            CancelUx6CApproval();
            return new(false, "Relancez l’aperçu sec après avoir rechargé les capacités gameplay.");
        }

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6C.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelUx6CApproval();
            return new(false, HumanCommandFailureUx6C(dryRun));
        }
        var applied = _setGameplayUx6C.Handle(history.State, command with { DryRun = false });
        CancelUx6CApproval();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6C(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Capacités gameplay appliquées · nage, escalade, radial, temps et combat sont enregistrés.");
    }

    internal StudioUx6CApprovalSnapshot CaptureGameplayCapabilityApprovalUx6C() =>
        new(_approvedGameplayUx6C, _approvedGameplayRevisionUx6C, _approvedGameplayFingerprintUx6C);

    internal void RestoreGameplayCapabilityApprovalUx6C(StudioUx6CApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedGameplayUx6C = snapshot.Draft;
        _approvedGameplayRevisionUx6C = snapshot.Revision;
        _approvedGameplayFingerprintUx6C = snapshot.Fingerprint;
    }

    internal void CancelUx6CApproval()
    {
        _approvedGameplayUx6C = null;
        _approvedGameplayRevisionUx6C = null;
        _approvedGameplayFingerprintUx6C = null;
    }

    internal string GameplayCapabilityDraftFingerprintUx6C(StudioUx6CDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Project is null ? string.Empty : CandidateFingerprintUx6C(Project, draft);
    }

    private bool TryGetCurrentUx6C(StudioUx6CDraft draft, out ProjectCommandHistory? history,
        out ProjectDefinition? project, out StudioUx6CPreview? failure)
    {
        history = History;
        project = Project;
        failure = null;
        if (history is null || project is null || project.Areas.All(item => item.Id != draft.ContextAreaId))
        {
            failure = FailedPreviewUx6C("Ouvrez un module et choisissez explicitement un contexte de zone.",
                new(Ux6CSelectionCode, "module/runtimePolicies/gameplay",
                    "Sélection explicite requise."));
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6C(project), StringComparison.Ordinal))
        {
            failure = FailedPreviewUx6C("Le projet a changé. Rechargez les capacités avant l’aperçu.",
                new(Ux6CSourceCode, "module/runtimePolicies/gameplay",
                    "La révision ou la source ne correspond plus."));
            return false;
        }
        return true;
    }

    private static bool TryBuildGameplayCandidateUx6C(StudioUx6CDraft draft,
        out ModuleGameplayControlProfile? candidate, out StudioUx6CDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            EnsureFiniteAndEnumsUx6C(draft);
            var source = draft.SourceGameplay;
            var swimming = new SwimmingProfile(source.Swimming.Id, source.Swimming.SchemaVersion,
                draft.Swimming.Enabled, draft.Swimming.Required, draft.Swimming.Buoyancy,
                draft.Swimming.SurfaceSnapDistance, draft.Swimming.SurfaceSpeed,
                draft.Swimming.DiveSpeed, draft.Swimming.CurrentInfluence,
                draft.Swimming.BreathSeconds, NormalizeLinesUx6C(draft.Swimming.WaterVolumeTags),
                draft.Swimming.SurfaceAnimation.Trim(), draft.Swimming.DiveAnimation.Trim(),
                draft.Swimming.IdleAnimation.Trim(), Ux6CProvenance);
            var climbing = new ClimbingProfile(source.Climbing.Id, source.Climbing.SchemaVersion,
                draft.Climbing.Enabled, draft.Climbing.Required, draft.Climbing.ClimbSpeed,
                draft.Climbing.StaminaCapacity, draft.Climbing.StaminaDrainPerSecond,
                draft.Climbing.LedgeReach, draft.Climbing.InverseKinematics,
                draft.Climbing.RequireAuthoredLinks, draft.Climbing.PreventUnsafeDrop,
                NormalizeLinesUx6C(draft.Climbing.SurfaceTags), draft.Climbing.ClimbAnimation.Trim(),
                draft.Climbing.LedgeAnimation.Trim(), Ux6CProvenance);
            var radial = new RadialMenuProfile(source.RadialMenu.Id, source.RadialMenu.SchemaVersion,
                draft.RadialMenu.Enabled, draft.RadialMenu.InputAction.Trim(), draft.RadialMenu.DeadZone,
                draft.RadialMenu.MaximumDepth, draft.RadialMenu.GamepadEnabled,
                draft.RadialMenu.KeyboardNavigation, draft.RadialMenu.ListFallback,
                draft.RadialMenu.ScreenReaderLabels,
                draft.RadialMenu.Actions.Select(item => new RadialActionDefinition(item.Id,
                    item.Key.Trim(), item.Label.Trim(), item.AccessibleLabel.Trim(), item.Kind,
                    item.Command.Trim(), item.ParentKey.Trim(), item.Order, item.RequiresTarget,
                    NormalizeLinesUx6C(item.Tags))).ToArray(), Ux6CProvenance);
            var time = new TimeControlPolicy(source.TimeControl.Id, source.TimeControl.SchemaVersion,
                draft.TimeControl.PauseEnabled, draft.TimeControl.SlowMotionEnabled,
                draft.TimeControl.SlowMotionScale, draft.TimeControl.PlanningEnabled,
                draft.TimeControl.MaximumPlannedActions, draft.TimeControl.TurnModeEnabled,
                draft.TimeControl.MultiplayerPolicy, draft.TimeControl.RequiredVoteFraction,
                NormalizeLinesUx6C(draft.TimeControl.PausePermissions), Ux6CProvenance);
            var combat = new CombatModeProfile(source.CombatModes.Id, source.CombatModes.SchemaVersion,
                draft.CombatModes.DefaultProviderId.Trim(),
                draft.CombatModes.Providers.Select(item => new CombatModeProviderDefinition(item.Id,
                    item.ProviderId.Trim(), item.DisplayName.Trim(), item.Kind,
                    item.CustomProvider.Trim(), NormalizeLinesUx6C(item.EventContracts),
                    item.ServerAuthoritative)).ToArray(), Ux6CProvenance);
            candidate = new ModuleGameplayControlProfile(source.Id, source.SchemaVersion,
                swimming, climbing, radial, time, combat, Ux6CProvenance,
                source.Foundation, source.Simulation, source.Session);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                              InvalidOperationException)
        {
            diagnostic = new(Ux6CProfileCode, "module/runtimePolicies/gameplay",
                $"Le contrat canonique refuse le profil : {exception.Message}");
            return false;
        }
    }

    private static void EnsureFiniteAndEnumsUx6C(StudioUx6CDraft draft)
    {
        var values = new[]
        {
            draft.Swimming.Buoyancy, draft.Swimming.SurfaceSnapDistance,
            draft.Swimming.SurfaceSpeed, draft.Swimming.DiveSpeed,
            draft.Swimming.CurrentInfluence, draft.Swimming.BreathSeconds,
            draft.Climbing.ClimbSpeed, draft.Climbing.StaminaCapacity,
            draft.Climbing.StaminaDrainPerSecond, draft.Climbing.LedgeReach,
            draft.RadialMenu.DeadZone, draft.TimeControl.SlowMotionScale,
            draft.TimeControl.RequiredVoteFraction,
        };
        if (values.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Les valeurs numériques doivent être finies.");
        if (!Enum.IsDefined(draft.TimeControl.MultiplayerPolicy) ||
            draft.RadialMenu.Actions.Any(item => !Enum.IsDefined(item.Kind)) ||
            draft.CombatModes.Providers.Any(item => !Enum.IsDefined(item.Kind)))
            throw new ArgumentException("Une option sélectionnée n’appartient pas au contrat gameplay.");
    }

    private static IReadOnlyList<string> BuildDiffUx6C(
        ModuleGameplayControlProfile source,
        ModuleGameplayControlProfile candidate)
    {
        var entries = new List<string>();
        if (source.Swimming != candidate.Swimming)
            entries.Add($"Nage · surface {source.Swimming.SurfaceSpeed:0.###} → {candidate.Swimming.SurfaceSpeed:0.###} m/s · plongée {source.Swimming.DiveSpeed:0.###} → {candidate.Swimming.DiveSpeed:0.###} m/s.");
        if (source.Climbing != candidate.Climbing)
            entries.Add($"Escalade · vitesse {source.Climbing.ClimbSpeed:0.###} → {candidate.Climbing.ClimbSpeed:0.###} m/s · endurance {candidate.Climbing.StaminaCapacity:0.###}.");
        if (!RadialEqualsUx6C(source.RadialMenu, candidate.RadialMenu))
            entries.Add($"Menu radial · {candidate.RadialMenu.Actions.Count} action(s) · profondeur {candidate.RadialMenu.MaximumDepth} · navigation clavier {(candidate.RadialMenu.KeyboardNavigation ? "active" : "désactivée")}.");
        if (source.TimeControl != candidate.TimeControl)
            entries.Add($"Temps tactique · ralenti {candidate.TimeControl.SlowMotionScale:0.###} · planification {candidate.TimeControl.MaximumPlannedActions} action(s) · politique {HumanPausePolicyUx6C(candidate.TimeControl.MultiplayerPolicy)}.");
        if (!CombatEqualsUx6C(source.CombatModes, candidate.CombatModes))
            entries.Add($"Modes de combat · {candidate.CombatModes.Providers.Count} fournisseur(s) · défaut {HumanUx6C(candidate.CombatModes.DefaultProvider.DisplayName)}.");
        return entries;
    }

    private static bool RadialEqualsUx6C(RadialMenuProfile left, RadialMenuProfile right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion && left.Enabled == right.Enabled &&
        left.InputAction == right.InputAction && left.DeadZone == right.DeadZone &&
        left.MaximumDepth == right.MaximumDepth && left.GamepadEnabled == right.GamepadEnabled &&
        left.KeyboardNavigation == right.KeyboardNavigation && left.ListFallback == right.ListFallback &&
        left.ScreenReaderLabels == right.ScreenReaderLabels && left.Actions.SequenceEqual(right.Actions) &&
        left.Provenance == right.Provenance;

    private static bool CombatEqualsUx6C(CombatModeProfile left, CombatModeProfile right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        left.DefaultProviderId == right.DefaultProviderId && left.Providers.SequenceEqual(right.Providers) &&
        left.Provenance == right.Provenance;

    private static IReadOnlyList<string> BuildRuntimePreviewUx6C(ModuleGameplayControlProfile profile)
    {
        var swim = MovementCapabilityRuntime.Resolve(profile,
            new(true, true, false, false, profile.Swimming.BreathSeconds, profile.Climbing.StaminaCapacity), .5f);
        var climb = MovementCapabilityRuntime.Resolve(profile,
            new(false, false, true, true, profile.Swimming.BreathSeconds, profile.Climbing.StaminaCapacity), .5f);
        var root = RadialActionResolver.Open(profile.RadialMenu);
        var childKey = root.Actions.FirstOrDefault(item => item.Kind == RadialActionKind.Navigation)?.Key;
        var child = string.IsNullOrWhiteSpace(childKey) ? null : RadialActionResolver.Open(profile.RadialMenu, childKey);
        var requested = profile.TimeControl.PlanningEnabled ? RuntimeTimeState.Planning : RuntimeTimeState.Running;
        var time = TimeControlCoordinator.Request(profile.TimeControl, TimeControlSession.Running,
            requested, "joueur-local", false, true);
        var provider = profile.CombatModes.DefaultProvider;
        return
        [
            $"Nage résolue · {HumanMovementUx6C(swim.Kind)} · vitesse {swim.Speed:0.###} m/s · souffle {swim.BreathRemaining:0.###} s.",
            $"Escalade résolue · {(climb.Accepted ? "acceptée" : "refusée")} · vitesse {climb.Speed:0.###} m/s · endurance {climb.StaminaRemaining:0.###}.",
            child is null
                ? $"Menu radial résolu · {root.Actions.Count} action(s) racine · {root.AccessibleSummary}."
                : $"Menu radial résolu · {root.Actions.Count} action(s) racine · sous-menu {HumanUx6C(child.ParentKey)} avec {child.Actions.Count} action(s).",
            $"Temps tactique résolu · {(time.Accepted ? "accepté" : "refusé")} · état {HumanTimeStateUx6C(time.Session.State)} · échelle {time.Session.Scale:0.###}.",
            $"Combat résolu · {HumanUx6C(provider.DisplayName)} · {HumanCombatModeUx6C(provider.Kind)} · {provider.EventContracts.Count} contrat(s) d’événement · autorité {(provider.ServerAuthoritative ? "serveur" : "locale")}.",
            "Aucun acteur, volume d’eau, lien d’escalade, roue native, horloge moteur, combat, réseau ou ForgeScript n’est exécuté dans Studio.",
        ];
    }

    private static StudioUx6CPreview FailedPreviewUx6C(string message, StudioUx6CDiagnostic diagnostic) =>
        new(false, false, message, string.Empty, [], [diagnostic], [], false, true);

    private static StudioUx6CPreview FailedCommandUx6C(CommandResult result)
    {
        var diagnostics = result.Diagnostics.Select(item => new StudioUx6CDiagnostic(item.Code,
            "module/runtimePolicies/gameplay", HumanDiagnosticUx6C(item.Code, item.Message))).ToArray();
        return new(false, false,
            diagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé le profil.",
            string.Empty, [], diagnostics, [], false, true);
    }

    private static string HumanCommandFailureUx6C(CommandResult result) =>
        HumanDiagnosticUx6C(result.Diagnostics.FirstOrDefault()?.Code,
            result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");

    private static string HumanDiagnosticUx6C(string? code, string message) => code switch
    {
        "command.revision_conflict" => "Le projet a changé; rechargez les capacités gameplay.",
        Forge.Domain.Validation.RuntimePolicyValidator.CombatEventContractCode =>
            "Tous les modes de combat doivent exposer exactement les mêmes contrats d’événement.",
        Forge.Domain.Validation.RuntimePolicyValidator.GameplayIdentityCode =>
            "L’identité du profil gameplay doit rester liée au module.",
        _ => string.IsNullOrWhiteSpace(message) ? "La validation canonique a refusé le profil." : message,
    };

    private static ProjectDefinition BuildGameplayProjectUx6C(
        ProjectDefinition source,
        ModuleGameplayControlProfile candidate)
    {
        var previous = source.Module.RuntimePolicies;
        var policies = new Forge.Domain.RuntimePolicies.ModuleRuntimePolicyProfile(previous.Id,
            previous.SchemaVersion, previous.Heartbeats, previous.Rest, previous.SequenceAbort,
            "gameplay-command", previous.Cinematics, candidate, previous.Rules,
            previous.Extensions, previous.GameplayEvents, previous.World, previous.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(policies),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    private static string CandidateFingerprintUx6C(ProjectDefinition project, StudioUx6CDraft draft)
    {
        if (!TryBuildGameplayCandidateUx6C(draft, out var candidate, out _) || candidate is null)
            return string.Empty;
        return HashUx6C(ProjectJsonSerializer.Serialize(BuildGameplayProjectUx6C(project, candidate)));
    }

    private static string SourceFingerprintUx6C(ProjectDefinition project) =>
        HashUx6C(ProjectJsonSerializer.Serialize(project));

    private static string HashUx6C(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HumanUx6C(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identifiant protégé";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string HumanMovementUx6C(MovementCapabilityKind kind) => kind switch
    {
        MovementCapabilityKind.Ground => "au sol",
        MovementCapabilityKind.Swimming => "nage en surface",
        MovementCapabilityKind.Diving => "plongée",
        MovementCapabilityKind.Climbing => "escalade",
        _ => "mode inconnu",
    };

    private static string HumanTimeStateUx6C(RuntimeTimeState state) => state switch
    {
        RuntimeTimeState.Running => "temps réel",
        RuntimeTimeState.SlowMotion => "ralenti",
        RuntimeTimeState.Paused => "pause",
        RuntimeTimeState.Planning => "planification",
        RuntimeTimeState.TurnBased => "tour par tour",
        _ => "état inconnu",
    };

    private static string HumanPausePolicyUx6C(MultiplayerPausePolicy policy) => policy switch
    {
        MultiplayerPausePolicy.Disabled => "désactivée",
        MultiplayerPausePolicy.HostOnly => "hôte seulement",
        MultiplayerPausePolicy.Vote => "vote",
        MultiplayerPausePolicy.Everyone => "tout le monde",
        _ => "politique inconnue",
    };

    private static string HumanCombatModeUx6C(CombatModeKind kind) => kind switch
    {
        CombatModeKind.RealTime => "temps réel",
        CombatModeKind.RealTimeWithPause => "temps réel avec pause",
        CombatModeKind.TurnBased => "tour par tour",
        CombatModeKind.Custom => "personnalisé",
        _ => "mode inconnu",
    };

    private static string[] NormalizeLinesUx6C(IEnumerable<string> values) => values
        .Select(value => (value ?? string.Empty).Trim()).Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal).ToArray();

    internal StudioUx6CSmokeResult RunUx6CContractSmoke()
    {
        var real = Project;
        if (real is null || real.Areas.Count == 0) return FailedUx6CSmoke();
        var realGameplay = real.Module.RuntimePolicies.Gameplay;
        var realData = realGameplay.Swimming.WaterVolumeTags.Count > 0 &&
                       realGameplay.Climbing.SurfaceTags.Count > 0 &&
                       realGameplay.RadialMenu.Actions.Count >= 4 &&
                       realGameplay.CombatModes.Providers.Count >= 3;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-c-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(root, real);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedUx6CSmoke();
            var areaId = editing.Project!.Areas[0].Id;
            var explicitSelection = editing.CreateGameplayCapabilityDraftUx6C(areaId) is null &&
                                    editing.CreateGameplayCapabilityDraftUx6C(areaId, true) is not null;
            var baseline = editing.CreateGameplayCapabilityDraftUx6C(areaId, true)!;
            var swimmingValidation = !editing.PreviewGameplayCapabilitiesUx6C(baseline with
            {
                Swimming = baseline.Swimming with { SurfaceSpeed = float.NaN },
            }).Succeeded;
            var climbingValidation = !editing.PreviewGameplayCapabilitiesUx6C(baseline with
            {
                Climbing = baseline.Climbing with { RequireAuthoredLinks = true, SurfaceTags = [] },
            }).Succeeded;
            var radialValidation = !editing.PreviewGameplayCapabilitiesUx6C(baseline with
            {
                RadialMenu = baseline.RadialMenu with
                {
                    Actions = baseline.RadialMenu.Actions.Select((item, index) => index == 0
                        ? item with { ParentKey = "parent-absent" } : item).ToArray(),
                },
            }).Succeeded;
            var timeValidation = !editing.PreviewGameplayCapabilitiesUx6C(baseline with
            {
                TimeControl = baseline.TimeControl with
                {
                    MultiplayerPolicy = (MultiplayerPausePolicy)999,
                },
            }).Succeeded;
            var combatValidation = !editing.PreviewGameplayCapabilitiesUx6C(baseline with
            {
                CombatModes = baseline.CombatModes with
                {
                    Providers = baseline.CombatModes.Providers.Select((item, index) => index == 0
                        ? item with { EventContracts = ["combat.incomplet"] } : item).ToArray(),
                },
            }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedDraftUx6C(baseline);
            var preview = editing.PreviewGameplayCapabilitiesUx6C(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;
            var runtimePreview = preview.RuntimePreview.Count == 6;

            var externalDraft = ChangedDraftUx6C(baseline) with
            {
                Swimming = changed.Swimming with
                {
                    DiveSpeed = changed.Swimming.DiveSpeed + .1f,
                },
            };
            if (!TryBuildGameplayCandidateUx6C(externalDraft, out var externalCandidate, out _) ||
                externalCandidate is null) return FailedUx6CSmoke();
            var external = editing._setGameplayUx6C.Handle(history.State,
                new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    externalCandidate, DryRun: false));
            if (!external.Succeeded) return FailedUx6CSmoke();
            history.Record(external);
            var staleSource = editing.ApplyApprovedGameplayCapabilitiesUx6C();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamperDraft = ChangedDraftUx6C(editing.CreateGameplayCapabilityDraftUx6C(areaId, true)!);
            var tamperPreview = editing.PreviewGameplayCapabilitiesUx6C(tamperDraft);
            var approval = editing.CaptureGameplayCapabilityApprovalUx6C();
            editing.RestoreGameplayCapabilityApprovalUx6C(approval with
            {
                Draft = approval.Draft! with
                {
                    TimeControl = approval.Draft.TimeControl with
                    {
                        MaximumPlannedActions = Math.Min(64,
                            approval.Draft.TimeControl.MaximumPlannedActions + 1),
                    },
                },
            });
            var staleApproval = editing.ApplyApprovedGameplayCapabilitiesUx6C();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceGameplay = sourceProject.Module.RuntimePolicies.Gameplay;
            var fresh = ChangedDraftUx6C(editing.CreateGameplayCapabilityDraftUx6C(areaId, true)!);
            if (!TryBuildGameplayCandidateUx6C(fresh, out var expectedGameplay, out _) ||
                expectedGameplay is null) return FailedUx6CSmoke();
            var freshPreview = editing.PreviewGameplayCapabilitiesUx6C(fresh);
            var applied = editing.ApplyApprovedGameplayCapabilitiesUx6C();
            var finalProject = editing.Project!;
            var finalGameplay = finalProject.Module.RuntimePolicies.Gameplay;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_gameplay_control_profile";
            var foundationPreserved = ReferenceEquals(sourceGameplay.Foundation, finalGameplay.Foundation);
            var gameplayTails = ReferenceEquals(sourceGameplay.Simulation, finalGameplay.Simulation) &&
                                ReferenceEquals(sourceGameplay.Session, finalGameplay.Session) &&
                                sourceGameplay.Swimming.Id == finalGameplay.Swimming.Id &&
                                sourceGameplay.Climbing.Id == finalGameplay.Climbing.Id &&
                                sourceGameplay.RadialMenu.Id == finalGameplay.RadialMenu.Id &&
                                sourceGameplay.TimeControl.Id == finalGameplay.TimeControl.Id &&
                                sourceGameplay.CombatModes.Id == finalGameplay.CombatModes.Id &&
                                sourceGameplay.RadialMenu.Actions.Select(item => item.Id)
                                    .SequenceEqual(finalGameplay.RadialMenu.Actions.Select(item => item.Id)) &&
                                sourceGameplay.CombatModes.Providers.Select(item => item.Id)
                                    .SequenceEqual(finalGameplay.CombatModes.Providers.Select(item => item.Id));
            var expectedProject = BuildGameplayProjectUx6C(sourceProject, expectedGameplay);
            var projectNeighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);

            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var undoRedo = undone && redone;
            var saved = editing.Save();
            var opened = reopened.Open(root);
            var persistence = saved.Succeeded && opened.Succeeded && reopened.Project is not null &&
                              string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            var humanText = !ContainsUuidUx6C(string.Join(' ',
                editing.GameplayCapabilityChoicesUx6C.Select(item => item.Label)) + " " +
                string.Join(' ', preview.Diff) + " " + string.Join(' ', preview.RuntimePreview));
            var bounded = preview.RuntimeClaimsBounded && preview.RuntimePreview.Any(item =>
                item.Contains("Aucun acteur", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6CTemporaryWorkspace(root);
            var succeeded = explicitSelection && realData && swimmingValidation && climbingValidation &&
                            radialValidation && timeValidation && combatValidation && canonicalDryRun &&
                            runtimePreview && staleSourceBlocked && staleApprovalBlocked && canonicalCommand &&
                            undoRedo && persistence && foundationPreserved && gameplayTails && projectNeighbors &&
                            humanText && bounded && cleanup;
            return new(succeeded, explicitSelection, realData, swimmingValidation, climbingValidation,
                radialValidation, timeValidation, combatValidation, canonicalDryRun, runtimePreview,
                staleSourceBlocked, staleApprovalBlocked, canonicalCommand, undoRedo, persistence,
                foundationPreserved, gameplayTails, projectNeighbors, humanText, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_C_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx6CSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6CTemporaryWorkspace(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_C_CLEANUP_FAILED");
        }
    }

    internal static StudioUx6CDraft ChangedDraftUx6C(StudioUx6CDraft source)
    {
        var actions = source.RadialMenu.Actions.Select((item, index) => index == 0
            ? item with
            {
                Label = item.Label.EndsWith(" · Studio", StringComparison.Ordinal)
                    ? item.Label.Replace(" · Studio", string.Empty, StringComparison.Ordinal)
                    : item.Label + " · Studio",
                AccessibleLabel = item.AccessibleLabel.EndsWith(" · Studio", StringComparison.Ordinal)
                    ? item.AccessibleLabel.Replace(" · Studio", string.Empty, StringComparison.Ordinal)
                    : item.AccessibleLabel + " · Studio",
            }
            : item).ToArray();
        var providers = source.CombatModes.Providers.Select((item, index) => index == 0
            ? item with
            {
                DisplayName = item.DisplayName.EndsWith(" · Studio", StringComparison.Ordinal)
                    ? item.DisplayName.Replace(" · Studio", string.Empty, StringComparison.Ordinal)
                    : item.DisplayName + " · Studio",
            }
            : item).ToArray();
        return source with
        {
            Swimming = source.Swimming with
            {
                SurfaceSpeed = source.Swimming.SurfaceSpeed >= 199 ?
                    source.Swimming.SurfaceSpeed - .125f : source.Swimming.SurfaceSpeed + .125f,
            },
            Climbing = source.Climbing with
            {
                ClimbSpeed = source.Climbing.ClimbSpeed >= 199 ?
                    source.Climbing.ClimbSpeed - .125f : source.Climbing.ClimbSpeed + .125f,
            },
            RadialMenu = source.RadialMenu with { Actions = actions },
            TimeControl = source.TimeControl with
            {
                MaximumPlannedActions = source.TimeControl.MaximumPlannedActions >= 64 ?
                    source.TimeControl.MaximumPlannedActions - 1 :
                    source.TimeControl.MaximumPlannedActions + 1,
            },
            CombatModes = source.CombatModes with { Providers = providers },
        };
    }

    private static bool ContainsUuidUx6C(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    private static StudioUx6CSmokeResult FailedUx6CSmoke() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false);

    internal static bool DeleteUx6CTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux6-c-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private void CloseUx6C() => CancelUx6CApproval();
}
