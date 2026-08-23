using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6ESelectionCode = "studio.gameplay.production.selection_required";
    private const string Ux6ESourceCode = "studio.gameplay.production.source_changed";
    private const string Ux6EProfileCode = "studio.gameplay.production.invalid";
    private const string Ux6EProvenance = "studio:ux6-e:gameplay-production";

    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx6E = new(new SystemClock());
    private StudioUx6EDraft? _approvedGameplayProductionUx6E;
    private ProjectRevision? _approvedGameplayProductionRevisionUx6E;
    private string? _approvedGameplayProductionFingerprintUx6E;

    internal IReadOnlyList<StudioUx6EProfileChoice> GameplayProductionChoicesUx6E
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var profile = project.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay;
            var achievements = profile.Achievements.Count == 1
                ? "1 succès authoré"
                : $"{profile.Achievements.Count} succès authorés";
            return [new($"{HumanUx6E(project.Module.DisplayName)} · {achievements}")];
        }
    }

    internal StudioUx6EDraft? CreateGameplayProductionDraftUx6E(bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var extensions = project.Module.RuntimePolicies.Extensions;
        var gameplay = extensions.PostLaunch.Gameplay;
        return new StudioUx6EDraft(
            history.State.Revision,
            SourceFingerprintUx6E(project),
            extensions,
            new StudioUx6EZeroGravityDraft(gameplay.ZeroGravity.Enabled,
                gameplay.ZeroGravity.ThrusterAcceleration, gameplay.ZeroGravity.MaximumSpeed,
                gameplay.ZeroGravity.LinearDrag, gameplay.ZeroGravity.OrientationAssist,
                gameplay.ZeroGravity.PreserveMomentum),
            new StudioUx6EVehicleDraft(gameplay.Vehicles.Enabled, gameplay.Vehicles.MaximumSeats,
                gameplay.Vehicles.MotionMode, gameplay.Vehicles.MaximumIntegrity,
                gameplay.Vehicles.AllowPossession, gameplay.Vehicles.ReplicateAuthoritatively),
            new StudioUx6EMountDraft(gameplay.Mounts.Enabled, gameplay.Mounts.AllowCombat,
                gameplay.Mounts.AllowInventory, gameplay.Mounts.PersistRiderState,
                gameplay.Mounts.DismountPolicy),
            gameplay.Achievements.Select(item => new StudioUx6EAchievementDraft(item.Id, item.Key,
                item.EventName, item.Threshold, item.PlatformKey,
                item.PrivacySafeTelemetry)).ToArray());
    }

    internal StudioUx6EPreview PreviewGameplayProductionUx6E(StudioUx6EDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx6EApproval();
        if (!TryGetCurrentUx6E(draft, out var history, out var project, out var failure)) return failure!;
        if (!TryBuildGameplayProductionCandidateUx6E(project!, draft, out var candidate,
                out var diagnostic))
            return FailedPreviewUx6E(diagnostic?.Message ?? "Le profil gameplay étendu est invalide.",
                diagnostic!);

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
            history!.State.Revision, candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6E.Handle(history.State, command);
        if (!dryRun.Succeeded) return FailedCommandUx6E(dryRun);

        var source = project!.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay;
        var target = candidate!.PostLaunch.Gameplay;
        var diff = BuildDiffUx6E(source, target);
        var runtime = BuildRuntimePreviewUx6E(target);
        if (diff.Count > 0)
        {
            _approvedGameplayProductionUx6E = draft;
            _approvedGameplayProductionRevisionUx6E = history.State.Revision;
            _approvedGameplayProductionFingerprintUx6E = CandidateFingerprintUx6E(project, draft);
        }
        return new(true, diff.Count > 0,
            diff.Count > 0
                ? "Aperçu sec du gameplay étendu validé · aucune mutation appliquée."
                : "Aperçu sec validé · le profil est déjà identique.",
            "Remplacement canonique d’apesanteur, véhicules, montures et succès; toutes les autres extensions restent inchangées.",
            diff, [], runtime, true, true);
    }

    internal StudioUx6EActionResult ApplyApprovedGameplayProductionUx6E()
    {
        var draft = _approvedGameplayProductionUx6E;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedGameplayProductionRevisionUx6E != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6E(project),
                StringComparison.Ordinal) ||
            !string.Equals(_approvedGameplayProductionFingerprintUx6E,
                CandidateFingerprintUx6E(project, draft), StringComparison.Ordinal) ||
            !TryBuildGameplayProductionCandidateUx6E(project, draft, out var candidate, out _))
        {
            CancelUx6EApproval();
            return new(false, "Relancez l’aperçu sec après avoir rechargé le gameplay étendu.");
        }

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6E.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelUx6EApproval();
            return new(false, HumanCommandFailureUx6E(dryRun));
        }
        var applied = _setRuntimeExtensionsUx6E.Handle(history.State, command with { DryRun = false });
        CancelUx6EApproval();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6E(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Gameplay étendu appliqué · apesanteur, véhicules, montures et succès sont enregistrés.");
    }

    internal StudioUx6EApprovalSnapshot CaptureGameplayProductionApprovalUx6E() =>
        new(_approvedGameplayProductionUx6E, _approvedGameplayProductionRevisionUx6E,
            _approvedGameplayProductionFingerprintUx6E);

    internal void RestoreGameplayProductionApprovalUx6E(StudioUx6EApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedGameplayProductionUx6E = snapshot.Draft;
        _approvedGameplayProductionRevisionUx6E = snapshot.Revision;
        _approvedGameplayProductionFingerprintUx6E = snapshot.Fingerprint;
    }

    internal void CancelUx6EApproval()
    {
        _approvedGameplayProductionUx6E = null;
        _approvedGameplayProductionRevisionUx6E = null;
        _approvedGameplayProductionFingerprintUx6E = null;
    }

    internal string GameplayProductionDraftFingerprintUx6E(StudioUx6EDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Project is null ? string.Empty : CandidateFingerprintUx6E(Project, draft);
    }

    private bool TryGetCurrentUx6E(StudioUx6EDraft draft,
        out ProjectCommandHistory? history,
        out ProjectDefinition? project,
        out StudioUx6EPreview? failure)
    {
        history = History;
        project = Project;
        failure = null;
        if (history is null || project is null)
        {
            failure = FailedPreviewUx6E("Ouvrez un module et choisissez explicitement son profil gameplay.",
                new(Ux6ESelectionCode, "module/runtimePolicies/extensions/postLaunch/gameplay",
                    "Sélection explicite requise."));
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6E(project),
                StringComparison.Ordinal))
        {
            failure = FailedPreviewUx6E("Le projet a changé. Rechargez le gameplay avant l’aperçu.",
                new(Ux6ESourceCode, "module/runtimePolicies/extensions/postLaunch/gameplay",
                    "La révision ou la source ne correspond plus."));
            return false;
        }
        return true;
    }

    private static bool TryBuildGameplayProductionCandidateUx6E(ProjectDefinition project,
        StudioUx6EDraft draft,
        out ModuleRuntimeExtensionProfile? candidate,
        out StudioUx6EDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            EnsureDraftValidUx6E(draft);
            var extensions = project.Module.RuntimePolicies.Extensions;
            var post = extensions.PostLaunch;
            var gameplay = post.Gameplay;
            var zeroGravity = new GameplayProductionProfile.ZeroGravitySettings(
                gameplay.ZeroGravity.Id, draft.ZeroGravity.Enabled,
                draft.ZeroGravity.ThrusterAcceleration, draft.ZeroGravity.MaximumSpeed,
                draft.ZeroGravity.LinearDrag, draft.ZeroGravity.OrientationAssist,
                draft.ZeroGravity.PreserveMomentum);
            var vehicles = new GameplayProductionProfile.VehicleSettings(
                gameplay.Vehicles.Id, draft.Vehicles.Enabled, draft.Vehicles.MaximumSeats,
                draft.Vehicles.MotionMode.Trim().ToLowerInvariant(), draft.Vehicles.MaximumIntegrity,
                draft.Vehicles.AllowPossession, draft.Vehicles.ReplicateAuthoritatively);
            var mounts = new GameplayProductionProfile.MountSettings(
                gameplay.Mounts.Id, draft.Mounts.Enabled, draft.Mounts.AllowCombat,
                draft.Mounts.AllowInventory, draft.Mounts.PersistRiderState,
                draft.Mounts.DismountPolicy.Trim().ToLowerInvariant());
            var achievements = draft.Achievements.Select(item =>
                new GameplayProductionProfile.AchievementDefinition(item.Id, item.Key.Trim(),
                    item.EventName.Trim(), item.Threshold,
                    string.IsNullOrWhiteSpace(item.PlatformKey) ? null : item.PlatformKey.Trim(),
                    item.PrivacySafeTelemetry)).ToArray();
            var updatedGameplay = new GameplayProductionProfile(gameplay.Id, gameplay.SchemaVersion,
                zeroGravity, vehicles, mounts, achievements, gameplay.CuratedPacks,
                Ux6EProvenance);
            var updatedPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion,
                updatedGameplay, post.LiveDirector, post.Network, post.CreatorExperience,
                post.Production, Ux6EProvenance);
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
                extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments,
                extensions.SnapshotPolicy, Ux6EProvenance, extensions.ModernTooling,
                extensions.OperationalReadiness, updatedPost);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                              InvalidOperationException)
        {
            diagnostic = new(Ux6EProfileCode,
                "module/runtimePolicies/extensions/postLaunch/gameplay",
                $"Le contrat canonique refuse le profil : {exception.Message}");
            return false;
        }
    }

    private static void EnsureDraftValidUx6E(StudioUx6EDraft draft)
    {
        var values = new[]
        {
            draft.ZeroGravity.ThrusterAcceleration,
            draft.ZeroGravity.MaximumSpeed,
            draft.ZeroGravity.LinearDrag,
        };
        if (values.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Les valeurs d’apesanteur doivent être finies.");
        if (draft.Vehicles.MotionMode is not ("kinematic" or "physics"))
            throw new ArgumentException("Le mouvement véhicule doit être cinématique ou physique.");
        if (draft.Mounts.DismountPolicy is not ("safe-ground" or "nearest-valid" or "explicit-only"))
            throw new ArgumentException("La descente de monture doit utiliser une politique prise en charge.");
        if (draft.Achievements.Any(item => item.Id == Guid.Empty ||
                                           string.IsNullOrWhiteSpace(item.Key) ||
                                           string.IsNullOrWhiteSpace(item.EventName)))
            throw new ArgumentException("Chaque succès exige une identité, une clé et un événement.");
        if (draft.Achievements.Select(item => item.Id).Distinct().Count() != draft.Achievements.Count ||
            draft.Achievements.Select(item => item.Key.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal).Count() != draft.Achievements.Count)
            throw new ArgumentException("Les identités et clés de succès doivent être uniques.");
    }

    private static IReadOnlyList<string> BuildDiffUx6E(
        GameplayProductionProfile source,
        GameplayProductionProfile candidate)
    {
        var entries = new List<string>();
        if (source.ZeroGravity != candidate.ZeroGravity)
            entries.Add($"Apesanteur · {(candidate.ZeroGravity.Enabled ? "activée" : "désactivée")} · accélération {candidate.ZeroGravity.ThrusterAcceleration:0.###} m/s² · vitesse {candidate.ZeroGravity.MaximumSpeed:0.###} m/s.");
        if (source.Vehicles != candidate.Vehicles)
            entries.Add($"Véhicules · {(candidate.Vehicles.Enabled ? "activés" : "désactivés")} · {candidate.Vehicles.MaximumSeats} place(s) · mouvement {HumanMotionUx6E(candidate.Vehicles.MotionMode)}.");
        if (source.Mounts != candidate.Mounts)
            entries.Add($"Montures · {(candidate.Mounts.Enabled ? "activées" : "désactivées")} · descente {HumanDismountUx6E(candidate.Mounts.DismountPolicy)}.");
        if (!source.Achievements.SequenceEqual(candidate.Achievements))
            entries.Add($"Succès et jalons · {source.Achievements.Count} → {candidate.Achievements.Count} entrée(s) · ordre canonique par clé.");
        return entries;
    }

    private static IReadOnlyList<string> BuildRuntimePreviewUx6E(GameplayProductionProfile profile)
    {
        var requestedSeats = Math.Min(2, profile.Vehicles.MaximumSeats);
        var mobility = PostLaunchCapabilityResolver.ResolveMobility(profile, insideZeroGravity: true,
            inputMagnitude: .5f, requestedSeats, requestMount: true);
        var achievement = profile.Achievements.FirstOrDefault();
        var achievementText = achievement is null
            ? "Succès · aucun jalon authoré; aucune plateforme ni télémétrie sollicitée."
            : AchievementPreviewUx6E(profile, achievement);
        return
        [
            $"Apesanteur résolue · {(mobility.ZeroGravityActive ? "active" : "inactive")} · accélération {mobility.AppliedAcceleration:0.###} m/s² · vitesse maximale {mobility.MaximumSpeed:0.###} m/s.",
            $"Véhicule résolu · demande {requestedSeats} place(s) · {(mobility.VehicleAccepted ? "acceptée" : "refusée")} · profil {(profile.Vehicles.Enabled ? "actif" : "inactif")}.",
            $"Monture résolue · demande explicite {(mobility.MountAccepted ? "acceptée" : "refusée")} · profil {(profile.Mounts.Enabled ? "actif" : "inactif")}.",
            achievementText,
            "Aucune gravité moteur, véhicule, monture, plateforme, télémétrie, physique, acteur, réseau ou ForgeScript n’est exécuté dans Studio.",
        ];
    }

    private static string AchievementPreviewUx6E(GameplayProductionProfile profile,
        GameplayProductionProfile.AchievementDefinition achievement)
    {
        var decision = PostLaunchCapabilityResolver.EvaluateAchievement(profile, achievement.Key,
            achievement.Threshold);
        return $"Succès résolu · {HumanUx6E(decision.Key)} · progression {decision.Progress}/{decision.Threshold} · {(decision.Unlocked ? "déverrouillable" : "incomplet")} · plateforme {(decision.PlatformDispatch ? "planifiée" : "non sollicitée")} · télémétrie {(decision.TelemetryAllowed ? "autorisée" : "désactivée")}.";
    }

    private static StudioUx6EPreview FailedPreviewUx6E(string message, StudioUx6EDiagnostic diagnostic) =>
        new(false, false, message, string.Empty, [], [diagnostic], [], false, true);

    private static StudioUx6EPreview FailedCommandUx6E(CommandResult result)
    {
        var diagnostics = result.Diagnostics.Select(item => new StudioUx6EDiagnostic(item.Code,
            "module/runtimePolicies/extensions/postLaunch/gameplay",
            HumanDiagnosticUx6E(item.Code, item.Message))).ToArray();
        return new(false, false,
            diagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé le profil.",
            string.Empty, [], diagnostics, [], false, true);
    }

    private static string HumanCommandFailureUx6E(CommandResult result) =>
        HumanDiagnosticUx6E(result.Diagnostics.FirstOrDefault()?.Code,
            result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");

    private static string HumanDiagnosticUx6E(string? code, string message) => code switch
    {
        "command.revision_conflict" => "Le projet a changé; rechargez le gameplay étendu.",
        _ => string.IsNullOrWhiteSpace(message)
            ? "La validation canonique a refusé le profil."
            : message,
    };

    private static ProjectDefinition BuildProjectUx6E(ProjectDefinition source,
        ModuleRuntimeExtensionProfile candidate)
    {
        var previous = source.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(previous.Id, previous.SchemaVersion,
            previous.Heartbeats, previous.Rest, previous.SequenceAbort, "runtime-extension-command",
            previous.Cinematics, previous.Gameplay, previous.Rules, candidate,
            previous.GameplayEvents, previous.World, previous.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title,
            source.Module.WithRuntimePolicies(policies), source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private static string CandidateFingerprintUx6E(ProjectDefinition project, StudioUx6EDraft draft)
    {
        if (!TryBuildGameplayProductionCandidateUx6E(project, draft, out var candidate, out _) ||
            candidate is null) return string.Empty;
        return HashUx6E(ProjectJsonSerializer.Serialize(BuildProjectUx6E(project, candidate)));
    }

    private static string SourceFingerprintUx6E(ProjectDefinition project) =>
        HashUx6E(ProjectJsonSerializer.Serialize(project));

    private static string HashUx6E(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HumanUx6E(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identité héritée protégée";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string HumanMotionUx6E(string value) => value switch
    {
        "kinematic" => "cinématique",
        "physics" => "physique",
        _ => "mode hérité protégé",
    };

    private static string HumanDismountUx6E(string value) => value switch
    {
        "safe-ground" => "sol sûr",
        "nearest-valid" => "position valide la plus proche",
        "explicit-only" => "action explicite uniquement",
        _ => "politique héritée protégée",
    };

    internal StudioUx6ESmokeResult RunUx6EContractSmoke()
    {
        var real = Project;
        if (real is null) return FailedUx6ESmoke();
        var realProfile = real.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay;
        var realData = realProfile.Id != Guid.Empty && realProfile.ZeroGravity.Id != Guid.Empty &&
                       realProfile.Vehicles.Id != Guid.Empty && realProfile.Mounts.Id != Guid.Empty &&
                       realProfile.CuratedPacks.Id != Guid.Empty;
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux6-e-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(root, real);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedUx6ESmoke();
            var explicitSelection = editing.CreateGameplayProductionDraftUx6E() is null &&
                                    editing.CreateGameplayProductionDraftUx6E(true) is not null;
            var baseline = editing.CreateGameplayProductionDraftUx6E(true)!;
            var zeroGravityValidation = !editing.PreviewGameplayProductionUx6E(baseline with
            {
                ZeroGravity = baseline.ZeroGravity with { MaximumSpeed = float.NaN },
            }).Succeeded;
            var vehicleValidation = !editing.PreviewGameplayProductionUx6E(baseline with
            {
                Vehicles = baseline.Vehicles with { MotionMode = "teleport" },
            }).Succeeded;
            var mountValidation = !editing.PreviewGameplayProductionUx6E(baseline with
            {
                Mounts = baseline.Mounts with { DismountPolicy = "unsafe" },
            }).Succeeded;
            var duplicateAchievement = new StudioUx6EAchievementDraft(Guid.NewGuid(), "duplicate",
                "module.started", 1, null, false);
            var achievementValidation = !editing.PreviewGameplayProductionUx6E(baseline with
            {
                Achievements = [duplicateAchievement, duplicateAchievement with { Id = Guid.NewGuid() }],
            }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedDraftUx6E(baseline);
            var preview = editing.PreviewGameplayProductionUx6E(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;
            var runtimePreview = preview.RuntimePreview.Count == 5;

            var externalDraft = ChangedDraftUx6E(baseline) with
            {
                Vehicles = changed.Vehicles with
                {
                    MaximumIntegrity = changed.Vehicles.MaximumIntegrity >= 999_999
                        ? changed.Vehicles.MaximumIntegrity - 1
                        : changed.Vehicles.MaximumIntegrity + 1,
                },
            };
            if (!TryBuildGameplayProductionCandidateUx6E(editing.Project!, externalDraft,
                    out var externalCandidate, out _) || externalCandidate is null)
                return FailedUx6ESmoke();
            var external = editing._setRuntimeExtensionsUx6E.Handle(history.State,
                new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    externalCandidate, DryRun: false));
            if (!external.Succeeded) return FailedUx6ESmoke();
            history.Record(external);
            var staleSource = editing.ApplyApprovedGameplayProductionUx6E();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamperDraft = ChangedDraftUx6E(editing.CreateGameplayProductionDraftUx6E(true)!);
            var tamperPreview = editing.PreviewGameplayProductionUx6E(tamperDraft);
            var approval = editing.CaptureGameplayProductionApprovalUx6E();
            editing.RestoreGameplayProductionApprovalUx6E(approval with
            {
                Draft = approval.Draft! with
                {
                    Vehicles = approval.Draft.Vehicles with
                    {
                        MaximumSeats = approval.Draft.Vehicles.MaximumSeats == 64
                            ? 63
                            : approval.Draft.Vehicles.MaximumSeats + 1,
                    },
                },
            });
            var staleApproval = editing.ApplyApprovedGameplayProductionUx6E();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceExtensions = sourceProject.Module.RuntimePolicies.Extensions;
            var sourcePost = sourceExtensions.PostLaunch;
            var sourceGameplay = sourcePost.Gameplay;
            var fresh = ChangedDraftUx6E(editing.CreateGameplayProductionDraftUx6E(true)!);
            if (!TryBuildGameplayProductionCandidateUx6E(sourceProject, fresh,
                    out var expectedExtensions, out _) || expectedExtensions is null)
                return FailedUx6ESmoke();
            var freshPreview = editing.PreviewGameplayProductionUx6E(fresh);
            var applied = editing.ApplyApprovedGameplayProductionUx6E();
            var finalProject = editing.Project!;
            var finalExtensions = finalProject.Module.RuntimePolicies.Extensions;
            var finalPost = finalExtensions.PostLaunch;
            var finalGameplay = finalPost.Gameplay;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_runtime_extensions";
            var neighborProfiles = ReferenceEquals(sourcePost.LiveDirector, finalPost.LiveDirector) &&
                                   ReferenceEquals(sourcePost.Network, finalPost.Network) &&
                                   ReferenceEquals(sourcePost.CreatorExperience, finalPost.CreatorExperience) &&
                                   ReferenceEquals(sourcePost.Production, finalPost.Production) &&
                                   SameCuratedPacksUx6E(sourceGameplay.CuratedPacks,
                                       finalGameplay.CuratedPacks) &&
                                   ReferenceEquals(sourceExtensions.ModernTooling, finalExtensions.ModernTooling) &&
                                   ReferenceEquals(sourceExtensions.OperationalReadiness,
                                       finalExtensions.OperationalReadiness) &&
                                   ReferenceEquals(sourceExtensions.SnapshotPolicy,
                                       finalExtensions.SnapshotPolicy);
            var stableIdentities = sourceGameplay.Id == finalGameplay.Id &&
                                   sourceGameplay.ZeroGravity.Id == finalGameplay.ZeroGravity.Id &&
                                   sourceGameplay.Vehicles.Id == finalGameplay.Vehicles.Id &&
                                   sourceGameplay.Mounts.Id == finalGameplay.Mounts.Id &&
                                   sourceGameplay.CuratedPacks.Id == finalGameplay.CuratedPacks.Id &&
                                   sourceGameplay.Achievements.All(before => finalGameplay.Achievements.Any(after =>
                                       before.Id == after.Id && before.Key == after.Key));
            var expectedProject = BuildProjectUx6E(sourceProject, expectedExtensions);
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
            var humanText = !ContainsUuidUx6E(string.Join(' ',
                editing.GameplayProductionChoicesUx6E.Select(item => item.Label)) + " " +
                string.Join(' ', preview.Diff) + " " + string.Join(' ', preview.RuntimePreview));
            var bounded = preview.RuntimeClaimsBounded && preview.RuntimePreview.Any(item =>
                item.Contains("Aucune gravité moteur", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6ETemporaryWorkspace(root);
            var succeeded = explicitSelection && realData && zeroGravityValidation &&
                            vehicleValidation && mountValidation && achievementValidation &&
                            canonicalDryRun && runtimePreview && staleSourceBlocked &&
                            staleApprovalBlocked && canonicalCommand && undoRedo && persistence &&
                            neighborProfiles && stableIdentities && projectNeighbors && humanText &&
                            bounded && cleanup;
            return new(succeeded, explicitSelection, realData, zeroGravityValidation,
                vehicleValidation, mountValidation, achievementValidation, canonicalDryRun,
                runtimePreview, staleSourceBlocked, staleApprovalBlocked, canonicalCommand,
                undoRedo, persistence, neighborProfiles, stableIdentities, projectNeighbors,
                humanText, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_E_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx6ESmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6ETemporaryWorkspace(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_E_CLEANUP_FAILED");
        }
    }

    internal static StudioUx6EDraft ChangedDraftUx6E(StudioUx6EDraft source)
    {
        var achievements = source.Achievements.Count == 0
            ? new[]
            {
                new StudioUx6EAchievementDraft(Guid.NewGuid(), "studio-first-milestone",
                    "module.started", 1, null, false),
            }
            : source.Achievements.Select((item, index) => index == 0
                ? item with { Threshold = item.Threshold >= 999_999 ? item.Threshold - 1 : item.Threshold + 1 }
                : item).ToArray();
        return source with
        {
            ZeroGravity = source.ZeroGravity with
            {
                Enabled = true,
                ThrusterAcceleration = source.ZeroGravity.ThrusterAcceleration >= 499
                    ? source.ZeroGravity.ThrusterAcceleration - .25f
                    : source.ZeroGravity.ThrusterAcceleration + .25f,
            },
            Vehicles = source.Vehicles with
            {
                Enabled = true,
                MaximumSeats = source.Vehicles.MaximumSeats == 64
                    ? 63
                    : source.Vehicles.MaximumSeats + 1,
            },
            Mounts = source.Mounts with
            {
                Enabled = true,
                AllowCombat = !source.Mounts.AllowCombat,
            },
            Achievements = achievements,
        };
    }

    private static bool SameCuratedPacksUx6E(
        GameplayProductionProfile.CuratedPackSettings source,
        GameplayProductionProfile.CuratedPackSettings candidate) =>
        source.Id == candidate.Id && source.Channel == candidate.Channel &&
        source.Compatibility == candidate.Compatibility &&
        source.AllowedLicenses.SequenceEqual(candidate.AllowedLicenses) &&
        source.RequireTrustedSignature == candidate.RequireTrustedSignature &&
        source.AllowOfflineFallback == candidate.AllowOfflineFallback;

    private static bool ContainsUuidUx6E(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    private static StudioUx6ESmokeResult FailedUx6ESmoke() => new(false, false, false,
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false);

    internal static bool DeleteUx6ETemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux6-e-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private void CloseUx6E() => CancelUx6EApproval();
}
