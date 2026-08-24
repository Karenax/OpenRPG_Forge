using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6DSelectionCode = "studio.gameplay.simulation.selection_required";
    private const string Ux6DSourceCode = "studio.gameplay.simulation.source_changed";
    private const string Ux6DProfileCode = "studio.gameplay.simulation.invalid";
    private const string Ux6DProvenance = "studio:ux6-d:gameplay-simulation";

    private readonly SetGameplayControlProfileHandler _setGameplayUx6D = new(new SystemClock());
    private StudioUx6DDraft? _approvedGameplayUx6D;
    private ProjectRevision? _approvedGameplayRevisionUx6D;
    private string? _approvedGameplayFingerprintUx6D;

    internal IReadOnlyList<StudioUx6DProfileChoice> GameplaySimulationChoicesUx6D
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            return project.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(area => new StudioUx6DProfileChoice(
                    $"{HumanUx6D(project.Module.DisplayName)} · contexte {HumanUx6D(area.DisplayName)}", area.Id))
                .ToArray();
        }
    }

    internal StudioUx6DDraft? CreateGameplaySimulationDraftUx6D(
        AreaId? contextAreaId,
        bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null || contextAreaId is null ||
            project.Areas.All(item => item.Id != contextAreaId)) return null;

        var source = project.Module.RuntimePolicies.Gameplay;
        var simulation = source.Simulation;
        return new StudioUx6DDraft(
            history.State.Revision,
            SourceFingerprintUx6D(project),
            contextAreaId,
            source,
            new StudioUx6DFlightDraft(simulation.Flight.Enabled, simulation.Flight.Required,
                simulation.Flight.MaximumSpeed, simulation.Flight.Acceleration,
                simulation.Flight.MaximumAltitude, simulation.Flight.BankingDegrees,
                simulation.Flight.Lift, simulation.Flight.RequiresLandingSurface,
                simulation.Flight.VolumetricNavigation, simulation.Flight.LandingProbeDistance,
                simulation.Flight.InputContext),
            new StudioUx6DProjectileDraft(simulation.Projectiles.Enabled,
                simulation.Projectiles.Required, simulation.Projectiles.Mode,
                simulation.Projectiles.Speed, simulation.Projectiles.Gravity,
                simulation.Projectiles.LifetimeSeconds, simulation.Projectiles.HomingStrength,
                simulation.Projectiles.CollisionRadius, simulation.Projectiles.ServerAuthoritative,
                simulation.Projectiles.Prediction, simulation.Projectiles.VfxAssetId,
                simulation.Projectiles.ImpactVfxAssetId, simulation.Projectiles.CollisionMask,
                simulation.Projectiles.ServerRevision, simulation.Projectiles.AuditTag),
            new StudioUx6DPhysicsDraft(simulation.Physics.Enabled, simulation.Physics.Required,
                simulation.Physics.FixedStepSeconds,
                simulation.Physics.Bodies.Select(item => new StudioUx6DPhysicsBodyDraft(item.Id,
                    item.Name, item.Mode, item.Mass, item.LinearDamping, item.GravityScale,
                    item.IsTrigger, item.InteractsWithCharacters, item.BoundaryMinimum,
                    item.BoundaryMaximum, item.NetworkPolicy, item.RequiredAtRuntime)).ToArray(),
                simulation.Physics.Joints.Select(item => new StudioUx6DPhysicsJointDraft(item.Id,
                    item.Name, item.Mode, item.BodyAId, item.BodyBId, item.BreakForce,
                    item.CollisionExcluded)).ToArray()),
            new StudioUx6DTargetingDraft(simulation.Targeting.Enabled, simulation.Targeting.Required,
                simulation.Targeting.Mode, simulation.Targeting.MaximumRange,
                simulation.Targeting.Radius, simulation.Targeting.AngleDegrees,
                simulation.Targeting.MaximumTargets, simulation.Targeting.RequiresLineOfSight,
                simulation.Targeting.InputAction, simulation.Targeting.PreviewColor,
                simulation.Targeting.RequiredTags.ToArray(), simulation.Targeting.BlockedTags.ToArray()));
    }

    internal StudioUx6DPreview PreviewGameplaySimulationUx6D(StudioUx6DDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx6DApproval();
        if (!TryGetCurrentUx6D(draft, out var history, out var project, out var failure)) return failure!;
        if (!TryBuildGameplayCandidateUx6D(draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6D(diagnostic?.Message ?? "La simulation gameplay est invalide.", diagnostic!);

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history!.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6D.Handle(history.State, command);
        if (!dryRun.Succeeded) return FailedCommandUx6D(dryRun);

        var source = project!.Module.RuntimePolicies.Gameplay;
        var diff = BuildDiffUx6D(source.Simulation, candidate!.Simulation);
        var runtime = BuildRuntimePreviewUx6D(candidate.Simulation);
        if (diff.Count > 0)
        {
            _approvedGameplayUx6D = draft;
            _approvedGameplayRevisionUx6D = history.State.Revision;
            _approvedGameplayFingerprintUx6D = CandidateFingerprintUx6D(project, draft);
        }
        return new(true, diff.Count > 0,
            diff.Count > 0
                ? "Aperçu sec de la simulation gameplay validé · aucune mutation appliquée."
                : "Aperçu sec validé · le profil est déjà identique.",
            "Remplacement canonique de vol, projectiles, physique et ciblage; temps du monde et autres queues conservés.",
            diff, [], runtime, true, true);
    }

    internal StudioUx6DActionResult ApplyApprovedGameplaySimulationUx6D()
    {
        var draft = _approvedGameplayUx6D;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedGameplayRevisionUx6D != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6D(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedGameplayFingerprintUx6D, CandidateFingerprintUx6D(project, draft),
                StringComparison.Ordinal) ||
            !TryBuildGameplayCandidateUx6D(draft, out var candidate, out _))
        {
            CancelUx6DApproval();
            return new(false, "Relancez l’aperçu sec après avoir rechargé la simulation gameplay.");
        }

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6D.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelUx6DApproval();
            return new(false, HumanCommandFailureUx6D(dryRun));
        }
        var applied = _setGameplayUx6D.Handle(history.State, command with { DryRun = false });
        CancelUx6DApproval();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6D(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Simulation gameplay appliquée · vol, projectiles, physique et ciblage sont enregistrés.");
    }

    internal StudioUx6DApprovalSnapshot CaptureGameplaySimulationApprovalUx6D() =>
        new(_approvedGameplayUx6D, _approvedGameplayRevisionUx6D, _approvedGameplayFingerprintUx6D);

    internal void RestoreGameplaySimulationApprovalUx6D(StudioUx6DApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedGameplayUx6D = snapshot.Draft;
        _approvedGameplayRevisionUx6D = snapshot.Revision;
        _approvedGameplayFingerprintUx6D = snapshot.Fingerprint;
    }

    internal void CancelUx6DApproval()
    {
        _approvedGameplayUx6D = null;
        _approvedGameplayRevisionUx6D = null;
        _approvedGameplayFingerprintUx6D = null;
    }

    internal string GameplaySimulationDraftFingerprintUx6D(StudioUx6DDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Project is null ? string.Empty : CandidateFingerprintUx6D(Project, draft);
    }

    private bool TryGetCurrentUx6D(StudioUx6DDraft draft, out ProjectCommandHistory? history,
        out ProjectDefinition? project, out StudioUx6DPreview? failure)
    {
        history = History;
        project = Project;
        failure = null;
        if (history is null || project is null || project.Areas.All(item => item.Id != draft.ContextAreaId))
        {
            failure = FailedPreviewUx6D("Ouvrez un module et choisissez explicitement un contexte de zone.",
                new(Ux6DSelectionCode, "module/runtimePolicies/gameplay/simulation",
                    "Sélection explicite requise."));
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6D(project), StringComparison.Ordinal))
        {
            failure = FailedPreviewUx6D("Le projet a changé. Rechargez la simulation avant l’aperçu.",
                new(Ux6DSourceCode, "module/runtimePolicies/gameplay/simulation",
                    "La révision ou la source ne correspond plus."));
            return false;
        }
        return true;
    }

    private static bool TryBuildGameplayCandidateUx6D(StudioUx6DDraft draft,
        out ModuleGameplayControlProfile? candidate, out StudioUx6DDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            EnsureFiniteAndEnumsUx6D(draft);
            var source = draft.SourceGameplay;
            var simulation = source.Simulation;
            var flight = new FlyingMovementProfile(simulation.Flight.Id, simulation.Flight.SchemaVersion,
                Ux6DProvenance, draft.Flight.Enabled, draft.Flight.Required,
                draft.Flight.MaximumSpeed, draft.Flight.Acceleration, draft.Flight.MaximumAltitude,
                draft.Flight.BankingDegrees, draft.Flight.Lift, draft.Flight.RequiresLandingSurface,
                draft.Flight.VolumetricNavigation, draft.Flight.LandingProbeDistance,
                draft.Flight.InputContext.Trim());
            var projectiles = new ProjectileProfile(simulation.Projectiles.Id,
                simulation.Projectiles.SchemaVersion, Ux6DProvenance, draft.Projectiles.Enabled,
                draft.Projectiles.Required, draft.Projectiles.Mode, draft.Projectiles.Speed,
                draft.Projectiles.Gravity, draft.Projectiles.LifetimeSeconds,
                draft.Projectiles.HomingStrength, draft.Projectiles.CollisionRadius,
                draft.Projectiles.ServerAuthoritative, draft.Projectiles.Prediction,
                draft.Projectiles.VfxAssetId.Trim(), draft.Projectiles.ImpactVfxAssetId.Trim(),
                draft.Projectiles.CollisionMask.Trim(), draft.Projectiles.ServerRevision,
                draft.Projectiles.AuditTag.Trim());
            var bodies = draft.Physics.Bodies.Select(item => new PhysicsBodyProfile(item.Id, 1,
                Ux6DProvenance, item.Name.Trim(), item.Mode, item.Mass, item.LinearDamping,
                item.GravityScale, item.IsTrigger, item.InteractsWithCharacters,
                item.BoundaryMinimum, item.BoundaryMaximum, item.NetworkPolicy.Trim(),
                item.RequiredAtRuntime)).ToArray();
            var joints = draft.Physics.Joints.Select(item => new PhysicsJointProfile(item.Id, 1,
                Ux6DProvenance, item.Name.Trim(), item.Mode, item.BodyAId, item.BodyBId,
                item.BreakForce, item.CollisionExcluded)).ToArray();
            var physics = new PhysicsSceneProfile(simulation.Physics.Id,
                simulation.Physics.SchemaVersion, Ux6DProvenance, draft.Physics.Enabled,
                draft.Physics.Required, draft.Physics.FixedStepSeconds, bodies, joints);
            var targeting = new TargetingProfile(simulation.Targeting.Id,
                simulation.Targeting.SchemaVersion, Ux6DProvenance, draft.Targeting.Enabled,
                draft.Targeting.Required, draft.Targeting.Mode, draft.Targeting.MaximumRange,
                draft.Targeting.Radius, draft.Targeting.AngleDegrees, draft.Targeting.MaximumTargets,
                draft.Targeting.RequiresLineOfSight, draft.Targeting.InputAction.Trim(),
                draft.Targeting.PreviewColor.Trim(), NormalizeLinesUx6D(draft.Targeting.RequiredTags),
                NormalizeLinesUx6D(draft.Targeting.BlockedTags));
            var replacement = new GameplaySimulationProfile(simulation.Id, simulation.SchemaVersion,
                Ux6DProvenance, simulation.Enabled, simulation.RequiredAtRuntime, flight, projectiles,
                physics, targeting, simulation.WorldTime);
            var invalid = GameplaySimulationProfileValidator.Validate(replacement).FirstOrDefault();
            if (invalid is not null)
            {
                diagnostic = new(invalid.Code, invalid.Path,
                    HumanDiagnosticUx6D(invalid.Code, invalid.Message));
                return false;
            }
            candidate = new ModuleGameplayControlProfile(source.Id, source.SchemaVersion,
                source.Swimming, source.Climbing, source.RadialMenu, source.TimeControl,
                source.CombatModes, Ux6DProvenance, source.Foundation, replacement, source.Session);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                              InvalidOperationException)
        {
            diagnostic = new(Ux6DProfileCode, "module/runtimePolicies/gameplay/simulation",
                $"Le contrat canonique refuse le profil : {exception.Message}");
            return false;
        }
    }

    private static void EnsureFiniteAndEnumsUx6D(StudioUx6DDraft draft)
    {
        var values = new List<float>
        {
            draft.Flight.MaximumSpeed, draft.Flight.Acceleration, draft.Flight.MaximumAltitude,
            draft.Flight.BankingDegrees, draft.Flight.Lift, draft.Flight.LandingProbeDistance,
            draft.Projectiles.Speed, draft.Projectiles.Gravity, draft.Projectiles.LifetimeSeconds,
            draft.Projectiles.HomingStrength, draft.Projectiles.CollisionRadius,
            draft.Physics.FixedStepSeconds, draft.Targeting.MaximumRange, draft.Targeting.Radius,
            draft.Targeting.AngleDegrees,
        };
        values.AddRange(draft.Physics.Bodies.SelectMany(item => new[]
        {
            item.Mass, item.LinearDamping, item.GravityScale,
            item.BoundaryMinimum.X, item.BoundaryMinimum.Y, item.BoundaryMinimum.Z,
            item.BoundaryMaximum.X, item.BoundaryMaximum.Y, item.BoundaryMaximum.Z,
        }));
        values.AddRange(draft.Physics.Joints.Select(item => item.BreakForce));
        if (values.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Les valeurs numériques doivent être finies.");
        if (!Enum.IsDefined(draft.Projectiles.Mode) || !Enum.IsDefined(draft.Targeting.Mode) ||
            draft.Physics.Bodies.Any(item => !Enum.IsDefined(item.Mode)) ||
            draft.Physics.Joints.Any(item => !Enum.IsDefined(item.Mode)))
            throw new ArgumentException("Une option sélectionnée n’appartient pas au contrat gameplay.");
    }

    private static IReadOnlyList<string> BuildDiffUx6D(
        GameplaySimulationProfile source,
        GameplaySimulationProfile candidate)
    {
        var entries = new List<string>();
        if (source.Flight != candidate.Flight)
            entries.Add($"Vol libre · vitesse {source.Flight.MaximumSpeed:0.###} → {candidate.Flight.MaximumSpeed:0.###} m/s · altitude {candidate.Flight.MaximumAltitude:0.###} m.");
        if (source.Projectiles != candidate.Projectiles)
            entries.Add($"Projectiles · {HumanProjectileUx6D(candidate.Projectiles.Mode)} · vitesse {candidate.Projectiles.Speed:0.###} m/s · durée {candidate.Projectiles.LifetimeSeconds:0.###} s.");
        if (!source.Physics.Bodies.SequenceEqual(candidate.Physics.Bodies) ||
            !source.Physics.Joints.SequenceEqual(candidate.Physics.Joints) ||
            source.Physics with { Bodies = [], Joints = [] } != candidate.Physics with { Bodies = [], Joints = [] })
            entries.Add($"Physique 3D · {candidate.Physics.Bodies.Count} corps · {candidate.Physics.Joints.Count} liaison(s) · pas {candidate.Physics.FixedStepSeconds:0.######} s.");
        if (source.Targeting != candidate.Targeting)
            entries.Add($"Ciblage · {HumanTargetingUx6D(candidate.Targeting.Mode)} · portée {candidate.Targeting.MaximumRange:0.###} m · {candidate.Targeting.MaximumTargets} cible(s).");
        return entries;
    }

    private static IReadOnlyList<string> BuildRuntimePreviewUx6D(GameplaySimulationProfile profile)
    {
        var flight = GameplaySimulation.FlyStep(
            new GameplayVectorState(new(0, 2, 0), Vector3Value.Zero), profile.Flight,
            new(1, .5f, 0), .25f,
            new FlightNavigationVolume(new(-20, 0, -20),
                new(20, profile.Flight.MaximumAltitude, 20), 0));
        var projectile = GameplaySimulation.StepProjectile(
            new ProjectileRuntimeState(new(Vector3Value.Zero,
                new(profile.Projectiles.Speed, 0, 0)), 0, true, profile.Projectiles.ServerRevision),
            profile.Projectiles, new(8, 0, 0), .1f, true, (_, _, _) => true);
        var states = profile.Physics.Bodies.Select(item => new PhysicsBodyRuntimeState(item.Id,
            new GameplayVectorState(new(0, 2, 0), new(1, 0, 0)), false)).ToArray();
        var physics = GameplaySimulation.SimulatePhysics(profile.Physics, states,
            MathF.Max(profile.Physics.FixedStepSeconds, .05f), true);
        var targeting = GameplaySimulation.PreviewTargeting(profile.Targeting, Vector3Value.Zero,
            new(0, 0, -1),
            [new(Guid.Parse("f6600000-0000-4000-8000-000000000001"), new(0, 0, -3),
                profile.Targeting.RequiredTags.Count > 0 ? profile.Targeting.RequiredTags : ["targetable"], true)]);
        return
        [
            $"Vol résolu · vitesse {LengthUx6D(flight.State.Velocity):0.###} m/s · inclinaison {flight.BankingDegrees:0.###}° · volume {(flight.InsideNavigationVolume ? "respecté" : "dépassé")}.",
            $"Projectile résolu · {(projectile.Impacted ? "impact" : "en vol")} · position {VectorUx6D(projectile.ImpactPosition)} · autorité {(projectile.AuthorityGranted ? "accordée" : "refusée")}.",
            $"Physique résolue · {physics.Bodies.Count} corps · {physics.ActiveJointIds.Count} liaison(s) · {physics.SimulatedSeconds:0.######} s simulée(s).",
            $"Ciblage résolu · géométrie {targeting.Geometry.Count} point(s) · {targeting.ValidTargetIds.Count} cible(s) valide(s) · couleur {HumanUx6D(targeting.Color)}.",
            "Aucun acteur, Rigidbody3D, collision Godot, projectile natif, VFX, caméra, pathfinding, réseau ou ForgeScript n’est exécuté dans Studio.",
        ];
    }

    private static StudioUx6DPreview FailedPreviewUx6D(string message, StudioUx6DDiagnostic diagnostic) =>
        new(false, false, message, string.Empty, [], [diagnostic], [], false, true);

    private static StudioUx6DPreview FailedCommandUx6D(CommandResult result)
    {
        var diagnostics = result.Diagnostics.Select(item => new StudioUx6DDiagnostic(item.Code,
            "module/runtimePolicies/gameplay/simulation", HumanDiagnosticUx6D(item.Code, item.Message))).ToArray();
        return new(false, false,
            diagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé le profil.",
            string.Empty, [], diagnostics, [], false, true);
    }

    private static string HumanCommandFailureUx6D(CommandResult result) =>
        HumanDiagnosticUx6D(result.Diagnostics.FirstOrDefault()?.Code,
            result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");

    private static string HumanDiagnosticUx6D(string? code, string message) => code switch
    {
        "command.revision_conflict" => "Le projet a changé; rechargez la simulation gameplay.",
        GameplaySimulationProfileValidator.FlightCode =>
            "Le vol exige vitesse, accélération, altitude, inclinaison, portance, atterrissage et contexte valides.",
        GameplaySimulationProfileValidator.ProjectileCode =>
            "Le projectile exige mouvement, collision, VFX, prédiction et autorité cohérents.",
        GameplaySimulationProfileValidator.PhysicsCode =>
            "Les corps physiques exigent bornes, masse, amortissement, gravité et politique réseau valides.",
        GameplaySimulationProfileValidator.PhysicsReferenceCode =>
            "Chaque liaison physique doit relier deux corps existants distincts.",
        GameplaySimulationProfileValidator.TargetingCode =>
            "Le ciblage exige géométrie, tags, couleur et action d’entrée valides.",
        _ => string.IsNullOrWhiteSpace(message) ? "La validation canonique a refusé le profil." : message,
    };

    private static ProjectDefinition BuildGameplayProjectUx6D(
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

    private static string CandidateFingerprintUx6D(ProjectDefinition project, StudioUx6DDraft draft)
    {
        if (!TryBuildGameplayCandidateUx6D(draft, out var candidate, out _) || candidate is null)
            return string.Empty;
        return HashUx6D(ProjectJsonSerializer.Serialize(BuildGameplayProjectUx6D(project, candidate)));
    }

    private static string SourceFingerprintUx6D(ProjectDefinition project) =>
        HashUx6D(ProjectJsonSerializer.Serialize(project));

    private static string HashUx6D(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HumanUx6D(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identifiant protégé";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string HumanProjectileUx6D(ProjectileMode mode) => mode switch
    {
        ProjectileMode.Ballistic => "balistique",
        ProjectileMode.Homing => "guidé",
        ProjectileMode.Hitscan => "impact instantané",
        _ => "mode inconnu",
    };

    private static string HumanTargetingUx6D(TargetingMode mode) => mode switch
    {
        TargetingMode.Self => "soi-même",
        TargetingMode.Entity => "entité",
        TargetingMode.Point => "point",
        TargetingMode.Direction => "direction",
        TargetingMode.Area => "zone",
        TargetingMode.Cone => "cône",
        TargetingMode.Line => "ligne",
        TargetingMode.Chain => "chaîne",
        _ => "mode inconnu",
    };

    private static string[] NormalizeLinesUx6D(IEnumerable<string> values) => values
        .Select(value => (value ?? string.Empty).Trim()).Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal).ToArray();

    private static float LengthUx6D(Vector3Value value) =>
        MathF.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);

    private static string VectorUx6D(Vector3Value value) =>
        $"({value.X:0.###}, {value.Y:0.###}, {value.Z:0.###})";

    internal StudioUx6DSmokeResult RunUx6DContractSmoke()
    {
        var real = Project;
        if (real is null || real.Areas.Count == 0) return FailedUx6DSmoke();
        var realSimulation = real.Module.RuntimePolicies.Gameplay.Simulation;
        var realData = realSimulation.Flight.Enabled && realSimulation.Projectiles.Enabled &&
                       realSimulation.Physics.Bodies.Count >= 2 && realSimulation.Physics.Joints.Count > 0 &&
                       realSimulation.Targeting.Enabled;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-d-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(root, real);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedUx6DSmoke();
            var areaId = editing.Project!.Areas[0].Id;
            var explicitSelection = editing.CreateGameplaySimulationDraftUx6D(areaId) is null &&
                                    editing.CreateGameplaySimulationDraftUx6D(areaId, true) is not null;
            var baseline = editing.CreateGameplaySimulationDraftUx6D(areaId, true)!;
            var flightValidation = !editing.PreviewGameplaySimulationUx6D(baseline with
            {
                Flight = baseline.Flight with { MaximumSpeed = float.NaN },
            }).Succeeded;
            var projectileValidation = !editing.PreviewGameplaySimulationUx6D(baseline with
            {
                Projectiles = baseline.Projectiles with { VfxAssetId = string.Empty },
            }).Succeeded;
            var physicsValidation = !editing.PreviewGameplaySimulationUx6D(baseline with
            {
                Physics = baseline.Physics with
                {
                    Joints = baseline.Physics.Joints.Select((item, index) => index == 0
                        ? item with { BodyBId = item.BodyAId } : item).ToArray(),
                },
            }).Succeeded;
            var targetingValidation = !editing.PreviewGameplaySimulationUx6D(baseline with
            {
                Targeting = baseline.Targeting with { PreviewColor = "orange" },
            }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedDraftUx6D(baseline);
            var preview = editing.PreviewGameplaySimulationUx6D(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;
            var runtimePreview = preview.RuntimePreview.Count == 5;

            var externalDraft = ChangedDraftUx6D(baseline) with
            {
                Flight = changed.Flight with { Acceleration = changed.Flight.Acceleration + .1f },
            };
            if (!TryBuildGameplayCandidateUx6D(externalDraft, out var externalCandidate, out _) ||
                externalCandidate is null) return FailedUx6DSmoke();
            var external = editing._setGameplayUx6D.Handle(history.State,
                new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    externalCandidate, DryRun: false));
            if (!external.Succeeded) return FailedUx6DSmoke();
            history.Record(external);
            var staleSource = editing.ApplyApprovedGameplaySimulationUx6D();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamperDraft = ChangedDraftUx6D(editing.CreateGameplaySimulationDraftUx6D(areaId, true)!);
            var tamperPreview = editing.PreviewGameplaySimulationUx6D(tamperDraft);
            var approval = editing.CaptureGameplaySimulationApprovalUx6D();
            editing.RestoreGameplaySimulationApprovalUx6D(approval with
            {
                Draft = approval.Draft! with
                {
                    Targeting = approval.Draft.Targeting with
                    {
                        MaximumTargets = approval.Draft.Targeting.MaximumTargets + 1,
                    },
                },
            });
            var staleApproval = editing.ApplyApprovedGameplaySimulationUx6D();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceGameplay = sourceProject.Module.RuntimePolicies.Gameplay;
            var sourceSimulation = sourceGameplay.Simulation;
            var fresh = ChangedDraftUx6D(editing.CreateGameplaySimulationDraftUx6D(areaId, true)!);
            if (!TryBuildGameplayCandidateUx6D(fresh, out var expectedGameplay, out _) ||
                expectedGameplay is null) return FailedUx6DSmoke();
            var freshPreview = editing.PreviewGameplaySimulationUx6D(fresh);
            var applied = editing.ApplyApprovedGameplaySimulationUx6D();
            var finalProject = editing.Project!;
            var finalGameplay = finalProject.Module.RuntimePolicies.Gameplay;
            var finalSimulation = finalGameplay.Simulation;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_gameplay_control_profile";
            var worldTimePreserved = ReferenceEquals(sourceSimulation.WorldTime, finalSimulation.WorldTime);
            var gameplayTails = ReferenceEquals(sourceGameplay.Swimming, finalGameplay.Swimming) &&
                                ReferenceEquals(sourceGameplay.Climbing, finalGameplay.Climbing) &&
                                ReferenceEquals(sourceGameplay.RadialMenu, finalGameplay.RadialMenu) &&
                                ReferenceEquals(sourceGameplay.TimeControl, finalGameplay.TimeControl) &&
                                ReferenceEquals(sourceGameplay.CombatModes, finalGameplay.CombatModes) &&
                                ReferenceEquals(sourceGameplay.Foundation, finalGameplay.Foundation) &&
                                ReferenceEquals(sourceGameplay.Session, finalGameplay.Session);
            var stableIdentities = sourceSimulation.Id == finalSimulation.Id &&
                                   sourceSimulation.Flight.Id == finalSimulation.Flight.Id &&
                                   sourceSimulation.Projectiles.Id == finalSimulation.Projectiles.Id &&
                                   sourceSimulation.Physics.Id == finalSimulation.Physics.Id &&
                                   sourceSimulation.Targeting.Id == finalSimulation.Targeting.Id &&
                                   sourceSimulation.Physics.Bodies.Select(item => item.Id)
                                       .SequenceEqual(finalSimulation.Physics.Bodies.Select(item => item.Id)) &&
                                   sourceSimulation.Physics.Joints.Select(item => item.Id)
                                       .SequenceEqual(finalSimulation.Physics.Joints.Select(item => item.Id));
            var expectedProject = BuildGameplayProjectUx6D(sourceProject, expectedGameplay);
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
            var humanText = !ContainsUuidUx6D(string.Join(' ',
                editing.GameplaySimulationChoicesUx6D.Select(item => item.Label)) + " " +
                string.Join(' ', preview.Diff) + " " + string.Join(' ', preview.RuntimePreview));
            var bounded = preview.RuntimeClaimsBounded && preview.RuntimePreview.Any(item =>
                item.Contains("Aucun acteur", StringComparison.Ordinal));

            editing.Close(); reopened.Close();
            cleanup = DeleteUx6DTemporaryWorkspace(root);
            var succeeded = explicitSelection && realData && flightValidation && projectileValidation &&
                            physicsValidation && targetingValidation && canonicalDryRun && runtimePreview &&
                            staleSourceBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && worldTimePreserved && gameplayTails && stableIdentities &&
                            projectNeighbors && humanText && bounded && cleanup;
            return new(succeeded, explicitSelection, realData, flightValidation, projectileValidation,
                physicsValidation, targetingValidation, canonicalDryRun, runtimePreview,
                staleSourceBlocked, staleApprovalBlocked, canonicalCommand, undoRedo, persistence,
                worldTimePreserved, gameplayTails, stableIdentities, projectNeighbors, humanText,
                bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_D_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx6DSmoke();
        }
        finally
        {
            editing.Close(); reopened.Close();
            cleanup = DeleteUx6DTemporaryWorkspace(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_D_CLEANUP_FAILED");
        }
    }

    internal static StudioUx6DDraft ChangedDraftUx6D(StudioUx6DDraft source)
    {
        var bodies = source.Physics.Bodies.Select((item, index) => index == 0
            ? item with { Mass = item.Mass >= 9999 ? item.Mass - .25f : item.Mass + .25f }
            : item).ToArray();
        return source with
        {
            Flight = source.Flight with
            {
                MaximumSpeed = source.Flight.MaximumSpeed >= 999 ?
                    source.Flight.MaximumSpeed - .25f : source.Flight.MaximumSpeed + .25f,
            },
            Projectiles = source.Projectiles with
            {
                Speed = source.Projectiles.Speed >= 9999 ?
                    source.Projectiles.Speed - .25f : source.Projectiles.Speed + .25f,
            },
            Physics = source.Physics with { Bodies = bodies },
            Targeting = source.Targeting with
            {
                MaximumRange = source.Targeting.MaximumRange >= 9999 ?
                    source.Targeting.MaximumRange - .25f : source.Targeting.MaximumRange + .25f,
            },
        };
    }

    private static bool ContainsUuidUx6D(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    private static StudioUx6DSmokeResult FailedUx6DSmoke() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false);

    internal static bool DeleteUx6DTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux6-d-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private void CloseUx6D() => CancelUx6DApproval();
}
