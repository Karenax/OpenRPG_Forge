namespace Forge.Application.Gameplay;

public static class RuntimeStartupVerificationPolicy
{
    public static bool ShouldRun(
        bool automatedTest,
        bool entryGroundSmokeTest,
        bool innUxSmokeTest,
        bool cryptMovementSmokeTest,
        bool combatFeedbackSmokeTest,
        bool combatFeedbackCaptureRequested) =>
        automatedTest &&
        !entryGroundSmokeTest &&
        !innUxSmokeTest &&
        !cryptMovementSmokeTest &&
        !combatFeedbackSmokeTest &&
        !combatFeedbackCaptureRequested;
}
