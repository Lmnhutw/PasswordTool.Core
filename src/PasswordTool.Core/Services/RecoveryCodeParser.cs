using System.Text.RegularExpressions;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public static partial class RecoveryCodeParser
{
    public static RecoveryCodeImportResult Parse(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return Invalid("The clipboard does not contain recovery codes.");
        }

        var candidates = clipboardText
            .Split(['\r', '\n', '\t', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(RemoveListMarker)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (candidates.Count < 2)
        {
            return Invalid("Paste at least two recovery codes, preferably one code per line.");
        }

        if (candidates.Any(code => !CodePattern().IsMatch(code)))
        {
            return Invalid("Each recovery code must be 4-128 characters and contain only letters, numbers, spaces, or hyphens.");
        }

        if (candidates.Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            return Invalid("The pasted recovery-code list contains duplicates.");
        }

        return new RecoveryCodeImportResult(candidates, null);
    }

    private static RecoveryCodeImportResult Invalid(string message) => new([], message);

    private static string RemoveListMarker(string value)
    {
        var match = ListMarkerPattern().Match(value);
        return match.Success ? match.Groups[1].Value.Trim() : value.Trim();
    }

    [GeneratedRegex("^(?:[-*]|\\d+[.)])\\s+(.+)$")]
    private static partial Regex ListMarkerPattern();

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9 -]{2,126}[A-Za-z0-9])?$")]
    private static partial Regex CodePattern();
}
