using Forge.Application.Gameplay;

namespace Forge.Application.Tests.Gameplay;

public sealed class RuntimeStartupVerificationPolicyTests
{
    [Fact]
    public void Interactive_player_session_does_not_run_verification_labs()
    {
        Assert.False(RuntimeStartupVerificationPolicy.ShouldRun(
            automatedTest: false,
            entryGroundSmokeTest: false,
            innUxSmokeTest: false,
            moduleMovementSmokeTest: false,
            combatFeedbackSmokeTest: false,
            combatFeedbackCaptureRequested: false));
    }

    [Fact]
    public void General_automated_smoke_keeps_verification_labs()
    {
        Assert.True(RuntimeStartupVerificationPolicy.ShouldRun(
            automatedTest: true,
            entryGroundSmokeTest: false,
            innUxSmokeTest: false,
            moduleMovementSmokeTest: false,
            combatFeedbackSmokeTest: false,
            combatFeedbackCaptureRequested: false));
    }

    [Fact]
    public void Map_quality_audit_skips_unrelated_verification_labs()
    {
        Assert.False(RuntimeStartupVerificationPolicy.ShouldRun(
            automatedTest: true,
            entryGroundSmokeTest: false,
            innUxSmokeTest: false,
            moduleMovementSmokeTest: false,
            combatFeedbackSmokeTest: false,
            combatFeedbackCaptureRequested: false,
            mapQualityAuditRequested: true));
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Specialized_automated_paths_skip_unrelated_verification_labs(
        bool entryGroundSmokeTest,
        bool innUxSmokeTest,
        bool moduleMovementSmokeTest,
        bool combatFeedbackSmokeTest,
        bool combatFeedbackCaptureRequested)
    {
        Assert.False(RuntimeStartupVerificationPolicy.ShouldRun(
            automatedTest: true,
            entryGroundSmokeTest,
            innUxSmokeTest,
            moduleMovementSmokeTest,
            combatFeedbackSmokeTest,
            combatFeedbackCaptureRequested));
    }
}
