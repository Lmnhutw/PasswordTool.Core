using PasswordTool.Core.Options;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class Pbkdf2Sha512PasswordHasher : NotImplementedPasswordHasher
{
    public Pbkdf2Sha512PasswordHasher(Pbkdf2Options options)
    {
        Options = options;
    }

    public override string AlgorithmName => PasswordHasherNames.Pbkdf2Sha512;

    public Pbkdf2Options Options { get; }
}
