namespace Forge.Domain.Validation;

public static class SpdxLicenseValidator
{
    private static readonly HashSet<string> SupportedLicenseIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "CC0-1.0", "CC-BY-4.0",
        "CC-BY-SA-4.0", "GPL-2.0-only", "GPL-2.0-or-later", "GPL-3.0-only", "GPL-3.0-or-later",
        "LGPL-2.1-only", "LGPL-2.1-or-later", "LGPL-3.0-only", "LGPL-3.0-or-later", "MPL-2.0",
        "OFL-1.1", "ISC", "Zlib", "Unlicense",
    };

    public static bool IsSupported(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parser = new Parser(value.AsSpan());
        return parser.Parse();
    }

    private static bool IsSupportedIdentifier(ReadOnlySpan<char> token)
    {
        var value = token.ToString();
        if (SupportedLicenseIdentifiers.Contains(value)) return true;
        const string prefix = "LicenseRef-";
        return value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length &&
               value.AsSpan(prefix.Length).IndexOfAnyExcept(
                   "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-") < 0;
    }

    private ref struct Parser(ReadOnlySpan<char> input)
    {
        private readonly ReadOnlySpan<char> _input = input;
        private int _offset;

        internal bool Parse()
        {
            SkipWhitespace();
            if (!ParseOr()) return false;
            SkipWhitespace();
            return _offset == _input.Length;
        }

        private bool ParseOr()
        {
            if (!ParseAnd()) return false;
            while (TryOperator("OR"))
                if (!ParseAnd()) return false;
            return true;
        }

        private bool ParseAnd()
        {
            if (!ParsePrimary()) return false;
            while (TryOperator("AND"))
                if (!ParsePrimary()) return false;
            return true;
        }

        private bool ParsePrimary()
        {
            SkipWhitespace();
            if (_offset >= _input.Length) return false;
            if (_input[_offset] == '(')
            {
                _offset++;
                if (!ParseOr()) return false;
                SkipWhitespace();
                if (_offset >= _input.Length || _input[_offset] != ')') return false;
                _offset++;
                return true;
            }

            var start = _offset;
            while (_offset < _input.Length && !char.IsWhiteSpace(_input[_offset]) &&
                   _input[_offset] is not '(' and not ')')
                _offset++;
            return _offset > start && IsSupportedIdentifier(_input[start.._offset]);
        }

        private bool TryOperator(string value)
        {
            var saved = _offset;
            SkipWhitespace();
            if (!_input[_offset..].StartsWith(value, StringComparison.Ordinal))
            {
                _offset = saved;
                return false;
            }
            var end = _offset + value.Length;
            if (end < _input.Length && !char.IsWhiteSpace(_input[end]) && _input[end] != '(')
            {
                _offset = saved;
                return false;
            }
            _offset = end;
            SkipWhitespace();
            return true;
        }

        private void SkipWhitespace()
        {
            while (_offset < _input.Length && char.IsWhiteSpace(_input[_offset])) _offset++;
        }
    }
}
