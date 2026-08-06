using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;

namespace Forge.Application.Tests.Assets;

public sealed class ExternalWorkerSecurityPolicyCommandTests
{
    private readonly SetExternalWorkerSecurityPolicyHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-05T15:00:00Z")));

    [Fact]
    public void DryRunApplyUndoAndRedoUseOneCanonicalPolicyCommand()
    {
        var history = new ExternalWorkerSecurityPolicyCommandHistory();
        var changed = history.State.Policy with { MaximumJobSeconds = 180, Provenance = "author:g12-l-test" };
        var planId = Guid.Parse("a1230000-0000-4000-8000-000000000010");

        var preview = _handler.Handle(history.State, Command(history.State.Revision, planId, changed, true));
        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Equal("assets/worker-security", preview.Patch?.Path);

        var applied = _handler.Handle(history.State, Command(history.State.Revision, planId, changed, false));
        history.Record(applied);

        Assert.Equal(180, history.State.Policy.MaximumJobSeconds);
        Assert.True(history.Undo());
        Assert.Equal(300, history.State.Policy.MaximumJobSeconds);
        Assert.True(history.Redo());
        Assert.Equal(180, history.State.Policy.MaximumJobSeconds);
    }

    [Fact]
    public void ApplyRequiresPlanDryRunPermissionRevisionAndSafePolicy()
    {
        var state = new ExternalWorkerSecurityPolicyState();
        var policy = state.Policy;
        var noPlan = _handler.Handle(state, Command(state.Revision, Guid.Empty, policy, false));
        var noPreview = _handler.Handle(state, Command(state.Revision, Guid.NewGuid(), policy, false));
        var denied = _handler.Handle(state, new SetExternalWorkerSecurityPolicyCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), state.Revision, policy,
            AssistantPermissionLevel.Propose, true));
        var stale = _handler.Handle(state, Command(new ExternalWorkerSecurityPolicyRevision(2), Guid.NewGuid(), policy, true));
        var unsafeResult = _handler.Handle(state, Command(state.Revision, Guid.NewGuid(),
            policy with { DenyShell = false }, true));

        Assert.Equal(SetExternalWorkerSecurityPolicyHandler.PlanRequiredCode, Assert.Single(noPlan.Diagnostics).Code);
        Assert.Equal(SetExternalWorkerSecurityPolicyHandler.PlanNotPreviewedCode, Assert.Single(noPreview.Diagnostics).Code);
        Assert.Equal(SetExternalWorkerSecurityPolicyHandler.PermissionDeniedCode, Assert.Single(denied.Diagnostics).Code);
        Assert.Equal(SetExternalWorkerSecurityPolicyHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
        Assert.Contains(unsafeResult.Diagnostics,
            item => item.Code == ExternalWorkerSecurityPolicyValidator.UnsafeExecutionCode && item.Message.Contains("/execution", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyMustMatchThePreviewedSemanticFingerprint()
    {
        var state = new ExternalWorkerSecurityPolicyState();
        var planId = Guid.NewGuid();
        var preview = _handler.Handle(state, Command(state.Revision, planId,
            state.Policy with { MaximumJobSeconds = 120 }, true));
        var tampered = _handler.Handle(state, Command(state.Revision, planId,
            state.Policy with { MaximumJobSeconds = 121 }, false));

        Assert.True(preview.Succeeded);
        Assert.Equal(SetExternalWorkerSecurityPolicyHandler.PlanNotPreviewedCode, Assert.Single(tampered.Diagnostics).Code);
    }

    private static SetExternalWorkerSecurityPolicyCommand Command(
        ExternalWorkerSecurityPolicyRevision revision,
        Guid planId,
        ExternalWorkerSecurityPolicy policy,
        bool dryRun) =>
        new(CommandId.From(Guid.NewGuid()), planId, revision, policy,
            AssistantPermissionLevel.Configure, dryRun);
}
