using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptInteropProfileTests
{
    [Fact]
    public void Profile_validates_stable_identity_schema_api_binding_and_quotas()
    {
        var valid = ForgeScriptInteropProfile.Default;
        var invalid = valid with { Id = Guid.Empty, ApiVersion = "99.0", MaximumSourceBytes = 1 };

        Assert.Empty(ForgeScriptInteropProfileValidator.Validate(valid));
        var diagnostics = ForgeScriptInteropProfileValidator.Validate(invalid);
        Assert.Contains(diagnostics, item => item.Code == "script.interop.uuid_required" && item.Path == "script/interop/id");
        Assert.Contains(diagnostics, item => item.Code == "script.interop.api_unsupported");
        Assert.Contains(diagnostics, item => item.Code == "script.interop.source_quota_invalid");
    }

    [Fact]
    public void Profile_preview_apply_reject_undo_redo_and_store_round_trip_are_canonical()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var initial = ForgeScriptInteropProfile.Default;
            var changed = initial with { Provenance = "test:r101", MaximumSources = 1024 };
            var history = new ForgeScriptInteropHistory(initial);
            var preview = history.Preview(new(Guid.NewGuid(), 0, changed, "preview"));
            var applied = history.Apply(new(Guid.NewGuid(), 0, changed, "apply"));
            var rejected = history.Preview(new(Guid.NewGuid(), history.Revision,
                changed with { EventBinding = "" }, "reject"));
            var undone = history.Undo();
            var redone = history.Redo();
            var store = new ForgeScriptInteropProfileStore(root);
            store.Save(redone.Profile);

            Assert.True(preview.Succeeded);
            Assert.True(applied.Succeeded);
            Assert.False(rejected.Succeeded);
            Assert.Equal(initial, undone.Profile);
            Assert.Equal(changed, redone.Profile);
            Assert.Equal(changed, store.Load());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
