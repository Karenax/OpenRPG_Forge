using Forge.Application.Rules;
using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Rules;

namespace Forge.Application.Tests.Rules;

public sealed class RulesScriptViewServiceTests
{
    [Fact]
    public void Typed_handles_expose_copied_read_only_module_and_area_values()
    {
        var module = ModuleRulesProfile.CreateDefault(ModuleId.From(Guid.NewGuid()));
        var area = AreaRulesProfile.CreateDefault(AreaId.From(Guid.NewGuid()));
        var service = new RulesScriptViewService(module, area);

        var moduleView = service.Resolve(service.ModuleHandle);
        var areaView = service.Resolve(service.AreaHandle);

        Assert.Equal("forge.generic", moduleView.Values["rulesetId"]);
        Assert.Equal("standard", moduleView.Values["activeDifficultyKey"]);
        Assert.Equal("disabled", areaView.Values["pvpMode"]);
        Assert.True((bool)areaView.Values["serverAuthorityRequired"]!);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)moduleView.Values).Add("forbidden", true));
    }

    [Fact]
    public void Unknown_handle_is_rejected()
    {
        var service = new RulesScriptViewService(
            ModuleRulesProfile.CreateDefault(ModuleId.From(Guid.NewGuid())),
            AreaRulesProfile.CreateDefault(AreaId.From(Guid.NewGuid())));

        Assert.Throws<KeyNotFoundException>(() =>
            service.Resolve(new RulesScriptHandle(Guid.NewGuid(), "module-rules", 1)));
    }
}
