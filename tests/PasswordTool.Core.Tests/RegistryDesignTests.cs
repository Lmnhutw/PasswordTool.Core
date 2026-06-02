using PasswordTool.Core.Hashers;
using PasswordTool.Core.Models;
using PasswordTool.Core.Registry;

namespace PasswordTool.Core.Tests;

public sealed class RegistryDesignTests
{
    [Fact]
    public void Argon2id_Is_The_Default_Recommendation()
    {
        var registry = new PasswordHasherRegistry();

        var defaultHasher = registry.GetAvailableHashers().Single(x => x.IsDefaultRecommendation);

        Assert.Equal(PasswordHasherNames.Argon2id, defaultHasher.AlgorithmName);
        Assert.Equal(PasswordHasherSecurityCategory.ProductionSafe, defaultHasher.SecurityCategory);
    }

    [Fact]
    public void Educational_Hashers_Are_Not_Default_Recommendations()
    {
        var registry = new PasswordHasherRegistry();

        var educationalHashers = registry
            .GetAvailableHashers()
            .Where(x => x.SecurityCategory == PasswordHasherSecurityCategory.EducationalOnly);

        Assert.All(educationalHashers, x => Assert.False(x.IsDefaultRecommendation));
    }
}
