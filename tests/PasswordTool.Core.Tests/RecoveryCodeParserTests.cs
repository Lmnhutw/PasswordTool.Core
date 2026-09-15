using PasswordTool.Core.Services;

namespace PasswordTool.Core.Tests;

public sealed class RecoveryCodeParserTests
{
    [Fact]
    public void Parse_normalizes_numbered_lines_without_changing_code_values()
    {
        const string clipboardText = "1. abcd-1234\r\n2. 5678 9012\r\n- XyZ9-8765";

        var result = RecoveryCodeParser.Parse(clipboardText);

        Assert.True(result.IsValid);
        Assert.Equal(["abcd-1234", "5678 9012", "XyZ9-8765"], result.Codes);
    }

    [Fact]
    public void Parse_supports_comma_separated_codes()
    {
        var result = RecoveryCodeParser.Parse("abcd-1234, efgh-5678; ijkl-9012");

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Codes.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcd-1234")]
    [InlineData("abcd-1234\nabcd-1234")]
    [InlineData("abcd-1234\ninvalid/code")]
    public void Parse_rejects_incomplete_duplicate_or_malformed_lists(string input)
    {
        var result = RecoveryCodeParser.Parse(input);

        Assert.False(result.IsValid);
        Assert.Empty(result.Codes);
        Assert.NotEmpty(result.ErrorMessage!);
    }
}
