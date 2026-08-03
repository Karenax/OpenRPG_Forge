namespace Forge.Domain.Navigation;

public enum NavigationPathFailure
{
    None,
    StartBlocked,
    GoalBlocked,
    Unreachable,
    SearchBudgetExceeded,
}
