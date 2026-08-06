using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public static class AssistantPromptTestRunner
{
    public static AssistantPromptTestReport Run(AssistantPromptDefinition prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var failures = new List<string>();
        var failedTests = new HashSet<Guid>();
        foreach (var test in prompt.Tests)
        {
            try
            {
                var rendered = AssistantPromptRenderer.Render(prompt, test.Variables);
                var combined = $"{rendered.SystemMessage}\n{rendered.UserMessage}";
                foreach (var fragment in test.ExpectedFragments.Where(fragment =>
                             !combined.Contains(fragment, StringComparison.Ordinal)))
                {
                    failures.Add($"{test.Name}: missing '{fragment}'");
                    failedTests.Add(test.Id);
                }
            }
            catch (ArgumentException exception)
            {
                failures.Add($"{test.Name}: {exception.Message}");
                failedTests.Add(test.Id);
            }
        }
        return new(failures.Count == 0, prompt.Tests.Count, prompt.Tests.Count - failedTests.Count, failures);
    }
}
