namespace PasswordTool.Core.Hashers.Identity;

public sealed class AspNetCoreIdentityPasswordHasherAdapter : NotImplementedPasswordHasher
{
    public override string AlgorithmName => PasswordHasherNames.AspNetCoreIdentity;
}
