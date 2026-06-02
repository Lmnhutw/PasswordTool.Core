using PasswordTool.Core.Options;
using System.Security.Cryptography;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class Pbkdf2Sha512PasswordHasher : Pbkdf2PasswordHasherBase
{
    public Pbkdf2Sha512PasswordHasher(Pbkdf2Options options) : base(options, HashAlgorithmName.SHA512, "PBKDF2-SHA512")
    {
    }

    public override string AlgorithmName => PasswordHasherNames.Pbkdf2Sha512;
}
