using PasswordTool.Core.Hashers;
using PasswordTool.Core.Inspection;
using PasswordTool.Core.Registry;
using PasswordTool.Core.Utilities;

namespace PasswordTool.Core.Tests;

public sealed class HasherBehaviorTests
{
    [Theory]
    [InlineData(PasswordHasherNames.Argon2id)]
    [InlineData(PasswordHasherNames.Bcrypt)]
    [InlineData(PasswordHasherNames.Pbkdf2Sha256)]
    [InlineData(PasswordHasherNames.Pbkdf2Sha512)]
    [InlineData(PasswordHasherNames.Scrypt)]
    [InlineData(PasswordHasherNames.Md5)]
    [InlineData(PasswordHasherNames.Sha1)]
    [InlineData(PasswordHasherNames.Sha256Unsalted)]
    [InlineData(PasswordHasherNames.Sha512Unsalted)]
    [InlineData(PasswordHasherNames.Sha256Salted)]
    [InlineData(PasswordHasherNames.Sha512Salted)]
    public void Hashers_Can_Hash_Verify_Reject_And_Inspect(string algorithmName)
    {
        var registry = new PasswordHasherRegistry();
        var hasher = registry.GetHasher(algorithmName);

        var hash = hasher.HashPassword("correct horse battery staple");
        var info = hasher.InspectHash(hash);

        Assert.True(hasher.VerifyPassword("correct horse battery staple", hash));
        Assert.False(hasher.VerifyPassword("wrong password", hash));
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.False(string.IsNullOrWhiteSpace(info.AlgorithmName));
        Assert.False(string.IsNullOrWhiteSpace(info.Notes));
    }

    [Fact]
    public void Inspector_Returns_Unknown_For_Invalid_Hash()
    {
        var inspector = new PasswordHashInspector();

        var info = inspector.Inspect("not-a-real-stored-hash");

        Assert.Equal("Unknown", info.AlgorithmName);
        Assert.False(info.IsSecureForPasswordStorage);
    }

    [Fact]
    public void Scrypt_Matches_Rfc7914_Test_Vector()
    {
        var actual = ScryptKeyDerivation.DeriveKey(
            "password"u8.ToArray(),
            "NaCl"u8.ToArray(),
            cost: 1024,
            blockSize: 8,
            parallelization: 16,
            outputBytes: 64);

        const string expected =
            "FDBABE1C9D3472007856E7190D01E9FE" +
            "7C6AD7CBC8237830E77376634B373162" +
            "2EAF30D92E22A3886FF109279D9830DA" +
            "C727AFB94A83EE6D8360CBDFA2CC0640";

        Assert.Equal(expected, Convert.ToHexString(actual));
    }
}
