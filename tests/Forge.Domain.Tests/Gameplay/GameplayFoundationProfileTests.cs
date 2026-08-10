using Forge.Domain.Gameplay;
using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Gameplay;

public sealed class GameplayFoundationProfileTests
{
    private static readonly ModuleId ModuleId =
        Forge.Domain.Modules.ModuleId.From(Guid.Parse("c7100000-0000-4000-8000-000000000100"));

    [Fact]
    public void Defaults_are_stable_complete_and_valid()
    {
        var first = GameplayFoundationProfile.CreateDefault(ModuleId);
        var second = GameplayFoundationProfile.CreateDefault(ModuleId);

        Assert.Empty(GameplayFoundationValidator.Validate(first));
        Assert.Equal(first.AllIds(), second.AllIds());
        Assert.Equal(Enum.GetValues<GameplayCameraMode>(), first.Cameras.Profiles.Select(item => item.Mode));
        Assert.True(first.GroundedMovement.NetworkPrediction);
        Assert.Contains(first.Interaction.Verbs, item => item.Key == "interact");
        Assert.Contains(first.Input.Bindings, item => item.Device == RuntimeInputDevice.Gamepad);
        Assert.Equal("SHA-256", first.Save.ChecksumAlgorithm);
    }

    [Fact]
    public void Validator_localizes_movement_camera_input_interaction_and_save_failures()
    {
        var source = GameplayFoundationProfile.CreateDefault(ModuleId);
        var invalid = source with
        {
            GroundedMovement = source.GroundedMovement with { RunSpeed = -1 },
            Interaction = source.Interaction with { InputAction = "missing", Verbs = [] },
            Cameras = source.Cameras with { DefaultProfileKey = "missing" },
            Input = source.Input with { Bindings = [] },
            Save = source.Save with { ChecksumAlgorithm = "MD5" },
        };

        var diagnostics = GameplayFoundationValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == GameplayFoundationValidator.MovementCode && item.Path.Contains("groundedMovement"));
        Assert.Contains(diagnostics, item => item.Code == GameplayFoundationValidator.InteractionCode && item.Path.Contains("interaction"));
        Assert.Contains(diagnostics, item => item.Code == GameplayFoundationValidator.InteractionReferenceCode);
        Assert.Contains(diagnostics, item => item.Code == GameplayFoundationValidator.CameraDefaultCode);
        Assert.Contains(diagnostics, item => item.Code == GameplayFoundationValidator.InputReferenceCode);
        Assert.Contains(diagnostics, item => item.Code == GameplayFoundationValidator.SaveCode && item.Message.Contains(source.Save.Id.ToString("D")));
    }

    [Fact]
    public void Validator_reports_duplicate_nested_uuid_without_throwing()
    {
        var source = GameplayFoundationProfile.CreateDefault(ModuleId);
        var duplicate = source with { Save = source.Save with { Id = source.Input.Id } };

        var diagnostics = GameplayFoundationValidator.Validate(duplicate);

        Assert.Contains(diagnostics, item => item.Code == GameplayFoundationValidator.DuplicateCode);
    }
}
