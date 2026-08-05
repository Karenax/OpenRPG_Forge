using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Domain.Tests.RuntimeExtensions;

public sealed class ModuleRuntimeExtensionProfileTests
{
    [Fact]
    public void Profile_canonicalizes_documents_and_resolves_ui_references()
    {
        var module = ModuleId.From(Guid.NewGuid());
        var source = ModuleRuntimeExtensionProfile.CreateDefault(module);
        var label = new ScriptableUiWidget(Guid.NewGuid(), "label", ScriptableUiWidgetKind.Label, null,
            "Chapter", "state:/chapter", false, 1, "player");
        var panel = new ScriptableUiWidget(Guid.NewGuid(), "panel", ScriptableUiWidgetKind.Panel, null,
            string.Empty, null, false, 0, "player");
        var ui = new ScriptableUiDocument(Guid.NewGuid(), 1, "HUD", "HUD", "Forge-Dark", false,
            [label, panel], [], "test");
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1,
            [new DataDrivenRuleSchema(Guid.NewGuid(), 1, "Weapon.Sword", RuleSchemaKind.Weapon,
                "{ \"type\": \"object\" }", "{ \"damage\": 8 }", false, "test")],
            [ui], [], source.SnapshotPolicy, "test");

        Assert.Equal("weapon.sword", profile.RuleSchemas[0].Key);
        Assert.Equal("hud", profile.UiDocuments[0].Key);
        Assert.Equal("panel", profile.UiDocuments[0].Widgets[0].Key);
        Assert.Equal("state:/chapter", profile.UiDocuments[0].Widgets[1].BindingKey);
    }

    [Fact]
    public void Profile_rejects_duplicate_ids_unresolved_widgets_and_unsafe_quotas()
    {
        var id = Guid.NewGuid();
        var first = new DataDrivenRuleSchema(id, 1, "class.one", RuleSchemaKind.Class, "{}", "{}", false, "test");
        var second = new DataDrivenRuleSchema(id, 1, "class.two", RuleSchemaKind.Class, "{}", "{}", false, "test");
        var source = ModuleRuntimeExtensionProfile.CreateDefault(ModuleId.From(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new ModuleRuntimeExtensionProfile(source.Id, 1, [first, second],
            [], [], source.SnapshotPolicy, "test"));
        var orphan = new ScriptableUiWidget(Guid.NewGuid(), "child", ScriptableUiWidgetKind.Label, "missing",
            "Child", null, false, 0, "player");
        Assert.Throws<ArgumentException>(() => new ScriptableUiDocument(Guid.NewGuid(), 1, "bad", "Bad",
            "forge-dark", false, [orphan], [], "test"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeJsonDocumentDefinition(Guid.NewGuid(), 1,
            "huge", "{}", "{}", 16, 8, true, "1.0", "test"));
    }

    [Fact]
    public void Modern_tooling_defaults_cover_copy_aoe_nui_compiler_and_generated_docs()
    {
        var profile = ModuleRuntimeExtensionProfile.CreateDefault(ModuleId.From(Guid.NewGuid())).ModernTooling;

        Assert.True(profile.RuntimeCopy.RequireServerAuthority);
        Assert.Contains("inventory", profile.RuntimeCopy.StateComponents);
        Assert.Equal(Enum.GetValues<AreaEffectShape>(), profile.AreaEffectIndicator.Shapes);
        Assert.Equal(TargetIndicatorColorMode.Deuteranopia, profile.AreaEffectIndicator.ColorMode);
        Assert.Equal("2.0", profile.UiToolkit.ToolkitVersion);
        Assert.True(profile.UiToolkit.Accessibility);
        Assert.True(profile.Compiler.Incremental);
        Assert.Equal(6, profile.Documentation.Catalogs.Count);
        Assert.True(profile.QualityAssurance.Accessibility.EditorKeyboardOnly);
        Assert.True(profile.QualityAssurance.Accessibility.RuntimeSubtitles);
        Assert.Equal(RuntimeColorVisionMode.Deuteranopia,
            profile.QualityAssurance.Accessibility.RuntimeColorVisionMode);
        Assert.True(SecurityFuzzProfile.SupportedBoundaries.SetEquals(
            profile.QualityAssurance.SecurityFuzz.Boundaries));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccessibilityQualityProfile(Guid.NewGuid(), 1,
            true, true, true, true, 2, 1, true, true, 1, true, RuntimeColorVisionMode.Standard,
            true, false, true, "test"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SecurityFuzzProfile(Guid.NewGuid(), 1, 1,
            1, 64, 100, ["unknown-boundary"], true, true, "test"));
        Assert.Throws<ArgumentException>(() => new GeneratedDocumentationProfile(Guid.NewGuid(), 1,
            "../outside", ["features"], true, "test"));
    }
}
