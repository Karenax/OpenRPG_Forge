using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public readonly record struct GameplayFoundationHandle(Guid Id);
public readonly record struct GameplayCameraHandle(Guid Id);
public readonly record struct GameplayInputActionHandle(Guid Id);

public sealed class GameplayFoundationScriptService
{
    private readonly GameplayFoundationProfile _profile;

    public GameplayFoundationScriptService(ModuleGameplayControlProfile gameplay) =>
        _profile = (gameplay ?? throw new ArgumentNullException(nameof(gameplay))).Foundation;

    public GameplayFoundationHandle Get() => new(_profile.Id);

    public GameplayCameraHandle? FindCamera(string key) => _profile.Cameras.Profiles
        .Where(item => string.Equals(item.Key, key, StringComparison.Ordinal))
        .Select(item => (GameplayCameraHandle?)new GameplayCameraHandle(item.Id)).SingleOrDefault();

    public GameplayInputActionHandle? FindInputAction(string key) => _profile.Input.Actions
        .Where(item => string.Equals(item.Key, key, StringComparison.Ordinal))
        .Select(item => (GameplayInputActionHandle?)new GameplayInputActionHandle(item.Id)).SingleOrDefault();

    public GameplayCameraProfile Read(GameplayCameraHandle handle) => _profile.Cameras.Profiles
        .Single(item => item.Id == handle.Id);

    public RuntimeInputActionDefinition Read(GameplayInputActionHandle handle) => _profile.Input.Actions
        .Single(item => item.Id == handle.Id);
}
