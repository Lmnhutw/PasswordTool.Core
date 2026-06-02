using PasswordTool.Core.Options;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class Argon2idPasswordHasher : NotImplementedPasswordHasher
{
    public Argon2idPasswordHasher(Argon2idOptions options)
    {
        Options = options;
    }

    public override string AlgorithmName => PasswordHasherNames.Argon2id;

    public Argon2idOptions Options { get; }
}
