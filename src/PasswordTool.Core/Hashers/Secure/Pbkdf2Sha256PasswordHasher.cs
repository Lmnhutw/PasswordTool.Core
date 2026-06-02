using PasswordTool.Core.Options;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class Pbkdf2Sha256PasswordHasher : NotImplementedPasswordHasher
{
    public Pbkdf2Sha256PasswordHasher(Pbkdf2Options options)
    {
        Options = options;
    }

    public override string AlgorithmName => PasswordHasherNames.Pbkdf2Sha256;

    public Pbkdf2Options Options { get; }
}
