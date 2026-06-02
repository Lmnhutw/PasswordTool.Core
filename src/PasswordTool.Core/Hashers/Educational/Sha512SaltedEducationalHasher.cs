namespace PasswordTool.Core.Hashers.Educational;

public sealed class Sha512SaltedEducationalHasher : NotImplementedPasswordHasher
{
    public override string AlgorithmName => PasswordHasherNames.Sha512Salted;
}
