using Forge.Domain.Localization;
using Forge.Domain.Projects;

namespace Forge.Application.Localization;

public static class LocalizationCatalogExtractor
{
    public static LocalizationExtractionResult Extract(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var existing = project.Module.Localization;
        var messages = new Dictionary<string, LocalizationMessage>(StringComparer.Ordinal);
        var diagnostics = new List<string>();

        void Add(string key, string fallback, IReadOnlyDictionary<string, string>? translations, string provenance)
        {
            if (messages.TryGetValue(key, out var previous))
            {
                if (!string.Equals(previous.Fallback, fallback, StringComparison.Ordinal))
                {
                    diagnostics.Add($"localization.key_conflict:{key}");
                }

                return;
            }

            var preserved = existing.Messages.SingleOrDefault(message => message.Key == key)?.Translations;
            messages.Add(key, new LocalizationMessage(key, fallback, preserved ?? translations, provenance));
        }

        var metadata = project.Module.Metadata;
        var defaultMetadata = metadata.GetDefaultLocalization();
        static string TextOrPlaceholder(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
        Add("module.name", TextOrPlaceholder(defaultMetadata.Name), metadata.Localizations.ToDictionary(item => item.Locale, item => TextOrPlaceholder(item.Name)), "module.metadata");
        Add("module.summary", TextOrPlaceholder(defaultMetadata.Summary), metadata.Localizations.ToDictionary(item => item.Locale, item => TextOrPlaceholder(item.Summary)), "module.metadata");
        Add("module.description", TextOrPlaceholder(defaultMetadata.Description), metadata.Localizations.ToDictionary(item => item.Locale, item => TextOrPlaceholder(item.Description)), "module.metadata");
        Add("module.credits", TextOrPlaceholder(defaultMetadata.Credits),
            metadata.Localizations.ToDictionary(item => item.Locale, item => TextOrPlaceholder(item.Credits)), "module.metadata");

        foreach (var dialogue in project.Dialogues)
        {
            foreach (var node in dialogue.Nodes)
            {
                Add(node.Text.Key, node.Text.Fallback, null, $"dialogue:{dialogue.Id}");
                foreach (var choice in node.Choices)
                {
                    Add(choice.Text.Key, choice.Text.Fallback, null, $"dialogue:{dialogue.Id}");
                }
            }
        }

        foreach (var quest in project.Quests)
        {
            foreach (var state in quest.States)
            {
                Add(state.Journal.LocalizationKey, state.Journal.Fallback, null, $"quest:{quest.Id}");
            }
        }

        var locales = metadata.Localizations.Select(item => item.Locale).Concat(existing.Locales).ToArray();
        return new LocalizationExtractionResult(
            new LocalizationCatalog(existing.Id, metadata.DefaultLocale, locales, messages.Values.ToArray(),
                existing.SchemaVersion, existing.PackageNamespace, existing.LicenseSpdx, existing.Provenance,
                existing.PlatformByteBudget),
            diagnostics);
    }
}
