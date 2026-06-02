namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha256SaltedEducationalHasher : NotImplementedPasswordHasher
{
    public override string AlgorithmName => PasswordHasherNames.Sha256Salted;
}
