using PasswordTool.Core.Options;
using System.Security.Cryptography;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class Pbkdf2Sha256PasswordHasher : Pbkdf2PasswordHasherBase
{
    public Pbkdf2Sha256PasswordHasher(Pbkdf2Options options) : base(options, HashAlgorithmName.SHA256, "PBKDF2-SHA256")
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Pbkdf2Sha256;
}
