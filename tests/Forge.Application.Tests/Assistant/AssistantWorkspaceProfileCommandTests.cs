using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assistant;

namespace Forge.Application.Tests.Assistant;

public sealed class AssistantWorkspaceProfileCommandTests
{
    private readonly SetAssistantWorkspaceProfileHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-05T10:00:00Z")));

    [Fact]
    public void DryRunApplyUndoAndRedoUseOneCanonicalProfileCommand()
    {
        var history = new AssistantWorkspaceProfileCommandHistory();
        var updated = WithPromptVersion(history.State.Profile, "1.1.0");
        var planId = Guid.Parse("a1220000-0000-4000-8000-000000000001");
        var preview = _handler.Handle(history.State, Command(history.State.Revision, planId, updated, true));

        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Equal("assistant/profile", preview.Patch?.Path);
        Assert.Equal("1.0.0", history.State.Profile.Prompts[0].Version);

        var applied = _handler.Handle(history.State, Command(history.State.Revision, planId, updated, false));
        history.Record(applied);

        Assert.Equal("1.1.0", history.State.Profile.Prompts[0].Version);
        Assert.True(history.Undo());
        Assert.Equal("1.0.0", history.State.Profile.Prompts[0].Version);
        Assert.True(history.Redo());
        Assert.Equal("1.1.0", history.State.Profile.Prompts[0].Version);
    }

    [Fact]
    public void MissingPlanInsufficientPermissionAndStaleRevisionAreRejected()
    {
        var state = new AssistantWorkspaceProfileState();
        var profile = state.Profile;

        var noPlan = _handler.Handle(state, Command(state.Revision, Guid.Empty, profile, false));
        var notPreviewed = _handler.Handle(state, Command(state.Revision, Guid.NewGuid(), profile, false));
        var denied = _handler.Handle(state, new SetAssistantWorkspaceProfileCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), state.Revision, profile,
            AssistantPermissionLevel.Propose, false));
        var stale = _handler.Handle(state, Command(new AssistantWorkspaceProfileRevision(2), Guid.NewGuid(), profile, false));

        Assert.Equal(SetAssistantWorkspaceProfileHandler.PlanRequiredCode, Assert.Single(noPlan.Diagnostics).Code);
        Assert.Equal(SetAssistantWorkspaceProfileHandler.PlanNotPreviewedCode, Assert.Single(notPreviewed.Diagnostics).Code);
        Assert.Equal(SetAssistantWorkspaceProfileHandler.PermissionDeniedCode, Assert.Single(denied.Diagnostics).Code);
        Assert.Equal(SetAssistantWorkspaceProfileHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
    }

    private static SetAssistantWorkspaceProfileCommand Command(
        AssistantWorkspaceProfileRevision revision,
        Guid planId,
        AssistantWorkspaceProfile profile,
        bool dryRun) =>
        new(CommandId.From(Guid.NewGuid()), planId, revision, profile, AssistantPermissionLevel.Configure, dryRun);

    private static AssistantWorkspaceProfile WithPromptVersion(AssistantWorkspaceProfile profile, string version)
    {
        var source = profile.Prompts[0];
        var prompt = new AssistantPromptDefinition(
            source.Id, source.SchemaVersion, source.Key, version, source.DisplayName, source.SystemTemplate,
            source.UserTemplate, source.ResponseJsonSchema, source.RequiredCapabilities, source.Tests,
            [.. source.Changelog, new AssistantPromptChange(version, "Test version update.")], source.Provenance);
        return new AssistantWorkspaceProfile(profile.Id, profile.SchemaVersion, profile.ActiveProviderId,
            profile.Providers, [prompt], profile.MaximumPermission, profile.NetworkAllowed,
            profile.ShareProjectContent, profile.Provenance);
    }
}
