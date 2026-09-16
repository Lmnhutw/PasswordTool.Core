using System.Security.Cryptography;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class PasswordGeneratorService
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.?";

    private static readonly string[] Adjectives =
    [
        "amber", "brave", "bright", "calm", "clear", "cobalt", "coral", "crisp",
        "daring", "eager", "ember", "fair", "gentle", "golden", "grand", "green",
        "happy", "honest", "ivory", "jolly", "keen", "lively", "lunar", "merry",
        "nimble", "noble", "quiet", "rapid", "silver", "steady", "swift", "vivid"
    ];

    private static readonly string[] Nouns =
    [
        "anchor", "apple", "badger", "beacon", "birch", "breeze", "brook", "cedar",
        "comet", "dawn", "falcon", "fern", "forest", "harbor", "heron", "island",
        "lantern", "maple", "meadow", "moon", "oak", "ocean", "orchid", "otter",
        "pebble", "river", "robin", "summit", "tiger", "valley", "willow", "zephyr"
    ];

    public string GeneratePassword(PasswordGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Length is < 8 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Password length must be between 8 and 128 characters.");
        }

        var selectedSets = new List<string>();
        if (options.IncludeUppercase) selectedSets.Add(Uppercase);
        if (options.IncludeLowercase) selectedSets.Add(Lowercase);
        if (options.IncludeDigits) selectedSets.Add(Digits);
        if (options.IncludeSymbols) selectedSets.Add(Symbols);

        if (selectedSets.Count == 0)
        {
            throw new ArgumentException("Select at least one character group.", nameof(options));
        }

        if (options.Length < selectedSets.Count)
        {
            throw new ArgumentException("Password length is shorter than the selected character groups.", nameof(options));
        }

        var pool = string.Concat(selectedSets);
        var result = new char[options.Length];
        var offset = 0;
        foreach (var set in selectedSets)
        {
            result[offset++] = set[RandomNumberGenerator.GetInt32(set.Length)];
        }

        while (offset < result.Length)
        {
            result[offset++] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }

        for (var index = result.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (result[index], result[swapIndex]) = (result[swapIndex], result[index]);
        }

        return new string(result);
    }

    public string GeneratePassphrase(int chunkCount = 7, string separator = "-")
    {
        if (chunkCount is < 4 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkCount), "Passphrases must contain between 4 and 12 chunks.");
        }

        if (string.IsNullOrEmpty(separator) || separator.Length > 3 || separator.Any(char.IsLetterOrDigit))
        {
            throw new ArgumentException("Use a short, non-alphanumeric passphrase separator.", nameof(separator));
        }

        var chunks = new string[chunkCount];
        for (var index = 0; index < chunks.Length; index++)
        {
            var adjective = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
            var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
            chunks[index] = adjective + noun;
        }

        return string.Join(separator, chunks);
    }

    public PasswordStrength EstimatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrength(0, "Empty");
        }

        var poolSize = 0;
        if (password.Any(char.IsUpper)) poolSize += 26;
        if (password.Any(char.IsLower)) poolSize += 26;
        if (password.Any(char.IsDigit)) poolSize += 10;
        if (password.Any(character => !char.IsLetterOrDigit(character))) poolSize += 32;
        poolSize = Math.Max(poolSize, 1);

        var entropy = password.Length * Math.Log2(poolSize);
        return new PasswordStrength(entropy, GetRating(entropy));
    }

    public PasswordStrength EstimatePassphraseStrength(int chunkCount)
    {
        if (chunkCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkCount));
        }

        var entropy = chunkCount * (Math.Log2(Adjectives.Length) + Math.Log2(Nouns.Length));
        return new PasswordStrength(entropy, GetRating(entropy));
    }

    private static string GetRating(double entropyBits) => entropyBits switch
    {
        < 40 => "Weak",
        < 60 => "Fair",
        < 80 => "Good",
        _ => "Strong"
    };
}
