using PasswordTool.Core.Options;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class ScryptPasswordHasher : NotImplementedPasswordHasher
{
    public ScryptPasswordHasher(ScryptOptions options)
    {
        Options = options;
    }

    public override string AlgorithmName => PasswordHasherNames.Scrypt;

    public ScryptOptions Options { get; }
}
