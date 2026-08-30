namespace Forge.Application.Gameplay;

public static class RuntimeStartupVerificationPolicy
{
    public static bool ShouldRun(
        bool automatedTest,
        bool entryGroundSmokeTest,
        bool innUxSmokeTest,
        bool moduleMovementSmokeTest,
        bool combatFeedbackSmokeTest,
        bool combatFeedbackCaptureRequested,
        bool mapQualityAuditRequested = false) =>
        automatedTest &&
        !entryGroundSmokeTest &&
        !innUxSmokeTest &&
        !moduleMovementSmokeTest &&
        !combatFeedbackSmokeTest &&
        !combatFeedbackCaptureRequested &&
        !mapQualityAuditRequested;
}
