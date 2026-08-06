using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public static partial class AssistantPromptRenderer
{
    private static readonly Regex Placeholder = PlaceholderRegex();

    public static AssistantPromptRenderResult Render(
        AssistantPromptDefinition prompt,
        IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(variables);
        string RenderTemplate(string template) => Placeholder.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!variables.TryGetValue(key, out var value))
            {
                throw new ArgumentException($"Prompt variable '{key}' is missing.", nameof(variables));
            }
            return value;
        });

        var system = RenderTemplate(prompt.SystemTemplate);
        var user = RenderTemplate(prompt.UserTemplate);
        var content = string.Join('\n', prompt.Key, prompt.Version, system, user, prompt.ResponseJsonSchema);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        return new(prompt.Key, prompt.Version, system, user, prompt.ResponseJsonSchema, hash);
    }

    [GeneratedRegex("\\{\\{([a-z0-9._-]+)\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
