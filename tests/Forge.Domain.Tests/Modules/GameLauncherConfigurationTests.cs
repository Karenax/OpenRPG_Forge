using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Modules;

public sealed class GameLauncherConfigurationTests
{
    [Fact]
    public void CreateDefault_IsStableAndUsesTheGameDisplayName()
    {
        var moduleId = ModuleId.From(Guid.Parse("b9f40000-0000-4000-8000-000000000001"));

        var first = GameLauncherConfiguration.CreateDefault(moduleId, "Échos : des Cendres");
        var second = GameLauncherConfiguration.CreateDefault(moduleId, "Échos : des Cendres");

        Assert.Equal(first.Id, second.Id);
        Assert.False(first.Enabled);
        Assert.Equal("Échos : des Cendres", first.DisplayName);
        Assert.Equal("Echos_des_Cendres", first.ExecutableName);
        Assert.Equal(GameLauncherMultiplayerMode.LocalHost, first.Multiplayer);
        Assert.Equal(5121, first.DefaultServerPort);
    }

    [Fact]
    public void Constructor_NormalizesSafeBrandingValues()
    {
        var configuration = Create(
            executableName: "Cendres",
            accentColor: "#a36b42",
            backgroundImagePath: " branding/hero.JPG ",
            newsUrl: " https://example.com/news ");

        Assert.Equal("#A36B42", configuration.AccentColor);
        Assert.Equal("branding/hero.JPG", configuration.BackgroundImagePath);
        Assert.Equal("https://example.com/news", configuration.NewsUrl);
    }

    [Theory]
    [InlineData("Cendres.exe", null, null, 5121)]
    [InlineData("CON", null, null, 5121)]
    [InlineData("Cendres", "../hero.png", null, 5121)]
    [InlineData("Cendres", "hero.svg", null, 5121)]
    [InlineData("Cendres", null, "file:///secret", 5121)]
    [InlineData("Cendres", null, null, 0)]
    public void Constructor_RejectsUnsafeOrUnsupportedValues(
        string executableName,
        string? backgroundImagePath,
        string? newsUrl,
        int port)
    {
        Assert.ThrowsAny<ArgumentException>(() => Create(
            executableName,
            "#A36B42",
            backgroundImagePath,
            newsUrl,
            port));
    }

    [Fact]
    public void Constructor_RejectsUnknownMultiplayerMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameLauncherConfiguration(
            GameLauncherConfigurationId.From(Guid.Parse("b9f40000-0000-4000-8000-000000000003")),
            enabled: true,
            "La Crypte",
            "Le feu attend sous la pierre.",
            "Cendres",
            "#A36B42",
            backgroundImagePath: null,
            newsUrl: null,
            supportUrl: null,
            settingsEnabled: true,
            (GameLauncherMultiplayerMode)999,
            5121,
            "test"));
    }

    private static GameLauncherConfiguration Create(
        string executableName,
        string accentColor,
        string? backgroundImagePath,
        string? newsUrl,
        int port = 5121) =>
        new(
            GameLauncherConfigurationId.From(Guid.Parse("b9f40000-0000-4000-8000-000000000002")),
            enabled: true,
            "La Crypte",
            "Le feu attend sous la pierre.",
            executableName,
            accentColor,
            backgroundImagePath,
            newsUrl,
            "https://example.com/support",
            settingsEnabled: true,
            GameLauncherMultiplayerMode.LocalHost,
            port,
            "test");
}
