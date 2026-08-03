using System.Text;

namespace Forge.Application.Localization;

public static class PseudoLocalization
{
    private static readonly IReadOnlyDictionary<char, char> Accents = new Dictionary<char, char>
    {
        ['a'] = 'à',
        ['e'] = 'ë',
        ['i'] = 'ï',
        ['o'] = 'ô',
        ['u'] = 'ü',
        ['A'] = 'À',
        ['E'] = 'Ë',
        ['I'] = 'Ï',
        ['O'] = 'Ô',
        ['U'] = 'Ü',
    };

    public static string Transform(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new StringBuilder("⟦");
        var insidePlaceholder = false;
        foreach (var character in value)
        {
            if (character == '{') insidePlaceholder = true;
            result.Append(insidePlaceholder || !Accents.TryGetValue(character, out var accented) ? character : accented);
            if (character == '}') insidePlaceholder = false;
        }

        result.Append(" ···⟧");
        return result.ToString();
    }
}
