using Forge.Application.RuntimeExtensions;
using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Tests.RuntimeExtensions;

public sealed class RuntimeExtensionRuntimeServiceTests
{
    [Fact]
    public void Optional_extensions_are_ignored_and_required_consumers_receive_localized_diagnostics()
    {
        var profile = ModuleRuntimeExtensionProfile.CreateDefault(ModuleId.From(Guid.NewGuid()));

        var optional = RuntimeExtensionRuntimeService.GetRule(profile, "class.missing", false, out var optionalDiagnostic);
        var required = RuntimeExtensionRuntimeService.GetRule(profile, "class.missing", true, out var requiredDiagnostic);

        Assert.Null(optional);
        Assert.Null(optionalDiagnostic);
        Assert.Null(required);
        Assert.Equal(RuntimeExtensionRuntimeService.RequiredRuleMissingCode, requiredDiagnostic!.Code);
        Assert.Contains("class.missing", requiredDiagnostic.Path);
    }

    [Fact]
    public void Rule_ui_and_json_handles_expose_typed_read_only_runtime_views()
    {
        var moduleId = ModuleId.From(Guid.NewGuid());
        var source = ModuleRuntimeExtensionProfile.CreateDefault(moduleId);
        var rule = new DataDrivenRuleSchema(Guid.NewGuid(), 1, "weapon.sword", RuleSchemaKind.Weapon,
            "{\"type\":\"object\"}", "{\"damage\":8}", true, "test");
        var widget = new ScriptableUiWidget(Guid.NewGuid(), "attack", ScriptableUiWidgetKind.Button, null,
            "Attack", null, true, 0, "player");
        var binding = new ScriptableUiEventBinding(Guid.NewGuid(), "attack", ScriptableUiEventKind.Pressed,
            "Attack", "1.0", "gameplay", "player");
        var ui = new ScriptableUiDocument(Guid.NewGuid(), 1, "hud", "HUD", "forge-dark", true,
            [widget], [binding], "test");
        var json = new RuntimeJsonDocumentDefinition(Guid.NewGuid(), 1, "state", "{\"type\":\"object\"}",
            "{}", 1024, 8, true, "1.0", "test");
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1, [rule], [ui], [json],
            source.SnapshotPolicy, "test");

        var ruleHandle = RuntimeExtensionRuntimeService.GetRule(profile, "weapon.sword", true, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.Equal(8, ruleHandle!.Defaults.GetProperty("damage").GetInt32());
        Assert.Equal("Attack", Assert.Single(RuntimeExtensionRuntimeService.GetUiDocuments(profile)).Commands.Single());
        Assert.Equal(1024, Assert.Single(RuntimeExtensionRuntimeService.GetJsonDocuments(profile)).MaxBytes);
        Assert.Empty(RuntimeExtensionRuntimeService.ValidateRequired(profile));
    }
}
