using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.Core.Tests;

public sealed class PasswordGeneratorServiceTests
{
    [Fact]
    public void Password_generation_uses_every_selected_character_group()
    {
        var service = new PasswordGeneratorService();
        var password = service.GeneratePassword(new PasswordGenerationOptions { Length = 32 });

        Assert.Equal(32, password.Length);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, character => !char.IsLetterOrDigit(character));
        Assert.Equal("Strong", service.EstimatePasswordStrength(password).Rating);
    }

    [Fact]
    public void Passphrase_generation_is_readable_and_reports_entropy()
    {
        var service = new PasswordGeneratorService();
        var passphrase = service.GeneratePassphrase(7);

        Assert.Equal(7, passphrase.Split('-').Length);
        var strength = service.EstimatePassphraseStrength(7);
        Assert.Equal(70, strength.EstimatedEntropyBits);
        Assert.Equal("Good", strength.Rating);
    }

    [Fact]
    public void Generator_rejects_an_empty_character_selection()
    {
        var service = new PasswordGeneratorService();
        Assert.Throws<ArgumentException>(() => service.GeneratePassword(new PasswordGenerationOptions
        {
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSymbols = false
        }));
    }
}
