using System.Text.Json;
using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptJsonAndUiTests
{
    [Fact]
    public void Safe_json_api_validates_schema_quota_permissions_and_revision()
    {
        var service = new ForgeScriptJsonService();
        var snapshot = service.Register(Guid.NewGuid(), "quest-state",
            "{\"type\":\"object\",\"required\":[\"chapter\"]}", "{\"chapter\":1}",
            new SafeJsonQuota(512, 8), true);
        var chapter = JsonSerializer.SerializeToElement(2);
        var applied = service.Set(snapshot.Handle, "/chapter", chapter, 0, "module-author");
        Assert.True(applied.Succeeded);
        Assert.Equal(1, applied.Snapshot.Revision);
        Assert.Equal(2, service.Read(snapshot.Handle, "/chapter").GetInt32());
        Assert.Equal("SCRIPT_JSON_REVISION_CONFLICT",
            service.Set(snapshot.Handle, "/chapter", chapter, 0, "module-author").Code);
        Assert.Equal("SCRIPT_JSON_PERMISSION_DENIED",
            service.Set(snapshot.Handle, "/chapter", chapter, 1, "player").Code);
    }

    [Fact]
    public void Typed_ui_events_enforce_document_context_and_permission()
    {
        var service = new ForgeScriptUiEventService();
        var handle = new ScriptUiHandle(Guid.NewGuid(), "adventure-hud");
        var binding = new ScriptUiBinding(Guid.NewGuid(), handle, "rest-button", "pressed",
            "RequestRest", "1.0", "gameplay", "player");
        service.Register(binding);
        var published = service.Publish(handle, "rest-button", "pressed",
            JsonSerializer.SerializeToElement(new { source = "keyboard" }), "gameplay", "player");
        Assert.Equal("RequestRest", published.Command);
        Assert.Equal(1, published.Sequence);
        Assert.Throws<InvalidOperationException>(() => service.Publish(handle, "rest-button", "pressed",
            JsonSerializer.SerializeToElement(new { }), "menu", "player"));
        Assert.Throws<UnauthorizedAccessException>(() => service.Publish(handle, "rest-button", "pressed",
            JsonSerializer.SerializeToElement(new { }), "gameplay", "admin"));
    }
}
