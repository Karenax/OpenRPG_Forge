using Forge.Domain.Entities;

namespace Forge.Domain.Tests.Entities;

public sealed class EntityClickTriggerSettingsTests
{
    [Fact]
    public void Merge_PreservesUnrelatedPropertiesAndRoundTripsMessageTrigger()
    {
        var original = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String, "asset-id")]);
        var settings = new EntityClickTriggerSettings(
            true,
            EntityClickTriggerAction.ShowMessage,
            "Une inscription ancienne.",
            null,
            4.5f);

        var merged = EntityClickTriggerSettings.Merge(original, settings);
        var read = EntityClickTriggerSettings.Read(merged);

        Assert.True(read.Enabled);
        Assert.Equal(EntityClickTriggerAction.ShowMessage, read.Action);
        Assert.Equal("Une inscription ancienne.", read.Message);
        Assert.Equal(4.5f, read.MaximumDistanceMeters);
        Assert.Contains(merged.Properties, property => property.Key == "visual.asset");
    }

    [Fact]
    public void Merge_DisabledRemovesOnlyClickTriggerProperties()
    {
        var enabled = EntityClickTriggerSettings.Merge(
            new EntityPropertyOverrides(
                EntityPropertyOverrides.CurrentSchemaVersion,
                [new EntityPropertyOverride("runtime.displayName", EntityPropertyValueKind.String, "Stèle")]),
            new EntityClickTriggerSettings(
                true,
                EntityClickTriggerAction.StartDialogue,
                null,
                Guid.NewGuid().ToString("D")));

        var disabled = EntityClickTriggerSettings.Merge(
            enabled,
            new EntityClickTriggerSettings(false, EntityClickTriggerAction.ShowMessage, null, null));

        Assert.DoesNotContain(disabled.Properties,
            property => property.Key.StartsWith(EntityClickTriggerSettings.PropertyPrefix, StringComparison.Ordinal));
        Assert.Contains(disabled.Properties, property => property.Key == "runtime.displayName");
    }

    [Fact]
    public void Merge_RejectsEnabledDialogueWithoutStableTarget()
    {
        var settings = new EntityClickTriggerSettings(
            true,
            EntityClickTriggerAction.StartDialogue,
            null,
            "missing-dialogue");

        Assert.Throws<ArgumentException>(() =>
            EntityClickTriggerSettings.Merge(EntityPropertyOverrides.Empty, settings));
    }

    [Fact]
    public void Merge_RoundTripsIndependentDoorToggleWithoutTarget()
    {
        var settings = new EntityClickTriggerSettings(
            true,
            EntityClickTriggerAction.ToggleDoor,
            null,
            null,
            3.5f);

        var merged = EntityClickTriggerSettings.Merge(EntityPropertyOverrides.Empty, settings);
        var read = EntityClickTriggerSettings.Read(merged);

        Assert.True(read.Enabled);
        Assert.Equal(EntityClickTriggerAction.ToggleDoor, read.Action);
        Assert.Equal(3.5f, read.MaximumDistanceMeters);
        Assert.DoesNotContain(merged.Properties,
            property => property.Key == EntityClickTriggerSettings.TargetProperty);
    }
}
