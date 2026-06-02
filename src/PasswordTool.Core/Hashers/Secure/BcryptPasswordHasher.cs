using PasswordTool.Core.Options;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class BcryptPasswordHasher : NotImplementedPasswordHasher
{
    public BcryptPasswordHasher(BcryptOptions options)
    {
        Options = options;
    }

    public override string AlgorithmName => PasswordHasherNames.Bcrypt;

    public BcryptOptions Options { get; }
}
