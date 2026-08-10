using Forge.Application.Commands;
using Forge.Application.Quality;
using Forge.Application.Tests.Entities;
using Forge.Domain.Quality;

namespace Forge.Application.Tests.Quality;

public sealed class QualityFoundationCommandTests
{
    private readonly SetQualityFoundationHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoUseOneRevisionedContract()
    {
        var original = QualityFoundationProfile.CreateDefault(Guid.NewGuid());
        var updated = original with { Provenance = "openrpg-forge:test-policy" };
        var history = new QualityFoundationCommandHistory(original);

        var preview = _handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, updated, true));

        Assert.True(preview.Succeeded);
        Assert.Equal(original, history.State.Profile);
        Assert.Null(preview.NewRevision);
        Assert.Equal("replace", preview.Patch!.Operation);

        var applied = _handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, updated, false));
        history.Record(applied);

        Assert.Equal(updated, history.State.Profile);
        Assert.True(history.Undo());
        Assert.Equal(original, history.State.Profile);
        Assert.True(history.Redo());
        Assert.Equal(updated, history.State.Profile);
    }

    [Fact]
    public void InvalidAndStaleCommandsCannotMutateState()
    {
        var profile = QualityFoundationProfile.CreateDefault(Guid.NewGuid());
        var state = new QualityFoundationState(profile, default);
        var invalid = profile with { Saves = profile.Saves with { PartialFailure = false } };

        var rejected = _handler.Handle(state,
            new(CommandId.From(Guid.NewGuid()), state.Revision, invalid, false));
        var stale = _handler.Handle(state,
            new(CommandId.From(Guid.NewGuid()), new QualityFoundationRevision(7), profile, false));

        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Diagnostics,
            item => item.Code == QualityFoundationProfileValidator.SaveCoverageInvalidCode);
        Assert.Equal(SetQualityFoundationHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
        Assert.Equal(profile, state.Profile);
    }

    [Fact]
    public void ValidationPolicyUsesTheSameDryRunApplyUndoRedoContract()
    {
        var original = QualityFoundationProfile.CreateDefault(Guid.NewGuid());
        var updated = original with
        {
            Validation = original.EffectiveValidation with { Provenance = "test:r027" },
        };
        var history = new QualityFoundationCommandHistory(original);

        Assert.True(_handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, updated, true)).Succeeded);
        history.Record(_handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, updated, false)));
        Assert.Equal("test:r027", history.State.Profile.EffectiveValidation.Provenance);
        Assert.True(history.Undo());
        Assert.Equal(original.EffectiveValidation.Provenance, history.State.Profile.EffectiveValidation.Provenance);
        Assert.True(history.Redo());
        Assert.Equal("test:r027", history.State.Profile.EffectiveValidation.Provenance);
    }
}
