using Forge.Domain.Validation;

namespace Forge.Domain.Gameplay;

public static class GameplayFoundationValidator
{
    public const string IdentityCode = "runtime.gameplay.foundation.identity";
    public const string DuplicateCode = "runtime.gameplay.foundation.uuid_duplicate";
    public const string MovementCode = "runtime.gameplay.grounded.invalid";
    public const string InteractionCode = "runtime.gameplay.interaction.invalid";
    public const string InteractionReferenceCode = "runtime.gameplay.interaction.input_missing";
    public const string CameraCode = "runtime.gameplay.camera.invalid";
    public const string CameraDefaultCode = "runtime.gameplay.camera.default_missing";
    public const string InputCode = "runtime.gameplay.input.invalid";
    public const string InputReferenceCode = "runtime.gameplay.input.reference_missing";
    public const string SaveCode = "runtime.gameplay.save.invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(GameplayFoundationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var result = new List<DomainDiagnostic>();
        const string root = "module/runtimePolicies/gameplay/foundation";
        if (profile.Id == Guid.Empty || profile.SchemaVersion != GameplayFoundationProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            Add(result, IdentityCode, "Gameplay foundation requires a stable UUID, schema v1 and provenance.", root, profile.Id);
        var ids = profile.AllIds();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            Add(result, DuplicateCode, "Gameplay foundation UUIDs must be non-empty and unique.", root, profile.Id);

        var movement = profile.GroundedMovement;
        if (movement.SchemaVersion != 1 || string.IsNullOrWhiteSpace(movement.Provenance) ||
            movement.WalkSpeed is <= 0 or > 50 || movement.RunSpeed < movement.WalkSpeed || movement.RunSpeed > 100 ||
            movement.Acceleration is <= 0 or > 500 || movement.Deceleration is <= 0 or > 500 ||
            movement.MaximumSlopeDegrees is < 0 or > 89 || movement.StepHeight is < 0 or > 5 ||
            movement.JumpVelocity is < 0 or > 100 || movement.CrouchSpeedMultiplier is <= 0 or > 1)
            Add(result, MovementCode, "Ground movement values must be finite, positive and within authoring bounds.",
                $"{root}/groundedMovement/{movement.Id}", movement.Id);

        var interaction = profile.Interaction;
        var verbKeys = interaction.Verbs.Select(item => Normalize(item.Key)).ToArray();
        if (interaction.SchemaVersion != 1 || string.IsNullOrWhiteSpace(interaction.Provenance) ||
            string.IsNullOrWhiteSpace(interaction.InputAction) || !Enum.IsDefined(interaction.Targeting) ||
            interaction.FocusDistance is <= 0 or > 100 || interaction.ViewAngleDegrees is <= 0 or > 360 ||
            interaction.Required && interaction.Verbs.Count == 0 ||
            interaction.Verbs.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Label) ||
                string.IsNullOrWhiteSpace(item.Command) || item.HoldSeconds is < 0 or > 30 || item.TargetTags.Count == 0) ||
            verbKeys.Distinct(StringComparer.Ordinal).Count() != verbKeys.Length)
            Add(result, InteractionCode, "Interaction requires bounded focus, unique verbs, labels, commands and target tags.",
                $"{root}/interaction/{interaction.Id}", interaction.Id);

        var cameras = profile.Cameras;
        var cameraKeys = cameras.Profiles.Select(item => Normalize(item.Key)).ToArray();
        if (cameras.SchemaVersion != 1 || string.IsNullOrWhiteSpace(cameras.Provenance) ||
            cameras.Required && cameras.Profiles.Count == 0 ||
            cameraKeys.Distinct(StringComparer.Ordinal).Count() != cameraKeys.Length ||
            cameras.Profiles.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.DisplayName) ||
                !Enum.IsDefined(item.Mode) || item.FieldOfViewDegrees is < 20 or > 140 ||
                item.Distance < 0 || item.MinimumDistance < 0 || item.MaximumDistance < item.MinimumDistance ||
                item.Distance < item.MinimumDistance || item.Distance > item.MaximumDistance ||
                !float.IsFinite(item.PitchDegrees) || item.PitchDegrees is < -89 or > 89 ||
                !Finite(item.FocusOffset)))
            Add(result, CameraCode, "Camera profiles require unique keys, supported modes and bounded optics.",
                $"{root}/cameras/{cameras.Id}", cameras.Id);
        foreach (var camera in cameras.Profiles)
        {
            if (!float.IsFinite(camera.TerrainClearance) || camera.TerrainClearance is <= 0 or > 20)
                Add(result, CameraCode, "Camera terrain clearance must be finite and between 0 and 20 metres.",
                    $"{root}/cameras/{camera.Id}/terrainClearance", camera.Id);
            if (camera.TerrainProbeSamples is < 4 or > 128)
                Add(result, CameraCode, "Camera terrain probing requires between 4 and 128 samples.",
                    $"{root}/cameras/{camera.Id}/terrainProbeSamples", camera.Id);
            if (!float.IsFinite(camera.InitialYawDegrees) || camera.InitialYawDegrees is < -360 or > 360)
                Add(result, CameraCode, "Camera initial yaw must be finite and between -360 and 360 degrees.",
                    $"{root}/cameras/{camera.Id}/initialYawDegrees", camera.Id);
            if (!float.IsFinite(camera.MinimumPitchDegrees) || !float.IsFinite(camera.MaximumPitchDegrees) ||
                camera.MinimumPitchDegrees is < -89 or > 89 ||
                camera.MaximumPitchDegrees is < -89 or > 89 ||
                camera.MinimumPitchDegrees >= camera.MaximumPitchDegrees ||
                camera.PitchDegrees < camera.MinimumPitchDegrees ||
                camera.PitchDegrees > camera.MaximumPitchDegrees)
                Add(result, CameraCode,
                    "Camera pitch limits must be finite, ordered, between -89 and 89 degrees, and include the initial pitch.",
                    $"{root}/cameras/{camera.Id}/pitchLimits", camera.Id);
        }
        if (cameras.Profiles.All(item => !string.Equals(item.Key, cameras.DefaultProfileKey, StringComparison.Ordinal)))
            Add(result, CameraDefaultCode, "The default camera key must resolve to a camera profile.",
                $"{root}/cameras/defaultProfileKey", cameras.Id);

        var input = profile.Input;
        var contexts = input.Contexts.Select(Normalize).ToArray();
        var actionKeys = input.Actions.Select(item => Normalize(item.Key)).ToArray();
        if (input.SchemaVersion != 1 || string.IsNullOrWhiteSpace(input.Provenance) || input.Required && input.Actions.Count == 0 ||
            contexts.Any(string.IsNullOrWhiteSpace) || contexts.Distinct(StringComparer.Ordinal).Count() != contexts.Length ||
            actionKeys.Distinct(StringComparer.Ordinal).Count() != actionKeys.Length ||
            input.Actions.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Label) ||
                string.IsNullOrWhiteSpace(item.Context)) ||
            input.Bindings.Any(item => !Enum.IsDefined(item.Device) || string.IsNullOrWhiteSpace(item.Binding) ||
                string.IsNullOrWhiteSpace(item.Glyph)))
            Add(result, InputCode, "Input requires unique contexts/actions and valid device bindings with glyphs.",
                $"{root}/input/{input.Id}", input.Id);
        var contextSet = contexts.ToHashSet(StringComparer.Ordinal);
        var actionSet = actionKeys.ToHashSet(StringComparer.Ordinal);
        if (input.Actions.Any(item => !contextSet.Contains(Normalize(item.Context))) ||
            input.Bindings.Any(item => !actionSet.Contains(Normalize(item.ActionKey))) ||
            input.Actions.Where(item => item.AccessibilityRequired).Any(action =>
                input.Bindings.All(binding => !string.Equals(binding.ActionKey, action.Key, StringComparison.Ordinal))))
            Add(result, InputReferenceCode, "Input action contexts and bindings must resolve; accessible actions require a binding.",
                $"{root}/input/bindings", input.Id);
        if (!actionSet.Contains(Normalize(interaction.InputAction)))
            Add(result, InteractionReferenceCode, "Interaction input action must resolve in the runtime input profile.",
                $"{root}/interaction/inputAction", interaction.Id);

        var save = profile.Save;
        if (save.SchemaVersion != 1 || string.IsNullOrWhiteSpace(save.Provenance) ||
            save.ChunkBytes is < 4096 or > 4_194_304 || save.AutosaveIntervalSeconds is < 0 or > 86_400 ||
            save.RollbackSlots is < 0 or > 100 ||
            !string.Equals(save.ChecksumAlgorithm, "SHA-256", StringComparison.OrdinalIgnoreCase))
            Add(result, SaveCode, "Save policy requires schema v1, SHA-256 and bounded chunk/autosave/rollback values.",
                $"{root}/save/{save.Id}", save.Id);
        return result;
    }

    private static bool Finite(Spatial.Vector3Value value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
    private static void Add(ICollection<DomainDiagnostic> result, string code, string message, string path, Guid id) =>
        result.Add(new(code, $"{message} UUID={id:D}", path));
}
