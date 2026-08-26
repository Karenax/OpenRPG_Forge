using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Domain.Gameplay;

public enum InteractionTargetingMode { Nearest, ViewCone, Cursor }
public enum GameplayCameraMode { Isometric, Orbit, Shoulder, FirstPerson, TopDown, Free, Vehicle, Cinematic }
public enum CameraOcclusionStrategy { Retract, FadeOccluder, HideOccluder }
public enum RuntimeInputDevice { KeyboardMouse, Gamepad }

public sealed record GroundedMovementProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool Required,
    float WalkSpeed,
    float RunSpeed,
    float Acceleration,
    float Deceleration,
    float MaximumSlopeDegrees,
    float StepHeight,
    float JumpVelocity,
    float CrouchSpeedMultiplier,
    bool RootMotion,
    bool NetworkPrediction);

public sealed record InteractionVerbDefinition(
    Guid Id,
    string Key,
    string Label,
    string Command,
    float HoldSeconds,
    IReadOnlyList<string> TargetTags,
    bool ServerAuthoritative);

public sealed record InteractionProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool Required,
    string InputAction,
    InteractionTargetingMode Targeting,
    float FocusDistance,
    float ViewAngleDegrees,
    bool ShowPrompt,
    IReadOnlyList<InteractionVerbDefinition> Verbs);

public sealed record GameplayCameraProfile(
    Guid Id,
    string Key,
    string DisplayName,
    GameplayCameraMode Mode,
    float FieldOfViewDegrees,
    float Distance,
    float MinimumDistance,
    float MaximumDistance,
    float PitchDegrees,
    Vector3Value FocusOffset,
    bool CollisionAvoidance,
    bool AlignCharacterFacing,
    float TerrainClearance = .9f,
    int TerrainProbeSamples = 32,
    float InitialYawDegrees = 39,
    float MinimumPitchDegrees = -85,
    float MaximumPitchDegrees = 85,
    int OcclusionSchemaVersion = 1,
    CameraOcclusionStrategy OcclusionStrategy = CameraOcclusionStrategy.Retract,
    float CollisionRetractSharpness = 24,
    float CollisionReleaseSharpness = 3.25f,
    int WorldLabelSchemaVersion = 1,
    float WorldLabelNearDistance = 2,
    float WorldLabelFarDistance = 28,
    float WorldLabelMaximumDistance = 42,
    float WorldLabelNearScale = .55f,
    float WorldLabelFarScale = 1.15f,
    bool HideLocalHeroLabelInFirstPerson = true);

public sealed record GameplayCameraCatalog(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Required,
    string DefaultProfileKey,
    IReadOnlyList<GameplayCameraProfile> Profiles);

public sealed record RuntimeInputActionDefinition(
    Guid Id,
    string Key,
    string Label,
    string Context,
    bool AccessibilityRequired);

public sealed record RuntimeInputBindingDefinition(
    Guid Id,
    string ActionKey,
    RuntimeInputDevice Device,
    string Binding,
    string Glyph,
    bool Primary);

public sealed record RuntimeInputProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Required,
    bool AllowRebinding,
    bool PerPlayerLocal,
    IReadOnlyList<string> Contexts,
    IReadOnlyList<RuntimeInputActionDefinition> Actions,
    IReadOnlyList<RuntimeInputBindingDefinition> Bindings);

public sealed record RuntimeSavePolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Required,
    int ChunkBytes,
    int AutosaveIntervalSeconds,
    int RollbackSlots,
    string ChecksumAlgorithm,
    bool AllowMigrations,
    bool RequireModuleDependencies);

public sealed record GameplayFoundationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    GroundedMovementProfile GroundedMovement,
    InteractionProfile Interaction,
    GameplayCameraCatalog Cameras,
    RuntimeInputProfile Input,
    RuntimeSavePolicy Save)
{
    public const int CurrentSchemaVersion = 1;

    public static GameplayFoundationProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(moduleId.Value, $"gameplay:foundation:{key}");
        const string provenance = "migration-default";
        var movement = new GroundedMovementProfile(Stable("movement"), 1, provenance, true, true,
            2.5f, 5.5f, 24, 30, 48, .45f, 9, .45f, false, true);
        var verbs = new[]
        {
            new InteractionVerbDefinition(Stable("verb:interact"), "interact", "Interagir", "interact", 0,
                ["creature", "door", "placeable", "item"], true),
            new InteractionVerbDefinition(Stable("verb:inspect"), "inspect", "Examiner", "inspect", .35f,
                ["world"], false),
        };
        var interaction = new InteractionProfile(Stable("interaction"), 1, provenance, true, true,
            "interact", InteractionTargetingMode.ViewCone, 3, 100, true, verbs);
        GameplayCameraProfile Camera(string key, string name, GameplayCameraMode mode, float fov,
            float distance, float pitch, bool align = false) => new(Stable($"camera:{key}"), key, name, mode,
            fov, distance, mode == GameplayCameraMode.FirstPerson ? 0 : 1.5f,
            mode == GameplayCameraMode.FirstPerson ? .1f : 40, pitch, new(0, 1.25f, 0), true, align);
        var cameras = new GameplayCameraCatalog(Stable("cameras"), 1, provenance, true, "orbit",
        [
            Camera("isometric", "Isométrique", GameplayCameraMode.Isometric, 52, 18, 32),
            Camera("orbit", "Orbite", GameplayCameraMode.Orbit, 60, 14, 24, true),
            Camera("shoulder", "Épaule", GameplayCameraMode.Shoulder, 68, 4, 12, true),
            Camera("first-person", "Première personne", GameplayCameraMode.FirstPerson, 75, 0, 0, true),
            Camera("top-down", "Vue du dessus", GameplayCameraMode.TopDown, 50, 20, 70),
            Camera("free", "Libre", GameplayCameraMode.Free, 65, 10, 20),
            Camera("vehicle", "Véhicule", GameplayCameraMode.Vehicle, 70, 9, 18, true),
            Camera("cinematic", "Cinématique", GameplayCameraMode.Cinematic, 50, 8, 15),
        ]);
        var actionSpecs = new (string Key, string Label, string Context, bool Accessible)[]
        {
            ("move_forward", "Avancer", "exploration", true),
            ("move_back", "Reculer", "exploration", true),
            ("move_left", "Aller à gauche", "exploration", true),
            ("move_right", "Aller à droite", "exploration", true),
            ("interact", "Interagir", "exploration", true),
            ("jump", "Sauter", "exploration", true),
            ("quick_save", "Sauvegarde rapide", "system", true),
            ("quick_load", "Chargement rapide", "system", true),
            ("camera_left", "Tourner la caméra à gauche", "exploration", false),
            ("camera_right", "Tourner la caméra à droite", "exploration", false),
        };
        var actions = actionSpecs.Select(spec => new RuntimeInputActionDefinition(Stable($"action:{spec.Key}"),
            spec.Key, spec.Label, spec.Context, spec.Accessible)).ToArray();
        var bindingSpecs = new (string Action, RuntimeInputDevice Device, string Binding, string Glyph, bool Primary)[]
        {
            ("move_forward", RuntimeInputDevice.KeyboardMouse, "W", "key-w", true),
            ("move_back", RuntimeInputDevice.KeyboardMouse, "S", "key-s", true),
            ("move_left", RuntimeInputDevice.KeyboardMouse, "A", "key-a", true),
            ("move_right", RuntimeInputDevice.KeyboardMouse, "D", "key-d", true),
            ("interact", RuntimeInputDevice.KeyboardMouse, "E", "key-e", true),
            ("interact", RuntimeInputDevice.Gamepad, "A", "pad-a", true),
            ("jump", RuntimeInputDevice.KeyboardMouse, "Space", "key-space", true),
            ("jump", RuntimeInputDevice.Gamepad, "B", "pad-b", true),
            ("quick_save", RuntimeInputDevice.KeyboardMouse, "F5", "key-f5", true),
            ("quick_load", RuntimeInputDevice.KeyboardMouse, "F9", "key-f9", true),
            ("camera_left", RuntimeInputDevice.KeyboardMouse, "Home", "key-home", true),
            ("camera_right", RuntimeInputDevice.KeyboardMouse, "End", "key-end", true),
        };
        var bindings = bindingSpecs.Select((spec, index) => new RuntimeInputBindingDefinition(
            Stable($"binding:{index}:{spec.Action}:{spec.Device}"), spec.Action, spec.Device, spec.Binding,
            spec.Glyph, spec.Primary)).ToArray();
        var input = new RuntimeInputProfile(Stable("input"), 1, provenance, true, true, true,
            ["exploration", "dialogue", "combat", "system"], actions, bindings);
        var save = new RuntimeSavePolicy(Stable("save"), 1, provenance, true, 65_536, 60, 3,
            "SHA-256", true, true);
        return new GameplayFoundationProfile(Stable("root"), 1, provenance, movement, interaction,
            cameras, input, save);
    }

    public IReadOnlyList<Guid> AllIds() =>
    [
        Id, GroundedMovement.Id, Interaction.Id, Cameras.Id, Input.Id, Save.Id,
        .. Interaction.Verbs.Select(item => item.Id),
        .. Cameras.Profiles.Select(item => item.Id),
        .. Input.Actions.Select(item => item.Id),
        .. Input.Bindings.Select(item => item.Id),
    ];
}
