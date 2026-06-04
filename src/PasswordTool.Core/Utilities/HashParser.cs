namespace PasswordTool.Core.Utilities;

public static class HashParser
{
    public static bool TryParseKeyValueFormat(
        string storedHash,
        out string algorithmName,
        out Dictionary<string, string> values)
    {
        algorithmName = string.Empty;
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        algorithmName = parts[0];

        for (var i = 1; i < parts.Length; i++)
        {
            var separatorIndex = parts[i].IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == parts[i].Length - 1)
            {
                return false;
            }

            var key = parts[i][..separatorIndex];
            var value = parts[i][(separatorIndex + 1)..];
            values[key] = value;
        }

        return true;
    }

    public static bool TryGetInt(Dictionary<string, string> values, string key, out int value)
    {
        value = 0;
        return values.TryGetValue(key, out var rawValue)
            && int.TryParse(rawValue, out value);
    }

    public static bool TryGetBase64(Dictionary<string, string> values, string key, out byte[] bytes)
    {
        bytes = [];

        if (!values.TryGetValue(key, out var rawValue))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(rawValue);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

}
